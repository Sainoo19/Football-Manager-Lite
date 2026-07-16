using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    public override void _Ready() => Callable.From(RunTests).CallDeferred();

    private void RunTests()
    {
        try
        {
            TestSquadLimits();
            TestMatchSimulation();
            TestPitchMovement();
            TestUiIntegration();
            GD.Print("PASS: toàn bộ logic, giao diện, sân 2D và kiểm thử đang chạy bằng C#/.NET.");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL: {exception.Message}\n{exception.StackTrace}");
            GetTree().Quit(1);
        }
    }

    private static void TestSquadLimits()
    {
        var players = new Array<FootballPlayer>();
        for (int index = 0; index < 35; index++)
            players.Add(new FootballPlayer().setup($"test_{index:00}", $"Cầu thủ {index:00}", "CM", 20, "Việt Nam", 50 + index));
        var catalog = new FormationCatalog();
        var manager = new LineupManager();
        var squad = new MatchSquad();
        FormationDefinition formation = catalog.find("4_3_3");
        manager.auto_build(squad, formation, players);
        Check(players.Count == 35, "Quân số toàn đội phải được giữ nguyên.");
        Check(squad.starter_ids.Count == 11, "Phải có đúng 11 cầu thủ đá chính.");
        Check(squad.substitute_ids.Count == 12, "Chỉ được có tối đa 12 dự bị.");
        Check(squad.starter_slots.Count == 11, "Phải xếp đủ 11 vị trí trên sân.");
        Check(squad.registered_count() == 23, "Danh sách trận phải có 23 cầu thủ.");
        Check(!squad.register_substitute(players[0].id), "Không được đăng ký dự bị thứ 13.");
        Check(squad.validate_against(players).Length == 0, "Danh sách tự chọn phải hợp lệ.");
        foreach (FormationDefinition item in catalog.all()) Check(item.slots.Count == 11, "Mỗi sơ đồ phải có 11 vị trí.");
        GD.Print("PASS: quân số không giới hạn, danh sách trận 11 + 12.");
    }

    private static void TestMatchSimulation()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        Check(teams.Count >= 2, "Cần ít nhất hai đội để kiểm tra trận đấu.");
        FootballMatchSimulation first = new FootballMatchSimulation().setup(teams[0], teams[1], 20260716);
        FootballMatchSimulation second = new FootballMatchSimulation().setup(teams[0], teams[1], 20260716);
        first.simulate_to_end();
        second.simulate_to_end();
        Check(first.is_finished && first.current_minute == 90, "Trận đấu phải kết thúc sau 90 phút.");
        Check(first.score_text() == second.score_text() && first.events.Count == second.events.Count, "Cùng seed phải cho cùng kết quả.");
        Check(first.events[^1].event_type == "full_time", "Sự kiện cuối phải là hết trận.");
        Check(first.home.stats["shots_on_target"].AsInt32() <= first.home.stats["shots"].AsInt32(), "Sút trúng đích không thể vượt tổng cú sút.");
        Check(Math.Abs(first.get_possession(first.home) + first.get_possession(first.away) - 100) <= 1, "Tổng kiểm soát bóng phải xấp xỉ 100%.");

        FootballMatchSimulation interactive = new FootballMatchSimulation().setup(teams[0], teams[1], 99);
        for (int count = 0; count < 5; count++)
        {
            StringName outgoing = interactive.home.squad.starter_ids[0];
            StringName incoming = interactive.home.squad.substitute_ids[0];
            Check(interactive.make_substitution(teams[0].id, outgoing, incoming) is not null, "Năm quyền thay người đầu tiên phải hợp lệ.");
        }
        Check(interactive.home.substitutions_used == 5, "Phải sử dụng được đúng 5 quyền thay người.");
        Check(interactive.make_substitution(teams[0].id, interactive.home.squad.starter_ids[0], interactive.home.squad.substitute_ids[0]) is null, "Quyền thay người thứ 6 phải bị từ chối.");
        Check(interactive.change_mentality(teams[0].id, "attacking") is not null && interactive.home.mentality == "attacking", "Phải đổi được tâm lý thi đấu.");
        GD.Print("PASS: mô phỏng 90 phút, thống kê, chiến thuật và thay người.");
    }

    private void TestPitchMovement()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 42);
        var pitch = new MatchPitch2D();
        AddChild(pitch);
        pitch.SetMatch(simulation);
        Check(pitch.CurrentPositions.Count == 22, "Sân 2D phải hiển thị đủ 22 cầu thủ.");
        var initial = pitch.CurrentPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        Array<FootballMatchEvent> events = simulation.advance_minute();
        pitch.AnimateMinute(events);
        pitch._Process(0.35);
        int moving = pitch.CurrentPositions.Count(pair => pair.Value.DistanceTo(initial[pair.Key]) > 0.001f);
        Check(moving >= 18, "Phần lớn cầu thủ phải chuyển động liên tục.");
        Check(pitch.BallPosition.X is >= 0 and <= 1 && pitch.BallPosition.Y is >= 0 and <= 1, "Bóng phải nằm trong vùng mô phỏng.");
        pitch.QueueFree();
        GD.Print("PASS: sân 2D đủ 22 cầu thủ và có chuyển động.");
    }

    private void TestUiIntegration()
    {
        var scene = GD.Load<PackedScene>("res://scenes/main.tscn");
        var main = scene.Instantiate<Main>();
        AddChild(main);
        Check(main.teams.Count == 4, "UI phải nhận đủ bốn đội từ C#.");
        main.ChooseSelectedTeam();
        Check(main.managed_team is not null, "UI phải chọn được CLB.");
        main.ShowMatchView();
        main.MatchView.PrepareNewMatch();
        Check(main.MatchView.simulation is not null, "Match Center phải tạo được engine C#.");
        main.MatchView.SimulateToEnd();
        Check(main.MatchView.simulation!.is_finished, "Match Center phải mô phỏng hết trận.");
        main.QueueFree();
        GD.Print("PASS: UI C# hoạt động xuyên suốt với lõi .NET.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
