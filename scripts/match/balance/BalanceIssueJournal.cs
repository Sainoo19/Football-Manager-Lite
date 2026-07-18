using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class BalanceIssueJournal
{
    private readonly List<BalanceIssue> _issues = new();
    private readonly IReadOnlyList<BalanceIssue> _issueView;

    public BalanceIssueJournal()
    {
        _issueView = new ReadOnlyCollection<BalanceIssue>(_issues);
    }

    public IReadOnlyList<BalanceIssue> Issues => _issueView;

    public void AddCodeBug(
        BalanceIssueSeverity severity,
        string code,
        string description,
        long? matchSeed = null,
        string? metricKey = null,
        double? observedValue = null,
        string? expectedValue = null)
    {
        _issues.Add(new BalanceIssue(
            BalanceIssueCategory.CodeBug,
            severity,
            code,
            description,
            matchSeed,
            metricKey,
            observedValue,
            expectedValue));
    }

    public void AddFootballLogic(
        BalanceIssueSeverity severity,
        string code,
        string description,
        long? matchSeed = null,
        string? metricKey = null,
        double? observedValue = null,
        string? expectedValue = null)
    {
        _issues.Add(new BalanceIssue(
            BalanceIssueCategory.FootballLogic,
            severity,
            code,
            description,
            matchSeed,
            metricKey,
            observedValue,
            expectedValue));
    }
}
