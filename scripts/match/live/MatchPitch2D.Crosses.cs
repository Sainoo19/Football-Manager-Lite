using Godot;

public partial class MatchPitch2D
{
    private StringName ChooseCrossTarget(StringName crosserId)
    {
        if (!CurrentPositions.TryGetValue(crosserId, out Vector2 crosserPosition))
        {
            return new StringName();
        }

        float direction = AttackDirection(_activeTeamId);
        StringName bestId = new();
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == crosserId ||
                _playerTeams[candidateId] != _activeTeamId ||
                IsCurrentlyOffside(candidateId))
            {
                continue;
            }

            Vector2 candidatePosition = CurrentPositions[candidateId];
            float distanceMeters = FootballPitchDimensions.DistanceMeters(crosserPosition, candidatePosition);
            float forwardGainMeters = direction * (candidatePosition.X - crosserPosition.X) *
                                      FootballPitchDimensions.LengthMeters;
            float laneRisk = PassingLaneRisk(crosserPosition, candidatePosition, _activeTeamId);
            if (!_passOptionEvaluator.CanConsiderCross(
                    _playerRoles[candidateId],
                    forwardGainMeters,
                    distanceMeters,
                    laneRisk))
            {
                continue;
            }

            float centrality = 1f - Mathf.Clamp(Mathf.Abs(candidatePosition.Y - 0.5f) / 0.5f, 0f, 1f);
            float candidateProgress = AttackProgress(_activeTeamId, candidatePosition);
            float receivingPressure = SpaceEvaluator.OpponentPressure(
                candidatePosition,
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
            float roleBonus = _playerRoles[candidateId] is "ST" or "AM" ? 0.28f : 0f;
            float score = candidateProgress * 0.75f +
                          centrality * 0.50f +
                          forwardGainMeters / FootballPitchDimensions.LengthMeters * 0.55f -
                          distanceMeters / FootballPitchDimensions.LengthMeters * 0.35f -
                          laneRisk * 0.72f -
                          receivingPressure * 0.38f +
                          roleBonus;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestId = candidateId;
        }

        return bestId;
    }
}
