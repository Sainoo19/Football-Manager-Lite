using Godot;

public sealed class PassOptionEvaluator
{
    public bool CanConsiderCross(
        string candidateRole,
        float forwardGainMeters,
        float distanceMeters,
        float passingLaneRisk)
    {
        bool attackingReceiver = candidateRole is "ST" or "LW" or "RW" or "AM" or "CM";
        if (!attackingReceiver || distanceMeters is < 7f or > 42f)
        {
            return false;
        }

        // A short cut-back is valid, but a cross must never become a diagonal
        // retreat across most of the pitch.
        return forwardGainMeters >= -10f && passingLaneRisk <= 0.82f;
    }

    public bool CanConsider(
        string ballCarrierRole,
        string candidateRole,
        float attackProgress,
        float forwardGainMeters,
        float distanceMeters,
        float passingLaneRisk,
        bool preferSafe)
    {
        if (distanceMeters is < 4.5f or > 45f)
        {
            return false;
        }

        bool attackingCarrier = ballCarrierRole is "CM" or "ST" or "LW" or "RW" or "AM";
        bool defensiveCandidate = candidateRole is "GK" or "CB" or "LB" or "RB" or "DM";
        bool advancedCarrier = attackProgress >= 0.58f;
        bool attackingTransition = attackingCarrier && advancedCarrier;
        if (advancedCarrier && forwardGainMeters < -15f && distanceMeters > 30f)
        {
            return false;
        }
        if (attackingTransition && defensiveCandidate && forwardGainMeters < -8f && distanceMeters > 24f)
        {
            return false;
        }
        if (attackingCarrier && attackProgress >= 0.70f)
        {
            return !defensiveCandidate && passingLaneRisk <= 0.64f;
        }

        bool aggressiveTransition = attackingTransition && !preferSafe;
        if (!aggressiveTransition)
        {
            return true;
        }

        if (defensiveCandidate && forwardGainMeters < -6f)
        {
            return false;
        }

        return forwardGainMeters >= -15f || distanceMeters <= 24f;
    }

    public float ScoreAdjustment(
        string candidateRole,
        float attackProgress,
        float forwardGainMeters,
        float distanceMeters)
    {
        float backwardPenalty = Mathf.Max(-forwardGainMeters - 4f, 0f) / 32f;
        float longPassPenalty = Mathf.Max(distanceMeters - 24f, 0f) / 85f;
        float attackingLinkBonus = attackProgress >= 0.58f &&
                                   candidateRole is ("ST" or "LW" or "RW" or "AM" or "CM")
            ? 0.14f
            : 0f;
        return attackingLinkBonus - backwardPenalty - longPassPenalty;
    }
}
