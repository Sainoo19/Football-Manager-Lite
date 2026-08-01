using System;
using System.Collections.Generic;
using Godot;

public sealed class RunCoordinator
{
    private readonly OffBallParticipationConfiguration _configuration;

    public RunCoordinator(OffBallParticipationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void AssignRuns(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        IReadOnlyList<StringName> candidates,
        int requestedRunners,
        Dictionary<StringName, PlayerIntent> intents,
        List<Vector2> reservedTargets)
    {
        List<StringName> ordered = new(candidates);
        ordered.Sort((first, second) =>
        {
            int roleComparison = RunnerPriority(world.PlayerRoles[second])
                .CompareTo(RunnerPriority(world.PlayerRoles[first]));
            return roleComparison != 0
                ? roleComparison
                : FootballIntentPlanner.ComparePlayerIds(first, second);
        });

        int assigned = 0;
        foreach (StringName playerId in ordered)
        {
            if (assigned >= Math.Min(requestedRunners, _configuration.MaximumForwardRunners))
            {
                break;
            }
            (OffBallAssignmentKind assignment, Vector2 target) = TargetFor(
                world,
                teamId,
                phase,
                playerId,
                assigned);
            target = AvoidReservedTarget(world, playerId, target, reservedTargets);
            PlayerIntentKind intentKind = assignment is OffBallAssignmentKind.HoldWidth or
                OffBallAssignmentKind.OccupyWideLane
                ? PlayerIntentKind.SupportBall
                : PlayerIntentKind.RunIntoSpace;
            intents[playerId] = new PlayerIntent(
                intentKind,
                target,
                phase,
                assignment: assignment,
                targetKey: SpaceOccupationMap.ZoneFor(target).Key);
            reservedTargets.Add(target);
            assigned++;
        }
    }

    private static (OffBallAssignmentKind Assignment, Vector2 Target) TargetFor(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        StringName playerId,
        int runIndex)
    {
        float direction = world.AttackDirection(teamId);
        string role = world.PlayerRoles[playerId];
        bool finalThird = phase == LiveTeamPhase.FinalThird;
        bool ballIsWide = world.BallPosition.Y < 0.27f || world.BallPosition.Y > 0.73f;
        if (finalThird && ballIsWide && runIndex < 3)
        {
            float goalX = direction > 0f ? 0.94f : 0.06f;
            bool ballOnTop = world.BallPosition.Y < 0.5f;
            return runIndex switch
            {
                0 => (OffBallAssignmentKind.AttackBoxNearPost,
                    new Vector2(goalX, ballOnTop ? 0.43f : 0.57f)),
                1 => (OffBallAssignmentKind.AttackBoxFarPost,
                    new Vector2(goalX, ballOnTop ? 0.57f : 0.43f)),
                _ => (OffBallAssignmentKind.AttackBoxCutBackZone,
                    new Vector2(direction > 0f ? 0.82f : 0.18f, 0.5f))
            };
        }

        Vector2 target = AttackingRoleTargeter.RunnerTarget(world, playerId, teamId);
        OffBallAssignmentKind assignment = role is "LW" or "RW" && runIndex > 1
            ? OffBallAssignmentKind.OccupyWideLane
            : runIndex % 2 == 0
                ? OffBallAssignmentKind.RunBehind
                : OffBallAssignmentKind.RunAcrossDefender;
        return (assignment, target);
    }

    private Vector2 AvoidReservedTarget(
        FootballWorldSnapshot world,
        StringName playerId,
        Vector2 target,
        IReadOnlyList<Vector2> reservedTargets)
    {
        foreach (Vector2 reserved in reservedTargets)
        {
            if (FootballPitchDimensions.DistanceMeters(target, reserved) >=
                _configuration.TargetReservationDistanceMeters)
            {
                continue;
            }
            float baseLane = world.BasePositions[playerId].Y;
            float shift = target.Y <= baseLane ? -0.07f : 0.07f;
            return SpaceEvaluator.ClampToPitch(new Vector2(target.X, target.Y + shift));
        }
        return target;
    }

    private static int RunnerPriority(string role)
    {
        return role switch
        {
            "ST" => 6,
            "LW" or "RW" => 5,
            "AM" => 4,
            "CM" => 3,
            "LB" or "RB" => 2,
            _ => 1
        };
    }
}
