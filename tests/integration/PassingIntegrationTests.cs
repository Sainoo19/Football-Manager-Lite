using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    private static void TestOffsideRule()
    {
        StringName attackingTeamId = "offside_attack";
        StringName defendingTeamId = "offside_defence";
        StringName receiverId = "receiver";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [receiverId] = new Vector2(0.78f, 0.5f),
            ["defender_goalkeeper"] = new Vector2(0.82f, 0.5f),
            ["defender_second_last"] = new Vector2(0.72f, 0.4f),
            ["defender_third_last"] = new Vector2(0.61f, 0.6f)
        };
        var playerTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [receiverId] = attackingTeamId,
            ["defender_goalkeeper"] = defendingTeamId,
            ["defender_second_last"] = defendingTeamId,
            ["defender_third_last"] = defendingTeamId
        };
        OffsideRule rule = new();

        Check(
            rule.IsOffside(receiverId, attackingTeamId, new Vector2(0.55f, 0.5f), 1f, positions, playerTeams),
            "Cầu thủ vượt bóng và hậu vệ áp chót trong phần sân đối phương phải bị bắt việt vị.");
        positions[receiverId] = new Vector2(0.72f, 0.5f);
        Check(
            !rule.IsOffside(receiverId, attackingTeamId, new Vector2(0.55f, 0.5f), 1f, positions, playerTeams),
            "Cầu thủ đứng ngang hàng hậu vệ áp chót không được bị bắt việt vị.");
        positions[receiverId] = new Vector2(0.48f, 0.5f);
        Check(
            !rule.IsOffside(receiverId, attackingTeamId, new Vector2(0.40f, 0.5f), 1f, positions, playerTeams),
            "Không được bắt việt vị cầu thủ còn ở phần sân nhà.");
        positions[receiverId] = new Vector2(0.78f, 0.5f);
        Check(
            !rule.IsOffside(receiverId, attackingTeamId, new Vector2(0.80f, 0.5f), 1f, positions, playerTeams),
            "Cầu thủ đứng sau bóng không được bị bắt việt vị.");

        positions[receiverId] = new Vector2(0.22f, 0.5f);
        positions["defender_goalkeeper"] = new Vector2(0.18f, 0.5f);
        positions["defender_second_last"] = new Vector2(0.28f, 0.4f);
        positions["defender_third_last"] = new Vector2(0.39f, 0.6f);
        Check(
            rule.IsOffside(receiverId, attackingTeamId, new Vector2(0.45f, 0.5f), -1f, positions, playerTeams),
            "Luật việt vị phải đảo đúng theo hướng tấn công ở hiệp còn lại.");
        GD.Print("PASS: việt vị xét đúng bóng, nửa sân, hậu vệ áp chót và hướng tấn công.");
    }

    private static void TestPassTrajectoryAndNearestContest()
    {
        DuelDistanceRules duelDistanceRules = new();
        Check(
            duelDistanceRules.CanAttemptTackle(1.4f) && !duelDistanceRules.CanAttemptTackle(5f),
            "Hậu vệ chỉ được tắc bóng khi đã áp sát thật, không phải từ khoảng cách 5–7 mét.");
        Check(
            duelDistanceRules.IsUnderPressure(3f) && !duelDistanceRules.IsUnderPressure(5f),
            "Trạng thái chịu áp lực phải dùng khoảng cách mét thật.");

        PassTrajectoryPlanner passPlanner = new();
        Vector2 ballPosition = new(0.30f, 0.50f);
        Vector2 receiverPosition = new(0.50f, 0.50f);
        Vector2 runTarget = new(0.82f, 0.50f);
        PassTrajectory standardPass = passPlanner.Plan(
            ballPosition,
            receiverPosition,
            runTarget,
            LivePassType.Standard);
        float standardLeadMeters = FootballPitchDimensions.DistanceMeters(receiverPosition, standardPass.Target);
        Check(
            standardLeadMeters <= 1.21f,
            "Đường chuyền thường không áp lực phải hướng gần chân người nhận thay vì bắt họ đuổi quá xa.");
        Check(
            standardLeadMeters <= 6.4f * standardPass.Duration,
            "Điểm đón của đường chuyền thường phải nằm trong quãng đường người nhận có thể chạy tới.");

        PassTrajectory throughBall = passPlanner.Plan(
            ballPosition,
            receiverPosition,
            runTarget,
            LivePassType.ThroughBall);
        float throughBallLeadMeters = FootballPitchDimensions.DistanceMeters(receiverPosition, throughBall.Target);
        Check(
            throughBallLeadMeters is >= 4.5f and <= 6.5f,
            "Chọc khe phải đưa bóng rõ ràng vào khoảng trống nhưng vẫn trong tầm tiền đạo đuổi tới.");

        StringName throughAttackTeam = "through_attack";
        StringName throughDefenseTeam = "through_defense";
        var throughPositions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            ["through_receiver"] = receiverPosition,
            ["central_defender"] = new Vector2(0.57f, 0.50f),
            ["cover_defender"] = new Vector2(0.64f, 0.68f)
        };
        var throughTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            ["through_receiver"] = throughAttackTeam,
            ["central_defender"] = throughDefenseTeam,
            ["cover_defender"] = throughDefenseTeam
        };
        Vector2 openThroughTarget = new ThroughBallTargetPlanner().FindTarget(
            ballPosition,
            receiverPosition,
            new Vector2(0.60f, 0.50f),
            1f,
            throughAttackTeam,
            throughPositions,
            throughTeams);
        Check(
            FootballPitchDimensions.DistanceMeters(receiverPosition, openThroughTarget) >= 5.9f,
            "Điểm chọc khe phải nằm phía trước người nhận thay vì đúng dưới chân họ.");
        Check(
            Mathf.Abs(openThroughTarget.Y - receiverPosition.Y) > 0.04f,
            "Khi trung lộ bị chặn, chọc khe phải tìm hành lang lệch khỏi hậu vệ.");

        StringName firstTeamId = "contest_first";
        StringName secondTeamId = "contest_second";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            ["first_cf"] = new Vector2(0.50f, 0.65f),
            ["first_lw"] = new Vector2(0.60f, 0.50f),
            ["second_cf"] = new Vector2(0.50f, 0.34f),
            ["second_lw"] = new Vector2(0.61f, 0.50f)
        };
        var basePositions = positions.ToDictionary(pair => pair.Key, pair => pair.Value);
        var playerTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            ["first_cf"] = firstTeamId,
            ["first_lw"] = firstTeamId,
            ["second_cf"] = secondTeamId,
            ["second_lw"] = secondTeamId
        };
        var roles = new System.Collections.Generic.Dictionary<StringName, string>
        {
            ["first_cf"] = "ST",
            ["first_lw"] = "LW",
            ["second_cf"] = "ST",
            ["second_lw"] = "LW"
        };
        FootballWorldSnapshot looseBallWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            new Vector2(0.50f, 0.50f),
            new Vector2(0.50f, 0.50f),
            new StringName(),
            new StringName(),
            firstTeamId,
            firstTeamId,
            false,
            true);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> looseBallIntents =
            new FootballIntentPlanner().Plan(looseBallWorld);
        int firstTeamChasers = looseBallIntents.Count(pair =>
            playerTeams[pair.Key] == firstTeamId && pair.Value.Kind == PlayerIntentKind.ChaseLooseBall);
        int secondTeamChasers = looseBallIntents.Count(pair =>
            playerTeams[pair.Key] == secondTeamId && pair.Value.Kind == PlayerIntentKind.ChaseLooseBall);
        Check(
            firstTeamChasers is >= 1 and <= 2 && secondTeamChasers is >= 1 and <= 2,
            "Bóng tự do phải cấp động một hoặc hai người mỗi đội tùy mật độ quanh bóng.");
        Check(
            looseBallIntents["first_cf"].Kind == PlayerIntentKind.ChaseLooseBall &&
            looseBallIntents["second_cf"].Kind == PlayerIntentKind.ChaseLooseBall,
            "Khoảng cách đến bóng phải được tính theo mét sân, không được chọn LW xa hơn CF.");

        StringName ballOwnerId = "first_owner";
        positions[ballOwnerId] = new Vector2(0.50f, 0.50f);
        basePositions[ballOwnerId] = positions[ballOwnerId];
        playerTeams[ballOwnerId] = firstTeamId;
        roles[ballOwnerId] = "CM";
        FootballWorldSnapshot possessionWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            positions[ballOwnerId],
            positions[ballOwnerId],
            ballOwnerId,
            new StringName(),
            firstTeamId,
            firstTeamId,
            false,
            false);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> possessionIntents =
            new FootballIntentPlanner().Plan(possessionWorld);
        Check(
            possessionIntents["second_cf"].Kind == PlayerIntentKind.PressBall,
            "Khi đối phương giữ bóng, cầu thủ phòng ngự gần nhất tính theo mét phải là người pressing.");

        GD.Print("PASS: chuyền thường có điểm đón thực tế và mỗi đội cử đúng người gần bóng nhất tranh chấp.");
    }

    private static void TestFinalThirdAttackDecisions()
    {
        PassOptionEvaluator passEvaluator = new();
        Check(
            !passEvaluator.CanConsider("ST", "CB", 0.82f, -8f, 20f, 0.15f, false),
            "Tiền đạo ở một phần ba cuối sân không được chọn trung vệ làm đường chuyền lùi mặc định.");
        Check(
            passEvaluator.CanConsider("ST", "AM", 0.82f, 4f, 18f, 0.25f, false),
            "Tiền đạo vẫn được phối hợp với cầu thủ tấn công khi đường chuyền an toàn.");
        Check(
            !passEvaluator.CanConsider("ST", "AM", 0.82f, 4f, 18f, 0.80f, false),
            "Tiền đạo không được chuyền vào một hành lang đã bị hậu vệ khóa rõ ràng.");
        Check(
            passEvaluator.CanConsider("ST", "CB", 0.55f, -12f, 22f, 0.15f, false),
            "Ở giữa sân, tiền đạo vẫn có thể nhả bóng về để giữ quyền kiểm soát.");
        Check(
            !passEvaluator.CanConsider("LW", "RB", 0.66f, -28f, 41f, 0.20f, false),
            "Cầu thủ tấn công trong pha chuyển trạng thái không được bỏ lợi thế để chuyền lùi xa cho hậu vệ biên.");
        Check(
            !passEvaluator.CanConsider("LW", "RB", 0.66f, -28f, 41f, 0.20f, true),
            "Ngay cả khi bị áp lực, cầu thủ chỉ được nhả ngắn chứ không chuyền ngược xuyên cả đội hình.");
        Check(
            !passEvaluator.CanConsider("CM", "RW", 0.66f, -23f, 37f, 0.20f, false),
            "Tiền vệ trung tâm tham gia phản công cũng không được chuyền chéo lùi xa xuyên cả đội hình.");
        Check(
            passEvaluator.CanConsider("LW", "ST", 0.66f, 8f, 24f, 0.35f, false),
            "Trong cùng tình huống, phương án phối hợp tiến với tiền đạo phải tiếp tục hợp lệ.");
        Check(
            passEvaluator.CanConsiderCross("ST", 6f, 27f, 0.35f),
            "Quả tạt tới tiền đạo phía trước, trong cự ly hợp lý phải được phép.");
        Check(
            passEvaluator.CanConsiderCross("AM", -6f, 20f, 0.35f),
            "Đường căng ngược ngắn cho tuyến hai vẫn là một lựa chọn hợp lệ.");
        Check(
            !passEvaluator.CanConsiderCross("RW", -28f, 41f, 0.20f),
            "LW không được tạt chéo lùi xuyên gần hết đội hình sang RW cánh đối diện.");
        Check(
            !passEvaluator.CanReceiverControl(1.3f, 0.30f, 4f, 16f),
            "Không được chuyền cho đồng đội đang bị đối thủ áp sát ngay sát người.");
        Check(
            !passEvaluator.CanReceiverControl(3f, 0.55f, 0f, 18f),
            "Đường chuyền ngang chỉ được chọn khi người nhận có tư thế đủ thoải mái.");
        Check(
            passEvaluator.CanReceiverControl(5f, 0.30f, 8f, 22f),
            "Đồng đội có khoảng trống phía trước vẫn phải là phương án chuyền hợp lệ.");


        StringName clearingTeamId = "clearance_team";
        StringName defendingTeamId = "clearance_opponent";
        StringName outletId = "clearance_outlet";
        var clearancePositions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [outletId] = new Vector2(0.48f, 0.50f),
            ["clearance_opponent"] = new Vector2(0.52f, 0.62f)
        };
        var clearanceTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [outletId] = clearingTeamId,
            ["clearance_opponent"] = defendingTeamId
        };
        var clearanceRoles = new System.Collections.Generic.Dictionary<StringName, string>
        {
            [outletId] = "ST",
            ["clearance_opponent"] = "CB"
        };
        Vector2 clearanceTarget = new ClearanceTargetPlanner().FindTarget(
            new Vector2(0.15f, 0.14f),
            1f,
            clearingTeamId,
            clearancePositions,
            clearanceTeams,
            clearanceRoles);
        Check(
            FootballPitchDimensions.DistanceMeters(clearanceTarget, clearancePositions[outletId]) <= 4f &&
            clearanceTarget.Y is > 0.10f and < 0.90f,
            "Pha phá bóng dài phải hướng tới khu vực đồng đội có thể tranh chấp, không bay vào góc sân trống.");

        GD.Print("PASS: tiền đạo trong vùng cấm ưu tiên dứt điểm và tránh chuyền lùi hoặc chuyền vào tuyến bị khóa.");
    }

    private static void TestFootballFundamentalsRuntimeAndTechnique()
    {
        LiveMatchRuntime runtime = new();
        runtime.SetSpeed(MatchPlaybackSpeed.Fast);
        runtime.Start();
        int elapsedMinutes = runtime.Advance(0.28d);
        Check(
            elapsedMinutes == 1 && Math.Abs(runtime.LastAdvancedGameSeconds - 60d) < 0.01d,
            $"Runtime phải cung cấp đúng lượng giây game cho sân 2D ở mọi tốc độ phát lại; " +
            $"phút={elapsedMinutes}, delta={runtime.LastAdvancedGameSeconds:0.000000}.");
        runtime.SetPhase(LiveMatchPhase.BallInFlight);
        Check(runtime.Phase == LiveMatchPhase.BallInFlight, "Runtime phải giữ trạng thái pha bóng rõ ràng.");
        runtime.SetPhase(LiveMatchPhase.FullTime);
        Check(!runtime.IsRunning, "Trạng thái hết trận phải dừng runtime duy nhất của trận đấu.");

        PassExecutionResolver passResolver = new();
        Vector2 ball = new(0.25f, 0.50f);
        Vector2 intendedTarget = new(0.62f, 0.36f);
        PassExecution elitePass = passResolver.Resolve(
            ball,
            intendedTarget,
            LivePassType.ThroughBall,
            92,
            92,
            90,
            82,
            7f,
            0.86f,
            0.14f);
        PassExecution poorPressuredPass = passResolver.Resolve(
            ball,
            intendedTarget,
            LivePassType.ThroughBall,
            38,
            40,
            35,
            45,
            1.2f,
            0.86f,
            0.14f);
        float eliteError = FootballPitchDimensions.DistanceMeters(elitePass.IntendedTarget, elitePass.ActualTarget);
        float poorError = FootballPitchDimensions.DistanceMeters(
            poorPressuredPass.IntendedTarget,
            poorPressuredPass.ActualTarget);
        Check(
            elitePass.Quality > poorPressuredPass.Quality && eliteError < poorError,
            "Chất lượng, áp lực và khoảng cách phải biến ý định chuyền thành sai số kỹ thuật có nguyên nhân.");
        PassExecution repeatedElitePass = passResolver.Resolve(
            ball,
            intendedTarget,
            LivePassType.ThroughBall,
            92,
            92,
            90,
            82,
            7f,
            0.86f,
            0.14f);
        Check(
            repeatedElitePass.ActualTarget.IsEqualApprox(elitePass.ActualTarget),
            "Thực thi đường chuyền phải deterministic khi đầu vào và roll giống nhau.");

        FirstTouchResolver firstTouchResolver = new();
        FirstTouchResolution eliteTouch = firstTouchResolver.Resolve(
            92,
            91,
            90,
            82,
            6f,
            18f,
            LivePassType.ThroughBall,
            0.80f,
            0.50f);
        FirstTouchResolution poorTouch = firstTouchResolver.Resolve(
            35,
            38,
            32,
            40,
            1.1f,
            24f,
            LivePassType.Cross,
            0.80f,
            0.90f);
        Check(
            eliteTouch.Outcome == FirstTouchOutcome.Controlled && poorTouch.Outcome != FirstTouchOutcome.Controlled,
            "Đỡ bước một phải phụ thuộc kỹ thuật, độ khó bóng đến và áp lực đối phương.");
        GD.Print("PASS: runtime duy nhất, pipeline thực thi chuyền và đỡ bước một phản ánh kỹ năng cùng áp lực.");
    }
}
