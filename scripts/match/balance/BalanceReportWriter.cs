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
            engine_version = LiveMatchEngineIdentity.EngineVersion,
            configuration_fingerprint = LiveMatchEngineIdentity.ConfigurationFingerprint(
                LiveMatchEngineConfiguration.CreateFootballFundamentalsV1()),
            summary.RequestedMatchCount,
            summary.CompletedMatchCount,
            summary.UniqueEventSequences,
            summary.UniqueEventSequenceRatio,
            metric_averages = summary.MetricAverages,
            goals_by_distance = summary.GoalsByDistance,
            goals_by_situation = summary.GoalsBySituation,
            action_reason_counts = summary.ActionReasonCounts,
            phase_transition_counts = summary.PhaseTransitionCounts,
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
            "average_possession_spell_seconds,possession_changes,action_decisions,action_score_margin," +
            "backward_passes,sideways_passes,forward_passes,progressive_action_rate,forced_actions," +
            "decision_cancellations,no_valid_actions,phase_transitions,counter_attacks," +
            "counter_attack_conversion,time_to_organize_seconds,final_third_rest_defence_players," +
            "emergency_defence_entries,event_sequence_signature," +
            "final_snapshot_signature");
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
                .Append(record.ActionMetrics.Decisions).Append(',')
                .Append(Format(record.ActionMetrics.AverageScoreMargin)).Append(',')
                .Append(record.ActionMetrics.BackwardPasses).Append(',')
                .Append(record.ActionMetrics.SidewaysPasses).Append(',')
                .Append(record.ActionMetrics.ForwardPasses).Append(',')
                .Append(Format(record.ActionMetrics.ProgressiveActionRate)).Append(',')
                .Append(record.ActionMetrics.ForcedActions).Append(',')
                .Append(record.ActionMetrics.DecisionCancellations).Append(',')
                .Append(record.ActionMetrics.NoValidActions).Append(',')
                .Append(record.TeamPhaseMetrics.TransitionCounts.Values.Sum()).Append(',')
                .Append(record.TeamPhaseMetrics.CounterAttacks).Append(',')
                .Append(Format(record.TeamPhaseMetrics.CounterAttackConversion)).Append(',')
                .Append(Format(record.TeamPhaseMetrics.AverageOrganizationSeconds)).Append(',')
                .Append(Format(record.TeamPhaseMetrics.AverageFinalThirdRestDefencePlayers)).Append(',')
                .Append(record.TeamPhaseMetrics.EmergencyDefenceEntries).Append(',')
                .Append(record.EventSequenceSignature).Append(',')
                .Append(record.FinalSnapshotSignature)
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
            .AppendLine($"- Engine version: `{LiveMatchEngineIdentity.EngineVersion}`")
            .AppendLine($"- Configuration fingerprint: `" +
                        $"{LiveMatchEngineIdentity.ConfigurationFingerprint(LiveMatchEngineConfiguration.CreateFootballFundamentalsV1())}`")
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
            .AppendLine("## M1 action selection")
            .AppendLine()
            .AppendLine("| Metric | Trung bình / trận |")
            .AppendLine("|---|---:|");
        foreach (FootballActionType actionType in Enum.GetValues<FootballActionType>())
        {
            string key = $"action_{actionType.ToString().ToLowerInvariant()}";
            report.Append("| ").Append(actionType)
                .Append(" | ").Append(Format(summary.MetricAverages.GetValueOrDefault(key)))
                .AppendLine(" |");
        }
        report.Append("| Score margin | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("action_score_margin")))
            .AppendLine(" |")
            .Append("| Progressive action rate | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("progressive_action_rate")))
            .AppendLine(" |")
            .Append("| Forced actions | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("forced_actions")))
            .AppendLine(" |")
            .Append("| Decision cancellations | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("decision_cancellations")))
            .AppendLine(" |")
            .Append("| No-valid actions | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("no_valid_actions")))
            .AppendLine(" |")
            .AppendLine()
            .AppendLine("### Decision reasons")
            .AppendLine();
        foreach ((string reason, int count) in summary.ActionReasonCounts
                     .OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            report.Append("- ").Append(reason).Append(": ").Append(count).AppendLine();
        }

        report.AppendLine()
            .AppendLine("## M2 possession phases")
            .AppendLine()
            .AppendLine("| Metric | Trung bình / trận |")
            .AppendLine("|---|---:|")
            .Append("| Phase transitions | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("phase_transitions"))).AppendLine(" |")
            .Append("| Counterattacks | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("counter_attacks"))).AppendLine(" |")
            .Append("| Counterattack conversion | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("counter_attack_conversion"))).AppendLine(" |")
            .Append("| Time to organize | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("time_to_organize_seconds"))).AppendLine(" |")
            .Append("| Rest-defence players in final third | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("final_third_rest_defence_players"))).AppendLine(" |")
            .Append("| Emergency-defence entries | ")
            .Append(Format(summary.MetricAverages.GetValueOrDefault("emergency_defence_entries"))).AppendLine(" |")
            .AppendLine()
            .AppendLine("### Phase transition matrix")
            .AppendLine();
        foreach ((string transition, int count) in summary.PhaseTransitionCounts
                     .OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            report.Append("- ").Append(transition).Append(": ").Append(count).AppendLine();
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
