using Godot;

public sealed class TraditionalGoalkeeperPlanner
{
    private const float MaximumClaimDistanceMeters = 14f;
    private const float MinimumArrivalAdvantageMeters = 1.5f;

    public Vector2 PositionTarget(FootballWorldSnapshot world, StringName goalkeeperId, StringName teamId)
    {
        Vector2 goal = world.OwnGoal(teamId);
        float direction = world.AttackDirection(teamId);
        bool defendingFlight = teamId != world.PossessionTeamId;
        if (defendingFlight && world.IsShotInFlight)
        {
            Vector2 keeper = world.Positions[goalkeeperId];
            float depth = Mathf.Clamp(Mathf.Abs(keeper.X - goal.X) * FootballPitchDimensions.LengthMeters, 1.4f, 4f);
            float targetX = goal.X + direction * depth / FootballPitchDimensions.LengthMeters;
            float remainingTravel = world.BallDestination.X - world.BallPosition.X;
            float progress = Mathf.Abs(remainingTravel) > 0.001f
                ? Mathf.Clamp((targetX - world.BallPosition.X) / remainingTravel, 0f, 1f)
                : 1f;
            float targetY = Mathf.Lerp(world.BallPosition.Y, world.BallDestination.Y, progress);
            return PlayerPitchBoundary.Clamp(new Vector2(targetX, ClampGoalLane(targetY)));
        }
        if (ShouldRushControlledBall(world, goalkeeperId, teamId))
        {
            Vector2 ballMeters = FootballPitchDimensions.ToMeters(world.BallPosition);
            Vector2 goalMeters = FootballPitchDimensions.ToMeters(goal);
            Vector2 goalSideDirection = (goalMeters - ballMeters).Normalized();
            Vector2 targetMeters = ballMeters + goalSideDirection * 1.35f;
            return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(targetMeters));
        }

        float ballDistanceFromGoal = FootballPitchDimensions.DistanceMeters(goal, world.BallPosition);
        float danger = 1f - Mathf.Clamp(ballDistanceFromGoal / 70f, 0f, 1f);
        float depthMeters = Mathf.Lerp(1.8f, 4.0f, danger);
        float laneWeight = Mathf.Lerp(0.20f, 0.48f, danger);
        bool claimingCross = defendingFlight &&
                             world.IsCrossInFlight &&
                             FootballPitchDimensions.DistanceMeters(goal, world.BallDestination) <= 20f;
        if (claimingCross)
        {
            depthMeters = 4.5f;
            laneWeight = 0.68f;
        }

