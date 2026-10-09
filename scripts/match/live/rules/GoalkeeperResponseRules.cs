using Godot;

public static class GoalkeeperResponseRules
{
    public static float ReactionDelaySeconds(int goalkeeping) => Mathf.Lerp(0.32f, 0.16f,
        Mathf.Clamp(goalkeeping / 99f, 0f, 1f));

    public static bool CanHoldContact(float contactDistanceMeters, float shotSpeedMetersPerSecond, int goalkeeping)
    {
        float secureReach = Mathf.Lerp(0.75f, 1.4f, Mathf.Clamp(goalkeeping / 99f, 0f, 1f));
        return contactDistanceMeters <= secureReach && shotSpeedMetersPerSecond <= 30f;
    }
}
