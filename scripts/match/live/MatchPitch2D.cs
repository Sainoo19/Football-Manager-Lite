using System.Collections.Generic;
using Godot;
using Godot.Collections;
using DotNetDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.Vector2>;
using TeamDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.StringName>;
using RoleDictionary = System.Collections.Generic.Dictionary<Godot.StringName, string>;
using IntentDictionary = System.Collections.Generic.Dictionary<Godot.StringName, PlayerIntent>;
using PaceDictionary = System.Collections.Generic.Dictionary<Godot.StringName, int>;

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
    private readonly FootballIntentPlanner _intentPlanner = new();
    private readonly FootballMovementController _movementController = new();
    private readonly MatchSideController _sideController = new();
    private readonly OffsideRule _offsideRule = new();
    private readonly PassTrajectoryPlanner _passTrajectoryPlanner = new();
    private readonly IntentDictionary _playerIntents = new();
    private readonly HashSet<StringName> _interceptionAttemptedBy = new();

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
    private int _decisionsSinceShot;
    private StringName _pendingShotOutcome = new();
    private StringName _pendingShotShooterId = new();
    private StringName _pendingShotGoalkeeperId = new();
    private StringName _pendingShotBlockerId = new();
    private StringName _pendingOffsideReceiverId = new();
    private bool _looseBallActive;
    private float _looseBallResolveTime;
    private bool _restartPending;
    private StringName _restartType = new();
    private StringName _restartTeamId = new();
    private Vector2 _restartPosition;
    private float _restartExecuteTime;
    private float _nextIntentPlanTime;

    public string LastActionName { get; private set; } = "Chuẩn bị giao bóng";
    public int CompletedPasses { get; private set; }
    public int Interceptions { get; private set; }
    public int Dribbles { get; private set; }
    public int FoulsCommitted { get; private set; }
    public int Restarts { get; private set; }
    public int LooseBallRecoveries { get; private set; }
    public int Clearances { get; private set; }
    public StringName CurrentBallOwnerId => _ballOwnerId;
    public IReadOnlyDictionary<StringName, PlayerIntent> CurrentIntents => _playerIntents;
    public bool IsPlaying { get; private set; }
    public bool AreSidesSwitched => _sideController.AreSidesSwitched;
    public StringName PendingRestartType => _restartPending ? _restartType : new StringName();
    public bool IsBallInFlight => _ballActionActive;
    public bool IsLooseBall => _looseBallActive;
    public Vector2 BallFlightStart => _ballActionFrom;
    public Vector2 BallFlightTarget => _ballActionTo;

    private bool _ballActionActive;
    private Vector2 _ballActionFrom = new(0.5f, 0.5f);
    private Vector2 _ballActionTo = new(0.5f, 0.5f);
    private float _ballActionElapsed;
    private float _ballActionDuration = 0.65f;
    private float _ballActionArc;
    private float _ballVisualHeight;
    private StringName _ballNextOwnerId = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(600, 400);
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
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
        _playerIntents.Clear();
        _interceptionAttemptedBy.Clear();
        _movementController.Reset();
        _sideController.Reset();
        _visualTime = 0;
        _lastPassTime = -10;
        _attackProgress = 0.22f;
        _phaseLane = 0.5f;
        _phaseSerial = 0;
        _decisionSerial = 0;
        _decisionsSinceShot = 0;
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
        _actionSourceId = new StringName();
        _actionSourceTeamId = new StringName();
        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
        _pendingOffsideReceiverId = new StringName();
        _looseBallActive = false;
        _looseBallResolveTime = 0f;
        _restartPending = false;
        _restartType = new StringName();
        _restartTeamId = new StringName();
        _restartPosition = new Vector2(0.5f, 0.5f);
        _restartExecuteTime = 0f;
        CompletedPasses = 0;
        Interceptions = 0;
        Dribbles = 0;
        FoulsCommitted = 0;
        Restarts = 0;
        LooseBallRecoveries = 0;
        Clearances = 0;
        IsPlaying = false;
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
            _nextDecisionTime = Mathf.Max(_nextDecisionTime, _visualTime + 0.15f);
    }

    public void AnimateMinute(Array<FootballMatchEvent> newEvents)
    {
        if (Simulation is null)
            return;
        SyncLineups(false);
        foreach (FootballMatchEvent matchEvent in newEvents)
        {
            if (matchEvent.event_type == "half_time")
            {
                BeginSecondHalf();
                QueueRedraw();
                return;
            }
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
        if (Simulation is null)
            return;
        if (!IsPlaying)
        {
            QueueRedraw();
            return;
        }
        float delta = (float)deltaValue;
        _visualTime += delta;
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
        QueueRedraw();
    }
}
