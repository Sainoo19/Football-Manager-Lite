using System;
using System.Collections.Generic;
using Godot;

public readonly struct SupportOption
{
    public SupportOption(Vector2 target, float score, string targetKey)
    {
        Target = target;
        Score = score;
        TargetKey = targetKey;
    }

    public Vector2 Target { get; }
    public float Score { get; }
    public string TargetKey { get; }
}

public sealed class SupportOptionEvaluator
{
    private static readonly Vector2[] RelativeOffsetsMeters =
    {
        new(-7f, -8f),
        new(-7f, 8f),
        new(5f, -11f),
        new(5f, 11f),
        new(-14f, 0f),
        new(11f, 0f),
        new(-3f, -16f),
        new(-3f, 16f)
    };

    private readonly OffBallParticipationConfiguration _configuration;

    public SupportOptionEvaluator(OffBallParticipationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public SupportOption BestOption(
        FootballWorldSnapshot world,
        SpaceOccupationMap occupation,
        StringName playerId,
        StringName teamId,
        IReadOnlyList<Vector2> reservedTargets,
        bool prefersForwardSupport)
    {
        float direction = world.AttackDirection(teamId);
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(world.BallPosition);
        SupportOption best = new(world.Positions[playerId], float.NegativeInfinity, string.Empty);
        foreach (Vector2 offset in RelativeOffsetsMeters)
        {
            Vector2 directedOffset = new(direction * offset.X, offset.Y);
            Vector2 candidate = SpaceEvaluator.ClampToPitch(
                FootballPitchDimensions.ToNormalized(ballMeters + directedOffset));
            candidate.Y = RoleLaneRules.ConstrainAttackingLane(
                world.PlayerRoles[playerId],
                candidate.Y,
                false,
                direction);
            float score = Score(world, occupation, playerId, teamId, candidate, prefersForwardSupport);
            if (IsReserved(candidate, reservedTargets))
            {
                score -= 2f;
            }
            if (score > best.Score)
            {
                best = new SupportOption(candidate, score, SpaceOccupationMap.ZoneFor(candidate).Key);
            }
        }

        return best;
    }

    public float Score(
        FootballWorldSnapshot world,
        SpaceOccupationMap occupation,
        StringName playerId,
        StringName teamId,
        Vector2 candidate,
        bool prefersForwardSupport)
    {
        float supportDistance = FootballPitchDimensions.DistanceMeters(world.BallPosition, candidate);
        float travelDistance = FootballPitchDimensions.DistanceMeters(world.Positions[playerId], candidate);
        float laneRisk = SpaceEvaluator.PassingLaneRisk(
            world.BallPosition,
            candidate,
            teamId,
            world.Positions,
            world.PlayerTeams);
        float direction = world.AttackDirection(teamId);
        float progress = direction * (candidate.X - world.BallPosition.X);
        float distanceFit = 1f - Mathf.Clamp(
            Mathf.Abs(supportDistance - 12f) / _configuration.ShortSupportMaximumDistanceMeters,
            0f,
            1f);
        float forwardFit = prefersForwardSupport ? Mathf.Clamp(progress * 8f, -0.2f, 0.35f) : 0f;
        float returnOption = progress <= 0.02f ? 0.13f : 0f;
        float score = occupation.SpaceScore(teamId, candidate, world) * 0.52f +
                      distanceFit * 0.36f -
                      laneRisk * 0.46f -
                      Mathf.Clamp(travelDistance / 35f, 0f, 1f) * 0.62f +
                      forwardFit +
                      returnOption;
        if (supportDistance < _configuration.ShortSupportMinimumDistanceMeters ||
            supportDistance > _configuration.ShortSupportMaximumDistanceMeters)
        {
            score -= 0.45f;
        }
        return score;
    }

    private bool IsReserved(Vector2 candidate, IReadOnlyList<Vector2> reservedTargets)
    {
        foreach (Vector2 target in reservedTargets)
        {
            if (FootballPitchDimensions.DistanceMeters(candidate, target) <
                _configuration.TargetReservationDistanceMeters)
            {
                return true;
            }
        }
        return false;
    }
}
