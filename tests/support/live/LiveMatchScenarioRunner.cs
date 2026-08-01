using System;

public sealed class LiveMatchScenarioRunner
{
    public LiveMatchSnapshot RunFor(
        LiveMatchEngine engine,
        double gameSeconds,
        double stepSeconds = 0.10d)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (gameSeconds < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(gameSeconds));
        }
        if (stepSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(stepSeconds));
        }

        double remainingSeconds = gameSeconds;
        while (remainingSeconds > 0.0000001d)
        {
            double step = Math.Min(stepSeconds, remainingSeconds);
            engine.AdvanceGameTime(step);
            remainingSeconds -= step;
        }
        return engine.GetSnapshot();
    }
}
