using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public readonly struct LiveGoalRecord
{
    public LiveGoalRecord(float distanceMeters, StringName situation)
    {
        DistanceMeters = distanceMeters;
        Situation = situation;
    }

    public float DistanceMeters { get; }
    public StringName Situation { get; }
}

public sealed class LiveMatchAnalyticsSnapshot
{
    public LiveMatchAnalyticsSnapshot(
        int possessionChanges,
        int possessionSpellCount,
        float totalPossessionSpellSeconds,
        int kickoffs,
        int corners,
        int goalKicks,
        int throwIns,
        int freeKicks,
        int penalties,
        IReadOnlyList<LiveGoalRecord> goals)
    {
        PossessionChanges = possessionChanges;
        PossessionSpellCount = possessionSpellCount;
        TotalPossessionSpellSeconds = totalPossessionSpellSeconds;
        Kickoffs = kickoffs;
        Corners = corners;
        GoalKicks = goalKicks;
        ThrowIns = throwIns;
        FreeKicks = freeKicks;
        Penalties = penalties;
        Goals = new ReadOnlyCollection<LiveGoalRecord>(new List<LiveGoalRecord>(goals));
    }

    public int PossessionChanges { get; }
    public int PossessionSpellCount { get; }
    public float TotalPossessionSpellSeconds { get; }
    public float AveragePossessionSpellSeconds => PossessionSpellCount == 0
        ? 0f
        : TotalPossessionSpellSeconds / PossessionSpellCount;
    public int Kickoffs { get; }
    public int Corners { get; }
    public int GoalKicks { get; }
    public int ThrowIns { get; }
    public int FreeKicks { get; }
    public int Penalties { get; }
    public IReadOnlyList<LiveGoalRecord> Goals { get; }
}
