using System;

public enum MatchPlaybackSpeed
{
    RealTime,
    Fast,
    Faster,
    Fastest
}

public sealed class LiveMatchClock
{
    public const double MatchDurationSeconds = 90d * 60d;
    private const double OriginalFastMatchDurationSeconds = 90d * 0.28d;

    public double ElapsedGameSeconds { get; private set; }
    public bool IsRunning { get; private set; }
    public MatchPlaybackSpeed Speed { get; private set; } = MatchPlaybackSpeed.RealTime;

    public double GameSecondsPerRealSecond => Speed switch
    {
        MatchPlaybackSpeed.RealTime => 1d,
        MatchPlaybackSpeed.Fast => MatchDurationSeconds / OriginalFastMatchDurationSeconds,
        MatchPlaybackSpeed.Faster => MatchDurationSeconds / (OriginalFastMatchDurationSeconds / 4d),
        MatchPlaybackSpeed.Fastest => MatchDurationSeconds / (OriginalFastMatchDurationSeconds / 8d),
        _ => 1d
    };

    public string DisplayTime
    {
        get
        {
            int totalSeconds = Math.Min((int)Math.Floor(ElapsedGameSeconds), (int)MatchDurationSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }

    public void Reset()
    {
        ElapsedGameSeconds = 0d;
        IsRunning = false;
    }

    public void Start() => IsRunning = ElapsedGameSeconds < MatchDurationSeconds;

    public void Pause() => IsRunning = false;

    public void SetSpeed(MatchPlaybackSpeed speed) => Speed = speed;

    public int Advance(double realDeltaSeconds)
    {
        if (!IsRunning || realDeltaSeconds <= 0d)
        {
            return 0;
        }

        int previousMinute = (int)Math.Floor(ElapsedGameSeconds / 60d);
        ElapsedGameSeconds = Math.Min(
            ElapsedGameSeconds + realDeltaSeconds * GameSecondsPerRealSecond,
            MatchDurationSeconds);
        int currentMinute = (int)Math.Floor(ElapsedGameSeconds / 60d);
        if (ElapsedGameSeconds >= MatchDurationSeconds)
        {
            IsRunning = false;
        }

        return currentMinute - previousMinute;
    }
}
