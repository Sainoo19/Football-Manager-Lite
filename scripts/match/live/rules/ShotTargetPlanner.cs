using Godot;

public sealed class ShotTargetPlanner
{
    private const float MinimumGoalkeeperCoverage = 0.05f;
    private const float ShotLaneCoverageMeters = 4.2f;

    public Vector2 ChooseGoalTarget(float goalX, Vector2 goalkeeperPosition, float sideRoll)
    {
        float goalkeeperOffset = goalkeeperPosition.Y - 0.5f;
        float targetSide = Mathf.Abs(goalkeeperOffset) > 0.012f
            ? -Mathf.Sign(goalkeeperOffset)
            : sideRoll < 0.5f ? -1f : 1f;
        float halfGoalWidth = FootballPitchDimensions.GoalWidthMeters /
                              FootballPitchDimensions.WidthMeters * 0.5f;
        return new Vector2(goalX, 0.5f + targetSide * halfGoalWidth * 0.62f);
    }

    public float GoalkeeperCoverage(
        Vector2 shooterPosition,
        Vector2 goalTarget,
        Vector2 goalkeeperPosition)
    {
        Vector2 shooterMeters = FootballPitchDimensions.ToMeters(shooterPosition);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(goalTarget);
        Vector2 goalkeeperMeters = FootballPitchDimensions.ToMeters(goalkeeperPosition);
        float laneDistanceMeters = DistanceToSegment(goalkeeperMeters, shooterMeters, targetMeters);
        float laneCoverage = 1f - laneDistanceMeters / ShotLaneCoverageMeters;
        float distanceFromGoalLineMeters = Mathf.Abs(goalkeeperPosition.X - goalTarget.X) *
                                           FootballPitchDimensions.LengthMeters;
        float excessiveDepthPenalty = Mathf.Max(distanceFromGoalLineMeters - 10f, 0f) / 22f;
        return Mathf.Clamp(laneCoverage - excessiveDepthPenalty, MinimumGoalkeeperCoverage, 1f);
    }

    public Vector2 ChooseOffTargetDestination(
        float goalX,
        float intendedTargetY,
        int finishing,
        float distanceMeters,
        float missRoll)
    {
        float difficulty = Mathf.Clamp(
            distanceMeters / 32f * 0.55f +
            Mathf.Max(70 - finishing, 0) / 70f * 0.25f +
            missRoll * 0.20f,
            0f,
            1f);
        float missDistanceMeters = Mathf.Lerp(0.45f, 2.8f, difficulty);
        float targetSide = intendedTargetY < 0.5f ? -1f : 1f;
        float halfGoalWidthMeters = FootballPitchDimensions.GoalWidthMeters * 0.5f;
        float missY = 0.5f + targetSide *
            (halfGoalWidthMeters + missDistanceMeters) / FootballPitchDimensions.WidthMeters;
        float outOfPlayX = goalX < 0.5f ? -0.012f : 1.012f;
        return new Vector2(outOfPlayX, missY);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point.DistanceTo(segmentStart);
        }

        float projection = Mathf.Clamp((point - segmentStart).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(segmentStart + segment * projection);
    }
}
