using System;
using System.Collections.Generic;
using Godot;

public sealed class FootballIntentPlanner
{
    private const int SupportPlayerCount = 3;
    private const int ForwardRunnerCount = 3;
    private const int LooseBallChaserCount = 1;
    private static readonly TraditionalGoalkeeperPlanner GoalkeeperPlanner = new();

    public Dictionary<StringName, PlayerIntent> Plan(FootballWorldSnapshot world)
    {
        Dictionary<StringName, PlayerIntent> intents = new();
        HashSet<StringName> teamIds = new();
        foreach (StringName playerId in world.Positions.Keys)
        {
            teamIds.Add(world.PlayerTeams[playerId]);
        }

        foreach (StringName teamId in teamIds)
        {
            LiveTeamPhase phase = world.PhaseFor(teamId);
            if (phase == LiveTeamPhase.LooseBall)
            {
                PlanLooseBallTeam(world, teamId, intents);
            }
            else if (phase is LiveTeamPhase.InPossession or LiveTeamPhase.BallInFlight)
            {
                PlanPossessionTeam(world, teamId, phase, intents);
            }
            else
            {
                DefensiveIntentPlanner.Plan(world, teamId, intents);
            }
        }

        TeamSpacingResolver.Resolve(world, intents);
        return intents;
    }

    private static void PlanPossessionTeam(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> outfieldPlayers = TeamOutfieldPlayers(world, teamId);
        List<StringName> supportPlayers = SelectSupportPlayers(
            world,
            outfieldPlayers,
            world.BallOwnerId,
            world.ExpectedReceiverId);
        HashSet<StringName> supportSet = new(supportPlayers);
        List<StringName> runners = SelectForwardRunners(
            world,
            outfieldPlayers,
            supportSet,
            world.BallOwnerId,
            world.ExpectedReceiverId);
        HashSet<StringName> runnerSet = new(runners);

        int supportIndex = 0;
        foreach (StringName playerId in TeamPlayers(world, teamId))
        {
            string role = world.PlayerRoles[playerId];
            if (role == "GK")
            {
                intents[playerId] = GoalkeeperIntent(world, playerId, teamId, phase);
            }
            else if (playerId == world.BallOwnerId)
            {
                Vector2 target = AttackingRoleTargeter.CarrierTarget(world, playerId, teamId);
                intents[playerId] = new PlayerIntent(PlayerIntentKind.CarryBall, target, phase);
            }
            else if (playerId == world.ExpectedReceiverId && world.IsBallInFlight)
            {
                intents[playerId] = new PlayerIntent(
                    PlayerIntentKind.ReceivePass,
                    world.BallDestination,
                    phase,
                    world.BallOwnerId);
            }
            else if (supportSet.Contains(playerId))
            {
                Vector2 target = AttackingRoleTargeter.SupportTarget(world, playerId, teamId, supportIndex++);
                intents[playerId] = new PlayerIntent(
                    PlayerIntentKind.SupportBall,
                    target,
                    phase,
                    world.BallOwnerId);
            }
            else if (runnerSet.Contains(playerId))
            {
                Vector2 target = AttackingRoleTargeter.RunnerTarget(world, playerId, teamId);
                intents[playerId] = new PlayerIntent(PlayerIntentKind.RunIntoSpace, target, phase);
            }
            else
            {
                intents[playerId] = new PlayerIntent(
                    PlayerIntentKind.HoldShape,
                    ShiftBaseTowardBall(world, playerId, 0.20f),
                    phase);
            }
        }
    }

