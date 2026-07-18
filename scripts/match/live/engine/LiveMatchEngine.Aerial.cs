using System.Collections.Generic;
using Godot;

public sealed partial class LiveMatchEngine
{
    private const float AerialSecondBallSpeedMetersPerSecond = 4.8f;

    private static AerialDeliveryType? AerialDeliveryFor(BallActionKind kind)
    {
        return kind switch
        {
            BallActionKind.LoftedPass => AerialDeliveryType.LoftedPass,
            BallActionKind.Cross => AerialDeliveryType.Cross,
            BallActionKind.Clearance => AerialDeliveryType.Clearance,
            BallActionKind.HeaderPass => AerialDeliveryType.HeaderPass,
            BallActionKind.HeaderClearance => AerialDeliveryType.HeaderClearance,
            _ => null
        };
    }

    private void ResolveAerialArrival(BallActionKind completedKind, StringName intendedReceiverId)
    {
        if (Simulation is null)
        {
            return;
        }

        StringName contestAttackingTeamId = completedKind == BallActionKind.Clearance
            ? OpposingTeam(_actionSourceTeamId)
            : _actionSourceTeamId;
        List<AerialDuelCandidate> candidates = BuildAerialDuelCandidates(contestAttackingTeamId);
        int nearbyOpponentCount = CountNearbyOpponentsForGoalkeeper(candidates);
        float actionRoll = DecisionRoll(
            _actionSourceId,
            intendedReceiverId,
            _decisionSerial + AerialDuels * 41 + 811);
        AerialDuelResolution resolution = _aerialDuelResolver.Resolve(
            candidates,
            nearbyOpponentCount,
            actionRoll);
        AerialDuels++;
        _aerialContenderIds.Clear();

        if (resolution.HasWinner && resolution.WinnerId == _pendingOffsideReceiverId)
        {
            ResolveOffside(resolution.WinnerId);
            return;
        }
        _pendingOffsideReceiverId = new StringName();

        switch (resolution.Outcome)
        {
            case AerialDuelOutcome.GoalkeeperCatch:
                GoalkeeperAerialCatches++;
                GivePossessionTo(resolution.WinnerId, 0.75f);
                SetAction($"{PlayerName(resolution.WinnerId)} lao ra bắt gọn bóng bổng");
                return;
            case AerialDuelOutcome.GoalkeeperPunch:
                GoalkeeperPunches++;
                StartAerialClearance(resolution.WinnerId, true);
                return;
            case AerialDuelOutcome.DefensiveHeaderClearance:
                HeadersWon++;
                DefensiveHeaders++;
                StartAerialClearance(resolution.WinnerId, false);
                return;
            case AerialDuelOutcome.HeaderShot:
                HeadersWon++;
                HeaderShots++;
                StartHeaderShot(resolution.WinnerId);
                return;
            case AerialDuelOutcome.HeaderPass:
                HeadersWon++;
                StartHeaderPass(resolution.WinnerId);
                return;
            default:
                StartAerialSecondBall("Không ai khống chế được pha không chiến — bóng hai bật ra");
                return;
        }
    }

    private List<AerialDuelCandidate> BuildAerialDuelCandidates(StringName contestAttackingTeamId)
    {
        List<AerialDuelCandidate> candidates = new();
        foreach (StringName playerId in _aerialContenderIds)
        {
            if (!CurrentPositions.ContainsKey(playerId))
            {
                continue;
            }

            FootballPlayer? player = GetPlayer(playerId);
            StringName teamId = _playerTeams[playerId];
            bool isAttackingTeam = teamId == contestAttackingTeamId;
            Vector2 attackingGoal = new(AttackingGoalX(teamId), 0.5f);
            float distanceToLanding = FootballPitchDimensions.DistanceMeters(
                CurrentPositions[playerId],
                BallPosition);
            AerialArrivalEstimate arrival = _aerialLandingPredictor.Estimate(
                CurrentPositions[playerId],
                BallPosition,
                _playerPaces[playerId],
                0f);
            candidates.Add(new AerialDuelCandidate(
                playerId,
                isAttackingTeam,
                _playerRoles[playerId] == "GK",
                _playerRoles[playerId],
                player?.Heading ?? 50,
                player?.JumpingReach ?? 50,
                player?.Strength ?? 50,
                player?.positioning ?? 50,
                player?.Composure ?? 50,
                player?.goalkeeping ?? 10,
                distanceToLanding,
                arrival.ArrivalMarginSeconds,
                FootballPitchDimensions.DistanceMeters(BallPosition, attackingGoal),
                HasNearbyHeaderOption(playerId),
                DecisionRoll(playerId, _actionSourceId, _decisionSerial + AerialDuels * 53 + 827)));
        }
        return candidates;
    }

    private int CountNearbyOpponentsForGoalkeeper(IReadOnlyList<AerialDuelCandidate> candidates)
    {
        StringName goalkeeperTeamId = new();
        foreach (AerialDuelCandidate candidate in candidates)
        {
            if (candidate.IsGoalkeeper)
            {
                goalkeeperTeamId = _playerTeams[candidate.PlayerId];
                break;
            }
        }
        if (goalkeeperTeamId == new StringName())
        {
            return 0;
        }

        int count = 0;
        foreach (AerialDuelCandidate candidate in candidates)
        {
            if (_playerTeams[candidate.PlayerId] != goalkeeperTeamId &&
                candidate.DistanceToLandingMeters <= 3f)
            {
                count++;
            }
        }
        return count;
    }

