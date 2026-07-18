using Godot;

public enum AerialDeliveryType
{
    LoftedPass,
    Cross,
    Clearance,
    HeaderPass,
    HeaderClearance
}

public readonly struct AerialBallSample
{
    public AerialBallSample(
        Vector2 position,
        float heightMeters,
        float verticalVelocityMetersPerSecond,
        bool hasLanded)
    {
        Position = position;
        HeightMeters = heightMeters;
        VerticalVelocityMetersPerSecond = verticalVelocityMetersPerSecond;
        HasLanded = hasLanded;
    }

    public Vector2 Position { get; }
    public float HeightMeters { get; }
    public float VerticalVelocityMetersPerSecond { get; }
    public bool HasLanded { get; }
}

public readonly struct AerialBallTrajectory
{
    public AerialBallTrajectory(
        Vector2 start,
        Vector2 landingPoint,
        float flightTimeSeconds,
        float initialVerticalVelocityMetersPerSecond,
        float gravityMetersPerSecondSquared,
        AerialDeliveryType deliveryType)
    {
        Start = start;
        LandingPoint = landingPoint;
        FlightTimeSeconds = flightTimeSeconds;
        InitialVerticalVelocityMetersPerSecond = initialVerticalVelocityMetersPerSecond;
        GravityMetersPerSecondSquared = gravityMetersPerSecondSquared;
        DeliveryType = deliveryType;
    }

    public Vector2 Start { get; }
    public Vector2 LandingPoint { get; }
    public float FlightTimeSeconds { get; }
    public float InitialVerticalVelocityMetersPerSecond { get; }
    public float GravityMetersPerSecondSquared { get; }
    public AerialDeliveryType DeliveryType { get; }

    public float ApexHeightMeters =>
        InitialVerticalVelocityMetersPerSecond * InitialVerticalVelocityMetersPerSecond /
        (2f * GravityMetersPerSecondSquared);

    public AerialBallSample Sample(float elapsedSeconds)
    {
        float time = Mathf.Clamp(elapsedSeconds, 0f, FlightTimeSeconds);
        float progress = FlightTimeSeconds <= 0f ? 1f : time / FlightTimeSeconds;
        Vector2 position = Start.Lerp(LandingPoint, progress);
        float height = Mathf.Max(
            0f,
            InitialVerticalVelocityMetersPerSecond * time -
            0.5f * GravityMetersPerSecondSquared * time * time);
        float verticalVelocity = InitialVerticalVelocityMetersPerSecond -
                                 GravityMetersPerSecondSquared * time;
        return new AerialBallSample(
            position,
            height,
            verticalVelocity,
            elapsedSeconds >= FlightTimeSeconds);
    }
}

public sealed class AerialBallTrajectoryPlanner
{
    private const float GravityMetersPerSecondSquared = 9.81f;

    public AerialBallTrajectory Plan(
        Vector2 start,
        Vector2 landingPoint,
        AerialDeliveryType deliveryType)
    {
        float distanceMeters = FootballPitchDimensions.DistanceMeters(start, landingPoint);
        float horizontalSpeedMetersPerSecond = deliveryType switch
        {
            AerialDeliveryType.LoftedPass => 19f,
            AerialDeliveryType.Cross => 21f,
            AerialDeliveryType.Clearance => 24f,
            AerialDeliveryType.HeaderPass => 13f,
            _ => 17f
        };
        float minimumFlightTime = deliveryType switch
        {
            AerialDeliveryType.LoftedPass => 0.95f,
            AerialDeliveryType.Cross => 1.05f,
            AerialDeliveryType.Clearance => 1.20f,
            AerialDeliveryType.HeaderPass => 0.58f,
            _ => 0.72f
        };
        float maximumFlightTime = deliveryType switch
        {
            AerialDeliveryType.Clearance => 2.35f,
            AerialDeliveryType.Cross => 1.95f,
            _ => 1.75f
        };
        float flightTime = Mathf.Clamp(
            distanceMeters / horizontalSpeedMetersPerSecond,
            minimumFlightTime,
            maximumFlightTime);
        float initialVerticalVelocity = GravityMetersPerSecondSquared * flightTime * 0.5f;
        Vector2 finalLandingPoint = deliveryType is AerialDeliveryType.Clearance or
            AerialDeliveryType.HeaderClearance
            ? new Vector2(
                Mathf.Clamp(landingPoint.X, -0.025f, 1.025f),
                Mathf.Clamp(landingPoint.Y, -0.035f, 1.035f))
            : SpaceEvaluator.ClampToPitch(landingPoint);
        return new AerialBallTrajectory(
            start,
            finalLandingPoint,
            flightTime,
            initialVerticalVelocity,
            GravityMetersPerSecondSquared,
            deliveryType);
    }
}
