using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class FootballFundamentalsIntegrationTests
{
    public static void Run()
    {
        VerifyCornerWaitsForTheTaker();
        VerifyThrowInUsesASeparateDelivery();
        VerifyDisplacedGoalkeeperCannotCatchAnAccurateShot();
        VerifyRestartContinuityInPreviouslyQuietMatch();
        GD.Print("PASS: production restart chờ người thực hiện, ném biên riêng và thủ môn chỉ bắt trong tầm với.");
    }

    private static void VerifyRestartContinuityInPreviouslyQuietMatch()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[3], teams[2], 202610080003);
        simulation.use_live_pitch_events = true;
        LiveMatchRuntime runtime = new();
        runtime.SetSpeed(MatchPlaybackSpeed.Fastest);
        LiveMatchEngine engine = new();
        engine.AttachRuntime(runtime);
        engine.SetMatch(simulation);
        runtime.Start();
        engine.SetPlaying(true);
        double waitingSeconds = 0d;
        StringName previousRestart = new();
        while (runtime.ElapsedGameSeconds < 6000d && !simulation.is_finished)
        {
            runtime.Advance(0.05d);
            engine.AdvanceSynchronizedGameTime(runtime.LastAdvancedGameSeconds);
            StringName restart = engine.PendingRestartType;
            waitingSeconds = restart != new StringName() && restart == previousRestart
                ? waitingSeconds + runtime.LastAdvancedGameSeconds
                : 0d;
            previousRestart = restart;
            if (waitingSeconds > 60d)
            {
                string positions = string.Join("; ", engine.PositionView.Select(pair =>
                    $"{pair.Key}:{FootballPitchDimensions.DistanceMeters(pair.Value, engine.BallPosition):0.00}m target={FootballPitchDimensions.DistanceMeters(pair.Value, engine.TargetPositionView[pair.Key]):0.00}m"));
                throw new InvalidOperationException($"Restart {restart} bị chờ quá 60 giây tại {runtime.ElapsedGameSeconds:0}s: {positions}");
            }
        }
        simulation.Dispose();
    }

    private static void VerifyCornerWaitsForTheTaker()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateEngine(202610080101);
        engine.SetPlaying(true);
        engine.AnimateMinute(new Array<FootballMatchEvent>
        {
            new FootballMatchEvent().setup(1, "corner", "Phạt góc kiểm thử", simulation.home.team.id)
        });
        Vector2 restartPosition = engine.BallPosition;
        new LiveMatchScenarioRunner().RunFor(engine, 3.1d);
        Check(engine.PendingRestartType == "corner" && !engine.IsBallInFlight,
            "Thời gian chuẩn bị hết nhưng người thực hiện còn xa thì phạt góc phải tiếp tục chờ.");
        bool delivered = false;
        for (int step = 0; step < 400; step++)
        {
            engine.AdvanceGameTime(0.1d);
            if (engine.IsBallInFlight && engine.BallActionType == "Cross")
            {
                delivered = true;
                Check(FootballPitchDimensions.DistanceMeters(
                        engine.PositionView[engine.LastBallTouchPlayerId], restartPosition) <= 0.81f,
                    "Bóng chỉ được rời góc sân khi người đá đã chạy tới bóng.");
                break;
            }
        }
        Check(delivered && engine.CornersTaken == 1, "Phạt góc phải thực hiện đúng một lần sau khi sẵn sàng.");
        simulation.Dispose();
    }

    private static void VerifyThrowInUsesASeparateDelivery()
    {
        (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateEngine(202610080102);
        engine.SetPlaying(true);
        engine.AnimateMinute(new Array<FootballMatchEvent>
        {
            new FootballMatchEvent().setup(1, "throw_in", "Ném biên kiểm thử", simulation.home.team.id)
        });
        int passesBefore = engine.PassAttempts;
        bool delivered = false;
        for (int step = 0; step < 400; step++)
        {
            engine.AdvanceGameTime(0.1d);
            if (engine.IsBallInFlight && engine.BallActionType == "ThrowIn")
            {
                delivered = true;
                Check(FootballPitchDimensions.DistanceMeters(engine.BallFlightStart, engine.BallFlightTarget) <= 24.01f,
                    "Ném biên phải đưa bóng tới đồng đội trong tầm ném.");
                Check(engine.LastBallTouchTeamId == simulation.home.team.id,
                    "Người ném phải được ghi nhận là người chạm bóng cuối.");
                break;
            }
        }
        Check(delivered && engine.ThrowInsTaken == 1 && engine.PassAttempts == passesBefore,
            "Ném biên phải có đường bóng riêng, không trở thành rê bóng hoặc cộng vào số đường chuyền.");
        simulation.Dispose();
    }

    private static void VerifyDisplacedGoalkeeperCannotCatchAnAccurateShot()
    {
        int accurateShots = 0;
        for (int seed = 0; seed < 4; seed++)
        {
            (LiveMatchEngine engine, FootballMatchSimulation simulation) = CreateEngine(202610080200 + seed);
            Check(engine.StartScenario(MatchScenarioKind.StrikerBackToGoalWithTwoOutlets), "Không tạo được scenario sút.");
            StringName shooterId = engine.CurrentBallOwnerId;
            StringName goalkeeperId = engine.PlayerRoles.Keys.First(id =>
                engine.PlayerTeams[id] == simulation.away.team.id && engine.PlayerRoles[id] == "GK");
            float direction = engine.PositionView[goalkeeperId].X < 0.5f ? -1f : 1f;
            Vector2 goalkeeperPosition = new(direction > 0f ? 0.96f : 0.04f, 0.9f);
            engine.OverridePlayerPosition(shooterId, new Vector2(direction > 0f ? 0.9f : 0.1f, 0.5f));
            int index = 0;
            foreach (StringName playerId in engine.PlayerTeams.Keys.Where(id =>
                         engine.PlayerTeams[id] == simulation.away.team.id && id != goalkeeperId).ToArray())
            {
                engine.OverridePlayerPosition(playerId, new Vector2(0.55f, 0.10f + index++ * 0.06f));
            }
            engine.OverridePlayerPosition(goalkeeperId, goalkeeperPosition);
            FootballPlayer? shooter = simulation.home.team.get_player(shooterId);
            if (shooter is not null)
            {
                shooter.finishing = 95;
            }
            engine.SetPlaying(true);
            bool shotStarted = false;
            int goalsBefore = simulation.home.stats["goals"].AsInt32();
            for (int step = 0; step < 30; step++)
            {
                engine.OverridePlayerPosition(goalkeeperId, goalkeeperPosition);
                engine.AdvanceGameTime(0.1d);
                if (engine.IsBallInFlight && engine.BallActionType == "Shot")
                {
                    shotStarted = true;
                }
                if (shotStarted && !engine.IsBallInFlight)
                {
                    break;
                }
            }
            Check(shotStarted && engine.CurrentBallOwnerId != goalkeeperId,
                "Thủ môn bị giữ xa đường bóng không được nhận bóng sau cú sút.");
            accurateShots += simulation.home.stats["goals"].AsInt32() - goalsBefore;
            simulation.Dispose();
        }
        Check(accurateShots >= 1, "Một cú sút trúng khung thành trống phải ghi bàn qua production engine.");
    }

    private static (LiveMatchEngine Engine, FootballMatchSimulation Simulation) CreateEngine(long seed)
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], seed);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
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
