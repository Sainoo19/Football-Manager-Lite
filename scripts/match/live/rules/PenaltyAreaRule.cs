using Godot;

public sealed class PenaltyAreaRule
{
    public bool IsInsideDefendingPenaltyArea(Vector2 position, float defendingGoalX)
    {
        float depthMeters = defendingGoalX < 0.5f
            ? position.X * FootballPitchDimensions.LengthMeters
            : (1f - position.X) * FootballPitchDimensions.LengthMeters;
        float lateralMeters = Mathf.Abs(position.Y - 0.5f) * FootballPitchDimensions.WidthMeters;
        return depthMeters <= FootballPitchDimensions.PenaltyAreaDepthMeters &&
               lateralMeters <= FootballPitchDimensions.PenaltyAreaWidthMeters * 0.5f;
    }
}
