using System;
using System.Collections.Generic;
using Godot;

// Phase 1 role-duty measurements taken at the moment of every non-penalty shot.
public sealed class Phase1DutyMetrics
{
    private const float BoxDepthMeters = 16.5f;
    private const float PenaltyAreaHalfWidthMeters = 20.16f;
    private const float PressureRadiusMeters = 3f;
    private const float ShotLaneHalfWidthMeters = 1.5f;
    private const float RecoveredDepthToleranceMeters = 10f;
    private const int RecoveredPlayerCount = 5;

    public static Phase1DutyMetrics Empty { get; } = new(0, 0, 0, 0, 0);

    public Phase1DutyMetrics(
        int openPlayShots,
        int boxShots,
        int freeBoxShots,
        int boxShotsWithTeamRecovered,
        int shotsWithGoalkeeperInBox)
    {
        OpenPlayShots = openPlayShots;
        BoxShots = boxShots;
        FreeBoxShots = freeBoxShots;
        BoxShotsWithTeamRecovered = boxShotsWithTeamRecovered;
        ShotsWithGoalkeeperInBox = shotsWithGoalkeeperInBox;
    }

    public int OpenPlayShots { get; }
    public int BoxShots { get; }
    // C1: shots inside the box with no defender within 3 m of the shooter and nobody on the line to goal.
    public int FreeBoxShots { get; }
    public int BoxShotsWithTeamRecovered { get; }
    public int ShotsWithGoalkeeperInBox { get; }
    // C2: share of box shots faced with at least five outfield players level with or behind the shooter.
    public double TeamRecoveryRate => BoxShots == 0 ? 1d : (double)BoxShotsWithTeamRecovered / BoxShots;
    // C3: share of shots faced with the goalkeeper inside his own penalty area.
    public double GoalkeeperInBoxRate => OpenPlayShots == 0 ? 1d : (double)ShotsWithGoalkeeperInBox / OpenPlayShots;

    public static Phase1DutyMetrics FromShots(IReadOnlyList<LiveShotRecord> shots)
    {
        ArgumentNullException.ThrowIfNull(shots);
        int openPlayShots = 0;
        int boxShots = 0;
        int freeBoxShots = 0;
        int recovered = 0;
        int goalkeeperInBox = 0;
        foreach (LiveShotRecord shot in shots)
        {
            if (shot.Situation == "penalty" || shot.PlayersAtLaunch is null)
            {
                continue;
            }
            openPlayShots++;
            Vector2 goal = new(shot.Destination.X > 0.5f ? 1f : 0f, 0.5f);
            Vector2 originMeters = FootballPitchDimensions.ToMeters(shot.Origin);
            Vector2 goalMeters = FootballPitchDimensions.ToMeters(goal);
            float shooterDepth = DepthMeters(shot.Origin, goal);
            float nearestDefender = float.PositiveInfinity;
            bool laneBlocked = false;
            int playersBack = 0;
            foreach (LiveShotPlayer player in shot.PlayersAtLaunch)
            {
                if (player.TeamId == shot.TeamId)
                {
                    continue;
                }
                if (player.Role == "GK")
                {
                    if (DepthMeters(player.Position, goal) <= BoxDepthMeters &&
                        Mathf.Abs(player.Position.Y - 0.5f) * FootballPitchDimensions.WidthMeters <=
                        PenaltyAreaHalfWidthMeters)
                    {
                        goalkeeperInBox++;
                    }
                    continue;
                }
                Vector2 playerMeters = FootballPitchDimensions.ToMeters(player.Position);
                nearestDefender = Mathf.Min(nearestDefender, playerMeters.DistanceTo(originMeters));
                laneBlocked |= IsOnShotLane(playerMeters, originMeters, goalMeters);
                if (DepthMeters(player.Position, goal) <= shooterDepth + RecoveredDepthToleranceMeters)
                {
                    playersBack++;
                }
            }
            if (shot.DistanceMeters > BoxDepthMeters)
            {
                continue;
            }
            boxShots++;
            recovered += playersBack >= RecoveredPlayerCount ? 1 : 0;
            freeBoxShots += nearestDefender > PressureRadiusMeters && !laneBlocked ? 1 : 0;
        }
        return new Phase1DutyMetrics(openPlayShots, boxShots, freeBoxShots, recovered, goalkeeperInBox);
    }

    private static bool IsOnShotLane(Vector2 playerMeters, Vector2 originMeters, Vector2 goalMeters)
    {
        Vector2 lane = goalMeters - originMeters;
        float lengthSquared = lane.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return false;
        }
        float progress = (playerMeters - originMeters).Dot(lane) / lengthSquared;
        if (progress is <= 0.05f or >= 1f)
        {
            return false;
        }
        return playerMeters.DistanceTo(originMeters + lane * progress) <= ShotLaneHalfWidthMeters;
    }

    private static float DepthMeters(Vector2 position, Vector2 goal) =>
        Mathf.Abs(goal.X - position.X) * FootballPitchDimensions.LengthMeters;
}
