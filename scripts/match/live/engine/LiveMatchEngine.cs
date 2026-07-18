using System.Collections.Generic;
using Godot;
using Godot.Collections;
using DotNetDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.Vector2>;
using TeamDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.StringName>;
using RoleDictionary = System.Collections.Generic.Dictionary<Godot.StringName, string>;
using IntentDictionary = System.Collections.Generic.Dictionary<Godot.StringName, PlayerIntent>;
using PaceDictionary = System.Collections.Generic.Dictionary<Godot.StringName, int>;
using NumberDictionary = System.Collections.Generic.Dictionary<Godot.StringName, int>;

public sealed partial class LiveMatchEngine
{
    public event System.Action<string>? ActionChanged;
    public event System.Action<FootballMatchEvent>? LiveMatchEvent;

    private enum BallActionKind
    {
        None,
        Pass,
        ThroughBall,
        LoftedPass,
        Cross,
        Shot,
        Clearance,
        HeaderPass,
        HeaderClearance
    }

    private readonly LiveMatchState _state = new();
    public FootballMatchSimulation? Simulation
    {
        get => _state.Simulation;
        private set => _state.Simulation = value;
    }
    internal DotNetDictionary BasePositions => _state.BasePositions;
    internal DotNetDictionary CurrentPositions => _state.CurrentPositions;
    internal DotNetDictionary TargetPositions => _state.TargetPositions;
    private readonly TeamDictionary _playerTeams;
    private readonly RoleDictionary _playerRoles;
    private readonly TeamDictionary _playerSlotIds;
    private readonly PaceDictionary _playerPaces;
    private readonly NumberDictionary _playerNumbers;
    private readonly FootballIntentPlanner _intentPlanner = new();
    private readonly FootballMovementController _movementController = new();
    private readonly MatchSideController _sideController = new();
    private readonly OffsideRule _offsideRule = new();
    private readonly PassTrajectoryPlanner _passTrajectoryPlanner = new();
    private readonly PassExecutionResolver _passExecutionResolver = new();
    private readonly FirstTouchResolver _firstTouchResolver;
    private readonly RollingBallPhysics _rollingBallPhysics = new();
    private readonly ShotDecisionEvaluator _shotDecisionEvaluator;
    private readonly ShotOutcomeResolver _shotOutcomeResolver;
    private readonly ShotTargetPlanner _shotTargetPlanner = new();
    private readonly TraditionalGoalkeeperPlanner _traditionalGoalkeeperPlanner = new();
    private readonly DirectAttackContinuationPlanner _directAttackContinuationPlanner = new();
    private readonly GoalKickRestartPlanner _goalKickRestartPlanner = new();
    private readonly KickoffRestartPlanner _kickoffRestartPlanner = new();
    private readonly FreeKickRestartPlanner _freeKickRestartPlanner = new();
    private readonly PenaltyAreaRule _penaltyAreaRule = new();
    private readonly PenaltyRestartPlanner _penaltyRestartPlanner = new();
    private readonly PenaltyKickResolver _penaltyKickResolver = new();
    private readonly AdvantageRuleEvaluator _advantageRuleEvaluator = new();
    private readonly DecisionVarietyTracker _decisionVarietyTracker = new();
    private readonly FinalThirdDecisionPlanner _finalThirdDecisionPlanner = new();
    private readonly MatchScenarioFactory _matchScenarioFactory = new();
    private readonly ThroughBallTargetPlanner _throughBallTargetPlanner = new();
    private readonly PassOptionEvaluator _passOptionEvaluator = new();
    private readonly BallCarrierDecisionEvaluator _ballCarrierDecisionEvaluator;
    private readonly ClearanceTargetPlanner _clearanceTargetPlanner = new();
    private readonly DuelDistanceRules _duelDistanceRules = new();
    private readonly DribbleTouchPlanner _dribbleTouchPlanner = new();
    private readonly DefenderEngagementPlanner _defenderEngagementPlanner = new();
    private readonly GroundDuelResolver _groundDuelResolver;
    private readonly AerialBallTrajectoryPlanner _aerialBallTrajectoryPlanner = new();
    private readonly AerialLandingPredictor _aerialLandingPredictor = new();
    private readonly AerialDuelResolver _aerialDuelResolver;
    private readonly LiveMatchEngineConfiguration _configuration;
    private readonly IntentDictionary _playerIntents = new();
    private readonly HashSet<StringName> _interceptionAttemptedBy = new();
    private LiveMatchRuntime _runtime = new();
    private bool _usesExternalRuntime;

