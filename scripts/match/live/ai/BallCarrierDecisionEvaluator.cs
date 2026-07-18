public sealed class BallCarrierDecisionEvaluator
{
    private const int MaximumPatientCarries = 5;
    private readonly float _underPressureDribbleProbability;

    public BallCarrierDecisionEvaluator(float underPressureDribbleProbability = 0.28f)
    {
        _underPressureDribbleProbability = underPressureDribbleProbability;
    }

    public bool ShouldKeepCarrying(
        bool isUnderPressure,
        int dribbling,
        int consecutiveCarries,
        float nearestOpponentDistanceMeters,
        PassSelection pass,
        float decisionRoll)
    {
        if (!pass.HasTarget)
        {
            return true;
        }

        if (isUnderPressure)
        {
            return dribbling >= 76 &&
                   consecutiveCarries < 2 &&
                   decisionRoll < _underPressureDribbleProbability;
        }

        bool carrierHasSpace = nearestOpponentDistanceMeters >= 5f;
        bool passAdvancesAttack = pass.ForwardGainMeters >= 5f;
        bool receiverHasClearSpace = pass.ReceiverSpaceMeters >= 4f && pass.LaneRisk <= 0.48f;
        if (carrierHasSpace && consecutiveCarries < MaximumPatientCarries &&
            (!passAdvancesAttack || !receiverHasClearSpace || pass.Score < -0.05f))
        {
            return true;
        }

        return dribbling >= 70 &&
               consecutiveCarries < 3 &&
               decisionRoll < 0.24f;
    }
}
