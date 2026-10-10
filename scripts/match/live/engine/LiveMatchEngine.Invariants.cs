using System;
using Godot;

// Read-only view of internal engine state for invariant checks. Nothing here changes how a match is played.
internal readonly record struct LiveMatchInvariantView(
    double SimulationSeconds,
    LiveMatchPhase Phase,
    StringName BallOwnerId,
    StringName ExpectedReceiverId,
    bool IsBallInFlight,
    bool IsShotInFlight,
    bool IsLooseBall,
    bool IsRestartPending,
    StringName RestartType,
    StringName RestartTeamId,
    StringName RestartTakerId,
    Vector2 RestartPosition,
    bool IsQuickFreeKick,
    StringName DuelCarrierId,
    StringName DuelDefenderId,
    StringName ShotShooterId,
    StringName ShotGoalkeeperId,
    bool IsLineupSyncPending,
    int OffsidesFromExemptRestarts);

public sealed partial class LiveMatchEngine
{
    // True from an offside-exempt restart (throw-in, corner, goal kick) until the next deliberate ball action.
    private bool _releaseExemptFromOffside;

    internal event Action? SimulationStepCompleted;

    public int OffsidesFromExemptRestarts { get; private set; }

    internal LiveMatchInvariantView CreateInvariantView()
    {
        bool shotInFlight = _ballActionActive && _ballActionKind == BallActionKind.Shot;
        return new LiveMatchInvariantView(
            _simulationTimeSeconds,
            _runtime.Phase,
            _state.BallOwnerId,
            _ballActionActive ? _ballNextOwnerId : new StringName(),
            _ballActionActive,
            shotInFlight,
            _state.IsLooseBallActive,
            _state.IsRestartPending,
            _state.IsRestartPending ? _state.RestartType : new StringName(),
            _state.IsRestartPending ? _state.RestartTeamId : new StringName(),
            _state.IsRestartPending ? _state.RestartTakerId : new StringName(),
            _state.RestartPosition,
            IsQuickFreeKick,
            _state.GroundDuel.CarrierId,
            _state.GroundDuel.DefenderId,
            shotInFlight ? _pendingShotShooterId : new StringName(),
            shotInFlight ? _pendingShotGoalkeeperId : new StringName(),
            _lineupSyncPending,
            OffsidesFromExemptRestarts);
    }

    internal float OwnGoalXOf(StringName teamId) => OwnGoalX(teamId);
}
