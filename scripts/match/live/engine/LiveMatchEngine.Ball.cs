using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed partial class LiveMatchEngine
{
    private void UpdateBall(float delta)
    {
        if (Simulation is null) return;
        if (_ballActionActive)
        {
            _ballActionElapsed += delta;
            if (_aerialFlightActive)
            {
                AerialBallSample sample = _aerialTrajectory.Sample(_ballActionElapsed);
                BallPosition = sample.Position;
                _ballVisualHeight = sample.HeightMeters;
                _ballVerticalVelocityMetersPerSecond = sample.VerticalVelocityMetersPerSecond;
                if (sample.HasLanded)
                {
                    CompleteBallAction();
                }
                return;
            }
            float progress = Mathf.Clamp(_ballActionElapsed / Mathf.Max(_ballActionDuration, 0.01f), 0, 1);
            BallPosition = _ballActionFrom.Lerp(_ballActionTo, progress);
            _ballVisualHeight = _ballActionKind == BallActionKind.Shot
                ? Mathf.Sin(progress * Mathf.Pi) *
                  _ballActionArc * FootballPitchDimensions.WidthMeters
                : 0f;
            _ballVerticalVelocityMetersPerSecond = 0f;
            if (progress is >= 0.14f and <= 0.94f &&
                _ballActionKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross)
            {
                TryInterceptMovingBall();
            }
            if (progress >= 1)
            {
                CompleteBallAction();
            }
            return;
        }
        if (_state.IsLooseBallActive)
        {
            AdvanceRollingBall(delta);
            return;
        }
        if (_state.BallOwnerId != new StringName() && CurrentPositions.TryGetValue(_state.BallOwnerId, out Vector2 owner))
        {
            float direction = AttackDirection(_playerTeams[_state.BallOwnerId]);
            Vector2 ballTarget = owner + new Vector2(direction * 0.012f, 0.012f);
            GroundDuelSequenceState dribbleSequence = _state.GroundDuel;
            if (dribbleSequence.HasCarrier &&
                dribbleSequence.CarrierId == _state.BallOwnerId &&
                dribbleSequence.TouchCount > 0)
            {
                Vector2 touchDirectionMeters = FootballPitchDimensions.ToMeters(dribbleSequence.CurrentTouch.Target) -
                                               FootballPitchDimensions.ToMeters(owner);
                Vector2 touchDirection = touchDirectionMeters.LengthSquared() > 0.001f
                    ? touchDirectionMeters.Normalized()
                    : new Vector2(direction, 0f);
                Vector2 leadMeters = touchDirection * dribbleSequence.CurrentTouch.BallLeadMeters;
                ballTarget = owner + FootballPitchDimensions.ToNormalized(leadMeters);
            }
            BallPosition = BallPosition.Lerp(
                SpaceEvaluator.ClampToPitch(ballTarget),
                1f - Mathf.Exp(-delta * 8f));
        }
    }

    private void AnimateEvent(FootballMatchEvent matchEvent)
    {
        if (Simulation is null) return;
        string type = matchEvent.event_type.ToString();
        if (type == "goal")
        {
            StringName concedingTeamId = matchEvent.team_id == Simulation.home.team.id
                ? Simulation.away.team.id
                : Simulation.home.team.id;
            ScheduleRestart("kickoff", concedingTeamId, new Vector2(0.5f, 0.5f));
            SetAction($"BÀN THẮNG — {PlayerName(matchEvent.player_id)}, hai đội trở lại giao bóng");
            return;
        }
        if (type is "shot_on_target" or "shot_off_target")
        {
            StringName defending = matchEvent.team_id == Simulation.home.team.id
                ? Simulation.away.team.id
                : Simulation.home.team.id;
            StartBallAction(
                new Vector2(AttackingGoalX(matchEvent.team_id), 0.5f),
                0.72f,
                0.045f,
                ChooseGoalkeeper(defending),
                BallActionKind.Shot);
            SetAction($"{PlayerName(matchEvent.player_id)} dứt điểm");
            return;
        }
        if (type == "corner")
        {
            float goalX = AttackingGoalX(matchEvent.team_id);
            BallPosition = new Vector2(goalX < 0.5f ? 0.018f : 0.982f, Simulation.current_minute % 2 == 0 ? 0.035f : 0.965f);
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChooseOwner(matchEvent.team_id, true);
            StartBallAction(new Vector2(goalX < 0.5f ? 0.12f : 0.88f, 0.5f), 0.68f, 0.055f, receiver, BallActionKind.Cross);
            SetAction("Quả tạt từ chấm phạt góc");
            return;
        }
        if (type == "full_time")
        {
            CompletePossessionSpell();
            _ballActionActive = false;
            _aerialFlightActive = false;
            _aerialContenderIds.Clear();
            _ballVisualHeight = 0f;
            _ballVerticalVelocityMetersPerSecond = 0f;
            _state.IsLooseBallActive = false;
            _state.IsRestartPending = false;
            _state.BallOwnerId = new StringName();
            _runtime.SetPhase(LiveMatchPhase.FullTime);
            SetAction("Hết trận");
            return;
        }
        if (type is "substitution" or "tactic") return;
        if (type == "yellow_card") StartPass(ChoosePassTarget());
        else if (matchEvent.player_id != new StringName() && CurrentPositions.ContainsKey(matchEvent.player_id)) StartPass(matchEvent.player_id);
        else if (_state.VisualTime - _lastPassTime >= 0.4f) StartPass(ChoosePassTarget());
    }

    private void StartPass(StringName receiverId, BallActionKind requestedKind = BallActionKind.Pass)
    {
        if (Simulation is null ||
            receiverId is null ||
            receiverId == new StringName() ||
            !CurrentPositions.TryGetValue(receiverId, out Vector2 receiverPosition))
        {
            return;
        }
        Vector2 runTarget = TargetPositions.GetValueOrDefault(receiverId, receiverPosition);
        BallActionKind kind = requestedKind;
        StringName passerId = _state.BallOwnerId;
        FootballPlayer? passer = GetPlayer(passerId);
        float receiverDistanceMeters = FootballPitchDimensions.DistanceMeters(BallPosition, receiverPosition);
        float forwardGainMeters = AttackDirection(_state.ActiveTeamId) * (receiverPosition.X - BallPosition.X) *
                                  FootballPitchDimensions.LengthMeters;
        if (kind == BallActionKind.Pass &&
            receiverId == _primaryRunnerId &&
            receiverDistanceMeters > 20f &&
            forwardGainMeters >= 8f &&
            (passer?.passing ?? 50) + (passer?.vision ?? 50) >=
            _configuration.MinimumThroughBallCreativeSkill)
        {
            kind = BallActionKind.ThroughBall;
        }
        if (kind == BallActionKind.Pass &&
            receiverDistanceMeters >= _configuration.MinimumLoftedPassDistanceMeters &&
            (passer?.passing ?? 50) + (passer?.vision ?? 50) >= 126)
        {
            kind = BallActionKind.LoftedPass;
        }
        LivePassType passType = kind switch
        {
            BallActionKind.ThroughBall => LivePassType.ThroughBall,
            BallActionKind.LoftedPass => LivePassType.Lofted,
            BallActionKind.Cross => LivePassType.Cross,
            _ => LivePassType.Standard
        };
        if (passType == LivePassType.ThroughBall)
        {
            runTarget = _throughBallTargetPlanner.FindTarget(
                BallPosition,
                receiverPosition,
                runTarget,
                AttackDirection(_state.ActiveTeamId),
                _state.ActiveTeamId,
                CurrentPositions,
                _playerTeams);
        }
        PassTrajectory intendedTrajectory = _passTrajectoryPlanner.Plan(
            BallPosition,
            receiverPosition,
            runTarget,
            passType);
        StringName nearestOpponentId = NearestOpponent(passerId);
        float pressureDistanceMeters = nearestOpponentId == new StringName()
            ? float.PositiveInfinity
            : FootballPitchDimensions.DistanceMeters(
                CurrentPositions[passerId],
                CurrentPositions[nearestOpponentId]);
        PassExecution execution = _passExecutionResolver.Resolve(
            BallPosition,
            intendedTrajectory.Target,
            passType,
            passer?.passing ?? 50,
            passer?.Technique ?? 50,
            passer?.Composure ?? 50,
            passer?.form ?? 50,
            pressureDistanceMeters,
            DecisionRoll(passerId, receiverId, _decisionSerial + 503),
            DecisionRoll(passerId, receiverId, _decisionSerial + 521));
        ResetCarrySequence();
        _lastPassTime = _state.VisualTime;
        _decisionVarietyTracker.RecordPassTarget(receiverId);
        PassAttempts++;
        Simulation.RegisterLivePassAttempt(_state.ActiveTeamId);
        bool receiverIsOffside = _offsideRule.IsOffside(
            receiverId,
            _state.ActiveTeamId,
            BallPosition,
            AttackDirection(_state.ActiveTeamId),
            CurrentPositions,
            _playerTeams);
        StartBallAction(
            execution.ActualTarget,
            execution.DurationSeconds,
            intendedTrajectory.VisualLift,
            receiverId,
            kind);
        _pendingPassType = passType;
        _pendingPassSpeedMetersPerSecond = execution.BallSpeedMetersPerSecond;
        _lastPassExecutionQuality = execution.Quality;
        _lastPassIntendedTarget = intendedTrajectory.Target;
        _lastPassActualTarget = execution.ActualTarget;
        _pendingOffsideReceiverId = receiverIsOffside ? receiverId : new StringName();
        string action = kind switch
        {
            BallActionKind.ThroughBall => "chọc khe vào khoảng trống cho",
            BallActionKind.LoftedPass => "phất bóng bổng tới khu vực của",
            BallActionKind.Cross => "tạt bóng tới",
            _ => "chuyền cho"
        };
        SetAction($"{PlayerName(_actionSourceId)} {action} {PlayerName(receiverId)}");
    }

    private void StartBallAction(Vector2 destination, float duration, float arc, StringName nextOwner, BallActionKind kind = BallActionKind.Pass)
    {
        _state.IsBallVisible = true;
        _actionSourceId = _state.BallOwnerId;
        _actionSourceTeamId = _actionSourceId != new StringName() && _playerTeams.ContainsKey(_actionSourceId) ? _playerTeams[_actionSourceId] : _state.ActiveTeamId;
        _ballActionActive = true;
        _ballActionFrom = BallPosition;
        _ballActionTo = destination;
        _ballActionElapsed = 0;
        AerialDeliveryType? aerialDeliveryType = AerialDeliveryFor(kind);
        _aerialFlightActive = aerialDeliveryType.HasValue;
        if (aerialDeliveryType.HasValue)
        {
            _aerialTrajectory = _aerialBallTrajectoryPlanner.Plan(
                _ballActionFrom,
                destination,
                aerialDeliveryType.Value);
            _ballActionTo = _aerialTrajectory.LandingPoint;
            _ballActionDuration = _aerialTrajectory.FlightTimeSeconds;
            _ballVerticalVelocityMetersPerSecond =
                _aerialTrajectory.InitialVerticalVelocityMetersPerSecond;
        }
        else
        {
            _aerialTrajectory = default;
            _ballActionDuration = duration;
            _ballVerticalVelocityMetersPerSecond = 0f;
        }
        _ballActionArc = arc;
        _ballNextOwnerId = nextOwner;
        _ballActionKind = kind;
        _runtime.SetPhase(LiveMatchPhase.BallInFlight);
        if (kind is not (BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross))
        {
            _pendingPassType = LivePassType.Standard;
            _pendingPassSpeedMetersPerSecond = 0f;
        }
        _pendingOffsideReceiverId = new StringName();
        _state.LooseBallVelocityMetersPerSecond = Vector2.Zero;
        _interceptionAttemptedBy.Clear();
        _state.BallOwnerId = new StringName();
        PrepareAerialContenders();
        SelectPhasePlayers();
    }

    private StringName ChooseOwner(StringName teamId, bool preferAttackers)
    {
        if (Simulation is null) return new StringName();
        var preferred = new List<StringName>();
        var fallback = new List<StringName>();
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] != teamId) continue;
            fallback.Add(playerId);
            string role = _playerRoles[playerId];
            bool preferredRole = preferAttackers ? role is "AM" or "LW" or "RW" or "ST" : role is "DM" or "CM" or "AM" or "LB" or "RB";
            if (preferredRole) preferred.Add(playerId);
        }
        List<StringName> candidates = preferred.Count > 0 ? preferred : fallback;
        if (candidates.Count == 0) return new StringName();
        return candidates[(Simulation.current_minute + (int)(_state.VisualTime * 3)) % candidates.Count];
    }

    private StringName ChooseGoalkeeper(StringName teamId)
    {
        foreach (StringName playerId in CurrentPositions.Keys)
            if (_playerTeams[playerId] == teamId && _playerRoles[playerId] == "GK") return playerId;
        return ChooseOwner(teamId, false);
    }

    private void CompleteBallAction()
    {
        BallActionKind completedKind = _ballActionKind;
        StringName intendedReceiverId = _ballNextOwnerId;
        bool completedAerialFlight = _aerialFlightActive;
        _ballActionActive = false;
        _aerialFlightActive = false;
        _ballNextOwnerId = new StringName();
        _ballActionKind = BallActionKind.None;
        _ballVisualHeight = 0f;
        _ballVerticalVelocityMetersPerSecond = 0f;

        if (completedKind != BallActionKind.Shot &&
            (BallPosition.X is < 0f or > 1f || BallPosition.Y is < 0f or > 1f))
        {
            StartLooseBall(
                "Bóng đi hết đường biên — hai đội chuẩn bị tình huống cố định",
                RollingVelocityAfterFlight(completedKind));
            _pendingOffsideReceiverId = new StringName();
            return;
        }

        if (completedKind == BallActionKind.Shot)
        {
            CompleteLiveShot();
        }
        else if (completedAerialFlight &&
                 completedKind is BallActionKind.LoftedPass or BallActionKind.Cross or BallActionKind.Clearance)
        {
            ResolveAerialArrival(completedKind, intendedReceiverId);
        }
        else if (_pendingOffsideReceiverId != new StringName())
        {
            ResolveOffside(_pendingOffsideReceiverId);
        }
        else if (completedKind == BallActionKind.Clearance)
        {
            StartLooseBall(
                "Bóng được phá lên khoảng trống — hai đội cùng lao tới",
                RollingVelocityAfterFlight(completedKind));
        }
        else if (completedKind == BallActionKind.HeaderClearance)
        {
            _aerialContenderIds.Clear();
            AerialSecondBalls++;
            StartLooseBall(
                "Bóng bật ra sau pha không chiến — hai đội tranh bóng hai",
                RollingVelocityAfterFlight(completedKind));
        }
        else if (completedKind == BallActionKind.HeaderPass)
        {
            _aerialContenderIds.Clear();
            CompletePassReception(intendedReceiverId, completedKind);
        }
        else if (completedKind is BallActionKind.Pass or BallActionKind.ThroughBall or
                 BallActionKind.LoftedPass or BallActionKind.Cross)
        {
            CompletePassReception(intendedReceiverId, completedKind);
        }
        else if (intendedReceiverId != new StringName())
        {
            GivePossessionTo(intendedReceiverId, 0.32f);
        }

        _pendingOffsideReceiverId = new StringName();
        _nextDecisionTime = _state.VisualTime + 0.32f;
        SelectPhasePlayers();
    }

    private void CompletePassReception(StringName receiverId, BallActionKind completedKind)
    {
        if (BallPosition.X is < 0f or > 1f || BallPosition.Y is < 0f or > 1f)
        {
            StartLooseBall(
                "Đường chuyền đi hết biên — trọng tài cho đội còn lại đưa bóng vào cuộc",
                RollingVelocityAfterFlight(completedKind));
            return;
        }

        float controlDistanceMeters = completedKind switch
        {
            BallActionKind.ThroughBall => 3.2f,
            BallActionKind.LoftedPass => 3.0f,
            BallActionKind.Cross => 2.7f,
            _ => 2.2f
        };
        controlDistanceMeters *= _configuration.PassControlDistanceMultiplier;
        if (receiverId != new StringName() &&
            CurrentPositions.TryGetValue(receiverId, out Vector2 receiverPosition) &&
            FootballPitchDimensions.DistanceMeters(receiverPosition, BallPosition) <= controlDistanceMeters)
        {
            FootballPlayer? receiver = GetPlayer(receiverId);
            StringName nearestOpponentId = NearestOpponent(receiverId);
            float pressureDistanceMeters = nearestOpponentId == new StringName()
                ? float.PositiveInfinity
                : FootballPitchDimensions.DistanceMeters(
                    receiverPosition,
                    CurrentPositions[nearestOpponentId]);
            FirstTouchResolution touch = _firstTouchResolver.Resolve(
                receiver?.FirstTouch ?? 50,
                receiver?.Technique ?? 50,
                receiver?.Composure ?? 50,
                receiver?.form ?? 50,
                pressureDistanceMeters,
                _pendingPassSpeedMetersPerSecond,
                _pendingPassType,
                DecisionRoll(receiverId, _actionSourceId, _decisionSerial + 547),
                DecisionRoll(receiverId, nearestOpponentId, _decisionSerial + 563));
            if (touch.Outcome != FirstTouchOutcome.Controlled)
            {
                ResolveFirstTouchError(receiverId, touch);
                return;
            }

            CompletedPasses++;
            Simulation!.RegisterLivePassCompletion(_actionSourceTeamId);
            if (ShouldBeginDirectAttack(receiverId, completedKind))
            {
                BeginDirectAttack(receiverId);
            }
            GivePossessionTo(receiverId, 0.32f);
            return;
        }

        StartLooseBall(
            "Đường chuyền thiếu lực — bóng tiếp tục lăn chậm, cầu thủ phải chạy tới",
            RollingVelocityAfterFlight(completedKind));
    }

    private void ResolveFirstTouchError(StringName receiverId, FirstTouchResolution touch)
    {
        FirstTouchErrors++;
        Simulation!.RegisterLiveFirstTouchError(_playerTeams[receiverId]);
        Vector2 flightVectorMeters = FootballPitchDimensions.ToMeters(_ballActionTo) -
                                     FootballPitchDimensions.ToMeters(_ballActionFrom);
        Vector2 direction = flightVectorMeters.LengthSquared() > 0.001f
            ? flightVectorMeters.Normalized()
            : Vector2.Right;
        string description = touch.Outcome == FirstTouchOutcome.HeavyTouch
            ? $"{PlayerName(receiverId)} đỡ bước một hơi dài — hai đội tranh bóng"
            : $"{PlayerName(receiverId)} khống chế lỗi và để bóng bật ra";
        StartLooseBall(description, direction * touch.LooseBallSpeedMetersPerSecond);
    }

    private void ResolveOffside(StringName receiverId)
    {
        if (Simulation is null)
            return;
        StringName attackingTeamId = _actionSourceTeamId;
        StringName defendingTeamId = attackingTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        FootballMatchEvent? offsideEvent = Simulation.RegisterLiveOffside(attackingTeamId, receiverId);
        if (offsideEvent is not null)
        {
            LiveMatchEvent?.Invoke(offsideEvent);
        }
        Vector2 restartPosition = CurrentPositions.GetValueOrDefault(receiverId, BallPosition);
        ScheduleRestart(
            "free_kick",
            defendingTeamId,
            restartPosition,
            allowsQuickRestart: true);
        SetAction($"{PlayerName(receiverId)} việt vị — đội phòng ngự chuẩn bị thực hiện đá phạt");
    }

    private StringName NearestOpponent(StringName playerId)
    {
        if (!CurrentPositions.TryGetValue(playerId, out Vector2 position)) return new StringName();
        return CurrentPositions.Keys.Where(id => _playerTeams[id] != _playerTeams[playerId])
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(position)).FirstOrDefault() ?? new StringName();
    }

    private float PassingLaneRisk(Vector2 from, Vector2 to, StringName passingTeamId)
    {
        float highestRisk = 0;
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] == passingTeamId || _playerRoles[playerId] == "GK") continue;
            float pressureDistanceMeters = FootballPitchDimensions.DistanceMeters(
                from,
                CurrentPositions[playerId]);
            if (pressureDistanceMeters <= DuelDistanceRules.PressureDistanceMeters)
            {
                // The execution resolver already applies the carrier's immediate pressure.
                // Counting the same marker as a lane obstacle would make every direction
                // appear blocked and trap the carrier in the ground-duel pipeline.
                continue;
            }
            float distance = DistanceToSegment(CurrentPositions[playerId], from, to);
            highestRisk = Mathf.Max(highestRisk, 1f - Mathf.Clamp(distance / 0.13f, 0, 1));
        }
        return highestRisk;
    }

    private FootballPlayer? GetPlayer(StringName playerId)
    {
        if (Simulation is null || playerId == new StringName()) return null;
        return Simulation.home.team.get_player(playerId) ?? Simulation.away.team.get_player(playerId);
    }

    private string PlayerName(StringName playerId) => GetPlayer(playerId)?.display_name ?? "Một cầu thủ";

    private void SetAction(string description)
    {
        LastActionName = description;
        ActionChanged?.Invoke(description);
    }

    private float DecisionRoll(StringName firstId, StringName secondId, int serial)
    {
        return CalculateRoll(firstId, secondId, serial, _liveDecisionSeed);
    }

    private float VarietyRoll(StringName firstId, StringName secondId, int serial)
    {
        return CalculateRoll(firstId, secondId, serial, _liveDecisionSeed ^ 0x9e3779b9u);
    }

    private static float CalculateRoll(StringName firstId, StringName secondId, int serial, uint seed)
    {
        uint value = unchecked(StableHash(firstId) * 2654435761u) ^ seed;
        value ^= unchecked(StableHash(secondId) * 2246822519u);
        value ^= unchecked((uint)serial * 3266489917u);
        value ^= value >> 16;
        value *= 0x85ebca6bu;
        value ^= value >> 13;
        value *= 0xc2b2ae35u;
        value ^= value >> 16;
        return (value & 0x00ffffff) / 16777215f;
    }

    private static uint StableHash(StringName value)
    {
        uint hash = 2166136261u;
        foreach (char character in value.ToString()) { hash ^= character; hash *= 16777619u; }
        return hash;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f) return point.DistanceTo(start);
        float progress = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0, 1);
        return point.DistanceTo(start + segment * progress);
    }
}
