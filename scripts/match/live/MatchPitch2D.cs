using System.Collections.Generic;
using Godot;
using Godot.Collections;
using DotNetDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.Vector2>;
using TeamDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.StringName>;
using RoleDictionary = System.Collections.Generic.Dictionary<Godot.StringName, string>;
using IntentDictionary = System.Collections.Generic.Dictionary<Godot.StringName, PlayerIntent>;
using PaceDictionary = System.Collections.Generic.Dictionary<Godot.StringName, int>;
using NumberDictionary = System.Collections.Generic.Dictionary<Godot.StringName, int>;

[GlobalClass]
public partial class MatchPitch2D : Control
{
    [Signal] public delegate void ActionChangedEventHandler(string description);
    [Signal] public delegate void LiveMatchEventEventHandler(FootballMatchEvent matchEvent);

    private enum BallActionKind
    {
        None,
        Pass,
        ThroughBall,
        Cross,
        Shot,
        Clearance
    }

    private readonly struct PendingCardAction
    {
        public PendingCardAction(StringName teamId, StringName offenderId, StringName card)
        {
            TeamId = teamId;
            OffenderId = offenderId;
            Card = card;
        }

        public StringName TeamId { get; }
        public StringName OffenderId { get; }
        public StringName Card { get; }
    }

    private static readonly Color HomeColor = new("4f8cff");
    private static readonly Color AwayColor = new("ff5d73");
    private static readonly Color BallColor = new("f7fbff");

    public FootballMatchSimulation? Simulation { get; private set; }
    public readonly DotNetDictionary BasePositions = new();
    public readonly DotNetDictionary CurrentPositions = new();
    public readonly DotNetDictionary TargetPositions = new();
    private readonly TeamDictionary _playerTeams = new();
    private readonly RoleDictionary _playerRoles = new();
    private readonly TeamDictionary _playerSlotIds = new();
    private readonly PaceDictionary _playerPaces = new();
    private readonly NumberDictionary _playerNumbers = new();
    private readonly FootballIntentPlanner _intentPlanner = new();
    private readonly FootballMovementController _movementController = new();
    private readonly MatchSideController _sideController = new();
    private readonly OffsideRule _offsideRule = new();
    private readonly PassTrajectoryPlanner _passTrajectoryPlanner = new();
    private readonly PassExecutionResolver _passExecutionResolver = new();
    private readonly FirstTouchResolver _firstTouchResolver = new();
    private readonly RollingBallPhysics _rollingBallPhysics = new();
    private readonly ShotDecisionEvaluator _shotDecisionEvaluator = new();
    private readonly ShotOutcomeResolver _shotOutcomeResolver = new();
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
    private readonly BallCarrierDecisionEvaluator _ballCarrierDecisionEvaluator = new();
    private readonly ClearanceTargetPlanner _clearanceTargetPlanner = new();
    private readonly DuelDistanceRules _duelDistanceRules = new();
    private readonly IntentDictionary _playerIntents = new();
    private readonly HashSet<StringName> _interceptionAttemptedBy = new();
    private readonly List<PendingCardAction> _pendingCardActions = new();
    private LiveMatchRuntime _runtime = new();
    private bool _usesExternalRuntime;

    public Vector2 BallPosition { get; private set; } = new(0.5f, 0.5f);
    private StringName _ballOwnerId = new();
    private StringName _activeTeamId = new();
    private float _visualTime;
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
    private bool _looseBallActive;
    private float _looseBallResolveTime;
    private Vector2 _looseBallVelocityMetersPerSecond;
    private bool _restartPending;
    private StringName _restartType = new();
    private StringName _restartTeamId = new();
    private Vector2 _restartPosition;
    private float _restartScheduledTime;
    private float _restartExecuteTime;
    private FreeKickRestartPlan _freeKickRestartPlan;
    private PenaltyRestartPlan _penaltyRestartPlan;
    private StringName _restartTakerId = new();
    private bool _isBallVisible = true;
    private bool _restartBallPlaced = true;
    private float _nextIntentPlanTime;
    private bool _kickoffPassPending;
    private StringName _kickoffReceiverId = new();

    public string LastActionName { get; private set; } = "Chuẩn bị giao bóng";
    public int CompletedPasses { get; private set; }
    public int Interceptions { get; private set; }
    public int Dribbles { get; private set; }
    public int FoulsCommitted { get; private set; }
    public int Restarts { get; private set; }
    public int LooseBallRecoveries { get; private set; }
    public int Clearances { get; private set; }
    public int PassAttempts { get; private set; }
    public int FirstTouchErrors { get; private set; }
    public bool IsKickoffPassPending => _kickoffPassPending;
    public StringName KickoffReceiverId => _kickoffReceiverId;
    public bool IsQuickFreeKick => _restartPending && _restartType == "free_kick" && _freeKickRestartPlan.IsQuick;
    public bool IsPenaltyRestart => _restartPending && _restartType == "penalty";
    public int PendingCardActionCount => _pendingCardActions.Count;
    public StringName CurrentBallOwnerId => _ballOwnerId;
    public IReadOnlyDictionary<StringName, PlayerIntent> CurrentIntents => _playerIntents;
    public bool IsPlaying { get; private set; }
    public LiveMatchPhase LivePhase => _runtime.Phase;
    public double ElapsedGameSeconds => _runtime.ElapsedGameSeconds;
    public bool AreSidesSwitched => _sideController.AreSidesSwitched;
    public StringName PendingRestartType => _restartPending ? _restartType : new StringName();
    public bool IsBallInFlight => _ballActionActive;
    public bool IsLooseBall => _looseBallActive;
    public bool IsBallVisible => _isBallVisible;
    public bool IsRestartBallPlaced => _restartBallPlaced;
    public Vector2 LooseBallVelocityMetersPerSecond => _looseBallVelocityMetersPerSecond;
    public Vector2 BallFlightStart => _ballActionFrom;
    public Vector2 BallFlightTarget => _ballActionTo;
    public StringName BallActionSourceTeamId => _actionSourceTeamId;
    public string BallActionType => _ballActionKind.ToString();
    public MatchScenarioKind? ActiveScenario { get; private set; }

