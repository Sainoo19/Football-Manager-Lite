using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public static class M1ProductionPipelineIntegrationTests
{
    public static void Run()
    {
        VerifyProductionScenario(
            MatchScenarioKind.GoalkeeperBuildUp,
            2026080111,
            metrics => Count(metrics, FootballActionType.GoalkeeperDistribution) >= 1,
            "Thủ môn phải chọn distribution bằng production action pipeline.");
        VerifyProductionScenario(
            MatchScenarioKind.WingerCutBackDecision,
            2026080112,
            metrics => Count(metrics, FootballActionType.Cross) +
                       Count(metrics, FootballActionType.GroundPass) +
                       Count(metrics, FootballActionType.Carry) >= 1,
            "Cầu thủ cánh phải cân nhắc cross, phối hợp hoặc carry trong production pipeline.");
        VerifyProductionScenario(
            MatchScenarioKind.StrikerBackToGoalWithTwoOutlets,
            2026080113,
            metrics => Count(metrics, FootballActionType.ProtectBall) +
                       Count(metrics, FootballActionType.GroundPass) +
                       Count(metrics, FootballActionType.Carry) >= 1,
            "Tiền đạo quay lưng phải dùng protect, outlet hoặc carry từ production pipeline.");
        VerifyProductionScenario(
            MatchScenarioKind.CentralMidfielderLateBoxEntry,
            2026080114,
            metrics => metrics.ProgressiveActions >= 1,
            "CM băng lên muộn phải tạo ít nhất một action progressive bằng production pipeline.");
        GD.Print("PASS: scenario M1 dùng trực tiếp unified production pipeline cho GK, winger, ST và CM.");
    }

    private static void VerifyProductionScenario(
        MatchScenarioKind kind,
        long seed,
        Func<FootballActionMetricsSnapshot, bool> assertion,
        string message)
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.StartScenario(kind), $"Không dựng được scenario {MatchScenarioFactory.DisplayName(kind)}.");
        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Scenario M1 phải chạy được.");
        new LiveMatchScenarioRunner().RunFor(engine, 8d, 0.05d);

        FootballActionMetricsSnapshot metrics = engine.ActionMetrics;
        Check(
            metrics.Decisions > 0 && engine.LastActionDecision is not null,
            $"{MatchScenarioFactory.DisplayName(kind)} phải đi qua FootballActionCoordinator production.");
        Check(
            assertion(metrics),
            $"{message} decisions={metrics.Decisions}, " +
            $"GKDistribution={Count(metrics, FootballActionType.GoalkeeperDistribution)}, " +
            $"GroundPass={Count(metrics, FootballActionType.GroundPass)}, " +
            $"Hold={Count(metrics, FootballActionType.Hold)}, " +
            $"Clearance={Count(metrics, FootballActionType.Clearance)}, " +
            $"last={engine.LastActionDecision?.Selected.ActionType}.");
    }

    private static int Count(FootballActionMetricsSnapshot metrics, FootballActionType actionType)
    {
        return metrics.AttemptsByType.GetValueOrDefault(actionType);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
