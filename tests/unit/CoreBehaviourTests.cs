using System;
using System.Collections.Generic;
using Godot;

public static class CoreBehaviourTests
{
    public static void Run()
    {
        VerifyBoxDefendingAndSpacing();
        VerifyImmediateDangerIsPressed();
        VerifyMarkerFollowsMovingReceiver();
        VerifyRunsWaitForReleaseInBothDirections();
        VerifyUncontestedAerialReception();
        VerifyExposedTouchCanBeChallengedImmediately();
        VerifyBlockedWideAttackCanRecycle();
        VerifyEngagementBelongsToTheCurrentDefender();
        VerifyProductionDribbleKeepsDefendersOnThePitch();
        GD.Print("PASS: core defending, live marking, onside runs, exposed touches and aerial reception.");
    }

    private static void VerifyBoxDefendingAndSpacing()
    {
        FootballWorldSnapshot world = BoxWorld();
        Vector2 shape = DefensiveBlockTargeter.ShapeTarget(world, "cb", "away");
        Check(shape.X > world.BallPosition.X,
            "A defender must recover goal-side when an attacker is inside the six-yard area.");
        Vector2 mark = DefensiveBlockTargeter.MarkTarget(world, "cb", "away", "runner");
        Check(mark.X > world.Positions["runner"].X &&
              FootballPitchDimensions.DistanceMeters(mark, world.Positions["runner"]) < 1.5f,
            "A dangerous receiver must have a close goal-side marking target.");
        Dictionary<StringName, PlayerIntent> intents = new()
        {
            ["cb"] = new PlayerIntent(PlayerIntentKind.MarkOpponent, mark, LiveTeamPhase.Defending,
                "runner", OffBallAssignmentKind.TrackRunner),
            ["cover"] = new PlayerIntent(PlayerIntentKind.CoverPress, mark + new Vector2(0.01f, 0f),
                LiveTeamPhase.Defending, "dm", OffBallAssignmentKind.CoverPresser)
        };
        TeamSpacingResolver.Resolve(world, intents);
        Check(intents["cb"].Target.IsEqualApprox(mark),
            "Spacing must not pull a marker away from the receiver to satisfy attacking team spacing.");
    }

    private static void VerifyImmediateDangerIsPressed()
    {
        FootballWorldSnapshot world = BoxWorld();
        Dictionary<StringName, Vector2> positions = (Dictionary<StringName, Vector2>)world.Positions;
        positions["cb"] = world.BallPosition + new Vector2(0.012f, 0f);
        positions["dm"] = world.BallPosition - new Vector2(0.10f, 0f);
        Dictionary<StringName, PlayerIntent> intents = new();
        new DefensiveAssignmentCoordinator(OffBallParticipationConfiguration.CreateM3Defaults())
            .Assign(world, "away", LiveTeamPhase.Defending, intents);
        Check(intents["cb"].Assignment == OffBallAssignmentKind.PressBall,
            "A CB next to a close-range shooter must intervene instead of waiting for a distant midfielder.");
    }

    private static void VerifyMarkerFollowsMovingReceiver()
    {
        FootballWorldSnapshot world = BoxWorld(ballX: 0.82f);
        OffBallIntentCoordinator coordinator = new(new FootballIntentPlanner());
        Dictionary<StringName, PlayerIntent> first = coordinator.Plan(world);
        StringName marker = new();
        foreach ((StringName id, PlayerIntent intent) in first)
        {
            if (intent.Assignment == OffBallAssignmentKind.TrackRunner && intent.RelatedPlayerId == "runner")
            {
                marker = id;
                break;
            }
        }
        Check(marker != new StringName(), "The dangerous receiver must be assigned a marker before cover/lane roles.");
        Dictionary<StringName, Vector2> positions = (Dictionary<StringName, Vector2>)world.Positions;
        positions["runner"] += new Vector2(0f, 0.035f);
        Dictionary<StringName, PlayerIntent> second = coordinator.Plan(AtTime(world, 0.4f));
        Check(second[marker].RelatedPlayerId == "runner" &&
              !second[marker].Target.IsEqualApprox(first[marker].Target),
            "Commitment must retain the marked player while refreshing the moving target.");
    }

