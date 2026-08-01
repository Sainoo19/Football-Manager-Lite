using Godot;

public sealed class RestartCoordinator
{
    public float PreparationDuration(StringName restartType, FreeKickRestartPlan freeKickPlan)
    {
        return restartType.ToString() switch
        {
            "goal_kick" => GoalKickRestartPlanner.PreparationDurationSeconds,
            "free_kick" => freeKickPlan.PreparationDurationSeconds,
            "penalty" => PenaltyRestartPlanner.PreparationDurationSeconds,
            _ => 0.46f
        };
    }
}