    private static void PlanLooseBallTeam(
        FootballWorldSnapshot world,
        StringName teamId,
        Dictionary<StringName, PlayerIntent> intents)
    {
        StringName goalkeeperId = new();
        foreach (StringName playerId in TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] == "GK")
            {
                goalkeeperId = playerId;
                break;
            }
        }
        bool goalkeeperClaims = goalkeeperId != new StringName() &&
                                GoalkeeperPlanner.ShouldClaimLooseBall(world, goalkeeperId, teamId);
        List<StringName> outfieldPlayers = TeamOutfieldPlayers(world, teamId);
        List<StringName> chasers = goalkeeperClaims
            ? new List<StringName>()
            : ClosestPlayers(world, outfieldPlayers, world.BallPosition, LooseBallChaserCount);
        HashSet<StringName> chaserSet = new(chasers);

        foreach (StringName playerId in TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] == "GK")
            {
                intents[playerId] = goalkeeperClaims
                    ? new PlayerIntent(
                        PlayerIntentKind.ChaseLooseBall,
                        world.BallPosition,
                        LiveTeamPhase.LooseBall)
                    : GoalkeeperIntent(world, playerId, teamId, LiveTeamPhase.LooseBall);
            }
            else if (chaserSet.Contains(playerId))
            {
                intents[playerId] = new PlayerIntent(
                    PlayerIntentKind.ChaseLooseBall,
                    world.BallPosition,
                    LiveTeamPhase.LooseBall);
            }
            else
            {
                intents[playerId] = new PlayerIntent(
                    PlayerIntentKind.HoldShape,
                    ShiftBaseTowardBall(world, playerId, 0.24f),
                    LiveTeamPhase.LooseBall);
            }
        }
    }

    internal static PlayerIntent GoalkeeperIntent(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId,
        LiveTeamPhase phase)
    {
        Vector2 target = GoalkeeperPlanner.PositionTarget(world, teamId);
        return new PlayerIntent(PlayerIntentKind.Goalkeep, target, phase, world.BallOwnerId);
    }

    internal static Vector2 ShiftBaseTowardBall(FootballWorldSnapshot world, StringName playerId, float weight)
    {
        Vector2 basePosition = world.BasePositions[playerId];
        Vector2 shiftedBall = new(world.BallPosition.X, Mathf.Lerp(basePosition.Y, world.BallPosition.Y, 0.55f));
        return SpaceEvaluator.ClampToPitch(basePosition.Lerp(shiftedBall, weight));
    }

    private static List<StringName> SelectForwardRunners(
        FootballWorldSnapshot world,
        List<StringName> candidates,
        HashSet<StringName> excluded,
        StringName ballOwnerId,
        StringName expectedReceiverId)
    {
        List<StringName> runners = new();
        foreach (StringName playerId in candidates)
        {
            if (excluded.Contains(playerId) || playerId == ballOwnerId || playerId == expectedReceiverId)
            {
                continue;
            }

            if (IsAttackingRole(world.PlayerRoles[playerId]))
            {
                runners.Add(playerId);
            }
        }

        runners.Sort((first, second) => RunnerPriority(world.PlayerRoles[second])
            .CompareTo(RunnerPriority(world.PlayerRoles[first])));
        if (runners.Count > ForwardRunnerCount)
        {
            runners.RemoveRange(ForwardRunnerCount, runners.Count - ForwardRunnerCount);
        }

        return runners;
    }

    private static List<StringName> SelectSupportPlayers(
        FootballWorldSnapshot world,
        List<StringName> candidates,
        StringName ballOwnerId,
        StringName expectedReceiverId)
    {
        List<StringName> preferred = new();
        List<StringName> fallback = new();
        foreach (StringName playerId in candidates)
        {
            if (playerId == ballOwnerId || playerId == expectedReceiverId)
            {
                continue;
            }

            fallback.Add(playerId);
            if (world.PlayerRoles[playerId] is "LB" or "RB" or "DM" or "CM" or "AM")
            {
                preferred.Add(playerId);
            }
        }

        Comparison<StringName> byBallDistance = (first, second) =>
            PlayerProximity.DistanceSquaredMeters(world.Positions[first], world.BallPosition)
                .CompareTo(PlayerProximity.DistanceSquaredMeters(world.Positions[second], world.BallPosition));
        preferred.Sort(byBallDistance);
        fallback.Sort(byBallDistance);

        List<StringName> supportPlayers = new();
        foreach (StringName playerId in preferred)
        {
            if (supportPlayers.Count >= SupportPlayerCount)
            {
                break;
            }

            supportPlayers.Add(playerId);
        }

        foreach (StringName playerId in fallback)
        {
            if (supportPlayers.Count >= SupportPlayerCount)
            {
                break;
            }

            if (!supportPlayers.Contains(playerId))
            {
                supportPlayers.Add(playerId);
            }
        }

        return supportPlayers;
    }

    private static List<StringName> ClosestPlayers(
        FootballWorldSnapshot world,
        List<StringName> candidates,
        Vector2 point,
        int count,
        StringName? excludedFirst = null,
        StringName? excludedSecond = null)
    {
        List<StringName> result = new();
        foreach (StringName playerId in candidates)
        {
            if ((excludedFirst is null || playerId != excludedFirst) &&
                (excludedSecond is null || playerId != excludedSecond))
            {
                result.Add(playerId);
            }
        }

        result.Sort((first, second) =>
            PlayerProximity.DistanceSquaredMeters(world.Positions[first], point)
                .CompareTo(PlayerProximity.DistanceSquaredMeters(world.Positions[second], point)));
        if (result.Count > count)
        {
            result.RemoveRange(count, result.Count - count);
        }

        return result;
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

        return players;
    }

    internal static List<StringName> TeamOutfieldPlayers(FootballWorldSnapshot world, StringName teamId)
    {
        List<StringName> players = TeamPlayers(world, teamId);
        players.RemoveAll(playerId => world.PlayerRoles[playerId] == "GK");
        return players;
    }

    private static bool IsAttackingRole(string role) => role is "CM" or "AM" or "LW" or "RW" or "ST";

    private static int RunnerPriority(string role) => role switch
    {
        "ST" => 5,
        "LW" or "RW" => 4,
        "AM" => 3,
        "CM" => 2,
        _ => 1
    };
}
