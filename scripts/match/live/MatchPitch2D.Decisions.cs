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

        if (TryStartKickoffPass(ownerId))
        {
            return;
        }

        StringName nearestOpponent = NearestOpponent(ownerId);
        float pressureDistanceMeters = nearestOpponent != new StringName()
            ? FootballPitchDimensions.DistanceMeters(CurrentPositions[ownerId], CurrentPositions[nearestOpponent])
            : float.PositiveInfinity;
        if (_duelDistanceRules.CanAttemptTackle(pressureDistanceMeters) &&
            TryResolveTackle(ownerId, nearestOpponent, pressureDistanceMeters))
            return;

        if (TryContinueDirectAttack(ownerId, pressureDistanceMeters))
            return;
        if (TryResolveFinalThirdAction(ownerId, pressureDistanceMeters))
            return;

        FootballPlayer? owner = GetPlayer(ownerId);
        bool underPressure = _duelDistanceRules.IsUnderPressure(pressureDistanceMeters);
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
        PassSelection pass = ChoosePassSelection(underPressure);
        float dribbleIntent = DecisionRoll(ownerId, nearestOpponent, _decisionSerial);
        int dribbling = owner?.dribbling ?? 50;
        if (_ballCarrierDecisionEvaluator.ShouldKeepCarrying(
                underPressure,
                dribbling,
                _carryOwnerId == ownerId ? _consecutiveCarries : 0,
                pressureDistanceMeters,
                pass,
                dribbleIntent))
        {
            StartDribble(ownerId, underPressure);
            return;
        }

        bool widePlayer = _playerRoles[ownerId] is "LB" or "RB" or "LW" or "RW";
        if (_attackProgress > 0.68f && widePlayer)
        {
            StringName receiver = ChooseCrossTarget(ownerId);
            if (receiver != new StringName())
            {
                StartPass(receiver, BallActionKind.Cross);
                return;
            }
        }

        StringName target = pass.ReceiverId;
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
        float threshold = underPressure ? 0.52f : role == "GK" ? 0.12f : 0.04f;
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
        Vector2 destination = _clearanceTargetPlanner.FindTarget(
            start,
            direction,
            _playerTeams[playerId],
            CurrentPositions,
            _playerTeams,
            _playerRoles);
        float flightDistance = FootballPitchDimensions.DistanceMeters(start, destination);
        float duration = Mathf.Clamp(flightDistance / 30f, 0.55f, 1.35f);
        Clearances++;
        ResetCarrySequence();
        StartBallAction(destination, duration, 0.075f, new StringName(), BallActionKind.Clearance);
        SetAction($"{PlayerName(playerId)} phá bóng lên khu vực có đồng đội tiếp ứng");
    }

    private void StartDribble(StringName ownerId, bool escapingPressure)
    {
        if (_carryOwnerId == ownerId)
        {
            _consecutiveCarries++;
        }
        else
        {
            _carryOwnerId = ownerId;
            _consecutiveCarries = 1;
        }
        FootballPlayer? player = GetPlayer(ownerId);
        float quality = ((player?.dribbling ?? 50) + (player?.pace ?? 50)) / 198f;
        _attackProgress = Mathf.Clamp(_attackProgress + Mathf.Lerp(0.035f, 0.075f, quality), 0.12f, 0.94f);
        _phaseLane = Mathf.Lerp(_phaseLane, CurrentPositions[ownerId].Y, 0.55f);
        Dribbles++;
        _nextDecisionTime = _visualTime + (escapingPressure ? 0.62f : 0.82f);
        SetAction(escapingPressure
            ? $"{PlayerName(ownerId)} thoát pressing"
            : $"{PlayerName(ownerId)} dẫn bóng lên phía trước");
    }

    private bool TryResolveTackle(StringName ownerId, StringName defenderId, float distanceMeters)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float tackleSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float controlSkill = ((owner?.dribbling ?? 50) + (owner?.pace ?? 50)) * 0.5f;
        float contactBonus = _duelDistanceRules.ContactBonus(distanceMeters) * 0.28f;
        float chance = Mathf.Clamp(0.22f + (tackleSkill - controlSkill) / 145f + contactBonus, 0.08f, 0.72f);
        float tackleRoll = DecisionRoll(ownerId, defenderId, _decisionSerial + 41);
        if (tackleRoll >= chance)
        {
            float foulChance = Mathf.Clamp(
                0.10f + (controlSkill - tackleSkill) / 210f + contactBonus * 0.30f,
                0.05f, 0.42f);
            if (DecisionRoll(defenderId, ownerId, _decisionSerial + 59) < foulChance)
            {
                ResolveLiveFoul(defenderId, ownerId, distanceMeters);
                return true;
            }
            return false;
        }

        _ballOwnerId = defenderId;
        ResetCarrySequence();
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

    private void ResolveLiveFoul(StringName offenderId, StringName victimId, float contactDistanceMeters)
    {
        if (Simulation is null)
        {
            return;
        }
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

        bool awardsPenalty = _penaltyAreaRule.IsInsideDefendingPenaltyArea(
            BallPosition,
            OwnGoalX(foulingTeamId));
        bool playsAdvantage = !awardsPenalty && _advantageRuleEvaluator.ShouldPlay(
            new AdvantageContext(
                _ballOwnerId == victimId,
                card,
                _attackProgress,
                contactDistanceMeters,
                DecisionRoll(victimId, offenderId, _decisionSerial + 97)));
        if (playsAdvantage)
        {
            FootballMatchEvent? advantageEvent = Simulation.RegisterLiveAdvantage(
                foulingTeamId,
                offenderId,
                victimId);
            if (advantageEvent is not null)
            {
                EmitSignal(SignalName.LiveMatchEvent, advantageEvent);
            }
            if (card == "yellow")
            {
                _pendingCardActions.Add(new PendingCardAction(foulingTeamId, offenderId, card));
            }
            FoulsCommitted++;
            GivePossessionTo(victimId, 0.34f);
            SetAction(card == "yellow"
                ? $"Lợi thế — {PlayerName(victimId)} tiếp tục bóng, trọng tài sẽ quay lại rút thẻ"
                : $"Lợi thế — {PlayerName(victimId)} vẫn kiểm soát được bóng");
            return;
        }

        FootballMatchEvent? foulEvent = Simulation.register_live_foul(
            foulingTeamId, offenderId, victimId, card);
        if (foulEvent is not null)
        {
            EmitSignal(SignalName.LiveMatchEvent, foulEvent);
        }
        FoulsCommitted++;
        bool isSentOff = Simulation.get_state(foulingTeamId)?.IsSentOff(offenderId) == true;
        if (isSentOff)
        {
            SyncLineups(false);
        }
        ScheduleRestart(
            awardsPenalty ? "penalty" : "free_kick",
            victimTeamId,
            awardsPenalty
                ? _penaltyRestartPlanner.CreatePlan(BallPosition, OwnGoalX(foulingTeamId)).PenaltySpot
                : BallPosition,
            allowsQuickRestart: !awardsPenalty && card == new StringName());
        SetAction(awardsPenalty
            ? $"PHẠT ĐỀN — {PlayerName(offenderId)} phạm lỗi trong vòng cấm"
            : isSentOff
            ? $"{PlayerName(offenderId)} bị truất quyền thi đấu"
            : card == "yellow"
                ? $"{PlayerName(offenderId)} nhận thẻ — chờ trọng tài cho thực hiện đá phạt"
                : $"{PlayerName(offenderId)} phạm lỗi — đội bạn chuẩn bị đưa bóng vào cuộc");
    }

    private StringName ChoosePassTarget(bool preferSafe = false) =>
        ChoosePassSelection(preferSafe).ReceiverId;

    private PassSelection ChoosePassSelection(bool preferSafe = false)
    {
        if (Simulation is null)
            return default;
        if (_ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return default;

        float direction = AttackDirection(_activeTeamId);
        Vector2 owner = CurrentPositions[_ballOwnerId];
        string ownerRole = _playerRoles[_ballOwnerId];
        float ownerAttackProgress = AttackProgress(_activeTeamId, owner);
        int ownerVision = GetPlayer(_ballOwnerId)?.vision ?? 50;
        PassSelection bestPass = default;
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == _ballOwnerId || _playerTeams[candidateId] != _activeTeamId || _playerRoles[candidateId] == "GK")
                continue;
            bool candidateIsOffside = IsCurrentlyOffside(candidateId);
            Vector2 candidate = CurrentPositions[candidateId];
            float distanceMeters = FootballPitchDimensions.DistanceMeters(owner, candidate);
            float forwardGainMeters = direction * (candidate.X - owner.X) *
                                      FootballPitchDimensions.LengthMeters;
            float laneRisk = PassingLaneRisk(owner, candidate, _activeTeamId);
            float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
                candidate,
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
            if (!_passOptionEvaluator.CanConsider(
                    ownerRole,
                    _playerRoles[candidateId],
                    ownerAttackProgress,
                    forwardGainMeters,
                    distanceMeters,
                    laneRisk,
                    preferSafe))
            {
                continue;
            }
            if (!_passOptionEvaluator.CanReceiverControl(
                    receiverSpaceMeters,
                    laneRisk,
                    forwardGainMeters,
                    distanceMeters))
            {
                continue;
            }
            float receivingPressure = SpaceEvaluator.OpponentPressure(
                candidate,
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
            float forwardWeight = preferSafe ? 1.35f : 2.7f;
            float score = forwardGainMeters / FootballPitchDimensions.LengthMeters * forwardWeight -
                          distanceMeters / FootballPitchDimensions.LengthMeters * 0.42f -
                          receivingPressure * (preferSafe ? 0.72f : 0.46f) -
                          laneRisk * (preferSafe ? 1.45f : 0.82f);
            score += _passOptionEvaluator.ScoreAdjustment(
                _playerRoles[candidateId],
                ownerAttackProgress,
                forwardGainMeters,
                distanceMeters);
            score += Mathf.Clamp((receiverSpaceMeters - 2f) / 8f, 0f, 1f) * 0.22f;
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
            bestPass = new PassSelection(
                candidateId,
                score,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters);
        }
        return bestPass;
    }

    private void ResetCarrySequence()
    {
        _carryOwnerId = new StringName();
        _consecutiveCarries = 0;
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
