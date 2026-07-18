using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class LiveMatchBalanceShardMerger : Node
{
    public override void _Ready()
    {
        Callable.From(Merge).CallDeferred();
    }

    private void Merge()
    {
        try
        {
            IReadOnlyDictionary<string, string> arguments = ParseArguments(OS.GetCmdlineUserArgs());
            if (!arguments.TryGetValue("inputs", out string? inputText) || string.IsNullOrWhiteSpace(inputText))
            {
                throw new ArgumentException("--inputs must contain comma-separated shard directories.");
            }
            string[] inputDirectories = inputText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string outputDirectory = arguments.GetValueOrDefault(
                "output",
                ProjectSettings.GlobalizePath(
                    "res://.artifacts/test-reports/live-match-balance/football-fundamentals-v1-500"));
            MergeShards(inputDirectories, outputDirectory);
            GD.Print($"BALANCE_SHARDS_MERGED shards={inputDirectories.Length} output={outputDirectory}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"BALANCE_SHARD_MERGE_FATAL {exception}");
            GetTree().Quit(2);
        }
    }

    private static void MergeShards(IReadOnlyList<string> inputDirectories, string outputDirectory)
    {
        int requestedMatches = 0;
        int completedMatches = 0;
        Dictionary<string, double> weightedMetricTotals = new(StringComparer.Ordinal);
        Dictionary<string, int> goalsByDistance = new(StringComparer.Ordinal);
        Dictionary<string, int> goalsBySituation = new(StringComparer.Ordinal);
        HashSet<string> eventSignatures = new(StringComparer.Ordinal);
        BalanceIssueJournal journal = new();
        List<string> csvPaths = new();

        foreach (string inputDirectory in inputDirectories)
        {
            string summaryPath = Path.Combine(inputDirectory, "summary.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(summaryPath));
            JsonElement root = document.RootElement;
            int shardRequested = root.GetProperty("requested_match_count").GetInt32();
            int shardCompleted = root.GetProperty("completed_match_count").GetInt32();
            requestedMatches += shardRequested;
            completedMatches += shardCompleted;
            foreach (JsonProperty metric in root.GetProperty("metric_averages").EnumerateObject())
            {
                weightedMetricTotals[metric.Name] = weightedMetricTotals.GetValueOrDefault(metric.Name) +
                                                    metric.Value.GetDouble() * shardCompleted;
            }
            AddCounts(root.GetProperty("goals_by_distance"), goalsByDistance);
            AddCounts(root.GetProperty("goals_by_situation"), goalsBySituation);

            string csvPath = Path.Combine(inputDirectory, "matches.csv");
            csvPaths.Add(csvPath);
            foreach (string line in File.ReadLines(csvPath).Skip(1))
            {
                int lastComma = line.LastIndexOf(',');
                if (lastComma >= 0 && lastComma < line.Length - 1)
                {
                    eventSignatures.Add(line[(lastComma + 1)..].Trim());
                }
            }
            ImportCodeBugs(Path.Combine(inputDirectory, "issue-journal.jsonl"), journal);
        }

        Dictionary<string, double> metricAverages = weightedMetricTotals.ToDictionary(
            pair => pair.Key,
            pair => completedMatches == 0 ? 0d : pair.Value / completedMatches,
            StringComparer.Ordinal);
        LiveMatchBatchSummary summary = LiveMatchBatchSummary.CreateFromAggregates(
            requestedMatches,
            completedMatches,
            metricAverages,
            goalsByDistance,
            goalsBySituation,
            eventSignatures.Count);
        LiveMatchBalanceConfiguration configuration = LiveMatchBalanceConfiguration.CreateFootballFundamentalsV1();
        new LiveMatchBalanceAnalyzer().ValidateSummary(summary, configuration, journal);
        new BalanceReportWriter().WriteMerged(outputDirectory, configuration, summary, csvPaths, journal);
    }

    private static void AddCounts(JsonElement source, IDictionary<string, int> destination)
    {
        foreach (JsonProperty property in source.EnumerateObject())
        {
            int existing = destination.TryGetValue(property.Name, out int count) ? count : 0;
            destination[property.Name] = existing + property.Value.GetInt32();
        }
    }

    private static void ImportCodeBugs(string journalPath, BalanceIssueJournal journal)
    {
        if (!File.Exists(journalPath))
        {
            return;
        }
        foreach (string line in File.ReadLines(journalPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.GetProperty("category").GetString() != "code_bug")
            {
                continue;
            }
            BalanceIssueSeverity severity = Enum.Parse<BalanceIssueSeverity>(
                ToPascalCase(root.GetProperty("severity").GetString() ?? "error"));
            journal.AddCodeBug(
                severity,
                root.GetProperty("code").GetString() ?? "SHARD_CODE_BUG",
                root.GetProperty("description").GetString() ?? "Shard reported a code bug.",
                ReadNullableLong(root, "match_seed"),
                ReadNullableString(root, "metric_key"),
                ReadNullableDouble(root, "observed_value"),
                ReadNullableString(root, "expected_value"));
        }
    }

    private static long? ReadNullableLong(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt64();
    }

    private static double? ReadNullableDouble(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDouble();
    }

    private static string? ReadNullableString(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static string ToPascalCase(string value)
    {
        return string.Concat(value
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static IReadOnlyDictionary<string, string> ParseArguments(string[] rawArguments)
    {
        Dictionary<string, string> arguments = new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawArgument in rawArguments)
        {
            int separator = rawArgument.IndexOf('=');
            if (!rawArgument.StartsWith("--", StringComparison.Ordinal) || separator <= 2)
            {
                continue;
            }
            arguments[rawArgument[2..separator]] = rawArgument[(separator + 1)..];
        }
        return arguments;
    }
}
