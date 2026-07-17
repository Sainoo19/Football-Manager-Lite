using System.Collections.Generic;
using Godot;

public sealed class ThroughBallTargetPlanner
{
    private const float MinimumLeadMeters = 6f;
    private const float MaximumLeadMeters = 11f;

    public Vector2 FindTarget(
        Vector2 ballPosition,
        Vector2 receiverPosition,
        Vector2 desiredRunTarget,
        float attackDirection,
        StringName attackingTeamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        Vector2 receiverMeters = FootballPitchDimensions.ToMeters(receiverPosition);
        Vector2 desiredMeters = FootballPitchDimensions.ToMeters(desiredRunTarget);
        float desiredLead = attackDirection * (desiredMeters.X - receiverMeters.X);
        float leadMeters = Mathf.Clamp(desiredLead, MinimumLeadMeters, MaximumLeadMeters);
        float targetX = receiverMeters.X + attackDirection * leadMeters;
        float preferredY = desiredMeters.Y;
        float[] lateralOffsetsMeters = { 0f, -4.5f, 4.5f, -7f, 7f };
        Vector2 bestTarget = new(targetX, preferredY);
        float bestScore = float.NegativeInfinity;

        foreach (float lateralOffset in lateralOffsetsMeters)
        {
            Vector2 candidateMeters = new(targetX, preferredY + lateralOffset);
            Vector2 candidate = SpaceEvaluator.ClampToPitch(
                FootballPitchDimensions.ToNormalized(candidateMeters));
            candidateMeters = FootballPitchDimensions.ToMeters(candidate);
            float targetClearance = NearestOpponentDistance(
                candidateMeters,
                attackingTeamId,
                positions,
                playerTeams);
            float laneClearance = NearestOpponentToSegment(
                FootballPitchDimensions.ToMeters(ballPosition),
                candidateMeters,
                attackingTeamId,
                positions,
                playerTeams);
            float lateralCost = Mathf.Abs(candidateMeters.Y - receiverMeters.Y);
            float score = Mathf.Min(targetClearance, 9f) * 0.42f +
                          Mathf.Min(laneClearance, 7f) * 0.30f -
                          lateralCost * 0.16f;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidateMeters;
            }
        }

        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(bestTarget));
    }

    private static float NearestOpponentDistance(
        Vector2 pointMeters,
        StringName attackingTeamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        float nearestDistance = float.PositiveInfinity;
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (playerTeams[playerId] == attackingTeamId)
            {
                continue;
            }
            nearestDistance = Mathf.Min(
                nearestDistance,
                pointMeters.DistanceTo(FootballPitchDimensions.ToMeters(position)));
        }
        return nearestDistance;
    }

    private static float NearestOpponentToSegment(
        Vector2 startMeters,
        Vector2 endMeters,
        StringName attackingTeamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        float nearestDistance = float.PositiveInfinity;
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (playerTeams[playerId] == attackingTeamId)
            {
                continue;
            }
            nearestDistance = Mathf.Min(
                nearestDistance,
                DistanceToSegment(FootballPitchDimensions.ToMeters(position), startMeters, endMeters));
        }
        return nearestDistance;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return point.DistanceTo(start);
        }
        float progress = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * progress);
    }
}
