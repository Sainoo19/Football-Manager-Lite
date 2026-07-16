using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class MatchPitch2D
{
    private void UpdatePlayerTargets()
    {
        if (Simulation is null) return;
        Vector2 ballAnchor = PhaseBallAnchor();
        var proposed = new System.Collections.Generic.Dictionary<StringName, Vector2>();
        foreach (StringName playerId in BasePositions.Keys)
            if (_playerTeams[playerId] == _activeTeamId)
                proposed[playerId] = AttackingTarget(playerId, ballAnchor);
        foreach (StringName playerId in BasePositions.Keys)
            if (_playerTeams[playerId] != _activeTeamId)
                proposed[playerId] = DefensiveTarget(playerId, ballAnchor, proposed);
        ApplyTeamSeparation(proposed);
        foreach ((StringName playerId, Vector2 target) in proposed)
            TargetPositions[playerId] = ClampToPitch(target);
    }

    private Vector2 AttackingTarget(StringName playerId, Vector2 ballAnchor)
    {
        bool isHome = _playerTeams[playerId] == Simulation!.home.team.id;
        float direction = isHome ? -1f : 1f;
        string role = _playerRoles[playerId];
        Vector2 basePosition = BasePositions[playerId];
        float ahead = role switch
        {
            "GK" => -0.52f, "CB" => -0.28f, "LB" or "RB" => -0.13f,
            "DM" => -0.14f, "CM" => -0.025f, "AM" => 0.10f,
            "LW" or "RW" => 0.16f, "ST" => 0.22f, _ => 0f
        };
        MatchTeamState state = _activeTeamId == Simulation.home.team.id ? Simulation.home : Simulation.away;
        if (state.mentality == "attacking" && role != "GK") ahead += 0.045f;
        if (state.mentality == "defensive" && role != "GK") ahead -= 0.035f;
        float targetY = Mathf.Lerp(basePosition.Y, _phaseLane, role is "CM" or "AM" or "ST" ? 0.34f : 0.12f);
        if (role is "LB" or "LW") targetY = Mathf.Min(targetY, 0.22f);
        if (role is "RB" or "RW") targetY = Mathf.Max(targetY, 0.78f);
        if (playerId == _primaryRunnerId)
        {
            ahead += 0.19f;
            targetY = Mathf.Lerp(targetY, _phaseLane, 0.58f);
        }
        else if (playerId == _secondaryRunnerId)
        {
            ahead += role is "LB" or "RB" ? 0.23f : 0.12f;
            if (role == "LB") targetY = 0.09f;
            if (role == "RB") targetY = 0.91f;
        }
        if (playerId == _ballOwnerId)
        {
            ahead = -0.005f;
            targetY = _phaseLane;
        }
        float supportOffset = ((Math.Abs(playerId.GetHashCode()) % 3) - 1) * 0.018f;
        return new Vector2(ballAnchor.X + direction * ahead, targetY + supportOffset);
    }

    private Vector2 DefensiveTarget(StringName playerId, Vector2 ballAnchor, System.Collections.Generic.Dictionary<StringName, Vector2> attackingTargets)
    {
        bool isHome = _playerTeams[playerId] == Simulation!.home.team.id;
        float attackDirection = isHome ? -1f : 1f;
        float goalSide = -attackDirection;
        string role = _playerRoles[playerId];
        Vector2 basePosition = BasePositions[playerId];
        if (role == "GK")
            return new Vector2(isHome ? 0.945f : 0.055f, Mathf.Lerp(0.5f, ballAnchor.Y, 0.18f));
        if (playerId == _pressingPlayerId)
            return ballAnchor + new Vector2(goalSide * 0.022f, 0);
        if (role is "CB" or "LB" or "RB")
        {
            var marks = attackingTargets
                .Where(pair => _playerRoles[pair.Key] is "ST" or "LW" or "RW" or "AM")
                .OrderBy(pair => Math.Abs(pair.Value.Y - basePosition.Y)).ToList();
            Vector2 reference = marks.Count > 0 ? marks[0].Value : ballAnchor;
            float lineX = ballAnchor.X + goalSide * 0.14f;
            float markedX = reference.X + goalSide * 0.035f;
            return new Vector2(Mathf.Lerp(lineX, markedX, 0.62f), Mathf.Lerp(basePosition.Y, reference.Y, 0.62f));
        }
        float distance = role switch { "DM" => 0.09f, "CM" => 0.065f, "AM" => 0.04f, _ => 0.025f };
        float compactness = role is "DM" or "CM" ? 0.48f : 0.28f;
        return new Vector2(ballAnchor.X + goalSide * distance, Mathf.Lerp(basePosition.Y, ballAnchor.Y, compactness));
    }

    private void AdvancePhase(bool turnover, FootballMatchEvent? focusEvent)
    {
        if (Simulation is null) return;
        _phaseSerial++;
        string eventType = focusEvent?.event_type.ToString() ?? "";
        if (eventType is "half_time" or "full_time")
        {
            _attackProgress = 0.48f;
            _phaseLane = 0.5f;
            return;
        }
        if (turnover)
        {
            bool homeAttack = _activeTeamId == Simulation.home.team.id;
            _attackProgress = Mathf.Clamp(homeAttack ? 1f - BallPosition.X : BallPosition.X, 0.18f, 0.58f);
        }
        else
        {
            _attackProgress += 0.075f;
            if (_attackProgress > 0.88f && !IsAttackingEvent(focusEvent?.event_type ?? new StringName()))
                _attackProgress = 0.34f;
        }
        if (eventType is "goal" or "shot_on_target" or "shot_off_target") _attackProgress = 0.96f;
        else if (eventType == "corner") _attackProgress = 0.91f;
        if (turnover || _phaseSerial % 2 == 0 || eventType == "corner")
        {
            float[] lanes = { 0.18f, 0.5f, 0.82f, 0.34f, 0.66f };
            int teamSalt = Math.Abs(_activeTeamId.GetHashCode()) % lanes.Length;
            _phaseLane = lanes[(Simulation.current_minute + _phaseSerial + teamSalt) % lanes.Length];
        }
    }

    private void SelectPhasePlayers()
    {
        if (Simulation is null || CurrentPositions.Count == 0) return;
        Vector2 anchor = PhaseBallAnchor();
        var runners = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == _activeTeamId && _playerRoles[id] is "ST" or "LW" or "RW" or "AM" or "CM")
            .OrderBy(id => Math.Abs(id.GetHashCode() + _phaseSerial * 17)).ToList();
        _primaryRunnerId = runners.Count > 0 ? runners[_phaseSerial % runners.Count] : new StringName();
        var supportRunners = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == _activeTeamId && id != _primaryRunnerId && _playerRoles[id] is "LB" or "RB" or "CM" or "LW" or "RW")
            .OrderBy(id => Math.Abs(id.GetHashCode() - _phaseSerial * 11)).ToList();
        _secondaryRunnerId = supportRunners.Count > 0 ? supportRunners[_phaseSerial % supportRunners.Count] : new StringName();
        _pressingPlayerId = CurrentPositions.Keys
            .Where(id => _playerTeams[id] != _activeTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(anchor)).FirstOrDefault() ?? new StringName();
    }

    private Vector2 PhaseBallAnchor()
    {
        if (Simulation is null) return new Vector2(0.5f, 0.5f);
        bool homeAttack = _activeTeamId == Simulation.home.team.id;
        return new Vector2(Mathf.Lerp(homeAttack ? 0.86f : 0.14f, homeAttack ? 0.08f : 0.92f, _attackProgress), _phaseLane);
    }

    private void ApplyTeamSeparation(System.Collections.Generic.Dictionary<StringName, Vector2> positions)
    {
        StringName[] ids = positions.Keys.ToArray();
        for (int first = 0; first < ids.Length; first++)
            for (int second = first + 1; second < ids.Length; second++)
            {
                if (_playerTeams[ids[first]] != _playerTeams[ids[second]]) continue;
                Vector2 delta = positions[ids[second]] - positions[ids[first]];
                if (delta.LengthSquared() >= 0.0016f) continue;
                float push = delta.Y >= 0 ? 0.022f : -0.022f;
                positions[ids[first]] += new Vector2(0, -push);
                positions[ids[second]] += new Vector2(0, push);
            }
    }

    private static bool IsAttackingEvent(StringName eventType) =>
        eventType.ToString() is "goal" or "shot_on_target" or "shot_off_target" or "corner";

    private static Vector2 ClampToPitch(Vector2 position) => new(
        Mathf.Clamp(position.X, 0.025f, 0.975f), Mathf.Clamp(position.Y, 0.035f, 0.965f));

    private void SyncLineups(bool reset)
    {
        if (Simulation is null) return;
        var valid = new HashSet<StringName>();
        SyncTeam(Simulation.home, false, valid, reset);
        SyncTeam(Simulation.away, true, valid, reset);
        foreach (StringName playerId in CurrentPositions.Keys.Where(id => !valid.Contains(id)).ToArray())
        {
            BasePositions.Remove(playerId); CurrentPositions.Remove(playerId); TargetPositions.Remove(playerId);
            _playerTeams.Remove(playerId); _playerRoles.Remove(playerId); _playerSlotIds.Remove(playerId);
        }
    }

    private void SyncTeam(MatchTeamState state, bool mirrored, HashSet<StringName> valid, bool reset)
    {
        foreach (Dictionary slot in state.formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!state.squad.starter_slots.TryGetValue(slotId, out Variant value)) continue;
            StringName playerId = value.AsStringName();
            Vector2 basePosition = new(slot["y"].AsSingle(), slot["x"].AsSingle());
            if (mirrored) basePosition = new Vector2(1 - basePosition.X, 1 - basePosition.Y);
            valid.Add(playerId); BasePositions[playerId] = basePosition; TargetPositions[playerId] = basePosition;
            _playerTeams[playerId] = state.team.id; _playerRoles[playerId] = slot["role"].AsString(); _playerSlotIds[playerId] = slotId;
            if (reset || !CurrentPositions.ContainsKey(playerId))
            {
                Vector2 previous = PositionForReplacedSlot(state.team.id, slotId);
                CurrentPositions[playerId] = previous == Vector2.Zero ? basePosition : previous;
            }
        }
    }

    private Vector2 PositionForReplacedSlot(StringName teamId, StringName slotId)
    {
        foreach (StringName oldId in CurrentPositions.Keys)
            if (_playerTeams.GetValueOrDefault(oldId) == teamId && _playerSlotIds.GetValueOrDefault(oldId) == slotId)
                return CurrentPositions[oldId];
        return Vector2.Zero;
    }

    private static float RoleSpeed(string role) => role switch
    {
        "GK" => 0.72f, "CB" => 0.86f, "LB" or "RB" => 1.08f,
        "DM" or "CM" or "AM" => 1.02f, _ => 1.14f
    };
}
