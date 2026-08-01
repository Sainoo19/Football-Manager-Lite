using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class TeamPhaseCoordinator
{
    private sealed class MutableTeamPhaseState
    {
        public MutableTeamPhaseState(StringName teamId, LiveTeamPhase phase, float enteredAt, bool hasPossession)
        {
            TeamId = teamId;
            Phase = phase;
            EnteredAt = enteredAt;
            HadPossession = hasPossession;
        }

        public StringName TeamId { get; }
        public LiveTeamPhase Phase { get; set; }
        public float EnteredAt { get; set; }
        public bool HadPossession { get; set; }
        public int RequiredRestDefencePlayers { get; set; }
        public bool CounterShotRecorded { get; set; }
    }

    private readonly TeamPhaseConfiguration _configuration;
    private readonly Dictionary<StringName, MutableTeamPhaseState> _states = new();
    private readonly Dictionary<LiveTeamPhase, float> _completedDurations = new();
    private readonly Dictionary<string, int> _transitionCounts = new(StringComparer.Ordinal);
    private int _counterAttacks;
    private int _counterAttackShots;
    private float _totalOrganizationSeconds;
    private int _organizationSamples;
    private int _finalThirdRestDefenceObservations;
    private int _finalThirdRestDefencePlayerTotal;
    private int _emergencyDefenceEntries;

    public TeamPhaseCoordinator(TeamPhaseConfiguration? configuration = null)
    {
        _configuration = configuration ?? TeamPhaseConfiguration.CreateM2Defaults();
    }

    public void Reset(
        IReadOnlyCollection<StringName> teamIds,
        StringName possessionTeamId,
        float gameTimeSeconds = 0f)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        _states.Clear();
        _completedDurations.Clear();
        _transitionCounts.Clear();
        _counterAttacks = 0;
        _counterAttackShots = 0;
        _totalOrganizationSeconds = 0f;
        _organizationSamples = 0;
        _finalThirdRestDefenceObservations = 0;
        _finalThirdRestDefencePlayerTotal = 0;
        _emergencyDefenceEntries = 0;

        foreach (StringName teamId in teamIds)
        {
            bool hasPossession = teamId == possessionTeamId;
            LiveTeamPhase phase = hasPossession
                ? LiveTeamPhase.BuildUp
                : LiveTeamPhase.DefensiveBlock;
            _states[teamId] = new MutableTeamPhaseState(teamId, phase, gameTimeSeconds, hasPossession)
            {
                RequiredRestDefencePlayers = hasPossession
                    ? Math.Max(2, _configuration.MinimumRestDefencePlayers - 1)
                    : 0
            };
        }
    }

    public TeamPhaseState Update(TeamPhaseContext context)
    {
        if (context.TeamId == new StringName())
        {
            throw new ArgumentException("A team phase context requires a team id.", nameof(context));
        }

        if (!_states.TryGetValue(context.TeamId, out MutableTeamPhaseState? state))
        {
            state = new MutableTeamPhaseState(
                context.TeamId,
                context.HasPossession ? PositionalPossessionPhase(context, LiveTeamPhase.Progression) :
                    LiveTeamPhase.DefensiveBlock,
                context.GameTimeSeconds,
                context.HasPossession);
            _states.Add(context.TeamId, state);
        }

        if (context.IsPossessionContested && !context.IsRestart)
        {
            return Snapshot(state);
        }

        bool wonPossession = context.HasPossession && !state.HadPossession;
        bool lostPossession = !context.HasPossession && state.HadPossession;
        LiveTeamPhase desired = DesiredPhase(context, state, wonPossession, lostPossession);
        if (desired != state.Phase && CanTransition(context, state, desired, wonPossession, lostPossession))
        {
            Transition(state, desired, context.GameTimeSeconds);
        }

        state.HadPossession = context.HasPossession;
        state.RequiredRestDefencePlayers = RequiredRestDefencePlayers(context, state.Phase);
        return Snapshot(state);
    }

    public LiveTeamPhase PhaseFor(StringName teamId)
    {
        return _states.TryGetValue(teamId, out MutableTeamPhaseState? state)
            ? state.Phase
            : LiveTeamPhase.DefensiveBlock;
    }

    public IReadOnlyDictionary<StringName, TeamPhaseState> CreateStateSnapshot()
    {
        Dictionary<StringName, TeamPhaseState> snapshot = new();
        foreach ((StringName teamId, MutableTeamPhaseState state) in _states)
        {
            snapshot[teamId] = Snapshot(state);
        }
        return new ReadOnlyDictionary<StringName, TeamPhaseState>(snapshot);
    }

    public void RecordShot(StringName teamId)
    {
        if (_states.TryGetValue(teamId, out MutableTeamPhaseState? state) &&
            state.Phase == LiveTeamPhase.CounterAttack &&
            !state.CounterShotRecorded)
        {
            _counterAttackShots++;
            state.CounterShotRecorded = true;
        }
    }

    public void ObserveRestDefence(StringName teamId, int playerCount)
    {
        if (PhaseFor(teamId) != LiveTeamPhase.FinalThird)
        {
            return;
        }
        _finalThirdRestDefenceObservations++;
        _finalThirdRestDefencePlayerTotal += Math.Max(playerCount, 0);
    }

    public TeamPhaseMetricsSnapshot CreateMetricsSnapshot(float gameTimeSeconds)
    {
        Dictionary<LiveTeamPhase, float> durations = new(_completedDurations);
        foreach (MutableTeamPhaseState state in _states.Values)
        {
            durations[state.Phase] = durations.GetValueOrDefault(state.Phase) +
                                     Math.Max(gameTimeSeconds - state.EnteredAt, 0f);
        }
        return new TeamPhaseMetricsSnapshot(
            durations,
            _transitionCounts,
            _counterAttacks,
            _counterAttackShots,
            _totalOrganizationSeconds,
            _organizationSamples,
            _finalThirdRestDefenceObservations,
            _finalThirdRestDefencePlayerTotal,
            _emergencyDefenceEntries);
    }

    public float PlanningInterval(
        LiveMatchEngineConfiguration configuration,
        bool isLooseBall,
        bool isBallInFlight)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return isLooseBall
            ? configuration.LooseBallPlanningIntervalSeconds
            : isBallInFlight
                ? configuration.BallInFlightPlanningIntervalSeconds
                : configuration.PossessionIntentPlanningIntervalSeconds;
    }

    private LiveTeamPhase DesiredPhase(
        TeamPhaseContext context,
        MutableTeamPhaseState state,
        bool wonPossession,
        bool lostPossession)
    {
        if (context.IsRestart)
        {
            return LiveTeamPhase.SetPiece;
        }
        if (wonPossession)
        {
            return IsCounterAttackAvailable(context)
                ? LiveTeamPhase.CounterAttack
                : LiveTeamPhase.TransitionToAttack;
        }
        if (lostPossession)
        {
            return IsEmergencyDefence(context)
                ? LiveTeamPhase.EmergencyDefence
                : LiveTeamPhase.TransitionToDefence;
        }
        if (context.HasPossession)
        {
            if (state.Phase == LiveTeamPhase.CounterAttack &&
                context.GameTimeSeconds - state.EnteredAt < _configuration.MaximumCounterAttackDurationSeconds &&
                IsCounterAttackAvailable(context))
            {
                return LiveTeamPhase.CounterAttack;
            }
            if (state.Phase == LiveTeamPhase.TransitionToAttack &&
                context.GameTimeSeconds - state.EnteredAt < _configuration.TransitionOrganizationSeconds)
            {
                return LiveTeamPhase.TransitionToAttack;
            }
            return PositionalPossessionPhase(context, state.Phase);
        }

        if (IsEmergencyDefence(context))
        {
            return LiveTeamPhase.EmergencyDefence;
        }
        if (state.Phase == LiveTeamPhase.TransitionToDefence &&
            context.GameTimeSeconds - state.EnteredAt < _configuration.TransitionOrganizationSeconds)
        {
            return LiveTeamPhase.TransitionToDefence;
        }
        return LiveTeamPhase.DefensiveBlock;
    }

    private bool CanTransition(
        TeamPhaseContext context,
        MutableTeamPhaseState state,
        LiveTeamPhase desired,
        bool wonPossession,
        bool lostPossession)
    {
        if (wonPossession || lostPossession || desired == LiveTeamPhase.SetPiece || state.Phase == LiveTeamPhase.SetPiece)
        {
            return true;
        }
        if (desired == LiveTeamPhase.EmergencyDefence)
        {
            return true;
        }
        return context.GameTimeSeconds - state.EnteredAt >= _configuration.MinimumPhaseDurationSeconds;
    }

    private LiveTeamPhase PositionalPossessionPhase(TeamPhaseContext context, LiveTeamPhase currentPhase)
    {
        if (currentPhase == LiveTeamPhase.FinalThird &&
            context.AttackProgress >= _configuration.FinalThirdExitProgress)
        {
            return LiveTeamPhase.FinalThird;
        }
        if (context.AttackProgress >= _configuration.FinalThirdEnterProgress)
        {
            return LiveTeamPhase.FinalThird;
        }
        if (currentPhase == LiveTeamPhase.BuildUp &&
            context.AttackProgress <= _configuration.BuildUpExitProgress)
        {
            return LiveTeamPhase.BuildUp;
        }
        return context.AttackProgress <= _configuration.BuildUpEnterProgress
            ? LiveTeamPhase.BuildUp
            : LiveTeamPhase.Progression;
    }

    private bool IsCounterAttackAvailable(TeamPhaseContext context)
    {
        return context.AttackProgress is >= 0.16f and <= 0.82f &&
               context.ForwardSpaceMeters >= _configuration.MinimumCounterForwardSpaceMeters &&
               context.PlayersAheadOfBall >= _configuration.MinimumCounterPlayersAhead &&
               context.RegionalNumericalAdvantage >= 0;
    }

    private bool IsEmergencyDefence(TeamPhaseContext context)
    {
        return context.GoalDistanceMeters <= _configuration.EmergencyDefenceGoalDistanceMeters ||
               context.AttackProgress <= 0.20f;
    }

    private int RequiredRestDefencePlayers(TeamPhaseContext context, LiveTeamPhase phase)
    {
        if (!context.HasPossession)
        {
            return 0;
        }
        if (phase is LiveTeamPhase.FinalThird or LiveTeamPhase.CounterAttack)
        {
            return _configuration.MinimumRestDefencePlayers;
        }
        return Math.Max(2, _configuration.MinimumRestDefencePlayers - 1);
    }

    private void Transition(MutableTeamPhaseState state, LiveTeamPhase next, float gameTimeSeconds)
    {
        float elapsed = Math.Max(gameTimeSeconds - state.EnteredAt, 0f);
        _completedDurations[state.Phase] = _completedDurations.GetValueOrDefault(state.Phase) + elapsed;
        string transitionKey = $"{state.Phase}->{next}";
        _transitionCounts[transitionKey] = _transitionCounts.GetValueOrDefault(transitionKey) + 1;
        if (next == LiveTeamPhase.CounterAttack)
        {
            _counterAttacks++;
            state.CounterShotRecorded = false;
        }
        if (next == LiveTeamPhase.EmergencyDefence)
        {
            _emergencyDefenceEntries++;
        }
        if (state.Phase == LiveTeamPhase.TransitionToDefence &&
            next is LiveTeamPhase.DefensiveBlock or LiveTeamPhase.EmergencyDefence)
        {
            _totalOrganizationSeconds += elapsed;
            _organizationSamples++;
        }
        state.Phase = next;
        state.EnteredAt = gameTimeSeconds;
    }

    private static TeamPhaseState Snapshot(MutableTeamPhaseState state)
    {
        return new TeamPhaseState(
            state.TeamId,
            state.Phase,
            state.EnteredAt,
            state.HadPossession,
            state.RequiredRestDefencePlayers);
    }
}
