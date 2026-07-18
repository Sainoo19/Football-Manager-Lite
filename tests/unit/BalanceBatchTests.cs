using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public static class BalanceBatchTests
{
    public static void Run()
    {
        VerifyConfigurationAndIssueClassification();
        VerifyAggregateValidationUsesConfiguredRanges();
        GD.Print("PASS: cấu hình balance và nhật ký phân loại code bug/football logic hoạt động độc lập.");
    }

    private static void VerifyConfigurationAndIssueClassification()
    {
        LiveMatchBalanceConfiguration configuration =
            LiveMatchBalanceConfiguration.CreateFootballFundamentalsV1();
        Check(configuration.BatchMatchCount == 500, "Batch chuẩn phải yêu cầu 500 trận.");
        Check(configuration.MetricRanges.ContainsKey("pass_completion") &&
              configuration.MetricRanges.ContainsKey("possession_changes"),
            "Configuration phải chứa ngưỡng chuyền bóng và đổi quyền kiểm soát.");

        BalanceIssueJournal journal = new();
        journal.AddCodeBug(BalanceIssueSeverity.Error, "TEST_CODE", "code");
        journal.AddFootballLogic(BalanceIssueSeverity.Warning, "TEST_LOGIC", "logic");
        Check(journal.Issues.Count == 2 &&
              journal.Issues[0].Category == BalanceIssueCategory.CodeBug &&
              journal.Issues[1].Category == BalanceIssueCategory.FootballLogic,
            "Nhật ký phải giữ đúng category thay vì trộn bug code với lỗi logic bóng đá.");
    }

    private static void VerifyAggregateValidationUsesConfiguredRanges()
    {
        LiveMatchBalanceConfiguration configuration =
            LiveMatchBalanceConfiguration.CreateFootballFundamentalsV1();
        Dictionary<string, double> averages = configuration.MetricRanges.Values.ToDictionary(
            range => range.Key,
            range => (range.Minimum + range.Maximum) * 0.5d);
        averages["goals"] = 99d;
        LiveMatchBatchSummary summary = LiveMatchBatchSummary.CreateFromAggregates(
            requestedMatchCount: 10,
            completedMatchCount: 10,
            averages,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            uniqueEventSequences: 10);
        BalanceIssueJournal journal = new();
        new LiveMatchBalanceAnalyzer().ValidateSummary(summary, configuration, journal);
        Check(journal.Issues.Any(issue =>
                issue.Category == BalanceIssueCategory.FootballLogic &&
                issue.MetricKey == "goals"),
            "Metric ngoài ngưỡng phải được ghi là FootballLogic.");
        Check(journal.Issues.All(issue => issue.Category != BalanceIssueCategory.CodeBug),
            "Một aggregate hợp lệ nhưng lệch cân bằng không được phân loại thành code bug.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
