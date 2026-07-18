using Godot;

public sealed class DuelDistanceRules
{
    public const float EngagementStartDistanceMeters = 3.8f;
    public const float EngagementExitDistanceMeters = 4.8f;
    public const float MinimumPlayerSeparationMeters = 0.70f;
    public const float TackleAttemptDistanceMeters = 1.6f;
    public const float PressureDistanceMeters = 3.2f;

    public bool CanAttemptTackle(float distanceMeters) =>
        distanceMeters <= TackleAttemptDistanceMeters;

    public bool IsUnderPressure(float distanceMeters) =>
        distanceMeters <= PressureDistanceMeters;

    public float ContactBonus(float distanceMeters)
    {
        return Mathf.Clamp(
            (TackleAttemptDistanceMeters - distanceMeters) / TackleAttemptDistanceMeters,
            0f,
            1f);
    }
}
