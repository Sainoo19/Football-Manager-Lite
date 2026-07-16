using System.Linq;
using Godot;
using Godot.Collections;
using DotNetDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.Vector2>;
using TeamDictionary = System.Collections.Generic.Dictionary<Godot.StringName, Godot.StringName>;
using RoleDictionary = System.Collections.Generic.Dictionary<Godot.StringName, string>;

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
        Interception
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
    private bool _interceptionChecked;
    private float _nextDecisionTime;
    private int _decisionSerial;
    private int _decisionsSinceShot;
    private StringName _pendingShotOutcome = new();
    private StringName _pendingShotShooterId = new();
    private StringName _pendingShotGoalkeeperId = new();
    private StringName _pendingShotBlockerId = new();
    private bool _looseBallActive;
    private float _looseBallResolveTime;
    private bool _restartPending;
    private StringName _restartType = new();
    private StringName _restartTeamId = new();
    private Vector2 _restartPosition;
    private float _restartExecuteTime;

    public string LastActionName { get; private set; } = "Chuẩn bị giao bóng";
    public int CompletedPasses { get; private set; }
    public int Interceptions { get; private set; }
    public int Dribbles { get; private set; }
    public int FoulsCommitted { get; private set; }
    public int Restarts { get; private set; }
    public int LooseBallRecoveries { get; private set; }
    public StringName CurrentBallOwnerId => _ballOwnerId;
    public bool IsPlaying { get; private set; }

    private bool _ballActionActive;
    private Vector2 _ballActionFrom = new(0.5f, 0.5f);
    private Vector2 _ballActionTo = new(0.5f, 0.5f);
    private float _ballActionElapsed;
    private float _ballActionDuration = 0.65f;
    private float _ballActionArc;
    private StringName _ballNextOwnerId = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(600, 270);
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
        _visualTime = 0;
        _lastPassTime = -10;
        _attackProgress = 0.22f;
        _phaseLane = 0.5f;
        _phaseSerial = 0;
        _decisionSerial = 0;
        _decisionsSinceShot = 0;
        _nextDecisionTime = 0.35f;
        _ballActionKind = BallActionKind.None;
        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
        _looseBallActive = false;
        _restartPending = false;
        CompletedPasses = 0;
        Interceptions = 0;
        Dribbles = 0;
        FoulsCommitted = 0;
        Restarts = 0;
        LooseBallRecoveries = 0;
        IsPlaying = false;
        SetAction("Chuẩn bị giao bóng");
        BallPosition = new Vector2(0.5f, 0.5f);
        _activeTeamId = simulation.home.team.id;
        simulation.set_live_possession(_activeTeamId);
        SyncLineups(true);
        SelectPhasePlayers();
        _ballOwnerId = ChooseOwner(_activeTeamId, false);
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
        float delta = (float)deltaValue;
        _visualTime += delta;
        UpdatePlayerTargets();
        float weight = 1f - Mathf.Exp(-delta * 3.6f);
        foreach (StringName playerId in CurrentPositions.Keys.ToArray())
        {
            if (TargetPositions.TryGetValue(playerId, out Vector2 target))
            {
                float roleSpeed = RoleSpeed(_playerRoles[playerId]);
                CurrentPositions[playerId] = CurrentPositions[playerId].Lerp(target, Mathf.Clamp(weight * roleSpeed, 0, 1));
            }
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
