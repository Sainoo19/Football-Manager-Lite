using System;
using System.Collections.Generic;
using Godot;

public static class Phase1RulesTests
{
    public static void Run()
    {
        VerifyRetreatingDefenderSprintsInsideDangerZone();
        VerifyRecoverySprintKeepsPaceWithRunner();
        VerifyCarrierNearGoalIsBlockedFromGoalSide();
        VerifyRollingBallBetweenPostsIsAGoal();
        VerifyNoChallengeFromBehindInsideOwnBox();
        VerifyOffsidePositionAtShotIsRemembered();
        VerifySomeRunnersLeaveEarly();
        VerifySubstitutionWaitsForStoppage();
        VerifyAiNeverRemovesGoalkeeper();
        VerifyStalledDuelDoesNotTrapTheBall();
        VerifyRestartPositionsDoNotOverlap();
        GD.Print("PASS: phase 1 defensive recovery, goal-side blocking and goal-line rule.");
    }

    private static void VerifyRetreatingDefenderSprintsInsideDangerZone()
    {
        DefensiveRecoveryConfiguration configuration = DefensiveRecoveryConfiguration.CreatePhase1Defaults();
        const float ownGoalX = 0.985f;
        Dictionary<StringName, Vector2> positions = new()
        {
            ["cb"] = new Vector2(0.80f, 0.5f),
            ["runner"] = new Vector2(0.86f, 0.45f)
        };
        PlayerIntent protectBox = new(PlayerIntentKind.HoldShape, new Vector2(0.92f, 0.5f),
            LiveTeamPhase.Defending, assignment: OffBallAssignmentKind.ProtectBox);
        Check(DefensiveUrgencyRules.IsUrgent(protectBox, positions["cb"], protectBox.Target,
                new Vector2(0.78f, 0.4f), ownGoalX, positions, configuration),
            "A defender who must retreat toward goal while the ball is 22 m away has to sprint.");
        Check(!DefensiveUrgencyRules.IsUrgent(protectBox, positions["cb"], protectBox.Target,
                new Vector2(0.40f, 0.4f), ownGoalX, positions, configuration),
            "A defender may jog back while the ball is far from goal.");

        PlayerIntent support = new(PlayerIntentKind.SupportBall, new Vector2(0.92f, 0.5f),
            LiveTeamPhase.Progression, assignment: OffBallAssignmentKind.OfferShortSupport);
        Check(!DefensiveUrgencyRules.IsUrgent(support, positions["cb"], support.Target,
                new Vector2(0.78f, 0.4f), ownGoalX, positions, configuration),
            "Attacking assignments are not defensive recovery runs.");

        PlayerIntent mark = new(PlayerIntentKind.MarkOpponent, new Vector2(0.82f, 0.46f),
            LiveTeamPhase.Defending, "runner", OffBallAssignmentKind.TrackRunner);
        Check(DefensiveUrgencyRules.IsUrgent(mark, positions["cb"], mark.Target,
                new Vector2(0.78f, 0.4f), ownGoalX, positions, configuration),
            "A marker whose runner is already goal-side must sprint to recover.");
    }

    private static void VerifyRecoverySprintKeepsPaceWithRunner()
    {
        FootballMovementController movement = new();
        Dictionary<StringName, Vector2> positions = new()
        {
            ["runner"] = new Vector2(0.50f, 0.40f),
            ["defender"] = new Vector2(0.50f, 0.60f)
        };
        Dictionary<StringName, Vector2> targets = new()
        {
            ["runner"] = new Vector2(0.90f, 0.40f),
            ["defender"] = new Vector2(0.90f, 0.60f)
        };
        Dictionary<StringName, PlayerIntent> intents = new()
        {
            ["runner"] = new PlayerIntent(PlayerIntentKind.RunIntoSpace, targets["runner"],
                LiveTeamPhase.Progression),
            ["defender"] = new PlayerIntent(PlayerIntentKind.HoldShape, targets["defender"],
                LiveTeamPhase.Defending, assignment: OffBallAssignmentKind.ProtectBox)
        };
        Dictionary<StringName, int> paces = new() { ["runner"] = 70, ["defender"] = 70 };
        HashSet<StringName> sprinters = new() { "defender" };
        float sprintSpeed = DefensiveRecoveryConfiguration.CreatePhase1Defaults().RecoverySprintSpeedMetersPerSecond;
        for (int step = 0; step < 30; step++)
        {
            movement.Advance(positions, targets, intents, paces, 0.1f, sprinters, sprintSpeed);
        }

        float runnerMeters = (positions["runner"].X - 0.50f) * FootballPitchDimensions.LengthMeters;
        float defenderMeters = (positions["defender"].X - 0.50f) * FootballPitchDimensions.LengthMeters;
        Check(defenderMeters >= runnerMeters * 0.98f,
            $"A sprinting defender must not lose ground to an equally quick runner ({defenderMeters:0.0} m vs {runnerMeters:0.0} m).");
    }

