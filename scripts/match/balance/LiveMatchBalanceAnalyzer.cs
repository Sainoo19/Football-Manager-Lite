using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;

public sealed class LiveMatchBalanceAnalyzer
{
    public LiveMatchBalanceRecord CreateRecord(int matchIndex, HeadlessLiveMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        FootballMatchSimulation simulation = result.Simulation;
        LiveMatchSnapshot snapshot = result.FinalSnapshot;
        LiveMatchMetrics metrics = snapshot.Metrics;
        LiveMatchAnalyticsSnapshot analytics = snapshot.Analytics;
        int goals = Stat(simulation.home, "goals") + Stat(simulation.away, "goals");
        int shots = Stat(simulation.home, "shots") + Stat(simulation.away, "shots");
        int shotsOnTarget = Stat(simulation.home, "shots_on_target") + Stat(simulation.away, "shots_on_target");
        int fouls = Stat(simulation.home, "fouls") + Stat(simulation.away, "fouls");
        int yellowCards = Stat(simulation.home, "yellow_cards") + Stat(simulation.away, "yellow_cards");
        int redCards = Stat(simulation.home, "red_cards") + Stat(simulation.away, "red_cards");
        int penalties = Stat(simulation.home, "penalties") + Stat(simulation.away, "penalties");
        int offsides = simulation.events.Count(matchEvent => matchEvent.event_type == "offside");
        List<BalanceGoalRecord> goalRecords = analytics.Goals
            .Select(goal => new BalanceGoalRecord(goal.DistanceMeters, goal.Situation.ToString()))
            .ToList();

        return new LiveMatchBalanceRecord(
            matchIndex,
            simulation.MatchSeed,
            simulation.home.team.display_name,
            simulation.away.team.display_name,
            goals,
            shots,
            shotsOnTarget,
            metrics.PassAttempts,
            metrics.CompletedPasses,
            metrics.Dribbles,
            metrics.CarrierEscapes,
            metrics.TacklesWon + metrics.CarrierEscapes,
            metrics.GroundDuelExchanges,
            metrics.AerialDuels,
            metrics.HeadersWon,
            fouls,
            yellowCards,
            redCards,
            offsides,
            penalties,
            analytics.Corners,
            analytics.GoalKicks,
            analytics.ThrowIns,
            analytics.FreeKicks,
            analytics.AveragePossessionSpellSeconds,
            analytics.PossessionChanges,
            CreateEventSequenceSignature(simulation),
            goalRecords);
    }

