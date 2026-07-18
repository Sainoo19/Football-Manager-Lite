using System;
using Godot;

public static class GroundDuelTests
{
    public static void Run()
    {
        VerifyDribbleTouchChoices();
        VerifyDefenderEngagementChoices();
        VerifyGroundDuelOutcomes();
        VerifyDistanceAndDirectionChangeTheContest();
        VerifySequenceSpansMultipleTouches();
        GD.Print("PASS: quyết định dắt bóng và tranh chấp mặt đất hoạt động theo nhiều nhịp.");
    }

    private static void VerifyDribbleTouchChoices()
    {
        DribbleTouchPlanner planner = new();
        DribbleTouchPlan centralEscape = planner.Plan(new DribbleTouchContext(
            new Vector2(0.50f, 0.50f),
            new Vector2(0.52f, 0.50f),
            1f,
            2.1f,
            3.8f,
            82,
            78,
            79,
            60,
            72,
            86,
            false,
            false,
            1,
            0.35f));
        Check(
            centralEscape.Type == DribbleTouchType.ChangeDirection,
            "Cầu thủ khéo léo bị chặn phía trước phải biết đổi hướng tránh hậu vệ.");

        DribbleTouchPlan wideKnockOn = planner.Plan(new DribbleTouchContext(
            new Vector2(0.50f, 0.08f),
            new Vector2(0.55f, 0.12f),
            1f,
            5.2f,
            4.4f,
            80,
            77,
            88,
            58,
            74,
            84,
            false,
            false,
            1,
            0.20f));
        Check(
            wideKnockOn.Type == DribbleTouchType.KnockOn &&
            wideKnockOn.BallLeadMeters >= 2.5f &&
            wideKnockOn.Target.Y >= 0f,
            "Ngoài biên và còn khoảng trống, cầu thủ nhanh phải có thể đẩy bóng dài để tăng tốc.");

        DribbleTouchPlan backToGoal = planner.Plan(new DribbleTouchContext(
            new Vector2(0.70f, 0.50f),
            new Vector2(0.72f, 0.50f),
            1f,
            1.5f,
            0.8f,
            72,
            75,
            70,
            86,
            82,
            66,
            true,
            true,
            0,
            0.40f));
        Check(
            backToGoal.Type == DribbleTouchType.Shield && backToGoal.Exposure < 0.20f,
            "Tiền đạo quay lưng và bị áp sát phải che bóng thay vì chạy xuyên hậu vệ.");
    }

    private static void VerifyDefenderEngagementChoices()
    {
        DefenderEngagementPlanner planner = new();
        DribbleTouchType exposedTouch = DribbleTouchType.KnockOn;
        DefenderEngagementPlan firstExchange = planner.Plan(CreateDefenderContext(
            exposedTouch,
            exchangeCount: 0,
            distanceMeters: 1.4f,
            cooldown: false,
            backToGoal: false,
            decisionRoll: 0.01f));
        Check(
            firstExchange.Type == DefenderEngagementType.Jockey && !firstExchange.AttemptsChallenge,
            "Hậu vệ không được tắc bóng ngay ở nhịp đầu tiên của mọi pha 1 đấu 1.");

        DefenderEngagementPlan timedTackle = planner.Plan(CreateDefenderContext(
            exposedTouch,
            exchangeCount: 3,
            distanceMeters: 1.4f,
            cooldown: false,
            backToGoal: false,
            decisionRoll: 0.05f));
        Check(
            timedTackle.Type == DefenderEngagementType.Tackle && timedTackle.AttemptsChallenge,
            "Sau khi chạy kèm, hậu vệ giỏi phải nhận ra nhịp bóng hở để tắc bóng.");

        DefenderEngagementPlan recovery = planner.Plan(CreateDefenderContext(
            exposedTouch,
            exchangeCount: 4,
            distanceMeters: 1.2f,
            cooldown: true,
            backToGoal: false,
            decisionRoll: 0.01f));
        Check(
            recovery.Type == DefenderEngagementType.Recover && !recovery.AttemptsChallenge,
            "Hậu vệ vừa vào bóng phải lùi và hồi vị thay vì tắc bóng liên tục.");

        DefenderEngagementPlan shoulder = planner.Plan(CreateDefenderContext(
            DribbleTouchType.Shield,
            exchangeCount: 3,
            distanceMeters: 1.2f,
            cooldown: false,
            backToGoal: true,
            decisionRoll: 0.10f));
        Check(
            shoulder.Type == DefenderEngagementType.ShoulderChallenge,
            "Khi tiền đạo quay lưng, hậu vệ đủ khỏe phải có thể tranh chấp vai.");
    }

