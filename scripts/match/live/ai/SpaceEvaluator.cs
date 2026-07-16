using System;
using System.Collections.Generic;
using Godot;

public static class SpaceEvaluator
{
    private const float MinimumX = 0.025f;
    private const float MaximumX = 0.975f;
    private const float MinimumY = 0.035f;
    private const float MaximumY = 0.965f;

    public static float OpponentPressure(
        Vector2 point,
        StringName teamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        float pressure = 0f;
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (playerTeams[playerId] == teamId)
            {
                continue;
            }

            float distance = point.DistanceTo(position);
            if (distance < 0.18f)
            {
                pressure += 1f - distance / 0.18f;
            }
        }

        return pressure;
    }

    public static float PassingLaneRisk(
        Vector2 from,
        Vector2 to,
        StringName teamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        float highestRisk = 0f;
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (playerTeams[playerId] == teamId)
            {
                continue;
            }

            float distance = DistanceToSegment(position, from, to);
            highestRisk = Mathf.Max(highestRisk, 1f - Mathf.Clamp(distance / 0.11f, 0f, 1f));
        }

        return highestRisk;
    }

    public static Vector2 ClampToPitch(Vector2 position) => new(
        Mathf.Clamp(position.X, MinimumX, MaximumX),
        Mathf.Clamp(position.Y, MinimumY, MaximumY));

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f)
        {
            return point.DistanceTo(start);
        }

        float progress = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * progress);
    }
}
