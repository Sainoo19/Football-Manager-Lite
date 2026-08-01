public sealed class DirectAttackContinuationPlanner
{
    public bool ShouldBeginAfterReception(
        string playerRole,
        bool isThroughBall,
        float attackProgress,
        float forwardGainMeters)
    {
        if (playerRole is not ("ST" or "LW" or "RW" or "AM"))
        {
            return false;
        }

        return isThroughBall || attackProgress >= 0.70f && forwardGainMeters >= 8f;
    }
}
