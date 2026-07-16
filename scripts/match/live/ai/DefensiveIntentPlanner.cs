using System.Collections.Generic;
using Godot;

public static class DefensiveIntentPlanner
{
    public static void Plan(
        FootballWorldSnapshot world,
        StringName teamId,
        Dictionary<StringName, PlayerIntent> intents)
    {
        LiveTeamPhase phase = LiveTeamPhase.Defending;
        List<StringName> outfieldPlayers = FootballIntentPlanner.TeamOutfieldPlayers(world, teamId);
        outfieldPlayers.Sort((first, second) =>
            PlayerProximity.DistanceSquaredMeters(world.Positions[first], world.BallPosition)
                .CompareTo(PlayerProximity.DistanceSquaredMeters(world.Positions[second], world.BallPosition)));

        StringName presser = outfieldPlayers.Count > 0 ? outfieldPlayers[0] : new StringName();
        StringName coverPlayer = outfieldPlayers.Count > 1 ? outfieldPlayers[1] : new StringName();
        HashSet<StringName> assigned = new();
        AssignPressingPair(world, phase, presser, coverPlayer, intents, assigned);
        AssignMarkers(world, teamId, phase, outfieldPlayers, intents, assigned);
        AssignRemainingPlayers(world, teamId, phase, intents);
    }

    private static void AssignPressingPair(
        FootballWorldSnapshot world,
        LiveTeamPhase phase,
        StringName presser,
        StringName coverPlayer,
        Dictionary<StringName, PlayerIntent> intents,
        HashSet<StringName> assigned)
    {
        if (presser != new StringName())
        {
            assigned.Add(presser);
            intents[presser] = new PlayerIntent(
                PlayerIntentKind.PressBall,
                world.BallPosition,
                phase,
                world.BallOwnerId);
        }

        if (coverPlayer == new StringName())
        {
            return;
        }

        assigned.Add(coverPlayer);
        Vector2 coverTarget = world.BallPosition.Lerp(world.OwnGoal(world.PlayerTeams[coverPlayer]), 0.18f);
        intents[coverPlayer] = new PlayerIntent(
            PlayerIntentKind.CoverPress,
            SpaceEvaluator.ClampToPitch(coverTarget),
            phase,
            presser);
    }

    private static void AssignMarkers(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        List<StringName> outfieldPlayers,
        Dictionary<StringName, PlayerIntent> intents,
        HashSet<StringName> assigned)
    {
        List<StringName> threats = OpponentThreats(world, teamId);
        int threatIndex = 0;
        foreach (StringName playerId in outfieldPlayers)
        {
            if (assigned.Contains(playerId) || !IsDefensiveRole(world.PlayerRoles[playerId]))
            {
                continue;
            }

            while (threatIndex < threats.Count && threats[threatIndex] == world.BallOwnerId)
            {
                threatIndex++;
            }

            if (threatIndex >= threats.Count)
            {
                break;
            }

            StringName opponentId = threats[threatIndex++];
            Vector2 opponentPosition = world.Positions[opponentId];
            Vector2 goalSideOffset = (world.OwnGoal(teamId) - opponentPosition).Normalized() * 0.04f;
            intents[playerId] = new PlayerIntent(
                PlayerIntentKind.MarkOpponent,
                SpaceEvaluator.ClampToPitch(opponentPosition + goalSideOffset),
                phase,
                opponentId);
            assigned.Add(playerId);
        }
    }

    private static void AssignRemainingPlayers(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, teamId))
        {
            if (intents.ContainsKey(playerId))
            {
                continue;
            }

            if (world.PlayerRoles[playerId] == "GK")
            {
                intents[playerId] = FootballIntentPlanner.GoalkeeperIntent(world, playerId, teamId, phase);
                continue;
            }

            float shift = IsMidfieldRole(world.PlayerRoles[playerId]) ? 0.34f : 0.20f;
            intents[playerId] = new PlayerIntent(
                PlayerIntentKind.HoldShape,
                FootballIntentPlanner.ShiftBaseTowardBall(world, playerId, shift),
                phase);
        }
    }

    private static List<StringName> OpponentThreats(FootballWorldSnapshot world, StringName teamId)
    {
        List<StringName> threats = new();
        Vector2 ownGoal = world.OwnGoal(teamId);
        foreach (StringName playerId in world.Positions.Keys)
        {
            if (world.PlayerTeams[playerId] != teamId && world.PlayerRoles[playerId] != "GK")
            {
                threats.Add(playerId);
            }
        }

        threats.Sort((first, second) => world.Positions[first].DistanceSquaredTo(ownGoal)
            .CompareTo(world.Positions[second].DistanceSquaredTo(ownGoal)));
        return threats;
    }

    private static bool IsMidfieldRole(string role) => role is "DM" or "CM" or "AM";

    private static bool IsDefensiveRole(string role) => role is "CB" or "LB" or "RB" or "DM";
}
