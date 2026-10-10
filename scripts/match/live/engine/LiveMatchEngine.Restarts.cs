using System.Linq;
using Godot;

public sealed partial class LiveMatchEngine
{
    private void CompleteLiveShot()
    {
        if (Simulation is null || _pendingShotOutcome == new StringName())
            return;
        _shotDiagnostics.Record(new LiveShotRecord(_simulationTimeSeconds,
            _pendingShotShooterId.ToString(), _actionSourceTeamId.ToString(),
            _ballActionFrom, _ballActionTo, _pendingShotKeeperStart, _pendingShotDistanceMeters,
            _pendingShotOutcome.ToString(), _pendingShotKeeperAttemptedContact,
            _pendingShotKeeperContactDistance, _pendingShotReboundVelocity,
            _pendingShotSituation.ToString(),
            float.IsFinite(_pendingShotPressureMeters) ? _pendingShotPressureMeters : null, _pendingShotFinishing,
            CurrentPositions[_pendingShotGoalkeeperId], _pendingShotPlayers));
        FootballMatchEvent? matchEvent = Simulation.register_live_shot(
            _actionSourceTeamId,
            _pendingShotShooterId,
            _pendingShotOutcome,
            _pendingShotGoalkeeperId,
            _pendingShotBlockerId);
        if (matchEvent is not null)
            LiveMatchEvent?.Invoke(matchEvent);

        string outcome = _pendingShotOutcome.ToString();
        if (outcome == "goal")
        {
            RecordGoal(_pendingShotDistanceMeters, _pendingShotSituation);
        }
        string resultText = outcome switch
        {
            "goal" => $"BÀN THẮNG — {PlayerName(_pendingShotShooterId)}",
            "saved" => $"{PlayerName(_pendingShotGoalkeeperId)} cản phá",
            "parried" or "parried_corner" => $"{PlayerName(_pendingShotGoalkeeperId)} đẩy bóng ra",
            "blocked" => $"{PlayerName(_pendingShotBlockerId)} chắn cú sút",
            "blocked_corner" => $"{PlayerName(_pendingShotBlockerId)} chắn bóng ra biên ngang",
            _ => $"{PlayerName(_pendingShotShooterId)} sút chệch khung thành"
        };
        SetAction(resultText);

        StringName defendingTeamId = _actionSourceTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        if (outcome == "goal")
            ScheduleRestart("kickoff", defendingTeamId, new Vector2(0.5f, 0.5f));
        else if (outcome == "off_target")
            ScheduleRestart("goal_kick", defendingTeamId, GoalKickPosition(defendingTeamId));
        else if (outcome is "parried_corner" or "blocked_corner")
            ScheduleRestart("corner", _actionSourceTeamId, BallPosition);
        else if (outcome is "parried" or "blocked")
            StartLooseBall(
                "Bóng bật ra — hai đội tranh bóng hai",
                _pendingShotReboundVelocity);
        else
            GivePossessionTo(_pendingShotGoalkeeperId, 0.75f);

        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
        _pendingShotDistanceMeters = 0f;
        _pendingShotSituation = new StringName();
    }

    private void GivePossessionTo(StringName playerId, float decisionDelay)
    {
        if (Simulation is null || playerId == new StringName() || !CurrentPositions.ContainsKey(playerId))
            return;
        ResetCarrySequence();
        if (_playerTeams[playerId] != _offsideExposure.AttackingTeamId)
        {
            // Deliberate play by an opponent ends any offside exposure.
            _offsideExposure.Clear();
        }
        _state.IsBallVisible = true;
        _state.BallOwnerId = playerId;
        _lastBallTouch.Record(playerId, _playerTeams[playerId]);
        _runtime.SetPhase(LiveMatchPhase.InPossession);
        _state.LooseBallVelocityMetersPerSecond = Vector2.Zero;
        SetTrackedPossession(_playerTeams[playerId]);
        _state.PossessionSequence.ObserveOwner(
            _playerTeams[playerId],
            playerId,
            CurrentPositions[playerId],
            _state.VisualTime);
        UpdatePossessionDiagnostics();
        _attackProgress = Mathf.Clamp(AttackProgress(_state.ActiveTeamId, CurrentPositions[playerId]), 0.10f, 0.72f);
        _phaseLane = CurrentPositions[playerId].Y;
        SelectPhasePlayers();
        _nextDecisionTime = _state.VisualTime + decisionDelay;
    }