    private static void VerifyCarrierNearGoalIsBlockedFromGoalSide()
    {
        // The home carrier attacks the goal at x = 1; the away team defends it.
        FootballWorldSnapshot world = new FootballWorldSnapshotBuilder()
            .WithHomeTeam("away")
            .AddPlayer("owner", "home", "AM", new Vector2(0.80f, 0.45f))
            .AddPlayer("st", "home", "ST", new Vector2(0.90f, 0.55f))
            .AddPlayer("lw", "home", "LW", new Vector2(0.86f, 0.25f))
            .AddPlayer("cb1", "away", "CB", new Vector2(0.86f, 0.52f))
            .AddPlayer("cb2", "away", "CB", new Vector2(0.87f, 0.40f))
            .AddPlayer("lb", "away", "LB", new Vector2(0.84f, 0.25f))
            .AddPlayer("rb", "away", "RB", new Vector2(0.84f, 0.75f))
            .AddPlayer("dm", "away", "DM", new Vector2(0.76f, 0.47f))
            .AddPlayer("cm", "away", "CM", new Vector2(0.72f, 0.60f))
            .AddPlayer("keeper", "away", "GK", new Vector2(0.98f, 0.5f))
            .WithPossession("home", "owner", new Vector2(0.80f, 0.45f)).Build();
        Dictionary<StringName, PlayerIntent> intents = new();
        OffBallParticipationConfiguration configuration = OffBallParticipationConfiguration.CreateM3Defaults();
        new DefensiveAssignmentCoordinator(configuration).Assign(world, "away", LiveTeamPhase.Defending, intents);

        Vector2 ownGoal = world.OwnGoal("away");
        float ballDepth = Mathf.Abs(world.BallPosition.X - ownGoal.X);
        int blockers = 0;
        foreach ((StringName playerId, PlayerIntent intent) in intents)
        {
            if (intent.Assignment == OffBallAssignmentKind.PressBall)
            {
                Check(Mathf.Abs(intent.Target.X - ownGoal.X) < ballDepth,
                    "The presser must approach the carrier from the goal side.");
            }
            if (intent.Assignment != OffBallAssignmentKind.BlockShotLine)
            {
                continue;
            }
            blockers++;
            Vector2 expected = DefensiveBlockTargeter.ShotLineBlockTarget(
                world.BallPosition, ownGoal, configuration.ShotLineBlockOffsetMeters);
            Check(intent.Target.IsEqualApprox(expected) && Mathf.Abs(intent.Target.X - ownGoal.X) < ballDepth,
                "The shot-line blocker must stand between the carrier and the goal.");
        }
        Check(blockers == 1, $"A carrier 20 m from goal must face exactly one shot-line blocker (found {blockers}).");
    }

    private static void VerifyRollingBallBetweenPostsIsAGoal()
    {
        Check(GoalLineRule.CrossesBetweenPosts(new Vector2(0.995f, 0.52f), new Vector2(1.004f, 0.53f), 1f),
            "A rolling ball crossing the line inside the posts must count as a goal.");
        Check(!GoalLineRule.CrossesBetweenPosts(new Vector2(0.995f, 0.40f), new Vector2(1.004f, 0.39f), 1f),
            "A ball crossing the goal line wide of the post is a corner or goal kick.");
        Check(GoalLineRule.CrossesBetweenPosts(new Vector2(0.004f, 0.49f), new Vector2(-0.003f, 0.47f), 0f),
            "The rule applies to both goals.");
    }

