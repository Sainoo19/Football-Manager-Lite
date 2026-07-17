using Godot;

public partial class MatchPitch2D
{
    private bool TryResolveFinalThirdAction(StringName ownerId, float pressureDistanceMeters)
    {
        if (Simulation?.use_live_pitch_events != true)
        {
            return false;
        }

        Vector2 ownerPosition = CurrentPositions[ownerId];
        Vector2 goal = new(AttackingGoalX(_playerTeams[ownerId]), 0.5f);
        float distanceToGoalMeters = FootballPitchDimensions.DistanceMeters(ownerPosition, goal);
        float laneOffsetMeters = Mathf.Abs(ownerPosition.Y - 0.5f) * FootballPitchDimensions.WidthMeters;
        FinalThirdAction action = _finalThirdDecisionPlanner.Decide(
            _playerRoles[ownerId],
            distanceToGoalMeters,
            laneOffsetMeters);

        if (action == FinalThirdAction.Shoot)
        {
            StartLiveShot(ownerId, pressureDistanceMeters);
            return true;
        }

        if (action == FinalThirdAction.Carry)
        {
            StartDribble(ownerId, pressureDistanceMeters < 2.4f);
            return true;
        }

        return false;
    }
}
