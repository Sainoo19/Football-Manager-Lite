using Godot;

public static class AttackingRoleTargeter
{
    private const float FinalThirdStart = 0.66f;
    private const float WideLaneStart = 0.78f;
    private const float WideLaneEnd = 0.22f;
    private static readonly Vector2[] SupportOffsetsMeters =
    {
        new(-6f, -8f),
        new(-6f, 8f),
        new(2f, -10f),
        new(2f, 10f),
        new(-12f, 0f),
        new(6f, 0f)
    };

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
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(world.BallPosition);
        Vector2 bestTarget = world.Positions[playerId];
        float bestScore = float.NegativeInfinity;
        for (int index = 0; index < SupportOffsetsMeters.Length; index++)
        {
            int rotatedIndex = (index + supportIndex * 2) % SupportOffsetsMeters.Length;
            Vector2 offset = SupportOffsetsMeters[rotatedIndex];
            Vector2 candidateMeters = ballMeters + new Vector2(direction * offset.X, offset.Y);
            Vector2 candidate = SpaceEvaluator.ClampToPitch(
                FootballPitchDimensions.ToNormalized(candidateMeters));
            candidate.Y = RoleLaneRules.ConstrainAttackingLane(
                world.PlayerRoles[playerId],
                candidate.Y,
                false,
                direction);

            float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
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
            float travelDistanceMeters = FootballPitchDimensions.DistanceMeters(
                world.Positions[playerId],
                candidate);
            float passDistanceMeters = FootballPitchDimensions.DistanceMeters(
                world.BallPosition,
                candidate);
            float roleProgressBonus = SupportProgressBonus(
                world.PlayerRoles[playerId],
                offset.X);
            float preferredOffsetBonus = index == 0 ? 0.10f : 0f;
            float score = Mathf.Clamp(receiverSpaceMeters / 9f, 0f, 1f) * 0.52f -
                          laneRisk * 0.42f -
                          Mathf.Clamp(travelDistanceMeters / 30f, 0f, 1f) * 0.18f +
                          roleProgressBonus +
                          preferredOffsetBonus;
            if (passDistanceMeters < 4.5f)
            {
                score -= 0.35f;
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
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

    private static float SupportProgressBonus(string role, float forwardOffsetMeters)
    {
        bool attackingMidfielder = role is "AM" or "CM";
        bool defensiveSupport = role is "CB" or "LB" or "RB" or "DM";
        if (attackingMidfielder && forwardOffsetMeters > 0f)
        {
            return 0.14f;
        }
        if (defensiveSupport && forwardOffsetMeters <= 0f)
        {
            return 0.10f;
        }
        return 0f;
    }

    private static float CornerPenalty(Vector2 point)
    {
        float goalLineDistance = Mathf.Min(point.X, 1f - point.X);
        float touchlineDistance = Mathf.Min(point.Y, 1f - point.Y);
        return goalLineDistance < 0.08f && touchlineDistance < 0.10f ? 0.65f : 0f;
    }
}
