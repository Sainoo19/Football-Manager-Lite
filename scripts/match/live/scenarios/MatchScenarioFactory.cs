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
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders => new MatchScenarioDefinition(
                kind,
                "Phản công 3 đánh 2",
                new Vector2(0.610f, 0.50f),
                new List<Vector2> { new(0.660f, 0.32f), new(0.670f, 0.68f) },
                new List<Vector2> { new(0.760f, 0.41f), new(0.770f, 0.60f) }),
            MatchScenarioKind.CentralOneVersusOne => new MatchScenarioDefinition(
                kind,
                "1 đấu 1 trung lộ",
                new Vector2(0.600f, 0.50f),
                new List<Vector2>(),
                new List<Vector2> { new(0.625f, 0.50f) }),
            MatchScenarioKind.WideOneVersusOne => new MatchScenarioDefinition(
                kind,
                "1 đấu 1 ngoài biên",
                new Vector2(0.600f, 0.16f),
                new List<Vector2>(),
                new List<Vector2> { new(0.625f, 0.18f) }),
            MatchScenarioKind.AerialCrossIntoBox => new MatchScenarioDefinition(
                kind,
                "Tạt bóng bổng — tranh chấp điểm rơi",
                new Vector2(0.790f, 0.10f),
                new List<Vector2> { new(0.855f, 0.43f), new(0.845f, 0.60f) },
                new List<Vector2> { new(0.865f, 0.48f), new(0.850f, 0.64f) }),
            MatchScenarioKind.LoftedPassAerialDuel => new MatchScenarioDefinition(
                kind,
                "Chuyền bổng — hai đội tranh điểm rơi",
                new Vector2(0.500f, 0.50f),
                new List<Vector2> { new(0.715f, 0.50f) },
                new List<Vector2> { new(0.720f, 0.44f), new(0.735f, 0.58f) }),
            MatchScenarioKind.AerialClearanceUnderPressure => new MatchScenarioDefinition(
                kind,
                "Phá bóng bổng dưới áp lực",
                new Vector2(0.165f, 0.50f),
                new List<Vector2> { new(0.480f, 0.44f) },
                new List<Vector2> { new(0.185f, 0.53f), new(0.490f, 0.56f) }),
            _ => new MatchScenarioDefinition(
                kind,
                "Tiền đạo quay lưng che bóng",
                new Vector2(0.710f, 0.50f),
                new List<Vector2>(),
                new List<Vector2> { new(0.725f, 0.50f) })
        };

        return attackDirection > 0f ? canonical : Mirror(canonical);
    }

    public static string DisplayName(MatchScenarioKind kind) => kind switch
    {
        MatchScenarioKind.ThroughBallBreakaway => "Chọc khe — nhận bóng cách gôn 35 m",
        MatchScenarioKind.TwoAttackersVersusOneDefender => "Phản công 2 đánh 1",
        MatchScenarioKind.ThreeAttackersVersusTwoDefenders => "Phản công 3 đánh 2",
        MatchScenarioKind.CentralOneVersusOne => "1 đấu 1 trung lộ",
        MatchScenarioKind.WideOneVersusOne => "1 đấu 1 ngoài biên",
        MatchScenarioKind.AerialCrossIntoBox => "Tạt bóng bổng — tranh chấp điểm rơi",
        MatchScenarioKind.LoftedPassAerialDuel => "Chuyền bổng — hai đội tranh điểm rơi",
        MatchScenarioKind.AerialClearanceUnderPressure => "Phá bóng bổng dưới áp lực",
        _ => "Tiền đạo quay lưng che bóng"
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
