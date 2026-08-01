using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class M2PhaseIntegrationTests
{
    public static void Run()
    {
        VerifyThreeVersusTwoStartsAsRealCounterAttack();
        VerifyDefendingTeamPressesCoversAndRecoversAfterTurnover();
        VerifyFinalThirdKeepsRestDefence();
        VerifyCounterAttackCanReorganizeThroughProductionEngine();
        VerifyBuildUpCanProgressThroughTwoLines();
        GD.Print("PASS: M2 production engine đồng bộ counterattack, defensive transition và rest-defence cho cả đội.");
    }

    private static void VerifyThreeVersusTwoStartsAsRealCounterAttack()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            2026080201);
        StringName homeTeamId = simulation.home.team.id;
        StringName awayTeamId = simulation.away.team.id;
        Check(engine.CurrentTeamPhaseStates[homeTeamId].Phase == LiveTeamPhase.CounterAttack,
            "Scenario 3v2 phải dùng production phase CounterAttack sau turnover.");
        Check(engine.CurrentTeamPhaseStates[awayTeamId].Phase == LiveTeamPhase.TransitionToDefence,
            "Đội phòng ngự trong 3v2 phải vào TransitionToDefence.");
        simulation.Dispose();
    }

    private static void VerifyDefendingTeamPressesCoversAndRecoversAfterTurnover()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            2026080202);
        StringName defendingTeamId = simulation.away.team.id;
        int pressers = engine.CurrentIntents.Count(pair =>
            engine.PlayerTeams[pair.Key] == defendingTeamId && pair.Value.Kind == PlayerIntentKind.PressBall);
        int covers = engine.CurrentIntents.Count(pair =>
            engine.PlayerTeams[pair.Key] == defendingTeamId && pair.Value.Kind == PlayerIntentKind.CoverPress);
        int recoveries = engine.CurrentIntents.Count(pair =>
            engine.PlayerTeams[pair.Key] == defendingTeamId && pair.Value.Kind == PlayerIntentKind.RecoverGoalSide);
        Check(pressers == 1 && covers >= 1 && recoveries >= 1,
            $"Turnover phải có nearest pressure, cover và recovery run; press={pressers}, cover={covers}, recover={recoveries}.");
        simulation.Dispose();
    }

    private static void VerifyFinalThirdKeepsRestDefence()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.WingerCutBackDecision,
            2026080203);
        StringName attackingTeamId = simulation.home.team.id;
        TeamPhaseState phase = engine.CurrentTeamPhaseStates[attackingTeamId];
        int restDefence = engine.CurrentIntents.Count(pair =>
            engine.PlayerTeams[pair.Key] == attackingTeamId &&
            pair.Value.TeamPhase == LiveTeamPhase.RestDefence);
        Check(phase.Phase == LiveTeamPhase.FinalThird &&
              restDefence >= phase.RequiredRestDefencePlayers,
            $"FinalThird phải giữ đủ rest-defence; phase={phase.Phase}, required={phase.RequiredRestDefencePlayers}, actual={restDefence}.");
        simulation.Dispose();
    }

    private static void VerifyCounterAttackCanReorganizeThroughProductionEngine()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            2026080204);
        StringName attackingTeamId = simulation.home.team.id;
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        new LiveMatchScenarioRunner().RunFor(engine, 12d, 0.05d);
        TeamPhaseMetricsSnapshot metrics = engine.TeamPhaseMetrics;
        bool leftCounter = metrics.TransitionCounts.Keys.Any(key =>
            key.StartsWith("CounterAttack->", StringComparison.Ordinal));
        Check(metrics.CounterAttacks >= 1 &&
              (leftCounter || engine.CurrentTeamPhaseStates[attackingTeamId].Phase != LiveTeamPhase.CounterAttack),
            "Counterattack mất lợi thế phải tái tổ chức qua production phase pipeline.");
        simulation.Dispose();
    }

    private static void VerifyBuildUpCanProgressThroughTwoLines()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateScenario(
            MatchScenarioKind.GoalkeeperBuildUp,
            2026080205);
        StringName attackingTeamId = simulation.home.team.id;
        Check(engine.CurrentTeamPhaseStates[attackingTeamId].Phase == LiveTeamPhase.BuildUp,
            "Build-up từ thủ môn phải bắt đầu ở phase BuildUp.");
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        new LiveMatchScenarioRunner().RunFor(engine, 45d, 0.05d);
        bool reachedProgression = engine.TeamPhaseMetrics.TransitionCounts.Keys.Any(key =>
            key == "BuildUp->Progression" || key == "BuildUp->FinalThird");
        Check(engine.CompletedPasses >= 2 && reachedProgression,
            $"Build-up phải chuyền qua ít nhất hai tuyến và vào Progression; passes={engine.CompletedPasses}, " +
            $"phase={engine.CurrentTeamPhaseStates[attackingTeamId].Phase}.");
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
