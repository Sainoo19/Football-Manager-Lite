using Godot;

public static class DefensiveBlockTargeter
{
    private const float MinimumBlockDepth = 0.005f;
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
        // Recover toward the goal when play enters the box rather than leaving a fixed line behind the ball.
        roleDepth *= Mathf.Clamp(ballDepth / 0.22f, 0.05f, 1f);
        float laneShift = role is "CB" or "LB" or "RB" ? 0.12f : 0.22f;
        float targetLane = Mathf.Lerp(world.BasePositions[playerId].Y, world.BallPosition.Y, laneShift);
        return SpaceEvaluator.ClampToPitch(new Vector2(
            ownGoal.X + direction * (blockDepth + roleDepth),
            targetLane));
    }

    // The presser approaches from the goal side so the carrier cannot simply run past toward goal.
    public static Vector2 PressApproachTarget(Vector2 ballPosition, Vector2 ownGoal, float distanceMeters)
    {
        return PointTowardGoal(ballPosition, ownGoal, distanceMeters);
    }

    // A second defender stands on the ball-goal line, between the presser and the goal.
    public static Vector2 ShotLineBlockTarget(Vector2 ballPosition, Vector2 ownGoal, float offsetMeters)
    {
        float ballToGoalMeters = FootballPitchDimensions.DistanceMeters(ballPosition, ownGoal);
        return PointTowardGoal(ballPosition, ownGoal, Mathf.Min(offsetMeters, ballToGoalMeters * 0.5f));
    }

    private static Vector2 PointTowardGoal(Vector2 ballPosition, Vector2 ownGoal, float distanceMeters)
    {
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 towardGoal = FootballPitchDimensions.ToMeters(ownGoal) - ballMeters;
        if (towardGoal.LengthSquared() <= 0.0001f)
        {
            return PlayerPitchBoundary.Clamp(ballPosition);
        }
        Vector2 targetMeters = ballMeters + towardGoal.Normalized() * Mathf.Min(distanceMeters, towardGoal.Length());
        return PlayerPitchBoundary.Clamp(FootballPitchDimensions.ToNormalized(targetMeters));
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
        if (FootballPitchDimensions.DistanceMeters(opponentPosition, world.OwnGoal(teamId)) <= 30f)
        {
            return PlayerPitchBoundary.Clamp(FootballPitchDimensions.ToNormalized(
                opponentMeters + goalSideDirection * 1.4f));
        }
        Vector2 markingTarget = shapeTarget.Lerp(goalSideTarget, 0.34f);
        return LimitDisplacement(shapeTarget, markingTarget, MaximumMarkingDisplacementMeters);
    }

    public static Vector2 PassingLaneTarget(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId)
    {
        Vector2 shapeTarget = ShapeTarget(world, playerId, teamId);
        StringName receiverId = new();
        float shortestPassDistanceMeters = float.PositiveInfinity;
        foreach ((StringName candidateId, Vector2 position) in world.Positions)
        {
            if (world.PlayerTeams[candidateId] == teamId ||
                candidateId == world.BallOwnerId ||
                world.PlayerRoles[candidateId] == "GK")
            {
                continue;
            }

            float distanceMeters = FootballPitchDimensions.DistanceMeters(world.BallPosition, position);
            if (distanceMeters < shortestPassDistanceMeters ||
                Mathf.IsEqualApprox(distanceMeters, shortestPassDistanceMeters) &&
                FootballIntentPlanner.ComparePlayerIds(candidateId, receiverId) < 0)
            {
                shortestPassDistanceMeters = distanceMeters;
                receiverId = candidateId;
            }
        }

        if (receiverId == new StringName())
        {
            return shapeTarget;
        }

        Vector2 lanePoint = world.BallPosition.Lerp(world.Positions[receiverId], 0.58f);
        return LimitDisplacement(shapeTarget, shapeTarget.Lerp(lanePoint, 0.55f), 7f);
    }

    public static Vector2 RecoveryTarget(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId)
    {
        Vector2 shapeTarget = ShapeTarget(world, playerId, teamId);
        Vector2 current = world.Positions[playerId];
        Vector2 ownGoal = world.OwnGoal(teamId);
        bool isGoalSide = FootballPitchDimensions.DistanceMeters(current, ownGoal) <=
                          FootballPitchDimensions.DistanceMeters(world.BallPosition, ownGoal);
        if (isGoalSide)
        {
            return current.Lerp(shapeTarget, 0.48f);
        }
        Vector2 recoveryLane = new Vector2(
            Mathf.Lerp(current.X, shapeTarget.X, 0.78f),
            Mathf.Lerp(current.Y, shapeTarget.Y, 0.42f));
        return LimitDisplacement(current, recoveryLane, 10f);
    }

    private static Vector2 LimitDisplacement(Vector2 origin, Vector2 target, float maximumDistanceMeters)
    {
        Vector2 originMeters = FootballPitchDimensions.ToMeters(origin);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(target);
        Vector2 limitedMeters = originMeters.MoveToward(targetMeters, maximumDistanceMeters);
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(limitedMeters));
    }
}
