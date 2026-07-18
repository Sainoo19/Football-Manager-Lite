using System;
using System.Linq;
using Godot;
using Godot.Collections;

public static class PressureReleaseScenarioIntegrationTests
{
    public static void Run()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation =
            new FootballMatchSimulation().setup(teams[0], teams[1], 2026071815);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        Check(
            engine.StartScenario(MatchScenarioKind.TwoAttackersVersusOneDefender),
            "Không dựng được sandbox kiểm tra thoát pressing.");

        StringName carrierId = engine.CurrentBallOwnerId;
        StringName attackingTeamId = engine.PlayerTeams[carrierId];
        Vector2 carrierPosition = engine.PositionView[carrierId];
        StringName supportingAttackerId = engine.PositionView.Keys
            .Where(id => id != carrierId &&
                         engine.PlayerTeams[id] == attackingTeamId &&
                         engine.PlayerRoles[id] != "GK")
            .OrderBy(id => FootballPitchDimensions.DistanceMeters(
                carrierPosition,
                engine.PositionView[id]))
            .First();
        StringName pressingDefenderId = engine.PositionView.Keys
            .Where(id => engine.PlayerTeams[id] != attackingTeamId &&
                         engine.PlayerRoles[id] != "GK")
            .OrderBy(id => FootballPitchDimensions.DistanceMeters(
                carrierPosition,
                engine.PositionView[id]))
            .First();

        float direction = engine.PositionView.Keys
            .Where(id => engine.PlayerTeams[id] != attackingTeamId && engine.PlayerRoles[id] == "GK")
            .Select(id => engine.PositionView[id].X > carrierPosition.X ? 1f : -1f)
            .First();
        engine.OverridePlayerPosition(
            supportingAttackerId,
            new Vector2(
                carrierPosition.X + direction * (7f / FootballPitchDimensions.LengthMeters),
                Mathf.Clamp(carrierPosition.Y + 0.15f, 0.12f, 0.88f)));
        engine.OverridePlayerPosition(
            pressingDefenderId,
            new Vector2(
                carrierPosition.X + direction * (1.15f / FootballPitchDimensions.LengthMeters),
                carrierPosition.Y - 0.01f));

        Check(engine.Execute(new LiveMatchCommand(LiveMatchCommandKind.Play)), "Sandbox thoát pressing phải chạy được.");
        for (int step = 0; step < 60 && engine.PassAttempts == 0; step++)
        {
            engine.AdvanceGameTime(0.05d);
        }

        LiveMatchMetrics metrics = engine.GetSnapshot().Metrics;
        Check(
            metrics.PassAttempts >= 1,
            "Khi bị kèm sát nhưng có đồng đội trống tạo lợi thế, người giữ bóng phải cân nhắc chuyền " +
            "trước khi mắc kẹt trong vòng lặp tranh chấp.");
        Check(
            metrics.GroundDuelExchanges <= 2,
            $"Lối thoát hợp lệ phải được dùng trước một chuỗi tranh chấp kéo dài; " +
            $"đã có {metrics.GroundDuelExchanges} nhịp trước đường chuyền đầu tiên.");

        GD.Print("PASS: sandbox 2v1 xác nhận người cầm bóng có thể thoát kèm bằng một đường chuyền thật của engine.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
