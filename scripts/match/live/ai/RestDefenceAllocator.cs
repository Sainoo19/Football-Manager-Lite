using System;
using System.Collections.Generic;
using Godot;

public sealed class RestDefenceAllocator
{
    public List<StringName> Select(
        FootballWorldSnapshot world,
        StringName teamId,
        IReadOnlyList<StringName> candidates,
        int requiredCount)
    {
        List<StringName> eligible = new();
        foreach (StringName playerId in candidates)
        {
            if (playerId == world.BallOwnerId || playerId == world.ExpectedReceiverId)
            {
                continue;
            }
            if (world.PlayerRoles[playerId] is "CB" or "LB" or "RB" or "DM" or "CM")
            {
                eligible.Add(playerId);
            }
        }

        int dangerousOpponents = CountDangerousOpponents(world, teamId);
        int dynamicRequired = Math.Min(requiredCount + (dangerousOpponents >= 3 ? 1 : 0), eligible.Count);
        eligible.Sort((first, second) =>
        {
            int roleComparison = Priority(world.PlayerRoles[first]).CompareTo(Priority(world.PlayerRoles[second]));
            if (roleComparison != 0)
            {
                return roleComparison;
            }
            int distanceComparison = PlayerProximity.DistanceSquaredMeters(
                    world.Positions[first],
                    world.OwnGoal(teamId))
                .CompareTo(PlayerProximity.DistanceSquaredMeters(
                    world.Positions[second],
                    world.OwnGoal(teamId)));
            return distanceComparison != 0
                ? distanceComparison
                : FootballIntentPlanner.ComparePlayerIds(first, second);
        });
        if (eligible.Count > dynamicRequired)
        {
            eligible.RemoveRange(dynamicRequired, eligible.Count - dynamicRequired);
        }
        return eligible;
    }

    private static int CountDangerousOpponents(FootballWorldSnapshot world, StringName teamId)
    {
        float direction = world.AttackDirection(teamId);
        int count = 0;
        foreach ((StringName playerId, Vector2 position) in world.Positions)
        {
            if (world.PlayerTeams[playerId] == teamId || world.PlayerRoles[playerId] == "GK")
            {
                continue;
            }
            if (direction * (position.X - world.BallPosition.X) < 0.08f)
            {
                count++;
            }
        }
        return count;
    }

    private static int Priority(string role)
    {
        return role switch
        {
            "CB" => 0,
            "DM" => 1,
            "LB" or "RB" => 2,
            "CM" => 3,
            _ => 4
        };
    }
}
