using System;
using System.Collections.Generic;
using Godot;

public sealed class TouchlineRestartPlanner
{
    public const float TakerControlDistanceMeters = 0.8f;
    public const float ThrowInDefenderDistanceMeters = 2f;
    public const float CornerDefenderDistanceMeters = 9.15f;
    public const float MinimumThrowDistanceMeters = 3f;
    public const float MaximumThrowDistanceMeters = 24f;
    // Two players cannot stand closer than the body separation, so restart positions must not overlap either.
    public const float RestartPositionSpacingMeters = PlayerCollisionResolver.MinimumSeparationMeters + 0.3f;

    public Vector2 PlaceCorner(Vector2 previousPosition)
    {
        return new Vector2(previousPosition.X < 0.5f ? 0.025f : 0.975f,
            previousPosition.Y < 0.5f ? 0.035f : 0.965f);
    }

    public bool IsTakerReady(Vector2 playerPosition, Vector2 ballPosition)
    {
        return FootballPitchDimensions.DistanceMeters(playerPosition, ballPosition) <= TakerControlDistanceMeters;
    }

    public Vector2 KeepDefenderAway(Vector2 playerPosition, Vector2 ballPosition, bool isCorner)
    {
        float requiredDistance = isCorner ? CornerDefenderDistanceMeters : ThrowInDefenderDistanceMeters;
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 playerMeters = FootballPitchDimensions.ToMeters(playerPosition);
        Vector2 offset = playerMeters - ballMeters;
        if (offset.Length() >= requiredDistance + 0.2f)
        {
            return playerPosition;
        }
        Vector2 towardCenter = FootballPitchDimensions.ToMeters(new Vector2(0.5f, 0.5f)) - ballMeters;
        // At the touchline, a radial push toward the outside would be clamped back into the exclusion zone.
        Vector2 direction = offset.LengthSquared() > 0.01f && offset.Dot(towardCenter) > 0f
            ? offset.Normalized()
            : towardCenter.Normalized();
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(
            ballMeters + direction * (requiredDistance + 0.3f)));
    }

    // A defender sent to a spot another player already occupies would be held short of it, possibly inside the
    // required distance, and the restart could then never be taken. Move him further from the ball instead.
    public Vector2 SeparateDefenderPosition(
        Vector2 defenderPosition,
        Vector2 ballPosition,
        IEnumerable<Vector2> occupiedPositions)
    {
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 positionMeters = FootballPitchDimensions.ToMeters(defenderPosition);
        Vector2 offset = positionMeters - ballMeters;
        Vector2 towardCenter = FootballPitchDimensions.ToMeters(new Vector2(0.5f, 0.5f)) - ballMeters;
        Vector2 awayFromBall = offset.LengthSquared() > 0.01f ? offset.Normalized() : towardCenter.Normalized();
        List<Vector2> occupiedMeters = new();
        foreach (Vector2 occupied in occupiedPositions)
        {
            occupiedMeters.Add(FootballPitchDimensions.ToMeters(occupied));
        }
        for (int attempt = 0; attempt < 8; attempt++)
        {
            bool isFree = true;
            foreach (Vector2 occupied in occupiedMeters)
            {
                if (occupied.DistanceTo(positionMeters) < RestartPositionSpacingMeters)
                {
                    isFree = false;
                    break;
                }
            }
            if (isFree)
            {
                break;
            }
            positionMeters += awayFromBall * RestartPositionSpacingMeters;
        }
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(positionMeters));
    }

    public float ThrowReceptionScore(Vector2 ballPosition, Vector2 receiverPosition, float opponentDistanceMeters)
    {
        float distance = FootballPitchDimensions.DistanceMeters(ballPosition, receiverPosition);
        if (distance < MinimumThrowDistanceMeters || distance > MaximumThrowDistanceMeters)
        {
            return float.NegativeInfinity;
        }
        return Math.Min(opponentDistanceMeters, 10f) - distance * 0.25f;
    }
}
