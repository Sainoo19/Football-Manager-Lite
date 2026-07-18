using Godot;

public enum FirstTouchOutcome
{
    Controlled,
    HeavyTouch,
    Miscontrol
}

public readonly struct FirstTouchResolution
{
    public FirstTouchResolution(FirstTouchOutcome outcome, float looseBallSpeedMetersPerSecond)
    {
        Outcome = outcome;
        LooseBallSpeedMetersPerSecond = looseBallSpeedMetersPerSecond;
    }

    public FirstTouchOutcome Outcome { get; }
    public float LooseBallSpeedMetersPerSecond { get; }
}

public sealed class FirstTouchResolver
{
    public FirstTouchResolution Resolve(
        int firstTouch,
        int technique,
        int composure,
        int form,
        float pressureDistanceMeters,
        float incomingBallSpeedMetersPerSecond,
        LivePassType passType,
        float controlRoll,
        float severityRoll)
    {
        float skill = firstTouch * 0.44f + technique * 0.28f + composure * 0.18f + form * 0.10f;
        float pressurePenalty = pressureDistanceMeters switch
        {
            <= 1.5f => 0.25f,
            >= 5f => 0f,
            _ => (5f - pressureDistanceMeters) / 3.5f * 0.25f
        };
        float speedPenalty = Mathf.Clamp((incomingBallSpeedMetersPerSecond - 14f) / 14f, 0f, 1f) * 0.15f;
        float typePenalty = passType switch
        {
            LivePassType.Cross => 0.12f,
            LivePassType.Lofted => 0.09f,
            LivePassType.ThroughBall => 0.05f,
            _ => 0f
        };
        float controlChance = Mathf.Clamp(0.48f + skill / 190f - pressurePenalty - speedPenalty - typePenalty, 0.20f, 0.94f);
        if (controlRoll <= controlChance)
        {
            return new FirstTouchResolution(FirstTouchOutcome.Controlled, 0f);
        }

        float failureMargin = controlRoll - controlChance;
        if (failureMargin < 0.20f || severityRoll < 0.72f)
        {
            float heavyTouchSpeed = Mathf.Lerp(1.8f, 4.2f, Mathf.Clamp(failureMargin / 0.25f, 0f, 1f));
            return new FirstTouchResolution(FirstTouchOutcome.HeavyTouch, heavyTouchSpeed);
        }

        return new FirstTouchResolution(FirstTouchOutcome.Miscontrol, Mathf.Lerp(3.8f, 6.2f, severityRoll));
    }
}
