using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FootballActionCoordinator
{
    private readonly FootballActionSelectionConfiguration _configuration;
    private readonly FootballActionEvaluator _evaluator;
    private readonly IReadOnlyList<IFootballActionCandidateGenerator> _generators;
    private readonly FootballActionDiagnostics _diagnostics = new();
    private string _committedActionKey = string.Empty;
    private string _committedActorId = string.Empty;
    private int _remainingCommitmentDecisions;
    private float _lastPressureDistanceMeters = float.PositiveInfinity;

    public FootballActionCoordinator(FootballActionSelectionConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _evaluator = new FootballActionEvaluator(configuration);
        _generators = new IFootballActionCandidateGenerator[]
        {
            new PassCandidateGenerator(),
            new CarryCandidateGenerator(),
            new ShotCandidateGenerator(configuration),
            new CrossCandidateGenerator(),
            new ClearanceCandidateGenerator(configuration),
            new GoalkeeperActionCandidateGenerator()
        };
    }

    public FootballActionMetricsSnapshot Metrics => _diagnostics.CreateSnapshot();

    public void Reset()
    {
        _committedActionKey = string.Empty;
        _committedActorId = string.Empty;
        _remainingCommitmentDecisions = 0;
        _lastPressureDistanceMeters = float.PositiveInfinity;
        _diagnostics.Reset();
    }

    public FootballActionDecision Decide(FootballActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<FootballActionCandidate> generated = new();
        foreach (IFootballActionCandidateGenerator generator in _generators)
        {
            generator.Generate(context, generated);
        }

        List<FootballActionCandidate> evaluated = generated
            .Where(candidate => candidate.IsLegal)
            .Select(candidate => _evaluator.Evaluate(context, candidate))
            .OrderByDescending(candidate => candidate.Score.Total)
            .ThenBy(candidate => candidate.StableKey, StringComparer.Ordinal)
            .ToList();
        bool noValidAction = evaluated.All(candidate => candidate.ActionType == FootballActionType.Hold);
        if (evaluated.Count == 0)
        {
            throw new InvalidOperationException("At least the Hold fallback candidate must be generated.");
        }

        FootballActionCandidate selected = evaluated[0];
        bool cancelledCommitment = ShouldCancelCommitment(context, evaluated);
        bool usedCommitment = false;
        if (!cancelledCommitment && TryApplyCommitment(context, evaluated, selected, out FootballActionCandidate retained))
        {
            selected = retained;
            usedCommitment = true;
        }

        int nonFallbackCount = evaluated.Count(candidate => candidate.ActionType != FootballActionType.Hold);
        bool wasForced = nonFallbackCount <= 1;
        float runnerUpScore = evaluated
            .Where(candidate => candidate.StableKey != selected.StableKey)
            .Select(candidate => candidate.Score.Total)
            .DefaultIfEmpty(selected.Score.Total)
            .Max();
        float margin = Math.Max(0f, selected.Score.Total - runnerUpScore);
        List<RejectedFootballActionSummary> rejected = evaluated
            .Where(candidate => candidate.StableKey != selected.StableKey)
            .Take(_configuration.MaximumRejectedDiagnostics)
            .Select(candidate => new RejectedFootballActionSummary(
                candidate.ActionType,
                candidate.TargetPlayerId.ToString(),
                candidate.Score.Total,
                candidate.SourceReason))
            .ToList();
        string reason = usedCommitment
            ? $"commit_{selected.ActionType.ToString().ToLowerInvariant()}"
            : $"score_{selected.ActionType.ToString().ToLowerInvariant()}";
        FootballActionDecision decision = new(
            selected,
            selected.Score.DeterministicVariation,
            margin,
            rejected,
            reason,
            usedCommitment,
            cancelledCommitment,
            wasForced);
        RememberDecision(context, selected);
        _diagnostics.Record(decision, noValidAction);
        return decision;
    }

    private bool ShouldCancelCommitment(
        FootballActionContext context,
        IReadOnlyCollection<FootballActionCandidate> candidates)
    {
        if (_remainingCommitmentDecisions <= 0 ||
            _committedActorId != context.ActorId.ToString())
        {
            return false;
        }

        bool candidateDisappeared = candidates.All(candidate => candidate.CommitmentKey != _committedActionKey);
        bool pressureChanged = float.IsFinite(_lastPressureDistanceMeters) &&
                               float.IsFinite(context.PressureDistanceMeters) &&
                               Math.Abs(context.PressureDistanceMeters - _lastPressureDistanceMeters) >=
                               _configuration.CommitmentCancellationPressureDeltaMeters;
        if (!candidateDisappeared && !pressureChanged)
        {
            return false;
        }

        _remainingCommitmentDecisions = 0;
        _committedActionKey = string.Empty;
        return true;
    }

    private bool TryApplyCommitment(
        FootballActionContext context,
        IReadOnlyCollection<FootballActionCandidate> candidates,
        FootballActionCandidate best,
        out FootballActionCandidate retained)
    {
        retained = best;
        if (_remainingCommitmentDecisions <= 0 ||
            _committedActorId != context.ActorId.ToString())
        {
            return false;
        }

        FootballActionCandidate? existing = candidates.FirstOrDefault(candidate =>
            candidate.CommitmentKey == _committedActionKey);
        if (existing is null)
        {
            return false;
        }

        FootballActionCandidate committed = _evaluator.AddCommitment(existing);
        if (committed.Score.Total + 0.000001f < best.Score.Total)
        {
            return false;
        }
        retained = committed;
        _remainingCommitmentDecisions--;
        return true;
    }

    private void RememberDecision(FootballActionContext context, FootballActionCandidate selected)
    {
        bool changed = _committedActorId != context.ActorId.ToString() ||
                       _committedActionKey != selected.CommitmentKey;
        _committedActorId = context.ActorId.ToString();
        _committedActionKey = selected.CommitmentKey;
        _lastPressureDistanceMeters = context.PressureDistanceMeters;
        if (changed)
        {
            _remainingCommitmentDecisions = selected.ActionType == FootballActionType.Hold
                ? 0
                : _configuration.CommitmentDecisionCount;
        }
    }
}
