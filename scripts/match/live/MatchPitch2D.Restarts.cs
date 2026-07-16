using System.Linq;
using Godot;

public partial class MatchPitch2D
{
    private void CompleteLiveShot()
    {
        if (Simulation is null || _pendingShotOutcome == new StringName())
            return;
        FootballMatchEvent? matchEvent = Simulation.register_live_shot(
            _actionSourceTeamId,
            _pendingShotShooterId,
            _pendingShotOutcome,
            _pendingShotGoalkeeperId,
            _pendingShotBlockerId);
        if (matchEvent is not null)
            EmitSignal(SignalName.LiveMatchEvent, matchEvent);

        string outcome = _pendingShotOutcome.ToString();
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
            ScheduleRestart("goal_kick", defendingTeamId, new Vector2(_actionSourceTeamId == Simulation.home.team.id ? 0.055f : 0.945f, 0.5f));
        else if (outcome is "parried_corner" or "blocked_corner")
            ScheduleRestart("corner", _actionSourceTeamId, BallPosition);
        else if (outcome is "parried" or "blocked")
            StartLooseBall();
        else
            GivePossessionTo(_pendingShotGoalkeeperId, 0.12f);

        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
    }

    private void StartLooseBall()
    {
        _ballOwnerId = new StringName();
        _looseBallActive = true;
        _restartPending = false;
        _looseBallResolveTime = _visualTime + 0.30f;
        SetAction("Bóng bật ra — hai đội tranh bóng hai");
    }

    private void ResolveLooseBall()
    {
        if (Simulation is null)
            return;
        _looseBallActive = false;
        var candidates = CurrentPositions.Keys
            .Select(id => new
            {
                Id = id,
                Score = CurrentPositions[id].DistanceTo(BallPosition) /
                        Mathf.Lerp(0.78f, 1.18f, (GetPlayer(id)?.pace ?? 50) / 99f) -
                        (GetPlayer(id)?.positioning ?? 50) / 9000f
            })
            .OrderBy(candidate => candidate.Score)
            .ToList();
        if (candidates.Count == 0)
            return;
        StringName winnerId = candidates[0].Id;
        LooseBallRecoveries++;
        GivePossessionTo(winnerId, 0.26f);
        SetAction($"{PlayerName(winnerId)} giành được bóng hai");
    }

    private void GivePossessionTo(StringName playerId, float decisionDelay)
    {
        if (Simulation is null || playerId == new StringName() || !CurrentPositions.ContainsKey(playerId))
            return;
        _ballOwnerId = playerId;
        _activeTeamId = _playerTeams[playerId];
        Simulation.set_live_possession(_activeTeamId);
        bool homeTeam = _activeTeamId == Simulation.home.team.id;
        _attackProgress = Mathf.Clamp(homeTeam ? 1f - CurrentPositions[playerId].X : CurrentPositions[playerId].X, 0.10f, 0.72f);
        _phaseLane = CurrentPositions[playerId].Y;
        SelectPhasePlayers();
        _nextDecisionTime = _visualTime + decisionDelay;
    }

    private void ScheduleRestart(StringName restartType, StringName teamId, Vector2 position)
    {
        if (Simulation is null)
            return;
        _ballOwnerId = new StringName();
        _looseBallActive = false;
        _restartPending = true;
        _restartType = restartType;
        _restartTeamId = teamId;
        _activeTeamId = teamId;
        Simulation.set_live_possession(teamId);
        _restartPosition = ClampToPitch(position);
        BallPosition = _restartPosition;
        _restartExecuteTime = _visualTime + 0.46f;
        FootballMatchEvent? restartEvent = Simulation.register_live_restart(teamId, restartType);
        if (restartEvent is not null)
            EmitSignal(SignalName.LiveMatchEvent, restartEvent);
    }

    private void ExecuteRestart()
    {
        if (Simulation is null)
            return;
        _restartPending = false;
        Restarts++;
        _activeTeamId = _restartTeamId;
        Simulation.set_live_possession(_activeTeamId);
        SyncLineups(false);

        string type = _restartType.ToString();
        if (type == "corner")
        {
            _attackProgress = 0.91f;
            _phaseLane = _restartPosition.Y;
            SelectPhasePlayers();
            StringName taker = ChooseNearestPlayer(_restartTeamId, _restartPosition, true);
            _ballOwnerId = taker;
            BallPosition = _restartPosition;
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChooseOwner(_restartTeamId, true);
            bool homeAttack = _restartTeamId == Simulation.home.team.id;
            StartBallAction(new Vector2(homeAttack ? 0.13f : 0.87f, 0.5f), 0.68f, 0.06f, receiver, BallActionKind.Cross);
            SetAction($"{PlayerName(taker)} thực hiện phạt góc");
            return;
        }

        StringName ownerId = type == "goal_kick"
            ? ChooseGoalkeeper(_restartTeamId)
            : ChooseNearestPlayer(_restartTeamId, _restartPosition, false);
        BallPosition = _restartPosition;
        GivePossessionTo(ownerId, type == "free_kick" ? 0.24f : 0.38f);
        _attackProgress = type == "kickoff" ? 0.20f : type == "goal_kick" ? 0.08f : _attackProgress;
        SetAction(type switch
        {
            "goal_kick" => $"{PlayerName(ownerId)} chuẩn bị phát bóng",
            "free_kick" => $"{PlayerName(ownerId)} thực hiện đá phạt",
            "throw_in" => $"{PlayerName(ownerId)} chuẩn bị ném biên",
            _ => $"{PlayerName(ownerId)} giao bóng"
        });
    }

    private StringName ChooseNearestPlayer(StringName teamId, Vector2 position, bool preferWide)
    {
        var candidates = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == teamId && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(position) -
                           (preferWide && _playerRoles[id] is "LB" or "RB" or "LW" or "RW" ? 0.08f : 0))
            .ToList();
        return candidates.Count > 0 ? candidates[0] : ChooseOwner(teamId, false);
    }
}
