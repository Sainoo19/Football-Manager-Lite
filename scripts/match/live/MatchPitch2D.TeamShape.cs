using System.Collections.Generic;
using Godot;
using Godot.Collections;

public partial class MatchPitch2D
{
    private const float PossessionIntentPlanningInterval = 0.18f;
    private const float BallInFlightPlanningInterval = 0.12f;
    private const float LooseBallPlanningInterval = 0.08f;

    private void UpdatePlayerTargets()
    {
        if (Simulation is null)
        {
            return;
        }

        PlanPlayerIntents(false);
        foreach ((StringName playerId, PlayerIntent intent) in _playerIntents)
        {
            TargetPositions[playerId] = intent.Kind switch
            {
                PlayerIntentKind.PressBall or PlayerIntentKind.ChaseLooseBall => BallPosition,
                PlayerIntentKind.ReceivePass when _ballActionActive => _ballActionTo,
                _ => intent.Target
            };
        }
    }

    private void PlanPlayerIntents(bool force)
    {
        if (Simulation is null || CurrentPositions.Count == 0)
        {
            return;
        }

        if (!force && _playerIntents.Count == CurrentPositions.Count && _visualTime < _nextIntentPlanTime)
        {
            return;
        }

        _attackProgress = AttackProgress(_activeTeamId, BallPosition);
        _phaseLane = BallPosition.Y;

        FootballWorldSnapshot world = new(
            CurrentPositions,
            BasePositions,
            _playerTeams,
            _playerRoles,
            BallPosition,
            _ballActionTo,
            _ballOwnerId,
            _ballNextOwnerId,
            _activeTeamId,
            Simulation.home.team.id,
            _ballActionActive,
            _looseBallActive,
            _sideController.HomeAttacksLeft);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> planned = _intentPlanner.Plan(world);

        _playerIntents.Clear();
        _primaryRunnerId = new StringName();
        _secondaryRunnerId = new StringName();
        _pressingPlayerId = new StringName();
        foreach ((StringName playerId, PlayerIntent intent) in planned)
        {
            _playerIntents[playerId] = intent;
            TargetPositions[playerId] = intent.Target;

            if (_playerTeams[playerId] == _activeTeamId)
            {
                if (_primaryRunnerId == new StringName() &&
                    intent.Kind is PlayerIntentKind.RunIntoSpace or PlayerIntentKind.ReceivePass)
                {
                    _primaryRunnerId = playerId;
                }
                else if (_secondaryRunnerId == new StringName() && intent.Kind == PlayerIntentKind.SupportBall)
                {
                    _secondaryRunnerId = playerId;
                }
            }
            else if (_pressingPlayerId == new StringName() && intent.Kind == PlayerIntentKind.PressBall)
            {
                _pressingPlayerId = playerId;
            }
        }

        float planningInterval = _looseBallActive
            ? LooseBallPlanningInterval
            : _ballActionActive
                ? BallInFlightPlanningInterval
                : PossessionIntentPlanningInterval;
        _nextIntentPlanTime = _visualTime + planningInterval;
    }

    private void AdvancePhase(bool turnover, FootballMatchEvent? focusEvent)
    {
        if (Simulation is null)
        {
            return;
        }

        _phaseSerial++;
        _attackProgress = AttackProgress(_activeTeamId, BallPosition);
        _phaseLane = BallPosition.Y;

        string eventType = focusEvent?.event_type.ToString() ?? string.Empty;
        if (eventType is "goal" or "shot_on_target" or "shot_off_target")
        {
            _attackProgress = 0.96f;
        }
        else if (eventType == "corner")
        {
            _attackProgress = 0.91f;
        }
        else if (eventType is "half_time" or "full_time")
        {
            _attackProgress = 0.5f;
            _phaseLane = 0.5f;
        }

        if (turnover)
        {
            _nextIntentPlanTime = 0f;
        }
    }

    private void SelectPhasePlayers()
    {
        PlanPlayerIntents(true);
    }

    private static bool IsAttackingEvent(StringName eventType) =>
        eventType.ToString() is "goal" or "shot_on_target" or "shot_off_target" or "corner";

    private static Vector2 ClampToPitch(Vector2 position) => SpaceEvaluator.ClampToPitch(position);

    private void SyncLineups(bool reset)
    {
        if (Simulation is null)
        {
            return;
        }

        HashSet<StringName> valid = new();
        SyncTeam(
            Simulation.home,
            _sideController.ShouldMirrorFormation(Simulation.home.team.id, Simulation.home.team.id),
            valid,
            reset);
        SyncTeam(
            Simulation.away,
            _sideController.ShouldMirrorFormation(Simulation.away.team.id, Simulation.home.team.id),
            valid,
            reset);

        List<StringName> removedPlayers = new();
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (!valid.Contains(playerId))
            {
                removedPlayers.Add(playerId);
            }
        }

        foreach (StringName playerId in removedPlayers)
        {
            BasePositions.Remove(playerId);
            CurrentPositions.Remove(playerId);
            TargetPositions.Remove(playerId);
            _playerTeams.Remove(playerId);
            _playerRoles.Remove(playerId);
            _playerSlotIds.Remove(playerId);
            _playerPaces.Remove(playerId);
            _playerIntents.Remove(playerId);
            _movementController.RemovePlayer(playerId);
        }
    }

    private void SyncTeam(MatchTeamState state, bool mirrored, HashSet<StringName> valid, bool reset)
    {
        foreach (Dictionary slot in state.formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!state.squad.starter_slots.TryGetValue(slotId, out Variant value))
            {
                continue;
            }

            StringName playerId = value.AsStringName();
            Vector2 basePosition = new(slot["y"].AsSingle(), slot["x"].AsSingle());
            if (mirrored)
            {
                basePosition = new Vector2(1f - basePosition.X, 1f - basePosition.Y);
            }

            valid.Add(playerId);
            BasePositions[playerId] = basePosition;
            TargetPositions[playerId] = basePosition;
            _playerTeams[playerId] = state.team.id;
            _playerRoles[playerId] = slot["role"].AsString();
            _playerSlotIds[playerId] = slotId;
            _playerPaces[playerId] = state.team.get_player(playerId)?.pace ?? 50;
            _movementController.EnsurePlayer(playerId);

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
        {
            if (_playerTeams.GetValueOrDefault(oldId) == teamId &&
                _playerSlotIds.GetValueOrDefault(oldId) == slotId)
            {
                return CurrentPositions[oldId];
            }
        }

        return Vector2.Zero;
    }

}