    internal void ScheduleRestart(
        StringName restartType,
        StringName teamId,
        Vector2 position,
        bool allowsQuickRestart = false)
    {
        if (Simulation is null)
            return;
        if (_lineupSyncPending)
        {
            // Players leave and enter only at a stoppage, before a restart taker is chosen.
            _lineupSyncPending = false;
            SyncLineups(false);
        }
        ApplyPendingCardsAtStoppage();
        _offsideExposure.Clear();
        ResetCarrySequence();
        ClearDirectAttack();
        _state.PossessionSequence.Reset();
        SuspendTrackedPossession();
        if (restartType == "kickoff")
            ResetPlayersForKickoff(teamId);
        _state.BallOwnerId = new StringName();
        _state.IsLooseBallActive = false;
        _state.LooseBallVelocityMetersPerSecond = Vector2.Zero;
        _state.IsRestartPending = true;
        _runtime.SetPhase(LiveMatchPhase.Restart);
        _state.RestartType = restartType;
        _state.RestartTeamId = teamId;
        _state.ActiveTeamId = teamId;
        Simulation.set_live_possession(teamId);
        _state.RestartPosition = ClampToPitch(position);
        if (restartType == "corner")
        {
            _state.RestartPosition = _touchlineRestartPlanner.PlaceCorner(position);
        }
        _state.RestartScheduledTime = _state.VisualTime;
        _state.RestartTakerId = new StringName();
        bool waitsForBallPresentation = restartType == "goal_kick" ||
                                        restartType == "free_kick" ||
                                        restartType == "penalty";
        _state.IsRestartBallPlaced = !waitsForBallPresentation;
        _state.IsBallVisible = true;
        if (restartType == "free_kick")
        {
            _state.RestartTakerId = ChooseNearestPlayer(teamId, _state.RestartPosition, false);
            _freeKickRestartPlan = _freeKickRestartPlanner.CreatePlan(
                BallPosition,
                _state.RestartPosition,
                allowsQuickRestart,
                VarietyRoll(teamId, _state.RestartTakerId, _decisionSerial + Restarts * 617 + 29));
        }
        else if (restartType == "penalty")
        {
            StringName defendingTeamId = teamId == Simulation.home.team.id
                ? Simulation.away.team.id
                : Simulation.home.team.id;
            _penaltyRestartPlan = _penaltyRestartPlanner.CreatePlan(BallPosition, OwnGoalX(defendingTeamId));
            _state.RestartPosition = _penaltyRestartPlan.PenaltySpot;
            _state.RestartTakerId = ChoosePenaltyTaker(teamId);
        }
        else
        {
            _freeKickRestartPlan = default;
            _penaltyRestartPlan = default;
            _state.RestartTakerId = restartType == "goal_kick"
                ? ChooseGoalkeeper(teamId)
                : ChooseNearestPlayer(teamId, _state.RestartPosition, restartType == "corner");
        }
        if (!waitsForBallPresentation)
        {
            BallPosition = _state.RestartPosition;
        }
        float preparationDuration = _restartCoordinator.PreparationDuration(
            restartType,
            _freeKickRestartPlan);
        _state.RestartExecuteTime = _state.VisualTime + preparationDuration;
        FootballMatchEvent? restartEvent = Simulation.register_live_restart(teamId, restartType);
        if (restartEvent is not null)
            LiveMatchEvent?.Invoke(restartEvent);
        UpdateTeamPhases();
    }

    private void UpdateRestartBallPresentation()
    {
        if (!_state.IsRestartPending || _state.IsRestartBallPlaced)
        {
            return;
        }

        if (_state.RestartType == "free_kick")
        {
            UpdateFreeKickBallPresentation();
            return;
        }
        if (_state.RestartType == "penalty")
        {
            UpdatePenaltyBallPresentation();
            return;
        }
        if (_state.RestartType != "goal_kick")
        {
            return;
        }

        GoalKickBallPresentation presentation = _goalKickRestartPlanner.BallPresentation(
            _state.VisualTime - _state.RestartScheduledTime);
        if (presentation == GoalKickBallPresentation.OutOfPlayVisible)
        {
            _state.IsBallVisible = true;
            return;
        }
        if (presentation == GoalKickBallPresentation.BeingRetrieved)
        {
            _state.IsBallVisible = false;
            return;
        }

        _state.IsRestartBallPlaced = true;
        _state.IsBallVisible = true;
        BallPosition = _state.RestartPosition;
        StringName goalkeeperId = ChooseGoalkeeper(_state.RestartTeamId);
        SetAction($"{PlayerName(goalkeeperId)} nhận bóng mới và đặt xuống chuẩn bị phát bóng");
    }

