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
        _directAttackActionsRemaining = _configuration.MaximumDirectAttackActions;
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


    private void ClearDirectAttack()
    {
        _directAttackOwnerId = new StringName();
        _directAttackActionsRemaining = 0;
    }
}
