using Godot;

public static class DefensiveBlockTargeter
{
    private const float MinimumBlockDepth = 0.10f;
    private const float MaximumBlockDepth = 0.30f;
    private const float MaximumMarkingDisplacementMeters = 5.5f;

    public static Vector2 ShapeTarget(FootballWorldSnapshot world, StringName playerId, StringName teamId)
    {
        string role = world.PlayerRoles[playerId];
        if (role == "GK")
        {
            return FootballIntentPlanner.GoalkeeperIntent(
                world,
                playerId,
                teamId,
                LiveTeamPhase.Defending).Target;
        }

        float direction = world.AttackDirection(teamId);
        Vector2 ownGoal = world.OwnGoal(teamId);
        float ballDepth = Mathf.Max(direction * (world.BallPosition.X - ownGoal.X), 0f);
        float blockDepth = Mathf.Clamp(ballDepth * 0.52f, MinimumBlockDepth, MaximumBlockDepth);
        float roleDepth = role switch
        {
            "CB" => 0f,
            "LB" or "RB" => 0.015f,
            "DM" => 0.075f,
            "CM" => 0.135f,
            "AM" => 0.175f,
            "LW" or "RW" => 0.205f,
            "ST" => 0.235f,
            _ => 0.12f
        };
        float laneShift = role is "CB" or "LB" or "RB" ? 0.12f : 0.22f;
        float targetLane = Mathf.Lerp(world.BasePositions[playerId].Y, world.BallPosition.Y, laneShift);
        return SpaceEvaluator.ClampToPitch(new Vector2(
            ownGoal.X + direction * (blockDepth + roleDepth),
            targetLane));
    }

    public static Vector2 CoverTarget(FootballWorldSnapshot world, StringName playerId, StringName teamId)
    {
        Vector2 shapeTarget = ShapeTarget(world, playerId, teamId);
        Vector2 ownGoal = world.OwnGoal(teamId);
        float ballDistanceFromGoal = FootballPitchDimensions.DistanceMeters(world.BallPosition, ownGoal);
        bool emergencyCover = ballDistanceFromGoal <= 18f;
        Vector2 behindBall = world.BallPosition.Lerp(ownGoal, emergencyCover ? 0.42f : 0.30f);
        float targetWeight = emergencyCover ? 0.72f : 0.38f;
        float maximumDisplacement = emergencyCover ? 9f : MaximumMarkingDisplacementMeters;
        return LimitDisplacement(shapeTarget, shapeTarget.Lerp(behindBall, targetWeight), maximumDisplacement);
    }

    public static Vector2 MarkTarget(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId,
        StringName opponentId)
    {
        Vector2 shapeTarget = ShapeTarget(world, playerId, teamId);
        Vector2 opponentPosition = world.Positions[opponentId];
        Vector2 opponentMeters = FootballPitchDimensions.ToMeters(opponentPosition);
        Vector2 goalSideDirection = (
            FootballPitchDimensions.ToMeters(world.OwnGoal(teamId)) - opponentMeters).Normalized();
        Vector2 goalSideTarget = FootballPitchDimensions.ToNormalized(opponentMeters + goalSideDirection * 2.5f);
        Vector2 markingTarget = shapeTarget.Lerp(goalSideTarget, 0.34f);
        return LimitDisplacement(shapeTarget, markingTarget, MaximumMarkingDisplacementMeters);
    }

    private static Vector2 LimitDisplacement(Vector2 origin, Vector2 target, float maximumDistanceMeters)
    {
        Vector2 originMeters = FootballPitchDimensions.ToMeters(origin);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(target);
        Vector2 limitedMeters = originMeters.MoveToward(targetMeters, maximumDistanceMeters);
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(limitedMeters));
    }
}
