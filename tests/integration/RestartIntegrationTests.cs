using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    private void TestKickoffGoalResetAndHalfTimeSides()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 303);
        simulation.use_live_pitch_events = true;
        MatchPitch2D pitch = new();
        AddChild(pitch);
        pitch.SetMatch(simulation);

        CheckTeamIsInOwnHalf(pitch, simulation.home.squad.starter_ids, false,
            "Đội chủ nhà phải đứng trong phần sân nhà trước lúc giao bóng.");
        CheckTeamIsInOwnHalf(pitch, simulation.away.squad.starter_ids, true,
            "Đội khách phải đứng trong phần sân nhà trước lúc giao bóng.");
        Check(pitch.BallPosition.IsEqualApprox(new Vector2(0.5f, 0.5f)),
            "Bóng phải nằm ở chấm giữa sân trước trận đấu.");
        Check(
            pitch.IsKickoffPassPending && pitch.KickoffReceiverId != new StringName(),
            "Giao bóng phải chuẩn bị sẵn người nhận đường chuyền mở màn.");
        pitch.SetPlaying(true);
        pitch._Process(0.40d);
        Check(
            !pitch.IsKickoffPassPending &&
            pitch.IsBallInFlight &&
            pitch.BallFlightTarget.X > pitch.BallFlightStart.X &&
            pitch.LastActionName.Contains("chuyền bóng về"),
            "Cầu thủ giao bóng phải chuyền về phần sân nhà trước, không được tự dẫn bóng lao lên.");
        pitch.SetPlaying(false);

        foreach (StringName playerId in pitch.CurrentPositions.Keys.ToArray())
        {
            pitch.OverridePlayerPosition(playerId, new Vector2(0.1f, 0.1f));
        }
        FootballMatchEvent goalEvent = new FootballMatchEvent().setup(
            12,
            "goal",
            "Bàn thắng kiểm thử.",
            simulation.home.team.id,
            simulation.home.squad.starter_ids[^1]);
        pitch.AnimateMinute(new Array<FootballMatchEvent> { goalEvent });
        Check(pitch.PendingRestartType == "kickoff", "Sau bàn thắng phải chờ đội thủng lưới giao bóng lại.");
        Check(pitch.IsKickoffPassPending, "Sau bàn thắng, lần giao bóng mới cũng phải bắt đầu bằng một đường chuyền.");
        CheckTeamIsInOwnHalf(pitch, simulation.home.squad.starter_ids, false,
            "Sau bàn thắng, đội chủ nhà phải trở lại phần sân của mình.");
        CheckTeamIsInOwnHalf(pitch, simulation.away.squad.starter_ids, true,
            "Sau bàn thắng, đội khách phải trở lại phần sân của mình.");

        FootballMatchEvent halfTimeEvent = new FootballMatchEvent().setup(
            45,
            "half_time",
            "Hết hiệp một.");
        pitch.AnimateMinute(new Array<FootballMatchEvent> { halfTimeEvent });
        Check(pitch.AreSidesSwitched, "Hết hiệp một phải đổi phần sân và hướng tấn công.");
        CheckTeamIsInOwnHalf(pitch, simulation.home.squad.starter_ids, true,
            "Sang hiệp hai, đội chủ nhà phải đứng ở nửa sân đối diện.");
        CheckTeamIsInOwnHalf(pitch, simulation.away.squad.starter_ids, false,
            "Sang hiệp hai, đội khách phải đứng ở nửa sân đối diện.");
        Check(pitch.PendingRestartType == "kickoff",
            "Hiệp hai phải bắt đầu bằng giao bóng của đội không giao bóng hiệp một.");

        FootballMatchEvent cornerEvent = new FootballMatchEvent().setup(
            46,
            "corner",
            "Phạt góc kiểm thử.",
            simulation.home.team.id);
        pitch.AnimateMinute(new Array<FootballMatchEvent> { cornerEvent });
        Check(pitch.BallPosition.X > 0.95f,
            "Sau khi đổi sân, phạt góc của đội chủ nhà phải được đặt ở biên ngang bên phải.");

        pitch.QueueFree();
        GD.Print("PASS: giao bóng, reset sau bàn thắng, phạt góc và đổi sân giữa hai hiệp hoạt động đúng.");
    }

    private static void CheckTeamIsInOwnHalf(
        MatchPitch2D pitch,
        Array<StringName> playerIds,
        bool ownsLeftHalf,
        string message)
    {
        bool isInOwnHalf = playerIds.All(playerId =>
            ownsLeftHalf
                ? pitch.CurrentPositions[playerId].X <= 0.5f
                : pitch.CurrentPositions[playerId].X >= 0.5f);
        Check(isInOwnHalf, message);
    }

    private static void TestFreeKickRestartTimingAndDistance()
    {
        FreeKickRestartPlanner planner = new();
        Vector2 ballStart = new(0.42f, 0.38f);
        Vector2 restartPosition = new(0.55f, 0.52f);
        FreeKickRestartPlan ceremonial = planner.CreatePlan(
            ballStart,
            restartPosition,
            false,
            0.01f);
        Check(
            !ceremonial.IsQuick && ceremonial.PreparationDurationSeconds >= 5f,
            "Đá phạt có còi phải có đủ thời gian đặt bóng và dàn vị trí.");
        Check(
            ceremonial.BallPositionAt(0.4f).IsEqualApprox(ballStart),
            "Ngay khi trọng tài thổi phạt, bóng phải còn ở vị trí cũ thay vì teleport.");
        Vector2 movingBall = ceremonial.BallPositionAt(2f);
        Check(
            !movingBall.IsEqualApprox(ballStart) && !movingBall.IsEqualApprox(restartPosition),
            "Trong thời gian chờ, bóng phải được đưa dần tới điểm đá phạt.");
        Check(
            ceremonial.BallPositionAt(3.3f).IsEqualApprox(restartPosition) &&
            ceremonial.IsBallPlaced(3.3f),
            "Bóng chỉ được nằm đúng điểm phạm lỗi sau giai đoạn đặt bóng.");

        FreeKickRestartPlan quick = planner.CreatePlan(
            ballStart,
            restartPosition,
            true,
            0.10f);
        Check(
            quick.IsQuick &&
            quick.PreparationDurationSeconds < ceremonial.PreparationDurationSeconds &&
            quick.PreparationDurationSeconds >= 1.5f,
            "Đá phạt nhanh phải nhanh hơn nhưng không được bắt đầu tức thì.");
        FreeKickRestartPlan declinedQuick = planner.CreatePlan(
            ballStart,
            restartPosition,
            true,
            0.90f);
        Check(
            !declinedQuick.IsQuick,
            "Cho phép đá nhanh không có nghĩa mọi tình huống đều bắt buộc đá nhanh.");

        Vector2 closeDefender = new(
            restartPosition.X + 2f / FootballPitchDimensions.LengthMeters,
            restartPosition.Y);
        Vector2 legalDefender = planner.EnsureRequiredDefenderDistance(
            closeDefender,
            restartPosition,
            false);
        Check(
            FootballPitchDimensions.DistanceMeters(legalDefender, restartPosition) >= 9.14f,
            "Khi chờ còi, đối phương phải lùi đủ 9,15 m khỏi điểm đá phạt.");
        Check(
            planner.EnsureRequiredDefenderDistance(closeDefender, restartPosition, true)
                .IsEqualApprox(closeDefender),
            "Đá phạt nhanh không được chờ engine cưỡng chế hàng rào rồi mới thực hiện.");
        GD.Print("PASS: đá phạt có thời gian đặt bóng, tùy chọn đá nhanh và cự ly phòng ngự 9,15 m.");
    }

    private static void TestPenaltyAdvantageAndDiscipline()
    {
        PenaltyAreaRule penaltyAreaRule = new();
        Check(
            penaltyAreaRule.IsInsideDefendingPenaltyArea(new Vector2(0.10f, 0.50f), 0.015f) &&
            penaltyAreaRule.IsInsideDefendingPenaltyArea(new Vector2(0.90f, 0.50f), 0.985f),
            "Phạm lỗi trong vòng cấm ở cả hai đầu sân phải được nhận diện là penalty.");
        Check(
            !penaltyAreaRule.IsInsideDefendingPenaltyArea(new Vector2(0.30f, 0.50f), 0.015f) &&
            !penaltyAreaRule.IsInsideDefendingPenaltyArea(new Vector2(0.10f, 0.90f), 0.015f),
            "Phạm lỗi ngoài chiều sâu hoặc ngoài bề rộng vòng cấm không được biến thành penalty.");

        PenaltyRestartPlanner penaltyRestartPlanner = new();
        PenaltyRestartPlan penaltyPlan = penaltyRestartPlanner.CreatePlan(new Vector2(0.22f, 0.62f), 0.015f);
        Check(
            Mathf.IsEqualApprox(
                penaltyPlan.PenaltySpot.X * FootballPitchDimensions.LengthMeters,
                FootballPitchDimensions.PenaltySpotDistanceMeters),
            "Bóng penalty phải được đặt đúng 11 m từ đường biên ngang.");
        Check(
            penaltyPlan.BallPositionAt(0.8f).IsEqualApprox(penaltyPlan.BallStart) &&
            penaltyPlan.IsBallPlaced(PenaltyRestartPlanner.BallPlacedAfterSeconds + 0.1f) &&
            PenaltyRestartPlanner.PreparationDurationSeconds >= 7f,
            "Penalty phải có thời gian trọng tài đặt bóng và dàn cầu thủ, không được thực hiện tức thì.");
        Vector2 stagedPlayer = penaltyRestartPlanner.EnsureOutsidePenaltyAreaAndArc(
            new Vector2(0.08f, 0.50f),
            penaltyPlan.PenaltySpot,
            0.015f);
        Check(
            !penaltyAreaRule.IsInsideDefendingPenaltyArea(stagedPlayer, 0.015f) &&
            FootballPitchDimensions.DistanceMeters(stagedPlayer, penaltyPlan.PenaltySpot) >= 9.14f,
            "Ngoài người sút và thủ môn, cầu thủ phải đứng ngoài vòng cấm và cách bóng 9,15 m.");

        PenaltyKickResolver penaltyKickResolver = new();
        Check(
            penaltyKickResolver.Resolve(90, 92, 82, 75, 70, 0.10f, 0.10f) == PenaltyKickOutcome.Goal &&
            penaltyKickResolver.Resolve(35, 30, 40, 80, 75, 0.99f, 0.20f) == PenaltyKickOutcome.OffTarget,
            "Penalty phải phụ thuộc khả năng dứt điểm, bình tĩnh, thủ môn và roll xác định.");

        AdvantageRuleEvaluator advantageRule = new();
        Check(
            advantageRule.ShouldPlay(new AdvantageContext(true, new StringName(), 0.76f, 3.5f, 0.10f)),
            "Trọng tài nên cho lợi thế khi đội tấn công còn bóng trong tình huống thuận lợi.");
        Check(
            !advantageRule.ShouldPlay(new AdvantageContext(true, "red", 0.80f, 4f, 0.01f)) &&
            !advantageRule.ShouldPlay(new AdvantageContext(true, new StringName(), 0.20f, 4f, 0.01f)),
            "Không được cho lợi thế với thẻ đỏ trực tiếp hoặc pha bóng không đem lại lợi ích tấn công.");

        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation disciplineMatch = new FootballMatchSimulation().setup(teams[0], teams[1], 817);
        disciplineMatch.use_live_pitch_events = true;
        StringName offenderId = disciplineMatch.home.squad.starter_ids[1];
        StringName victimId = disciplineMatch.away.squad.starter_ids[1];
        Check(
            disciplineMatch.register_live_foul(teams[0].id, offenderId, victimId, "yellow")?.event_type ==
            "yellow_card",
            "Thẻ vàng đầu tiên phải được ghi cho đúng cầu thủ.");
        Check(
            disciplineMatch.register_live_foul(teams[0].id, offenderId, victimId, "yellow")?.event_type ==
            "red_card",
            "Thẻ vàng thứ hai của cùng cầu thủ phải tự động trở thành thẻ đỏ.");
        Check(
            disciplineMatch.home.YellowCardCount(offenderId) == 2 &&
            disciplineMatch.home.stats["yellow_cards"].AsInt32() == 2 &&
            disciplineMatch.home.stats["red_cards"].AsInt32() == 1 &&
            disciplineMatch.home.squad.starter_ids.Count == 10,
            "Kỷ luật phải lưu theo cầu thủ và loại cầu thủ nhận hai thẻ vàng khỏi sân.");

        FootballMatchSimulation advantageMatch = new FootballMatchSimulation().setup(teams[0], teams[1], 819);
        advantageMatch.use_live_pitch_events = true;
        StringName delayedOffenderId = advantageMatch.home.squad.starter_ids[2];
        Check(
            advantageMatch.RegisterLiveAdvantage(teams[0].id, delayedOffenderId, victimId)?.event_type ==
            "advantage",
            "Pha lợi thế phải được ghi nhận mà chưa dừng trận.");
        Check(
            advantageMatch.RegisterLiveDelayedCard(teams[0].id, delayedOffenderId, "yellow")?.event_type ==
            "yellow_card",
            "Khi bóng chết, trọng tài phải quay lại rút thẻ đã hoãn.");
        Check(
            advantageMatch.home.stats["fouls"].AsInt32() == 1 &&
            advantageMatch.home.stats["yellow_cards"].AsInt32() == 1,
            "Lợi thế và thẻ hoãn không được cộng trùng số lần phạm lỗi.");
        Check(
            advantageMatch.register_live_restart(teams[1].id, "penalty")?.event_type == "penalty" &&
            advantageMatch.away.stats["penalties"].AsInt32() == 1,
            "Live match phải ghi nhận penalty cho đúng đội.");
        GD.Print("PASS: penalty, lợi thế, thẻ hoãn và hai vàng thành đỏ hoạt động theo luật nền tảng.");
    }
}

