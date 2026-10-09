using System;
using System.Collections.Generic;
using Godot;

public sealed class OffBallIntentCoordinator
{
    private sealed class CommittedIntent
    {
        public CommittedIntent(PlayerIntent intent, float expiresAt)
        {
            Intent = intent;
            ExpiresAt = expiresAt;
        }

        public PlayerIntent Intent { get; }
        public float ExpiresAt { get; }
    }

    private readonly FootballIntentPlanner _planner;
    private readonly OffBallParticipationConfiguration _configuration;
    private readonly Dictionary<StringName, CommittedIntent> _committed = new();
    private int _observations;
    private float _totalTeamWidthMeters;
    private float _totalTeamLengthMeters;
    private float _totalCompactnessMeters;
    private int _sameTargetCollisions;
    private int _totalPassingOptions;
    private int _possessionObservations;
    private int _totalRunnerLaneDiversity;
    private int _runnerObservations;
    private int _totalRestDefencePlayers;
    private int _totalUnmarkedDangerousReceivers;
    private int _nearPostOccupations;
    private int _farPostOccupations;
    private int _cutBackOccupations;

    public OffBallIntentCoordinator(
        FootballIntentPlanner planner,
        OffBallParticipationConfiguration? configuration = null)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _configuration = configuration ?? OffBallParticipationConfiguration.CreateM3Defaults();
    }

    public OffBallMetricsSnapshot Metrics => new(
        _observations,
        _totalTeamWidthMeters,
        _totalTeamLengthMeters,
        _totalCompactnessMeters,
        _sameTargetCollisions,
        _totalPassingOptions,
        _possessionObservations,
        _totalRunnerLaneDiversity,
        _runnerObservations,
        _totalRestDefencePlayers,
        _totalUnmarkedDangerousReceivers,
        _nearPostOccupations,
        _farPostOccupations,
        _cutBackOccupations);

    public void Reset()
    {
        _committed.Clear();
        _observations = 0;
        _totalTeamWidthMeters = 0f;
        _totalTeamLengthMeters = 0f;
        _totalCompactnessMeters = 0f;
        _sameTargetCollisions = 0;
        _totalPassingOptions = 0;
        _possessionObservations = 0;
        _totalRunnerLaneDiversity = 0;
        _runnerObservations = 0;
        _totalRestDefencePlayers = 0;
        _totalUnmarkedDangerousReceivers = 0;
        _nearPostOccupations = 0;
        _farPostOccupations = 0;
        _cutBackOccupations = 0;
    }

    public Dictionary<StringName, PlayerIntent> Plan(FootballWorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Dictionary<StringName, PlayerIntent> planned = _planner.Plan(world);
        bool retainedCommitment = ApplyCommitment(world, planned);
        if (retainedCommitment)
        {
            TeamSpacingResolver.Resolve(world, planned);
        }
        OnsideRunPlanner.ConstrainTargets(world, planned, _configuration);
        Observe(world, planned);
        return planned;
    }

    private bool ApplyCommitment(
        FootballWorldSnapshot world,
        Dictionary<StringName, PlayerIntent> planned)
    {
        bool retainedCommitment = false;
        foreach (StringName playerId in FootballIntentPlanner.TeamPlayers(world, world.HomeTeamId))
        {
            retainedCommitment |= StabilizePlayer(world, planned, playerId);
        }
        List<StringName> otherTeamPlayers = new();
        foreach (StringName playerId in world.Positions.Keys)
        {
            if (world.PlayerTeams[playerId] != world.HomeTeamId)
            {
                otherTeamPlayers.Add(playerId);
            }
        }
        otherTeamPlayers.Sort(FootballIntentPlanner.ComparePlayerIds);
        foreach (StringName playerId in otherTeamPlayers)
        {
            retainedCommitment |= StabilizePlayer(world, planned, playerId);
        }
        return retainedCommitment;
    }

    private bool StabilizePlayer(
        FootballWorldSnapshot world,
        Dictionary<StringName, PlayerIntent> planned,
        StringName playerId)
    {
        if (!planned.TryGetValue(playerId, out PlayerIntent? next))
        {
            _committed.Remove(playerId);
            return false;
        }
        if (IsImmediateAssignment(next.Assignment))
        {
            _committed[playerId] = new CommittedIntent(next, world.GameTimeSeconds);
            return false;
        }
        if (_committed.TryGetValue(playerId, out CommittedIntent? previous) &&
            world.GameTimeSeconds < previous.ExpiresAt &&
            CanRetain(world, playerId, previous.Intent, next))
        {
            if (IsDefensivePositionAssignment(previous.Intent.Assignment) &&
                IsDefensivePositionAssignment(next.Assignment))
            {
                // A zonal defender keeps the role but must follow the current ball, not a stale target.
                return false;
            }
            // Retain the marker identity, not an obsolete location of a moving opponent.
            planned[playerId] = previous.Intent.Assignment == OffBallAssignmentKind.TrackRunner
                ? new PlayerIntent(previous.Intent.Kind,
                    DefensiveBlockTargeter.MarkTarget(world, playerId, world.PlayerTeams[playerId],
                        previous.Intent.RelatedPlayerId), previous.Intent.TeamPhase,
                    previous.Intent.RelatedPlayerId, previous.Intent.Assignment)
                : previous.Intent;
            return true;
        }
        _committed[playerId] = new CommittedIntent(
            next,
            world.GameTimeSeconds + _configuration.MinimumAssignmentSeconds);
        return false;
    }

    private bool CanRetain(
        FootballWorldSnapshot world,
        StringName playerId,
        PlayerIntent previous,
        PlayerIntent next)
    {
        if (previous.TeamPhase != next.TeamPhase ||
            AssignmentFamily(previous.Assignment) != AssignmentFamily(next.Assignment))
        {
            return false;
        }
        if (previous.Assignment == OffBallAssignmentKind.TrackRunner)
        {
            if (previous.RelatedPlayerId == new StringName() ||
                !world.Positions.ContainsKey(previous.RelatedPlayerId))
            {
                return false;
            }
            return FootballPitchDimensions.DistanceMeters(
                world.Positions[playerId],
                world.Positions[previous.RelatedPlayerId]) <= _configuration.AssignmentHandoffDistanceMeters;
        }
        return FootballPitchDimensions.DistanceMeters(previous.Target, next.Target) <=
               _configuration.AssignmentHandoffDistanceMeters;
    }

    private void Observe(FootballWorldSnapshot world, IReadOnlyDictionary<StringName, PlayerIntent> intents)
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
        foreach (StringName teamId in teamIds)
        {
            ObserveTeamShape(world, intents, teamId);
            ObserveAssignments(world, intents, teamId);
        }
        _sameTargetCollisions += CountSameTargetCollisions(world, intents);
    }

    private void ObserveTeamShape(
        FootballWorldSnapshot world,
        IReadOnlyDictionary<StringName, PlayerIntent> intents,
        StringName teamId)
    {
        float minimumX = 1f;
        float maximumX = 0f;
        float minimumY = 1f;
        float maximumY = 0f;
        int playerCount = 0;
        foreach ((StringName playerId, PlayerIntent intent) in intents)
        {
            if (world.PlayerTeams[playerId] != teamId || world.PlayerRoles[playerId] == "GK")
            {
                continue;
            }
            minimumX = Mathf.Min(minimumX, intent.Target.X);
            maximumX = Mathf.Max(maximumX, intent.Target.X);
            minimumY = Mathf.Min(minimumY, intent.Target.Y);
            maximumY = Mathf.Max(maximumY, intent.Target.Y);
            playerCount++;
        }
        if (playerCount == 0)
        {
            return;
        }
        float length = (maximumX - minimumX) * FootballPitchDimensions.LengthMeters;
        float width = (maximumY - minimumY) * FootballPitchDimensions.WidthMeters;
        _observations++;
        _totalTeamLengthMeters += length;
        _totalTeamWidthMeters += width;
        _totalCompactnessMeters += Mathf.Sqrt(length * length + width * width);
    }

    private void ObserveAssignments(
        FootballWorldSnapshot world,
        IReadOnlyDictionary<StringName, PlayerIntent> intents,
        StringName teamId)
    {
        HashSet<int> runnerLanes = new();
        int passingOptions = 0;
        int restDefence = 0;
        foreach ((StringName playerId, PlayerIntent intent) in intents)
        {
            if (world.PlayerTeams[playerId] != teamId)
            {
                continue;
            }
            if (intent.Assignment is OffBallAssignmentKind.OfferShortSupport or
                OffBallAssignmentKind.OfferThirdManSupport or OffBallAssignmentKind.RecyclePossession)
            {
                passingOptions++;
            }
            if (IsRunnerAssignment(intent.Assignment))
            {
                runnerLanes.Add(SpaceOccupationMap.ZoneFor(intent.Target).Lane);
            }
            if (intent.Assignment == OffBallAssignmentKind.ProtectAgainstCounter)
            {
                restDefence++;
            }
            switch (intent.Assignment)
            {
                case OffBallAssignmentKind.AttackBoxNearPost:
                    _nearPostOccupations++;
                    break;
                case OffBallAssignmentKind.AttackBoxFarPost:
                    _farPostOccupations++;
                    break;
                case OffBallAssignmentKind.AttackBoxCutBackZone:
                    _cutBackOccupations++;
                    break;
            }
        }
        if (teamId == world.PossessionTeamId && !world.IsLooseBall)
        {
            _possessionObservations++;
            _totalPassingOptions += passingOptions;
            _totalRestDefencePlayers += restDefence;
            _runnerObservations++;
            _totalRunnerLaneDiversity += runnerLanes.Count;
        }
        else
        {
            _totalUnmarkedDangerousReceivers += CountUnmarkedDangerousReceivers(world, intents, teamId);
        }
    }

    private int CountUnmarkedDangerousReceivers(
        FootballWorldSnapshot world,
        IReadOnlyDictionary<StringName, PlayerIntent> intents,
        StringName defendingTeamId)
    {
        Vector2 ownGoal = world.OwnGoal(defendingTeamId);
        int count = 0;
        foreach ((StringName attackerId, Vector2 attackerPosition) in world.Positions)
        {
            if (world.PlayerTeams[attackerId] == defendingTeamId || world.PlayerRoles[attackerId] == "GK" ||
                FootballPitchDimensions.DistanceMeters(attackerPosition, ownGoal) >
                _configuration.DangerousReceiverDistanceMeters)
            {
                continue;
            }
            bool controlled = false;
            foreach ((StringName defenderId, PlayerIntent intent) in intents)
            {
                if (world.PlayerTeams[defenderId] != defendingTeamId)
                {
                    continue;
                }
                if (FootballPitchDimensions.DistanceMeters(
                        world.Positions[defenderId],
                        attackerPosition) <= _configuration.MarkerControlDistanceMeters)
                {
                    controlled = true;
                    break;
                }
            }
            if (!controlled)
            {
                count++;
            }
        }
        return count;
    }

    private int CountSameTargetCollisions(
        FootballWorldSnapshot world,
        IReadOnlyDictionary<StringName, PlayerIntent> intents)
    {
        List<StringName> players = new(intents.Keys);
        players.Sort(FootballIntentPlanner.ComparePlayerIds);
        int collisions = 0;
        for (int firstIndex = 0; firstIndex < players.Count; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < players.Count; secondIndex++)
            {
                StringName first = players[firstIndex];
                StringName second = players[secondIndex];
                if (world.PlayerTeams[first] != world.PlayerTeams[second] ||
                    IsImmediateAssignment(intents[first].Assignment) ||
                    IsImmediateAssignment(intents[second].Assignment))
                {
                    continue;
                }
                if (FootballPitchDimensions.DistanceMeters(intents[first].Target, intents[second].Target) <
                    _configuration.TargetReservationDistanceMeters * 0.55f)
                {
                    collisions++;
                }
            }
        }
        return collisions;
    }

    private static bool IsImmediateAssignment(OffBallAssignmentKind assignment)
    {
        return assignment is OffBallAssignmentKind.Goalkeep or
            OffBallAssignmentKind.CarryBall or
            OffBallAssignmentKind.ReceivePass or
            OffBallAssignmentKind.PressBall or
            OffBallAssignmentKind.BlockShotLine or
            OffBallAssignmentKind.ChaseLooseBall;
    }

    private static bool IsDefensivePositionAssignment(OffBallAssignmentKind assignment)
    {
        return assignment is OffBallAssignmentKind.ProtectBox or
            OffBallAssignmentKind.HoldLine or
            OffBallAssignmentKind.RecoverGoalSide;
    }

    private static int AssignmentFamily(OffBallAssignmentKind assignment)
    {
        if (assignment is OffBallAssignmentKind.OfferShortSupport or
            OffBallAssignmentKind.OfferThirdManSupport or OffBallAssignmentKind.RecyclePossession)
        {
            return 1;
        }
        if (IsRunnerAssignment(assignment))
        {
            return 2;
        }
        if (assignment is OffBallAssignmentKind.TrackRunner or OffBallAssignmentKind.ProtectBox or
            OffBallAssignmentKind.HoldLine or OffBallAssignmentKind.RecoverGoalSide)
        {
            return 3;
        }
        return (int)assignment + 10;
    }

    private static bool IsRunnerAssignment(OffBallAssignmentKind assignment)
    {
        return assignment is OffBallAssignmentKind.RunBehind or
            OffBallAssignmentKind.RunAcrossDefender or
            OffBallAssignmentKind.OccupyWideLane or
            OffBallAssignmentKind.AttackBoxNearPost or
            OffBallAssignmentKind.AttackBoxFarPost or
            OffBallAssignmentKind.AttackBoxCutBackZone;
    }
}
