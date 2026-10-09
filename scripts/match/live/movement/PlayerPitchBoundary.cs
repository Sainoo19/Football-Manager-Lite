using Godot;

public static class PlayerPitchBoundary
{
    public const float BodyRadiusMeters = 0.35f;

    public static Vector2 Clamp(Vector2 position)
    {
        float marginX = BodyRadiusMeters / FootballPitchDimensions.LengthMeters;
        float marginY = BodyRadiusMeters / FootballPitchDimensions.WidthMeters;
        return new Vector2(Mathf.Clamp(position.X, marginX, 1f - marginX),
            Mathf.Clamp(position.Y, marginY, 1f - marginY));
    }
}
