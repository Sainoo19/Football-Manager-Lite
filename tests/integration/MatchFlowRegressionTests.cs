using System;
using Godot;

public static class MatchFlowRegressionTests
{
    public static void Run()
    {
        VerifyPenaltyRestartCompletesWithPhysicalGoalkeeperPosition();
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[1], teams[2], 202610080006);
        simulation.use_live_pitch_events = true;
        LiveMatchRuntime runtime = new();
        runtime.SetSpeed(MatchPlaybackSpeed.Fastest);
        LiveMatchEngine engine = new();
        engine.AttachRuntime(runtime);
        engine.SetMatch(simulation);
        runtime.Start();
        engine.SetPlaying(true);
        MatchFlowDiagnostics diagnostics = new();
        while (runtime.ElapsedGameSeconds < 1500d && !simulation.is_finished)
        {
            runtime.Advance(0.0005d);
            engine.AdvanceSynchronizedGameTime(runtime.LastAdvancedGameSeconds);
            diagnostics.Observe(engine);
            if (diagnostics.MaximumStationaryLooseBallSeconds > 30d)
            {
                throw new InvalidOperationException("Seed 202610080006 bị mắc bóng sát biên quá 30 giây.");
            }
            if (diagnostics.MaximumOwnerHoldSeconds > 30f)
            {
                throw new InvalidOperationException("Cầu thủ không được đứng giữ bóng quá 30 giây sau khi thu hồi.");
            }
        }
        if (engine.LooseBallRecoveries == 0)
        {
            throw new InvalidOperationException("Cần quan sát được cầu thủ thật sự thu hồi bóng tự do.");
        }
        simulation.Dispose();
        GD.Print("PASS: seed ít diễn biến tiếp tục chơi và thu hồi bóng sát biên.");
    }

    private static void VerifyPenaltyRestartCompletesWithPhysicalGoalkeeperPosition()
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 202610080000);
        MatchFlowDiagnostics diagnostics = new();
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        engine.SetPlaying(true);
        engine.ScheduleRestart("penalty", teams[0].id, new Vector2(0.1f, 0.5f));
        for (int step = 0; step < 1200 && engine.PenaltiesTaken == 0; step++)
        {
            engine.AdvanceGameTime(0.05d);
            diagnostics.Observe(engine);
            if (diagnostics.MaximumRestartWaitSeconds > 60d)
            {
                throw new InvalidOperationException("Penalty không được chờ vô hạn khi thủ môn đã đứng đúng vị trí.");
            }
        }
        if (engine.PenaltiesTaken == 0)
        {
            throw new InvalidOperationException("Penalty fixture phải thực sự thực hiện cú đá qua production engine.");
        }
        simulation.Dispose();
    }
}
