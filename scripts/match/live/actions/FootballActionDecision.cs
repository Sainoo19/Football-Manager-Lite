using System.Collections.Generic;
using System.Collections.ObjectModel;

public readonly struct RejectedFootballActionSummary
{
    public RejectedFootballActionSummary(FootballActionType actionType, string target, float score, string reason)
    {
        ActionType = actionType;
        Target = target;
        Score = score;
        Reason = reason;
    }

    public FootballActionType ActionType { get; }
    public string Target { get; }
    public float Score { get; }
    public string Reason { get; }
}

public sealed class FootballActionDecision
{
    public FootballActionDecision(
        FootballActionCandidate selected,
        float deterministicVariation,
        float scoreMargin,
        IReadOnlyList<RejectedFootballActionSummary> rejectedCandidates,
        string reason,
        bool usedCommitment,
        bool cancelledCommitment,
        bool wasForced)
    {
        Selected = selected;
        DeterministicVariation = deterministicVariation;
        ScoreMargin = scoreMargin;
        RejectedCandidates = new ReadOnlyCollection<RejectedFootballActionSummary>(
            new List<RejectedFootballActionSummary>(rejectedCandidates));
        Reason = reason;
        UsedCommitment = usedCommitment;
        CancelledCommitment = cancelledCommitment;
        WasForced = wasForced;
    }

    public FootballActionCandidate Selected { get; }
    public float DeterministicVariation { get; }
    public float ScoreMargin { get; }
    public IReadOnlyList<RejectedFootballActionSummary> RejectedCandidates { get; }
    public string Reason { get; }
    public bool UsedCommitment { get; }
    public bool CancelledCommitment { get; }
    public bool WasForced { get; }
}
