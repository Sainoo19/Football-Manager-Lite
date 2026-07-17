using System.Collections.Generic;
using Godot;

public partial class MatchPitch2D
{
    public bool StartScenario(MatchScenarioKind kind)
    {
        if (Simulation is null || CurrentPositions.Count != 22)
        {
            return false;
        }

        StringName attackingTeamId = Simulation.home.team.id;
        StringName defendingTeamId = Simulation.away.team.id;
        MatchScenarioDefinition definition = _matchScenarioFactory.Create(
            kind,
            AttackDirection(attackingTeamId));
        ResetForScenario(kind, attackingTeamId);

        HashSet<StringName> selectedPlayers = new();
        StringName ballCarrierId = SelectScenarioPlayer(
            attackingTeamId,
            selectedPlayers,
            BallCarrierRolePreferences(kind));
        if (ballCarrierId == new StringName())
        {
            return false;
        }
        selectedPlayers.Add(ballCarrierId);

        List<StringName> supportingAttackers = new();
        for (int index = 0; index < definition.SupportingAttackerPositions.Count; index++)
        {
            StringName playerId = SelectScenarioPlayer(
                attackingTeamId,
                selectedPlayers,
                SupportingRolePreferences(kind, index));
            if (playerId == new StringName())
            {
                return false;
            }
            selectedPlayers.Add(playerId);
            supportingAttackers.Add(playerId);
        }

        List<StringName> defenders = new();
        for (int index = 0; index < definition.DefenderPositions.Count; index++)
        {
            StringName playerId = SelectScenarioPlayer(
                defendingTeamId,
                selectedPlayers,
                new[] { "CB", "LB", "RB", "DM" });
            if (playerId == new StringName())
            {
                return false;
            }
            selectedPlayers.Add(playerId);
            defenders.Add(playerId);
        }

        StageNonParticipants(attackingTeamId, selectedPlayers);
        PlaceScenarioPlayer(ballCarrierId, definition.BallCarrierPosition);
        for (int index = 0; index < supportingAttackers.Count; index++)
        {
            PlaceScenarioPlayer(supportingAttackers[index], definition.SupportingAttackerPositions[index]);
        }
        for (int index = 0; index < defenders.Count; index++)
        {
            PlaceScenarioPlayer(defenders[index], definition.DefenderPositions[index]);
        }
        PlaceScenarioGoalkeepers(attackingTeamId, defendingTeamId);

        _ballOwnerId = ballCarrierId;
        BallPosition = definition.BallCarrierPosition;
        _activeTeamId = attackingTeamId;
        Simulation.set_live_possession(attackingTeamId);
        _attackProgress = AttackProgress(attackingTeamId, BallPosition);
        _phaseLane = BallPosition.Y;
        SelectPhasePlayers();

        if (definition.StartsWithThroughBall && supportingAttackers.Count > 0)
        {
            StringName receiverId = supportingAttackers[0];
            Vector2 receptionTarget = definition.ThroughBallReceptionTarget ??
                                      definition.SupportingAttackerPositions[0];
            TargetPositions[receiverId] = receptionTarget;
            StartPass(receiverId, BallActionKind.ThroughBall);
        }
        else
        {
            _nextDecisionTime = _visualTime + 0.35f;
            SetAction($"Bắt đầu test: {definition.DisplayName}");
        }

        QueueRedraw();
        return true;
    }

    private void ResetForScenario(MatchScenarioKind kind, StringName attackingTeamId)
    {
        SetPlaying(false);
        ActiveScenario = kind;
        ClearDirectAttack();
        _movementController.Reset();
        _playerIntents.Clear();
        _interceptionAttemptedBy.Clear();
        _ballActionActive = false;
        _ballActionKind = BallActionKind.None;
        _ballNextOwnerId = new StringName();
        _pendingOffsideReceiverId = new StringName();
        _pendingShotOutcome = new StringName();
        _looseBallActive = false;
        _looseBallVelocityMetersPerSecond = Vector2.Zero;
        _restartPending = false;
        _restartType = new StringName();
        _activeTeamId = attackingTeamId;
        _nextIntentPlanTime = 0f;
    }

    private void StageNonParticipants(
        StringName attackingTeamId,
        IReadOnlySet<StringName> selectedPlayers)
    {
        int attackingIndex = 0;
        int defendingIndex = 0;
        float direction = AttackDirection(attackingTeamId);
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (selectedPlayers.Contains(playerId) || _playerRoles[playerId] == "GK")
            {
                continue;
            }

            bool isAttacker = _playerTeams[playerId] == attackingTeamId;
            int index = isAttacker ? attackingIndex++ : defendingIndex++;
            float canonicalX = isAttacker ? 0.30f + index * 0.006f : 0.28f + index * 0.006f;
            float x = direction > 0f ? canonicalX : 1f - canonicalX;
            float y = Mathf.Clamp(BasePositions[playerId].Y, 0.08f, 0.92f);
            PlaceScenarioPlayer(playerId, new Vector2(x, y));
        }
    }

    private void PlaceScenarioGoalkeepers(StringName attackingTeamId, StringName defendingTeamId)
    {
        StringName attackingGoalkeeperId = ChooseGoalkeeper(attackingTeamId);
        StringName defendingGoalkeeperId = ChooseGoalkeeper(defendingTeamId);
        float direction = AttackDirection(attackingTeamId);
        PlaceScenarioPlayer(
            attackingGoalkeeperId,
            direction > 0f ? new Vector2(0.04f, 0.50f) : new Vector2(0.96f, 0.50f));
        PlaceScenarioPlayer(
            defendingGoalkeeperId,
            direction > 0f ? new Vector2(0.96f, 0.50f) : new Vector2(0.04f, 0.50f));
    }

    private void PlaceScenarioPlayer(StringName playerId, Vector2 position)
    {
        CurrentPositions[playerId] = position;
        TargetPositions[playerId] = position;
    }

    private StringName SelectScenarioPlayer(
        StringName teamId,
        IReadOnlySet<StringName> excluded,
        IReadOnlyList<string> preferredRoles)
    {
        foreach (string role in preferredRoles)
        {
            foreach (StringName playerId in CurrentPositions.Keys)
            {
                if (_playerTeams[playerId] == teamId &&
                    _playerRoles[playerId] == role &&
                    !excluded.Contains(playerId))
                {
                    return playerId;
                }
            }
        }

        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] == teamId &&
                _playerRoles[playerId] != "GK" &&
                !excluded.Contains(playerId))
            {
                return playerId;
            }
        }
        return new StringName();
    }

    private static string[] BallCarrierRolePreferences(MatchScenarioKind kind) => kind switch
    {
        MatchScenarioKind.ThroughBallBreakaway => new[] { "AM", "CM", "DM" },
        MatchScenarioKind.TwoAttackersVersusOneDefender => new[] { "ST", "AM", "RW", "LW" },
        _ => new[] { "AM", "CM", "ST" }
    };

    private static string[] SupportingRolePreferences(MatchScenarioKind kind, int index) => kind switch
    {
        MatchScenarioKind.ThroughBallBreakaway => new[] { "ST", "LW", "RW" },
        MatchScenarioKind.TwoAttackersVersusOneDefender => new[] { "LW", "RW", "ST", "AM" },
        _ when index == 0 => new[] { "ST", "LW", "RW" },
        _ => new[] { "RW", "LW", "ST", "AM" }
    };
}
