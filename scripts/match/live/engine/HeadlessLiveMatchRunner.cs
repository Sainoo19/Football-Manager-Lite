using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed record LiveAerialRecord(int Arrivals, int ContestedDuels, int ControlledReceptions, int HeaderTouches = 0);

public sealed record LiveFoulRecord(
    double GameSeconds,
    string OffenderRole,
    string EngagementType,
    bool FromBehind,
    bool AwardsPenalty,
    bool PlaysAdvantage,
    float DistanceToOwnGoalMeters,
    float OffenderSpeedMetersPerSecond,
    float VictimSpeedMetersPerSecond);

public sealed class HeadlessLiveMatchResult
{
    public HeadlessLiveMatchResult(FootballMatchSimulation simulation, LiveMatchSnapshot finalSnapshot,
        IReadOnlyList<LiveShotRecord>? shots = null, LiveAerialRecord? aerial = null,
        IReadOnlyList<LiveFoulRecord>? fouls = null)
    {
        Simulation = simulation;
        FinalSnapshot = finalSnapshot;
        Aerial = aerial;
        Shots = new ReadOnlyCollection<LiveShotRecord>(
            shots is null ? new List<LiveShotRecord>() : new List<LiveShotRecord>(shots));
        Fouls = new ReadOnlyCollection<LiveFoulRecord>(
            fouls is null ? new List<LiveFoulRecord>() : new List<LiveFoulRecord>(fouls));
    }

    public FootballMatchSimulation Simulation { get; }
    public LiveMatchSnapshot FinalSnapshot { get; }
    public IReadOnlyList<LiveShotRecord> Shots { get; }
    public LiveAerialRecord? Aerial { get; }
    public IReadOnlyList<LiveFoulRecord> Fouls { get; }
}

public sealed class HeadlessLiveMatchRunner
{
    private const int MaximumSteps = 500_000;

    public HeadlessLiveMatchResult RunToFullTime(
        FootballMatchSimulation simulation,
        MatchPlaybackSpeed speed = MatchPlaybackSpeed.Fastest,
        double realStepSeconds = 0.05d,
        Action<LiveMatchEngine>? observe = null)
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
            runtime.Advance(realStepSeconds);
            engine.AdvanceSynchronizedGameTime(runtime.LastAdvancedGameSeconds);
            observe?.Invoke(engine);
            stepCount++;
        }

        if (!simulation.is_finished)
        {
            throw new InvalidOperationException("Headless live match exceeded the maximum step count.");
        }

        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Pause));
        return new HeadlessLiveMatchResult(simulation, engine.GetSnapshot(), engine.ShotRecords,
            new LiveAerialRecord(engine.AerialArrivals, engine.AerialDuels,
                engine.AerialControlledReceptions, engine.AerialHeaderTouches),
            engine.FoulRecords);
    }
}
