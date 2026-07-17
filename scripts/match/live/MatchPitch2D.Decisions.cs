using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MatchPitch2D
{
    private void DecideNextAction()
    {
        if (Simulation is null || Simulation.is_finished || _ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return;
        _decisionSerial++;
        _decisionsSinceShot++;
        StringName ownerId = _ballOwnerId;
        if (_playerTeams[ownerId] != _activeTeamId)
        {
            _activeTeamId = _playerTeams[ownerId];
            Simulation.set_live_possession(_activeTeamId);
            SelectPhasePlayers();
        }

        StringName nearestOpponent = NearestOpponent(ownerId);
        float pressureDistance = nearestOpponent != new StringName()
            ? CurrentPositions[ownerId].DistanceTo(CurrentPositions[nearestOpponent])
            : 1f;
        float pressureDistanceMeters = nearestOpponent != new StringName()
            ? FootballPitchDimensions.DistanceMeters(CurrentPositions[ownerId], CurrentPositions[nearestOpponent])
            : float.PositiveInfinity;
        if (pressureDistance < 0.068f && TryResolveTackle(ownerId, nearestOpponent, pressureDistance))
            return;

        if (TryContinueDirectAttack(ownerId, pressureDistanceMeters))
            return;

        FootballPlayer? owner = GetPlayer(ownerId);
        bool underPressure = pressureDistance < 0.105f;
        StringName goalkeeperSupport = ChooseGoalkeeperBackPass(ownerId, underPressure);
        if (goalkeeperSupport != new StringName())
        {
            StartPass(goalkeeperSupport, BallActionKind.Pass);
            return;
        }
        if (_playerRoles[ownerId] == "GK" && !underPressure)
        {
            StringName distributionTarget = ChooseGoalkeeperDistributionTarget(ownerId);
            if (distributionTarget != new StringName())
            {
                StartPass(distributionTarget, BallActionKind.Pass);
                return;
            }
        }
        if (ShouldClearBall(ownerId, underPressure))
        {
            StartClearance(ownerId);
            return;
        }
        if (Simulation.use_live_pitch_events && ShouldShoot(ownerId, pressureDistanceMeters))
        {
            StartLiveShot(ownerId, pressureDistanceMeters);
            return;
        }
        if (_attackProgress > 0.62f &&
            _decisionsSinceShot >= 9 &&
            _primaryRunnerId != new StringName() &&
            ownerId != _primaryRunnerId &&
            !IsCurrentlyOffside(_primaryRunnerId))
        {
            StartPass(_primaryRunnerId, BallActionKind.ThroughBall);
            return;
        }
        bool widePlayer = _playerRoles[ownerId] is "LB" or "RB" or "LW" or "RW";
        if (_attackProgress > 0.68f && widePlayer)
        {
            StringName receiver = _primaryRunnerId != new StringName() && !IsCurrentlyOffside(_primaryRunnerId)
                ? _primaryRunnerId
                : ChoosePassTarget(false);
            StartPass(receiver, BallActionKind.Cross);
            return;
        }

        float dribbleIntent = DecisionRoll(ownerId, nearestOpponent, _decisionSerial);
        int dribbling = owner?.dribbling ?? 50;
        if ((!underPressure && dribbling >= 67 && dribbleIntent < 0.34f) ||
            (underPressure && dribbling >= 75 && dribbleIntent < 0.22f))
        {
            StartDribble(ownerId, underPressure);
            return;
        }

        StringName target = ChoosePassTarget(underPressure);
        if (target == new StringName())
        {
            StartDribble(ownerId, underPressure);
            return;
        }

        int creativeSkill = ((owner?.passing ?? 50) + (owner?.vision ?? 50)) / 2;
        BallActionKind kind = target == _primaryRunnerId && creativeSkill >= 68 && !underPressure
            ? BallActionKind.ThroughBall
            : BallActionKind.Pass;
        StartPass(target, kind);
    }

    private bool ShouldClearBall(StringName ownerId, bool underPressure)
    {
        string role = _playerRoles[ownerId];
        bool defensiveRole = role is "GK" or "CB" or "LB" or "RB" or "DM";
        if (!defensiveRole || _attackProgress > 0.40f)
        {
            return false;
        }

        float clearanceIntent = DecisionRoll(ownerId, _pressingPlayerId, _decisionSerial + 307);
        float threshold = underPressure ? 0.62f : role == "GK" ? 0.34f : 0.18f;
        return clearanceIntent < threshold;
    }

    private StringName ChooseGoalkeeperDistributionTarget(StringName goalkeeperId)
    {
        StringName teamId = _playerTeams[goalkeeperId];
        Vector2 goalkeeperPosition = CurrentPositions[goalkeeperId];
        return CurrentPositions.Keys
            .Where(id => id != goalkeeperId &&
                         _playerTeams[id] == teamId &&
                         _playerRoles[id] is "CB" or "LB" or "RB" or "DM")
            .Where(id => FootballPitchDimensions.DistanceMeters(goalkeeperPosition, CurrentPositions[id]) <= 36f)
            .OrderBy(id =>
                SpaceEvaluator.OpponentPressure(CurrentPositions[id], teamId, CurrentPositions, _playerTeams) * 12f +
                FootballPitchDimensions.DistanceMeters(goalkeeperPosition, CurrentPositions[id]))
            .FirstOrDefault() ?? new StringName();
    }

    private StringName ChooseGoalkeeperBackPass(StringName passerId, bool isUnderPressure)
    {
        if (_playerRoles[passerId] == "GK")
        {
            return new StringName();
        }

        StringName goalkeeperId = ChooseGoalkeeper(_playerTeams[passerId]);
        if (goalkeeperId == new StringName() || goalkeeperId == passerId)
        {
            return new StringName();
        }

        float distanceMeters = FootballPitchDimensions.DistanceMeters(
            CurrentPositions[passerId],
            CurrentPositions[goalkeeperId]);
        float laneRisk = PassingLaneRisk(
            CurrentPositions[passerId],
            CurrentPositions[goalkeeperId],
            _playerTeams[passerId]);
        bool shouldUseBackPass = _traditionalGoalkeeperPlanner.ShouldUseBackPass(
            _playerRoles[passerId],
            _attackProgress,
            isUnderPressure,
            distanceMeters,
            laneRisk,
            DecisionRoll(passerId, goalkeeperId, _decisionSerial + 331));
        return shouldUseBackPass ? goalkeeperId : new StringName();
    }

    private void StartClearance(StringName playerId)
    {
        Vector2 start = CurrentPositions[playerId];
        float direction = AttackDirection(_playerTeams[playerId]);
        float distance = Mathf.Lerp(0.28f, 0.46f, DecisionRoll(playerId, _pressingPlayerId, _decisionSerial + 311));
        float targetLane = Mathf.Lerp(0.16f, 0.84f, DecisionRoll(playerId, _activeTeamId, _decisionSerial + 313));
        Vector2 destination = ClampToPitch(new Vector2(start.X + direction * distance, targetLane));
        float flightDistance = FootballPitchDimensions.DistanceMeters(start, destination);
        float duration = Mathf.Clamp(flightDistance / 30f, 0.55f, 1.35f);
        Clearances++;
        StartBallAction(destination, duration, 0.075f, new StringName(), BallActionKind.Clearance);
        SetAction($"{PlayerName(playerId)} phá bóng lên khoảng trống");
    }

    private void StartDribble(StringName ownerId, bool escapingPressure)
    {
        FootballPlayer? player = GetPlayer(ownerId);
        float quality = ((player?.dribbling ?? 50) + (player?.pace ?? 50)) / 198f;
        _attackProgress = Mathf.Clamp(_attackProgress + Mathf.Lerp(0.035f, 0.075f, quality), 0.12f, 0.94f);
        _phaseLane = Mathf.Lerp(_phaseLane, CurrentPositions[ownerId].Y, 0.55f);
        Dribbles++;
        _nextDecisionTime = _visualTime + (escapingPressure ? 0.48f : 0.62f);
        SetAction(escapingPressure
            ? $"{PlayerName(ownerId)} thoát pressing"
            : $"{PlayerName(ownerId)} dẫn bóng lên phía trước");
    }

    private bool TryResolveTackle(StringName ownerId, StringName defenderId, float distance)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float tackleSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float controlSkill = ((owner?.dribbling ?? 50) + (owner?.pace ?? 50)) * 0.5f;
        float contactBonus = Mathf.Clamp((0.068f - distance) / 0.068f, 0, 1) * 0.28f;
        float chance = Mathf.Clamp(0.22f + (tackleSkill - controlSkill) / 145f + contactBonus, 0.08f, 0.72f);
        float tackleRoll = DecisionRoll(ownerId, defenderId, _decisionSerial + 41);
        if (tackleRoll >= chance)
        {
            float foulChance = Mathf.Clamp(
                0.10f + (controlSkill - tackleSkill) / 210f + contactBonus * 0.30f,
                0.05f, 0.42f);
            if (DecisionRoll(defenderId, ownerId, _decisionSerial + 59) < foulChance)
            {
                ResolveLiveFoul(defenderId, ownerId);
                return true;
            }
            return false;
        }

        _ballOwnerId = defenderId;
        _activeTeamId = _playerTeams[defenderId];
        Simulation!.set_live_possession(_activeTeamId);
        _attackProgress = Mathf.Clamp(AttackProgress(_activeTeamId, BallPosition), 0.16f, 0.62f);
        _phaseLane = CurrentPositions[defenderId].Y;
        Interceptions++;
        SelectPhasePlayers();
        _nextDecisionTime = _visualTime + 0.38f;
        SetAction($"{PlayerName(defenderId)} đoạt bóng từ {PlayerName(ownerId)}");
        return true;
    }

    private void ResolveLiveFoul(StringName offenderId, StringName victimId)
    {
        if (Simulation is null)
            return;
        StringName foulingTeamId = _playerTeams[offenderId];
        StringName victimTeamId = _playerTeams[victimId];
        FootballPlayer? offender = GetPlayer(offenderId);
        float cardRoll = DecisionRoll(offenderId, victimId, _decisionSerial + 83);
        bool stopsClearChance = _attackProgress > 0.78f && _playerRoles[victimId] is "ST" or "LW" or "RW" or "AM";
        StringName card = stopsClearChance && cardRoll < 0.18f
            ? "red"
            : cardRoll < Mathf.Clamp(0.30f + (68 - (offender?.tackling ?? 50)) / 120f, 0.18f, 0.58f)
                ? "yellow"
                : new StringName();

        FootballMatchEvent? foulEvent = Simulation.register_live_foul(
            foulingTeamId, offenderId, victimId, card);
        if (foulEvent is not null)
            EmitSignal(SignalName.LiveMatchEvent, foulEvent);
        FoulsCommitted++;
        if (card == "red")
            SyncLineups(false);
        ScheduleRestart("free_kick", victimTeamId, BallPosition);
        SetAction(card == "red"
            ? $"{PlayerName(offenderId)} bị truất quyền thi đấu"
            : $"{PlayerName(offenderId)} phạm lỗi với {PlayerName(victimId)}");
    }

    private StringName ChoosePassTarget(bool preferSafe = false)
    {
        if (Simulation is null)
            return new StringName();
        if (_ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return new StringName();

        float direction = AttackDirection(_activeTeamId);
        Vector2 owner = CurrentPositions[_ballOwnerId];
        int ownerVision = GetPlayer(_ballOwnerId)?.vision ?? 50;
        StringName bestId = new();
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == _ballOwnerId || _playerTeams[candidateId] != _activeTeamId || _playerRoles[candidateId] == "GK")
                continue;
            bool candidateIsOffside = IsCurrentlyOffside(candidateId);
            Vector2 candidate = TargetPositions.GetValueOrDefault(candidateId, CurrentPositions[candidateId]);
            float distance = owner.DistanceTo(candidate);
            if (distance > 0.48f || distance < 0.045f)
                continue;
            float forwardGain = direction * (candidate.X - owner.X);
            float laneRisk = PassingLaneRisk(owner, candidate, _activeTeamId);
            float receivingPressure = SpaceEvaluator.OpponentPressure(
                candidate,
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
            float forwardWeight = preferSafe ? 1.35f : 2.7f;
            float score = forwardGain * forwardWeight - distance * 0.42f -
                          receivingPressure * (preferSafe ? 0.72f : 0.46f) -
                          laneRisk * (preferSafe ? 1.45f : 0.82f);
            score += _decisionVarietyTracker.PassScoreAdjustment(
                candidateId,
                VarietyRoll(_ballOwnerId, candidateId, _decisionSerial + _phaseSerial * 97),
                preferSafe);
            if (candidateIsOffside)
            {
                score -= Mathf.Lerp(0.62f, 1.25f, ownerVision / 99f);
            }
            if (_playerIntents.TryGetValue(candidateId, out PlayerIntent? intent))
            {
                score += intent.Kind switch
                {
                    PlayerIntentKind.ReceivePass => 0.34f,
                    PlayerIntentKind.RunIntoSpace => _attackProgress > 0.52f ? 0.28f : 0.10f,
                    PlayerIntentKind.SupportBall => preferSafe ? 0.24f : 0.12f,
                    _ => 0f
                };
            }
            if (score <= bestScore) continue;
            bestScore = score;
            bestId = candidateId;
        }
        return bestId;
    }

    private bool IsCurrentlyOffside(StringName playerId)
    {
        return _offsideRule.IsOffside(
            playerId,
            _activeTeamId,
            BallPosition,
            AttackDirection(_activeTeamId),
            CurrentPositions,
            _playerTeams);
    }
}
