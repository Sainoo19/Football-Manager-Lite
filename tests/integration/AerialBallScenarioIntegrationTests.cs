using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class AerialBallScenarioIntegrationTests
{
    public static void Run()
    {
        VerifyAerialScenario(
            MatchScenarioKind.LoftedPassAerialDuel,
            "LoftedPass",
            2026071821);
        VerifyAerialScenario(
            MatchScenarioKind.AerialCrossIntoBox,
            "Cross",
            2026071822);
        VerifyAerialScenario(
            MatchScenarioKind.AerialClearanceUnderPressure,
            "Clearance",
            2026071823);
        VerifyGroundThroughBallStaysOnTheGround();
        GD.Print("PASS: chuyền bổng, tạt bóng và phá bóng bổng chạy bằng vật lý điểm rơi trong live engine.");
    }

    private static void VerifyAerialScenario(
        MatchScenarioKind kind,
        string expectedOpeningAction,
        int seed)
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.StartScenario(kind), $"Không dựng được {MatchScenarioFactory.DisplayName(kind)}.");
        Check(
            engine.IsAerialBall && engine.BallActionType == expectedOpeningAction,
            $"{MatchScenarioFactory.DisplayName(kind)} phải mở đầu bằng đúng loại bóng bổng.");
        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Sandbox bóng bổng phải chạy được.");

        float maximumHeight = 0f;
        bool observedRisingBall = false;
        bool observedFallingBall = false;
        bool observedBothTeamsAtLanding = false;
        for (int step = 0; step < 180; step++)
        {
            engine.AdvanceGameTime(0.05d);
            maximumHeight = Mathf.Max(maximumHeight, engine.BallHeightMeters);
            observedRisingBall |= engine.IsAerialBall &&
                                  engine.BallVerticalVelocityMetersPerSecond > 0.1f;
            observedFallingBall |= engine.IsAerialBall &&
                                   engine.BallVerticalVelocityMetersPerSecond < -0.1f;
            int aerialContestants = engine.CurrentIntents.Values.Count(intent =>
                intent.Kind is PlayerIntentKind.ContestAerialBall or PlayerIntentKind.ClaimAerialBall);
            observedBothTeamsAtLanding |= aerialContestants >= 2;
        }

        LiveMatchMetrics metrics = engine.GetSnapshot().Metrics;
        Check(maximumHeight > 0.9f, "Bóng bổng phải có độ cao vật lý quan sát được theo mét.");
        Check(
            observedRisingBall && observedFallingBall,
            "Live engine phải quan sát được cả pha bóng đi lên và rơi xuống.");
        Check(
            observedBothTeamsAtLanding,
            "Cầu thủ hai đội phải chạy tới điểm rơi thay vì chỉ receiver được chọn di chuyển.");
        Check(metrics.AerialDuels >= 1, "Mỗi sandbox bóng bổng phải tạo ít nhất một pha không chiến.");
        Check(
            metrics.HeadersWon + metrics.GoalkeeperAerialCatches +
            metrics.GoalkeeperPunches + metrics.AerialSecondBalls >= 1,
            "Pha không chiến phải kết thúc bằng đánh đầu, thủ môn xử lý hoặc bóng hai.");
    }

    private static void VerifyGroundThroughBallStaysOnTheGround()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(
            teams[0],
            teams[1],
            2026071824);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(
            engine.StartScenario(MatchScenarioKind.ThroughBallBreakaway),
            "Phải dựng được sandbox chọc khe sệt để đối chứng.");
        Check(!engine.IsAerialBall, "Chọc khe sệt không được dùng state vật lý bóng bổng.");
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        engine.AdvanceGameTime(0.20d);
        Check(
            engine.BallHeightMeters <= 0.001f &&
            Mathf.Abs(engine.BallVerticalVelocityMetersPerSecond) <= 0.001f,
            "Chuyền sệt phải giữ Height và vận tốc thẳng đứng bằng 0.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
