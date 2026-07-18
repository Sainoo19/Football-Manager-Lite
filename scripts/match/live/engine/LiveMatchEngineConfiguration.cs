using System;

public sealed class LiveMatchEngineConfiguration
{
    public LiveMatchEngineConfiguration(
        double fixedStepSeconds,
        float possessionIntentPlanningIntervalSeconds,
        float ballInFlightPlanningIntervalSeconds,
        float looseBallPlanningIntervalSeconds,
        int maximumUnresolvedGroundDuelExchanges,
        float groundDuelFoulProbabilityMultiplier,
        float shotGoalProbabilityMultiplier,
        float firstTouchControlChanceBonus,
        float passControlDistanceMultiplier,
        float yellowCardBaseProbability,
        float clearChanceRedCardProbability,
        float blockedShotCornerProbability,
        float parriedShotCornerProbability,
        float shotAttemptProbabilityMultiplier,
        float minimumLoftedPassDistanceMeters,
        int minimumThroughBallCreativeSkill,
        float headerShotProbability,
        float defensiveHeaderOutOfPlayProbability,
        float underPressureDribbleProbability,
        float minimumOffsideAvoidanceProbability,
        float maximumOffsideAvoidanceProbability,
        float penaltyAreaChallengeProbability,
        int maximumDirectAttackActions,
        float bookedPlayerChallengeProbability)
    {
        if (fixedStepSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds));
        }
        if (maximumUnresolvedGroundDuelExchanges < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumUnresolvedGroundDuelExchanges));
        }

        FixedStepSeconds = fixedStepSeconds;
        PossessionIntentPlanningIntervalSeconds = possessionIntentPlanningIntervalSeconds;
        BallInFlightPlanningIntervalSeconds = ballInFlightPlanningIntervalSeconds;
        LooseBallPlanningIntervalSeconds = looseBallPlanningIntervalSeconds;
        MaximumUnresolvedGroundDuelExchanges = maximumUnresolvedGroundDuelExchanges;
        GroundDuelFoulProbabilityMultiplier = groundDuelFoulProbabilityMultiplier;
        ShotGoalProbabilityMultiplier = shotGoalProbabilityMultiplier;
        FirstTouchControlChanceBonus = firstTouchControlChanceBonus;
        PassControlDistanceMultiplier = passControlDistanceMultiplier;
        YellowCardBaseProbability = yellowCardBaseProbability;
        ClearChanceRedCardProbability = clearChanceRedCardProbability;
        BlockedShotCornerProbability = blockedShotCornerProbability;
        ParriedShotCornerProbability = parriedShotCornerProbability;
        ShotAttemptProbabilityMultiplier = shotAttemptProbabilityMultiplier;
        MinimumLoftedPassDistanceMeters = minimumLoftedPassDistanceMeters;
        MinimumThroughBallCreativeSkill = minimumThroughBallCreativeSkill;
        HeaderShotProbability = headerShotProbability;
        DefensiveHeaderOutOfPlayProbability = defensiveHeaderOutOfPlayProbability;
        UnderPressureDribbleProbability = underPressureDribbleProbability;
        MinimumOffsideAvoidanceProbability = minimumOffsideAvoidanceProbability;
        MaximumOffsideAvoidanceProbability = maximumOffsideAvoidanceProbability;
        PenaltyAreaChallengeProbability = penaltyAreaChallengeProbability;
        MaximumDirectAttackActions = maximumDirectAttackActions;
        BookedPlayerChallengeProbability = bookedPlayerChallengeProbability;
    }

    public double FixedStepSeconds { get; }
    public float PossessionIntentPlanningIntervalSeconds { get; }
    public float BallInFlightPlanningIntervalSeconds { get; }
    public float LooseBallPlanningIntervalSeconds { get; }
    public int MaximumUnresolvedGroundDuelExchanges { get; }
    public float GroundDuelFoulProbabilityMultiplier { get; }
    public float ShotGoalProbabilityMultiplier { get; }
    public float FirstTouchControlChanceBonus { get; }
    public float PassControlDistanceMultiplier { get; }
    public float YellowCardBaseProbability { get; }
    public float ClearChanceRedCardProbability { get; }
    public float BlockedShotCornerProbability { get; }
    public float ParriedShotCornerProbability { get; }
    public float ShotAttemptProbabilityMultiplier { get; }
    public float MinimumLoftedPassDistanceMeters { get; }
    public int MinimumThroughBallCreativeSkill { get; }
    public float HeaderShotProbability { get; }
    public float DefensiveHeaderOutOfPlayProbability { get; }
    public float UnderPressureDribbleProbability { get; }
    public float MinimumOffsideAvoidanceProbability { get; }
    public float MaximumOffsideAvoidanceProbability { get; }
    public float PenaltyAreaChallengeProbability { get; }
    public int MaximumDirectAttackActions { get; }
    public float BookedPlayerChallengeProbability { get; }

    public static LiveMatchEngineConfiguration CreateFootballFundamentalsV1()
    {
        return new LiveMatchEngineConfiguration(
            fixedStepSeconds: 0.10d,
            possessionIntentPlanningIntervalSeconds: 0.50f,
            ballInFlightPlanningIntervalSeconds: 0.24f,
            looseBallPlanningIntervalSeconds: 0.14f,
            maximumUnresolvedGroundDuelExchanges: 3,
            groundDuelFoulProbabilityMultiplier: 0.52f,
            shotGoalProbabilityMultiplier: 0.78f,
            firstTouchControlChanceBonus: 0.14f,
            passControlDistanceMultiplier: 1.35f,
            yellowCardBaseProbability: 0.15f,
            clearChanceRedCardProbability: 0.02f,
            blockedShotCornerProbability: 0.44f,
            parriedShotCornerProbability: 0.40f,
            shotAttemptProbabilityMultiplier: 0.075f,
            minimumLoftedPassDistanceMeters: 36f,
            minimumThroughBallCreativeSkill: 150,
            headerShotProbability: 0.38f,
            defensiveHeaderOutOfPlayProbability: 0.48f,
            underPressureDribbleProbability: 0.08f,
            minimumOffsideAvoidanceProbability: 0.72f,
            maximumOffsideAvoidanceProbability: 0.96f,
            penaltyAreaChallengeProbability: 0.04f,
            maximumDirectAttackActions: 3,
            bookedPlayerChallengeProbability: 0.25f);
    }
}
