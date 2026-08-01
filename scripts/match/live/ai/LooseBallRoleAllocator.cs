using System;
using System.Collections.Generic;
using Godot;

public sealed class LooseBallRoleAllocator
{
    private static readonly TraditionalGoalkeeperPlanner GoalkeeperPlanner = new();
    private readonly OffBallParticipationConfiguration _configuration;

    public LooseBallRoleAllocator(OffBallParticipationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Allocate(
        FootballWorldSnapshot world,
        StringName teamId,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> players = FootballIntentPlanner.TeamOutfieldPlayers(world, teamId);
        players.Sort((first, second) =>
        {
            int distanceComparison = PlayerProximity.DistanceSquaredMeters(world.Positions[first], world.BallPosition)
                .CompareTo(PlayerProximity.DistanceSquaredMeters(world.Positions[second], world.BallPosition));
            return distanceComparison != 0
                ? distanceComparison
                : FootballIntentPlanner.ComparePlayerIds(first, second);
        });
        int closePlayers = 0;
        foreach (StringName playerId in players)
        {
            if (FootballPitchDimensions.DistanceMeters(world.Positions[playerId], world.BallPosition) <= 12f)
            {
                closePlayers++;
            }
        }
        int chaserCount = Math.Min(
            closePlayers >= 2 ? _configuration.MaximumLooseBallChasers : 1,
            players.Count);
        StringName goalkeeperId = FindGoalkeeper(world, teamId);
        bool goalkeeperClaims = goalkeeperId != new StringName() &&
                                GoalkeeperPlanner.ShouldClaimLooseBall(world, goalkeeperId, teamId);
        if (goalkeeperClaims)
        {
            chaserCount = 0;
        }

        HashSet<StringName> chasers = new();
        for (int index = 0; index < chaserCount; index++)
        {
            chasers.Add(players[index]);
        }
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] == "GK")
            {
                intents[playerId] = goalkeeperClaims
                    ? CreateIntent(
                        PlayerIntentKind.ChaseLooseBall,
                        world.BallPosition,
                        OffBallAssignmentKind.ChaseLooseBall)
                    : FootballIntentPlanner.GoalkeeperIntent(world, playerId, teamId, LiveTeamPhase.LooseBall);
            }
            else if (chasers.Contains(playerId))
            {
                intents[playerId] = CreateIntent(
                    PlayerIntentKind.ChaseLooseBall,
                    world.BallPosition,
                    OffBallAssignmentKind.ChaseLooseBall);
            }
            else
            {
                intents[playerId] = CreateIntent(
                    PlayerIntentKind.HoldShape,
                    FootballIntentPlanner.ShiftBaseTowardBall(world, playerId, 0.25f),
                    OffBallAssignmentKind.ClaimSecondBallZone);
            }
        }
    }

    private static StringName FindGoalkeeper(FootballWorldSnapshot world, StringName teamId)
    {
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] == "GK")
            {
                return playerId;
            }
        }
        return new StringName();
    }

    private static PlayerIntent CreateIntent(
        PlayerIntentKind kind,
        Vector2 target,
        OffBallAssignmentKind assignment)
    {
        Vector2 clamped = SpaceEvaluator.ClampToPitch(target);
        return new PlayerIntent(
            kind,
            clamped,
            LiveTeamPhase.LooseBall,
            assignment: assignment,
            targetKey: SpaceOccupationMap.ZoneFor(clamped).Key);
    }
}
