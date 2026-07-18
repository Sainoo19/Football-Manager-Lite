using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class BalanceReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonOptions)
    {
        WriteIndented = false
    };

    public void Write(
        string outputDirectory,
        LiveMatchBalanceConfiguration configuration,
        LiveMatchBatchSummary summary,
        IReadOnlyList<LiveMatchBalanceRecord> records,
        BalanceIssueJournal journal)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        WriteSummaryJson(outputDirectory, configuration, summary, journal);
        WriteMatchesCsv(outputDirectory, records);
        WriteIssueJournal(outputDirectory, journal);
        WriteMarkdownReport(outputDirectory, configuration, summary, journal);
    }

    public void WriteMerged(
        string outputDirectory,
        LiveMatchBalanceConfiguration configuration,
        LiveMatchBatchSummary summary,
        IReadOnlyList<string> matchCsvPaths,
        BalanceIssueJournal journal)
    {
        Directory.CreateDirectory(outputDirectory);
        WriteSummaryJson(outputDirectory, configuration, summary, journal);
        WriteMergedMatchesCsv(outputDirectory, matchCsvPaths);
        WriteIssueJournal(outputDirectory, journal);
        WriteMarkdownReport(outputDirectory, configuration, summary, journal);
    }

    private static void WriteSummaryJson(
        string outputDirectory,
        LiveMatchBalanceConfiguration configuration,
        LiveMatchBatchSummary summary,
        BalanceIssueJournal journal)
    {
        var document = new
        {
            generated_at_utc = DateTimeOffset.UtcNow,
            summary.RequestedMatchCount,
            summary.CompletedMatchCount,
            summary.UniqueEventSequences,
            summary.UniqueEventSequenceRatio,
            metric_averages = summary.MetricAverages,
            goals_by_distance = summary.GoalsByDistance,
            goals_by_situation = summary.GoalsBySituation,
            thresholds = configuration.MetricRanges.Values.Select(range => new
            {
                range.Key,
                range.DisplayName,
                range.Minimum,
                range.Maximum
            }),
            issue_counts = new
            {
                code_bug = journal.Issues.Count(issue => issue.Category == BalanceIssueCategory.CodeBug),
                football_logic = journal.Issues.Count(issue => issue.Category == BalanceIssueCategory.FootballLogic),
                error = journal.Issues.Count(issue => issue.Severity == BalanceIssueSeverity.Error),
                warning = journal.Issues.Count(issue => issue.Severity == BalanceIssueSeverity.Warning)
            }
        };
        string path = Path.Combine(outputDirectory, "summary.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions), Encoding.UTF8);
    }

    private static void WriteMatchesCsv(string outputDirectory, IReadOnlyList<LiveMatchBalanceRecord> records)
    {
        StringBuilder csv = new();
        csv.AppendLine(
            "match_index,seed,home_team,away_team,goals,shots,shots_on_target,shot_conversion," +
            "pass_attempts,completed_passes,pass_completion,dribbles,successful_dribbles,ground_duel_wins," +
            "ground_duel_exchanges,aerial_duels,headers_won,fouls,yellow_cards,red_cards," +
            "offsides,penalties,corners,goal_kicks,throw_ins,free_kicks," +
            "average_possession_spell_seconds,possession_changes,event_sequence_signature");
        foreach (LiveMatchBalanceRecord record in records)
        {
            csv.Append(record.MatchIndex).Append(',')
                .Append(record.Seed).Append(',')
                .Append(EscapeCsv(record.HomeTeam)).Append(',')
                .Append(EscapeCsv(record.AwayTeam)).Append(',')
                .Append(record.Goals).Append(',')
                .Append(record.Shots).Append(',')
                .Append(record.ShotsOnTarget).Append(',')
                .Append(Format(record.ShotConversion)).Append(',')
                .Append(record.PassAttempts).Append(',')
                .Append(record.CompletedPasses).Append(',')
                .Append(Format(record.PassCompletion)).Append(',')
                .Append(record.Dribbles).Append(',')
                .Append(record.SuccessfulDribbles).Append(',')
                .Append(record.GroundDuelWins).Append(',')
                .Append(record.GroundDuelExchanges).Append(',')
                .Append(record.AerialDuels).Append(',')
                .Append(record.HeadersWon).Append(',')
                .Append(record.Fouls).Append(',')
                .Append(record.YellowCards).Append(',')
                .Append(record.RedCards).Append(',')
                .Append(record.Offsides).Append(',')
                .Append(record.Penalties).Append(',')
                .Append(record.Corners).Append(',')
                .Append(record.GoalKicks).Append(',')
                .Append(record.ThrowIns).Append(',')
                .Append(record.FreeKicks).Append(',')
                .Append(Format(record.AveragePossessionSpellSeconds)).Append(',')
                .Append(record.PossessionChanges).Append(',')
                .Append(record.EventSequenceSignature)
                .AppendLine();
        }
        File.WriteAllText(Path.Combine(outputDirectory, "matches.csv"), csv.ToString(), Encoding.UTF8);
    }

    private static void WriteIssueJournal(string outputDirectory, BalanceIssueJournal journal)
    {
        string path = Path.Combine(outputDirectory, "issue-journal.jsonl");
        using StreamWriter writer = new(path, false, new UTF8Encoding(false));
        foreach (BalanceIssue issue in journal.Issues)
        {
            writer.WriteLine(JsonSerializer.Serialize(issue, JsonLineOptions));
        }
    }

    private static void WriteMergedMatchesCsv(string outputDirectory, IReadOnlyList<string> matchCsvPaths)
    {
        string outputPath = Path.Combine(outputDirectory, "matches.csv");
        using StreamWriter writer = new(outputPath, false, new UTF8Encoding(true));
        bool wroteHeader = false;
        int mergedIndex = 1;
        foreach (string csvPath in matchCsvPaths)
        {
            bool isFirstLine = true;
            foreach (string line in File.ReadLines(csvPath))
            {
                if (isFirstLine)
                {
                    if (!wroteHeader)
                    {
                        writer.WriteLine(line.TrimStart('\uFEFF'));
                        wroteHeader = true;
                    }
                    isFirstLine = false;
                    continue;
                }
                int firstComma = line.IndexOf(',');
                if (firstComma < 0)
                {
                    continue;
                }
                writer.Write(mergedIndex++);
                writer.WriteLine(line[firstComma..]);
            }
        }
    }

    private static void WriteMarkdownReport(
        string outputDirectory,
        LiveMatchBalanceConfiguration configuration,
        LiveMatchBatchSummary summary,
        BalanceIssueJournal journal)
    {
        StringBuilder report = new();
        report.AppendLine("# Football Fundamentals Engine v1 — Batch balance report")
            .AppendLine()
            .AppendLine($"- Hoàn tất: {summary.CompletedMatchCount}/{summary.RequestedMatchCount} trận")
            .AppendLine($"- Chuỗi diễn biến độc nhất: {summary.UniqueEventSequences} " +
                        $"({summary.UniqueEventSequenceRatio:P1})")
            .AppendLine($"- Code bug: {journal.Issues.Count(issue => issue.Category == BalanceIssueCategory.CodeBug)}")
            .AppendLine($"- Football logic: {journal.Issues.Count(issue => issue.Category == BalanceIssueCategory.FootballLogic)}")
            .AppendLine()
            .AppendLine("## Aggregate metrics")
            .AppendLine()
            .AppendLine("| Metric | Trung bình | Khoảng mong đợi | Kết quả |")
            .AppendLine("|---|---:|---:|:---:|");
        foreach (BalanceMetricRange range in configuration.MetricRanges.Values)
        {
            double value = summary.MetricAverages.GetValueOrDefault(range.Key);
            report.Append("| ").Append(range.DisplayName)
                .Append(" | ").Append(Format(value))
                .Append(" | ").Append(Format(range.Minimum)).Append("–").Append(Format(range.Maximum))
                .Append(" | ").Append(range.Contains(value) ? "PASS" : "REVIEW")
                .AppendLine(" |");
        }

        report.AppendLine()
            .AppendLine("## Goals by distance")
            .AppendLine();
        foreach ((string bucket, int count) in summary.GoalsByDistance)
        {
            report.Append("- ").Append(bucket).Append(": ").Append(count).AppendLine();
        }
        report.AppendLine()
            .AppendLine("## Goals by situation")
            .AppendLine();
        foreach ((string situation, int count) in summary.GoalsBySituation)
        {
            report.Append("- ").Append(situation).Append(": ").Append(count).AppendLine();
        }

        report.AppendLine()
            .AppendLine("## Issue journal")
            .AppendLine();
        if (journal.Issues.Count == 0)
        {
            report.AppendLine("Không phát hiện vấn đề.");
        }
        else
        {
            foreach (BalanceIssue issue in journal.Issues)
            {
                report.Append("- [").Append(issue.Category).Append('/').Append(issue.Severity).Append("] ")
                    .Append(issue.Code).Append(": ").Append(issue.Description);
                if (issue.ObservedValue.HasValue)
                {
                    report.Append(" Quan sát=").Append(Format(issue.ObservedValue.Value));
                }
                if (!string.IsNullOrWhiteSpace(issue.ExpectedValue))
                {
                    report.Append(", mong đợi=").Append(issue.ExpectedValue);
                }
                report.AppendLine();
            }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "report.md"), report.ToString(), Encoding.UTF8);
    }

    private static string Format(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