    private void StartHeaderShot(StringName playerId)
    {
        SetAerialActionOwner(playerId);
        StringName nearestOpponentId = NearestOpponent(playerId);
        float pressureDistance = nearestOpponentId == new StringName()
            ? float.PositiveInfinity
            : FootballPitchDimensions.DistanceMeters(
                CurrentPositions[playerId],
                CurrentPositions[nearestOpponentId]);
        StartLiveShot(playerId, pressureDistance, true);
    }

    private void StartHeaderPass(StringName playerId)
    {
        StringName targetId = ChooseHeaderPassTarget(playerId);
        if (targetId == new StringName())
        {
            StartAerialSecondBall($"{PlayerName(playerId)} đánh đầu nhưng không tìm được đồng đội");
            return;
        }

        SetAerialActionOwner(playerId);
        PassAttempts++;
        Simulation!.RegisterLivePassAttempt(_playerTeams[playerId]);
        _pendingPassType = LivePassType.Lofted;
        _pendingPassSpeedMetersPerSecond = 13f;
        StartBallAction(
            CurrentPositions[targetId],
            0.65f,
            0.02f,
            targetId,
            BallActionKind.HeaderPass);
        SetAction($"{PlayerName(playerId)} đánh đầu chuyền cho {PlayerName(targetId)}");
    }

    private StringName ChooseHeaderPassTarget(StringName playerId)
    {
        StringName teamId = _playerTeams[playerId];
        StringName bestTarget = new();
        float bestScore = float.PositiveInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == playerId ||
                _playerTeams[candidateId] != teamId ||
                _playerRoles[candidateId] == "GK")
            {
                continue;
            }
            float distance = FootballPitchDimensions.DistanceMeters(
                CurrentPositions[playerId],
                CurrentPositions[candidateId]);
            if (distance > 14f)
            {
                continue;
            }
            float pressure = SpaceEvaluator.OpponentPressure(
                CurrentPositions[candidateId],
                teamId,
                CurrentPositions,
                _playerTeams);
            float score = distance + pressure * 8f;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidateId;
            }
        }
        return bestTarget;
    }

    private void StartAerialClearance(StringName playerId, bool isGoalkeeperPunch)
    {
        SetAerialActionOwner(playerId);
        StringName teamId = _playerTeams[playerId];
        Vector2 ownGoalMeters = FootballPitchDimensions.ToMeters(new Vector2(OwnGoalX(teamId), 0.5f));
        Vector2 ballMeters = FootballPitchDimensions.ToMeters(BallPosition);
        Vector2 awayFromGoal = ballMeters - ownGoalMeters;
        Vector2 direction = awayFromGoal.LengthSquared() > 0.001f
            ? awayFromGoal.Normalized()
            : new Vector2(AttackDirection(teamId), 0f);
        float lateral = DecisionRoll(playerId, _actionSourceId, _decisionSerial + 863) < 0.5f ? -0.28f : 0.28f;
        direction = (direction + new Vector2(0f, lateral)).Normalized();
        float distanceMeters = isGoalkeeperPunch ? 13f : 17f;
        Vector2 destination = SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(ballMeters + direction * distanceMeters));
        StartBallAction(
            destination,
            0.85f,
            0.04f,
            new StringName(),
            BallActionKind.HeaderClearance);
        SetAction(isGoalkeeperPunch
            ? $"{PlayerName(playerId)} lao ra đấm bóng khỏi vùng nguy hiểm"
            : $"{PlayerName(playerId)} bật cao đánh đầu phá bóng");
    }

    private void StartAerialSecondBall(string description)
    {
        AerialSecondBalls++;
        Vector2 flightMeters = FootballPitchDimensions.ToMeters(_ballActionTo) -
                               FootballPitchDimensions.ToMeters(_ballActionFrom);
        Vector2 direction = flightMeters.LengthSquared() > 0.001f
            ? flightMeters.Normalized()
            : Vector2.Right;
        StartLooseBall(description, direction * AerialSecondBallSpeedMetersPerSecond);
    }

    private void SetAerialActionOwner(StringName playerId)
    {
        _state.BallOwnerId = playerId;
        _state.ActiveTeamId = _playerTeams[playerId];
        Simulation?.set_live_possession(_state.ActiveTeamId);
        BallPosition = CurrentPositions[playerId];
    }

    private bool HasNearbyHeaderOption(StringName playerId)
    {
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId != playerId &&
                _playerTeams[candidateId] == _playerTeams[playerId] &&
                _playerRoles[candidateId] != "GK" &&
                FootballPitchDimensions.DistanceMeters(
                    CurrentPositions[playerId],
                    CurrentPositions[candidateId]) <= 14f)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsInsideOwnPenaltyArea(Vector2 position, StringName teamId)
    {
        float depthMeters = Mathf.Abs(position.X - OwnGoalX(teamId)) *
                            FootballPitchDimensions.LengthMeters;
        float lateralMeters = Mathf.Abs(position.Y - 0.5f) *
                              FootballPitchDimensions.WidthMeters;
        return depthMeters <= FootballPitchDimensions.PenaltyAreaDepthMeters &&
               lateralMeters <= FootballPitchDimensions.PenaltyAreaWidthMeters * 0.5f;
    }

    private StringName OpposingTeam(StringName teamId)
    {
        if (Simulation is null)
        {
            return new StringName();
        }
        return teamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
    }
}
