using Godot;

public readonly struct AdvantageContext
{
    public AdvantageContext(
        bool ballCarrierCanContinue,
        StringName proposedCard,
        float attackProgress,
        float nearestDefenderDistanceMeters,
        float decisionRoll)
    {
        BallCarrierCanContinue = ballCarrierCanContinue;
        ProposedCard = proposedCard;
        AttackProgress = attackProgress;
        NearestDefenderDistanceMeters = nearestDefenderDistanceMeters;
        DecisionRoll = decisionRoll;
    }

    public bool BallCarrierCanContinue { get; }
    public StringName ProposedCard { get; }
    public float AttackProgress { get; }
    public float NearestDefenderDistanceMeters { get; }
    public float DecisionRoll { get; }
}

public sealed class AdvantageRuleEvaluator
{
    private const float MinimumAttackProgress = 0.38f;
    private const float MinimumControlDistanceMeters = 0.75f;

    public bool ShouldPlay(AdvantageContext context)
    {
        if (!context.BallCarrierCanContinue ||
            context.ProposedCard == "red" ||
            context.AttackProgress < MinimumAttackProgress ||
            context.NearestDefenderDistanceMeters < MinimumControlDistanceMeters)
        {
            return false;
        }

        float chance = 0.30f +
                       Mathf.Clamp((context.AttackProgress - MinimumAttackProgress) * 0.70f, 0f, 0.32f) +
                       Mathf.Clamp((context.NearestDefenderDistanceMeters - 1.2f) / 8f, 0f, 0.18f);
        return context.DecisionRoll < Mathf.Clamp(chance, 0.20f, 0.72f);
    }
}
