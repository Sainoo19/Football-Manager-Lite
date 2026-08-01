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
                new List<Vector2> { new(0.770f, 0.53f) },
                startsAfterTurnover: true),
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders => new MatchScenarioDefinition(
                kind,
                "Phản công 3 đánh 2",
                new Vector2(0.610f, 0.50f),
                new List<Vector2> { new(0.660f, 0.32f), new(0.670f, 0.68f) },
                new List<Vector2> { new(0.760f, 0.41f), new(0.770f, 0.60f) },
                startsAfterTurnover: true),
            MatchScenarioKind.GoalkeeperBuildUp => new MatchScenarioDefinition(
                kind,
                "Thủ môn triển khai bóng từ sân nhà",
                new Vector2(0.065f, 0.50f),
                new List<Vector2> { new(0.155f, 0.35f), new(0.155f, 0.65f), new(0.245f, 0.50f) },
                new List<Vector2> { new(0.310f, 0.42f), new(0.320f, 0.60f) }),
            MatchScenarioKind.WingerCutBackDecision => new MatchScenarioDefinition(
                kind,
                "Cầu thủ cánh chọn căng ngang, tạt hoặc recycle",
                new Vector2(0.835f, 0.12f),
                new List<Vector2> { new(0.865f, 0.47f), new(0.790f, 0.58f), new(0.720f, 0.24f) },
                new List<Vector2> { new(0.850f, 0.38f), new(0.800f, 0.52f) }),
            MatchScenarioKind.StrikerBackToGoalWithTwoOutlets => new MatchScenarioDefinition(
                kind,
                "Tiền đạo quay lưng với hai điểm nhả bóng",
                new Vector2(0.720f, 0.50f),
                new List<Vector2> { new(0.690f, 0.30f), new(0.680f, 0.70f) },
                new List<Vector2> { new(0.738f, 0.50f), new(0.760f, 0.58f) }),
            MatchScenarioKind.CentralMidfielderLateBoxEntry => new MatchScenarioDefinition(
                kind,
                "Tiền vệ trung tâm băng lên vòng cấm muộn",
                new Vector2(0.705f, 0.52f),
                new List<Vector2> { new(0.815f, 0.46f), new(0.760f, 0.76f) },
                new List<Vector2> { new(0.800f, 0.38f), new(0.825f, 0.62f) }),
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
        MatchScenarioKind.GoalkeeperBuildUp => "Thủ môn triển khai bóng",
        MatchScenarioKind.WingerCutBackDecision => "Cầu thủ cánh: căng ngang/tạt/recycle",
        MatchScenarioKind.StrikerBackToGoalWithTwoOutlets => "Tiền đạo quay lưng — hai outlet",
        MatchScenarioKind.CentralMidfielderLateBoxEntry => "CM băng lên vòng cấm muộn",
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
                : null,
            source.StartsAfterTurnover);
    }

    private static Vector2 Mirror(Vector2 position) => new(1f - position.X, position.Y);
}
