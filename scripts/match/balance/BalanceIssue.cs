public enum BalanceIssueCategory
{
    CodeBug,
    FootballLogic
}

public enum BalanceIssueSeverity
{
    Information,
    Warning,
    Error
}

public sealed class BalanceIssue
{
    public BalanceIssue(
        BalanceIssueCategory category,
        BalanceIssueSeverity severity,
        string code,
        string description,
        long? matchSeed = null,
        string? metricKey = null,
        double? observedValue = null,
        string? expectedValue = null)
    {
        Category = category;
        Severity = severity;
        Code = code;
        Description = description;
        MatchSeed = matchSeed;
        MetricKey = metricKey;
        ObservedValue = observedValue;
        ExpectedValue = expectedValue;
    }

    public BalanceIssueCategory Category { get; }
    public BalanceIssueSeverity Severity { get; }
    public string Code { get; }
    public string Description { get; }
    public long? MatchSeed { get; }
    public string? MetricKey { get; }
    public double? ObservedValue { get; }
    public string? ExpectedValue { get; }
}
