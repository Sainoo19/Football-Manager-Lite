using System;
using System.Collections.Generic;
using Godot;

public sealed class OffBallRoleAllocator
{
    private readonly OffBallParticipationConfiguration _configuration;
    private readonly SupportOptionEvaluator _supportEvaluator;
    private readonly RunCoordinator _runCoordinator;
    private readonly RestDefenceAllocator _restDefenceAllocator = new();
    private readonly DefensiveAssignmentCoordinator _defensiveCoordinator;
    private readonly LooseBallRoleAllocator _looseBallAllocator;
    private readonly OffBallParticipationSizer _participationSizer = new();

    public OffBallRoleAllocator(OffBallParticipationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _supportEvaluator = new SupportOptionEvaluator(configuration);
        _runCoordinator = new RunCoordinator(configuration);
        _defensiveCoordinator = new DefensiveAssignmentCoordinator(configuration);
        _looseBallAllocator = new LooseBallRoleAllocator(configuration);
    }

    public Dictionary<StringName, PlayerIntent> Allocate(FootballWorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Dictionary<StringName, PlayerIntent> intents = new();
        List<StringName> teamIds = TeamIds(world);
        SpaceOccupationMap occupation = new(world);
        foreach (StringName teamId in teamIds)
        {
            LiveTeamPhase phase = world.PhaseFor(teamId);
            if (phase == LiveTeamPhase.LooseBall)
            {
                _looseBallAllocator.Allocate(world, teamId, intents);
            }
            else if (LiveTeamPhaseRules.IsPossessionPhase(phase) ||
                     phase == LiveTeamPhase.SetPiece && teamId == world.PossessionTeamId)
            {
                AllocatePossession(world, occupation, teamId, phase, intents);
            }
            else
            {
                _defensiveCoordinator.Assign(world, teamId, phase, intents);
            }
        }
        TeamSpacingResolver.Resolve(world, intents);
        OnsideRunPlanner.ConstrainTargets(world, intents, _configuration);
        return intents;
    }

    private void AllocatePossession(
        FootballWorldSnapshot world,
        SpaceOccupationMap occupation,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> outfield = FootballIntentPlanner.TeamOutfieldPlayers(world, teamId);
        List<StringName> restDefenders = _restDefenceAllocator.Select(
            world,
            teamId,
            outfield,
            world.RequiredRestDefencePlayersFor(teamId));
        HashSet<StringName> assigned = new(restDefenders);
        assigned.Add(world.BallOwnerId);
        if (world.ExpectedReceiverId != new StringName())
        {
            assigned.Add(world.ExpectedReceiverId);
        }

        foreach (StringName playerId in restDefenders)
        {
            Vector2 target = RestDefenceTargeter.Target(world, playerId, teamId);
            intents[playerId] = CreateIntent(
                PlayerIntentKind.HoldShape,
                target,
                LiveTeamPhase.RestDefence,
                assignment: OffBallAssignmentKind.ProtectAgainstCounter);
        }
        AssignOwnerAndReceiver(world, teamId, phase, intents);
        AssignGoalkeeper(world, teamId, phase, intents);

        List<StringName> available = new();
        foreach (StringName playerId in outfield)
        {
            if (!assigned.Contains(playerId))
            {
                available.Add(playerId);
            }
        }
        AssignWeakSideWidth(world, teamId, phase, available, intents);
        available.RemoveAll(intents.ContainsKey);
        int regionalAdvantage = _participationSizer.RegionalAdvantage(world, teamId);
        int supportCount = _participationSizer.SupportCount(world, phase, regionalAdvantage, available.Count);
        bool wideFinalThird = phase == LiveTeamPhase.FinalThird &&
                              (world.BallPosition.Y < 0.27f || world.BallPosition.Y > 0.73f);
        int requiredBoxRuns = wideFinalThird ? Math.Min(3, available.Count) : 0;
        if (available.Count - supportCount < requiredBoxRuns)
        {
            supportCount = Math.Max(available.Count - requiredBoxRuns, 0);
        }
        int runnerCount = _participationSizer.RunnerCount(
            phase,
            regionalAdvantage,
            available.Count - supportCount);
        List<Vector2> reservedTargets = new();
        AssignSupports(
            world,
            occupation,
            teamId,
            phase,
            available,
            supportCount,
            intents,
            reservedTargets);
        available.RemoveAll(intents.ContainsKey);
        _runCoordinator.AssignRuns(
            world,
            teamId,
            phase,
            available,
            runnerCount,
            intents,
            reservedTargets);
        available.RemoveAll(intents.ContainsKey);
        AssignStructurePlayers(world, teamId, phase, available, intents);
    }

