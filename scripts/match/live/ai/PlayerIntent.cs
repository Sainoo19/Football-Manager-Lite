using System.Collections.Generic;
using Godot;

public enum LiveTeamPhase
{
    InPossession,
    BallInFlight,
    Defending,
    LooseBall
}

public enum PlayerIntentKind
{
    Goalkeep,
    HoldShape,
    CarryBall,
    ReceivePass,
    SupportBall,
    RunIntoSpace,
    PressBall,
    CoverPress,
    MarkOpponent,
    ChaseLooseBall
}

public sealed class PlayerIntent
{
    public PlayerIntent(
        PlayerIntentKind kind,
        Vector2 target,
        LiveTeamPhase teamPhase,
        StringName? relatedPlayerId = null)
    {
        Kind = kind;
        Target = target;
        TeamPhase = teamPhase;
        RelatedPlayerId = relatedPlayerId ?? new StringName();
    }

    public PlayerIntentKind Kind { get; }
    public Vector2 Target { get; }
    public LiveTeamPhase TeamPhase { get; }
    public StringName RelatedPlayerId { get; }
}

public sealed class FootballWorldSnapshot
{
    public FootballWorldSnapshot(
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> basePositions,
        IReadOnlyDictionary<StringName, StringName> playerTeams,
        IReadOnlyDictionary<StringName, string> playerRoles,
        Vector2 ballPosition,
        Vector2 ballDestination,
        StringName ballOwnerId,
        StringName expectedReceiverId,
        StringName possessionTeamId,
        StringName homeTeamId,
        bool isBallInFlight,
        bool isLooseBall,
        bool homeAttacksLeft = true)
    {
        Positions = positions;
        BasePositions = basePositions;
        PlayerTeams = playerTeams;
        PlayerRoles = playerRoles;
        BallPosition = ballPosition;
        BallDestination = ballDestination;
        BallOwnerId = ballOwnerId;
        ExpectedReceiverId = expectedReceiverId;
        PossessionTeamId = possessionTeamId;
        HomeTeamId = homeTeamId;
        IsBallInFlight = isBallInFlight;
        IsLooseBall = isLooseBall;
        HomeAttacksLeft = homeAttacksLeft;
    }

    public IReadOnlyDictionary<StringName, Vector2> Positions { get; }
    public IReadOnlyDictionary<StringName, Vector2> BasePositions { get; }
    public IReadOnlyDictionary<StringName, StringName> PlayerTeams { get; }
    public IReadOnlyDictionary<StringName, string> PlayerRoles { get; }
    public Vector2 BallPosition { get; }
    public Vector2 BallDestination { get; }
    public StringName BallOwnerId { get; }
    public StringName ExpectedReceiverId { get; }
    public StringName PossessionTeamId { get; }
    public StringName HomeTeamId { get; }
    public bool IsBallInFlight { get; }
    public bool IsLooseBall { get; }
    public bool HomeAttacksLeft { get; }

    public float AttackDirection(StringName teamId)
    {
        bool isHomeTeam = teamId == HomeTeamId;
        return isHomeTeam == HomeAttacksLeft ? -1f : 1f;
    }

    public Vector2 OwnGoal(StringName teamId) => new(AttackDirection(teamId) > 0f ? 0.015f : 0.985f, 0.5f);

    public LiveTeamPhase PhaseFor(StringName teamId)
    {
        if (IsLooseBall)
        {
            return LiveTeamPhase.LooseBall;
        }

        if (teamId != PossessionTeamId)
        {
            return LiveTeamPhase.Defending;
        }

        return IsBallInFlight ? LiveTeamPhase.BallInFlight : LiveTeamPhase.InPossession;
    }
}
