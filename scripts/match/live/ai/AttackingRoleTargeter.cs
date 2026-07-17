using Godot;

public static class AttackingRoleTargeter
{
    private const float FinalThirdStart = 0.66f;
    private const float WideLaneStart = 0.78f;
    private const float WideLaneEnd = 0.22f;

    public static Vector2 CarrierTarget(FootballWorldSnapshot world, StringName playerId, StringName teamId)
    {
        Vector2 current = world.Positions[playerId];
        float direction = world.AttackDirection(teamId);
        float progress = BallProgress(world, teamId);
        bool isFinalThird = progress >= FinalThirdStart;
        float desiredY = current.Y;
        if (progress >= FinalThirdStart && IsWide(current.Y))
        {
            desiredY = Mathf.Lerp(current.Y, 0.5f, 0.22f);
        }

        Vector2 bestTarget = current;
        float bestScore = float.NegativeInfinity;
        float[] lateralAdjustments = { -0.04f, 0f, 0.04f };
        foreach (float lateralAdjustment in lateralAdjustments)
        {
            float targetLane = RoleLaneRules.ConstrainAttackingLane(
                world.PlayerRoles[playerId],
                desiredY + lateralAdjustment,
                isFinalThird,
                direction);
            Vector2 candidate = SpaceEvaluator.ClampToPitch(new Vector2(
                current.X + direction * 0.08f,
                targetLane));
            float pressure = SpaceEvaluator.OpponentPressure(
                candidate,
                teamId,
                world.Positions,
                world.PlayerTeams);
            float cornerPenalty = CornerPenalty(candidate);
            float score = direction * (candidate.X - current.X) * 3f - pressure * 0.62f - cornerPenalty;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    public static Vector2 SupportTarget(
        FootballWorldSnapshot world,
        StringName playerId,
        StringName teamId,
        int supportIndex)
    {
        float direction = world.AttackDirection(teamId);
        float baseLane = world.BasePositions[playerId].Y;
        float[] distancesBehindBallMeters = { 7f, 11f, 15f };
        float targetX = world.BallPosition.X - direction *
            (distancesBehindBallMeters[supportIndex % 3] / FootballPitchDimensions.LengthMeters);
        float upperSupportLane = world.BallPosition.Y - 7f / FootballPitchDimensions.WidthMeters;
        float lowerSupportLane = world.BallPosition.Y + 7f / FootballPitchDimensions.WidthMeters;
        float targetY = supportIndex % 3 switch
        {
            0 => upperSupportLane,
            1 => lowerSupportLane,
            _ => Mathf.Lerp(baseLane, 0.5f, 0.12f)
        };
        targetY = RoleLaneRules.ConstrainAttackingLane(
            world.PlayerRoles[playerId],
            targetY,
            false,
            direction);
        return SpaceEvaluator.ClampToPitch(new Vector2(targetX, targetY));
    }

    public static Vector2 RunnerTarget(FootballWorldSnapshot world, StringName playerId, StringName teamId)
    {
        float direction = world.AttackDirection(teamId);
        float progress = BallProgress(world, teamId);
        bool isFinalThird = progress >= FinalThirdStart;
        string role = world.PlayerRoles[playerId];
        float baseLane = world.BasePositions[playerId].Y;

        float aheadOfBall = role switch
        {
            "ST" => 0.17f,
            "LW" or "RW" => 0.14f,
            "AM" => 0.10f,
            _ => 0.06f
        };
        float targetX = isFinalThird
            ? direction > 0f ? FinalThirdX(role) : 1f - FinalThirdX(role)
            : world.BallPosition.X + direction * aheadOfBall;
        float preferredLane = PreferredRunnerLane(role, baseLane, isFinalThird, direction);
        return FindOpenLaneNear(world, teamId, targetX, preferredLane, role, isFinalThird, direction);
    }

    private static Vector2 FindOpenLaneNear(
        FootballWorldSnapshot world,
        StringName teamId,
        float targetX,
        float preferredLane,
        string role,
        bool isFinalThird,
        float attackDirection)
    {
        Vector2 bestTarget = new(targetX, preferredLane);
        float bestScore = float.NegativeInfinity;
        float[] laneAdjustments = { 0f, -0.035f, 0.035f };
        foreach (float adjustment in laneAdjustments)
        {
            float candidateY = RoleLaneRules.ConstrainAttackingLane(
                role,
                preferredLane + adjustment,
                isFinalThird,
                attackDirection);
            Vector2 candidate = SpaceEvaluator.ClampToPitch(new Vector2(targetX, candidateY));
            float pressure = SpaceEvaluator.OpponentPressure(
                candidate,
                teamId,
                world.Positions,
                world.PlayerTeams);
            float laneRisk = SpaceEvaluator.PassingLaneRisk(
                world.BallPosition,
                candidate,
                teamId,
                world.Positions,
                world.PlayerTeams);
            float shapeCost = Mathf.Abs(candidateY - preferredLane);
            float score = -pressure * 0.58f - laneRisk * 0.24f - shapeCost * 1.8f;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private static float PreferredRunnerLane(
        string role,
        float baseLane,
        bool isFinalThird,
        float attackDirection)
    {
        if (!isFinalThird)
        {
            return RoleLaneRules.ConstrainAttackingLane(role, baseLane, false, attackDirection);
        }

        return role switch
        {
            "ST" => Mathf.Clamp(baseLane, 0.34f, 0.66f),
            "LW" => attackDirection > 0f ? 0.40f : 0.60f,
            "RW" => attackDirection > 0f ? 0.60f : 0.40f,
            "AM" => Mathf.Lerp(baseLane, 0.5f, 0.65f),
            "CM" => Mathf.Clamp(baseLane, 0.28f, 0.72f),
            _ => Mathf.Clamp(baseLane, 0.20f, 0.80f)
        };
    }

    private static float BallProgress(FootballWorldSnapshot world, StringName teamId)
    {
        float direction = world.AttackDirection(teamId);
        return direction > 0f ? world.BallPosition.X : 1f - world.BallPosition.X;
    }

    private static float FinalThirdX(string role) => role is "CM" or "AM" ? 0.80f : 0.89f;

    private static bool IsWide(float lane) => lane <= WideLaneEnd || lane >= WideLaneStart;

    private static float CornerPenalty(Vector2 point)
    {
        float goalLineDistance = Mathf.Min(point.X, 1f - point.X);
        float touchlineDistance = Mathf.Min(point.Y, 1f - point.Y);
        return goalLineDistance < 0.08f && touchlineDistance < 0.10f ? 0.65f : 0f;
    }
}
