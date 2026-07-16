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
            float eased = progress * progress * (3 - 2 * progress);
            BallPosition = _ballActionFrom.Lerp(_ballActionTo, eased);
            BallPosition = new Vector2(BallPosition.X, BallPosition.Y - Mathf.Sin(progress * Mathf.Pi) * _ballActionArc);
            if (!_interceptionChecked && progress >= 0.44f && _ballActionKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross)
            {
                _interceptionChecked = true;
                TryInterceptMovingBall();
            }
            if (progress >= 1)
            {
                _ballActionActive = false;
                _ballOwnerId = _ballNextOwnerId;
                _ballNextOwnerId = new StringName();
                if (_ballActionKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross) CompletedPasses++;
                if (_ballActionKind == BallActionKind.Interception)
                {
                    Interceptions++;
                    SetAction($"{PlayerName(_ballOwnerId)} cắt được đường bóng");
                }
                if (_ballActionKind == BallActionKind.Shot) CompleteLiveShot();
                _ballActionKind = BallActionKind.None;
                _nextDecisionTime = _visualTime + 0.32f;
            }
            return;
        }
        if (_ballOwnerId != new StringName() && CurrentPositions.TryGetValue(_ballOwnerId, out Vector2 owner))
        {
            bool isHome = _playerTeams[_ballOwnerId] == Simulation.home.team.id;
            BallPosition = BallPosition.Lerp(owner + new Vector2(isHome ? -0.012f : 0.012f, 0.012f), 1f - Mathf.Exp(-delta * 8f));
        }
    }

    private void AnimateEvent(FootballMatchEvent matchEvent)
    {
        if (Simulation is null) return;
        string type = matchEvent.event_type.ToString();
        if (type is "goal" or "shot_on_target" or "shot_off_target")
        {
            bool homeAttack = matchEvent.team_id == Simulation.home.team.id;
            StringName defending = homeAttack ? Simulation.away.team.id : Simulation.home.team.id;
            StartBallAction(new Vector2(homeAttack ? 0.006f : 0.994f, 0.5f), 0.72f, 0.045f, ChooseGoalkeeper(defending), BallActionKind.Shot);
            SetAction($"{PlayerName(matchEvent.player_id)} dứt điểm");
            return;
        }
        if (type == "corner")
        {
            bool homeCorner = matchEvent.team_id == Simulation.home.team.id;
            BallPosition = new Vector2(homeCorner ? 0.018f : 0.982f, Simulation.current_minute % 2 == 0 ? 0.035f : 0.965f);
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChooseOwner(matchEvent.team_id, true);
            StartBallAction(new Vector2(homeCorner ? 0.12f : 0.88f, 0.5f), 0.68f, 0.055f, receiver, BallActionKind.Cross);
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

    private void StartPass(StringName ownerId, BallActionKind requestedKind = BallActionKind.Pass)
    {
        if (ownerId == new StringName() || !CurrentPositions.TryGetValue(ownerId, out Vector2 current)) return;
        Vector2 runTarget = TargetPositions.GetValueOrDefault(ownerId, current);
        float lead = ownerId == _primaryRunnerId ? 0.78f : ownerId == _secondaryRunnerId ? 0.58f : 0.34f;
        Vector2 target = current.Lerp(runTarget, lead);
        float distance = BallPosition.DistanceTo(target);
        BallActionKind kind = requestedKind;
        if (kind == BallActionKind.Pass && ownerId == _primaryRunnerId && distance > 0.16f) kind = BallActionKind.ThroughBall;
        _lastPassTime = _visualTime;
        StartBallAction(target, Mathf.Clamp(0.28f + distance * 0.72f, 0.34f, 0.68f), 0.018f + distance * 0.035f, ownerId, kind);
        string action = kind switch { BallActionKind.ThroughBall => "chọc khe cho", BallActionKind.Cross => "tạt bóng tới", _ => "chuyền cho" };
        SetAction($"{PlayerName(_actionSourceId)} {action} {PlayerName(ownerId)}");
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
        _interceptionChecked = kind is BallActionKind.None or BallActionKind.Shot;
        _ballOwnerId = new StringName();
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

    private void TryInterceptMovingBall()
    {
        if (Simulation is null || _actionSourceTeamId == new StringName()) return;
        var defenders = CurrentPositions.Keys
            .Where(id => _playerTeams[id] != _actionSourceTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(BallPosition)).ToList();
        if (defenders.Count == 0) return;
        StringName defenderId = defenders[0];
        float distance = CurrentPositions[defenderId].DistanceTo(BallPosition);
        float threshold = _ballActionKind switch { BallActionKind.Cross => 0.115f, BallActionKind.ThroughBall => 0.095f, _ => 0.075f };
        if (distance > threshold) return;
        FootballPlayer? passer = GetPlayer(_actionSourceId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float passingSkill = ((passer?.passing ?? 50) + (passer?.vision ?? 50)) * 0.5f;
        float defensiveSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float chance = Mathf.Clamp(0.12f + (threshold - distance) / threshold * 0.38f + (defensiveSkill - passingSkill) / 190f, 0.06f, 0.68f);
        float roll = DecisionRoll(_actionSourceId, defenderId, _decisionSerial + _phaseSerial * 13);
        if (roll >= chance)
        {
            float deflectionChance = _ballActionKind == BallActionKind.Cross ? 0.16f : 0.08f;
            if (roll < chance + deflectionChance)
            {
                _ballActionActive = false;
                _ballActionKind = BallActionKind.None;
                BallPosition = new Vector2(BallPosition.X, BallPosition.Y < 0.5f ? 0.025f : 0.975f);
                ScheduleRestart("throw_in", _actionSourceTeamId, BallPosition);
                SetAction($"{PlayerName(defenderId)} phá bóng ra đường biên");
            }
            return;
        }
        _ballActionFrom = BallPosition;
        _ballActionTo = CurrentPositions[defenderId];
        _ballActionElapsed = 0;
        _ballActionDuration = 0.18f;
        _ballActionArc = 0.004f;
        _ballNextOwnerId = defenderId;
        _ballActionKind = BallActionKind.Interception;
        _activeTeamId = _playerTeams[defenderId];
        Simulation.set_live_possession(_activeTeamId);
        bool homeRecovery = _activeTeamId == Simulation.home.team.id;
        _attackProgress = Mathf.Clamp(homeRecovery ? 1f - BallPosition.X : BallPosition.X, 0.16f, 0.60f);
        _phaseLane = CurrentPositions[defenderId].Y;
        SelectPhasePlayers();
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

    private static float DecisionRoll(StringName firstId, StringName secondId, int serial)
    {
        uint value = unchecked(StableHash(firstId) * 2654435761u);
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
