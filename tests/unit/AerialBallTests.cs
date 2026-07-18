using System;
using System.Collections.Generic;
using Godot;

public static class AerialBallTests
{
    public static void Run()
    {
        VerifyBallisticHeightAndVerticalVelocity();
        VerifyLandingPredictionUsesArrivalTime();
        VerifyAerialDuelDoesNotReserveTheBallForAReceiver();
        VerifyHeadersAndGoalkeeperActions();
        GD.Print("PASS: vật lý bóng bổng, điểm rơi, không chiến và quyết định thủ môn hoạt động độc lập.");
    }

    private static void VerifyBallisticHeightAndVerticalVelocity()
    {
        AerialBallTrajectoryPlanner planner = new();
        Vector2 start = new(0.25f, 0.15f);
        Vector2 landing = new(0.72f, 0.52f);
        AerialBallTrajectory cross = planner.Plan(start, landing, AerialDeliveryType.Cross);
        AerialBallSample rising = cross.Sample(cross.FlightTimeSeconds * 0.25f);
        AerialBallSample falling = cross.Sample(cross.FlightTimeSeconds * 0.75f);
        AerialBallSample landed = cross.Sample(cross.FlightTimeSeconds);

        Check(cross.ApexHeightMeters > 1f, "Quả tạt phải thực sự rời mặt đất hơn một mét.");
        Check(
            rising.HeightMeters > 0f && rising.VerticalVelocityMetersPerSecond > 0f,
            "Nửa đầu quỹ đạo bóng bổng phải có độ cao tăng và vận tốc dọc hướng lên.");
        Check(
            falling.HeightMeters > 0f && falling.VerticalVelocityMetersPerSecond < 0f,
            "Nửa sau quỹ đạo phải có vận tốc dọc hướng xuống.");
        Check(
            landed.HasLanded && landed.HeightMeters == 0f &&
            landed.Position.DistanceTo(landing) < 0.0001f,
            "Quỹ đạo phải kết thúc đúng điểm rơi trên mặt sân.");

        AerialBallTrajectory clearance = planner.Plan(
            start,
            landing,
            AerialDeliveryType.Clearance);
        AerialBallTrajectory headerPass = planner.Plan(
            start,
            new Vector2(0.34f, 0.20f),
            AerialDeliveryType.HeaderPass);
        Check(
            clearance.FlightTimeSeconds > headerPass.FlightTimeSeconds &&
            clearance.ApexHeightMeters > headerPass.ApexHeightMeters,
            "Phá bóng bổng và đánh đầu chuyền phải có hai profile chuyến bay khác nhau.");
    }

    private static void VerifyLandingPredictionUsesArrivalTime()
    {
        AerialLandingPredictor predictor = new();
        Vector2 landing = new(0.60f, 0.50f);
        AerialArrivalEstimate closePlayer = predictor.Estimate(
            new Vector2(0.55f, 0.50f),
            landing,
            70,
            1.2f);
        AerialArrivalEstimate distantPlayer = predictor.Estimate(
            new Vector2(0.30f, 0.10f),
            landing,
            70,
            1.2f);
        Check(
            closePlayer.CanContest && closePlayer.ArrivalMarginSeconds > 0f,
            "Cầu thủ đủ gần phải dự đoán được điểm rơi và đến trước bóng.");
        Check(
            !distantPlayer.CanContest && distantPlayer.ArrivalMarginSeconds < 0f,
            "Cầu thủ ở quá xa không được dịch chuyển hoặc được chọn thẳng làm người nhận.");
    }

    private static void VerifyAerialDuelDoesNotReserveTheBallForAReceiver()
    {
        AerialDuelResolver resolver = new();
        List<AerialDuelCandidate> candidates = new()
        {
            CreateCandidate(
                "designated_attacker",
                true,
                false,
                "ST",
                58,
                56,
                58,
                60,
                0.8f,
                0.1f,
                12f,
                0.20f),
            CreateCandidate(
                "strong_defender",
                false,
                false,
                "CB",
                88,
                87,
                86,
                84,
                0.9f,
                0.05f,
                90f,
                0.75f)
        };
        AerialDuelResolution result = resolver.Resolve(candidates, 0, 0.4f);
        Check(
            result.WinnerId == new StringName("strong_defender") &&
            result.Outcome == AerialDuelOutcome.DefensiveHeaderClearance,
            "Cầu thủ được nhắm tới không được mặc định thắng bóng trước hậu vệ không chiến tốt hơn.");
    }

    private static void VerifyHeadersAndGoalkeeperActions()
    {
        AerialDuelResolver resolver = new();
        List<AerialDuelCandidate> striker = new()
        {
            CreateCandidate(
                "striker",
                true,
                false,
                "ST",
                90,
                92,
                86,
                88,
                0.4f,
                0.2f,
                10f,
                0.50f)
        };
        Check(
            resolver.Resolve(striker, 0, 0.2f).Outcome == AerialDuelOutcome.HeaderShot,
            "Tiền đạo thắng bóng gần khung thành phải có thể đánh đầu dứt điểm.");

        List<AerialDuelCandidate> goalkeeper = new()
        {
            CreateCandidate(
                "goalkeeper",
                false,
                true,
                "GK",
                45,
                90,
                82,
                91,
                0.3f,
                0.3f,
                95f,
                0.50f,
                92)
        };
        Check(
            resolver.Resolve(goalkeeper, 0, 0.1f).Outcome == AerialDuelOutcome.GoalkeeperCatch,
            "Thủ môn ít chịu áp lực phải có thể lao ra bắt gọn bóng bổng.");
        Check(
            resolver.Resolve(goalkeeper, 3, 0.95f).Outcome == AerialDuelOutcome.GoalkeeperPunch,
            "Khi đông người và khó bắt, thủ môn phải có thể chọn đấm bóng.");
        Check(
            resolver.Resolve(Array.Empty<AerialDuelCandidate>(), 0, 0.5f).Outcome ==
            AerialDuelOutcome.LooseSecondBall,
            "Không ai tới được điểm rơi thì bóng phải trở thành bóng hai.");
    }

    private static AerialDuelCandidate CreateCandidate(
        string id,
        bool isAttackingTeam,
        bool isGoalkeeper,
        string role,
        int heading,
        int jumpingReach,
        int strength,
        int positioning,
        float distanceToLandingMeters,
        float arrivalMarginSeconds,
        float distanceToAttackingGoalMeters,
        float contestRoll,
        int goalkeeping = 10)
    {
        return new AerialDuelCandidate(
            id,
            isAttackingTeam,
            isGoalkeeper,
            role,
            heading,
            jumpingReach,
            strength,
            positioning,
            82,
            goalkeeping,
            distanceToLandingMeters,
            arrivalMarginSeconds,
            distanceToAttackingGoalMeters,
            true,
            contestRoll);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
