using System.Collections.Generic;
using Godot;

public static class TeamSpacingResolver
{
    private const float MinimumSpacingMeters = 5.5f;
    private const int ResolutionIterations = 4;

    public static void Resolve(
        FootballWorldSnapshot world,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> playerIds = new(intents.Keys);
        for (int iteration = 0; iteration < ResolutionIterations; iteration++)
        {
            for (int firstIndex = 0; firstIndex < playerIds.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < playerIds.Count; secondIndex++)
                {
                    StringName firstId = playerIds[firstIndex];
                    StringName secondId = playerIds[secondIndex];
                    if (world.PlayerTeams[firstId] != world.PlayerTeams[secondId])
                    {
                        continue;
                    }

                    SeparatePair(world, intents, firstId, secondId);
                }
            }
        }
    }

    private static void SeparatePair(
        FootballWorldSnapshot world,
        Dictionary<StringName, PlayerIntent> intents,
        StringName firstId,
        StringName secondId)
    {
        PlayerIntent firstIntent = intents[firstId];
        PlayerIntent secondIntent = intents[secondId];
        Vector2 firstMeters = FootballPitchDimensions.ToMeters(firstIntent.Target);
        Vector2 secondMeters = FootballPitchDimensions.ToMeters(secondIntent.Target);
        Vector2 difference = secondMeters - firstMeters;
        float distance = difference.Length();
        if (distance >= MinimumSpacingMeters)
        {
            return;
        }

        Vector2 direction = distance > 0.01f
            ? difference / distance
            : SeparationDirection(world, firstId, secondId);
        bool firstAnchored = IsAnchored(firstIntent.Kind);
        bool secondAnchored = IsAnchored(secondIntent.Kind);
        if (firstAnchored && secondAnchored)
        {
            return;
        }

        float missingDistance = MinimumSpacingMeters - distance;
        float firstPush = firstAnchored ? 0f : secondAnchored ? missingDistance : missingDistance * 0.5f;
        float secondPush = secondAnchored ? 0f : firstAnchored ? missingDistance : missingDistance * 0.5f;
        if (firstPush > 0f)
        {
            intents[firstId] = WithTarget(firstIntent, firstMeters - direction * firstPush);
        }
        if (secondPush > 0f)
        {
            intents[secondId] = WithTarget(secondIntent, secondMeters + direction * secondPush);
        }
    }

    private static Vector2 SeparationDirection(
        FootballWorldSnapshot world,
        StringName firstId,
        StringName secondId)
    {
        Vector2 baseDifference = FootballPitchDimensions.ToMeters(world.BasePositions[secondId]) -
                                 FootballPitchDimensions.ToMeters(world.BasePositions[firstId]);
        if (baseDifference.LengthSquared() > 0.01f)
        {
            return baseDifference.Normalized();
        }

        return string.CompareOrdinal(firstId.ToString(), secondId.ToString()) <= 0
            ? Vector2.Down
            : Vector2.Up;
    }

    private static PlayerIntent WithTarget(PlayerIntent intent, Vector2 targetMeters)
    {
        return new PlayerIntent(
            intent.Kind,
            SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(targetMeters)),
            intent.TeamPhase,
            intent.RelatedPlayerId);
    }

    private static bool IsAnchored(PlayerIntentKind kind) => kind is
        PlayerIntentKind.Goalkeep or
        PlayerIntentKind.CarryBall or
        PlayerIntentKind.ReceivePass or
        PlayerIntentKind.PressBall or
        PlayerIntentKind.ChaseLooseBall;
}
