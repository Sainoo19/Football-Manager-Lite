using System.Globalization;
using Godot;

public sealed class FootballActionCandidate
{
    public FootballActionCandidate(
        FootballActionType actionType,
        StringName actorId,
        StringName targetPlayerId,
        Vector2 targetPoint,
        float expectedProgressionMeters,
        float turnoverRisk,
        float immediatePressure,
        float receiverControlProbability,
        float threatValue,
        float executionDifficulty,
        bool isLegal,
        string sourceReason,
        FootballActionScoreBreakdown score = default)
    {
        ActionType = actionType;
        ActorId = actorId;
        TargetPlayerId = targetPlayerId;
        TargetPoint = targetPoint;
        ExpectedProgressionMeters = expectedProgressionMeters;
        TurnoverRisk = turnoverRisk;
        ImmediatePressure = immediatePressure;
        ReceiverControlProbability = receiverControlProbability;
        ThreatValue = threatValue;
        ExecutionDifficulty = executionDifficulty;
        IsLegal = isLegal;
        SourceReason = sourceReason;
        Score = score;
    }

    public FootballActionType ActionType { get; }
    public StringName ActorId { get; }
    public StringName TargetPlayerId { get; }
    public Vector2 TargetPoint { get; }
    public float ExpectedProgressionMeters { get; }
    public float TurnoverRisk { get; }
    public float ImmediatePressure { get; }
    public float ReceiverControlProbability { get; }
    public float ThreatValue { get; }
    public float ExecutionDifficulty { get; }
    public bool IsLegal { get; }
    public string SourceReason { get; }
    public FootballActionScoreBreakdown Score { get; }
    public string StableKey => string.Join(
        ':',
        ActionType,
        TargetPlayerId,
        TargetPoint.X.ToString("R", CultureInfo.InvariantCulture),
        TargetPoint.Y.ToString("R", CultureInfo.InvariantCulture));
    public string CommitmentKey => TargetPlayerId == new StringName()
        ? ActionType.ToString()
        : $"{ActionType}:{TargetPlayerId}";

    public FootballActionCandidate WithScore(FootballActionScoreBreakdown score)
    {
        return new FootballActionCandidate(
            ActionType,
            ActorId,
            TargetPlayerId,
            TargetPoint,
            ExpectedProgressionMeters,
            TurnoverRisk,
            ImmediatePressure,
            ReceiverControlProbability,
            ThreatValue,
            ExecutionDifficulty,
            IsLegal,
            SourceReason,
            score);
    }
}
