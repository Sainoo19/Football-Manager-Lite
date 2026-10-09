using System;
using System.Collections.Generic;
using Godot;

public sealed class DefensiveAssignmentCoordinator
{
    private readonly OffBallParticipationConfiguration _configuration;

    public DefensiveAssignmentCoordinator(OffBallParticipationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Assign(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> available = FootballIntentPlanner.TeamOutfieldPlayers(world, teamId);
        StringName presser = SelectPresser(world, teamId, available);
        if (presser != new StringName())
        {
            intents[presser] = Intent(
                PlayerIntentKind.PressBall,
                DefensiveBlockTargeter.PressApproachTarget(
                    world.BallPosition,
                    world.OwnGoal(teamId),
                    _configuration.PressApproachDistanceMeters),
                phase,
                world.BallOwnerId,
                OffBallAssignmentKind.PressBall);
            available.Remove(presser);
        }

        AssignThreats(world, teamId, phase, available, intents);

        bool blocksShotLine = AssignShotLineBlocker(world, teamId, phase, available, intents);
        StringName cover = blocksShotLine
            ? new StringName()
            : SelectClosest(world, available, world.BallPosition.Lerp(world.OwnGoal(teamId), 0.30f));
        if (cover != new StringName())
        {
            intents[cover] = Intent(
                PlayerIntentKind.CoverPress,
                DefensiveBlockTargeter.CoverTarget(world, cover, teamId),
                phase,
                presser,
                OffBallAssignmentKind.CoverPresser);
            available.Remove(cover);
        }

        StringName laneBlocker = SelectLaneBlocker(world, teamId, available);
        if (laneBlocker != new StringName())
        {
            intents[laneBlocker] = Intent(
                PlayerIntentKind.BlockPassingLane,
                DefensiveBlockTargeter.PassingLaneTarget(world, laneBlocker, teamId),
                phase,
                world.BallOwnerId,
                OffBallAssignmentKind.BlockPrimaryLane);
            available.Remove(laneBlocker);
        }

        AssignRemaining(world, teamId, phase, available, intents);
        AssignGoalkeeper(world, teamId, phase, intents);
    }

    private static StringName SelectPresser(
        FootballWorldSnapshot world,
        StringName teamId,
        IReadOnlyList<StringName> candidates)
    {
        StringName selected = new();
        float bestScore = float.PositiveInfinity;
        Vector2 ownGoal = world.OwnGoal(teamId);
        foreach (StringName playerId in candidates)
        {
            float ballDistance = FootballPitchDimensions.DistanceMeters(world.Positions[playerId], world.BallPosition);
            float goalProtection = FootballPitchDimensions.DistanceMeters(world.Positions[playerId], ownGoal);
            bool centralDefender = world.PlayerRoles[playerId] == "CB";
            bool ballNearOwnGoal = FootballPitchDimensions.DistanceMeters(world.BallPosition, ownGoal) < 30f;
            // Keep a comparable midfielder pressing, but do not ignore a CB who can stop an immediate shot.
            float abandonDangerPenalty = centralDefender && ballNearOwnGoal && ballDistance > 4f ? 4f : 0f;
            float score = ballDistance + abandonDangerPenalty - Mathf.Clamp(goalProtection / 100f, 0f, 0.25f);
            if (score < bestScore ||
                Mathf.IsEqualApprox(score, bestScore) &&
                FootballIntentPlanner.ComparePlayerIds(playerId, selected) < 0)
            {
                selected = playerId;
                bestScore = score;
            }
        }
        return selected;
    }

    // Near goal the cover player stops covering the presser and blocks the carrier's line to goal instead.
    private bool AssignShotLineBlocker(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        List<StringName> available,
        Dictionary<StringName, PlayerIntent> intents)
    {
        Vector2 ownGoal = world.OwnGoal(teamId);
        if (!world.PlayerTeams.TryGetValue(world.BallOwnerId, out StringName? ownerTeamId) ||
            ownerTeamId == teamId ||
            FootballPitchDimensions.DistanceMeters(world.BallPosition, ownGoal) >
            _configuration.ShotLineBlockActivationDistanceMeters)
        {
            return false;
        }

        Vector2 target = DefensiveBlockTargeter.ShotLineBlockTarget(
            world.BallPosition,
            ownGoal,
            _configuration.ShotLineBlockOffsetMeters);
        StringName blocker = SelectClosestDefender(world, available, target);
        if (blocker == new StringName())
        {
            return false;
        }
        intents[blocker] = Intent(
            PlayerIntentKind.CoverPress,
            target,
            phase,
            world.BallOwnerId,
            OffBallAssignmentKind.BlockShotLine);
        available.Remove(blocker);
        return true;
    }

    private static StringName SelectLaneBlocker(
        FootballWorldSnapshot world,
        StringName teamId,
        IReadOnlyList<StringName> candidates)
    {
        Vector2 laneTarget = world.BallPosition.Lerp(world.OwnGoal(teamId), 0.30f);
        return SelectClosest(world, candidates, laneTarget);
    }

    private void AssignThreats(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        List<StringName> available,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> threats = DangerousThreats(world, teamId);
        foreach (StringName threatId in threats)
        {
            // Keep one cover player and one lane blocker after assigning the most dangerous receivers.
            if (available.Count <= 2)
            {
                break;
            }
            StringName defender = SelectClosestDefender(world, available, world.Positions[threatId]);
            float defenderDistance = FootballPitchDimensions.DistanceMeters(
                world.Positions[defender],
                world.Positions[threatId]);
            if (defenderDistance > _configuration.AssignmentHandoffDistanceMeters &&
                world.PlayerRoles[defender] is not ("CB" or "LB" or "RB" or "DM"))
            {
                continue;
            }
            intents[defender] = Intent(
                PlayerIntentKind.MarkOpponent,
                DefensiveBlockTargeter.MarkTarget(world, defender, teamId, threatId),
                phase,
                threatId,
                OffBallAssignmentKind.TrackRunner);
            available.Remove(defender);
        }
    }

    private static void AssignRemaining(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        IReadOnlyList<StringName> available,
        Dictionary<StringName, PlayerIntent> intents)
    {
        Vector2 ownGoal = world.OwnGoal(teamId);
        foreach (StringName playerId in available)
        {
            string role = world.PlayerRoles[playerId];
            float goalDistance = FootballPitchDimensions.DistanceMeters(world.Positions[playerId], ownGoal);
            OffBallAssignmentKind assignment;
            PlayerIntentKind kind;
            Vector2 target;
            if (phase == LiveTeamPhase.TransitionToDefence)
            {
                assignment = OffBallAssignmentKind.RecoverGoalSide;
                kind = PlayerIntentKind.RecoverGoalSide;
                target = DefensiveBlockTargeter.RecoveryTarget(world, playerId, teamId);
            }
            else if (role is "CB" or "LB" or "RB" || goalDistance < 30f)
            {
                assignment = goalDistance < 24f
                    ? OffBallAssignmentKind.ProtectBox
                    : OffBallAssignmentKind.HoldLine;
                kind = PlayerIntentKind.HoldShape;
                target = DefensiveBlockTargeter.ShapeTarget(world, playerId, teamId);
            }
            else
            {
                assignment = OffBallAssignmentKind.ClaimSecondBallZone;
                kind = PlayerIntentKind.HoldShape;
                target = world.Positions[playerId].Lerp(world.BallPosition, 0.25f);
            }
            intents[playerId] = Intent(kind, target, phase, assignment: assignment);
        }
    }

    private static void AssignGoalkeeper(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] != "GK")
            {
                continue;
            }
            PlayerIntent goalkeeper = FootballIntentPlanner.GoalkeeperIntent(world, playerId, teamId, phase);
            intents[playerId] = new PlayerIntent(
                goalkeeper.Kind,
                goalkeeper.Target,
                goalkeeper.TeamPhase,
                goalkeeper.RelatedPlayerId,
                OffBallAssignmentKind.Goalkeep,
                "goalkeeper");
        }
    }

    private List<StringName> DangerousThreats(FootballWorldSnapshot world, StringName teamId)
    {
        List<StringName> threats = new();
        List<StringName> fallback = new();
        Vector2 ownGoal = world.OwnGoal(teamId);
        foreach ((StringName playerId, Vector2 position) in world.Positions)
        {
            if (world.PlayerTeams[playerId] == teamId || world.PlayerRoles[playerId] == "GK" ||
                playerId == world.BallOwnerId)
            {
                continue;
            }
            fallback.Add(playerId);
            if (FootballPitchDimensions.DistanceMeters(position, ownGoal) <=
                _configuration.DangerousReceiverDistanceMeters + 18f)
            {
                threats.Add(playerId);
            }
        }
        Comparison<StringName> byGoalDanger = (first, second) =>
        {
            int goalComparison = PlayerProximity.DistanceSquaredMeters(world.Positions[first], ownGoal)
                .CompareTo(PlayerProximity.DistanceSquaredMeters(world.Positions[second], ownGoal));
            return goalComparison != 0
                ? goalComparison
                : FootballIntentPlanner.ComparePlayerIds(first, second);
        };
        threats.Sort(byGoalDanger);
        fallback.Sort(byGoalDanger);
        foreach (StringName playerId in fallback)
        {
            if (threats.Count >= 2)
            {
                break;
            }
            if (!threats.Contains(playerId))
            {
                threats.Add(playerId);
            }
        }
        return threats;
    }

    private static StringName SelectClosestDefender(
        FootballWorldSnapshot world,
        IReadOnlyList<StringName> candidates,
        Vector2 point)
    {
        List<StringName> defenders = new();
        foreach (StringName playerId in candidates)
        {
            if (world.PlayerRoles[playerId] is "CB" or "LB" or "RB" or "DM")
            {
                defenders.Add(playerId);
            }
        }
        return SelectClosest(world, defenders.Count > 0 ? defenders : candidates, point);
    }

    private static StringName SelectClosest(
        FootballWorldSnapshot world,
        IReadOnlyList<StringName> candidates,
        Vector2 point)
    {
        StringName selected = new();
        float bestDistance = float.PositiveInfinity;
        foreach (StringName playerId in candidates)
        {
            float distance = PlayerProximity.DistanceSquaredMeters(world.Positions[playerId], point);
            if (distance < bestDistance ||
                Mathf.IsEqualApprox(distance, bestDistance) &&
                FootballIntentPlanner.ComparePlayerIds(playerId, selected) < 0)
            {
                selected = playerId;
                bestDistance = distance;
            }
        }
        return selected;
    }

    private static PlayerIntent Intent(
        PlayerIntentKind kind,
        Vector2 target,
        LiveTeamPhase phase,
        StringName? relatedPlayerId = null,
        OffBallAssignmentKind assignment = OffBallAssignmentKind.None)
    {
        Vector2 clamped = SpaceEvaluator.ClampToPitch(target);
        return new PlayerIntent(
            kind,
            clamped,
            phase,
            relatedPlayerId,
            assignment,
            SpaceOccupationMap.ZoneFor(clamped).Key);
    }
}
