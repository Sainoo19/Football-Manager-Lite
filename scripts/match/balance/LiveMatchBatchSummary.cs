using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class LiveMatchBatchSummary
{
    private LiveMatchBatchSummary(
        int requestedMatchCount,
        IReadOnlyDictionary<string, double> metricAverages,
        IReadOnlyDictionary<string, int> goalsByDistance,
        IReadOnlyDictionary<string, int> goalsBySituation,
        int uniqueEventSequences,
        int completedMatchCount)
    {
        RequestedMatchCount = requestedMatchCount;
        CompletedMatchCount = completedMatchCount;
        MetricAverages = metricAverages;
        GoalsByDistance = goalsByDistance;
        GoalsBySituation = goalsBySituation;
        UniqueEventSequences = uniqueEventSequences;
    }

    public int RequestedMatchCount { get; }
    public int CompletedMatchCount { get; }
    public IReadOnlyDictionary<string, double> MetricAverages { get; }
    public IReadOnlyDictionary<string, int> GoalsByDistance { get; }
    public IReadOnlyDictionary<string, int> GoalsBySituation { get; }
    public int UniqueEventSequences { get; }
    public double UniqueEventSequenceRatio => CompletedMatchCount == 0
        ? 0d
        : (double)UniqueEventSequences / CompletedMatchCount;

    public static LiveMatchBatchSummary Create(
        IReadOnlyList<LiveMatchBalanceRecord> matches,
        int requestedMatchCount)
    {
        Dictionary<string, double> metricAverages = new();
        if (matches.Count > 0)
        {
            foreach (string key in matches[0].GetMetricValues().Keys)
            {
                metricAverages[key] = matches.Average(match => match.GetMetricValues()[key]);
            }
        }

        Dictionary<string, int> goalsByDistance = new()
        {
            { "0-10m", 0 },
            { "10-20m", 0 },
            { "20-30m", 0 },
            { "30m+", 0 }
        };
        Dictionary<string, int> goalsBySituation = new(StringComparer.Ordinal);
        foreach (BalanceGoalRecord goal in matches.SelectMany(match => match.GoalRecords))
        {
            string distanceBucket = goal.DistanceMeters < 10f
                ? "0-10m"
                : goal.DistanceMeters < 20f
                    ? "10-20m"
                    : goal.DistanceMeters < 30f
                        ? "20-30m"
                        : "30m+";
            goalsByDistance[distanceBucket]++;
            goalsBySituation[goal.Situation] = goalsBySituation.GetValueOrDefault(goal.Situation) + 1;
        }

        int uniqueSequences = matches
            .Select(match => match.EventSequenceSignature)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new LiveMatchBatchSummary(
            requestedMatchCount,
            new ReadOnlyDictionary<string, double>(metricAverages),
            new ReadOnlyDictionary<string, int>(goalsByDistance),
            new ReadOnlyDictionary<string, int>(goalsBySituation),
            uniqueSequences,
            matches.Count);
    }

    public static LiveMatchBatchSummary CreateFromAggregates(
        int requestedMatchCount,
        int completedMatchCount,
        IReadOnlyDictionary<string, double> metricAverages,
        IReadOnlyDictionary<string, int> goalsByDistance,
        IReadOnlyDictionary<string, int> goalsBySituation,
        int uniqueEventSequences)
    {
        return new LiveMatchBatchSummary(
            requestedMatchCount,
            new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(metricAverages)),
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(goalsByDistance)),
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(goalsBySituation)),
            uniqueEventSequences,
            completedMatchCount);
    }
}
