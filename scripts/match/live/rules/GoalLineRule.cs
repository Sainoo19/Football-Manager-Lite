using Godot;

public static class GoalLineRule
{
    // A ball on the ground that crosses the goal line between the posts is a goal, whoever touched it last.
    public static bool CrossesBetweenPosts(Vector2 insidePosition, Vector2 outPosition, float goalLineX)
    {
        float travelX = outPosition.X - insidePosition.X;
        float progress = Mathf.Abs(travelX) > 0.000001f
            ? Mathf.Clamp((goalLineX - insidePosition.X) / travelX, 0f, 1f)
            : 1f;
        float crossingY = Mathf.Lerp(insidePosition.Y, outPosition.Y, progress);
        float halfGoalWidth = FootballPitchDimensions.GoalWidthMeters * 0.5f / FootballPitchDimensions.WidthMeters;
        return Mathf.Abs(crossingY - 0.5f) <= halfGoalWidth;
    }
}
