using Godot;

public static class PlayerProximity
{
    public static float DistanceSquaredMeters(Vector2 first, Vector2 second)
    {
        Vector2 differenceMeters = FootballPitchDimensions.ToMeters(first) -
                                   FootballPitchDimensions.ToMeters(second);
        return differenceMeters.LengthSquared();
    }
}