    public Vector2 BallPosition
    {
        get => _state.BallPosition;
        private set => _state.BallPosition = value;
    }
    private float _lastPassTime = -10;
    private float _attackProgress = 0.22f;
    private float _phaseLane = 0.5f;
    private int _phaseSerial;
    private StringName _primaryRunnerId = new();
    private StringName _secondaryRunnerId = new();
    private StringName _pressingPlayerId = new();
    private BallActionKind _ballActionKind;
    private StringName _actionSourceId = new();
    private StringName _actionSourceTeamId = new();
    private float _nextDecisionTime;
    private int _decisionSerial;
    private uint _liveDecisionSeed;
    private int _decisionsSinceShot;
    private StringName _carryOwnerId = new();
    private int _consecutiveCarries;
    private StringName _pendingShotOutcome = new();
    private StringName _pendingShotShooterId = new();
    private StringName _pendingShotGoalkeeperId = new();
    private StringName _pendingShotBlockerId = new();
    private StringName _pendingOffsideReceiverId = new();
    private StringName _directAttackOwnerId = new();
    private int _directAttackActionsRemaining;
    private FreeKickRestartPlan _freeKickRestartPlan;
    private PenaltyRestartPlan _penaltyRestartPlan;
    private float _nextIntentPlanTime;
    private bool _kickoffPassPending;
    private StringName _kickoffReceiverId = new();
    private StringName _possessionTeamId = new();
    private StringName _lastCompletedPossessionTeamId = new();
    private float _possessionSpellStartTime;
    private float _totalPossessionSpellSeconds;
    private int _possessionSpellCount;
    private double _synchronizedGameSeconds;
    private double _fixedStepAccumulatorSeconds;
    private readonly List<LiveGoalRecord> _goalRecords = new();
    private float _pendingShotDistanceMeters;
    private StringName _pendingShotSituation = new();

    public string LastActionName
    {
        get => _state.LastActionName;
        private set => _state.LastActionName = value;
    }
    public int CompletedPasses { get; private set; }
    public int Interceptions { get; private set; }
    public int Dribbles { get; private set; }
    public int FoulsCommitted { get; private set; }
    public int Restarts { get; private set; }
    public int LooseBallRecoveries { get; private set; }
    public int Clearances { get; private set; }
    public int PassAttempts { get; private set; }
    public int FirstTouchErrors { get; private set; }
    public int GroundDuelExchanges { get; private set; }
    public int TackleAttempts { get; private set; }
    public int ShoulderChallenges { get; private set; }
    public int GroundDuelLooseBalls { get; private set; }
    public int MaxGroundDuelTouches { get; private set; }
    public int CarrierEscapes { get; private set; }
    public int TacklesWon { get; private set; }
    public int AerialDuels { get; private set; }
    public int HeadersWon { get; private set; }
    public int DefensiveHeaders { get; private set; }
    public int HeaderShots { get; private set; }
    public int GoalkeeperAerialCatches { get; private set; }
    public int GoalkeeperPunches { get; private set; }
    public int AerialSecondBalls { get; private set; }
    public int PossessionChanges { get; private set; }
    public int KickoffsTaken { get; private set; }
    public int CornersTaken { get; private set; }
    public int GoalKicksTaken { get; private set; }
    public int ThrowInsTaken { get; private set; }
    public int FreeKicksTaken { get; private set; }
    public int PenaltiesTaken { get; private set; }
    public float MinimumObservedGroundDuelSeparationMeters { get; private set; } = float.PositiveInfinity;
    public bool IsKickoffPassPending => _kickoffPassPending;
    public StringName KickoffReceiverId => _kickoffReceiverId;
    public bool IsQuickFreeKick => _state.IsRestartPending && _state.RestartType == "free_kick" && _freeKickRestartPlan.IsQuick;
    public bool IsPenaltyRestart => _state.IsRestartPending && _state.RestartType == "penalty";
    public int PendingCardActionCount => _state.PendingCardActions.Count;
    public StringName CurrentBallOwnerId => _state.BallOwnerId;
    public IReadOnlyDictionary<StringName, PlayerIntent> CurrentIntents => _playerIntents;
    public bool IsPlaying
    {
        get => _state.IsPlaying;
        private set => _state.IsPlaying = value;
    }
    public LiveMatchPhase LivePhase => _runtime.Phase;
    public double ElapsedGameSeconds => _runtime.ElapsedGameSeconds;
    public bool AreSidesSwitched => _sideController.AreSidesSwitched;
    public StringName PendingRestartType => _state.IsRestartPending ? _state.RestartType : new StringName();
    public bool IsBallInFlight => _ballActionActive;
    public bool IsLooseBall => _state.IsLooseBallActive;
    public bool IsBallVisible => _state.IsBallVisible;
    public bool IsRestartBallPlaced => _state.IsRestartBallPlaced;
    public Vector2 LooseBallVelocityMetersPerSecond => _state.LooseBallVelocityMetersPerSecond;
    public Vector2 BallFlightStart => _ballActionFrom;
    public Vector2 BallFlightTarget => _ballActionTo;
    public StringName BallActionSourceTeamId => _actionSourceTeamId;
    public string BallActionType => _ballActionKind.ToString();
    public bool IsAerialBall => _aerialFlightActive;
    public float BallHeightMeters => _ballVisualHeight;
    public float BallVerticalVelocityMetersPerSecond => _ballVerticalVelocityMetersPerSecond;
    public MatchScenarioKind? ActiveScenario { get; private set; }
    public DribbleTouchType? ActiveDribbleTouch => _state.GroundDuel.HasCarrier
        ? _state.GroundDuel.CurrentTouch.Type
        : null;

