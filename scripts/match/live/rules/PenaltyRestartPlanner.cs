using Godot;

public readonly struct PenaltyRestartPlan
{
    public PenaltyRestartPlan(Vector2 ballStart, Vector2 penaltySpot)
    {
        BallStart = ballStart;
        PenaltySpot = penaltySpot;
    }

    public Vector2 BallStart { get; }
    public Vector2 PenaltySpot { get; }

    public Vector2 BallPositionAt(float elapsedSeconds)
    {
        if (elapsedSeconds <= PenaltyRestartPlanner.BallMovementStartsAfterSeconds)
        {
            return BallStart;
        }
        if (elapsedSeconds >= PenaltyRestartPlanner.BallPlacedAfterSeconds)
        {
            return PenaltySpot;
        }

        float progress = (elapsedSeconds - PenaltyRestartPlanner.BallMovementStartsAfterSeconds) /
                         (PenaltyRestartPlanner.BallPlacedAfterSeconds -
                          PenaltyRestartPlanner.BallMovementStartsAfterSeconds);
        return BallStart.Lerp(PenaltySpot, Mathf.Clamp(progress, 0f, 1f));
    }

    public bool IsBallPlaced(float elapsedSeconds)
    {
        return elapsedSeconds >= PenaltyRestartPlanner.BallPlacedAfterSeconds;
    }
}

public sealed class PenaltyRestartPlanner
{
    public const float PreparationDurationSeconds = 8f;
    public const float BallMovementStartsAfterSeconds = 1.2f;
    public const float BallPlacedAfterSeconds = 4.2f;
    public const float RequiredDistanceMeters = 9.15f;
    private readonly PenaltyAreaRule _penaltyAreaRule = new();

    public PenaltyRestartPlan CreatePlan(Vector2 ballStart, float defendingGoalX)
    {
        float spotX = defendingGoalX < 0.5f
            ? FootballPitchDimensions.PenaltySpotDistanceMeters / FootballPitchDimensions.LengthMeters
            : 1f - FootballPitchDimensions.PenaltySpotDistanceMeters / FootballPitchDimensions.LengthMeters;
        return new PenaltyRestartPlan(ballStart, new Vector2(spotX, 0.5f));
    }

    public Vector2 PositionTaker(Vector2 penaltySpot, float defendingGoalX)
    {
        float stepBack = 1.4f / FootballPitchDimensions.LengthMeters;
        return penaltySpot + new Vector2(defendingGoalX < 0.5f ? stepBack : -stepBack, 0f);
    }

    public Vector2 PositionGoalkeeper(float defendingGoalX)
    {
        return new Vector2(defendingGoalX < 0.5f ? 0.015f : 0.985f, 0.5f);
    }

    public Vector2 EnsureOutsidePenaltyAreaAndArc(
        Vector2 playerPosition,
        Vector2 penaltySpot,
        float defendingGoalX)
    {
        float penaltyBoundaryX = defendingGoalX < 0.5f
            ? FootballPitchDimensions.PenaltyAreaDepthMeters / FootballPitchDimensions.LengthMeters
            : 1f - FootballPitchDimensions.PenaltyAreaDepthMeters / FootballPitchDimensions.LengthMeters;
        bool isInsideArea = _penaltyAreaRule.IsInsideDefendingPenaltyArea(playerPosition, defendingGoalX);
        Vector2 legalPosition = isInsideArea
            ? new Vector2(
                defendingGoalX < 0.5f ? penaltyBoundaryX + 0.008f : penaltyBoundaryX - 0.008f,
                playerPosition.Y)
            : playerPosition;

        Vector2 playerMeters = FootballPitchDimensions.ToMeters(legalPosition);
        Vector2 spotMeters = FootballPitchDimensions.ToMeters(penaltySpot);
        Vector2 fromSpot = playerMeters - spotMeters;
        float distanceMeters = fromSpot.Length();
        if (distanceMeters >= RequiredDistanceMeters)
        {
            return SpaceEvaluator.ClampToPitch(legalPosition);
        }

        Vector2 direction = distanceMeters > 0.05f
            ? fromSpot / distanceMeters
            : new Vector2(defendingGoalX < 0.5f ? 1f : -1f, 0f);
        return SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(spotMeters + direction * RequiredDistanceMeters));
    }
}