    private bool _ballActionActive;
    private Vector2 _ballActionFrom = new(0.5f, 0.5f);
    private Vector2 _ballActionTo = new(0.5f, 0.5f);
    private float _ballActionElapsed;
    private float _ballActionDuration = 0.65f;
    private float _ballActionArc;
    private float _ballVisualHeight;
    private StringName _ballNextOwnerId = new();
    private LivePassType _pendingPassType = LivePassType.Standard;
    private float _pendingPassSpeedMetersPerSecond;
    private float _lastPassExecutionQuality = 1f;
    private Vector2 _lastPassIntendedTarget;
    private Vector2 _lastPassActualTarget;

    public float LastPassExecutionQuality => _lastPassExecutionQuality;
    public Vector2 LastPassIntendedTarget => _lastPassIntendedTarget;
    public Vector2 LastPassActualTarget => _lastPassActualTarget;

    public override void _Ready()
    {
        SetExpandedDisplay(IsExpandedDisplay);
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public void AttachRuntime(LiveMatchRuntime runtime)
    {
        _runtime = runtime ?? throw new System.ArgumentNullException(nameof(runtime));
        _usesExternalRuntime = true;
    }

    public void SetMatch(FootballMatchSimulation simulation)
    {
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
        _pendingCardActions.Clear();
        _movementController.Reset();
        _sideController.Reset();
        _visualTime = 0;
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
        _pendingOffsideReceiverId = new StringName();
        _directAttackOwnerId = new StringName();
        _directAttackActionsRemaining = 0;
        _looseBallActive = false;
        _looseBallResolveTime = 0f;
        _looseBallVelocityMetersPerSecond = Vector2.Zero;
        _restartPending = false;
        _restartType = new StringName();
        _restartTeamId = new StringName();
        _restartPosition = new Vector2(0.5f, 0.5f);
        _restartScheduledTime = 0f;
        _restartExecuteTime = 0f;
        _freeKickRestartPlan = default;
        _penaltyRestartPlan = default;
        _restartTakerId = new StringName();
        _isBallVisible = true;
        _restartBallPlaced = true;
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
        ActiveScenario = null;
        IsPlaying = false;
        _runtime.SetPhase(LiveMatchPhase.AwaitingKickoff);
        SetAction("Chuẩn bị giao bóng");
        BallPosition = new Vector2(0.5f, 0.5f);
        _activeTeamId = simulation.home.team.id;
        simulation.set_live_possession(_activeTeamId);
        SyncLineups(true);
        ResetPlayersForKickoff(_activeTeamId);
        SelectPhasePlayers();
        QueueRedraw();
    }

    public void SetPlaying(bool playing)
    {
        IsPlaying = playing && Simulation is not null && !Simulation.is_finished;
        if (IsPlaying)
        {
            _nextDecisionTime = Mathf.Max(_nextDecisionTime, _visualTime + 0.15f);
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
            QueueRedraw();
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
            possessionTeam = _activeTeamId;

        bool turnover = possessionTeam != _activeTeamId;
        _activeTeamId = possessionTeam;
        AdvancePhase(turnover, focusEvent);
        SelectPhasePlayers();

        if (focusEvent is not null)
            AnimateEvent(focusEvent);
        else if (!_ballActionActive && _ballOwnerId != new StringName() && _visualTime >= _nextDecisionTime)
            DecideNextAction();
        QueueRedraw();
    }

    public override void _Process(double deltaValue)
    {
        if (_usesExternalRuntime)
        {
            QueueRedraw();
            return;
        }

        AdvanceGameTime(deltaValue);
    }

    public void AdvanceGameTime(double gameDeltaSeconds)
    {
        if (Simulation is null)
            return;
        if (!IsPlaying)
        {
            QueueRedraw();
            return;
        }

        const float fixedStepSeconds = 0.05f;
        double remainingSeconds = System.Math.Max(gameDeltaSeconds, 0d);
        while (remainingSeconds > 0.000001d && IsPlaying && !Simulation.is_finished)
        {
            float step = (float)System.Math.Min(remainingSeconds, fixedStepSeconds);
            AdvanceSimulationStep(step);
            remainingSeconds -= step;
        }
        QueueRedraw();
    }

    private void AdvanceSimulationStep(float delta)
    {
        _visualTime += delta;
        UpdateRestartBallPresentation();
        bool waitingForKickoff = _restartPending && _restartType == "kickoff";
        if (!waitingForKickoff)
        {
            UpdatePlayerTargets();
            _movementController.Advance(CurrentPositions, TargetPositions, _playerIntents, _playerPaces, delta);
        }
        UpdateBall(delta);
        if (IsPlaying && !_ballActionActive && _restartPending && _visualTime >= _restartExecuteTime)
            ExecuteRestart();
        else if (IsPlaying && !_ballActionActive && _looseBallActive && _visualTime >= _looseBallResolveTime)
            ResolveLooseBall();
        if (IsPlaying && !_ballActionActive && _ballOwnerId != new StringName() && _visualTime >= _nextDecisionTime)
            DecideNextAction();
    }
}