        if (claimingCross)
        {
            return SpaceEvaluator.ClampToPitch(new Vector2(
                goal.X + direction * depthMeters / FootballPitchDimensions.LengthMeters,
                Mathf.Lerp(0.5f, world.BallDestination.Y, laneWeight)));
        }
        // Cover the angle from the goal centre instead of following a winger outside the posts.
        Vector2 towardBall = FootballPitchDimensions.ToMeters(world.BallPosition) - FootballPitchDimensions.ToMeters(goal);
        Vector2 stance = FootballPitchDimensions.ToMeters(goal) + towardBall.Normalized() * depthMeters;
        Vector2 normalizedStance = FootballPitchDimensions.ToNormalized(stance);
        normalizedStance.Y = ClampGoalLane(normalizedStance.Y);
        return PlayerPitchBoundary.Clamp(normalizedStance);
    }

    private static float ClampGoalLane(float lane)
    {
        const float postAllowanceMeters = 0.8f;
        float halfWidth = (FootballPitchDimensions.GoalWidthMeters * 0.5f + postAllowanceMeters) /
                          FootballPitchDimensions.WidthMeters;
        return Mathf.Clamp(lane, 0.5f - halfWidth, 0.5f + halfWidth);
    }

    public bool ShouldRushControlledBall(
        FootballWorldSnapshot world,
        StringName goalkeeperId,
        StringName teamId)
    {
        if (world.IsBallInFlight ||
            world.IsLooseBall ||
            world.PossessionTeamId == teamId ||
            world.BallOwnerId == new StringName() ||
            !world.Positions.TryGetValue(goalkeeperId, out Vector2 goalkeeperPosition) ||
            !IsInsideOwnPenaltyArea(world.BallPosition, world.OwnGoal(teamId)))
        {
            return false;
        }

        float goalkeeperDistance = FootballPitchDimensions.DistanceMeters(goalkeeperPosition, world.BallPosition);
        float ballDistanceFromGoal = FootballPitchDimensions.DistanceMeters(world.OwnGoal(teamId), world.BallPosition);
        if (goalkeeperDistance > 12f || ballDistanceFromGoal > 14f)
        {
            return false;
        }

        float nearestOutfieldDefenderDistance = float.PositiveInfinity;
        foreach (StringName playerId in world.Positions.Keys)
        {
            if (playerId == goalkeeperId ||
                world.PlayerTeams[playerId] != teamId ||
                world.PlayerRoles[playerId] == "GK")
            {
                continue;
            }

            nearestOutfieldDefenderDistance = Mathf.Min(
                nearestOutfieldDefenderDistance,
                FootballPitchDimensions.DistanceMeters(world.Positions[playerId], world.BallPosition));
        }

        bool immediateGoalThreat = ballDistanceFromGoal <= 8.5f;
        bool goalkeeperArrivesFirst = goalkeeperDistance + 0.75f < nearestOutfieldDefenderDistance;
        return immediateGoalThreat || goalkeeperArrivesFirst;
    }

    public bool ShouldUseBackPass(
        string passerRole,
        float attackProgress,
        bool isUnderPressure,
        float goalkeeperDistanceMeters,
        float passingLaneRisk,
        float decisionRoll)
    {
        if (passerRole is not ("CB" or "LB" or "RB" or "DM") ||
            attackProgress > 0.38f ||
            goalkeeperDistanceMeters is < 4f or > 32f ||
            passingLaneRisk > 0.72f)
        {
            return false;
        }

        float chance = isUnderPressure ? 0.54f : 0.08f;
        return decisionRoll < chance;
    }

    public bool ShouldClaimLooseBall(
        FootballWorldSnapshot world,
        StringName goalkeeperId,
        StringName teamId)
    {
        if (!world.IsLooseBall ||
            !world.Positions.TryGetValue(goalkeeperId, out Vector2 goalkeeperPosition) ||
            !IsInsideOwnPenaltyArea(world.BallPosition, world.OwnGoal(teamId)))
        {
            return false;
        }

        float goalkeeperDistance = FootballPitchDimensions.DistanceMeters(goalkeeperPosition, world.BallPosition);
        if (goalkeeperDistance > MaximumClaimDistanceMeters)
        {
            return false;
        }

        float nearestOpponentDistance = float.PositiveInfinity;
        foreach (StringName playerId in world.Positions.Keys)
        {
            if (world.PlayerTeams[playerId] == teamId || world.PlayerRoles[playerId] == "GK")
            {
                continue;
            }

            nearestOpponentDistance = Mathf.Min(
                nearestOpponentDistance,
                FootballPitchDimensions.DistanceMeters(world.Positions[playerId], world.BallPosition));
        }

        return goalkeeperDistance <= FootballPitchDimensions.GoalAreaDepthMeters + 1f ||
               goalkeeperDistance + MinimumArrivalAdvantageMeters < nearestOpponentDistance;
    }

    public bool CanCollectLooseBall(Vector2 ballPosition, Vector2 goalkeeperPosition, Vector2 ownGoal)
    {
        return IsInsideOwnPenaltyArea(ballPosition, ownGoal) &&
               FootballPitchDimensions.DistanceMeters(goalkeeperPosition, ballPosition) <= 2.4f;
    }

    private static bool IsInsideOwnPenaltyArea(Vector2 position, Vector2 ownGoal)
    {
        float depthMeters = Mathf.Abs(position.X - ownGoal.X) * FootballPitchDimensions.LengthMeters;
        float lateralMeters = Mathf.Abs(position.Y - 0.5f) * FootballPitchDimensions.WidthMeters;
        return depthMeters <= FootballPitchDimensions.PenaltyAreaDepthMeters &&
               lateralMeters <= FootballPitchDimensions.PenaltyAreaWidthMeters * 0.5f;
    }
}