    private static void VerifyRunsWaitForReleaseInBothDirections()
    {
        foreach (bool left in new[] { false, true })
        {
            float X(float x) => left ? 1f - x : x;
            FootballWorldSnapshot source = new FootballWorldSnapshotBuilder()
                .AddPlayer("owner", "home", "CM", new Vector2(X(0.72f), 0.4f))
                .AddPlayer("runner", "home", "ST", new Vector2(X(0.81f), 0.5f))
                .AddPlayer("cb", "away", "CB", new Vector2(X(0.84f), 0.5f))
                .AddPlayer("keeper", "away", "GK", new Vector2(X(0.98f), 0.5f))
                .WithPossession("home", "owner", new Vector2(X(0.72f), 0.4f)).Build();
            FootballWorldSnapshot world = AtTime(source, 0f, left);
            Vector2 destination = new(X(0.94f), 0.5f);
            Dictionary<StringName, PlayerIntent> intents = new()
            {
                ["runner"] = new PlayerIntent(PlayerIntentKind.RunIntoSpace, destination,
                    LiveTeamPhase.FinalThird, assignment: OffBallAssignmentKind.AttackBoxNearPost)
            };
            OnsideRunPlanner.ConstrainTargets(world, intents);
            float direction = left ? -1f : 1f;
            Check(direction * intents["runner"].Target.X < direction * source.Positions["cb"].X,
                "A runner must wait behind the second-last opponent before release.");
            intents["runner"] = new PlayerIntent(PlayerIntentKind.RunIntoSpace, destination, LiveTeamPhase.FinalThird);
            OnsideRunPlanner.ConstrainTargets(AtTime(source, 0f, left, flight: true), intents);
            Check(intents["runner"].Target.IsEqualApprox(destination),
                "After release, the runner must be free to attack beyond the offside line.");
            Dictionary<StringName, Vector2> positions = (Dictionary<StringName, Vector2>)source.Positions;
            positions["owner"] = new Vector2(X(0.91f), 0.4f);
            FootballWorldSnapshot advancedBall = new(source.Positions, source.BasePositions, source.PlayerTeams,
                source.PlayerRoles, positions["owner"], destination, "owner", new StringName(),
                "home", "home", false, false, left);
            OnsideRunPlanner.ConstrainTargets(advancedBall, intents);
            Check(direction * intents["runner"].Target.X > direction * positions["cb"].X,
                "The ball, when beyond the defensive line, must set the legal waiting depth.");
        }
    }

    private static void VerifyUncontestedAerialReception()
    {
        AerialDuelCandidate Candidate(string id, bool attacking, float distance) => new(
            id, attacking, false, "CM", 72, 72, 72, 72, 72, 10, distance, 0f, 40f, true, 0.5f);
        AerialDuelCandidate[] free = { Candidate("receiver", true, 0.8f), Candidate("far", false, 5f) };
        AerialDuelResolver resolver = new();
        Check(!AerialDuelResolver.IsContested(free) &&
              resolver.Resolve(free, 0, 0.4f, true).Outcome == AerialDuelOutcome.ControlledReception,
            "An unopposed aerial arrival must allow control and must not count as a duel.");
        AerialDuelCandidate[] contest = { Candidate("receiver", true, 0.8f), Candidate("defender", false, 1f) };
        Check(AerialDuelResolver.IsContested(contest) &&
              resolver.Resolve(contest, 1, 0.4f, true).Outcome != AerialDuelOutcome.ControlledReception,
            "Opponents within contest range must still compete for the ball.");
        Check(!AerialDuelResolver.IsContested(Array.Empty<AerialDuelCandidate>()) &&
              resolver.Resolve(Array.Empty<AerialDuelCandidate>(), 0, 0.4f, true).Outcome ==
              AerialDuelOutcome.LooseSecondBall, "A missed arrival must produce a second ball.");
    }

