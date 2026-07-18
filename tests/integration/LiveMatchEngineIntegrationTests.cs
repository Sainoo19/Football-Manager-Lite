using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class LiveMatchEngineIntegrationTests
{
    public static void Run()
    {
        VerifyHeadlessMatchCompletesDeterministically();
        VerifyPlaybackSpeedDoesNotChangeFullMatch();
        VerifySnapshotIsDetachedFromMutableEngineState();
        VerifyPitchAdapterMatchesDirectEngine();
        VerifyGoalkeeperCanConfrontControlledBall();
        GD.Print("PASS: LiveMatchEngine chạy headless, snapshot bất biến và adapter sân giữ nguyên kết quả.");
    }

    private static void VerifyGoalkeeperCanConfrontControlledBall()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation =
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071814);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(
            engine.StartScenario(MatchScenarioKind.CentralOneVersusOne),
            "Không dựng được tình huống kiểm tra thủ môn đối mặt người đang kiểm soát bóng.");

        StringName carrierId = engine.CurrentBallOwnerId;
        StringName attackingTeamId = engine.PlayerTeams[carrierId];
        StringName goalkeeperId = engine.PositionView.Keys.First(id =>
            engine.PlayerTeams[id] != attackingTeamId && engine.PlayerRoles[id] == "GK");
        StringName defendingTeamId = engine.PlayerTeams[goalkeeperId];
        Vector2 goalkeeperStart = engine.PositionView[goalkeeperId];
        float fieldDirection = goalkeeperStart.X < 0.5f ? 1f : -1f;
        Vector2 carrierPosition = new(
            goalkeeperStart.X + fieldDirection * (6f / FootballPitchDimensions.LengthMeters),
            0.50f);
        Vector2 goalkeeperPosition = new(
            carrierPosition.X - fieldDirection * (1.2f / FootballPitchDimensions.LengthMeters),
            0.50f);

        foreach (StringName playerId in engine.PositionView.Keys.ToArray())
        {
            if (engine.PlayerTeams[playerId] != defendingTeamId || engine.PlayerRoles[playerId] == "GK")
            {
                continue;
            }

            float stagedX = goalkeeperStart.X < 0.5f ? 0.55f : 0.45f;
            float stagedY = Mathf.Clamp(engine.PositionView[playerId].Y, 0.08f, 0.92f);
            engine.OverridePlayerPosition(playerId, new Vector2(stagedX, stagedY));
        }
        engine.OverridePlayerPosition(carrierId, carrierPosition);
        engine.OverridePlayerPosition(goalkeeperId, goalkeeperPosition);
        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Tình huống đối mặt phải chạy được.");

        bool observedGoalkeeperPressure = false;
        bool observedDecisiveOutcome = false;
        for (int step = 0; step < 80; step++)
        {
            engine.AdvanceGameTime(0.05d);
            observedGoalkeeperPressure |= engine.CurrentIntents.TryGetValue(
                goalkeeperId,
                out PlayerIntent? goalkeeperIntent) &&
                goalkeeperIntent.Kind == PlayerIntentKind.CloseDownBall;
            observedDecisiveOutcome |= engine.CurrentBallOwnerId != carrierId ||
                                      engine.IsBallInFlight ||
                                      engine.IsLooseBall ||
                                      engine.PendingRestartType != new StringName();
        }

        LiveMatchMetrics metrics = engine.GetSnapshot().Metrics;
        string activeDefenderRole = engine.PlayerRoles.TryGetValue(
            engine.ActiveGroundDuelDefenderId,
            out string? role)
            ? role
            : "none";
        Check(
            metrics.GroundDuelExchanges >= 1 && observedGoalkeeperPressure && observedDecisiveOutcome,
            "Thủ môn ở gần nhất phải được tính là đối thủ tranh bóng và kết thúc pha đối mặt thay vì đứng nhìn; " +
            $"nhịp={metrics.GroundDuelExchanges}, tắc={metrics.TackleAttempts}, " +
            $"chủ bóng={engine.CurrentBallOwnerId}, thủ môn={goalkeeperId}, " +
            $"người kèm={engine.ActiveGroundDuelDefenderId}, kiểu={engine.ActiveDefenderEngagement}, " +
            $"vai trò={activeDefenderRole}, " +
            $"hành động={engine.GetSnapshot().LastActionName}.");
    }

    private static void VerifyPlaybackSpeedDoesNotChangeFullMatch()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        HeadlessLiveMatchRunner runner = new();
        HeadlessLiveMatchResult fast = runner.RunToFullTime(
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071804),
            MatchPlaybackSpeed.Fastest,
            0.05d);
        HeadlessLiveMatchResult realTime = runner.RunToFullTime(
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071804),
            MatchPlaybackSpeed.RealTime,
            60d);

        Check(fast.Simulation.score_text() == realTime.Simulation.score_text() &&
              SnapshotsMatch(fast.FinalSnapshot, realTime.FinalSnapshot),
            "Realtime và tăng tốc phải tạo đúng cùng diễn biến với cùng seed.");
    }

    private static void VerifyHeadlessMatchCompletesDeterministically()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        HeadlessLiveMatchRunner runner = new();
        HeadlessLiveMatchResult first = runner.RunToFullTime(
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071801));
        HeadlessLiveMatchResult second = runner.RunToFullTime(
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071801));

        Check(first.Simulation.is_finished && first.Simulation.current_minute == 90,
            "Headless live engine phải chạy đủ một trận 90 phút mà không cần Godot Node.");
        Check(first.FinalSnapshot.Phase == LiveMatchPhase.FullTime,
            "Snapshot cuối của trận headless phải ở trạng thái hết trận.");
        Check(first.Simulation.score_text() == second.Simulation.score_text(),
            "Hai trận headless cùng seed phải có cùng tỷ số.");
        Check(SnapshotsMatch(first.FinalSnapshot, second.FinalSnapshot),
            "Hai trận headless cùng seed phải có cùng state cầu thủ, bóng và thống kê hành động.");
    }

    private static void VerifySnapshotIsDetachedFromMutableEngineState()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 2026071802);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        LiveMatchSnapshot before = engine.GetSnapshot();
        Vector2 originalBall = before.BallPosition;
        var originalPositions = before.Positions.ToDictionary(pair => pair.Key, pair => pair.Value);

        engine.Process(6d);
        LiveMatchSnapshot after = engine.GetSnapshot();
        Check(before.BallPosition.IsEqualApprox(originalBall) &&
              before.Positions.All(pair => pair.Value.IsEqualApprox(originalPositions[pair.Key])),
            "Snapshot cũ không được thay đổi khi engine tiếp tục chạy.");
        Check(!SnapshotsMatch(before, after),
            "Snapshot mới phải phản ánh diễn biến sau khi engine tiến thêm game-time.");
        Check(after.ElapsedGameSeconds >= 5.99d,
            "LiveMatchEngine trực tiếp phải tự quản lý game-time khi không gắn runtime bên ngoài.");
    }

    private static void VerifyPitchAdapterMatchesDirectEngine()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation engineSimulation =
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071803);
        FootballMatchSimulation viewSimulation =
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071803);
        engineSimulation.use_live_pitch_events = true;
        viewSimulation.use_live_pitch_events = true;

        LiveMatchEngine engine = new();
        engine.SetMatch(engineSimulation);
        engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play));
        MatchPitch2D pitch = new();
        pitch.SetMatch(viewSimulation);
        pitch.SetPlaying(true);
        for (int step = 0; step < 400; step++)
        {
            engine.AdvanceGameTime(0.05d);
            pitch.AdvanceGameTime(0.05d);
        }

        Check(SnapshotsMatch(engine.GetSnapshot(), pitch.GetSnapshot()),
            "Adapter MatchPitch2D không được làm thay đổi kết quả của LiveMatchEngine.");
        pitch.Free();
    }

    private static bool SnapshotsMatch(LiveMatchSnapshot first, LiveMatchSnapshot second)
    {
        return first.Positions.Count == second.Positions.Count &&
               first.Positions.All(pair =>
                   second.Positions.TryGetValue(pair.Key, out Vector2 other) &&
                   pair.Value.DistanceTo(other) < 0.0001f) &&
               first.BallPosition.DistanceTo(second.BallPosition) < 0.0001f &&
               first.BallOwnerId == second.BallOwnerId &&
               first.ActiveTeamId == second.ActiveTeamId &&
               first.Phase == second.Phase &&
               first.PendingRestartType == second.PendingRestartType &&
               first.Metrics.CompletedPasses == second.Metrics.CompletedPasses &&
               first.Metrics.PassAttempts == second.Metrics.PassAttempts &&
               first.Metrics.Interceptions == second.Metrics.Interceptions &&
               first.Metrics.Dribbles == second.Metrics.Dribbles &&
               first.Metrics.Restarts == second.Metrics.Restarts &&
               first.Metrics.AerialDuels == second.Metrics.AerialDuels &&
               first.Metrics.HeadersWon == second.Metrics.HeadersWon &&
               first.Metrics.AerialSecondBalls == second.Metrics.AerialSecondBalls &&
               first.Analytics.PossessionChanges == second.Analytics.PossessionChanges &&
               first.Analytics.Corners == second.Analytics.Corners &&
               first.Analytics.GoalKicks == second.Analytics.GoalKicks &&
               first.Analytics.ThrowIns == second.Analytics.ThrowIns &&
               first.Analytics.Goals.Count == second.Analytics.Goals.Count &&
               first.Metrics.ResolvedActions == second.Metrics.ResolvedActions;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
