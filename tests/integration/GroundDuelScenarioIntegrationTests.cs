using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

public static class GroundDuelScenarioIntegrationTests
{
    public static void Run()
    {
        VerifyScenario(MatchScenarioKind.CentralOneVersusOne, 2026071811, false);
        VerifyScenario(MatchScenarioKind.WideOneVersusOne, 2026071812, false);
        VerifyScenario(MatchScenarioKind.StrikerBackToGoalOneVersusOne, 2026071813, true);
        GD.Print("PASS: sandbox 1v1 trung lộ, ngoài biên và quay lưng đều chạy qua nhiều nhịp.");
    }

    private static void VerifyScenario(MatchScenarioKind kind, int seed, bool expectsShielding)
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.StartScenario(kind), $"Không dựng được sandbox {MatchScenarioFactory.DisplayName(kind)}.");
        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Sandbox 1v1 phải chạy được.");

        HashSet<DribbleTouchType> observedTouches = new();
        bool observedCover = false;
        for (int step = 0; step < 160; step++)
        {
            engine.AdvanceGameTime(0.05d);
            if (engine.ActiveDribbleTouch.HasValue)
            {
                observedTouches.Add(engine.ActiveDribbleTouch.Value);
            }
            observedCover |= engine.CurrentIntents.Values.Any(
                intent => intent.Kind == PlayerIntentKind.CoverPress);
        }

        LiveMatchMetrics metrics = engine.GetSnapshot().Metrics;
        Check(
            metrics.GroundDuelExchanges >= 2 && metrics.MaxGroundDuelTouches >= 2,
            $"{MatchScenarioFactory.DisplayName(kind)} phải kéo dài ít nhất hai nhịp chạm và hai lần đối đầu; " +
            $"hiện có chạm={metrics.MaxGroundDuelTouches}, nhịp={metrics.GroundDuelExchanges}.");
        Check(
            observedTouches.Count >= 1,
            $"{MatchScenarioFactory.DisplayName(kind)} phải hiển thị được ý định chạm bóng của người rê bóng.");
        if (expectsShielding)
        {
            Check(
                observedTouches.Contains(DribbleTouchType.Shield) ||
                observedTouches.Contains(DribbleTouchType.HoldUp),
                "Tiền đạo quay lưng phải được quan sát đang che bóng hoặc giảm tốc chờ hỗ trợ.");
        }
        Check(
            metrics.TackleAttempts + metrics.ShoulderChallenges <=
            Math.Max(1, (metrics.GroundDuelExchanges + 1) / 2),
            $"Hậu vệ trong {MatchScenarioFactory.DisplayName(kind)} đang lao vào tranh bóng quá liên tục.");
        Check(
            float.IsPositiveInfinity(metrics.MinimumObservedGroundDuelSeparationMeters) ||
            metrics.MinimumObservedGroundDuelSeparationMeters >=
            DuelDistanceRules.MinimumPlayerSeparationMeters - 0.01f,
            $"Người rê bóng không được chạy xuyên hậu vệ trong {MatchScenarioFactory.DisplayName(kind)}.");
        Check(
            observedCover,
            $"Khi một hậu vệ áp sát trong {MatchScenarioFactory.DisplayName(kind)}, khối phòng ngự phải có người bọc lót.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