    private void UpdateFreeKickBallPresentation()
    {
        float elapsedSeconds = _state.VisualTime - _state.RestartScheduledTime;
        BallPosition = _freeKickRestartPlan.BallPositionAt(elapsedSeconds);
        _state.IsBallVisible = true;
        if (!_freeKickRestartPlan.IsBallPlaced(elapsedSeconds))
        {
            return;
        }

        _state.IsRestartBallPlaced = true;
        BallPosition = _state.RestartPosition;
        SetAction(_freeKickRestartPlan.IsQuick
            ? $"{PlayerName(_state.RestartTakerId)} đã đặt bóng và chuẩn bị đá phạt nhanh"
            : $"{PlayerName(_state.RestartTakerId)} đặt bóng đúng vị trí, chờ hiệu lệnh thực hiện");
    }

    private void UpdatePenaltyBallPresentation()
    {
        float elapsedSeconds = _state.VisualTime - _state.RestartScheduledTime;
        BallPosition = _penaltyRestartPlan.BallPositionAt(elapsedSeconds);
        _state.IsBallVisible = true;
        if (!_penaltyRestartPlan.IsBallPlaced(elapsedSeconds))
        {
            return;
        }

        _state.IsRestartBallPlaced = true;
        BallPosition = _state.RestartPosition;
        SetAction($"{PlayerName(_state.RestartTakerId)} đặt bóng lên chấm phạt đền, hai đội chờ hiệu lệnh");
    }

    private void ExecuteRestart()
    {
        if (Simulation is null || !IsRestartReady())
            return;
        _state.IsRestartPending = false;
        _state.IsRestartBallPlaced = true;
        _state.IsBallVisible = true;
        Restarts++;
        RecordRestartTaken(_state.RestartType);
        SetTrackedPossession(_state.RestartTeamId);
        UpdateTeamPhases();

        string type = _state.RestartType.ToString();
        if (type == "goal_kick")
        {
            ExecuteGoalKick();
            return;
        }

        if (type == "free_kick")
        {
            ExecuteFreeKick();
            return;
        }

        if (type == "penalty")
        {
            ExecutePenaltyKick();
            return;
        }

        if (type == "corner")
        {
            _attackProgress = 0.91f;
            _phaseLane = _state.RestartPosition.Y;
            SelectPhasePlayers();
            StringName taker = _state.RestartTakerId;
            _state.BallOwnerId = taker;
            BallPosition = _state.RestartPosition;
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChooseOwner(_state.RestartTeamId, true);
            float attackingGoalX = AttackingGoalX(_state.RestartTeamId);
            float crossTargetX = attackingGoalX < 0.5f ? 0.13f : 0.87f;
            StartBallAction(new Vector2(crossTargetX, 0.5f), 0.68f, 0.06f, receiver, BallActionKind.Cross);
            // No offside from a corner kick.
            _offsideExposure.Clear();
            _releaseExemptFromOffside = true;
            SetAction($"{PlayerName(taker)} thực hiện phạt góc");
            return;
        }

        if (type == "throw_in")
        {
            ExecuteThrowIn();
            return;
        }

        StringName ownerId = ChooseNearestPlayer(_state.RestartTeamId, _state.RestartPosition, false);
        BallPosition = _state.RestartPosition;
        GivePossessionTo(ownerId, 0.38f);
        _attackProgress = type == "kickoff" ? 0.20f : _attackProgress;
        SetAction(type switch
        {
            "throw_in" => $"{PlayerName(ownerId)} chuẩn bị ném biên",
            _ => $"{PlayerName(ownerId)} giao bóng"
        });
    }

