using Godot;

public readonly struct RollingBallStep
{
    public RollingBallStep(Vector2 position, Vector2 velocityMetersPerSecond)
    {
        Position = position;
        VelocityMetersPerSecond = velocityMetersPerSecond;
    }

    public Vector2 Position { get; }
    public Vector2 VelocityMetersPerSecond { get; }
}

public sealed class RollingBallPhysics
{
    public const float DecelerationMetersPerSecondSquared = 3.2f;
    private const float StopSpeedMetersPerSecond = 0.05f;

    public RollingBallStep Advance(
        Vector2 position,
        Vector2 velocityMetersPerSecond,
        float delta)
    {
        if (delta <= 0f || velocityMetersPerSecond.Length() <= StopSpeedMetersPerSecond)
        {
            return new RollingBallStep(position, Vector2.Zero);
        }

        Vector2 nextVelocity = velocityMetersPerSecond.MoveToward(
            Vector2.Zero,
            DecelerationMetersPerSecondSquared * delta);
        Vector2 averageVelocity = (velocityMetersPerSecond + nextVelocity) * 0.5f;
        Vector2 nextPositionMeters = FootballPitchDimensions.ToMeters(position) + averageVelocity * delta;
        return new RollingBallStep(
            FootballPitchDimensions.ToNormalized(nextPositionMeters),
            nextVelocity);
    }
}