    private static void VerifyGroundDuelOutcomes()
    {
        GroundDuelResolver resolver = new();
        DribbleTouchPlan closeTouch = new(
            DribbleTouchType.CloseControl,
            new Vector2(0.52f, 0.50f),
            0.75f,
            0.44f,
            0.34f);
        Check(
            resolver.Resolve(CreateDuelContext(
                DefenderEngagementType.Jockey,
                closeTouch,
                timingRoll: 0.3f,
                outcomeRoll: 0.3f,
                foulRoll: 0.9f)).Outcome == GroundDuelOutcome.NoChallenge,
            "Chạy kèm và giữ khoảng cách không được tự biến thành một pha đoạt bóng tức thì.");

        Check(
            resolver.Resolve(CreateDuelContext(
                DefenderEngagementType.Tackle,
                closeTouch,
                timingRoll: 0.2f,
                outcomeRoll: 0.01f,
                foulRoll: 0.9f)).Outcome == GroundDuelOutcome.DefenderWins,
            "Một pha tắc bóng đúng nhịp phải có thể giúp hậu vệ đoạt bóng.");

        DribbleTouchPlan shieldTouch = new(
            DribbleTouchType.Shield,
            new Vector2(0.50f, 0.52f),
            0.55f,
            0.48f,
            0.16f);
        GroundDuelResolution looseBall = resolver.Resolve(CreateDuelContext(
            DefenderEngagementType.ShoulderChallenge,
            shieldTouch,
            timingRoll: 0.1f,
            outcomeRoll: 0.48f,
            foulRoll: 0.9f));
        Check(
            looseBall.Outcome == GroundDuelOutcome.LooseBall &&
            looseBall.LooseBallSpeedMetersPerSecond > 2f,
            "Va chạm vai phải có thể làm bóng bật khỏi chân và trở thành bóng tự do có vận tốc.");

        GroundDuelContext foulContext = new(
            DefenderEngagementType.Tackle,
            closeTouch,
            74,
            74,
            70,
            72,
            72,
            78,
            75,
            78,
            75,
            5f,
            5f,
            1.3f,
            -0.6f,
            true,
            0.9f,
            0.4f,
            0.01f);
        Check(
            resolver.Resolve(foulContext).Outcome == GroundDuelOutcome.Foul,
            "Tắc bóng từ phía sau và sai thời điểm phải có thể tạo phạm lỗi.");
    }

    private static void VerifySequenceSpansMultipleTouches()
    {
        GroundDuelSequenceState sequence = new();
        sequence.Begin("carrier", "defender", false);
        sequence.RecordTouch(new DribbleTouchPlan(
            DribbleTouchType.CloseControl,
            new Vector2(0.51f, 0.50f),
            0.75f,
            0.44f,
            0.34f));
        sequence.RecordEngagement(new DefenderEngagementPlan(
            DefenderEngagementType.Jockey,
            new Vector2(0.52f, 0.50f),
            false));
        sequence.RecordTouch(new DribbleTouchPlan(
            DribbleTouchType.ChangeDirection,
            new Vector2(0.53f, 0.45f),
            1.25f,
            0.52f,
            0.52f));
        Check(
            sequence.TouchCount == 2 && sequence.ExchangeCount == 1 && sequence.HasDefender,
            "State của pha 1 đấu 1 phải giữ được nhiều nhịp chạm bóng trước khi phân thắng bại.");
    }

    private static void VerifyDistanceAndDirectionChangeTheContest()
    {
        GroundDuelResolver resolver = new();
        DribbleTouchPlan exposedTouch = new(
            DribbleTouchType.CloseControl,
            new Vector2(0.52f, 0.50f),
            0.75f,
            0.44f,
            0.34f);
        GroundDuelOutcome closeOutcome = resolver.Resolve(CreateDuelContext(
            DefenderEngagementType.Tackle,
            exposedTouch,
            timingRoll: 0.2f,
            outcomeRoll: 0.42f,
            foulRoll: 0.9f,
            challengeDistanceMeters: 0.9f)).Outcome;
        GroundDuelOutcome stretchedOutcome = resolver.Resolve(CreateDuelContext(
            DefenderEngagementType.Tackle,
            exposedTouch,
            timingRoll: 0.2f,
            outcomeRoll: 0.42f,
            foulRoll: 0.9f,
            challengeDistanceMeters: 1.7f)).Outcome;
        Check(
            closeOutcome == GroundDuelOutcome.DefenderWins && stretchedOutcome != closeOutcome,
            "Cùng cầu thủ và thời điểm, pha vào bóng đủ gần phải hiệu quả hơn pha với chân từ xa.");

        GroundDuelOutcome headOnOutcome = resolver.Resolve(CreateDuelContext(
            DefenderEngagementType.Tackle,
            exposedTouch,
            timingRoll: 0.2f,
            outcomeRoll: 0.49f,
            foulRoll: 0.9f,
            movementAlignment: -1f)).Outcome;
        GroundDuelOutcome sameDirectionOutcome = resolver.Resolve(CreateDuelContext(
            DefenderEngagementType.Tackle,
            exposedTouch,
            timingRoll: 0.2f,
            outcomeRoll: 0.49f,
            foulRoll: 0.9f,
            movementAlignment: 1f)).Outcome;
        Check(
            headOnOutcome != sameDirectionOutcome,
            "Hai cầu thủ lao ngược chiều và chạy cùng chiều phải tạo kết quả va chạm khác nhau.");
    }

    private static DefenderEngagementContext CreateDefenderContext(
        DribbleTouchType touchType,
        int exchangeCount,
        float distanceMeters,
        bool cooldown,
        bool backToGoal,
        float decisionRoll)
    {
        return new DefenderEngagementContext(
            new Vector2(0.52f, 0.50f),
            new Vector2(0.50f, 0.50f),
            new Vector2(0.99f, 0.50f),
            distanceMeters,
            touchType,
            exchangeCount,
            86,
            82,
            82,
            78,
            5f,
            5.2f,
            cooldown,
            backToGoal,
            true,
            decisionRoll);
    }

    private static GroundDuelContext CreateDuelContext(
        DefenderEngagementType engagementType,
        DribbleTouchPlan touch,
        float timingRoll,
        float outcomeRoll,
        float foulRoll,
        float challengeDistanceMeters = 0.9f,
        float movementAlignment = 0f)
    {
        return new GroundDuelContext(
            engagementType,
            touch,
            72,
            72,
            72,
            72,
            72,
            82,
            80,
            82,
            80,
            6f,
            6f,
            challengeDistanceMeters,
            movementAlignment,
            false,
            timingRoll,
            outcomeRoll,
            foulRoll);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
