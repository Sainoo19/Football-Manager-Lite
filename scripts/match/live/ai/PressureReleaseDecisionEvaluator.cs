public readonly struct PressureReleaseContext
{
    public PressureReleaseContext(
        bool isUnderPressure,
        int duelTouchCount,
        int duelExchangeCount,
        int passing,
        int vision,
        int composure,
        int dribbling,
        PassSelection pass,
        float decisionRoll)
    {
        IsUnderPressure = isUnderPressure;
        DuelTouchCount = duelTouchCount;
        DuelExchangeCount = duelExchangeCount;
        Passing = passing;
        Vision = vision;
        Composure = composure;
        Dribbling = dribbling;
        Pass = pass;
        DecisionRoll = decisionRoll;
    }

    public bool IsUnderPressure { get; }
    public int DuelTouchCount { get; }
    public int DuelExchangeCount { get; }
    public int Passing { get; }
    public int Vision { get; }
    public int Composure { get; }
    public int Dribbling { get; }
    public PassSelection Pass { get; }
    public float DecisionRoll { get; }
}

public sealed class PressureReleaseDecisionEvaluator
{
    private const float MinimumUsableReceiverSpaceMeters = 3.8f;
    private const float ClearOutletReceiverSpaceMeters = 5.0f;
    private const float MaximumUsableLaneRisk = 0.56f;
    private const float MaximumClearOutletLaneRisk = 0.48f;
    private const float MinimumProgressiveGainMeters = 3.0f;
    private const float MinimumSidewaysGainMeters = -1.5f;
    private const float MaximumPressuredPassDistanceMeters = 32f;
    private const int ExceptionalDribblerRating = 82;

    public bool ShouldRelease(PressureReleaseContext context)
    {
        PassSelection pass = context.Pass;
        if (!context.IsUnderPressure || !pass.HasTarget)
        {
            return false;
        }

        bool usableOutlet = pass.ReceiverSpaceMeters >= MinimumUsableReceiverSpaceMeters &&
                            pass.LaneRisk <= MaximumUsableLaneRisk &&
                            pass.DistanceMeters <= MaximumPressuredPassDistanceMeters;
        if (!usableOutlet)
        {
            return false;
        }

        bool progressiveOutlet = pass.ForwardGainMeters >= MinimumProgressiveGainMeters;
        bool clearSidewaysOutlet = pass.ForwardGainMeters >= MinimumSidewaysGainMeters &&
                                   pass.ReceiverSpaceMeters >= ClearOutletReceiverSpaceMeters &&
                                   pass.LaneRisk <= MaximumClearOutletLaneRisk;
        if (!progressiveOutlet && !clearSidewaysOutlet)
        {
            return false;
        }

        float decisionQuality = (context.Passing + context.Vision + context.Composure) / 297f;
        float releaseProbability = 0.42f + decisionQuality * 0.24f;
        if (clearSidewaysOutlet)
        {
            releaseProbability += 0.20f;
        }
        if (context.DuelTouchCount > 0)
        {
            releaseProbability += 0.16f;
        }
        if (context.DuelExchangeCount > 0)
        {
            releaseProbability += 0.12f;
        }
        if (context.Dribbling >= ExceptionalDribblerRating &&
            context.DuelTouchCount == 0 &&
            context.DuelExchangeCount == 0)
        {
            releaseProbability -= 0.10f;
        }

        return context.DecisionRoll < System.Math.Clamp(releaseProbability, 0.30f, 0.98f);
    }
}