    private static void AssignWeakSideWidth(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        IReadOnlyList<StringName> available,
        Dictionary<StringName, PlayerIntent> intents)
    {
        if (world.BallPosition.Y is >= 0.34f and <= 0.66f)
        {
            return;
        }
        StringName weakSidePlayer = new();
        float greatestLaneDistance = 0f;
        foreach (StringName playerId in available)
        {
            if (world.PlayerRoles[playerId] is not ("LW" or "RW"))
            {
                continue;
            }
            float laneDistance = Mathf.Abs(world.Positions[playerId].Y - world.BallPosition.Y);
            if (laneDistance > greatestLaneDistance)
            {
                weakSidePlayer = playerId;
                greatestLaneDistance = laneDistance;
            }
        }
        if (weakSidePlayer == new StringName() || greatestLaneDistance < 0.30f)
        {
            return;
        }
        float direction = world.AttackDirection(teamId);
        Vector2 target = new(
            Mathf.Clamp(world.BallPosition.X - direction * 0.02f, 0.04f, 0.96f),
            world.BasePositions[weakSidePlayer].Y);
        intents[weakSidePlayer] = CreateIntent(
            PlayerIntentKind.SupportBall,
            target,
            phase,
            assignment: OffBallAssignmentKind.HoldWidth);
    }

    private static void AssignOwnerAndReceiver(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        if (world.BallOwnerId != new StringName() &&
            world.PlayerTeams.TryGetValue(world.BallOwnerId, out StringName? ownerTeamId) &&
            ownerTeamId is not null &&
            ownerTeamId == teamId &&
            world.Positions.ContainsKey(world.BallOwnerId))
        {
            intents[world.BallOwnerId] = CreateIntent(
                PlayerIntentKind.CarryBall,
                AttackingRoleTargeter.CarrierTarget(world, world.BallOwnerId, teamId),
                phase,
                assignment: OffBallAssignmentKind.CarryBall);
        }
        if (world.IsBallInFlight && world.ExpectedReceiverId != new StringName() &&
            world.PlayerTeams.TryGetValue(world.ExpectedReceiverId, out StringName? receiverTeamId) &&
            receiverTeamId is not null &&
            receiverTeamId == teamId &&
            world.Positions.ContainsKey(world.ExpectedReceiverId))
        {
            intents[world.ExpectedReceiverId] = CreateIntent(
                PlayerIntentKind.ReceivePass,
                world.BallDestination,
                phase,
                world.BallOwnerId,
                OffBallAssignmentKind.ReceivePass);
        }
    }

    private void AssignSupports(
        FootballWorldSnapshot world,
        SpaceOccupationMap occupation,
        StringName teamId,
        LiveTeamPhase phase,
        IReadOnlyList<StringName> candidates,
        int requestedCount,
        Dictionary<StringName, PlayerIntent> intents,
        List<Vector2> reservedTargets)
    {
        List<StringName> ordered = new(candidates);
        ordered.Sort((first, second) =>
        {
            float firstScore = SupportPlayerPriority(world, first);
            float secondScore = SupportPlayerPriority(world, second);
            int scoreComparison = secondScore.CompareTo(firstScore);
            return scoreComparison != 0 ? scoreComparison : FootballIntentPlanner.ComparePlayerIds(first, second);
        });

        int assigned = 0;
        foreach (StringName playerId in ordered)
        {
            if (assigned >= Math.Min(requestedCount, _configuration.MaximumSupportPlayers))
            {
                break;
            }
            bool thirdMan = playerId == world.PreviousBallOwnerId &&
                            playerId != world.BallOwnerId;
            SupportOption option = _supportEvaluator.BestOption(
                world,
                occupation,
                playerId,
                teamId,
                reservedTargets,
                phase is LiveTeamPhase.CounterAttack or LiveTeamPhase.Progression);
            OffBallAssignmentKind assignment = thirdMan
                ? OffBallAssignmentKind.OfferThirdManSupport
                : OffBallAssignmentKind.OfferShortSupport;
            intents[playerId] = CreateIntent(
                PlayerIntentKind.SupportBall,
                option.Target,
                phase,
                world.BallOwnerId,
                assignment,
                option.TargetKey);
            reservedTargets.Add(option.Target);
            assigned++;
        }
    }

