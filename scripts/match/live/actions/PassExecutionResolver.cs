using Godot;

public readonly struct PassExecution
{
    public PassExecution(
        Vector2 intendedTarget,
        Vector2 actualTarget,
        float durationSeconds,
        float ballSpeedMetersPerSecond,
        float quality)
    {
        IntendedTarget = intendedTarget;
        ActualTarget = actualTarget;
        DurationSeconds = durationSeconds;
        BallSpeedMetersPerSecond = ballSpeedMetersPerSecond;
        Quality = quality;
    }

    public Vector2 IntendedTarget { get; }
    public Vector2 ActualTarget { get; }
    public float DurationSeconds { get; }
    public float BallSpeedMetersPerSecond { get; }
    public float Quality { get; }
}

public sealed class PassExecutionResolver
{
    private const float MinimumBallSpeedMetersPerSecond = 9.5f;
    private const float MaximumBallSpeedMetersPerSecond = 26f;

    public PassExecution Resolve(
        Vector2 ballPosition,
        Vector2 intendedTarget,
        LivePassType passType,
        int passing,
        int technique,
        int composure,
        int form,
        float pressureDistanceMeters,
        float directionRoll,
        float powerRoll)
    {
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 intendedMeters = FootballPitchDimensions.ToMeters(intendedTarget);
        Vector2 passVector = intendedMeters - ballMeters;
        float distanceMeters = passVector.Length();
        if (distanceMeters <= 0.05f)
        {
            return new PassExecution(intendedTarget, intendedTarget, 0.32f, MinimumBallSpeedMetersPerSecond, 1f);
        }

        float technicalRating = passing * 0.44f + technique * 0.28f + composure * 0.18f + form * 0.10f;
        float pressure = pressureDistanceMeters switch
        {
            <= 1.6f => 1f,
            >= 6f => 0f,
            _ => 1f - (pressureDistanceMeters - 1.6f) / 4.4f
        };
        float distanceDifficulty = Mathf.Clamp((distanceMeters - 8f) / 38f, 0f, 1f);
        float passTypeDifficulty = passType switch
        {
            LivePassType.ThroughBall => 0.12f,
            LivePassType.Lofted => 0.14f,
            LivePassType.Cross => 0.18f,
            _ => 0f
        };
        float quality = Mathf.Clamp(
            technicalRating / 99f - pressure * 0.24f - distanceDifficulty * 0.16f - passTypeDifficulty,
            0.12f,
            0.98f);

        Vector2 forward = passVector / distanceMeters;
        Vector2 lateral = new(-forward.Y, forward.X);
        float lateralErrorLimit = Mathf.Lerp(3.8f, 0.22f, quality) * Mathf.Lerp(0.75f, 1.25f, distanceDifficulty);
        float powerErrorLimit = Mathf.Lerp(5.8f, 0.30f, quality) * Mathf.Lerp(0.65f, 1.30f, distanceDifficulty);
        float lateralError = SignedRoll(directionRoll) * lateralErrorLimit;
        float powerError = SignedRoll(powerRoll) * powerErrorLimit;
        Vector2 actualMeters = intendedMeters + lateral * lateralError + forward * powerError;
        Vector2 normalizedTarget = FootballPitchDimensions.ToNormalized(actualMeters);
        Vector2 actualTarget = new(
            Mathf.Clamp(normalizedTarget.X, -0.025f, 1.025f),
            Mathf.Clamp(normalizedTarget.Y, -0.035f, 1.035f));

        float baseSpeed = passType switch
        {
            LivePassType.ThroughBall => 19.5f,
            LivePassType.Lofted => 19f,
            LivePassType.Cross => 24f,
            _ => 18f
        };
        float speedFactor = 0.88f + quality * 0.16f + SignedRoll(powerRoll) * (1f - quality) * 0.16f;
        float ballSpeed = Mathf.Clamp(baseSpeed * speedFactor, MinimumBallSpeedMetersPerSecond, MaximumBallSpeedMetersPerSecond);
        float actualDistance = FootballPitchDimensions.DistanceMeters(ballPosition, actualTarget);
        float duration = Mathf.Clamp(actualDistance / ballSpeed, 0.32f, 1.65f);
        return new PassExecution(intendedTarget, actualTarget, duration, ballSpeed, quality);
    }

    private static float SignedRoll(float roll)
    {
        return Mathf.Clamp(roll, 0f, 1f) * 2f - 1f;
    }
}
