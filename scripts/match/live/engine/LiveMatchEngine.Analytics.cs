using Godot;

public sealed partial class LiveMatchEngine
{
    private void ResetLiveAnalytics()
    {
        _possessionTeamId = new StringName();
        _lastCompletedPossessionTeamId = new StringName();
        _possessionSpellStartTime = 0f;
        _totalPossessionSpellSeconds = 0f;
        _possessionSpellCount = 0;
        PossessionChanges = 0;
        KickoffsTaken = 0;
        CornersTaken = 0;
        GoalKicksTaken = 0;
        ThrowInsTaken = 0;
        FreeKicksTaken = 0;
        PenaltiesTaken = 0;
        _goalRecords.Clear();
    }

    private void SetTrackedPossession(StringName teamId)
    {
        if (teamId == new StringName() || Simulation?.get_state(teamId) is null)
        {
            return;
        }

        if (_possessionTeamId == new StringName())
        {
            if (_lastCompletedPossessionTeamId != new StringName() &&
                _lastCompletedPossessionTeamId != teamId)
            {
                PossessionChanges++;
            }
            _possessionTeamId = teamId;
            _possessionSpellStartTime = _state.VisualTime;
        }
        else if (_possessionTeamId != teamId)
        {
            CompletePossessionSpell();
            PossessionChanges++;
            _possessionTeamId = teamId;
            _possessionSpellStartTime = _state.VisualTime;
        }

        _state.ActiveTeamId = teamId;
        Simulation.set_live_possession(teamId);
    }

    private void CompletePossessionSpell()
    {
        if (_possessionTeamId == new StringName())
        {
            return;
        }
        float duration = Mathf.Max(_state.VisualTime - _possessionSpellStartTime, 0f);
        if (duration > 0.001f)
        {
            _totalPossessionSpellSeconds += duration;
            _possessionSpellCount++;
        }
        _lastCompletedPossessionTeamId = _possessionTeamId;
        _possessionTeamId = new StringName();
    }

    private void SuspendTrackedPossession()
    {
        CompletePossessionSpell();
    }

    private void RecordRestartTaken(StringName restartType)
    {
        switch (restartType.ToString())
        {
            case "kickoff":
                KickoffsTaken++;
                break;
            case "corner":
                CornersTaken++;
                break;
            case "goal_kick":
                GoalKicksTaken++;
                break;
            case "throw_in":
                ThrowInsTaken++;
                break;
            case "free_kick":
                FreeKicksTaken++;
                break;
            case "penalty":
                PenaltiesTaken++;
                break;
        }
    }

    private void RecordGoal(float distanceMeters, StringName situation)
    {
        _goalRecords.Add(new LiveGoalRecord(distanceMeters, situation));
    }

    private LiveMatchAnalyticsSnapshot CreateAnalyticsSnapshot()
    {
        float totalDuration = _totalPossessionSpellSeconds;
        int spellCount = _possessionSpellCount;
        if (_possessionTeamId != new StringName() && !Simulation!.is_finished)
        {
            totalDuration += Mathf.Max(_state.VisualTime - _possessionSpellStartTime, 0f);
            spellCount++;
        }
        return new LiveMatchAnalyticsSnapshot(
            PossessionChanges,
            spellCount,
            totalDuration,
            KickoffsTaken,
            CornersTaken,
            GoalKicksTaken,
            ThrowInsTaken,
            FreeKicksTaken,
            PenaltiesTaken,
            _goalRecords);
    }
}
