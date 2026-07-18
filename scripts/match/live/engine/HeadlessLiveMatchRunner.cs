using System;

public sealed class HeadlessLiveMatchResult
{
    public HeadlessLiveMatchResult(FootballMatchSimulation simulation, LiveMatchSnapshot finalSnapshot)
    {
        Simulation = simulation;
        FinalSnapshot = finalSnapshot;
    }

    public FootballMatchSimulation Simulation { get; }
    public LiveMatchSnapshot FinalSnapshot { get; }
}

public sealed class HeadlessLiveMatchRunner
{
    private const int MaximumSteps = 500_000;

    public HeadlessLiveMatchResult RunToFullTime(
        FootballMatchSimulation simulation,
        MatchPlaybackSpeed speed = MatchPlaybackSpeed.Fastest,
        double realStepSeconds = 0.05d)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (simulation.home is null || simulation.away is null)
        {
            throw new InvalidOperationException("The match simulation must be set up before running live.");
        }
        if (simulation.is_finished)
        {
            throw new InvalidOperationException("A finished match cannot be started again.");
        }
        if (realStepSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(realStepSeconds));
        }

        simulation.use_live_pitch_events = true;
        LiveMatchRuntime runtime = new();
        runtime.SetSpeed(speed);
        LiveMatchEngine engine = new();
        engine.AttachRuntime(runtime);
        engine.SetMatch(simulation);
        runtime.Start();
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));

        int stepCount = 0;
        while (!simulation.is_finished && stepCount < MaximumSteps)
        {
            int elapsedMinutes = runtime.Advance(realStepSeconds);
            engine.AdvanceGameTime(runtime.LastAdvancedGameSeconds);
            for (int minute = 0; minute < elapsedMinutes && !simulation.is_finished; minute++)
            {
                engine.AnimateMinute(simulation.advance_minute());
            }
            stepCount++;
        }

        if (!simulation.is_finished)
        {
            throw new InvalidOperationException("Headless live match exceeded the maximum step count.");
        }

        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Pause));
        return new HeadlessLiveMatchResult(simulation, engine.GetSnapshot());
    }
}
