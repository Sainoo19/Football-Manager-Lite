using System.Collections.Generic;
using Godot;

public sealed class PlayerCollisionResolver
{
    public const float MinimumSeparationMeters = 0.70f;
    private const float AvoidanceDistanceMeters = 1.6f;
    private const float PredictionSeconds = 0.35f;

    public Vector2 AvoidPlayers(
        StringName playerId,
        Vector2 position,
        Vector2 desiredVelocity,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> velocities)
    {
        Vector2 steering = Vector2.Zero;
        Vector2 positionMeters = FootballPitchDimensions.ToMeters(position);
        foreach ((StringName otherId, Vector2 otherPosition) in positions)
        {
            if (otherId == playerId)
            {
                continue;
            }
            Vector2 offset = FootballPitchDimensions.ToMeters(otherPosition) - positionMeters;
            float distance = offset.Length();
            if (distance >= AvoidanceDistanceMeters || distance < 0.01f)
            {
                continue;
            }
            Vector2 otherVelocity = velocities.TryGetValue(otherId, out Vector2 velocity) ? velocity : Vector2.Zero;
            Vector2 relativeVelocity = desiredVelocity - otherVelocity;
            if (relativeVelocity.Dot(offset) <= 0f)
            {
                continue;
            }
            float encounterTime = relativeVelocity.LengthSquared() > 0.01f
                ? Mathf.Clamp(offset.Dot(relativeVelocity) / relativeVelocity.LengthSquared(), 0f, PredictionSeconds)
                : 0f;
            Vector2 predictedOffset = offset - relativeVelocity * encounterTime;
            if (predictedOffset.Length() >= MinimumSeparationMeters + 0.25f)
            {
                continue;
            }
            // Each runner passes on its own right, giving an opposing pair complementary steering.
            Vector2 side = desiredVelocity.LengthSquared() > 0.01f
                ? desiredVelocity.Normalized().Orthogonal()
                : offset.Normalized().Orthogonal();
            steering += side * (AvoidanceDistanceMeters - distance) * 1.5f;
        }
        Vector2 adjusted = desiredVelocity + steering;
        return adjusted.LimitLength(desiredVelocity.Length());
    }

    public void Resolve(
        IDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> previousPositions,
        IDictionary<StringName, Vector2> velocities,
        IReadOnlyList<StringName> orderedPlayers)
    {
        for (int iteration = 0; iteration < 2; iteration++)
        {
            for (int firstIndex = 0; firstIndex < orderedPlayers.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < orderedPlayers.Count; secondIndex++)
                {
                    ResolvePair(positions, previousPositions, velocities,
                        orderedPlayers[firstIndex], orderedPlayers[secondIndex], iteration == 0);
                }
            }
        }
    }

    private static void ResolvePair(
        IDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> previousPositions,
        IDictionary<StringName, Vector2> velocities,
        StringName firstId,
        StringName secondId,
        bool checkTravel)
    {
        Vector2 first = FootballPitchDimensions.ToMeters(positions[firstId]);
        Vector2 second = FootballPitchDimensions.ToMeters(positions[secondId]);
        Vector2 separation = second - first;
        float distance = separation.Length();
        bool crossedBodies = false;
        if (checkTravel && distance >= MinimumSeparationMeters)
        {
            Vector2 previousFirst = FootballPitchDimensions.ToMeters(previousPositions[firstId]);
            Vector2 previousSecond = FootballPitchDimensions.ToMeters(previousPositions[secondId]);
            Vector2 originalSeparation = previousSecond - previousFirst;
            Vector2 relativeTravel = separation - originalSeparation;
            float closestTime = relativeTravel.LengthSquared() > 0.0001f
                ? Mathf.Clamp(-originalSeparation.Dot(relativeTravel) / relativeTravel.LengthSquared(), 0f, 1f)
                : 0f;
            if (closestTime > 0f && closestTime < 1f &&
                (originalSeparation + relativeTravel * closestTime).Length() < MinimumSeparationMeters)
            {
                // Stop the crossing before the two bodies swap sides in a single simulation step.
                crossedBodies = true;
                first = previousFirst;
                second = previousSecond;
                separation = originalSeparation;
                distance = separation.Length();
            }
        }
        if (distance >= MinimumSeparationMeters && !crossedBodies)
        {
            return;
        }
        Vector2 direction = distance > 0.001f ? separation / distance : Vector2.Down;
        float correction = Mathf.Max(0f, MinimumSeparationMeters - distance) * 0.5f + 0.001f;
        Vector2 correctedFirst = ClampMeters(first - direction * correction);
        Vector2 correctedSecond = ClampMeters(second + direction * correction);
        float remainingOverlap = MinimumSeparationMeters - correctedFirst.DistanceTo(correctedSecond);
        if (remainingOverlap > 0f)
        {
            // Redistribute the correction when a touchline prevents one of the players moving outward.
            correctedSecond = ClampMeters(correctedSecond + direction * (remainingOverlap + 0.001f));
            remainingOverlap = MinimumSeparationMeters - correctedFirst.DistanceTo(correctedSecond);
            if (remainingOverlap > 0f)
            {
                correctedFirst = ClampMeters(correctedFirst - direction * (remainingOverlap + 0.001f));
            }
        }
        positions[firstId] = FootballPitchDimensions.ToNormalized(correctedFirst);
        positions[secondId] = FootballPitchDimensions.ToNormalized(correctedSecond);
        RemoveClosingVelocity(velocities, firstId, direction);
        RemoveClosingVelocity(velocities, secondId, -direction);
    }

    private static Vector2 ClampMeters(Vector2 position)
    {
        return FootballPitchDimensions.ToMeters(
            PlayerPitchBoundary.Clamp(FootballPitchDimensions.ToNormalized(position)));
    }

    private static void RemoveClosingVelocity(IDictionary<StringName, Vector2> velocities, StringName id, Vector2 towardOther)
    {
        Vector2 velocity = velocities[id];
        float closingSpeed = velocity.Dot(towardOther);
        if (closingSpeed > 0f)
        {
            velocities[id] = velocity - towardOther * closingSpeed;
        }
    }
}
