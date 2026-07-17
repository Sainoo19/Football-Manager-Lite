using System.Collections.Generic;
using Godot;

public sealed class MatchScenarioFactory
{
    public MatchScenarioDefinition Create(MatchScenarioKind kind, float attackDirection)
    {
        MatchScenarioDefinition canonical = kind switch
        {
            MatchScenarioKind.ThroughBallBreakaway => new MatchScenarioDefinition(
                kind,
                "Chọc khe phá bẫy — nhận bóng cách gôn 35 m",
                new Vector2(0.48f, 0.50f),
                new List<Vector2> { new(0.638f, 0.50f) },
                new List<Vector2> { new(0.650f, 0.42f), new(0.660f, 0.58f) },
                new Vector2(0.667f, 0.50f)),
            MatchScenarioKind.TwoAttackersVersusOneDefender => new MatchScenarioDefinition(
                kind,
                "Phản công 2 đánh 1",
                new Vector2(0.640f, 0.43f),
                new List<Vector2> { new(0.655f, 0.67f) },
                new List<Vector2> { new(0.770f, 0.53f) }),
            _ => new MatchScenarioDefinition(
                kind,
                "Phản công 3 đánh 2",
                new Vector2(0.610f, 0.50f),
                new List<Vector2> { new(0.660f, 0.32f), new(0.670f, 0.68f) },
                new List<Vector2> { new(0.760f, 0.41f), new(0.770f, 0.60f) })
        };

        return attackDirection > 0f ? canonical : Mirror(canonical);
    }

    public static string DisplayName(MatchScenarioKind kind) => kind switch
    {
        MatchScenarioKind.ThroughBallBreakaway => "Chọc khe — nhận bóng cách gôn 35 m",
        MatchScenarioKind.TwoAttackersVersusOneDefender => "Phản công 2 đánh 1",
        _ => "Phản công 3 đánh 2"
    };

    private static MatchScenarioDefinition Mirror(MatchScenarioDefinition source)
    {
        List<Vector2> attackers = new();
        foreach (Vector2 position in source.SupportingAttackerPositions)
        {
            attackers.Add(Mirror(position));
        }

        List<Vector2> defenders = new();
        foreach (Vector2 position in source.DefenderPositions)
        {
            defenders.Add(Mirror(position));
        }

        return new MatchScenarioDefinition(
            source.Kind,
            source.DisplayName,
            Mirror(source.BallCarrierPosition),
            attackers,
            defenders,
            source.ThroughBallReceptionTarget.HasValue
                ? Mirror(source.ThroughBallReceptionTarget.Value)
                : null);
    }

    private static Vector2 Mirror(Vector2 position) => new(1f - position.X, position.Y);
}