    private static void VerifyExposedTouchCanBeChallengedImmediately()
    {
        DefenderEngagementPlan result = new DefenderEngagementPlanner().Plan(new DefenderEngagementContext(
            new Vector2(0.6f, 0.5f), new Vector2(0.61f, 0.5f), new Vector2(1f, 0.5f),
            1.1f, DribbleTouchType.KnockOn, 0, 80, 80, 80, 80, 4f, 4f,
            false, false, true, 0.01f));
        Check(result.AttemptsChallenge,
            "A defender in reach must be able to challenge an exposed touch without waiting two exchanges.");
    }

    private static void VerifyBlockedWideAttackCanRecycle()
    {
        PassOptionEvaluator evaluator = new();
        Check(evaluator.CanConsider("LW", "LB", 0.93f, -12f, 16f, 0.2f, true),
            "A winger near the byline must keep a safe short recycle outlet behind the ball.");
        Check(!evaluator.CanConsider("LW", "CB", 0.93f, -28f, 40f, 0.2f, true) &&
              !evaluator.CanConsider("LW", "LB", 0.93f, -12f, 16f, 0.8f, true),
            "A distant retreat or blocked recycle lane must still be rejected.");
    }

    private static void VerifyEngagementBelongsToTheCurrentDefender()
    {
        GroundDuelSequenceState state = new();
        state.Begin("carrier", "first", false);
        Check(!state.HasEngagement, "A newly attached defender has no engagement target yet.");
        state.RecordEngagement(new DefenderEngagementPlan(DefenderEngagementType.Jockey,
            new Vector2(0.6f, 0.5f), false));
        state.AttachDefender("first");
        Check(state.HasEngagement, "The same defender must retain the current engagement.");
        state.AttachDefender("second");
        Check(!state.HasEngagement, "A new defender must not inherit another defender's target.");
        state.Reset();
        Check(!state.HasEngagement, "Reset must remove the engagement along with the participants.");
    }

    private static void VerifyProductionDribbleKeepsDefendersOnThePitch()
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 202610080004);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.StartScenario(MatchScenarioKind.WideOneVersusOne), "The dribble regression fixture must start.");
        engine.SetPlaying(true);
        bool observedDribble = false;
        for (int step = 0; step < 400; step++)
        {
            engine.AdvanceGameTime(0.05d);
            observedDribble |= engine.ActiveDribbleTouch.HasValue;
            foreach (Vector2 target in engine.TargetPositionView.Values)
            {
                Check(target.X > 0f && target.Y > 0f,
                    "A production defender must never receive the uninitialized corner target (0,0).");
            }
        }
        Check(observedDribble, "The fixture must exercise a real production dribble.");
        simulation.Dispose();
    }

    private static FootballWorldSnapshot BoxWorld(float ballX = 0.94f) => new FootballWorldSnapshotBuilder()
        .WithHomeTeam("away")
        .AddPlayer("owner", "home", "LW", new Vector2(ballX, 0.35f))
        .AddPlayer("runner", "home", "ST", new Vector2(0.93f, 0.5f))
        .AddPlayer("cb", "away", "CB", new Vector2(0.90f, 0.5f))
        .AddPlayer("cover", "away", "CB", new Vector2(0.88f, 0.65f))
        .AddPlayer("dm", "away", "DM", new Vector2(ballX - 0.01f, 0.35f))
        .AddPlayer("lane", "away", "CM", new Vector2(0.82f, 0.6f))
        .AddPlayer("keeper", "away", "GK", new Vector2(0.98f, 0.5f))
        .WithPossession("home", "owner", new Vector2(ballX, 0.35f)).Build();

    private static FootballWorldSnapshot AtTime(FootballWorldSnapshot world, float time,
        bool? left = null, bool flight = false) => new(world.Positions, world.BasePositions,
        world.PlayerTeams, world.PlayerRoles, world.BallPosition, world.BallDestination,
        world.BallOwnerId, world.ExpectedReceiverId, world.PossessionTeamId, world.HomeTeamId,
        flight, world.IsLooseBall, left ?? world.HomeAttacksLeft, false, false, null, null, time);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
