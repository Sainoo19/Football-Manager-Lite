using Godot;

public enum ShotOutcome
{
    Goal,
    Saved,
    Parried,
    ParriedCorner,
    OffTarget
}

public sealed class ShotOutcomeResolver
{
    public ShotOutcome Resolve(
        int finishing,
        int shooterPositioning,
        int shooterForm,
        int goalkeeping,
        int goalkeeperForm,
        float distanceMeters,
        float angleFactor,
        float pressureDistanceMeters,
        float accuracyRoll,
        float goalRoll,
        float handlingRoll,
        float cornerRoll)
    {
        float distanceFactor = Mathf.Clamp((distanceMeters - 8f) / 26f, 0f, 1f);
        float onTargetChance = Mathf.Lerp(0.74f, 0.20f, distanceFactor) +
                               (finishing - 65) / 180f -
                               angleFactor * 0.22f;
        if (pressureDistanceMeters < 2.2f)
        {
            onTargetChance -= 0.12f;
        }
        else if (pressureDistanceMeters < 5f)
        {
            onTargetChance -= 0.05f;
        }
        if (accuracyRoll > Mathf.Clamp(onTargetChance, 0.12f, 0.82f))
        {
            return ShotOutcome.OffTarget;
        }

        float shotQuality = finishing * 0.58f + shooterPositioning * 0.22f + shooterForm * 0.20f;
        float keeperQuality = goalkeeping * 0.80f + goalkeeperForm * 0.20f;
        float goalDistanceFactor = Mathf.Clamp((distanceMeters - 7f) / 25f, 0f, 1f);
        float goalChance = Mathf.Lerp(0.36f, 0.035f, goalDistanceFactor) +
                           (shotQuality - keeperQuality) / 260f -
                           angleFactor * 0.16f;
        if (pressureDistanceMeters < 2.2f)
        {
            goalChance -= 0.06f;
        }
        if (goalRoll < Mathf.Clamp(goalChance, 0.015f, 0.52f))
        {
            return ShotOutcome.Goal;
        }

        float holdingChance = 0.56f +
                              (goalkeeping - 65) / 180f +
                              Mathf.Clamp((distanceMeters - 12f) / 40f, 0f, 1f) * 0.18f;
        if (handlingRoll < Mathf.Clamp(holdingChance, 0.40f, 0.86f))
        {
            return ShotOutcome.Saved;
        }

        return cornerRoll < 0.24f ? ShotOutcome.ParriedCorner : ShotOutcome.Parried;
    }
}
