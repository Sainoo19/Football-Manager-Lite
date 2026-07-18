using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class LiveMatchState
{
    public LiveMatchState()
    {
        BasePositionView = new ReadOnlyDictionary<StringName, Vector2>(BasePositions);
        CurrentPositionView = new ReadOnlyDictionary<StringName, Vector2>(CurrentPositions);
        TargetPositionView = new ReadOnlyDictionary<StringName, Vector2>(TargetPositions);
        PlayerTeamView = new ReadOnlyDictionary<StringName, StringName>(PlayerTeams);
        PlayerRoleView = new ReadOnlyDictionary<StringName, string>(PlayerRoles);
        PlayerNumberView = new ReadOnlyDictionary<StringName, int>(PlayerNumbers);
    }

    public FootballMatchSimulation? Simulation { get; set; }
    public Dictionary<StringName, Vector2> BasePositions { get; } = new();
    public Dictionary<StringName, Vector2> CurrentPositions { get; } = new();
    public Dictionary<StringName, Vector2> TargetPositions { get; } = new();
    public Dictionary<StringName, StringName> PlayerTeams { get; } = new();
    public Dictionary<StringName, string> PlayerRoles { get; } = new();
    public Dictionary<StringName, StringName> PlayerSlotIds { get; } = new();
    public Dictionary<StringName, int> PlayerPaces { get; } = new();
    public Dictionary<StringName, int> PlayerNumbers { get; } = new();
    public Dictionary<StringName, float> DefenderChallengeReadyTimes { get; } = new();
    public IReadOnlyDictionary<StringName, Vector2> BasePositionView { get; }
    public IReadOnlyDictionary<StringName, Vector2> CurrentPositionView { get; }
    public IReadOnlyDictionary<StringName, Vector2> TargetPositionView { get; }
    public IReadOnlyDictionary<StringName, StringName> PlayerTeamView { get; }
    public IReadOnlyDictionary<StringName, string> PlayerRoleView { get; }
    public IReadOnlyDictionary<StringName, int> PlayerNumberView { get; }
    public Vector2 BallPosition { get; set; } = new(0.5f, 0.5f);
    public StringName BallOwnerId { get; set; } = new();
    public StringName ActiveTeamId { get; set; } = new();
    public float VisualTime { get; set; }
    public bool IsLooseBallActive { get; set; }
    public float LooseBallResolveTime { get; set; }
    public Vector2 LooseBallVelocityMetersPerSecond { get; set; }
    public bool IsRestartPending { get; set; }
    public StringName RestartType { get; set; } = new();
    public StringName RestartTeamId { get; set; } = new();
    public Vector2 RestartPosition { get; set; }
    public float RestartScheduledTime { get; set; }
    public float RestartExecuteTime { get; set; }
    public StringName RestartTakerId { get; set; } = new();
    public bool IsBallVisible { get; set; } = true;
    public bool IsRestartBallPlaced { get; set; } = true;
    public List<PendingCardAction> PendingCardActions { get; } = new();
    public GroundDuelSequenceState GroundDuel { get; } = new();
    public string LastActionName { get; set; } = "Chuẩn bị giao bóng";
    public bool IsPlaying { get; set; }
}

internal readonly struct PendingCardAction
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
