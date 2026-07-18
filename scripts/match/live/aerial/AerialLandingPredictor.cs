using Godot;

public readonly struct AerialArrivalEstimate
{
    public AerialArrivalEstimate(
        float distanceMeters,
        float arrivalTimeSeconds,
        float arrivalMarginSeconds,
        bool canContest)
    {
        DistanceMeters = distanceMeters;
        ArrivalTimeSeconds = arrivalTimeSeconds;
        ArrivalMarginSeconds = arrivalMarginSeconds;
        CanContest = canContest;
    }

    public float DistanceMeters { get; }
    public float ArrivalTimeSeconds { get; }
    public float ArrivalMarginSeconds { get; }
    public bool CanContest { get; }
}

public sealed class AerialLandingPredictor
{
    private const float MinimumRunSpeedMetersPerSecond = 4.8f;
    private const float MaximumRunSpeedMetersPerSecond = 8.2f;
    private const float ContestReachAllowanceMeters = 1.8f;

    public AerialArrivalEstimate Estimate(
        Vector2 playerPosition,
        Vector2 landingPoint,
        int pace,
        float remainingFlightTimeSeconds)
    {
        float distanceMeters = FootballPitchDimensions.DistanceMeters(playerPosition, landingPoint);
        float speed = Mathf.Lerp(
            MinimumRunSpeedMetersPerSecond,
            MaximumRunSpeedMetersPerSecond,
            Mathf.Clamp(pace, 1, 99) / 99f);
        float arrivalTime = distanceMeters / speed;
        float margin = remainingFlightTimeSeconds - arrivalTime;
        bool canContest = distanceMeters <=
                          speed * Mathf.Max(remainingFlightTimeSeconds, 0f) + ContestReachAllowanceMeters;
        return new AerialArrivalEstimate(distanceMeters, arrivalTime, margin, canContest);
    }
}
