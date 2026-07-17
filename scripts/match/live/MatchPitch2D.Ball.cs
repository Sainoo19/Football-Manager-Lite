using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MatchPitch2D
{
    private void UpdateBall(float delta)
    {
        if (Simulation is null) return;
        if (_ballActionActive)
        {
            _ballActionElapsed += delta;
            float progress = Mathf.Clamp(_ballActionElapsed / Mathf.Max(_ballActionDuration, 0.01f), 0, 1);
            BallPosition = _ballActionFrom.Lerp(_ballActionTo, progress);
            _ballVisualHeight = Mathf.Sin(progress * Mathf.Pi) * _ballActionArc;
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
        if (_looseBallActive)
        {
            AdvanceRollingBall(delta);
            return;
        }
        if (_ballOwnerId != new StringName() && CurrentPositions.TryGetValue(_ballOwnerId, out Vector2 owner))
        {
            float direction = AttackDirection(_playerTeams[_ballOwnerId]);
            BallPosition = BallPosition.Lerp(
                owner + new Vector2(direction * 0.012f, 0.012f),
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
        if (type is "half_time" or "full_time")
        {
            StartBallAction(new Vector2(0.5f, 0.5f), 0.5f, 0, new StringName(), BallActionKind.None);
            return;
        }
        if (type is "substitution" or "tactic") return;
        if (type == "yellow_card") StartPass(ChoosePassTarget());
        else if (matchEvent.player_id != new StringName() && CurrentPositions.ContainsKey(matchEvent.player_id)) StartPass(matchEvent.player_id);
        else if (_visualTime - _lastPassTime >= 0.4f) StartPass(ChoosePassTarget());
    }

    private void StartPass(StringName receiverId, BallActionKind requestedKind = BallActionKind.Pass)
    {
        if (receiverId == new StringName() ||
            !CurrentPositions.TryGetValue(receiverId, out Vector2 receiverPosition))
        {
            return;
        }
        Vector2 runTarget = TargetPositions.GetValueOrDefault(receiverId, receiverPosition);
        BallActionKind kind = requestedKind;
        float receiverDistanceMeters = FootballPitchDimensions.DistanceMeters(BallPosition, receiverPosition);
        if (kind == BallActionKind.Pass && receiverId == _primaryRunnerId && receiverDistanceMeters > 17f)
        {
            kind = BallActionKind.ThroughBall;
        }
        LivePassType passType = kind switch
        {
            BallActionKind.ThroughBall => LivePassType.ThroughBall,
            BallActionKind.Cross => LivePassType.Cross,
            _ => LivePassType.Standard
        };
        if (passType == LivePassType.ThroughBall)
        {
            runTarget = _throughBallTargetPlanner.FindTarget(
                BallPosition,
                receiverPosition,
                runTarget,
                AttackDirection(_activeTeamId),
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
        }
        PassTrajectory trajectory = _passTrajectoryPlanner.Plan(
            BallPosition,
            receiverPosition,
            runTarget,
            passType);
        _lastPassTime = _visualTime;
        _decisionVarietyTracker.RecordPassTarget(receiverId);
        bool receiverIsOffside = _offsideRule.IsOffside(
            receiverId,
            _activeTeamId,
            BallPosition,
            AttackDirection(_activeTeamId),
            CurrentPositions,
            _playerTeams);
        StartBallAction(
            trajectory.Target,
            trajectory.Duration,
            trajectory.VisualLift,
            receiverId,
            kind);
        _pendingOffsideReceiverId = receiverIsOffside ? receiverId : new StringName();
        string action = kind switch
        {
            BallActionKind.ThroughBall => "chọc khe vào khoảng trống cho",
            BallActionKind.Cross => "tạt bóng tới",
            _ => "chuyền cho"
        };
        SetAction($"{PlayerName(_actionSourceId)} {action} {PlayerName(receiverId)}");
    }

    private void StartBallAction(Vector2 destination, float duration, float arc, StringName nextOwner, BallActionKind kind = BallActionKind.Pass)
    {
        _actionSourceId = _ballOwnerId;
        _actionSourceTeamId = _actionSourceId != new StringName() && _playerTeams.ContainsKey(_actionSourceId) ? _playerTeams[_actionSourceId] : _activeTeamId;
        _ballActionActive = true;
        _ballActionFrom = BallPosition;
        _ballActionTo = destination;
        _ballActionElapsed = 0;
        _ballActionDuration = duration;
        _ballActionArc = arc;
        _ballNextOwnerId = nextOwner;
        _ballActionKind = kind;
        _pendingOffsideReceiverId = new StringName();
        _looseBallVelocityMetersPerSecond = Vector2.Zero;
        _interceptionAttemptedBy.Clear();
        _ballOwnerId = new StringName();
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
        return candidates[(Simulation.current_minute + (int)(_visualTime * 3)) % candidates.Count];
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
        _ballActionActive = false;
        _ballNextOwnerId = new StringName();
        _ballActionKind = BallActionKind.None;
        _ballVisualHeight = 0f;

        if (completedKind == BallActionKind.Shot)
        {
            CompleteLiveShot();
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
        else if (completedKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross)
        {
            CompletePassReception(intendedReceiverId, completedKind);
        }
        else if (intendedReceiverId != new StringName())
        {
            GivePossessionTo(intendedReceiverId, 0.32f);
        }

        _pendingOffsideReceiverId = new StringName();
        _nextDecisionTime = _visualTime + 0.32f;
        SelectPhasePlayers();
    }

    private void CompletePassReception(StringName receiverId, BallActionKind completedKind)
    {
        float controlDistanceMeters = completedKind == BallActionKind.ThroughBall ? 3.2f : 2.2f;
        if (receiverId != new StringName() &&
            CurrentPositions.TryGetValue(receiverId, out Vector2 receiverPosition) &&
            FootballPitchDimensions.DistanceMeters(receiverPosition, BallPosition) <= controlDistanceMeters)
        {
            CompletedPasses++;
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
            EmitSignal(SignalName.LiveMatchEvent, offsideEvent);
        }
        Vector2 restartPosition = CurrentPositions.GetValueOrDefault(receiverId, BallPosition);
        ScheduleRestart("free_kick", defendingTeamId, restartPosition);
        SetAction($"{PlayerName(receiverId)} việt vị — đội phòng ngự được đá phạt");
    }

    private StringName NearestOpponent(StringName playerId)
    {
        if (!CurrentPositions.TryGetValue(playerId, out Vector2 position)) return new StringName();
        return CurrentPositions.Keys.Where(id => _playerTeams[id] != _playerTeams[playerId] && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(position)).FirstOrDefault() ?? new StringName();
    }

    private float PassingLaneRisk(Vector2 from, Vector2 to, StringName passingTeamId)
    {
        float highestRisk = 0;
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] == passingTeamId || _playerRoles[playerId] == "GK") continue;
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
        EmitSignal(SignalName.ActionChanged, description);
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
