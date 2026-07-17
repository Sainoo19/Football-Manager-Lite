using System;

public enum LiveMatchPhase
{
    AwaitingKickoff,
    InPossession,
    BallInFlight,
    LooseBall,
    Restart,
    HalfTime,
    FullTime
}

public sealed class LiveMatchRuntime
{
    private readonly LiveMatchClock _clock = new();

    public double ElapsedGameSeconds => _clock.ElapsedGameSeconds;
    public double LastAdvancedGameSeconds { get; private set; }
    public bool IsRunning => _clock.IsRunning;
    public MatchPlaybackSpeed Speed => _clock.Speed;
    public double GameSecondsPerRealSecond => _clock.GameSecondsPerRealSecond;
    public string DisplayTime => _clock.DisplayTime;
    public LiveMatchPhase Phase { get; private set; } = LiveMatchPhase.AwaitingKickoff;

    public void Reset()
    {
        _clock.Reset();
        LastAdvancedGameSeconds = 0d;
        Phase = LiveMatchPhase.AwaitingKickoff;
    }

    public void Start()
    {
        _clock.Start();
    }

    public void Pause()
    {
        _clock.Pause();
        LastAdvancedGameSeconds = 0d;
    }

    public void SetSpeed(MatchPlaybackSpeed speed)
    {
        _clock.SetSpeed(speed);
    }

    public void SetPhase(LiveMatchPhase phase)
    {
        Phase = phase;
        if (phase == LiveMatchPhase.FullTime)
        {
            Pause();
        }
    }

    public int Advance(double realDeltaSeconds)
    {
        double previousGameSeconds = _clock.ElapsedGameSeconds;
        int elapsedMinutes = _clock.Advance(realDeltaSeconds);
        LastAdvancedGameSeconds = Math.Max(_clock.ElapsedGameSeconds - previousGameSeconds, 0d);
        return elapsedMinutes;
    }
}