    private bool _ballActionActive;
    private Vector2 _ballActionFrom = new(0.5f, 0.5f);
    private Vector2 _ballActionTo = new(0.5f, 0.5f);
    private float _ballActionElapsed;
    private float _ballActionDuration = 0.65f;
    private float _ballActionArc;
    private float _ballVisualHeight;
    private float _ballVerticalVelocityMetersPerSecond;
    private bool _aerialFlightActive;
    private AerialBallTrajectory _aerialTrajectory;
    private readonly HashSet<StringName> _aerialContenderIds = new();
    private StringName _ballNextOwnerId = new();
    private LivePassType _pendingPassType = LivePassType.Standard;
    private float _pendingPassSpeedMetersPerSecond;
    private float _lastPassExecutionQuality = 1f;
    private Vector2 _lastPassIntendedTarget;
    private Vector2 _lastPassActualTarget;

    public float LastPassExecutionQuality => _lastPassExecutionQuality;
    public Vector2 LastPassIntendedTarget => _lastPassIntendedTarget;
    public Vector2 LastPassActualTarget => _lastPassActualTarget;
    public IReadOnlyDictionary<StringName, Vector2> PositionView => _state.CurrentPositionView;
    public IReadOnlyDictionary<StringName, Vector2> BasePositionView => _state.BasePositionView;
    public IReadOnlyDictionary<StringName, Vector2> TargetPositionView => _state.TargetPositionView;
    internal float FixedStepInterpolationAlpha => (float)System.Math.Clamp(
        _fixedStepAccumulatorSeconds / _configuration.FixedStepSeconds,
        0d,
        1d);
    internal IReadOnlyDictionary<StringName, StringName> PlayerTeams => _state.PlayerTeamView;
    internal IReadOnlyDictionary<StringName, string> PlayerRoles => _state.PlayerRoleView;
    internal IReadOnlyDictionary<StringName, int> PlayerNumbers => _state.PlayerNumberView;
    internal float BallVisualHeight => _ballVisualHeight;

    public LiveMatchEngine()
        : this(LiveMatchEngineConfiguration.CreateFootballFundamentalsV1())
    {
    }

    public LiveMatchEngine(LiveMatchEngineConfiguration configuration)
    {
        _configuration = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
        _firstTouchResolver = new FirstTouchResolver(configuration.FirstTouchControlChanceBonus);
        _shotOutcomeResolver = new ShotOutcomeResolver(
            configuration.ShotGoalProbabilityMultiplier,
            configuration.ParriedShotCornerProbability);
        _shotDecisionEvaluator = new ShotDecisionEvaluator(configuration.ShotAttemptProbabilityMultiplier);
        _groundDuelResolver = new GroundDuelResolver(configuration.GroundDuelFoulProbabilityMultiplier);
        _aerialDuelResolver = new AerialDuelResolver(configuration.HeaderShotProbability);
        _ballCarrierDecisionEvaluator = new BallCarrierDecisionEvaluator(
            configuration.UnderPressureDribbleProbability);
        _playerTeams = _state.PlayerTeams;
        _playerRoles = _state.PlayerRoles;
        _playerSlotIds = _state.PlayerSlotIds;
        _playerPaces = _state.PlayerPaces;
        _playerNumbers = _state.PlayerNumbers;
    }

