using System;
using System.Collections.Generic;
using Godot;

public sealed class DefensiveRecoveryConfiguration
{
    public DefensiveRecoveryConfiguration(
        float dangerZoneDistanceMeters,
        float minimumRetreatDepthMeters,
        float markerLagDistanceMeters,
        float recoverySprintSpeedMetersPerSecond)
    {
        if (dangerZoneDistanceMeters <= 0f || recoverySprintSpeedMetersPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(dangerZoneDistanceMeters));
        }

        DangerZoneDistanceMeters = dangerZoneDistanceMeters;
        MinimumRetreatDepthMeters = minimumRetreatDepthMeters;
        MarkerLagDistanceMeters = markerLagDistanceMeters;
        RecoverySprintSpeedMetersPerSecond = recoverySprintSpeedMetersPerSecond;
    }

    public float DangerZoneDistanceMeters { get; }
    public float MinimumRetreatDepthMeters { get; }
    public float MarkerLagDistanceMeters { get; }
    public float RecoverySprintSpeedMetersPerSecond { get; }

    public static DefensiveRecoveryConfiguration CreatePhase1Defaults()
    {
        return new DefensiveRecoveryConfiguration(
            dangerZoneDistanceMeters: 40f,
            minimumRetreatDepthMeters: 2f,
            markerLagDistanceMeters: 4f,
            recoverySprintSpeedMetersPerSecond: 7.8f);
    }
}

// Decides which defenders must sprint back. Without this, retreating players jog while attackers sprint past them.
public static class DefensiveUrgencyRules
{
    public static HashSet<StringName> SelectSprintingDefenders(
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> targets,
        IReadOnlyDictionary<StringName, PlayerIntent> intents,
        IReadOnlyDictionary<StringName, StringName> playerTeams,
        StringName possessionTeamId,
        bool isLooseBall,
        Vector2 ballPosition,
        Func<StringName, float> ownGoalX,
        DefensiveRecoveryConfiguration configuration)
    {
        HashSet<StringName> sprinting = new();
        if (isLooseBall || possessionTeamId == new StringName())
        {
            return sprinting;
        }

        foreach ((StringName playerId, PlayerIntent intent) in intents)
        {
            if (!playerTeams.TryGetValue(playerId, out StringName? teamId) ||
                teamId is null ||
                teamId == possessionTeamId ||
                !positions.TryGetValue(playerId, out Vector2 position) ||
                !targets.TryGetValue(playerId, out Vector2 target))
            {
                continue;
            }

            float goalX = ownGoalX(teamId);
            if (IsUrgent(intent, position, target, ballPosition, goalX, positions, configuration))
            {
                sprinting.Add(playerId);
            }
        }
        return sprinting;
    }

    public static bool IsUrgent(
        PlayerIntent intent,
        Vector2 position,
        Vector2 target,
        Vector2 ballPosition,
        float ownGoalX,
        IReadOnlyDictionary<StringName, Vector2> positions,
        DefensiveRecoveryConfiguration configuration)
    {
        if (!IsDefensiveAssignment(intent.Assignment))
        {
            return false;
        }

        Vector2 ownGoal = new(ownGoalX, 0.5f);
        if (FootballPitchDimensions.DistanceMeters(ballPosition, ownGoal) > configuration.DangerZoneDistanceMeters)
        {
            return false;
        }

        float retreatMeters = DepthMeters(position, ownGoalX) - DepthMeters(target, ownGoalX);
        if (retreatMeters >= configuration.MinimumRetreatDepthMeters)
        {
            return true;
        }

        if (intent.Assignment != OffBallAssignmentKind.TrackRunner ||
            !positions.TryGetValue(intent.RelatedPlayerId, out Vector2 opponent))
        {
            return false;
        }

        bool opponentIsGoalSide = DepthMeters(opponent, ownGoalX) < DepthMeters(position, ownGoalX);
        return opponentIsGoalSide ||
               FootballPitchDimensions.DistanceMeters(position, target) >= configuration.MarkerLagDistanceMeters;
    }

    private static bool IsDefensiveAssignment(OffBallAssignmentKind assignment) => assignment is
        OffBallAssignmentKind.RecoverGoalSide or
        OffBallAssignmentKind.BlockShotLine or
        OffBallAssignmentKind.ProtectBox or
        OffBallAssignmentKind.HoldLine or
        OffBallAssignmentKind.CoverPresser or
        OffBallAssignmentKind.BlockPrimaryLane or
        OffBallAssignmentKind.TrackRunner or
        OffBallAssignmentKind.ClaimSecondBallZone;

    private static float DepthMeters(Vector2 position, float ownGoalX) =>
        Mathf.Abs(position.X - ownGoalX) * FootballPitchDimensions.LengthMeters;
}