    public void ValidateMatch(
        HeadlessLiveMatchResult result,
        LiveMatchBalanceRecord record,
        BalanceIssueJournal journal)
    {
        FootballMatchSimulation simulation = result.Simulation;
        if (!simulation.is_finished || simulation.current_minute != 90 || result.FinalSnapshot.Phase != LiveMatchPhase.FullTime)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "MATCH_DID_NOT_FINISH",
                "Trận headless không kết thúc đúng phút 90 và trạng thái FullTime.",
                record.Seed);
        }
        if (record.ShotsOnTarget > record.Shots)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "SHOT_STAT_INVARIANT",
                "Số cú sút trúng đích lớn hơn tổng số cú sút.",
                record.Seed);
        }
        if (record.Goals > record.ShotsOnTarget)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "GOAL_STAT_INVARIANT",
                "Số bàn thắng lớn hơn số cú sút trúng đích.",
                record.Seed);
        }
        if (record.CompletedPasses > record.PassAttempts)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "PASS_STAT_INVARIANT",
                "Số đường chuyền thành công lớn hơn số lần chuyền.",
                record.Seed);
        }
        if (record.Goals != record.GoalRecords.Count)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "GOAL_TELEMETRY_MISMATCH",
                "Số bàn trong thống kê không khớp nhật ký khoảng cách bàn thắng.",
                record.Seed,
                "goals",
                record.GoalRecords.Count,
                record.Goals.ToString());
        }
        if (record.GoalRecords.Any(goal =>
                !float.IsFinite(goal.DistanceMeters) ||
                goal.DistanceMeters < 0f ||
                goal.DistanceMeters > FootballPitchDimensions.LengthMeters))
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "INVALID_GOAL_DISTANCE",
                "Khoảng cách bàn thắng nằm ngoài kích thước sân.",
                record.Seed);
        }
    }

    public LiveMatchBatchSummary AnalyzeBatch(
        IReadOnlyList<LiveMatchBalanceRecord> records,
        int requestedMatchCount,
        LiveMatchBalanceConfiguration configuration,
        BalanceIssueJournal journal)
    {
        LiveMatchBatchSummary summary = LiveMatchBatchSummary.Create(records, requestedMatchCount);
        ValidateSummary(summary, configuration, journal);
        return summary;
    }

    public void ValidateSummary(
        LiveMatchBatchSummary summary,
        LiveMatchBalanceConfiguration configuration,
        BalanceIssueJournal journal)
    {
        if (summary.CompletedMatchCount != summary.RequestedMatchCount)
        {
            journal.AddCodeBug(
                BalanceIssueSeverity.Error,
                "INCOMPLETE_BATCH",
                "Không phải tất cả trận yêu cầu đều chạy hoàn tất.",
                observedValue: summary.CompletedMatchCount,
                expectedValue: summary.RequestedMatchCount.ToString());
        }

        foreach (BalanceMetricRange range in configuration.MetricRanges.Values)
        {
            if (!summary.MetricAverages.TryGetValue(range.Key, out double average))
            {
                journal.AddCodeBug(
                    BalanceIssueSeverity.Error,
                    "MISSING_METRIC",
                    $"Báo cáo thiếu metric {range.DisplayName}.",
                    metricKey: range.Key);
                continue;
            }
            if (!double.IsFinite(average))
            {
                journal.AddCodeBug(
                    BalanceIssueSeverity.Error,
                    "INVALID_METRIC",
                    $"Metric {range.DisplayName} không phải số hữu hạn.",
                    metricKey: range.Key,
                    observedValue: average);
                continue;
            }
            if (!range.Contains(average))
            {
                journal.AddFootballLogic(
                    BalanceIssueSeverity.Warning,
                    "BALANCE_RANGE_MISS",
                    $"{range.DisplayName} nằm ngoài khoảng cân bằng Football Fundamentals v1.",
                    metricKey: range.Key,
                    observedValue: average,
                    expectedValue: $"{range.Minimum:0.###}–{range.Maximum:0.###}");
            }
        }

        if (summary.UniqueEventSequenceRatio < configuration.MinimumUniqueSequenceRatio)
        {
            journal.AddFootballLogic(
                BalanceIssueSeverity.Warning,
                "REPEATED_MATCH_PATTERN",
                "Tỷ lệ chuỗi diễn biến độc nhất quá thấp; engine có dấu hiệu lặp bất thường.",
                metricKey: "unique_event_sequence_ratio",
                observedValue: summary.UniqueEventSequenceRatio,
                expectedValue: $">= {configuration.MinimumUniqueSequenceRatio:0.###}");
        }

    }

    public bool AreEquivalent(LiveMatchBalanceRecord first, LiveMatchBalanceRecord second)
    {
        return first.EventSequenceSignature == second.EventSequenceSignature &&
               first.GetMetricValues().All(pair =>
                   second.GetMetricValues().TryGetValue(pair.Key, out double value) &&
                   Math.Abs(pair.Value - value) < 0.000001d) &&
               first.GoalRecords.Count == second.GoalRecords.Count &&
               first.GoalRecords.Zip(second.GoalRecords).All(pair =>
                   Math.Abs(pair.First.DistanceMeters - pair.Second.DistanceMeters) < 0.0001f &&
                   pair.First.Situation == pair.Second.Situation);
    }

    private static int Stat(MatchTeamState state, string key)
    {
        return state.stats.TryGetValue(key, out Variant value) ? value.AsInt32() : 0;
    }

    private static string CreateEventSequenceSignature(FootballMatchSimulation simulation)
    {
        StringBuilder builder = new();
        foreach (FootballMatchEvent matchEvent in simulation.events)
        {
            builder
                .Append(matchEvent.minute)
                .Append(':')
                .Append(matchEvent.event_type)
                .Append(':')
                .Append(matchEvent.team_id)
                .Append(':')
                .Append(matchEvent.player_id)
                .Append('|');
        }
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }
}