    public bool Execute(LiveMatchCommand command)
    {
        switch (command.Kind)
        {
            case LiveMatchCommandKind.Play:
                SetPlaying(true);
                return IsPlaying;
            case LiveMatchCommandKind.Pause:
                SetPlaying(false);
                return !IsPlaying;
            case LiveMatchCommandKind.StartScenario when command.Scenario.HasValue:
                return StartScenario(command.Scenario.Value);
            default:
                return false;
        }
    }

    internal bool OverridePlayerPosition(StringName playerId, Vector2 position)
    {
        if (!CurrentPositions.ContainsKey(playerId))
        {
            return false;
        }

        Vector2 clampedPosition = ClampToPitch(position);
        CurrentPositions[playerId] = clampedPosition;
        TargetPositions[playerId] = clampedPosition;
        return true;
    }

    public LiveMatchSnapshot GetSnapshot()
    {
        return new LiveMatchSnapshot(
            CurrentPositions,
            TargetPositions,
            BallPosition,
            _state.BallOwnerId,
            _state.ActiveTeamId,
            _runtime.Phase,
            _runtime.ElapsedGameSeconds,
            LastActionName,
            IsPlaying,
            _ballActionActive,
            _state.IsLooseBallActive,
            _state.IsRestartPending ? _state.RestartType : new StringName(),
            new LiveMatchMetrics(
                CompletedPasses,
                PassAttempts,
                Interceptions,
                Dribbles,
                FoulsCommitted,
                Restarts,
                LooseBallRecoveries,
                Clearances,
                FirstTouchErrors,
                GroundDuelExchanges,
                TackleAttempts,
                ShoulderChallenges,
                GroundDuelLooseBalls,
                MaxGroundDuelTouches,
                CarrierEscapes,
                TacklesWon,
                MinimumObservedGroundDuelSeparationMeters,
                AerialDuels,
                HeadersWon,
                DefensiveHeaders,
                HeaderShots,
                GoalkeeperAerialCatches,
                GoalkeeperPunches,
                AerialSecondBalls),
            CreateAnalyticsSnapshot());
    }

    public void AttachRuntime(LiveMatchRuntime runtime)
    {
        _runtime = runtime ?? throw new System.ArgumentNullException(nameof(runtime));
        _usesExternalRuntime = true;
    }

