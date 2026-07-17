using System.Collections.Generic;
using Godot;

public enum MatchScenarioKind
{
    ThroughBallBreakaway,
    TwoAttackersVersusOneDefender,
    ThreeAttackersVersusTwoDefenders
}

public sealed class MatchScenarioDefinition
{
    public MatchScenarioDefinition(
        MatchScenarioKind kind,
        string displayName,
        Vector2 ballCarrierPosition,
        IReadOnlyList<Vector2> supportingAttackerPositions,
        IReadOnlyList<Vector2> defenderPositions,
        Vector2? throughBallReceptionTarget = null)
    {
        Kind = kind;
        DisplayName = displayName;
        BallCarrierPosition = ballCarrierPosition;
        SupportingAttackerPositions = supportingAttackerPositions;
        DefenderPositions = defenderPositions;
        ThroughBallReceptionTarget = throughBallReceptionTarget;
    }

    public MatchScenarioKind Kind { get; }
    public string DisplayName { get; }
    public Vector2 BallCarrierPosition { get; }
    public IReadOnlyList<Vector2> SupportingAttackerPositions { get; }
    public IReadOnlyList<Vector2> DefenderPositions { get; }
    public Vector2? ThroughBallReceptionTarget { get; }
    public int AttackerCount => SupportingAttackerPositions.Count + 1;
    public int DefenderCount => DefenderPositions.Count;
    public bool StartsWithThroughBall => ThroughBallReceptionTarget.HasValue;
}
