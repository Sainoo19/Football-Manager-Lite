using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed partial class LiveMatchEngine
{
    private void DecideNextAction()
    {
        if (Simulation is null ||
            Simulation.is_finished ||
            _state.BallOwnerId == new StringName() ||
            !CurrentPositions.ContainsKey(_state.BallOwnerId))
        {
            return;
        }

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
        bool isUnderPressure = _duelDistanceRules.IsUnderPressure(pressureDistanceMeters);
        PossessionSequenceState possession = _state.PossessionSequence;
        possession.ObserveOwner(
            _playerTeams[ownerId],
            ownerId,
            CurrentPositions[ownerId],
            _state.VisualTime);
        possession.RecordOwnerDecision();
        if (isUnderPressure && nearestOpponent != new StringName())
        {
            possession.ObserveDuel(nearestOpponent);
        }
        else if (pressureDistanceMeters > DuelDistanceRules.EngagementExitDistanceMeters)
        {
            possession.ClearDuel();
        }
        UpdatePossessionDiagnostics();

        FootballActionContext context = CreateFootballActionContext(
            ownerId,
            pressureDistanceMeters,
            isUnderPressure,
            possession);
        FootballActionDecision decision = _footballActionCoordinator.Decide(context);
        LastActionDecision = decision;
        ExecuteFootballAction(decision, nearestOpponent, pressureDistanceMeters);
        _previousActionType = decision.Selected.ActionType;
        _previousActionTargetId = decision.Selected.TargetPlayerId;
    }

    private FootballActionContext CreateFootballActionContext(
        StringName ownerId,
        float pressureDistanceMeters,
        bool isUnderPressure,
        PossessionSequenceState possession)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        List<PassSelection> selections = _playerRoles[ownerId] == "GK"
            ? new List<PassSelection>()
            : GeneratePassSelections(false);
        foreach (PassSelection safeSelection in _playerRoles[ownerId] == "GK"
                     ? new List<PassSelection>()
                     : GeneratePassSelections(true))
        {
            int existingIndex = selections.FindIndex(selection =>
                selection.ReceiverId == safeSelection.ReceiverId);
            if (existingIndex < 0)
            {
                selections.Add(safeSelection);
            }
            else if (safeSelection.Score > selections[existingIndex].Score)
            {
                selections[existingIndex] = safeSelection;
            }
        }

        List<FootballPassOption> passOptions = new();
        foreach (PassSelection selection in selections)
        {
            passOptions.Add(new FootballPassOption(
                selection,
                PredictedReceptionPoint(selection.ReceiverId),
                SuggestedPassAction(ownerId, selection, isUnderPressure)));
        }
        FootballPassOption backPass = CreateGoalkeeperBackPassOption(ownerId);
        if (backPass.HasTarget &&
            passOptions.TrueForAll(option =>
                option.Selection.ReceiverId != backPass.Selection.ReceiverId))
        {
            passOptions.Add(backPass);
        }

        Vector2 ownerPosition = CurrentPositions[ownerId];
        Vector2 attackingGoal = new(AttackingGoalX(_playerTeams[ownerId]), 0.5f);
        bool stalledDuel = isUnderPressure &&
                           (possession.DuelPairSeconds >=
                            _configuration.ActionSelection.MaximumStalledDuelSeconds ||
                            possession.DuelPairDecisionCount >=
                            _configuration.ActionSelection.MaximumStalledDuelDecisions);
        return new FootballActionContext(
            ownerId,
            _playerTeams[ownerId],
            _playerRoles[ownerId],
            ownerPosition,
            attackingGoal,
            _teamPhaseCoordinator.PhaseFor(_playerTeams[ownerId]),
            _attackProgress,
            isUnderPressure,
            pressureDistanceMeters,
            ownerId == _directAttackOwnerId && _directAttackActionsRemaining > 0,
            stalledDuel,
            possession.OwnerHeldSeconds,
            possession.OwnerCarriedDistanceMeters,
            possession.OwnerDecisionCount,
            owner?.passing ?? 50,
            owner?.vision ?? 50,
            owner?.Composure ?? 50,
            owner?.dribbling ?? 50,
            owner?.finishing ?? 50,
            CalculateForwardSpaceMeters(ownerId),
            CalculateDefensiveDanger(ownerId, isUnderPressure),
            CalculateShotValue(ownerId, pressureDistanceMeters),
            passOptions,
            CreateCrossOption(ownerId),
            CreateGoalkeeperDistributionOption(ownerId),
            _previousActionType,
            _previousActionTargetId,
            _liveDecisionSeed,
            _decisionSerial);
    }

    private FootballActionType SuggestedPassAction(
        StringName ownerId,
        PassSelection selection,
        bool isUnderPressure)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        int creativeSkill = (owner?.passing ?? 50) + (owner?.vision ?? 50);
        if (!isUnderPressure &&
            selection.ReceiverId == _primaryRunnerId &&
            creativeSkill >= _configuration.MinimumThroughBallCreativeSkill &&
            selection.ForwardGainMeters >= 8f &&
            selection.DistanceMeters >= 18f)
        {
            return FootballActionType.ThroughBall;
        }
        if (selection.DistanceMeters >= _configuration.MinimumLoftedPassDistanceMeters &&
            creativeSkill >= 126)
        {
            return FootballActionType.LoftedPass;
        }
        return FootballActionType.GroundPass;
    }

    private FootballPassOption CreateGoalkeeperBackPassOption(StringName ownerId)
    {
        if (_playerRoles[ownerId] == "GK" || _playerRoles[ownerId] is not ("CB" or "LB" or "RB" or "DM"))
        {
            return default;
        }

        StringName goalkeeperId = ChooseGoalkeeper(_playerTeams[ownerId]);
        if (goalkeeperId == new StringName() || goalkeeperId == ownerId)
        {
            return default;
        }
        Vector2 target = CurrentPositions[goalkeeperId];
        float distanceMeters = FootballPitchDimensions.DistanceMeters(CurrentPositions[ownerId], target);
        float laneRisk = PassingLaneRisk(CurrentPositions[ownerId], target, _playerTeams[ownerId]);
        if (_attackProgress > 0.38f || distanceMeters is < 4f or > 32f || laneRisk > 0.72f)
        {
            return default;
        }

        float direction = AttackDirection(_playerTeams[ownerId]);
        float forwardGainMeters = direction * (target.X - CurrentPositions[ownerId].X) *
                                  FootballPitchDimensions.LengthMeters;
        float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
            target,
            _playerTeams[ownerId],
            CurrentPositions,
            _playerTeams);
        return new FootballPassOption(
            new PassSelection(
                goalkeeperId,
                0f,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters),
            target,
            FootballActionType.GroundPass);
    }

    private FootballPassOption CreateGoalkeeperDistributionOption(StringName ownerId)
    {
        if (_playerRoles[ownerId] != "GK")
        {
            return default;
        }
        StringName targetId = ChooseGoalkeeperDistributionTarget(ownerId);
        if (targetId == new StringName())
        {
            return default;
        }

        Vector2 target = PredictedReceptionPoint(targetId);
        float direction = AttackDirection(_playerTeams[ownerId]);
        float distanceMeters = FootballPitchDimensions.DistanceMeters(CurrentPositions[ownerId], target);
        float forwardGainMeters = direction * (target.X - CurrentPositions[ownerId].X) *
                                  FootballPitchDimensions.LengthMeters;
        float laneRisk = PassingLaneRisk(CurrentPositions[ownerId], target, _playerTeams[ownerId]);
        float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
            target,
            _playerTeams[ownerId],
            CurrentPositions,
            _playerTeams);
        return new FootballPassOption(
            new PassSelection(
                targetId,
                0f,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters),
            target,
            FootballActionType.GoalkeeperDistribution);
    }

    private FootballPassOption CreateCrossOption(StringName ownerId)
    {
        bool isWidePlayer = _playerRoles[ownerId] is "LB" or "RB" or "LW" or "RW";
        if (!isWidePlayer || _attackProgress < 0.56f)
        {
            return default;
        }
        StringName targetId = ChooseCrossTarget(ownerId);
        if (targetId == new StringName())
        {
            return default;
        }

        Vector2 target = PredictedReceptionPoint(targetId);
        Vector2 owner = CurrentPositions[ownerId];
        float direction = AttackDirection(_playerTeams[ownerId]);
        float forwardGainMeters = direction * (target.X - owner.X) *
                                  FootballPitchDimensions.LengthMeters;
        float distanceMeters = FootballPitchDimensions.DistanceMeters(owner, target);
        float laneRisk = PassingLaneRisk(owner, target, _playerTeams[ownerId]);
        float receiverSpaceMeters = SpaceEvaluator.NearestOpponentDistanceMeters(
            target,
            _playerTeams[ownerId],
            CurrentPositions,
            _playerTeams);
        return new FootballPassOption(
            new PassSelection(
                targetId,
                0f,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters),
            target,
            FootballActionType.Cross);
    }

    private float CalculateForwardSpaceMeters(StringName ownerId)
    {
        Vector2 owner = CurrentPositions[ownerId];
        float direction = AttackDirection(_playerTeams[ownerId]);
        Vector2 probeMeters = FootballPitchDimensions.ToMeters(owner) + new Vector2(direction * 8f, 0f);
        Vector2 probe = SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(probeMeters));
        return SpaceEvaluator.NearestOpponentDistanceMeters(
            probe,
            _playerTeams[ownerId],
            CurrentPositions,
            _playerTeams);
    }

    private float CalculateDefensiveDanger(StringName ownerId, bool isUnderPressure)
    {
        if (_playerRoles[ownerId] is not ("GK" or "CB" or "LB" or "RB" or "DM"))
        {
            return 0f;
        }
        float territoryDanger = Mathf.Clamp((0.46f - _attackProgress) / 0.46f, 0f, 1f);
        return Mathf.Clamp(territoryDanger + (isUnderPressure ? 0.24f : 0f), 0f, 1f);
    }

    private float CalculateShotValue(StringName ownerId, float pressureDistanceMeters)
    {
        if (Simulation?.use_live_pitch_events != true || _playerRoles[ownerId] == "GK")
        {
            return 0f;
        }

        Vector2 position = CurrentPositions[ownerId];
        Vector2 goal = new(AttackingGoalX(_playerTeams[ownerId]), 0.5f);
        float distanceMeters = FootballPitchDimensions.DistanceMeters(position, goal);
        float distanceQuality = Mathf.Clamp((38f - distanceMeters) / 31f, 0f, 1f);
        float angleQuality = 1f - Mathf.Clamp(
            Mathf.Abs(position.Y - 0.5f) * FootballPitchDimensions.WidthMeters / 30f,
            0f,
            0.78f);
        float pressureQuality = float.IsFinite(pressureDistanceMeters)
            ? Mathf.Clamp(pressureDistanceMeters / 4f, 0.28f, 1f)
            : 1f;
        float finishingQuality = (GetPlayer(ownerId)?.finishing ?? 50) / 99f;
        return Mathf.Clamp(
            distanceQuality * distanceQuality * 0.62f *
            angleQuality *
            Mathf.Lerp(0.72f, 1.15f, finishingQuality) *
            pressureQuality,
            0f,
            0.82f);
    }

    private void ExecuteFootballAction(
        FootballActionDecision decision,
        StringName nearestOpponent,
        float pressureDistanceMeters)
    {
        FootballActionCandidate selected = decision.Selected;
        StringName ownerId = selected.ActorId;
        switch (selected.ActionType)
        {
            case FootballActionType.Hold:
                HoldBall(ownerId);
                break;
            case FootballActionType.Carry:
            case FootballActionType.ProtectBall:
                if (selected.ActionType == FootballActionType.ProtectBall &&
                    _state.PossessionSequence.DuelPairDecisionCount >=
                    _configuration.ActionSelection.MaximumStalledDuelDecisions)
                {
                    ResolveStalledPossessionContest(ownerId, nearestOpponent);
                    break;
                }
                if (!TryAdvanceGroundDuel(ownerId, nearestOpponent, pressureDistanceMeters))
                {
                    StartDribble(ownerId, selected.ActionType == FootballActionType.ProtectBall);
                }
                break;
            case FootballActionType.GroundPass:
                StartPass(selected.TargetPlayerId, BallActionKind.Pass);
                break;
            case FootballActionType.ThroughBall:
                StartPass(selected.TargetPlayerId, BallActionKind.ThroughBall);
                break;
            case FootballActionType.LoftedPass:
                StartPass(selected.TargetPlayerId, BallActionKind.LoftedPass);
                break;
            case FootballActionType.Cross:
                StartPass(selected.TargetPlayerId, BallActionKind.Cross);
                break;
            case FootballActionType.Shot:
                StartLiveShot(ownerId, pressureDistanceMeters);
                break;
            case FootballActionType.Clearance:
                StartClearance(ownerId);
                break;
            case FootballActionType.GoalkeeperDistribution:
                StartPass(selected.TargetPlayerId, BallActionKind.Pass);
                break;
            default:
                HoldBall(ownerId);
                break;
        }
        UpdateDirectAttackAfterDecision(ownerId, selected.ActionType);
    }

    private void HoldBall(StringName ownerId)
    {
        TargetPositions[ownerId] = CurrentPositions[ownerId];
        _nextDecisionTime = _state.VisualTime + 0.28f;
        SetAction($"{PlayerName(ownerId)} giữ bóng và quan sát phương án");
    }

    private void UpdateDirectAttackAfterDecision(StringName ownerId, FootballActionType actionType)
    {
        if (ownerId != _directAttackOwnerId || _directAttackActionsRemaining <= 0)
        {
            return;
        }
        _directAttackActionsRemaining--;
        if (actionType is not (FootballActionType.Carry or FootballActionType.ProtectBall) ||
            _directAttackActionsRemaining <= 0)
        {
            ClearDirectAttack();
        }
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
        return GeneratePassSelections(preferSafe)
            .OrderByDescending(selection => selection.Score)
            .ThenBy(selection => selection.ReceiverId.ToString(), System.StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private List<PassSelection> GeneratePassSelections(bool preferSafe)
    {
        if (Simulation is null)
            return new List<PassSelection>();
        if (_state.BallOwnerId == new StringName() || !CurrentPositions.ContainsKey(_state.BallOwnerId))
            return new List<PassSelection>();

        float direction = AttackDirection(_state.ActiveTeamId);
        Vector2 owner = CurrentPositions[_state.BallOwnerId];
        string ownerRole = _playerRoles[_state.BallOwnerId];
        float ownerAttackProgress = AttackProgress(_state.ActiveTeamId, owner);
        int ownerVision = GetPlayer(_state.BallOwnerId)?.vision ?? 50;
        List<PassSelection> selections = new();
        foreach (StringName candidateId in OrderedPlayerIds(CurrentPositions.Keys))
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
            Vector2 candidate = PredictedReceptionPoint(candidateId);
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
            selections.Add(new PassSelection(
                candidateId,
                score,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters));
        }
        return selections;
    }

    private Vector2 PredictedReceptionPoint(StringName playerId)
    {
        Vector2 current = CurrentPositions[playerId];
        if (!TargetPositions.TryGetValue(playerId, out Vector2 target) ||
            !_playerIntents.TryGetValue(playerId, out PlayerIntent? intent))
        {
            return current;
        }

        float maximumLeadMeters = intent.Kind switch
        {
            PlayerIntentKind.RunIntoSpace => 4.5f,
            PlayerIntentKind.ReceivePass => 4f,
            PlayerIntentKind.SupportBall => 2.5f,
            _ => 1.2f
        };
        Vector2 currentMeters = FootballPitchDimensions.ToMeters(current);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(target);
        return SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(
                currentMeters.MoveToward(targetMeters, maximumLeadMeters)));
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
