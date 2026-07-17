using Godot;

public partial class MatchPitch2D
{
    private void TryInterceptMovingBall()
    {
        if (Simulation is null || _actionSourceTeamId == new StringName()) return;
        if (_ballActionKind == BallActionKind.Cross && TryGoalkeeperClaimCross())
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

    private bool TryGoalkeeperClaimCross()
    {
        if (Simulation is null)
        {
            return false;
        }

        StringName defendingTeamId = _actionSourceTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        if (goalkeeperId == new StringName() ||
            _interceptionAttemptedBy.Contains(goalkeeperId) ||
            FootballPitchDimensions.DistanceMeters(CurrentPositions[goalkeeperId], BallPosition) > 2.2f)
        {
            return false;
        }

        _interceptionAttemptedBy.Add(goalkeeperId);
        FootballPlayer? goalkeeper = GetPlayer(goalkeeperId);
        float claimChance = Mathf.Clamp(
            0.52f + ((goalkeeper?.goalkeeping ?? 55) - 65) / 120f,
            0.32f,
            0.82f);
        if (DecisionRoll(goalkeeperId, _actionSourceId, _decisionSerial + 229) >= claimChance)
        {
            return false;
        }

        _ballActionActive = false;
        _ballActionKind = BallActionKind.None;
        _ballNextOwnerId = new StringName();
        _pendingOffsideReceiverId = new StringName();
        _ballVisualHeight = 0f;
        GivePossessionTo(goalkeeperId, 0.75f);
        SetAction($"{PlayerName(goalkeeperId)} lao ra bắt gọn quả tạt");
        return true;
    }
}
