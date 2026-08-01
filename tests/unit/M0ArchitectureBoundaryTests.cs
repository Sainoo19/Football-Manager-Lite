using System;
using Godot;

public static class M0ArchitectureBoundaryTests
{
    public static void Run()
    {
        VerifyEngineIdentityIsStable();
        VerifyStablePlayerAttributes();
        VerifyCollaboratorFacadesPreserveCurrentBehavior();
        VerifySharedLiveFixturesBuildValidMatches();
        GD.Print("PASS: M0 khóa metadata, fixture và ranh giới collaborator mà không đổi hành vi bóng đá.");
    }

    private static void VerifyStablePlayerAttributes()
    {
        FootballPlayer player = new FootballPlayer().setup(
            "stable_player",
            "Stable Player",
            "CM",
            24,
            "Việt Nam",
            70);
        Check(
            player.pace == 68,
            "Thuộc tính phát sinh từ ID phải dùng stable hash, không phụ thuộc process runtime.");
    }

    private static void VerifyEngineIdentityIsStable()
    {
        LiveMatchEngineConfiguration configuration =
            LiveMatchEngineConfiguration.CreateFootballFundamentalsV1();
        string first = LiveMatchEngineIdentity.ConfigurationFingerprint(configuration);
        string second = LiveMatchEngineIdentity.ConfigurationFingerprint(
            LiveMatchEngineConfiguration.CreateFootballFundamentalsV1());
        LiveMatchEngine engine = new(configuration);
        Check(
            first == second && first.Length == 64 && engine.ConfigurationFingerprint == first,
            "Configuration fingerprint phải ổn định và được engine công bố.");
        Check(
            engine.EngineVersion == LiveMatchEngineIdentity.EngineVersion,
            "Engine version phải dùng một nguồn metadata duy nhất.");
    }

    private static void VerifyCollaboratorFacadesPreserveCurrentBehavior()
    {
        LiveMatchEngineConfiguration configuration =
            LiveMatchEngineConfiguration.CreateFootballFundamentalsV1();
        TeamPhaseCoordinator phaseCoordinator = new();
        Check(
            phaseCoordinator.PlanningInterval(configuration, true, false) ==
            configuration.LooseBallPlanningIntervalSeconds,
            "Phase facade phải giữ interval loose-ball hiện tại.");
        Check(
            new RestartCoordinator().PreparationDuration("goal_kick", default) ==
            GoalKickRestartPlanner.PreparationDurationSeconds,
            "Restart facade phải giữ preparation duration hiện tại.");

        FootballActionContext actionContext = new(
            "actor",
            "team",
            "CM",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.99f, 0.5f),
            LiveTeamPhase.InPossession,
            0.5f,
            false,
            8f,
            false,
            false,
            0.5f,
            2f,
            1,
            70,
            70,
            70,
            75,
            65,
            8f,
            0f,
            0.02f,
            new System.Collections.Generic.List<FootballPassOption>(),
            default,
            default,
            null,
            new StringName(),
            42u,
            1);
        FootballActionCoordinator actionCoordinator = new(configuration.ActionSelection);
        Check(
            actionCoordinator.Decide(actionContext).Selected.ActionType == FootballActionType.Carry,
            "Action facade M1 phải tạo và chọn candidate từ cùng context production.");

        DribbleTouchPlan touch = new(
            DribbleTouchType.CloseControl,
            new Vector2(0.51f, 0.5f),
            0.6f,
            0.2f,
            0.4f);
        GroundDuelContext duelContext = new(
            DefenderEngagementType.Contain,
            touch,
            70,
            70,
            70,
            70,
            70,
            70,
            70,
            70,
            70,
            4f,
            4f,
            2f,
            0f,
            false,
            0.5f,
            0.5f,
            0.5f);
        Check(
            new BallContestCoordinator(new GroundDuelResolver())
                .ResolveGroundDuel(duelContext).Outcome == GroundDuelOutcome.NoChallenge,
            "Contest facade phải giữ outcome resolver hiện tại.");

        FootballWorldSnapshot world = new FootballWorldSnapshotBuilder()
            .AddPlayer("home_owner", "home", "CM", new Vector2(0.45f, 0.50f))
            .AddPlayer("home_runner", "home", "ST", new Vector2(0.60f, 0.50f))
            .AddPlayer("away_defender", "away", "CB", new Vector2(0.72f, 0.50f))
            .AddPlayer("away_goalkeeper", "away", "GK", new Vector2(0.96f, 0.50f))
            .WithPossession("home", "home_owner", new Vector2(0.45f, 0.50f))
            .Build();
        Check(
            new OffBallIntentCoordinator(new FootballIntentPlanner()).Plan(world).Count == 4,
            "Off-ball facade phải trả intent cho mọi cầu thủ trong snapshot.");
    }

    private static void VerifySharedLiveFixturesBuildValidMatches()
    {
        FootballTeam home = new LiveMatchTestTeamBuilder("fixture_home")
            .AddStandardEleven()
            .Build();
        FootballTeam away = new LiveMatchTestTeamBuilder("fixture_away")
            .AddStandardEleven()
            .Build();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(home, away, 2026080100);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Fixture match phải chạy được.");
        LiveMatchSnapshot snapshot = new LiveMatchScenarioRunner().RunFor(engine, 0.5d);
        Check(snapshot.Positions.Count == 22, "Fixture team builder phải tạo đủ 22 cầu thủ trên sân.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
