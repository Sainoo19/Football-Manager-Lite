using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed partial class LiveMatchEngine
{
    private void DecideNextAction()
    {
        if (Simulation is null || Simulation.is_finished || _state.BallOwnerId == new StringName() || !CurrentPositions.ContainsKey(_state.BallOwnerId))
            return;
        _decisionSerial++;
        _decisionsSinceShot++;
        StringName ownerId = _state.BallOwnerId;
        if (_playerTeams[ownerId] != _state.ActiveTeamId)
        {
            SetTrackedPossession(_playerTeams[ownerId]);
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
        if (TryAdvanceGroundDuel(ownerId, nearestOpponent, pressureDistanceMeters))
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
        bool isCredibleThroughBall = target == _primaryRunnerId &&
                                     creativeSkill * 2 >= _configuration.MinimumThroughBallCreativeSkill &&
                                     pass.ForwardGainMeters >= 8f &&
                                     pass.DistanceMeters >= 18f &&
                                     !underPressure;
        BallActionKind kind = isCredibleThroughBall
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
        StartDribbleTouch(ownerId, escapingPressure);
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
        StringName card = stopsClearChance && cardRoll < _configuration.ClearChanceRedCardProbability
            ? "red"
            : cardRoll < Mathf.Clamp(
                _configuration.YellowCardBaseProbability +
                (68 - (offender?.tackling ?? 50)) / 200f,
                0.10f,
                0.30f)
                ? "yellow"
                : new StringName();

        bool awardsPenalty = _penaltyAreaRule.IsInsideDefendingPenaltyArea(
            BallPosition,
            OwnGoalX(foulingTeamId));
        bool playsAdvantage = !awardsPenalty && _advantageRuleEvaluator.ShouldPlay(
            new AdvantageContext(
                _state.BallOwnerId == victimId,
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
                LiveMatchEvent?.Invoke(advantageEvent);
            }
            if (card == "yellow")
            {
                _state.PendingCardActions.Add(new PendingCardAction(foulingTeamId, offenderId, card));
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
            LiveMatchEvent?.Invoke(foulEvent);
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

    private StringName ChoosePassTarget(bool preferSafe = false)
    {
        return ChoosePassSelection(preferSafe).ReceiverId ?? new StringName();
    }

    private PassSelection ChoosePassSelection(bool preferSafe = false)
    {
        if (Simulation is null)
            return default;
        if (_state.BallOwnerId == new StringName() || !CurrentPositions.ContainsKey(_state.BallOwnerId))
            return default;

        float direction = AttackDirection(_state.ActiveTeamId);
        Vector2 owner = CurrentPositions[_state.BallOwnerId];
        string ownerRole = _playerRoles[_state.BallOwnerId];
        float ownerAttackProgress = AttackProgress(_state.ActiveTeamId, owner);
        int ownerVision = GetPlayer(_state.BallOwnerId)?.vision ?? 50;
        PassSelection bestPass = default;
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == _state.BallOwnerId || _playerTeams[candidateId] != _state.ActiveTeamId || _playerRoles[candidateId] == "GK")
                continue;
            bool candidateIsOffside = IsCurrentlyOffside(candidateId);
            if (candidateIsOffside)
            {
                float offsideAvoidanceProbability = Mathf.Lerp(
                    _configuration.MinimumOffsideAvoidanceProbability,
                    _configuration.MaximumOffsideAvoidanceProbability,
                    ownerVision / 99f);
                if (DecisionRoll(_state.BallOwnerId, candidateId, _decisionSerial + 991) <
                    offsideAvoidanceProbability)
                {
                    continue;
                }
            }
            Vector2 candidate = CurrentPositions[candidateId];
            float distanceMeters = FootballPitchDimensions.DistanceMeters(owner, candidate);
            float forwardGainMeters = direction * (candidate.X - owner.X) *
                                      FootballPitchDimensions.LengthMeters;
            float laneRisk = PassingLaneRisk(owner, candidate, _state.ActiveTeamId);
            float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
                candidate,
                _state.ActiveTeamId,
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
                _state.ActiveTeamId,
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
                VarietyRoll(_state.BallOwnerId, candidateId, _decisionSerial + _phaseSerial * 97),
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
        _state.GroundDuel.Reset();
    }

    private bool IsCurrentlyOffside(StringName playerId)
    {
        return _offsideRule.IsOffside(
            playerId,
            _state.ActiveTeamId,
            BallPosition,
            AttackDirection(_state.ActiveTeamId),
            CurrentPositions,
            _playerTeams);
    }
}
