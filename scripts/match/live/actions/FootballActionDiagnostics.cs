using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class FootballActionMetricsSnapshot
{
    public FootballActionMetricsSnapshot(
        IReadOnlyDictionary<FootballActionType, int> attemptsByType,
        IReadOnlyDictionary<string, int> reasons,
        int decisions,
        float totalScoreMargin,
        int backwardPasses,
        int sidewaysPasses,
        int forwardPasses,
        int progressiveActions,
        int forcedActions,
        int decisionCancellations,
        int noValidActions)
    {
        AttemptsByType = new ReadOnlyDictionary<FootballActionType, int>(
            new Dictionary<FootballActionType, int>(attemptsByType));
        Reasons = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(reasons));
        Decisions = decisions;
        TotalScoreMargin = totalScoreMargin;
        BackwardPasses = backwardPasses;
        SidewaysPasses = sidewaysPasses;
        ForwardPasses = forwardPasses;
        ProgressiveActions = progressiveActions;
        ForcedActions = forcedActions;
        DecisionCancellations = decisionCancellations;
        NoValidActions = noValidActions;
    }

    public IReadOnlyDictionary<FootballActionType, int> AttemptsByType { get; }
    public IReadOnlyDictionary<string, int> Reasons { get; }
    public int Decisions { get; }
    public float TotalScoreMargin { get; }
    public float AverageScoreMargin => Decisions == 0 ? 0f : TotalScoreMargin / Decisions;
    public int BackwardPasses { get; }
    public int SidewaysPasses { get; }
    public int ForwardPasses { get; }
    public int ProgressiveActions { get; }
    public float ProgressiveActionRate => Decisions == 0 ? 0f : (float)ProgressiveActions / Decisions;
    public int ForcedActions { get; }
    public int DecisionCancellations { get; }
    public int NoValidActions { get; }
}

public sealed class FootballActionDiagnostics
{
    private readonly Dictionary<FootballActionType, int> _attemptsByType = new();
    private readonly Dictionary<string, int> _reasons = new(StringComparer.Ordinal);
    private int _decisions;
    private float _totalScoreMargin;
    private int _backwardPasses;
    private int _sidewaysPasses;
    private int _forwardPasses;
    private int _progressiveActions;
    private int _forcedActions;
    private int _decisionCancellations;
    private int _noValidActions;

    public void Reset()
    {
        _attemptsByType.Clear();
        _reasons.Clear();
        _decisions = 0;
        _totalScoreMargin = 0f;
        _backwardPasses = 0;
        _sidewaysPasses = 0;
        _forwardPasses = 0;
        _progressiveActions = 0;
        _forcedActions = 0;
        _decisionCancellations = 0;
        _noValidActions = 0;
    }

    public void Record(FootballActionDecision decision, bool noValidAction)
    {
        ArgumentNullException.ThrowIfNull(decision);
        FootballActionCandidate selected = decision.Selected;
        _attemptsByType[selected.ActionType] = _attemptsByType.GetValueOrDefault(selected.ActionType) + 1;
        _reasons[decision.Reason] = _reasons.GetValueOrDefault(decision.Reason) + 1;
        _decisions++;
        _totalScoreMargin += decision.ScoreMargin;
        if (decision.WasForced)
        {
            _forcedActions++;
        }
        if (decision.CancelledCommitment)
        {
            _decisionCancellations++;
        }
        if (noValidAction)
        {
            _noValidActions++;
        }
        if (selected.ActionType is FootballActionType.GroundPass or FootballActionType.ThroughBall or
            FootballActionType.LoftedPass or FootballActionType.Cross or
            FootballActionType.GoalkeeperDistribution)
        {
            if (selected.ExpectedProgressionMeters > 1.5f)
            {
                _forwardPasses++;
            }
            else if (selected.ExpectedProgressionMeters < -1.5f)
            {
                _backwardPasses++;
            }
            else
            {
                _sidewaysPasses++;
            }
        }
        if (selected.ExpectedProgressionMeters >= 8f || selected.ActionType == FootballActionType.Shot)
        {
            _progressiveActions++;
        }
    }

    public FootballActionMetricsSnapshot CreateSnapshot()
    {
        return new FootballActionMetricsSnapshot(
            _attemptsByType,
            _reasons,
            _decisions,
            _totalScoreMargin,
            _backwardPasses,
            _sidewaysPasses,
            _forwardPasses,
            _progressiveActions,
            _forcedActions,
            _decisionCancellations,
            _noValidActions);
    }
}
