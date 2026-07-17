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
            Vector2 candidate = SpaceEvaluator.ClampToPitch(new Vector2(
                current.X + direction * 0.08f,
                Mathf.Clamp(desiredY + lateralAdjustment, 0.06f, 0.94f)));
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
        float[] distancesBehindBall = { 0.08f, 0.13f, 0.18f };
        float targetX = world.BallPosition.X - direction * distancesBehindBall[supportIndex % 3];
        float targetY = supportIndex % 3 switch
        {
            0 => Mathf.Lerp(world.BallPosition.Y, baseLane, 0.55f),
            1 => Mathf.Lerp(world.BallPosition.Y, 0.5f, 0.45f),
            _ => Mathf.Lerp(baseLane, 0.5f, 0.18f)
        };
        targetY = ClampRoleLane(world.PlayerRoles[playerId], targetY, false, direction);
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
            float candidateY = ClampRoleLane(role, preferredLane + adjustment, isFinalThird, attackDirection);
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
            return ClampRoleLane(role, baseLane, false, attackDirection);
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

    private static float ClampRoleLane(
        string role,
        float lane,
        bool isFinalThird,
        float attackDirection) => role switch
    {
        "ST" => Mathf.Clamp(lane, 0.30f, 0.70f),
        "LW" when !isFinalThird && attackDirection > 0f => Mathf.Clamp(lane, 0.08f, 0.44f),
        "LW" when !isFinalThird => Mathf.Clamp(lane, 0.56f, 0.92f),
        "RW" when !isFinalThird && attackDirection > 0f => Mathf.Clamp(lane, 0.56f, 0.92f),
        "RW" when !isFinalThird => Mathf.Clamp(lane, 0.08f, 0.44f),
        "LW" or "RW" => Mathf.Clamp(lane, 0.32f, 0.68f),
        "CM" or "AM" or "DM" => Mathf.Clamp(lane, 0.16f, 0.84f),
        _ => Mathf.Clamp(lane, 0.06f, 0.94f)
    };

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
