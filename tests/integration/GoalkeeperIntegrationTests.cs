using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    private static void TestShotSelectionAndTraditionalGoalkeeper()
    {
        ShotOutcomeResolver outcomeResolver = new();
        int goals = 0;
        int offTarget = 0;
        int goalkeeperStops = 0;
        int longRangeGoals = 0;
        int openGoalGoals = 0;
        int openGoalOffTarget = 0;
        for (int sample = 0; sample < 240; sample++)
        {
            float accuracyRoll = ((sample * 37) % 239) / 238f;
            float goalRoll = ((sample * 71 + 13) % 239) / 238f;
            float handlingRoll = ((sample * 97 + 29) % 239) / 238f;
            float cornerRoll = ((sample * 53 + 7) % 239) / 238f;
            ShotOutcome outcome = outcomeResolver.Resolve(
                70,
                68,
                65,
                72,
                65,
                17f,
                0.18f,
                5.5f,
                1f,
                accuracyRoll,
                goalRoll,
                handlingRoll,
                cornerRoll);
            if (outcome == ShotOutcome.Goal)
            {
                goals++;
            }
            else if (outcome == ShotOutcome.OffTarget)
            {
                offTarget++;
            }
            else
            {
                goalkeeperStops++;
            }

            ShotOutcome longRangeOutcome = outcomeResolver.Resolve(
                78,
                70,
                65,
                72,
                65,
                30f,
                0.15f,
                6f,
                1f,
                accuracyRoll,
                goalRoll,
                handlingRoll,
                cornerRoll);
            if (longRangeOutcome == ShotOutcome.Goal)
            {
                longRangeGoals++;
            }

            ShotOutcome openGoalOutcome = outcomeResolver.Resolve(
                70,
                68,
                65,
                72,
                65,
                12f,
                0.10f,
                6f,
                0.15f,
                accuracyRoll,
                goalRoll,
                handlingRoll,
                cornerRoll);
            if (openGoalOutcome == ShotOutcome.Goal)
            {
                openGoalGoals++;
            }
            else if (openGoalOutcome == ShotOutcome.OffTarget)
            {
                openGoalOffTarget++;
            }
        }
        Check(offTarget >= 55, "Một phần đáng kể cú sút phải đi ra ngoài khung thành.");
        Check(goalkeeperStops > goals, "Thủ môn phải cản phá nhiều cú sút hơn số bàn thua trong mẫu cân bằng.");
        Check(longRangeGoals <= 12, "Cú sút khoảng 30 mét không được có tỷ lệ thành bàn phi thực tế.");
        Check(
            openGoalGoals > goals && openGoalOffTarget < offTarget,
            "Khi thủ môn đã lệch khỏi đường bóng, cú sút gần phải dễ trúng đích và thành bàn hơn rõ ràng.");

        ShotTargetPlanner targetPlanner = new();
        Vector2 shooterPosition = new(0.25f, 0.68f);
        Vector2 displacedGoalkeeper = new(0.08f, 0.66f);
        Vector2 openGoalTarget = targetPlanner.ChooseGoalTarget(0.015f, displacedGoalkeeper, 0.8f);
        Check(
            openGoalTarget.Y < 0.5f,
            "Tiền đạo phải nhắm phần khung thành đối diện khi thủ môn đã lao lệch sang một phía.");
        float displacedCoverage = targetPlanner.GoalkeeperCoverage(
            shooterPosition,
            openGoalTarget,
            displacedGoalkeeper);
        Check(
            displacedCoverage < 0.55f,
            "Thủ môn lệch khỏi đường sút không được giữ nguyên toàn bộ sức cản như khi đứng đúng vị trí.");
        Vector2 nearMiss = targetPlanner.ChooseOffTargetDestination(
            0.015f,
            openGoalTarget.Y,
            76,
            12f,
            0.5f);
        float missBeyondPostMeters = Mathf.Abs(nearMiss.Y - 0.5f) * FootballPitchDimensions.WidthMeters -
                                     FootballPitchDimensions.GoalWidthMeters * 0.5f;
        Check(
            missBeyondPostMeters is >= 0.4f and <= 2.8f && nearMiss.X < 0f,
            "Cú sút gần chệch hướng phải đi sát cột ở mức hợp lý, không bay thẳng về phía cột cờ.");

        StringName homeTeamId = "keeper_home";
        StringName awayTeamId = "keeper_away";
        StringName goalkeeperId = "traditional_keeper";
        StringName shooterId = "keeper_test_shooter";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [goalkeeperId] = new Vector2(0.05f, 0.50f),
            [shooterId] = new Vector2(0.25f, 0.30f)
        };
        var basePositions = positions.ToDictionary(pair => pair.Key, pair => pair.Value);
        var playerTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [goalkeeperId] = awayTeamId,
            [shooterId] = homeTeamId
        };
        var roles = new System.Collections.Generic.Dictionary<StringName, string>
        {
            [goalkeeperId] = "GK",
            [shooterId] = "ST"
        };
        FootballWorldSnapshot shotWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            positions[shooterId],
            new Vector2(0.015f, 0.60f),
            new StringName(),
            new StringName(),
            homeTeamId,
            homeTeamId,
            true,
            false,
            true,
            true,
            false);
        PlayerIntent goalkeeperIntent = FootballIntentPlanner.GoalkeeperIntent(
            shotWorld,
            goalkeeperId,
            awayTeamId,
            LiveTeamPhase.Defending);
        Check(
            goalkeeperIntent.Target.Y > 0.56f,
            "Thủ môn phải đổ về phía điểm đến của cú sút thay vì đứng bất động giữa khung thành.");
        Check(
            goalkeeperIntent.Target.X > shotWorld.OwnGoal(awayTeamId).X,
            "Thủ môn truyền thống phải đứng hơi cao hơn vạch vôi để tham gia pha bóng.");
        TraditionalGoalkeeperPlanner goalkeeperPlanner = new();
        Check(
            goalkeeperPlanner.ShouldUseBackPass("CB", 0.24f, true, 18f, 0.25f, 0.20f),
            "Hậu vệ chịu áp lực trong phần sân nhà phải biết chuyền về cho thủ môn.");
        Check(
            !goalkeeperPlanner.ShouldUseBackPass("ST", 0.24f, true, 18f, 0.25f, 0.20f),
            "Tiền đạo không được dùng đường chuyền về thủ môn như hành vi mặc định.");

        positions[shooterId] = new Vector2(0.11f, 0.50f);
        FootballWorldSnapshot controlledBreakawayWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            positions[shooterId],
            positions[shooterId],
            shooterId,
            new StringName(),
            homeTeamId,
            homeTeamId,
            false,
            false,
            true);
        PlayerIntent rushingGoalkeeperIntent = FootballIntentPlanner.GoalkeeperIntent(
            controlledBreakawayWorld,
            goalkeeperId,
            awayTeamId,
            LiveTeamPhase.Defending);
        Check(
            goalkeeperPlanner.ShouldRushControlledBall(controlledBreakawayWorld, goalkeeperId, awayTeamId) &&
            rushingGoalkeeperIntent.Kind == PlayerIntentKind.CloseDownBall &&
            rushingGoalkeeperIntent.Target.X > positions[goalkeeperId].X &&
            rushingGoalkeeperIntent.Target.X < positions[shooterId].X,
            "Tiền đạo kiểm soát bóng sát khung thành phải khiến thủ môn lao ra khép góc từ phía cầu môn.");

        GD.Print("PASS: giới hạn sút xa, kết quả sút đa dạng và thủ môn truyền thống tham gia trận đấu.");
    }

    private static void TestDirectAttackContinuationAndGoalkeeperLooseBallClaim()
    {
        DirectAttackContinuationPlanner continuationPlanner = new();
        Check(
            continuationPlanner.ShouldBeginAfterReception("ST", false, 0.76f, 14f),
            "Đường chuyền phá tuyến ở phần sân cuối phải được nhận diện kể cả khi nhãn nội bộ chỉ là chuyền thường.");
        Check(
            !continuationPlanner.ShouldBeginAfterReception("ST", false, 0.45f, 14f),
            "Đường chuyền tiến ở giữa sân chưa phải một pha đối mặt cần khóa hành vi tấn công.");
        StringName defendingTeamId = "loose_keeper_team";
        StringName attackingTeamId = "loose_attacker_team";
        StringName goalkeeperId = "loose_keeper";
        StringName attackerId = "distant_attacker";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [goalkeeperId] = new Vector2(0.055f, 0.50f),
            [attackerId] = new Vector2(0.55f, 0.50f)
        };
        var basePositions = positions.ToDictionary(pair => pair.Key, pair => pair.Value);
        var playerTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [goalkeeperId] = defendingTeamId,
            [attackerId] = attackingTeamId
        };
        var roles = new System.Collections.Generic.Dictionary<StringName, string>
        {
            [goalkeeperId] = "GK",
            [attackerId] = "ST"
        };
        FootballWorldSnapshot looseBallWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            new Vector2(0.12f, 0.50f),
            new Vector2(0.12f, 0.50f),
            new StringName(),
            new StringName(),
            attackingTeamId,
            attackingTeamId,
            false,
            true,
            true);
        TraditionalGoalkeeperPlanner goalkeeperPlanner = new();
        Check(
            goalkeeperPlanner.ShouldClaimLooseBall(looseBallWorld, goalkeeperId, defendingTeamId),
            "Thủ môn gần bóng trong vùng cấm phải lao ra trước tiền đạo còn ở rất xa.");

        FootballIntentPlanner intentPlanner = new();
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> intents = intentPlanner.Plan(looseBallWorld);
        Check(
            intents[goalkeeperId].Kind == PlayerIntentKind.ChaseLooseBall,
            "Ý định của thủ môn phải đổi từ đứng giữ gôn sang lao tới bóng tự do an toàn.");

        FootballWorldSnapshot midfieldLooseBallWorld = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            new Vector2(0.50f, 0.50f),
            new Vector2(0.50f, 0.50f),
            new StringName(),
            new StringName(),
            attackingTeamId,
            attackingTeamId,
            false,
            true,
            true);
        Check(
            !goalkeeperPlanner.ShouldClaimLooseBall(midfieldLooseBallWorld, goalkeeperId, defendingTeamId),
            "Thủ môn truyền thống không được lao khỏi vùng cấm để đuổi bóng giữa sân.");

        GD.Print("PASS: tiền đạo tiếp tục pha chọc khe và thủ môn chủ động thu bóng tự do trong vùng cấm.");
    }

    private static void TestGoalKickShapeAndSeededDecisionVariety()
    {
        GoalKickRestartPlanner restartPlanner = new();
        Check(
            GoalKickRestartPlanner.PreparationDurationSeconds >= 8f,
            "Phát bóng phải có thời gian bóng chết để hai đội dàn lại vị trí.");
        Check(
            restartPlanner.BallPresentation(0.2f) == GoalKickBallPresentation.OutOfPlayVisible &&
            restartPlanner.BallPresentation(1.0f) == GoalKickBallPresentation.BeingRetrieved &&
            restartPlanner.BallPresentation(5.0f) == GoalKickBallPresentation.BeingRetrieved &&
            restartPlanner.BallPresentation(6.2f) == GoalKickBallPresentation.PlacedForRestart,
            "Bóng ra ngoài phải chờ được nhặt hoặc đưa bóng mới vào trước khi xuất hiện ở vị trí phát bóng.");
        Vector2 goalkeeperTarget = restartPlanner.PositionTarget(
            new Vector2(0.04f, 0.50f),
            "GK",
            true,
            0.015f,
            new Vector2(0.055f, 0.50f));
        Vector2 homeCenterBackTarget = restartPlanner.PositionTarget(
            new Vector2(0.20f, 0.40f),
            "CB",
            true,
            0.015f,
            goalkeeperTarget);
        Vector2 homeStrikerTarget = restartPlanner.PositionTarget(
            new Vector2(0.72f, 0.50f),
            "ST",
            true,
            0.015f,
            goalkeeperTarget);
        Vector2 opponentStrikerTarget = restartPlanner.PositionTarget(
            new Vector2(0.20f, 0.50f),
            "ST",
            false,
            0.015f,
            goalkeeperTarget);
        Vector2 opponentCenterBackTarget = restartPlanner.PositionTarget(
            new Vector2(0.75f, 0.40f),
            "CB",
            false,
            0.015f,
            goalkeeperTarget);

        Check(
            goalkeeperTarget.IsEqualApprox(new Vector2(0.055f, 0.50f)),
            "Thủ môn phải di chuyển tới vị trí đặt bóng thay vì có bóng ngay lập tức.");
        Check(
            homeStrikerTarget.X > homeCenterBackTarget.X,
            "Đội phát bóng phải dâng thành nhiều tuyến thay vì đứng tụm quanh khu 5,50 mét.");
        Check(
            opponentStrikerTarget.X > FootballPitchDimensions.PenaltyAreaDepthMeters /
                                      FootballPitchDimensions.LengthMeters &&
            opponentCenterBackTarget.X > opponentStrikerTarget.X,
            "Đối phương phải ra khỏi vùng cấm và lùi thành khối để ngăn phản công.");
        Vector2 illegalOpponent = new(0.08f, 0.50f);
        Vector2 legalOpponent = restartPlanner.EnsureOpponentOutsidePenaltyArea(illegalOpponent, 0.015f);
        Check(
            legalOpponent.X > FootballPitchDimensions.PenaltyAreaDepthMeters /
                              FootballPitchDimensions.LengthMeters,
            "Không cầu thủ đối phương nào được còn trong vùng cấm khi quả phát bóng được thực hiện.");

        DecisionVarietyTracker varietyTracker = new();
        StringName repeatedTarget = "repeated_receiver";
        StringName alternativeTarget = "alternative_receiver";
        varietyTracker.RecordPassTarget(repeatedTarget);
        varietyTracker.RecordPassTarget(repeatedTarget);
        float repeatedScore = varietyTracker.PassScoreAdjustment(repeatedTarget, 0.5f, false);
        float alternativeScore = varietyTracker.PassScoreAdjustment(alternativeTarget, 0.5f, false);
        Check(
            alternativeScore > repeatedScore,
            "AI phải giảm ưu tiên tuyến chuyền vừa lặp lại nhiều lần khi có phương án tương đương.");

        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation seeded = new FootballMatchSimulation().setup(teams[0], teams[1], 987654321);
        Check(
            seeded.MatchSeed == 987654321,
            "Sân 2D phải nhận được seed của trận để chuỗi quyết định thay đổi giữa các trận.");

        GD.Print("PASS: phát bóng có pha dàn đội hình hợp luật và quyết định dùng biến thiên có seed.");
    }
}
