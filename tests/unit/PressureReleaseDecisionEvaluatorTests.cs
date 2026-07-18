using System;
using Godot;

public static class PressureReleaseDecisionEvaluatorTests
{
    public static void Run()
    {
        PressureReleaseDecisionEvaluator evaluator = new();
        PassSelection freeOutlet = new(
            "free_runner",
            0.35f,
            5f,
            18f,
            0.24f,
            7f);
        Check(
            evaluator.ShouldRelease(CreateContext(freeOutlet, decisionRoll: 0.70f)),
            "Người cầm bóng bị áp sát phải nhận ra đồng đội trống tạo lợi thế phía trước.");

        PassSelection markedOutlet = new(
            "marked_runner",
            0.10f,
            6f,
            17f,
            0.30f,
            2.4f);
        Check(
            !evaluator.ShouldRelease(CreateContext(markedOutlet, decisionRoll: 0.10f)),
            "Không được chuyền chỉ để thoát tranh chấp nếu người nhận cũng đang bị khóa.");

        PassSelection dangerousLane = new(
            "blocked_runner",
            0.10f,
            8f,
            20f,
            0.72f,
            7f);
        Check(
            !evaluator.ShouldRelease(CreateContext(dangerousLane, decisionRoll: 0.10f)),
            "Một đường chuyền xuyên hành lang bị khóa không phải lối thoát pressing hợp lệ.");

        PassSelection safeButMarginalOutlet = new(
            "support_player",
            0.04f,
            3.5f,
            16f,
            0.42f,
            4.2f);
        Check(
            !evaluator.ShouldRelease(CreateContext(
                safeButMarginalOutlet,
                dribbling: 88,
                decisionRoll: 0.90f)),
            "Cầu thủ rê bóng giỏi vẫn có thể xử lý nhịp đầu nếu phương án chuyền chỉ ở mức vừa phải.");
        Check(
            evaluator.ShouldRelease(CreateContext(
                safeButMarginalOutlet,
                duelTouchCount: 1,
                duelExchangeCount: 1,
                dribbling: 88,
                decisionRoll: 0.72f)),
            "Sau khi bị giữ trong tranh chấp, một lối thoát an toàn phải được ưu tiên hơn vòng lặp rê bóng.");

        Check(
            !evaluator.ShouldRelease(CreateContext(
                freeOutlet,
                isUnderPressure: false,
                decisionRoll: 0f)),
            "Ngoài áp lực, evaluator phải để pipeline quyết định thông thường chọn hành động.");

        GD.Print("PASS: người giữ bóng biết nhả bóng khỏi pressing mà không bị ép chuyền vô điều kiện.");
    }

    private static PressureReleaseContext CreateContext(
        PassSelection pass,
        bool isUnderPressure = true,
        int duelTouchCount = 0,
        int duelExchangeCount = 0,
        int dribbling = 70,
        float decisionRoll = 0.5f)
    {
        return new PressureReleaseContext(
            isUnderPressure,
            duelTouchCount,
            duelExchangeCount,
            passing: 72,
            vision: 74,
            composure: 70,
            dribbling,
            pass,
            decisionRoll);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
