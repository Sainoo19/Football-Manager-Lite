public enum LiveMatchCommandKind
{
    Play,
    Pause,
    StartScenario
}

public readonly struct LiveMatchCommand
{
    public LiveMatchCommand(LiveMatchCommandKind kind, MatchScenarioKind? scenario = null)
    {
        Kind = kind;
        Scenario = scenario;
    }

    public LiveMatchCommandKind Kind { get; }
    public MatchScenarioKind? Scenario { get; }
}
