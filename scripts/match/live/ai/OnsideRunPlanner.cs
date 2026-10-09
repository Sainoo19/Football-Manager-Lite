using System;
using System.Collections.Generic;
using Godot;

public static class OnsideRunPlanner
{
    private const float WaitingMarginMeters = 0.65f;
    private const float MinimumOvershootMeters = 0.5f;

    public static void ConstrainTargets(
        FootballWorldSnapshot world,
        Dictionary<StringName, PlayerIntent> intents,
        OffBallParticipationConfiguration? configuration = null)
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
            float runnerLimit = limit + EarlyRunAllowanceMeters(world, playerId, configuration) /
                FootballPitchDimensions.LengthMeters;
            if (playerId == world.BallOwnerId || !intents.TryGetValue(playerId, out PlayerIntent? intent) ||
                direction * intent.Target.X <= runnerLimit)
            {
                continue;
            }
            Vector2 target = new(direction * runnerLimit, intent.Target.Y);
            intents[playerId] = new PlayerIntent(intent.Kind, target, intent.TeamPhase,
                intent.RelatedPlayerId, intent.Assignment, SpaceOccupationMap.ZoneFor(target).Key);
        }
    }

    // Runners misjudge the line now and then: during some time windows a runner leaves too early and may be
    // caught offside if the pass comes. The window and roll are derived from stable inputs for determinism.
    public static float EarlyRunAllowanceMeters(
        FootballWorldSnapshot world,
        StringName playerId,
        OffBallParticipationConfiguration? configuration)
    {
        if (configuration is null || configuration.EarlyRunProbability <= 0f ||
            configuration.EarlyRunMaximumOvershootMeters <= 0f)
        {
            return 0f;
        }
        int window = (int)MathF.Floor(world.GameTimeSeconds / MathF.Max(configuration.EarlyRunWindowSeconds, 0.1f));
        uint hash = StableHash(playerId.ToString(), window);
        float roll = (hash & 0xffff) / 65535f;
        if (roll >= configuration.EarlyRunProbability)
        {
            return 0f;
        }
        float size = ((hash >> 16) & 0xffff) / 65535f;
        return WaitingMarginMeters +
               Mathf.Lerp(MinimumOvershootMeters, configuration.EarlyRunMaximumOvershootMeters, size);
    }

    private static uint StableHash(string value, int window)
    {
        uint hash = 2166136261u ^ unchecked((uint)window * 2654435761u);
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        hash ^= hash >> 15;
        hash *= 0x2c1b3c6du;
        hash ^= hash >> 12;
        return hash;
    }
}
