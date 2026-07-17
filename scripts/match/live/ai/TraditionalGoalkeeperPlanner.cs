using Godot;

public sealed class TraditionalGoalkeeperPlanner
{
    private const float MaximumClaimDistanceMeters = 14f;
    private const float MinimumArrivalAdvantageMeters = 1.5f;

    public Vector2 PositionTarget(FootballWorldSnapshot world, StringName teamId)
    {
        Vector2 goal = world.OwnGoal(teamId);
        float direction = world.AttackDirection(teamId);
        bool defendingFlight = teamId != world.PossessionTeamId;
        if (defendingFlight && world.IsShotInFlight)
        {
            return SpaceEvaluator.ClampToPitch(new Vector2(
                goal.X + direction * (1.4f / FootballPitchDimensions.LengthMeters),
                Mathf.Clamp(world.BallDestination.Y, 0.36f, 0.64f)));
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

        return SpaceEvaluator.ClampToPitch(new Vector2(
            goal.X + direction * (depthMeters / FootballPitchDimensions.LengthMeters),
            Mathf.Lerp(0.5f, claimingCross ? world.BallDestination.Y : world.BallPosition.Y, laneWeight)));
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
