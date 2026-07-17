using Godot;

public enum PenaltyKickOutcome
{
    Goal,
    Saved,
    OffTarget
}

public sealed class PenaltyKickResolver
{
    public PenaltyKickOutcome Resolve(
        int finishing,
        int composure,
        int form,
        int goalkeeping,
        int goalkeeperForm,
        float accuracyRoll,
        float goalRoll)
    {
        float takerQuality = finishing * 0.55f + composure * 0.30f + form * 0.15f;
        float goalkeeperQuality = goalkeeping * 0.82f + goalkeeperForm * 0.18f;
        float onTargetChance = Mathf.Clamp(0.82f + (takerQuality - 65f) / 260f, 0.68f, 0.96f);
        if (accuracyRoll > onTargetChance)
        {
            return PenaltyKickOutcome.OffTarget;
        }

        float goalChance = Mathf.Clamp(0.72f + (takerQuality - goalkeeperQuality) / 230f, 0.52f, 0.88f);
        return goalRoll < goalChance ? PenaltyKickOutcome.Goal : PenaltyKickOutcome.Saved;
    }
}
