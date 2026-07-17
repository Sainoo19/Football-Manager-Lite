public enum DirectAttackContinuation
{
    None,
    Carry,
    Shoot
}

public sealed class DirectAttackContinuationPlanner
{
    private const float MaximumContinuationDistanceMeters = 42f;
    private const float ShootingDistanceMeters = 24f;
    private const float MaximumShootingLaneOffsetMeters = 18f;

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

    public DirectAttackContinuation Decide(
        string playerRole,
        float distanceToGoalMeters,
        float laneOffsetMeters,
        int remainingActions)
    {
        if (remainingActions <= 0 ||
            playerRole is not ("ST" or "LW" or "RW" or "AM") ||
            distanceToGoalMeters > MaximumContinuationDistanceMeters)
        {
            return DirectAttackContinuation.None;
        }

        if (distanceToGoalMeters <= ShootingDistanceMeters &&
            laneOffsetMeters <= MaximumShootingLaneOffsetMeters)
        {
            return DirectAttackContinuation.Shoot;
        }

        return DirectAttackContinuation.Carry;
    }
}
