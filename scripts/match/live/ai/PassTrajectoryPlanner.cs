using Godot;

public enum LivePassType
{
    Standard,
    ThroughBall,
    Cross
}

public readonly struct PassTrajectory
{
    public PassTrajectory(Vector2 target, float duration, float visualLift)
    {
        Target = target;
        Duration = duration;
        VisualLift = visualLift;
    }

    public Vector2 Target { get; }
    public float Duration { get; }
    public float VisualLift { get; }
}

public sealed class PassTrajectoryPlanner
{
    private const float ReceiverRunSpeedMetersPerSecond = 6.4f;

    public PassTrajectory Plan(
        Vector2 ballPosition,
        Vector2 receiverPosition,
        Vector2 receiverRunTarget,
        LivePassType passType)
    {
        float ballSpeed = passType switch
        {
            LivePassType.ThroughBall => 19.5f,
            LivePassType.Cross => 24f,
            _ => 18f
        };
        float maximumLeadMeters = passType switch
        {
            LivePassType.ThroughBall => 8f,
            LivePassType.Cross => 4.2f,
            _ => 1.2f
        };

        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 receiverMeters = FootballPitchDimensions.ToMeters(receiverPosition);
        Vector2 runTargetMeters = FootballPitchDimensions.ToMeters(receiverRunTarget);
        float directFlightDuration = Mathf.Clamp(ballMeters.DistanceTo(receiverMeters) / ballSpeed, 0.32f, 1.35f);
        float anticipationFactor = passType == LivePassType.ThroughBall ? 0.86f : 0.72f;
        float reachableLeadMeters = ReceiverRunSpeedMetersPerSecond * directFlightDuration * anticipationFactor;
        float leadMeters = Mathf.Min(maximumLeadMeters, reachableLeadMeters);
        Vector2 targetMeters = receiverMeters.MoveToward(runTargetMeters, leadMeters);
        Vector2 target = SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(targetMeters));
        float duration = Mathf.Clamp(
            FootballPitchDimensions.DistanceMeters(ballPosition, target) / ballSpeed,
            0.32f,
            1.35f);
        float visualLift = passType switch
        {
            LivePassType.ThroughBall => 0.028f,
            LivePassType.Cross => 0.060f,
            _ => 0.014f
        };
        return new PassTrajectory(target, duration, visualLift);
    }
}
