using System;
using System.Collections.Generic;
using Godot;

public static class OnsideRunPlanner
{
    private const float WaitingMarginMeters = 0.65f;

    public static void ConstrainTargets(FootballWorldSnapshot world, Dictionary<StringName, PlayerIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intents);
        // Once the ball is released, a runner may cross the line and attack the reception point.
        if (world.IsBallInFlight || world.IsLooseBall || world.BallOwnerId == new StringName())
        {
            return;
        }
        float direction = world.AttackDirection(world.PossessionTeamId);
        float deepest = float.NegativeInfinity;
        float secondDeepest = float.NegativeInfinity;
        foreach ((StringName playerId, Vector2 position) in world.Positions)
        {
            if (world.PlayerTeams[playerId] == world.PossessionTeamId)
            {
                continue;
            }
            float depth = direction * position.X;
            if (depth >= deepest)
            {
                secondDeepest = deepest;
                deepest = depth;
            }
            else if (depth > secondDeepest)
            {
                secondDeepest = depth;
            }
        }
        if (float.IsNegativeInfinity(secondDeepest))
        {
            return;
        }
        float limit = Mathf.Max(direction * 0.5f, Mathf.Max(direction * world.BallPosition.X, secondDeepest)) -
                      WaitingMarginMeters / FootballPitchDimensions.LengthMeters;
        foreach (StringName playerId in FootballIntentPlanner.TeamOutfieldPlayers(world, world.PossessionTeamId))
        {
            if (playerId == world.BallOwnerId || !intents.TryGetValue(playerId, out PlayerIntent? intent) ||
                direction * intent.Target.X <= limit)
            {
                continue;
            }
            Vector2 target = new(direction * limit, intent.Target.Y);
            intents[playerId] = new PlayerIntent(intent.Kind, target, intent.TeamPhase,
                intent.RelatedPlayerId, intent.Assignment, SpaceOccupationMap.ZoneFor(target).Key);
        }
    }
}