    private void ApplyGoalKickRestartTargets()
    {
        float kickingGoalX = OwnGoalX(_state.RestartTeamId);
        _playerIntents.Clear();
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            bool isKickingTeam = _playerTeams[playerId] == _state.RestartTeamId;
            Vector2 target = _goalKickRestartPlanner.PositionTarget(
                BasePositions[playerId],
                _playerRoles[playerId],
                isKickingTeam,
                kickingGoalX,
                _state.RestartPosition);
            TargetPositions[playerId] = target;
            _playerIntents[playerId] = new PlayerIntent(
                PlayerIntentKind.RepositionForRestart,
                target,
                LiveTeamPhase.SetPiece);
        }
    }

    private void ApplyFreeKickRestartTargets()
    {
        if (Simulation is null)
        {
            return;
        }

        _playerIntents.Clear();
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            bool isRestartingTeam = _playerTeams[playerId] == _state.RestartTeamId;
            Vector2 target = CurrentPositions[playerId];
            if (playerId == _state.RestartTakerId)
            {
                target = _state.RestartPosition;
            }
            else if (!isRestartingTeam)
            {
                target = _freeKickRestartPlanner.EnsureRequiredDefenderDistance(
                    target,
                    _state.RestartPosition,
                    _freeKickRestartPlan.IsQuick);
            }

            TargetPositions[playerId] = target;
            _playerIntents[playerId] = new PlayerIntent(
                PlayerIntentKind.RepositionForRestart,
                target,
                LiveTeamPhase.SetPiece);
        }
    }

    private void ApplyPenaltyRestartTargets()
    {
        if (Simulation is null)
        {
            return;
        }

        StringName defendingTeamId = _state.RestartTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        float defendingGoalX = OwnGoalX(defendingTeamId);
        _playerIntents.Clear();
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            Vector2 target;
            if (playerId == _state.RestartTakerId)
            {
                target = _penaltyRestartPlanner.PositionTaker(_state.RestartPosition, defendingGoalX);
            }
            else if (playerId == goalkeeperId)
            {
                target = _penaltyRestartPlanner.PositionGoalkeeper(defendingGoalX);
            }
            else
            {
                target = _penaltyRestartPlanner.EnsureOutsidePenaltyAreaAndArc(
                    CurrentPositions[playerId],
                    _state.RestartPosition,
                    defendingGoalX);
            }

            TargetPositions[playerId] = target;
            _playerIntents[playerId] = new PlayerIntent(
                PlayerIntentKind.RepositionForRestart,
                target,
                LiveTeamPhase.SetPiece);
        }
    }

    private void ExecutePenaltyKick()
    {
        if (Simulation is null)
        {
            return;
        }

        StringName defendingTeamId = _state.RestartTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        StringName takerId = _state.RestartTakerId != new StringName() && CurrentPositions.ContainsKey(_state.RestartTakerId)
            ? _state.RestartTakerId
            : ChoosePenaltyTaker(_state.RestartTeamId);
        float goalX = AttackingGoalX(_state.RestartTeamId);
        BallPosition = _state.RestartPosition;
        _state.BallOwnerId = takerId;
        FootballPlayer? taker = GetPlayer(takerId);
        FootballPlayer? goalkeeper = GetPlayer(goalkeeperId);
        PenaltyKickOutcome outcome = _penaltyKickResolver.Resolve(
            taker?.finishing ?? 50,
            taker?.Composure ?? 50,
            taker?.form ?? 50,
            goalkeeper?.goalkeeping ?? 55,
            goalkeeper?.form ?? 50,
            DecisionRoll(takerId, goalkeeperId, _decisionSerial + 701),
            DecisionRoll(takerId, goalkeeperId, _decisionSerial + 719));
        Vector2 goalTarget = _shotTargetPlanner.ChooseGoalTarget(
            goalX,
            CurrentPositions[goalkeeperId],
            DecisionRoll(takerId, goalkeeperId, _decisionSerial + 733));
        bool onTarget = outcome != PenaltyKickOutcome.OffTarget;
        Vector2 destination = onTarget
            ? goalTarget
            : _shotTargetPlanner.ChooseOffTargetDestination(
                goalX, goalTarget.Y, taker?.finishing ?? 50,
                FootballPitchDimensions.PenaltySpotDistanceMeters, 0.98f);
        BeginShotFlight(takerId, goalkeeperId, destination, onTarget,
            taker?.finishing ?? 50, float.PositiveInfinity, 0f, "penalty");
        SetAction($"{PlayerName(takerId)} thực hiện quả phạt đền");
    }

    private StringName ChoosePenaltyTaker(StringName teamId)
    {
        return CurrentPositions.Keys
            .Where(id => _playerTeams[id] == teamId && _playerRoles[id] != "GK")
            .OrderByDescending(id =>
                (GetPlayer(id)?.finishing ?? 50) * 0.65f +
                (GetPlayer(id)?.Composure ?? 50) * 0.35f)
            .FirstOrDefault() ?? new StringName();
    }

    private void ApplyPendingCardsAtStoppage()
    {
        if (Simulation is null || _state.PendingCardActions.Count == 0)
        {
            return;
        }

        bool lineupChanged = false;
        foreach (PendingCardAction pendingCard in _state.PendingCardActions)
        {
            FootballMatchEvent? cardEvent = Simulation.RegisterLiveDelayedCard(
                pendingCard.TeamId,
                pendingCard.OffenderId,
                pendingCard.Card);
            if (cardEvent is not null)
            {
                LiveMatchEvent?.Invoke(cardEvent);
            }
            lineupChanged |= Simulation.get_state(pendingCard.TeamId)?.IsSentOff(pendingCard.OffenderId) == true;
        }
        _state.PendingCardActions.Clear();
        if (lineupChanged)
        {
            SyncLineups(false);
        }
    }

    private void ExecuteFreeKick()
    {
        if (Simulation is null)
        {
            return;
        }

        StringName takerId = _state.RestartTakerId != new StringName() && CurrentPositions.ContainsKey(_state.RestartTakerId)
            ? _state.RestartTakerId
            : ChooseNearestPlayer(_state.RestartTeamId, _state.RestartPosition, false);
        BallPosition = _state.RestartPosition;
        _state.BallOwnerId = takerId;
        SetTrackedPossession(_state.RestartTeamId);
        _attackProgress = AttackProgress(_state.ActiveTeamId, _state.RestartPosition);
        _phaseLane = _state.RestartPosition.Y;
        SelectPhasePlayers();

        StringName receiverId = ChoosePassTarget(true);
        if (receiverId == new StringName())
        {
            receiverId = ChooseOwner(_state.RestartTeamId, false);
        }
        if (receiverId == new StringName() || receiverId == takerId)
        {
            GivePossessionTo(takerId, 0.35f);
            SetAction($"{PlayerName(takerId)} đứng trước bóng chờ phương án đá phạt");
            return;
        }

        bool wasQuick = _freeKickRestartPlan.IsQuick;
        StartPass(receiverId, BallActionKind.Pass);
        SetAction(wasQuick
            ? $"{PlayerName(takerId)} đá phạt nhanh cho {PlayerName(receiverId)}"
            : $"{PlayerName(takerId)} thực hiện đá phạt cho {PlayerName(receiverId)}");
    }

    private void ExecuteGoalKick()
    {
        if (Simulation is null)
        {
            return;
        }

        float kickingGoalX = OwnGoalX(_state.RestartTeamId);
        StringName goalkeeperId = ChooseGoalkeeper(_state.RestartTeamId);
        BallPosition = _state.RestartPosition;
        _state.BallOwnerId = goalkeeperId;
        _attackProgress = 0.08f;
        _phaseLane = 0.5f;
        SelectPhasePlayers();

        StringName shortTarget = ChooseGoalkeeperDistributionTarget(goalkeeperId);
        bool playsShort = shortTarget != new StringName() &&
                          VarietyRoll(goalkeeperId, _state.RestartTeamId, _decisionSerial + Restarts * 401) < 0.62f;
        if (playsShort)
        {
            StartPass(shortTarget, BallActionKind.Pass);
            // No offside from a goal kick.
            _offsideExposure.Clear();
            _pendingOffsideReceiverId = new StringName();
            _releaseExemptFromOffside = true;
            SetAction($"{PlayerName(goalkeeperId)} phát bóng ngắn cho {PlayerName(shortTarget)}");
            return;
        }

        StartClearance(goalkeeperId);
        _offsideExposure.Clear();
        _releaseExemptFromOffside = true;
        SetAction($"{PlayerName(goalkeeperId)} phát bóng dài lên phía trên");
    }

    private bool TryStartKickoffPass(StringName ownerId)
    {
        if (!_kickoffPassPending ||
            _kickoffReceiverId == new StringName() ||
            !CurrentPositions.ContainsKey(_kickoffReceiverId) ||
            _playerTeams[ownerId] != _state.ActiveTeamId ||
            _playerTeams[_kickoffReceiverId] != _state.ActiveTeamId)
        {
            return false;
        }

        StringName receiverId = _kickoffReceiverId;
        _kickoffPassPending = false;
        _kickoffReceiverId = new StringName();
        UpdateTeamPhases();
        StartPass(receiverId, BallActionKind.Pass);
        SetAction($"{PlayerName(ownerId)} chuyền bóng về cho {PlayerName(receiverId)} để bắt đầu trận đấu");
        return true;
    }

    private StringName ChooseNearestPlayer(StringName teamId, Vector2 position, bool preferWide)
    {
        var candidates = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == teamId && _playerRoles[id] != "GK")
            .OrderBy(id => FootballPitchDimensions.DistanceMeters(CurrentPositions[id], position) -
                           (preferWide && _playerRoles[id] is "LB" or "RB" or "LW" or "RW" ? 2f : 0))
            .ToList();
        return candidates.Count > 0 ? candidates[0] : ChooseOwner(teamId, false);
    }

    private void BeginSecondHalf()
    {
        if (Simulation is null || _sideController.AreSidesSwitched)
            return;
        _sideController.SwitchEnds();
        SyncLineups(true);
        ScheduleRestart("kickoff", Simulation.away.team.id, new Vector2(0.5f, 0.5f));
        SetAction("Hết hiệp một — hai đội đổi sân, đội khách giao bóng");
    }

    private void ResetPlayersForKickoff(StringName kickingTeamId)
    {
        if (Simulation is null)
            return;
        SyncLineups(true);
        _movementController.Reset();
        _ballActionActive = false;
        _aerialFlightActive = false;
        _aerialTrajectory = default;
        _aerialContenderIds.Clear();
        _ballActionKind = BallActionKind.None;
        _pendingOffsideReceiverId = new StringName();
        _ballVisualHeight = 0f;
        _ballVerticalVelocityMetersPerSecond = 0f;
        _ballNextOwnerId = new StringName();
        _actionSourceId = new StringName();
        _actionSourceTeamId = new StringName();
        _state.IsLooseBallActive = false;
        _state.LooseBallVelocityMetersPerSecond = Vector2.Zero;
        ResetCarrySequence();
        _state.IsBallVisible = true;
        _state.IsRestartBallPlaced = true;
        BallPosition = new Vector2(0.5f, 0.5f);

        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            Vector2 basePosition = BasePositions[playerId];
            bool ownsLeftHalf = OwnGoalX(_playerTeams[playerId]) < 0.5f;
            float kickoffX = ownsLeftHalf
                ? basePosition.X * 0.5f
                : 0.5f + basePosition.X * 0.5f;
            Vector2 kickoffPosition = new(kickoffX, basePosition.Y);
            CurrentPositions[playerId] = kickoffPosition;
            TargetPositions[playerId] = kickoffPosition;
        }

        KickoffSetup setup = _kickoffRestartPlanner.Plan(
            kickingTeamId,
            AttackDirection(kickingTeamId),
            CurrentPositions,
            _playerTeams,
            _playerRoles);
        if (setup.IsValid)
        {
            CurrentPositions[setup.TakerId] = setup.TakerPosition;
            TargetPositions[setup.TakerId] = setup.TakerPosition;
            CurrentPositions[setup.ReceiverId] = setup.ReceiverPosition;
            TargetPositions[setup.ReceiverId] = setup.ReceiverPosition;
        }

        _state.BallOwnerId = setup.TakerId;
        _lastBallTouch.Reset();
        _lastBallTouch.Record(setup.TakerId, kickingTeamId);
        _kickoffReceiverId = setup.ReceiverId;
        _kickoffPassPending = setup.IsValid;
        SetTrackedPossession(kickingTeamId);
        _attackProgress = 0.5f;
        _phaseLane = 0.5f;
        _nextIntentPlanTime = 0f;
    }

    private float AttackDirection(StringName teamId)
    {
        return Simulation is null ? 1f : _sideController.AttackDirection(teamId, Simulation.home.team.id);
    }

    private float OwnGoalX(StringName teamId)
    {
        return Simulation is null ? 0.015f : _sideController.OwnGoalX(teamId, Simulation.home.team.id);
    }

    private float AttackingGoalX(StringName teamId)
    {
        return Simulation is null ? 0.994f : _sideController.AttackingGoalX(teamId, Simulation.home.team.id);
    }

    private float AttackProgress(StringName teamId, Vector2 position)
    {
        return AttackDirection(teamId) > 0f ? position.X : 1f - position.X;
    }

    private Vector2 GoalKickPosition(StringName teamId)
    {
        return new Vector2(OwnGoalX(teamId) < 0.5f ? 0.055f : 0.945f, 0.5f);
    }
}