    public void SetMatch(FootballMatchSimulation simulation)
    {
        System.ArgumentNullException.ThrowIfNull(simulation);
        Simulation = simulation;
        BasePositions.Clear();
        CurrentPositions.Clear();
        TargetPositions.Clear();
        _playerTeams.Clear();
        _playerRoles.Clear();
        _playerSlotIds.Clear();
        _playerPaces.Clear();
        _playerNumbers.Clear();
        _playerIntents.Clear();
        _interceptionAttemptedBy.Clear();
        _state.PendingCardActions.Clear();
        _state.DefenderChallengeReadyTimes.Clear();
        _state.GroundDuel.Reset();
        _movementController.Reset();
        _sideController.Reset();
        _state.VisualTime = 0;
        _synchronizedGameSeconds = 0d;
        _fixedStepAccumulatorSeconds = 0d;
        _lastPassTime = -10;
        _attackProgress = 0.22f;
        _phaseLane = 0.5f;
        _phaseSerial = 0;
        _decisionSerial = 0;
        _liveDecisionSeed = unchecked((uint)simulation.MatchSeed) ^
                            unchecked((uint)(simulation.MatchSeed >> 32));
        _decisionVarietyTracker.Reset();
        _decisionsSinceShot = 0;
        _carryOwnerId = new StringName();
        _consecutiveCarries = 0;
        _nextDecisionTime = 0.35f;
        _nextIntentPlanTime = 0f;
        _ballActionKind = BallActionKind.None;
        _ballActionActive = false;
        _ballActionFrom = new Vector2(0.5f, 0.5f);
        _ballActionTo = new Vector2(0.5f, 0.5f);
        _ballActionElapsed = 0f;
        _ballActionDuration = 0.65f;
        _ballActionArc = 0f;
        _ballVisualHeight = 0f;
        _ballVerticalVelocityMetersPerSecond = 0f;
        _aerialFlightActive = false;
        _aerialTrajectory = default;
        _aerialContenderIds.Clear();
        _ballNextOwnerId = new StringName();
        _pendingPassType = LivePassType.Standard;
        _pendingPassSpeedMetersPerSecond = 0f;
        _lastPassExecutionQuality = 1f;
        _lastPassIntendedTarget = new Vector2(0.5f, 0.5f);
        _lastPassActualTarget = new Vector2(0.5f, 0.5f);
        _actionSourceId = new StringName();
        _actionSourceTeamId = new StringName();
        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
        _pendingShotDistanceMeters = 0f;
        _pendingShotSituation = new StringName();
        _pendingOffsideReceiverId = new StringName();
        _directAttackOwnerId = new StringName();
        _directAttackActionsRemaining = 0;
        _state.IsLooseBallActive = false;
        _state.LooseBallResolveTime = 0f;
        _state.LooseBallVelocityMetersPerSecond = Vector2.Zero;
        _state.IsRestartPending = false;
        _state.RestartType = new StringName();
        _state.RestartTeamId = new StringName();
        _state.RestartPosition = new Vector2(0.5f, 0.5f);
        _state.RestartScheduledTime = 0f;
        _state.RestartExecuteTime = 0f;
        _freeKickRestartPlan = default;
        _penaltyRestartPlan = default;
        _state.RestartTakerId = new StringName();
        _state.IsBallVisible = true;
        _state.IsRestartBallPlaced = true;
        _kickoffPassPending = false;
        _kickoffReceiverId = new StringName();
        CompletedPasses = 0;
        Interceptions = 0;
        Dribbles = 0;
        FoulsCommitted = 0;
        Restarts = 0;
        LooseBallRecoveries = 0;
        Clearances = 0;
        PassAttempts = 0;
        FirstTouchErrors = 0;
        GroundDuelExchanges = 0;
        TackleAttempts = 0;
        ShoulderChallenges = 0;
        GroundDuelLooseBalls = 0;
        MaxGroundDuelTouches = 0;
        CarrierEscapes = 0;
        TacklesWon = 0;
        AerialDuels = 0;
        HeadersWon = 0;
        DefensiveHeaders = 0;
        HeaderShots = 0;
        GoalkeeperAerialCatches = 0;
        GoalkeeperPunches = 0;
        AerialSecondBalls = 0;
        ResetLiveAnalytics();
        MinimumObservedGroundDuelSeparationMeters = float.PositiveInfinity;
        ActiveScenario = null;
        IsPlaying = false;
        if (!_usesExternalRuntime)
        {
            _runtime.Reset();
        }
        _runtime.SetPhase(LiveMatchPhase.AwaitingKickoff);
        SetAction("Chuẩn bị giao bóng");
        BallPosition = new Vector2(0.5f, 0.5f);
        SetTrackedPossession(simulation.home.team.id);
        SyncLineups(true);
        ResetPlayersForKickoff(_state.ActiveTeamId);
        SelectPhasePlayers();
    }

    public void SetPlaying(bool playing)
    {
        IsPlaying = playing && Simulation is not null && !Simulation.is_finished;
        if (!_usesExternalRuntime)
        {
            if (IsPlaying)
            {
                _runtime.Start();
            }
            else
            {
                _runtime.Pause();
            }
        }
        if (IsPlaying)
        {
            _nextDecisionTime = Mathf.Max(_nextDecisionTime, _state.VisualTime + 0.15f);
            if (_runtime.Phase == LiveMatchPhase.AwaitingKickoff)
            {
                _runtime.SetPhase(LiveMatchPhase.InPossession);
            }
        }
    }

    public void AnimateMinute(Array<FootballMatchEvent> newEvents)
    {
        if (Simulation is null)
            return;
        SyncLineups(false);
        bool hasHalfTime = false;
        bool hasFullTime = false;
        foreach (FootballMatchEvent matchEvent in newEvents)
        {
            if (matchEvent.event_type == "half_time")
            {
                hasHalfTime = true;
            }
            if (matchEvent.event_type == "full_time")
            {
                hasFullTime = true;
            }
        }
        if (hasFullTime)
        {
            ApplyPendingCardsAtStoppage();
            _runtime.SetPhase(LiveMatchPhase.FullTime);
        }
        else if (hasHalfTime)
        {
            ApplyPendingCardsAtStoppage();
            _runtime.SetPhase(LiveMatchPhase.HalfTime);
            BeginSecondHalf();
            return;
        }
        FootballMatchEvent? focusEvent = null;
        foreach (FootballMatchEvent matchEvent in newEvents)
        {
            string type = matchEvent.event_type.ToString();
            if (type is "goal" or "shot_on_target" or "shot_off_target" or "corner" or
                "yellow_card" or "substitution" or "half_time" or "full_time")
                focusEvent = matchEvent;
        }

        StringName possessionTeam = Simulation.last_possession_team_id;
        if (focusEvent is not null && IsAttackingEvent(focusEvent.event_type) && focusEvent.team_id != new StringName())
            possessionTeam = focusEvent.team_id;
        if (possessionTeam == new StringName())
            possessionTeam = _state.ActiveTeamId;

        bool turnover = possessionTeam != _state.ActiveTeamId;
        SetTrackedPossession(possessionTeam);
        AdvancePhase(turnover, focusEvent);
        SelectPhasePlayers();

        if (focusEvent is not null)
            AnimateEvent(focusEvent);
        else if (!_ballActionActive && _state.BallOwnerId != new StringName() && _state.VisualTime >= _nextDecisionTime)
            DecideNextAction();
    }

