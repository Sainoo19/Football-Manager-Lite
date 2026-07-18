using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class LiveMatchBalanceConfiguration
{
    public const int DefaultBatchMatchCount = 500;

    private readonly IReadOnlyDictionary<string, BalanceMetricRange> _metricRanges;

    public LiveMatchBalanceConfiguration(
        int batchMatchCount,
        int determinismAuditCount,
        int speedParityAuditCount,
        double minimumUniqueSequenceRatio,
        IEnumerable<BalanceMetricRange> metricRanges)
    {
        Dictionary<string, BalanceMetricRange> ranges = new();
        foreach (BalanceMetricRange range in metricRanges)
        {
            ranges.Add(range.Key, range);
        }

        BatchMatchCount = batchMatchCount;
        DeterminismAuditCount = determinismAuditCount;
        SpeedParityAuditCount = speedParityAuditCount;
        MinimumUniqueSequenceRatio = minimumUniqueSequenceRatio;
        _metricRanges = new ReadOnlyDictionary<string, BalanceMetricRange>(ranges);
    }

    public int BatchMatchCount { get; }
    public int DeterminismAuditCount { get; }
    public int SpeedParityAuditCount { get; }
    public double MinimumUniqueSequenceRatio { get; }
    public IReadOnlyDictionary<string, BalanceMetricRange> MetricRanges => _metricRanges;

    public static LiveMatchBalanceConfiguration CreateFootballFundamentalsV1()
    {
        return new LiveMatchBalanceConfiguration(
            DefaultBatchMatchCount,
            determinismAuditCount: 10,
            speedParityAuditCount: 3,
            minimumUniqueSequenceRatio: 0.98d,
            new[]
            {
                new BalanceMetricRange("goals", "Bàn thắng / trận", 1.8d, 3.6d),
                new BalanceMetricRange("shots", "Cú sút / trận", 18d, 32d),
                new BalanceMetricRange("shots_on_target", "Sút trúng đích / trận", 6d, 13d),
                new BalanceMetricRange("shot_conversion", "Tỷ lệ chuyển hóa cú sút", 0.08d, 0.16d),
                new BalanceMetricRange("pass_completion", "Tỷ lệ chuyền thành công", 0.68d, 0.88d),
                new BalanceMetricRange("dribbles", "Pha rê bóng bị tranh chấp / trận", 60d, 240d),
                new BalanceMetricRange("successful_dribbles", "Rê bóng vượt người thành công / trận", 5d, 80d),
                new BalanceMetricRange("ground_duel_wins", "Thắng tranh chấp đất / trận", 12d, 110d),
                new BalanceMetricRange("aerial_duels", "Tranh chấp trên không / trận", 8d, 55d),
                new BalanceMetricRange("fouls", "Phạm lỗi / trận", 12d, 30d),
                new BalanceMetricRange("yellow_cards", "Thẻ vàng / trận", 2d, 7d),
                new BalanceMetricRange("red_cards", "Thẻ đỏ / trận", 0d, 0.5d),
                new BalanceMetricRange("offsides", "Việt vị / trận", 1d, 6d),
                new BalanceMetricRange("penalties", "Phạt đền / trận", 0.05d, 0.6d),
                new BalanceMetricRange("corners", "Phạt góc / trận", 4d, 14d),
                new BalanceMetricRange("goal_kicks", "Phát bóng / trận", 5d, 18d),
                new BalanceMetricRange("throw_ins", "Ném biên / trận", 10d, 35d),
                new BalanceMetricRange("possession_spell_seconds", "Thời lượng đợt kiểm soát bóng", 5d, 30d),
                new BalanceMetricRange("possession_changes", "Đổi quyền kiểm soát / trận", 55d, 180d)
            });
    }
}
