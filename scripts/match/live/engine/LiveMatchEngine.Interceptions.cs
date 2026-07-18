using Godot;

public sealed partial class LiveMatchEngine
{
    private void TryInterceptMovingBall()
    {
        if (Simulation is null || _actionSourceTeamId == new StringName()) return;
        if (_aerialFlightActive)
        {
            return;
        }
        float contactDistanceMeters = _ballActionKind switch
        {
            BallActionKind.Cross => 1.6f,
            BallActionKind.ThroughBall => 1.3f,
            _ => 1.1f
        };
        StringName defenderId = new();
        float distance = float.PositiveInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (_playerTeams[candidateId] == _actionSourceTeamId ||
                _playerRoles[candidateId] == "GK" ||
                _interceptionAttemptedBy.Contains(candidateId))
            {
                continue;
            }

            float candidateDistance = FootballPitchDimensions.DistanceMeters(CurrentPositions[candidateId], BallPosition);
            if (candidateDistance <= contactDistanceMeters && candidateDistance < distance)
            {
                defenderId = candidateId;
                distance = candidateDistance;
            }
        }

        if (defenderId == new StringName()) return;
        _interceptionAttemptedBy.Add(defenderId);
        FootballPlayer? passer = GetPlayer(_actionSourceId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float passingSkill = ((passer?.passing ?? 50) + (passer?.vision ?? 50)) * 0.5f;
        float defensiveSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float chance = Mathf.Clamp(
            0.12f + (contactDistanceMeters - distance) / contactDistanceMeters * 0.38f +
            (defensiveSkill - passingSkill) / 190f,
            0.06f,
            0.68f);
        float roll = DecisionRoll(_actionSourceId, defenderId, _decisionSerial + _phaseSerial * 13);
        if (roll >= chance)
        {
            return;
        }
        StringName intendedReceiverId = _ballNextOwnerId;
        _ballActionActive = false;
        _ballActionKind = BallActionKind.None;
        _ballNextOwnerId = new StringName();
        _pendingOffsideReceiverId = new StringName();
        _ballVisualHeight = 0f;
        Interceptions++;
        GivePossessionTo(defenderId, 0.32f);
        SetAction(intendedReceiverId != new StringName()
            ? $"{PlayerName(defenderId)} đọc đường chuyền của {PlayerName(_actionSourceId)} cho " +
              $"{PlayerName(intendedReceiverId)} và cắt bóng"
            : $"{PlayerName(defenderId)} cắt được đường bóng");
    }
}
