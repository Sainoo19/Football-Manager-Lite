using Godot;

public sealed partial class LiveMatchEngine
{
    private void BeginDirectAttack(StringName receiverId)
    {
        if (!_playerRoles.TryGetValue(receiverId, out string? role) ||
            role is not ("ST" or "LW" or "RW" or "AM"))
        {
            ClearDirectAttack();
            return;
        }

        _directAttackOwnerId = receiverId;
        _directAttackActionsRemaining = 6;
    }

    private bool ShouldBeginDirectAttack(StringName receiverId, BallActionKind completedKind)
    {
        if (!_playerRoles.TryGetValue(receiverId, out string? role))
        {
            return false;
        }

        float attackDirection = AttackDirection(_playerTeams[receiverId]);
        float forwardGainMeters = attackDirection * (BallPosition.X - _ballActionFrom.X) *
                                  FootballPitchDimensions.LengthMeters;
        float attackProgress = AttackProgress(_playerTeams[receiverId], BallPosition);
        return _directAttackContinuationPlanner.ShouldBeginAfterReception(
            role,
            completedKind == BallActionKind.ThroughBall,
            attackProgress,
            forwardGainMeters);
    }

    private bool TryContinueDirectAttack(StringName ownerId, float pressureDistanceMeters)
    {
        if (ownerId != _directAttackOwnerId || _directAttackActionsRemaining <= 0)
        {
            ClearDirectAttack();
            return false;
        }

        Vector2 goal = new(AttackingGoalX(_playerTeams[ownerId]), 0.5f);
        Vector2 ownerPosition = CurrentPositions[ownerId];
        float distanceToGoalMeters = FootballPitchDimensions.DistanceMeters(ownerPosition, goal);
        float laneOffsetMeters = Mathf.Abs(ownerPosition.Y - 0.5f) * FootballPitchDimensions.WidthMeters;
        DirectAttackContinuation continuation = _directAttackContinuationPlanner.Decide(
            _playerRoles[ownerId],
            distanceToGoalMeters,
            laneOffsetMeters,
            _directAttackActionsRemaining);

        _directAttackActionsRemaining--;
        if (continuation == DirectAttackContinuation.Shoot)
        {
            ClearDirectAttack();
            StartLiveShot(ownerId, pressureDistanceMeters);
            return true;
        }

        if (continuation == DirectAttackContinuation.Carry)
        {
            StartDribble(ownerId, pressureDistanceMeters < 2.4f);
            return true;
        }

        ClearDirectAttack();
        return false;
    }

    private void ClearDirectAttack()
    {
        _directAttackOwnerId = new StringName();
        _directAttackActionsRemaining = 0;
    }
}
