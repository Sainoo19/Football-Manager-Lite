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
    private readonly float _goalProbabilityMultiplier;
    private readonly float _parriedShotCornerProbability;

    public ShotOutcomeResolver(
        float goalProbabilityMultiplier = 1f,
        float parriedShotCornerProbability = 0.24f)
    {
        _goalProbabilityMultiplier = goalProbabilityMultiplier;
        _parriedShotCornerProbability = parriedShotCornerProbability;
    }

    public ShotOutcome Resolve(
        int finishing,
        int shooterPositioning,
        int shooterForm,
        int goalkeeping,
        int goalkeeperForm,
        float distanceMeters,
        float angleFactor,
        float pressureDistanceMeters,
        float goalkeeperCoverage,
        float accuracyRoll,
        float goalRoll,
        float handlingRoll,
        float cornerRoll)
    {
        goalkeeperCoverage = Mathf.Clamp(goalkeeperCoverage, 0f, 1f);
        float distanceFactor = Mathf.Clamp((distanceMeters - 8f) / 26f, 0f, 1f);
        float onTargetChance = Mathf.Lerp(0.74f, 0.20f, distanceFactor) +
                               (finishing - 65) / 180f -
                               angleFactor * 0.22f +
                               (1f - goalkeeperCoverage) * 0.16f;
        if (pressureDistanceMeters < 2.2f)
        {
            onTargetChance -= 0.12f;
        }
        else if (pressureDistanceMeters < 5f)
        {
            onTargetChance -= 0.05f;
        }
        float maximumOnTargetChance = Mathf.Lerp(0.94f, 0.82f, goalkeeperCoverage);
        if (accuracyRoll > Mathf.Clamp(onTargetChance, 0.12f, maximumOnTargetChance))
        {
            return ShotOutcome.OffTarget;
        }

        float shotQuality = finishing * 0.58f + shooterPositioning * 0.22f + shooterForm * 0.20f;
        float keeperQuality = goalkeeping * 0.80f + goalkeeperForm * 0.20f;
        float goalDistanceFactor = Mathf.Clamp((distanceMeters - 7f) / 25f, 0f, 1f);
        float goalChance = Mathf.Lerp(0.36f, 0.035f, goalDistanceFactor) +
                           (shotQuality - keeperQuality) / 260f -
                           angleFactor * 0.16f +
                           (1f - goalkeeperCoverage) * 0.36f;
        if (pressureDistanceMeters < 2.2f)
        {
            goalChance -= 0.06f;
        }
        goalChance *= _goalProbabilityMultiplier;
        float maximumGoalChance = Mathf.Lerp(0.78f, 0.52f, goalkeeperCoverage);
        if (goalRoll < Mathf.Clamp(goalChance, 0.015f, maximumGoalChance))
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

        return cornerRoll < _parriedShotCornerProbability
            ? ShotOutcome.ParriedCorner
            : ShotOutcome.Parried;
    }
}
