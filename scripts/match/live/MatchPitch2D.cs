using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchPitch2D : Control
{
    [Signal] public delegate void ActionChangedEventHandler(string description);
    [Signal] public delegate void LiveMatchEventEventHandler(FootballMatchEvent matchEvent);

    private static readonly Color HomeColor = new("4f8cff");
    private static readonly Color AwayColor = new("ff5d73");
    private static readonly Color BallColor = new("f7fbff");
    private readonly LiveMatchEngine _engine = new();

    public FootballMatchSimulation? Simulation => _engine.Simulation;
    public IReadOnlyDictionary<StringName, Vector2> BasePositions => _engine.BasePositionView;
    public IReadOnlyDictionary<StringName, Vector2> CurrentPositions => _engine.PositionView;
    public IReadOnlyDictionary<StringName, Vector2> TargetPositions => _engine.TargetPositionView;
    public IReadOnlyDictionary<StringName, PlayerIntent> CurrentIntents => _engine.CurrentIntents;
    public Vector2 BallPosition => _engine.BallPosition;
    public string LastActionName => _engine.LastActionName;
    public int CompletedPasses => _engine.CompletedPasses;
    public int Interceptions => _engine.Interceptions;
    public int Dribbles => _engine.Dribbles;
    public int FoulsCommitted => _engine.FoulsCommitted;
    public int Restarts => _engine.Restarts;
    public int LooseBallRecoveries => _engine.LooseBallRecoveries;
    public int Clearances => _engine.Clearances;
    public int PassAttempts => _engine.PassAttempts;
    public int FirstTouchErrors => _engine.FirstTouchErrors;
    public int GroundDuelExchanges => _engine.GroundDuelExchanges;
    public int TackleAttempts => _engine.TackleAttempts;
    public int ShoulderChallenges => _engine.ShoulderChallenges;
    public int GroundDuelLooseBalls => _engine.GroundDuelLooseBalls;
    public int MaxGroundDuelTouches => _engine.MaxGroundDuelTouches;
    public int CarrierEscapes => _engine.CarrierEscapes;
    public int TacklesWon => _engine.TacklesWon;
    public float MinimumObservedGroundDuelSeparationMeters =>
        _engine.MinimumObservedGroundDuelSeparationMeters;
    public bool IsKickoffPassPending => _engine.IsKickoffPassPending;
    public StringName KickoffReceiverId => _engine.KickoffReceiverId;
    public bool IsQuickFreeKick => _engine.IsQuickFreeKick;
    public bool IsPenaltyRestart => _engine.IsPenaltyRestart;
    public int PendingCardActionCount => _engine.PendingCardActionCount;
    public StringName CurrentBallOwnerId => _engine.CurrentBallOwnerId;
    public bool IsPlaying => _engine.IsPlaying;
    public LiveMatchPhase LivePhase => _engine.LivePhase;
    public double ElapsedGameSeconds => _engine.ElapsedGameSeconds;
    public bool AreSidesSwitched => _engine.AreSidesSwitched;
    public StringName PendingRestartType => _engine.PendingRestartType;
    public bool IsBallInFlight => _engine.IsBallInFlight;
    public bool IsLooseBall => _engine.IsLooseBall;
    public bool IsBallVisible => _engine.IsBallVisible;
    public bool IsRestartBallPlaced => _engine.IsRestartBallPlaced;
    public Vector2 LooseBallVelocityMetersPerSecond => _engine.LooseBallVelocityMetersPerSecond;
    public Vector2 BallFlightStart => _engine.BallFlightStart;
    public Vector2 BallFlightTarget => _engine.BallFlightTarget;
    public StringName BallActionSourceTeamId => _engine.BallActionSourceTeamId;
    public string BallActionType => _engine.BallActionType;
    public bool IsAerialBall => _engine.IsAerialBall;
    public float BallHeightMeters => _engine.BallHeightMeters;
    public float BallVerticalVelocityMetersPerSecond => _engine.BallVerticalVelocityMetersPerSecond;
    public int AerialDuels => _engine.AerialDuels;
    public int HeadersWon => _engine.HeadersWon;
    public int DefensiveHeaders => _engine.DefensiveHeaders;
    public int HeaderShots => _engine.HeaderShots;
    public int GoalkeeperAerialCatches => _engine.GoalkeeperAerialCatches;
    public int GoalkeeperPunches => _engine.GoalkeeperPunches;
    public int AerialSecondBalls => _engine.AerialSecondBalls;
    public MatchScenarioKind? ActiveScenario => _engine.ActiveScenario;
    public float LastPassExecutionQuality => _engine.LastPassExecutionQuality;
    public Vector2 LastPassIntendedTarget => _engine.LastPassIntendedTarget;
    public Vector2 LastPassActualTarget => _engine.LastPassActualTarget;

    private IReadOnlyDictionary<StringName, StringName> PlayerTeams => _engine.PlayerTeams;
    private IReadOnlyDictionary<StringName, string> PlayerRoles => _engine.PlayerRoles;
    private IReadOnlyDictionary<StringName, int> PlayerNumbers => _engine.PlayerNumbers;
    private float BallVisualHeight => _engine.BallVisualHeight;

    public override void _Ready()
    {
        _engine.ActionChanged += HandleActionChanged;
        _engine.LiveMatchEvent += HandleLiveMatchEvent;
        SetExpandedDisplay(IsExpandedDisplay);
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        _engine.ActionChanged -= HandleActionChanged;
        _engine.LiveMatchEvent -= HandleLiveMatchEvent;
    }

    public override void _Process(double deltaValue)
    {
        _engine.Process(deltaValue);
        QueueRedraw();
    }

    public void AttachRuntime(LiveMatchRuntime runtime)
    {
        _engine.AttachRuntime(runtime);
    }

    public void SetMatch(FootballMatchSimulation simulation)
    {
        _engine.SetMatch(simulation);
        QueueRedraw();
    }

    public void SetPlaying(bool playing)
    {
        _engine.SetPlaying(playing);
    }

    public void AnimateMinute(Array<FootballMatchEvent> newEvents)
    {
        _engine.AnimateMinute(newEvents);
        QueueRedraw();
    }

    public void AdvanceGameTime(double gameDeltaSeconds)
    {
        _engine.AdvanceGameTime(gameDeltaSeconds);
        QueueRedraw();
    }

    public bool StartScenario(MatchScenarioKind kind)
    {
        bool started = _engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.StartScenario, kind));
        QueueRedraw();
        return started;
    }

    public LiveMatchSnapshot GetSnapshot()
    {
        return _engine.GetSnapshot();
    }

    internal bool OverridePlayerPosition(StringName playerId, Vector2 position)
    {
        return _engine.OverridePlayerPosition(playerId, position);
    }

    private void HandleActionChanged(string description)
    {
        EmitSignal(SignalName.ActionChanged, description);
    }

    private void HandleLiveMatchEvent(FootballMatchEvent matchEvent)
    {
        EmitSignal(SignalName.LiveMatchEvent, matchEvent);
    }
}