    private static void VerifyNoChallengeFromBehindInsideOwnBox()
    {
        Vector2 ownGoal = new(0.985f, 0.5f);
        DefenderEngagementContext Context(Vector2 defender, DribbleTouchType touch) => new(
            defender, new Vector2(0.92f, 0.45f), ownGoal, 1.2f, touch, 3, 80, 70, 60, 60, 6f, 6f,
            false, false, true, 0.01f, isInsideOwnPenaltyArea: true, penaltyAreaChallengeProbability: 0.04f,
            distanceToOwnGoalMeters: 7f);
        DefenderEngagementPlanner planner = new();
        Check(planner.Plan(Context(new Vector2(0.905f, 0.45f), DribbleTouchType.CloseControl)).Type ==
              DefenderEngagementType.Recover,
            "A defender behind the carrier inside his own box must recover goal-side instead of tackling.");
        Check(planner.Plan(Context(new Vector2(0.935f, 0.45f), DribbleTouchType.CloseControl)).AttemptsChallenge,
            "A goal-side defender inside his own box may still tackle.");
    }

    private static void VerifyOffsidePositionAtShotIsRemembered()
    {
        // Home attacks toward x = 1. The striker stands beyond the second-last defender when the shot is taken.
        Dictionary<StringName, Vector2> positions = new()
        {
            ["shooter"] = new Vector2(0.80f, 0.45f),
            ["striker"] = new Vector2(0.93f, 0.55f),
            ["cb"] = new Vector2(0.88f, 0.50f),
            ["keeper"] = new Vector2(0.98f, 0.50f)
        };
        Dictionary<StringName, StringName> teams = new()
        {
            ["shooter"] = "home", ["striker"] = "home", ["cb"] = "away", ["keeper"] = "away"
        };
        OffsideExposureLedger ledger = new();
        ledger.Capture(new OffsideRule(), "home", "shooter", positions["shooter"], 1f, positions, teams);
        Check(ledger.IsExposed("striker"),
            "A striker beyond the second-last defender when a team-mate shoots must stay exposed to offside.");
        Check(!ledger.IsExposed("shooter"), "The player who plays the ball is never offside from his own touch.");
        ledger.Clear();
        Check(!ledger.IsExposed("striker"), "Deliberate play by the defending team ends the offside exposure.");
    }

    private static void VerifySomeRunnersLeaveEarly()
    {
        OffBallParticipationConfiguration configuration = OffBallParticipationConfiguration.CreateM3Defaults();
        int early = 0;
        const int samples = 400;
        for (int window = 0; window < samples; window++)
        {
            FootballWorldSnapshot world = new FootballWorldSnapshotBuilder()
                .WithHomeTeam("away")
                .AddPlayer("runner", "home", "ST", new Vector2(0.70f, 0.5f))
                .WithPossession("home", "runner", new Vector2(0.70f, 0.5f))
                .AtTime(window * configuration.EarlyRunWindowSeconds)
                .Build();
            float allowance = OnsideRunPlanner.EarlyRunAllowanceMeters(world, "runner", configuration);
            if (allowance > 0f)
            {
                early++;
                Check(allowance <= configuration.EarlyRunMaximumOvershootMeters + 0.66f,
                    "An early run must stay within the configured overshoot.");
            }
        }
        float rate = early / (float)samples;
        Check(Mathf.Abs(rate - configuration.EarlyRunProbability) < 0.08f,
            $"Runners must leave early at roughly the configured rate (observed {rate:0.00}).");
        Check(OnsideRunPlanner.EarlyRunAllowanceMeters(
                new FootballWorldSnapshotBuilder().WithHomeTeam("away")
                    .AddPlayer("runner", "home", "ST", new Vector2(0.70f, 0.5f))
                    .WithPossession("home", "runner", new Vector2(0.70f, 0.5f)).Build(),
                "runner", null) == 0f,
            "Without a configuration runners keep waiting for the release.");
    }