    public void Process(double deltaValue)
    {
        if (_usesExternalRuntime)
        {
            return;
        }

        _runtime.Advance(deltaValue);
        AdvanceSynchronizedGameTime(_runtime.LastAdvancedGameSeconds);
    }

    public Array<FootballMatchEvent> AdvanceSynchronizedGameTime(double gameDeltaSeconds)
    {
        Array<FootballMatchEvent> emittedEvents = new();
        if (Simulation is null || gameDeltaSeconds <= 0d || !IsPlaying)
        {
            return emittedEvents;
        }

        const double boundaryToleranceSeconds = 0.000001d;
        double remainingSeconds = gameDeltaSeconds;
        while (remainingSeconds > boundaryToleranceSeconds && !Simulation.is_finished && IsPlaying)
        {
            double nextMinuteBoundary = (Simulation.current_minute + 1d) * 60d;
            double secondsToBoundary = nextMinuteBoundary - _synchronizedGameSeconds;
            if (secondsToBoundary > boundaryToleranceSeconds)
            {
                double step = System.Math.Min(remainingSeconds, secondsToBoundary);
                AdvanceGameTime(step);
                _synchronizedGameSeconds += step;
                remainingSeconds -= step;
            }

            if (nextMinuteBoundary - _synchronizedGameSeconds > boundaryToleranceSeconds)
            {
                continue;
            }

            Array<FootballMatchEvent> minuteEvents = Simulation.advance_minute();
            foreach (FootballMatchEvent matchEvent in minuteEvents)
            {
                emittedEvents.Add(matchEvent);
            }
            AnimateMinute(minuteEvents);
        }

        return emittedEvents;
    }

    public void AdvanceGameTime(double gameDeltaSeconds)
    {
        if (Simulation is null)
            return;
        if (!IsPlaying)
        {
            return;
        }

        const double accumulatorToleranceSeconds = 0.000000001d;
        _fixedStepAccumulatorSeconds += System.Math.Max(gameDeltaSeconds, 0d);
        while (_fixedStepAccumulatorSeconds + accumulatorToleranceSeconds >= _configuration.FixedStepSeconds &&
               IsPlaying &&
               !Simulation.is_finished)
        {
            AdvanceSimulationStep((float)_configuration.FixedStepSeconds);
            _fixedStepAccumulatorSeconds -= _configuration.FixedStepSeconds;
            if (_fixedStepAccumulatorSeconds < accumulatorToleranceSeconds)
            {
                _fixedStepAccumulatorSeconds = 0d;
            }
        }
    }

    private void AdvanceSimulationStep(float delta)
    {
        _state.VisualTime += delta;
        UpdateRestartBallPresentation();
        bool waitingForKickoff = _state.IsRestartPending && _state.RestartType == "kickoff";
        if (!waitingForKickoff)
        {
            UpdatePlayerTargets();
            _movementController.Advance(CurrentPositions, TargetPositions, _playerIntents, _playerPaces, delta);
            EnforceGroundPlayerSeparation();
        }
        UpdateBall(delta);
        if (IsPlaying && !_ballActionActive && _state.IsRestartPending && _state.VisualTime >= _state.RestartExecuteTime)
            ExecuteRestart();
        else if (IsPlaying && !_ballActionActive && _state.IsLooseBallActive && _state.VisualTime >= _state.LooseBallResolveTime)
            ResolveLooseBall();
        if (IsPlaying && !_ballActionActive && _state.BallOwnerId != new StringName() && _state.VisualTime >= _nextDecisionTime)
            DecideNextAction();
    }
}