    private static float SupportPlayerPriority(FootballWorldSnapshot world, StringName playerId)
    {
        float distance = FootballPitchDimensions.DistanceMeters(world.Positions[playerId], world.BallPosition);
        float roleBonus = world.PlayerRoles[playerId] is "CM" or "AM" or "DM" ? 0.35f : 0f;
        float thirdManBonus = playerId == world.PreviousBallOwnerId ? 0.60f : 0f;
        return 1f - Mathf.Clamp(distance / 45f, 0f, 1f) + roleBonus + thirdManBonus;
    }

    private static void AssignStructurePlayers(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        IReadOnlyList<StringName> players,
        Dictionary<StringName, PlayerIntent> intents)
    {
        float direction = world.AttackDirection(teamId);
        foreach (StringName playerId in players)
        {
            string role = world.PlayerRoles[playerId];
            bool isWeakSideWidePlayer = role is "LW" or "RW" &&
                                        Mathf.Abs(world.Positions[playerId].Y - world.BallPosition.Y) > 0.28f;
            OffBallAssignmentKind assignment = isWeakSideWidePlayer
                ? OffBallAssignmentKind.HoldWidth
                : role is "CB" or "LB" or "RB" or "DM" or "CM"
                    ? OffBallAssignmentKind.RecyclePossession
                    : OffBallAssignmentKind.OccupyWideLane;
            Vector2 target = isWeakSideWidePlayer
                ? new Vector2(
                    Mathf.Clamp(world.BallPosition.X - direction * 0.02f, 0.04f, 0.96f),
                    world.BasePositions[playerId].Y)
                : FootballIntentPlanner.ShiftBaseTowardBall(world, playerId, 0.18f);
            intents[playerId] = CreateIntent(
                assignment == OffBallAssignmentKind.OccupyWideLane
                    ? PlayerIntentKind.RunIntoSpace
                    : PlayerIntentKind.HoldShape,
                target,
                phase,
                assignment: assignment);
        }
    }

    private static void AssignGoalkeeper(
        FootballWorldSnapshot world,
        StringName teamId,
        LiveTeamPhase phase,
        Dictionary<StringName, PlayerIntent> intents)
    {
        StringName goalkeeperId = FindGoalkeeper(world, teamId);
        if (goalkeeperId == new StringName())
        {
            return;
        }
        PlayerIntent goalkeeper = FootballIntentPlanner.GoalkeeperIntent(world, goalkeeperId, teamId, phase);
        intents[goalkeeperId] = new PlayerIntent(
            goalkeeper.Kind,
            goalkeeper.Target,
            goalkeeper.TeamPhase,
            goalkeeper.RelatedPlayerId,
            OffBallAssignmentKind.Goalkeep,
            "goalkeeper");
    }

    private static StringName FindGoalkeeper(FootballWorldSnapshot world, StringName teamId)
    {
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, teamId))
        {
            if (world.PlayerRoles[playerId] == "GK")
            {
                return playerId;
            }
        }
        return new StringName();
    }

    private static List<StringName> TeamIds(FootballWorldSnapshot world)
    {
        List<StringName> teamIds = new();
        foreach (StringName playerId in world.Positions.Keys)
        {
            StringName teamId = world.PlayerTeams[playerId];
            if (!teamIds.Contains(teamId))
            {
                teamIds.Add(teamId);
            }
        }
        teamIds.Sort(FootballIntentPlanner.ComparePlayerIds);
        return teamIds;
    }

    private static PlayerIntent CreateIntent(
        PlayerIntentKind kind,
        Vector2 target,
        LiveTeamPhase phase,
        StringName? relatedPlayerId = null,
        OffBallAssignmentKind assignment = OffBallAssignmentKind.None,
        string targetKey = "")
    {
        Vector2 clamped = SpaceEvaluator.ClampToPitch(target);
        return new PlayerIntent(
            kind,
            clamped,
            phase,
            relatedPlayerId,
            assignment,
            string.IsNullOrEmpty(targetKey) ? SpaceOccupationMap.ZoneFor(clamped).Key : targetKey);
    }
}
