using System;
using System.Collections.Generic;
using Godot;

public sealed class FootballIntentPlanner
{
    private static readonly TraditionalGoalkeeperPlanner GoalkeeperPlanner = new();
    private readonly OffBallRoleAllocator _allocator;

    public FootballIntentPlanner()
        : this(new OffBallRoleAllocator(OffBallParticipationConfiguration.CreateM3Defaults()))
    {
    }

    public FootballIntentPlanner(OffBallRoleAllocator allocator)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
    }

    public Dictionary<StringName, PlayerIntent> Plan(FootballWorldSnapshot world)
    {
        return _allocator.Allocate(world);
    }

    internal static PlayerIntent GoalkeeperIntent(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId,
        LiveTeamPhase phase)
    {
        bool rushesControlledBall = GoalkeeperPlanner.ShouldRushControlledBall(world, playerId, teamId);
        Vector2 target = GoalkeeperPlanner.PositionTarget(world, playerId, teamId);
        PlayerIntentKind intentKind = rushesControlledBall
            ? PlayerIntentKind.CloseDownBall
            : PlayerIntentKind.Goalkeep;
        return new PlayerIntent(
            intentKind,
            target,
            phase,
            world.BallOwnerId,
            OffBallAssignmentKind.Goalkeep,
            "goalkeeper");
    }

    internal static Vector2 ShiftBaseTowardBall(FootballWorldSnapshot world, StringName playerId, float weight)
    {
        Vector2 basePosition = world.BasePositions[playerId];
        Vector2 shiftedBall = new(world.BallPosition.X, Mathf.Lerp(basePosition.Y, world.BallPosition.Y, 0.55f));
        return SpaceEvaluator.ClampToPitch(basePosition.Lerp(shiftedBall, weight));
    }

    internal static List<StringName> TeamPlayers(FootballWorldSnapshot world, StringName teamId)
    {
        List<StringName> players = new();
        foreach (StringName playerId in world.Positions.Keys)
        {
            if (world.PlayerTeams[playerId] == teamId)
            {
                players.Add(playerId);
            }
        }
        players.Sort(ComparePlayerIds);
        return players;
    }

    internal static List<StringName> TeamOutfieldPlayers(FootballWorldSnapshot world, StringName teamId)
    {
        List<StringName> players = TeamPlayers(world, teamId);
        players.RemoveAll(playerId => world.PlayerRoles[playerId] == "GK");
        return players;
    }

    internal static int ComparePlayerIds(StringName first, StringName second)
    {
        return StringComparer.Ordinal.Compare(first.ToString(), second.ToString());
    }
}
