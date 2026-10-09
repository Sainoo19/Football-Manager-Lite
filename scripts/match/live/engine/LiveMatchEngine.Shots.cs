using Godot;

public sealed partial class LiveMatchEngine
{
    private readonly ShotContactResolver _shotContactResolver = new();
    private int _pendingShotFinishing;
    private float _pendingShotPressureMeters;
    private float _pendingShotAngle;
    private int _pendingShotDecisionSerial;
    private Vector2 _pendingShotReboundVelocity;
    private readonly LiveShotDiagnostics _shotDiagnostics = new();
    private Vector2 _pendingShotKeeperStart;
    private float _pendingShotKeeperReactionEnds;
    private bool _pendingShotKeeperAttemptedContact;
    private float _pendingShotKeeperContactDistance;
    private bool _pendingShotKeeperHasReacted;
    private System.Collections.Generic.IReadOnlyList<LiveShotPlayer>? _pendingShotPlayers;

    public System.Collections.Generic.IReadOnlyList<LiveShotRecord> ShotRecords => _shotDiagnostics.Records;

    private void StartLiveShot(StringName shooterId, float pressureDistanceMeters, bool isHeader = false)
    {
        if (Simulation is null)
        {
            return;
        }
        FootballPlayer? shooter = GetPlayer(shooterId);
        StringName goalkeeperId = ChooseGoalkeeper(OpposingTeam(_playerTeams[shooterId]));
        Vector2 position = CurrentPositions[shooterId];
        Vector2 goalTarget = _shotTargetPlanner.ChooseGoalTarget(
            AttackingGoalX(_playerTeams[shooterId]),
            CurrentPositions[goalkeeperId],
            DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 101));
        int finishing = isHeader
            ? Mathf.RoundToInt((shooter?.Heading ?? 50) * 0.68f + (shooter?.finishing ?? 50) * 0.32f)
            : shooter?.finishing ?? 50;
        float distance = FootballPitchDimensions.DistanceMeters(position, goalTarget);
        float angle = Mathf.Clamp(Mathf.Abs(position.Y - 0.5f) * 2f, 0f, 1f);
        float coverage = _shotTargetPlanner.GoalkeeperCoverage(position, goalTarget, CurrentPositions[goalkeeperId]);
        float accuracyRoll = DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 151);
        bool onTarget = _shotOutcomeResolver.IsOnTarget(
            finishing, distance, angle, pressureDistanceMeters, coverage, accuracyRoll);
        Vector2 destination = onTarget
            ? goalTarget
            : _shotTargetPlanner.ChooseOffTargetDestination(
                goalTarget.X, goalTarget.Y, finishing, distance, accuracyRoll);
        BeginShotFlight(shooterId, goalkeeperId, destination, onTarget, finishing,
            pressureDistanceMeters, angle, isHeader ? "header" : "open_play");
        SetAction(isHeader
            ? $"{PlayerName(shooterId)} bật cao đánh đầu dứt điểm"
            : $"{PlayerName(shooterId)} tung cú sút");
    }

    private void BeginShotFlight(
        StringName shooterId,
        StringName goalkeeperId,
        Vector2 destination,
        bool onTarget,
        int finishing,
        float pressureDistanceMeters,
        float angle,
        StringName situation)
    {
        ResetCarrySequence();
        _teamPhaseCoordinator.RecordShot(_playerTeams[shooterId]);
        _pendingShotDistanceMeters = FootballPitchDimensions.DistanceMeters(
            BallPosition, new Vector2(AttackingGoalX(_playerTeams[shooterId]), 0.5f));
        _pendingShotSituation = situation;
        _pendingShotShooterId = shooterId;
        _pendingShotGoalkeeperId = goalkeeperId;
        _pendingShotBlockerId = new StringName();
        _pendingShotFinishing = finishing;
        _pendingShotPressureMeters = pressureDistanceMeters;
        _pendingShotAngle = angle;
        _pendingShotDecisionSerial = _decisionSerial;
        _pendingShotKeeperStart = CurrentPositions[goalkeeperId];
        System.Collections.Generic.List<LiveShotPlayer> players = new(CurrentPositions.Count);
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            _playerIntents.TryGetValue(playerId, out PlayerIntent? intent);
            players.Add(new LiveShotPlayer(playerId.ToString(), _playerTeams[playerId].ToString(),
                _playerRoles[playerId], CurrentPositions[playerId], TargetPositions[playerId],
                intent?.Assignment.ToString() ?? "", intent?.RelatedPlayerId.ToString() ?? ""));
        }
        _pendingShotPlayers = players.AsReadOnly();
        _pendingShotKeeperReactionEnds = _state.VisualTime + GoalkeeperResponseRules.ReactionDelaySeconds(
            GetPlayer(goalkeeperId)?.goalkeeping ?? 55);
        _pendingShotKeeperAttemptedContact = false;
        _pendingShotKeeperHasReacted = false;
        _pendingShotKeeperContactDistance = 0f;
        _nextIntentPlanTime = 0f;
        _pendingShotReboundVelocity = Vector2.Zero;
        // An accurate shot scores only if it reaches the goal without a successful physical contact.
        _pendingShotOutcome = onTarget ? "goal" : "off_target";
        _decisionsSinceShot = 0;
        float distance = FootballPitchDimensions.DistanceMeters(BallPosition, destination);
        StartBallAction(destination, Mathf.Clamp(distance / 28f, 0.20f, 1.6f),
            0.012f, new StringName(), BallActionKind.Shot);
    }

    private bool TryResolveShotContact(Vector2 previousBallPosition)
    {
        // A failed challenge must not hide another contact later in the same swept segment.
        for (int attempt = 0; attempt < CurrentPositions.Count; attempt++)
        {
            StringName candidateId = new();
            Vector2 contactPosition = BallPosition;
            float earliestProgress = float.PositiveInfinity;
            foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
            {
                if (_playerTeams[playerId] == _actionSourceTeamId || _interceptionAttemptedBy.Contains(playerId))
                {
                    continue;
                }
                bool isGoalkeeper = playerId == _pendingShotGoalkeeperId;
                if (isGoalkeeper && _pendingShotOutcome == "off_target" ||
                    _ballVisualHeight > (isGoalkeeper ? 2.6f : 1.5f))
                {
                    continue;
                }
                float reach = isGoalkeeper
                    ? _state.VisualTime >= _pendingShotKeeperReactionEnds
                        ? ShotContactResolver.GoalkeeperReachMeters
                        : ShotContactResolver.OutfieldReachMeters
                    : ShotContactResolver.OutfieldReachMeters;
                if (_shotContactResolver.TryContact(previousBallPosition, BallPosition,
                        CurrentPositions[playerId], reach, out Vector2 contact, out float progress) &&
                    progress < earliestProgress)
                {
                    candidateId = playerId;
                    contactPosition = contact;
                    earliestProgress = progress;
                }
            }
            if (candidateId == new StringName())
            {
                return false;
            }
            _interceptionAttemptedBy.Add(candidateId);
            bool goalkeeperContact = candidateId == _pendingShotGoalkeeperId;
            if (goalkeeperContact)
            {
                _pendingShotKeeperAttemptedContact = true;
                _pendingShotKeeperContactDistance = FootballPitchDimensions.DistanceMeters(
                    contactPosition, CurrentPositions[candidateId]);
                ShotOutcome outcome = ResolveGoalkeeperShotContact(candidateId);
                if (outcome == ShotOutcome.Goal)
                {
                    continue;
                }
                bool holds = outcome == ShotOutcome.Saved && GoalkeeperResponseRules.CanHoldContact(
                    _shotContactResolver.LaneDistanceMeters(_ballActionFrom, _ballActionTo, CurrentPositions[candidateId]),
                    ShotVelocity().Length(), GetPlayer(candidateId)?.goalkeeping ?? 55);
                _pendingShotOutcome = holds ? "saved" : "parried";
            }
            else
            {
                FootballPlayer? blocker = GetPlayer(candidateId);
                float blockChance = Mathf.Clamp(0.48f + (blocker?.positioning ?? 50) / 250f, 0.48f, 0.88f);
                if (DecisionRoll(_pendingShotShooterId, candidateId, _pendingShotDecisionSerial + 131) >= blockChance)
                {
                    continue;
                }
                _pendingShotBlockerId = candidateId;
                _pendingShotOutcome = "blocked";
            }
            BallPosition = contactPosition;
            _lastBallTouch.Record(candidateId, _playerTeams[candidateId]);
            _pendingShotReboundVelocity = _pendingShotOutcome == "saved"
                ? Vector2.Zero
                : _shotContactResolver.ReboundVelocity(
                    ShotVelocity(), contactPosition, CurrentPositions[candidateId], goalkeeperContact);
            _ballActionActive = false;
            _ballActionKind = BallActionKind.None;
            _ballVisualHeight = 0f;
            CompleteLiveShot();
            return true;
        }
        return false;
    }

    private Vector2 ShotVelocity() =>
        (FootballPitchDimensions.ToMeters(_ballActionTo) - FootballPitchDimensions.ToMeters(_ballActionFrom)) /
        Mathf.Max(_ballActionDuration, 0.01f);

    private void ApplyShotGoalkeeperResponse()
    {
        if (!_ballActionActive || _ballActionKind != BallActionKind.Shot ||
            _state.VisualTime >= _pendingShotKeeperReactionEnds)
        {
            return;
        }
        TargetPositions[_pendingShotGoalkeeperId] = _pendingShotKeeperStart;
        _playerIntents[_pendingShotGoalkeeperId] = new PlayerIntent(
            PlayerIntentKind.Goalkeep, _pendingShotKeeperStart,
            _teamPhaseCoordinator.PhaseFor(_playerTeams[_pendingShotGoalkeeperId]));
    }

    private void RefreshShotGoalkeeperReaction()
    {
        if (_ballActionActive && _ballActionKind == BallActionKind.Shot &&
            !_pendingShotKeeperHasReacted && _state.VisualTime >= _pendingShotKeeperReactionEnds)
        {
            _pendingShotKeeperHasReacted = true;
            _nextIntentPlanTime = 0f;
        }
    }

    private ShotOutcome ResolveGoalkeeperShotContact(StringName goalkeeperId)
    {
        FootballPlayer? shooter = GetPlayer(_pendingShotShooterId);
        FootballPlayer? goalkeeper = GetPlayer(goalkeeperId);
        if (_pendingShotSituation == "penalty")
        {
            PenaltyKickOutcome penalty = _penaltyKickResolver.Resolve(
                _pendingShotFinishing, shooter?.Composure ?? 50, shooter?.form ?? 50,
                goalkeeper?.goalkeeping ?? 55, goalkeeper?.form ?? 50, 0f,
                DecisionRoll(_pendingShotShooterId, goalkeeperId, _pendingShotDecisionSerial + 719));
            return penalty == PenaltyKickOutcome.Goal ? ShotOutcome.Goal : ShotOutcome.Saved;
        }
        return _shotOutcomeResolver.Resolve(
            _pendingShotFinishing, shooter?.positioning ?? 50, shooter?.form ?? 50,
            goalkeeper?.goalkeeping ?? 55, goalkeeper?.form ?? 50,
            _pendingShotDistanceMeters, _pendingShotAngle, _pendingShotPressureMeters,
            1f, 0f,
            DecisionRoll(_pendingShotShooterId, goalkeeperId, _pendingShotDecisionSerial + 181),
            DecisionRoll(goalkeeperId, _pendingShotShooterId, _pendingShotDecisionSerial + 197),
            DecisionRoll(goalkeeperId, _pendingShotShooterId, _pendingShotDecisionSerial + 211));
    }
}
