using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class M3OffBallIntegrationTests
{
    public static void Run()
    {
        VerifyCounterAttackAllocatesMultipleDistinctParticipants();
        VerifyWideAttackUsesProductionBoxOccupation();
        VerifyOffBallMetricsAreProducedByLiveEngine();
        GD.Print("PASS: M3 production engine phân công runner/support/box/rest-defence động và xuất telemetry.");
    }

    private static void VerifyCounterAttackAllocatesMultipleDistinctParticipants()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            2026080301);
        StringName attackingTeamId = simulation.home.team.id;
        int involved = engine.CurrentIntents.Count(pair =>
            engine.PlayerTeams[pair.Key] == attackingTeamId && pair.Value.Assignment is
                OffBallAssignmentKind.OfferShortSupport or OffBallAssignmentKind.OfferThirdManSupport or
                OffBallAssignmentKind.RunBehind or OffBallAssignmentKind.RunAcrossDefender);
        int distinctZones = engine.CurrentIntents
            .Where(pair => engine.PlayerTeams[pair.Key] == attackingTeamId && pair.Value.Assignment is
                OffBallAssignmentKind.OfferShortSupport or OffBallAssignmentKind.OfferThirdManSupport or
                OffBallAssignmentKind.RunBehind or OffBallAssignmentKind.RunAcrossDefender)
            .Select(pair => pair.Value.TargetKey)
            .Distinct(StringComparer.Ordinal)
            .Count();
        Check(involved >= 3 && distinctZones >= 2,
            $"Phản công 3v2 phải có nhiều support/runner ở các zone khác nhau; involved={involved}, zones={distinctZones}.");
        simulation.Dispose();
    }

    private static void VerifyWideAttackUsesProductionBoxOccupation()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.WingerCutBackDecision,
            2026080302);
        OffBallAssignmentKind[] assignments = engine.CurrentIntents.Values.Select(intent => intent.Assignment).ToArray();
        Check(assignments.Contains(OffBallAssignmentKind.AttackBoxNearPost) &&
              assignments.Contains(OffBallAssignmentKind.AttackBoxFarPost) &&
              assignments.Contains(OffBallAssignmentKind.AttackBoxCutBackZone),
            "Scenario tấn công biên phải dùng production allocator cho near/far/cut-back occupation.");
        simulation.Dispose();
    }

    private static void VerifyOffBallMetricsAreProducedByLiveEngine()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            2026080303);
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        new LiveMatchScenarioRunner().RunFor(engine, 10d, 0.05d);
        OffBallMetricsSnapshot metrics = engine.OffBallMetrics;
        Check(metrics.Observations > 0 && metrics.AverageTeamWidthMeters > 0f &&
              metrics.AveragePassingOptions > 0f && metrics.AverageRunnerLaneDiversity > 0f,
            "Live engine phải xuất width, passing options và runner lane diversity từ production planner.");
        simulation.Dispose();
    }

    private static (LiveMatchEngine Engine, FootballMatchSimulation Simulation) CreateScenario(
        MatchScenarioKind kind,
        long seed)
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.StartScenario(kind), $"Không dựng được scenario {MatchScenarioFactory.DisplayName(kind)}.");
        return (engine, simulation);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
