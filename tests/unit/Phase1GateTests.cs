using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public static class Phase1GateTests
{
    public static void Run()
    {
        VerifyFreeBoxShotDefinition();
        VerifyPhase1ProfileCoversDutyAndPlausibilityGates();
        VerifyInvariantCheckerObservesWithoutChangingTheMatch();
        GD.Print("PASS: phase 1 gates — duty metrics, acceptance profile and invariant checker.");
    }

    private static void VerifyFreeBoxShotDefinition()
    {
        // The home side shoots at the goal on x = 1 from 12 m.
        Vector2 origin = new(1f - 12f / FootballPitchDimensions.LengthMeters, 0.5f);
        LiveShotPlayer keeper = new("gk", "away", "GK", new Vector2(0.985f, 0.5f), default, "Goalkeep", "");
        LiveShotPlayer Defender(string id, Vector2 position) =>
            new(id, "away", "CB", position, default, "ProtectBox", "");
        Vector2 farAway = new(0.60f, 0.20f);
        Vector2 withinThreeMetres = origin + new Vector2(0f, 2f / FootballPitchDimensions.WidthMeters);
        Vector2 onTheLine = new(origin.X + 5f / FootballPitchDimensions.LengthMeters, 0.5f);

        Phase1DutyMetrics free = Phase1DutyMetrics.FromShots(new[] { Shot(origin, keeper, Defender("d1", farAway)) });
        Check(free.BoxShots == 1 && free.FreeBoxShots == 1,
            "A box shot with no defender near the shooter or on the line to goal is a free shot.");
        Check(free.TeamRecoveryRate == 0d && free.GoalkeeperInBoxRate == 1d,
            "One distant defender is not a recovered team; a keeper on his line is inside his box.");

        Phase1DutyMetrics pressed = Phase1DutyMetrics.FromShots(
            new[] { Shot(origin, keeper, Defender("d1", withinThreeMetres)) });
        Check(pressed.FreeBoxShots == 0, "A defender within 3 m of the shooter makes the shot contested.");

        Phase1DutyMetrics blocked = Phase1DutyMetrics.FromShots(
            new[] { Shot(origin, keeper, Defender("d1", onTheLine)) });
        Check(blocked.FreeBoxShots == 0, "A defender on the line between ball and goal makes the shot contested.");

        LiveShotRecord penalty = Shot(origin, keeper, Defender("d1", farAway)) with { Situation = "penalty" };
        Check(Phase1DutyMetrics.FromShots(new[] { penalty }).BoxShots == 0, "Penalties are not duty observations.");
    }

    private static LiveShotRecord Shot(Vector2 origin, params LiveShotPlayer[] defenders)
    {
        List<LiveShotPlayer> players = new(defenders)
        {
            new LiveShotPlayer("shooter", "home", "ST", origin, default, "CarryBall", "")
        };
        return new LiveShotRecord(0d, "shooter", "home", origin, new Vector2(0.994f, 0.52f),
            new Vector2(0.985f, 0.5f), 12f, "saved", true, 0.5f, Vector2.Zero, "open_play",
            PlayersAtLaunch: players);
    }

    private static void VerifyPhase1ProfileCoversDutyAndPlausibilityGates()
    {
        LiveMatchBalanceConfiguration configuration = LiveMatchBalanceConfiguration.CreatePhase1();
        Check(configuration.IsPhase1, "The phase 1 profile must identify itself.");
        Check(configuration.MetricRanges.Values.Count(range => range.Gate == "C") == 6,
            "Gate C must define the six role-duty measurements.");
        Check(configuration.MetricRanges["free_box_shots"].Maximum == 5d,
            "The agreed free box shot threshold is 5 per match.");
        Check(configuration.MetricRanges.Values.Where(range => range.Gate == "D").Select(range => range.Key)
                .ToHashSet().IsSupersetOf(new[] { "goals", "shots", "penalties", "offsides" }),
            "Gate D must bound goals, shots, penalties and offsides.");
        Check(!LiveMatchBalanceConfiguration.CreateFootballFundamentalsV1().IsPhase1,
            "The professional profile stays available for later phases.");
    }

    private static void VerifyInvariantCheckerObservesWithoutChangingTheMatch()
    {
        (string plainSignature, _) = PlayTenMinutes(attachChecker: false);
        (string checkedSignature, EngineInvariantChecker? checker) = PlayTenMinutes(attachChecker: true);
        Check(plainSignature == checkedSignature,
            "Attaching the invariant checker must not change how a match unfolds.");
        Check(checker is not null && checker.Violations.All(violation => violation.Code != "CHECKER_ERROR"),
            "The invariant checker must not fail internally.");
    }

    private static (string Signature, EngineInvariantChecker? Checker) PlayTenMinutes(bool attachChecker)
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 2026101001);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        EngineInvariantChecker? checker = null;
        if (attachChecker)
        {
            checker = new EngineInvariantChecker();
            checker.Attach(engine);
        }
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        new LiveMatchScenarioRunner().RunFor(engine, 600d, 0.05d);
        LiveMatchSnapshot snapshot = engine.GetSnapshot();
        string signature = string.Join("|", simulation.events.Select(matchEvent =>
            $"{matchEvent.event_type}:{matchEvent.player_id}")) +
            $"|{snapshot.BallPosition}|{snapshot.Metrics.PassAttempts}|{snapshot.Metrics.GroundDuelExchanges}";
        simulation.Dispose();
        return (signature, checker);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
