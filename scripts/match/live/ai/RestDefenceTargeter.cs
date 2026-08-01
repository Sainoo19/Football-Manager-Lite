using Godot;

public static class RestDefenceTargeter
{
    public static Vector2 Target(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId)
    {
        float direction = world.AttackDirection(teamId);
        Vector2 basePosition = world.BasePositions[playerId];
        float safeDepth = world.BallPosition.X - direction * 0.20f;
        float baseGoalSide = direction > 0f
            ? Mathf.Min(basePosition.X, safeDepth)
            : Mathf.Max(basePosition.X, safeDepth);
        float ballSideShift = world.PlayerRoles[playerId] == "DM" ? 0.22f : 0.10f;
        float lane = Mathf.Lerp(basePosition.Y, world.BallPosition.Y, ballSideShift);
        return SpaceEvaluator.ClampToPitch(new Vector2(baseGoalSide, lane));
    }
}
