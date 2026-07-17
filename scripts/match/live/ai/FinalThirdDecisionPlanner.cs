public enum FinalThirdAction
{
    None,
    Carry,
    Shoot
}

public sealed class FinalThirdDecisionPlanner
{
    public FinalThirdAction Decide(string playerRole, float distanceToGoalMeters, float laneOffsetMeters)
    {
        if (playerRole == "ST")
        {
            if (distanceToGoalMeters <= 19f && laneOffsetMeters <= 19f)
            {
                return FinalThirdAction.Shoot;
            }

            return distanceToGoalMeters <= 24f ? FinalThirdAction.Carry : FinalThirdAction.None;
        }

        if (playerRole is "LW" or "RW" or "AM")
        {
            if (distanceToGoalMeters <= 17f && laneOffsetMeters <= 17f)
            {
                return FinalThirdAction.Shoot;
            }

            if (playerRole == "AM" && distanceToGoalMeters <= 21f)
            {
                return FinalThirdAction.Carry;
            }
        }

        return FinalThirdAction.None;
    }
}
