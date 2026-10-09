using Godot;

public sealed class MatchFlowDiagnostics
{
    private double _restartStartedAt;
    private double _stationaryStartedAt;
    private string _previousRestart = string.Empty;
    private int _previousRestartSerial;
    private bool _wasStationary;
    private Vector2 _previousBallPosition;

    public double MaximumRestartWaitSeconds { get; private set; }
    public double MaximumStationaryLooseBallSeconds { get; private set; }
    public float MaximumOwnerHoldSeconds { get; private set; }

    public void Observe(LiveMatchEngine engine)
    {
        double now = engine.ElapsedGameSeconds;
        string restart = engine.PendingRestartType.ToString();
        if (restart != _previousRestart || engine.Restarts != _previousRestartSerial)
        {
            _restartStartedAt = now;
            _previousRestart = restart;
            _previousRestartSerial = engine.Restarts;
        }
        if (restart.Length > 0)
        {
            MaximumRestartWaitSeconds = System.Math.Max(MaximumRestartWaitSeconds, now - _restartStartedAt);
        }
        bool stationary = engine.IsLooseBall && engine.LooseBallVelocityMetersPerSecond.Length() < 0.1f &&
                          FootballPitchDimensions.DistanceMeters(_previousBallPosition, engine.BallPosition) < 0.05f;
        if (!stationary || !_wasStationary)
        {
            _stationaryStartedAt = now;
        }
        if (stationary)
        {
            MaximumStationaryLooseBallSeconds = System.Math.Max(MaximumStationaryLooseBallSeconds,
                now - _stationaryStartedAt);
        }
        _wasStationary = stationary;
        _previousBallPosition = engine.BallPosition;
        MaximumOwnerHoldSeconds = engine.MaximumObservedOwnerHoldSeconds;
    }
}
