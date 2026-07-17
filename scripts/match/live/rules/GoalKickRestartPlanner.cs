using Godot;

public sealed class GoalKickRestartPlanner
{
    public const float PreparationDurationSeconds = 3.2f;

    public Vector2 PositionTarget(
        Vector2 basePosition,
        string role,
        bool isKickingTeam,
        float kickingGoalX,
        Vector2 restartPosition)
    {
        if (isKickingTeam && role == "GK")
        {
            return restartPosition;
        }

        float progressFromKickingGoal = isKickingTeam
            ? KickingTeamProgress(role)
            : DefendingTeamProgress(role);
        float x = kickingGoalX < 0.5f
            ? progressFromKickingGoal
            : 1f - progressFromKickingGoal;
        return SpaceEvaluator.ClampToPitch(new Vector2(x, basePosition.Y));
    }

    public Vector2 EnsureOpponentOutsidePenaltyArea(Vector2 position, float kickingGoalX)
    {
        float penaltyDepth = FootballPitchDimensions.PenaltyAreaDepthMeters /
                             FootballPitchDimensions.LengthMeters;
        float halfPenaltyWidth = FootballPitchDimensions.PenaltyAreaWidthMeters /
                                 FootballPitchDimensions.WidthMeters * 0.5f;
        bool insideWidth = Mathf.Abs(position.Y - 0.5f) < halfPenaltyWidth;
        bool insideDepth = kickingGoalX < 0.5f
            ? position.X < penaltyDepth
            : position.X > 1f - penaltyDepth;
        if (!insideWidth || !insideDepth)
        {
            return position;
        }

        float legalX = kickingGoalX < 0.5f ? penaltyDepth + 0.008f : 1f - penaltyDepth - 0.008f;
        return new Vector2(legalX, position.Y);
    }

    private static float KickingTeamProgress(string role) => role switch
    {
        "CB" => 0.19f,
        "LB" or "RB" => 0.24f,
        "DM" => 0.34f,
        "CM" => 0.44f,
        "AM" => 0.53f,
        "LW" or "RW" or "ST" => 0.62f,
        _ => 0.40f
    };

    private static float DefendingTeamProgress(string role) => role switch
    {
        "GK" => 0.94f,
        "CB" or "LB" or "RB" => 0.76f,
        "DM" => 0.66f,
        "CM" => 0.58f,
        "AM" => 0.47f,
        "LW" or "RW" or "ST" => 0.30f,
        _ => 0.55f
    };
}
