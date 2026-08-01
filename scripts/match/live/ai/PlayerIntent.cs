using System.Collections.Generic;
using Godot;

public enum PlayerIntentKind
{
    Goalkeep,
    HoldShape,
    CarryBall,
    DribbleCloseControl,
    DribbleKnockOn,
    DribbleChangeDirection,
    ShieldBall,
    HoldUpBall,
    ReceivePass,
    SupportBall,
    RunIntoSpace,
    PressBall,
    CloseDownBall,
    JockeyBall,
    ContainBall,
    TackleBall,
    ShoulderChallenge,
    CoverPress,
    BlockPassingLane,
    MarkOpponent,
    ChaseLooseBall,
    ContestAerialBall,
    ClaimAerialBall,
    RecoverGoalSide,
    RepositionForRestart
}

public sealed class PlayerIntent
{
    public PlayerIntent(
        PlayerIntentKind kind,
        Vector2 target,
        LiveTeamPhase teamPhase,
        StringName? relatedPlayerId = null,
        OffBallAssignmentKind assignment = OffBallAssignmentKind.None,
        string targetKey = "")
    {
        Kind = kind;
        Target = target;
        TeamPhase = teamPhase;
        RelatedPlayerId = relatedPlayerId ?? new StringName();
        Assignment = assignment;
        TargetKey = targetKey;
    }

    public PlayerIntentKind Kind { get; }
    public Vector2 Target { get; }
    public LiveTeamPhase TeamPhase { get; }
    public StringName RelatedPlayerId { get; }
    public OffBallAssignmentKind Assignment { get; }
    public string TargetKey { get; }
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
        bool homeAttacksLeft = true,
        bool isShotInFlight = false,
        bool isCrossInFlight = false,
        StringName? previousBallOwnerId = null,
        IReadOnlyDictionary<StringName, TeamPhaseState>? teamPhaseStates = null)
        : this(
            positions,
            basePositions,
            playerTeams,
            playerRoles,
            ballPosition,
            ballDestination,
            ballOwnerId,
            expectedReceiverId,
            possessionTeamId,
            homeTeamId,
            isBallInFlight,
            isLooseBall,
            homeAttacksLeft,
            isShotInFlight,
            isCrossInFlight,
            previousBallOwnerId,
            teamPhaseStates,
            0f)
    {
    }

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
        bool homeAttacksLeft,
        bool isShotInFlight,
        bool isCrossInFlight,
        StringName? previousBallOwnerId,
        IReadOnlyDictionary<StringName, TeamPhaseState>? teamPhaseStates,
        float gameTimeSeconds)
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
        IsShotInFlight = isShotInFlight;
        IsCrossInFlight = isCrossInFlight;
        PreviousBallOwnerId = previousBallOwnerId ?? new StringName();
        TeamPhaseStates = teamPhaseStates;
        GameTimeSeconds = gameTimeSeconds;
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
    public bool IsShotInFlight { get; }
    public bool IsCrossInFlight { get; }
    public StringName PreviousBallOwnerId { get; }
    public IReadOnlyDictionary<StringName, TeamPhaseState>? TeamPhaseStates { get; }
    public float GameTimeSeconds { get; }

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

        if (TeamPhaseStates is not null && TeamPhaseStates.TryGetValue(teamId, out TeamPhaseState state))
        {
            return state.Phase;
        }

        if (teamId != PossessionTeamId)
        {
            return LiveTeamPhase.Defending;
        }

        return IsBallInFlight ? LiveTeamPhase.BallInFlight : LiveTeamPhase.InPossession;
    }

    public int RequiredRestDefencePlayersFor(StringName teamId)
    {
        return TeamPhaseStates is not null && TeamPhaseStates.TryGetValue(teamId, out TeamPhaseState state)
            ? state.RequiredRestDefencePlayers
            : 2;
    }
}
