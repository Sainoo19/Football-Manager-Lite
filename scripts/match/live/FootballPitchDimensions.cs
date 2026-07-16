using Godot;

public static class FootballPitchDimensions
{
    public const float LengthMeters = 105f;
    public const float WidthMeters = 68f;
    public const float AspectRatio = LengthMeters / WidthMeters;
    public const float CenterCircleRadiusMeters = 9.15f;
    public const float PenaltyAreaDepthMeters = 16.5f;
    public const float PenaltyAreaWidthMeters = 40.32f;
    public const float GoalAreaDepthMeters = 5.5f;
    public const float GoalAreaWidthMeters = 18.32f;
    public const float PenaltySpotDistanceMeters = 11f;
    public const float GoalWidthMeters = 7.32f;

    public static Vector2 ToMeters(Vector2 normalizedPosition) => new(
        normalizedPosition.X * LengthMeters,
        normalizedPosition.Y * WidthMeters);

    public static Vector2 ToNormalized(Vector2 positionMeters) => new(
        positionMeters.X / LengthMeters,
        positionMeters.Y / WidthMeters);

    public static float DistanceMeters(Vector2 first, Vector2 second) =>
        ToMeters(first).DistanceTo(ToMeters(second));
}
