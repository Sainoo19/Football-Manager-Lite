using System.Collections.Generic;
using Godot;
using Godot.Collections;

public sealed partial class LiveMatchEngine
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

        if (_state.IsRestartPending && _state.RestartType == "goal_kick")
        {
            ApplyGoalKickRestartTargets();
            return;
        }
        if (_state.IsRestartPending && _state.RestartType == "free_kick")
        {
            ApplyFreeKickRestartTargets();
            return;
        }
        if (_state.IsRestartPending && _state.RestartType == "penalty")
        {
            ApplyPenaltyRestartTargets();
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
        ApplyGroundDuelTargets();
        ApplyAerialContestTargets();
    }

    private void PlanPlayerIntents(bool force)
    {
        if (Simulation is null || CurrentPositions.Count == 0)
        {
            return;
        }

        if (!force && _playerIntents.Count == CurrentPositions.Count && _state.VisualTime < _nextIntentPlanTime)
        {
            return;
        }

        _attackProgress = AttackProgress(_state.ActiveTeamId, BallPosition);
        _phaseLane = BallPosition.Y;

        FootballWorldSnapshot world = new(
            CurrentPositions,
            BasePositions,
            _playerTeams,
            _playerRoles,
            BallPosition,
            _ballActionTo,
            _state.BallOwnerId,
            _ballNextOwnerId,
            _state.ActiveTeamId,
            Simulation.home.team.id,
            _ballActionActive,
            _state.IsLooseBallActive,
            _sideController.HomeAttacksLeft,
            _ballActionActive && _ballActionKind == BallActionKind.Shot,
            _ballActionActive && _ballActionKind == BallActionKind.Cross);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> planned = _intentPlanner.Plan(world);

        _playerIntents.Clear();
        _primaryRunnerId = new StringName();
        _secondaryRunnerId = new StringName();
        _pressingPlayerId = new StringName();
        foreach ((StringName playerId, PlayerIntent intent) in planned)
        {
            _playerIntents[playerId] = intent;
            TargetPositions[playerId] = intent.Target;

            if (_playerTeams[playerId] == _state.ActiveTeamId)
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
        ApplyAerialContestTargets();

        float planningInterval = _state.IsLooseBallActive
            ? LooseBallPlanningInterval
            : _ballActionActive
                ? BallInFlightPlanningInterval
                : PossessionIntentPlanningInterval;
        _nextIntentPlanTime = _state.VisualTime + planningInterval;
    }

    private void AdvancePhase(bool turnover, FootballMatchEvent? focusEvent)
    {
        if (Simulation is null)
        {
            return;
        }

        _phaseSerial++;
        _attackProgress = AttackProgress(_state.ActiveTeamId, BallPosition);
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
        StringName homeTeamId = Simulation.home.team.id;
        SyncTeam(
            Simulation.home,
            homeTeamId,
            valid,
            reset);
        SyncTeam(
            Simulation.away,
            homeTeamId,
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
            _playerNumbers.Remove(playerId);
            _playerIntents.Remove(playerId);
            _movementController.RemovePlayer(playerId);
        }
    }

    private void SyncTeam(
        MatchTeamState state,
        StringName homeTeamId,
        HashSet<StringName> valid,
        bool reset)
    {
        foreach (Dictionary slot in state.formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!state.squad.starter_slots.TryGetValue(slotId, out Variant value))
            {
                continue;
            }

            StringName playerId = value.AsStringName();
            Vector2 basePosition = _sideController.FormationPosition(
                slot["x"].AsSingle(),
                slot["y"].AsSingle(),
                state.team.id,
                homeTeamId);

            valid.Add(playerId);
            BasePositions[playerId] = basePosition;
            TargetPositions[playerId] = basePosition;
            _playerTeams[playerId] = state.team.id;
            _playerRoles[playerId] = slot["role"].AsString();
            _playerSlotIds[playerId] = slotId;
            _playerPaces[playerId] = state.team.get_player(playerId)?.pace ?? 50;
            _playerNumbers[playerId] = SquadNumber(state.team, playerId);
            _movementController.EnsurePlayer(playerId);

            if (reset || !CurrentPositions.ContainsKey(playerId))
            {
                Vector2 previous = PositionForReplacedSlot(state.team.id, slotId);
                CurrentPositions[playerId] = previous == Vector2.Zero ? basePosition : previous;
            }
        }
    }

    private static int SquadNumber(FootballTeam team, StringName playerId)
    {
        FootballPlayer? player = team.get_player(playerId);
        if (player is not null && player.SquadNumber > 0)
        {
            return player.SquadNumber;
        }

        for (int index = 0; index < team.players.Count; index++)
        {
            if (team.players[index].id == playerId)
            {
                return index + 1;
            }
        }

        return 0;
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
