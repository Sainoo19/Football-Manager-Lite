using System.Collections.Generic;
using Godot;

public sealed class ClearanceTargetPlanner
{
    private const float MinimumForwardGainMeters = 12f;
    private const float MaximumDistanceMeters = 52f;
    private const float ReceiverLeadMeters = 3.5f;
    private const float FallbackDistanceMeters = 36f;

    public Vector2 FindTarget(
        Vector2 ballPosition,
        float attackDirection,
        StringName clearingTeamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams,
        IReadOnlyDictionary<StringName, string> playerRoles)
    {
        StringName bestOutletId = new();
        float bestScore = float.NegativeInfinity;
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (playerTeams[playerId] != clearingTeamId || playerRoles[playerId] == "GK")
            {
                continue;
            }

            float forwardGainMeters = attackDirection * (position.X - ballPosition.X) *
                                      FootballPitchDimensions.LengthMeters;
            float distanceMeters = FootballPitchDimensions.DistanceMeters(ballPosition, position);
            if (forwardGainMeters < MinimumForwardGainMeters || distanceMeters > MaximumDistanceMeters)
            {
                continue;
            }

            float opponentDistanceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
                position,
                clearingTeamId,
                positions,
                playerTeams);
            float centrality = 1f - Mathf.Abs(position.Y - 0.5f) * 2f;
            float attackingRoleBonus = playerRoles[playerId] is "ST" or "LW" or "RW" ? 2.5f : 0f;
            float score = opponentDistanceMeters * 0.65f + centrality * 3f + attackingRoleBonus -
                          Mathf.Abs(distanceMeters - 34f) * 0.08f;
            if (score > bestScore)
            {
                bestScore = score;
                bestOutletId = playerId;
            }
        }

        if (bestOutletId != new StringName())
        {
            Vector2 outletMeters = FootballPitchDimensions.ToMeters(positions[bestOutletId]);
            outletMeters.X += attackDirection * ReceiverLeadMeters;
            return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(outletMeters));
        }

        Vector2 ballMeters = FootballPitchDimensions.ToMeters(ballPosition);
        Vector2 fallbackMeters = new(
            ballMeters.X + attackDirection * FallbackDistanceMeters,
            Mathf.Lerp(ballMeters.Y, FootballPitchDimensions.WidthMeters * 0.5f, 0.70f));
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(fallbackMeters));
    }

}
