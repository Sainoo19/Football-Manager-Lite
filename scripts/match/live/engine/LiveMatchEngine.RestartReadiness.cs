using System.Collections.Generic;
using Godot;

public sealed partial class LiveMatchEngine
{
    private bool IsRestartReady()
    {
        if (!_state.IsRestartBallPlaced ||
            !CurrentPositions.TryGetValue(_state.RestartTakerId, out Vector2 takerPosition))
        {
            return false;
        }
        string type = _state.RestartType.ToString();
        if (type == "penalty")
        {
            float goalX = AttackingGoalX(_state.RestartTeamId);
            StringName goalkeeperId = ChooseGoalkeeper(OpposingTeam(_state.RestartTeamId));
            if (FootballPitchDimensions.DistanceMeters(takerPosition,
                    _penaltyRestartPlanner.PositionTaker(_state.RestartPosition, goalX)) > 0.8f ||
                FootballPitchDimensions.DistanceMeters(CurrentPositions[goalkeeperId],
                    PlayerPitchBoundary.Clamp(_penaltyRestartPlanner.PositionGoalkeeper(goalX))) > 0.5f)
            {
                return false;
            }
            foreach (StringName playerId in CurrentPositions.Keys)
            {
                if (playerId == _state.RestartTakerId || playerId == goalkeeperId)
                {
                    continue;
                }
                if (_penaltyAreaRule.IsInsideDefendingPenaltyArea(CurrentPositions[playerId], goalX) ||
                    FootballPitchDimensions.DistanceMeters(CurrentPositions[playerId], _state.RestartPosition) < 9.14f)
                {
                    return false;
                }
            }
            return true;
        }
        if (type == "throw_in" && FindThrowInReceiver() == new StringName())
        {
            return false;
        }
        if (!_touchlineRestartPlanner.IsTakerReady(takerPosition, _state.RestartPosition))
        {
            return false;
        }
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] == _state.RestartTeamId)
            {
                continue;
            }
            if (type == "goal_kick" && IsInsideOwnPenaltyArea(CurrentPositions[playerId], _state.RestartTeamId))
            {
                return false;
            }
            float requiredDistance = type switch
            {
                "corner" => TouchlineRestartPlanner.CornerDefenderDistanceMeters,
                "throw_in" => TouchlineRestartPlanner.ThrowInDefenderDistanceMeters,
                "free_kick" when !_freeKickRestartPlan.IsQuick => 9.15f,
                _ => 0f
            };
            if (FootballPitchDimensions.DistanceMeters(CurrentPositions[playerId], _state.RestartPosition) <
                requiredDistance - 0.02f)
            {
                return false;
            }
        }
        return true;
    }

    private void ApplyTouchlineRestartTargets()
    {
        PlanPlayerIntents(false);
        bool isCorner = _state.RestartType == "corner";
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            Vector2 target = _playerIntents.TryGetValue(playerId, out PlayerIntent? intent)
                ? intent.Target
                : CurrentPositions[playerId];
            if (playerId == _state.RestartTakerId)
            {
                target = _state.RestartPosition;
            }
            else if (_playerTeams[playerId] != _state.RestartTeamId)
            {
                target = _touchlineRestartPlanner.KeepDefenderAway(target, _state.RestartPosition, isCorner);
            }
            else if (!isCorner && FootballPitchDimensions.DistanceMeters(target, _state.RestartPosition) > 22f &&
                     _playerRoles[playerId] is "LB" or "RB" or "CM" or "LW" or "RW")
            {
                target = SpaceEvaluator.ClampToPitch(_state.RestartPosition.Lerp(target, 0.25f));
            }
            TargetPositions[playerId] = target;
        }

        // Defenders whose position overlaps another player's are moved further from the ball, in a stable order.
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            if (playerId == _state.RestartTakerId || _playerTeams[playerId] == _state.RestartTeamId)
            {
                continue;
            }
            List<Vector2> occupied = new(TargetPositions.Count);
            foreach ((StringName otherId, Vector2 otherTarget) in TargetPositions)
            {
                if (otherId != playerId)
                {
                    occupied.Add(otherTarget);
                }
            }
            TargetPositions[playerId] = _touchlineRestartPlanner.SeparateDefenderPosition(
                TargetPositions[playerId], _state.RestartPosition, occupied);
        }

        foreach (StringName playerId in CurrentPositions.Keys)
        {
            _playerIntents[playerId] = new PlayerIntent(
                PlayerIntentKind.RepositionForRestart, TargetPositions[playerId], LiveTeamPhase.SetPiece);
        }
    }

    private StringName FindThrowInReceiver()
    {
        StringName takerId = _state.RestartTakerId;
        StringName receiverId = new();
        float bestScore = float.NegativeInfinity;
        foreach (StringName playerId in OrderedPlayerIds(CurrentPositions.Keys))
        {
            if (playerId == takerId || _playerTeams[playerId] != _state.RestartTeamId || _playerRoles[playerId] == "GK")
            {
                continue;
            }
            float opponentDistance = SpaceEvaluator.NearestOpponentDistanceMeters(
                CurrentPositions[playerId], _state.RestartTeamId, CurrentPositions, _playerTeams);
            float score = _touchlineRestartPlanner.ThrowReceptionScore(
                _state.RestartPosition, CurrentPositions[playerId], opponentDistance);
            if (score > bestScore)
            {
                receiverId = playerId;
                bestScore = score;
            }
        }
        return receiverId;
    }

    private void ExecuteThrowIn()
    {
        StringName takerId = _state.RestartTakerId;
        StringName receiverId = FindThrowInReceiver();
        _state.BallOwnerId = takerId;
        BallPosition = _state.RestartPosition;
        float distance = FootballPitchDimensions.DistanceMeters(BallPosition, CurrentPositions[receiverId]);
        StartBallAction(CurrentPositions[receiverId], distance / 12f, 0.015f, receiverId, BallActionKind.ThrowIn);
        _pendingPassSpeedMetersPerSecond = 12f;
        _pendingPassType = LivePassType.Standard;
        SetAction($"{PlayerName(takerId)} ném biên cho {PlayerName(receiverId)}");
    }
}