    private static void VerifySubstitutionWaitsForStoppage()
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 2026100901);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        LiveMatchScenarioRunner runner = new();
        runner.RunFor(engine, 6d, 0.05d);
        while (engine.PendingRestartType != new StringName())
        {
            runner.RunFor(engine, 0.5d, 0.05d);
        }

        StringName outgoing = new();
        foreach (FootballPlayer player in simulation.home.get_starter_players())
        {
            if (player.position != "GK")
            {
                outgoing = player.id;
                break;
            }
        }
        StringName incoming = simulation.home.get_substitute_players()[0].id;
        Check(simulation.make_substitution(simulation.home.team.id, outgoing, incoming) is not null,
            "The test substitution must be accepted.");
        engine.AnimateMinute(new Godot.Collections.Array<FootballMatchEvent>());
        Check(engine.PositionView.ContainsKey(outgoing) && !engine.PositionView.ContainsKey(incoming),
            "A substitution must not take effect while the ball is in play.");

        for (int step = 0; step < 12000 && engine.PendingRestartType == new StringName(); step++)
        {
            engine.AdvanceGameTime(0.05d);
        }
        Check(engine.PendingRestartType != new StringName(), "The test match must reach a stoppage.");
        Check(!engine.PositionView.ContainsKey(outgoing) && engine.PositionView.ContainsKey(incoming),
            "A pending substitution must take effect at the next stoppage.");
        simulation.Dispose();
    }

    private static void VerifyAiNeverRemovesGoalkeeper()
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 2026100902);
        simulation.use_live_pitch_events = true;
        simulation.ai_substitutions_for_home = true;
        while (simulation.current_minute < 85)
        {
            simulation.advance_minute();
        }
        foreach (MatchTeamState state in new[] { simulation.home, simulation.away })
        {
            int goalkeepers = 0;
            foreach (FootballPlayer player in state.get_starter_players())
            {
                goalkeepers += player.position == "GK" ? 1 : 0;
            }
            Check(state.substitutions_used == 3 && goalkeepers == 1,
                $"AI substitutions must keep exactly one goalkeeper on the pitch ({state.team.short_name}: " +
                $"subs={state.substitutions_used}, goalkeepers={goalkeepers}).");
        }
        simulation.Dispose();
    }

    // Regression for the poke loop: in 146 of 200 matches the ball stayed inside a 6 m circle for over 45 seconds
    // because a carrier whose ball was poked away collected it again 0.2 seconds later, about 14 times in a row.
    private static void VerifyStalledDuelDoesNotTrapTheBall()
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        foreach (long seed in new[] { 202612010000L, 202612010002L })
        {
            FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
            simulation.use_live_pitch_events = true;
            LiveMatchEngine engine = new();
            engine.SetMatch(simulation);
            EngineInvariantChecker checker = new();
            checker.Attach(engine);
            engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
            new LiveMatchScenarioRunner().RunFor(engine, 3000d, 0.05d);
            foreach (EngineInvariantViolation violation in checker.Violations)
            {
                Check(violation.Code != "I6_BALL_CONFINED",
                    $"Seed {seed}: the ball must not be trapped in one spot ({violation.FirstDetail}).");
            }
            simulation.Dispose();
        }
    }

    // Regression for a corner that waited over 60 seconds: two defenders were sent to spots 0.56 m apart, so one
    // was held 9.0 m from the ball and the corner could not be taken.
    private static void VerifyRestartPositionsDoNotOverlap()
    {
        TouchlineRestartPlanner planner = new();
        Vector2 corner = new(0.975f, 0.965f);
        Vector2 first = planner.KeepDefenderAway(new Vector2(0.93f, 0.90f), corner, isCorner: true);
        Vector2 clash = first + new Vector2(0.004f, 0f);
        Vector2 separated = planner.SeparateDefenderPosition(clash, corner, new[] { first });
        Check(FootballPitchDimensions.DistanceMeters(separated, first) >=
              TouchlineRestartPlanner.RestartPositionSpacingMeters - 0.01f,
            "Two defenders must not be given restart positions closer than they can physically stand.");
        Check(FootballPitchDimensions.DistanceMeters(separated, corner) >=
              TouchlineRestartPlanner.CornerDefenderDistanceMeters,
            "A separated defender must still respect the required distance from the corner.");
        Vector2 untouched = planner.SeparateDefenderPosition(first, corner, new[] { new Vector2(0.5f, 0.5f) });
        Check(untouched.IsEqualApprox(first), "A defender with a free position must not be moved.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
