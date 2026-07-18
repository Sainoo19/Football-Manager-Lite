using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    private void RunTests()
    {
        try
        {
            TestSquadLimits();
            TestLiveMatchClock();
            PlayerPositionInterpolatorTests.Run();
            TestFootballFundamentalsRuntimeAndTechnique();
            TestFreeKickRestartTimingAndDistance();
            TestPenaltyAdvantageAndDiscipline();
            TestMatchSimulation();
            TestLiveMatchRules();
            TestPitchScaleAndMovementSpeed();
            TestOffsideRule();
            TestPassTrajectoryAndNearestContest();
            TestDefensiveBlockSpacingAndRollingBall();
            TestShotSelectionAndTraditionalGoalkeeper();
            TestDirectAttackContinuationAndGoalkeeperLooseBallClaim();
            TestFinalThirdAttackDecisions();
            TestGoalKickShapeAndSeededDecisionVariety();
            TestScenarioFactoryAndPitchLauncher();
            TestWideAttackKeepsFootballShape();
            TestPitchPauseAndReset();
            TestPlaybackSpeedDoesNotChangeFootball();
            TestKickoffGoalResetAndHalfTimeSides();
            PressureReleaseDecisionEvaluatorTests.Run();
            PressureReleaseScenarioIntegrationTests.Run();
            GroundDuelTests.Run();
            GroundDuelScenarioIntegrationTests.Run();
            AerialBallTests.Run();
            BalanceBatchTests.Run();
            AerialBallScenarioIntegrationTests.Run();
            TestPitchMovement();
            LiveMatchEngineIntegrationTests.Run();
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

    private static void TestWideAttackKeepsFootballShape()
    {
        StringName homeTeamId = "shape_home";
        StringName awayTeamId = "shape_away";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>();
        var basePositions = new System.Collections.Generic.Dictionary<StringName, Vector2>();
        var teams = new System.Collections.Generic.Dictionary<StringName, StringName>();
        var roles = new System.Collections.Generic.Dictionary<StringName, string>();

        AddShapePlayer("away_gk", awayTeamId, "GK", new Vector2(0.05f, 0.50f));
        AddShapePlayer("away_lb", awayTeamId, "LB", new Vector2(0.24f, 0.15f));
        AddShapePlayer("away_lcb", awayTeamId, "CB", new Vector2(0.25f, 0.38f));
        AddShapePlayer("away_rcb", awayTeamId, "CB", new Vector2(0.25f, 0.62f));
        AddShapePlayer("away_rb", awayTeamId, "RB", new Vector2(0.24f, 0.85f));
        AddShapePlayer("away_lcm", awayTeamId, "CM", new Vector2(0.52f, 0.25f));
        AddShapePlayer("away_dm", awayTeamId, "DM", new Vector2(0.45f, 0.50f));
        AddShapePlayer("away_rcm", awayTeamId, "CM", new Vector2(0.52f, 0.75f));
        AddShapePlayer("away_lw", awayTeamId, "LW", new Vector2(0.76f, 0.18f));
        AddShapePlayer("away_st", awayTeamId, "ST", new Vector2(0.78f, 0.50f));
        AddShapePlayer("away_rw", awayTeamId, "RW", new Vector2(0.93f, 0.94f));

        AddShapePlayer("home_gk", homeTeamId, "GK", new Vector2(0.95f, 0.50f));
        AddShapePlayer("home_lb", homeTeamId, "LB", new Vector2(0.76f, 0.85f));
        AddShapePlayer("home_lcb", homeTeamId, "CB", new Vector2(0.75f, 0.62f));
        AddShapePlayer("home_rcb", homeTeamId, "CB", new Vector2(0.75f, 0.38f));
        AddShapePlayer("home_rb", homeTeamId, "RB", new Vector2(0.76f, 0.15f));
        AddShapePlayer("home_lcm", homeTeamId, "CM", new Vector2(0.48f, 0.75f));
        AddShapePlayer("home_dm", homeTeamId, "DM", new Vector2(0.55f, 0.50f));
        AddShapePlayer("home_rcm", homeTeamId, "CM", new Vector2(0.48f, 0.25f));
        AddShapePlayer("home_lw", homeTeamId, "LW", new Vector2(0.24f, 0.82f));
        AddShapePlayer("home_st", homeTeamId, "ST", new Vector2(0.22f, 0.50f));
        AddShapePlayer("home_rw", homeTeamId, "RW", new Vector2(0.24f, 0.18f));

        StringName ballOwnerId = "away_rw";
        Vector2 ballPosition = positions[ballOwnerId];
        FootballWorldSnapshot world = new(
            positions,
            basePositions,
            teams,
            roles,
            ballPosition,
            ballPosition,
            ballOwnerId,
            new StringName(),
            awayTeamId,
            homeTeamId,
            false,
            false);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> planned =
            new FootballIntentPlanner().Plan(world);

        PlayerIntent[] forwardRuns = planned
            .Where(pair => teams[pair.Key] == awayTeamId && pair.Value.Kind == PlayerIntentKind.RunIntoSpace)
            .Select(pair => pair.Value)
            .ToArray();
        Check(forwardRuns.Length >= 2, "Khi bóng ở biên, các tiền đạo còn lại phải chạy vào khu vực tấn công.");
        Check(
            forwardRuns.All(intent => intent.Target.Y is >= 0.20f and <= 0.80f),
            "Tiền đạo không được cùng chạy ra cột cờ khi đồng đội đang có bóng ở biên.");
        float laneSpread = forwardRuns.Max(intent => intent.Target.Y) - forwardRuns.Min(intent => intent.Target.Y);
        Check(laneSpread >= 0.09f, "Các cầu thủ tấn công phải chiếm những làn nhận bóng khác nhau.");
        int teammatesInBallCorner = planned.Count(pair =>
            pair.Key != ballOwnerId &&
            teams[pair.Key] == awayTeamId &&
            pair.Value.Target.X > 0.85f &&
            pair.Value.Target.Y > 0.86f);
        Check(teammatesInBallCorner == 0, "Không được kéo nhiều đồng đội vào cùng góc sân với người giữ bóng.");
        Check(
            planned["away_lb"].Target.Y <= 0.46f && planned["away_rb"].Target.Y >= 0.54f,
            "LB và RB phải giữ đúng hai hành lang theo hướng tấn công, không tự đổi cánh khi hỗ trợ bóng.");
        StringName[] attackingOutfield = planned.Keys
            .Where(id => teams[id] == awayTeamId && roles[id] != "GK")
            .ToArray();
        float closestTeammateTargets = float.PositiveInfinity;
        for (int first = 0; first < attackingOutfield.Length; first++)
        {
            for (int second = first + 1; second < attackingOutfield.Length; second++)
            {
                closestTeammateTargets = Mathf.Min(
                    closestTeammateTargets,
                    FootballPitchDimensions.DistanceMeters(
                        planned[attackingOutfield[first]].Target,
                        planned[attackingOutfield[second]].Target));
            }
        }
        Check(
            closestTeammateTargets >= 5.8f,
            "Đội tấn công phải tạo góc chuyền thay vì để nhiều mục tiêu di chuyển chụm một điểm.");

        StringName[] markedPlayers = planned
            .Where(pair => teams[pair.Key] == homeTeamId && pair.Value.Kind == PlayerIntentKind.MarkOpponent)
            .Select(pair => pair.Value.RelatedPlayerId)
            .ToArray();
        Check(markedPlayers.Distinct().Count() == markedPlayers.Length, "Các hậu vệ phải theo những đối thủ khác nhau.");
        GD.Print("PASS: bóng ở biên vẫn tạo chạy chỗ trong vòng cấm, hỗ trợ phía sau và kèm người có mục đích.");

        void AddShapePlayer(string id, StringName teamId, string role, Vector2 position)
        {
            StringName playerId = id;
            positions[playerId] = position;
            basePositions[playerId] = position;
            teams[playerId] = teamId;
            roles[playerId] = role;
        }
    }

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
        Check(
            looseBallIntents.Count(pair => pair.Value.Kind == PlayerIntentKind.ChaseLooseBall) == 2,
            "Bóng tự do phải có đúng người gần nhất của mỗi đội cùng lao vào tranh.");
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

    private static void TestDefensiveBlockSpacingAndRollingBall()
    {
        StringName homeTeamId = "block_home";
        StringName awayTeamId = "block_away";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>();
        var basePositions = new System.Collections.Generic.Dictionary<StringName, Vector2>();
        var playerTeams = new System.Collections.Generic.Dictionary<StringName, StringName>();
        var roles = new System.Collections.Generic.Dictionary<StringName, string>();

        AddPlayer("home_owner", homeTeamId, "LW", new Vector2(0.22f, 0.12f), new Vector2(0.25f, 0.18f));
        AddPlayer("home_st", homeTeamId, "ST", new Vector2(0.35f, 0.48f), new Vector2(0.22f, 0.50f));
        AddPlayer("home_rw", homeTeamId, "RW", new Vector2(0.48f, 0.82f), new Vector2(0.25f, 0.82f));
        AddPlayer("away_gk", awayTeamId, "GK", new Vector2(0.06f, 0.50f), new Vector2(0.05f, 0.50f));
        AddPlayer("away_lb", awayTeamId, "LB", new Vector2(0.72f, 0.12f), new Vector2(0.24f, 0.15f));
        AddPlayer("away_lcb", awayTeamId, "CB", new Vector2(0.70f, 0.38f), new Vector2(0.23f, 0.38f));
        AddPlayer("away_rcb", awayTeamId, "CB", new Vector2(0.82f, 0.64f), new Vector2(0.23f, 0.62f));
        AddPlayer("away_rb", awayTeamId, "RB", new Vector2(0.76f, 0.88f), new Vector2(0.24f, 0.85f));
        AddPlayer("away_dm", awayTeamId, "DM", new Vector2(0.68f, 0.50f), new Vector2(0.34f, 0.50f));
        AddPlayer("away_cm", awayTeamId, "CM", new Vector2(0.58f, 0.72f), new Vector2(0.42f, 0.70f));

        FootballWorldSnapshot world = new(
            positions,
            basePositions,
            playerTeams,
            roles,
            positions["home_owner"],
            positions["home_owner"],
            "home_owner",
            new StringName(),
            homeTeamId,
            homeTeamId,
            false,
            false);
        System.Collections.Generic.Dictionary<StringName, PlayerIntent> planned =
            new FootballIntentPlanner().Plan(world);
        PlayerIntent[] awayDefensiveIntents = planned
            .Where(pair => playerTeams[pair.Key] == awayTeamId && roles[pair.Key] != "GK")
            .Select(pair => pair.Value)
            .ToArray();
        Check(
            awayDefensiveIntents.All(intent => intent.Target.X < 0.50f),
            "Đội bảo vệ khung thành bên trái phải thu khối về nửa sân trái thay vì tản sang sân đối phương.");
        PlayerIntent[] backLineIntents = new[] { "away_lb", "away_lcb", "away_rcb", "away_rb" }
            .Select(id => planned[id])
            .Where(intent => intent.Kind != PlayerIntentKind.PressBall)
            .ToArray();
        float backLineDepthSpread = backLineIntents.Max(intent => intent.Target.X) -
                                    backLineIntents.Min(intent => intent.Target.X);
        Check(
            backLineDepthSpread < 0.13f,
            "Hàng phòng ngự phải giữ cùng một tuyến cơ bản thay vì mỗi người chạy theo một hướng.");

        var crowdedIntents = new System.Collections.Generic.Dictionary<StringName, PlayerIntent>
        {
            ["away_lb"] = new PlayerIntent(PlayerIntentKind.HoldShape, new Vector2(0.25f, 0.20f), LiveTeamPhase.Defending),
            ["away_lcb"] = new PlayerIntent(PlayerIntentKind.HoldShape, new Vector2(0.25f, 0.20f), LiveTeamPhase.Defending),
            ["away_rcb"] = new PlayerIntent(PlayerIntentKind.HoldShape, new Vector2(0.25f, 0.20f), LiveTeamPhase.Defending),
            ["away_rb"] = new PlayerIntent(PlayerIntentKind.HoldShape, new Vector2(0.25f, 0.20f), LiveTeamPhase.Defending)
        };
        TeamSpacingResolver.Resolve(world, crowdedIntents);
        float minimumSpacingMeters = float.PositiveInfinity;
        StringName[] crowdedIds = crowdedIntents.Keys.ToArray();
        for (int first = 0; first < crowdedIds.Length; first++)
        {
            for (int second = first + 1; second < crowdedIds.Length; second++)
            {
                minimumSpacingMeters = Mathf.Min(
                    minimumSpacingMeters,
                    FootballPitchDimensions.DistanceMeters(
                        crowdedIntents[crowdedIds[first]].Target,
                        crowdedIntents[crowdedIds[second]].Target));
            }
        }
        Check(minimumSpacingMeters >= 4.9f, "Bốn đồng đội không được nhận mục tiêu chụm vào cùng một điểm.");

        RollingBallPhysics rollingPhysics = new();
        RollingBallStep firstStep = rollingPhysics.Advance(
            new Vector2(0.30f, 0.50f),
            new Vector2(6f, 0f),
            0.5f);
        RollingBallStep secondStep = rollingPhysics.Advance(
            firstStep.Position,
            firstStep.VelocityMetersPerSecond,
            0.5f);
        Check(
            firstStep.Position.X > 0.30f && secondStep.Position.X > firstStep.Position.X,
            "Bóng thiếu lực phải tiếp tục lăn qua nhiều nhịp thay vì dừng ngay tại điểm kết thúc đường chuyền.");
        Check(
            firstStep.VelocityMetersPerSecond.Length() < 6f &&
            secondStep.VelocityMetersPerSecond.Length() < firstStep.VelocityMetersPerSecond.Length(),
            "Ma sát phải làm bóng giảm tốc dần theo thời gian.");

        GD.Print("PASS: khối phòng ngự giữ tuyến, đồng đội giữ cự ly và bóng lăn giảm tốc có quán tính.");

        void AddPlayer(string id, StringName teamId, string role, Vector2 position, Vector2 basePosition)
        {
            StringName playerId = id;
            positions[playerId] = position;
            basePositions[playerId] = basePosition;
            playerTeams[playerId] = teamId;
            roles[playerId] = role;
        }
    }

    private static void TestShotSelectionAndTraditionalGoalkeeper()
    {
        ShotDecisionEvaluator shotDecision = new();
        Check(
            !shotDecision.ShouldShoot("ST", 90, 45f, 8f, 20, 0f),
            "Tiền đạo không được sút cưỡng ép từ khoảng cách 40–50 mét.");
        Check(
            shotDecision.ShouldShoot("ST", 72, 18f, 6f, 6, 0.10f),
            "Tiền đạo có khoảng trống trong vùng dứt điểm hợp lý vẫn phải biết sút.");

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
        Check(
            continuationPlanner.Decide("ST", 18f, 4f, 3) == DirectAttackContinuation.Shoot,
            "Tiền đạo nhận chọc khe đối mặt trong vùng dứt điểm phải ưu tiên sút thay vì chuyền về.");
        Check(
            continuationPlanner.Decide("LW", 34f, 9f, 3) == DirectAttackContinuation.Carry,
            "Cầu thủ tấn công nhận chọc khe còn xa khung thành phải dắt bóng tiếp.");
        Check(
            continuationPlanner.Decide("CB", 18f, 4f, 3) == DirectAttackContinuation.None,
            "Quy tắc tiếp tục tấn công sau chọc khe không được áp dụng cho hậu vệ.");

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

    private static void TestFinalThirdAttackDecisions()
    {
        FinalThirdDecisionPlanner planner = new();
        PassOptionEvaluator passEvaluator = new();
        Check(
            planner.Decide("ST", 18f, 8f) == FinalThirdAction.Shoot,
            "Tiền đạo trung tâm trong vùng dứt điểm phải sút thay vì tìm đường chuyền về.");
        Check(
            planner.Decide("ST", 23f, 8f) == FinalThirdAction.Carry,
            "Tiền đạo ở sát vòng cấm nhưng chưa vào tầm sút phải tiếp tục dẫn bóng.");
        Check(
            planner.Decide("RW", 16f, 10f) == FinalThirdAction.Shoot,
            "Tiền đạo cánh đã bó vào trung lộ gần khung thành phải biết dứt điểm.");
        Check(
            planner.Decide("CM", 18f, 8f) == FinalThirdAction.None,
            "Quy tắc bắt buộc dứt điểm không được áp dụng cho mọi vị trí trên sân.");
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

        BallCarrierDecisionEvaluator carrierDecision = new();
        PassSelection uncomfortableSidePass = new("marked_teammate", -0.2f, 0f, 18f, 0.46f, 3.7f);
        Check(
            carrierDecision.ShouldKeepCarrying(false, 72, 0, 8f, uncomfortableSidePass, 0.9f),
            "Cầu thủ đang có khoảng trống phải tiếp tục dẫn bóng thay vì bị ép chuyền ngang vô nghĩa.");
        PassSelection progressivePass = new("free_runner", 0.4f, 9f, 21f, 0.25f, 6f);
        Check(
            !carrierDecision.ShouldKeepCarrying(false, 65, 0, 7f, progressivePass, 0.9f),
            "Khi đồng đội chạy phía trước và thoải mái, người cầm bóng phải biết nhả bóng đúng lúc.");
        Check(
            carrierDecision.ShouldKeepCarrying(false, 65, 20, 7f, default, 0.9f),
            "Không có người nhận hợp lệ thì engine không được chuyền chỉ vì đã giữ bóng lâu.");

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

    private void TestScenarioFactoryAndPitchLauncher()
    {
        MatchScenarioFactory factory = new();
        MatchScenarioDefinition throughBall = factory.Create(
            MatchScenarioKind.ThroughBallBreakaway,
            1f);
        Check(
            throughBall.AttackerCount == 2 && throughBall.StartsWithThroughBall,
            "Scenario chọc khe phải có tiền vệ chuyền và tiền đạo phá bẫy.");
        Vector2 receptionTarget = throughBall.ThroughBallReceptionTarget ?? Vector2.Zero;
        float receptionDistanceMeters = FootballPitchDimensions.DistanceMeters(
            receptionTarget,
            new Vector2(0.994f, 0.50f));
        Check(
            receptionDistanceMeters is >= 33f and <= 36f,
            "Điểm nhận đường chọc khe phải cách khung thành xấp xỉ 35 mét.");

        StringName attackingTeamId = "scenario_attack";
        StringName defendingTeamId = "scenario_defense";
        StringName receiverId = "scenario_runner";
        var offsidePositions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [receiverId] = throughBall.SupportingAttackerPositions[0],
            ["scenario_defender_1"] = throughBall.DefenderPositions[0],
            ["scenario_defender_2"] = throughBall.DefenderPositions[1],
            ["scenario_goalkeeper"] = new Vector2(0.96f, 0.50f)
        };
        var offsideTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [receiverId] = attackingTeamId,
            ["scenario_defender_1"] = defendingTeamId,
            ["scenario_defender_2"] = defendingTeamId,
            ["scenario_goalkeeper"] = defendingTeamId
        };
        Check(
            !new OffsideRule().IsOffside(
                receiverId,
                attackingTeamId,
                throughBall.BallCarrierPosition,
                1f,
                offsidePositions,
                offsideTeams),
            "Tiền đạo phải còn đứng trên bẫy việt vị tại thời điểm tiền vệ chọc khe.");

        MatchScenarioDefinition twoVersusOne = factory.Create(
            MatchScenarioKind.TwoAttackersVersusOneDefender,
            -1f);
        MatchScenarioDefinition threeVersusTwo = factory.Create(
            MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            -1f);
        Check(
            twoVersusOne.AttackerCount == 2 && twoVersusOne.DefenderCount == 1,
            "Scenario 2 đánh 1 phải dựng đúng quân số tham gia chính.");
        Check(
            threeVersusTwo.AttackerCount == 3 && threeVersusTwo.DefenderCount == 2,
            "Scenario 3 đánh 2 phải dựng đúng quân số tham gia chính.");

        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        int completedThroughBallReceptions = 0;
        for (int seed = 1; seed <= 10; seed++)
        {
            FootballMatchSimulation sample = new FootballMatchSimulation().setup(teams[0], teams[1], 7000 + seed);
            sample.use_live_pitch_events = true;
            MatchPitch2D samplePitch = new();
            AddChild(samplePitch);
            samplePitch.SetMatch(sample);
            samplePitch.StartScenario(MatchScenarioKind.ThroughBallBreakaway);
            samplePitch.SetPlaying(true);
            for (int frame = 0; frame < 30; frame++)
            {
                samplePitch._Process(0.05d);
            }
            if (samplePitch.CompletedPasses > 0)
            {
                completedThroughBallReceptions++;
            }
            samplePitch.QueueFree();
        }
        Check(
            completedThroughBallReceptions >= 4,
            $"Chọc khe hợp lý không được bị hậu vệ đoạt 100% qua 10 seed; " +
            $"tiền đạo mới nhận được {completedThroughBallReceptions}/10 lần.");

        int observedThreeVersusTwoFlights = 0;
        int extremeBackwardFlights = 0;
        int emptyLongPasses = 0;
        System.Collections.Generic.Dictionary<string, int> extremeBackwardFlightTypes = new();
        for (int seed = 1; seed <= 10; seed++)
        {
            FootballMatchSimulation sample = new FootballMatchSimulation().setup(teams[0], teams[1], 8000 + seed);
            sample.use_live_pitch_events = true;
            MatchPitch2D samplePitch = new();
            AddChild(samplePitch);
            samplePitch.SetMatch(sample);
            samplePitch.StartScenario(MatchScenarioKind.ThreeAttackersVersusTwoDefenders);
            samplePitch.SetPlaying(true);
            Vector2 previousFlightStart = new(-1f, -1f);
            Vector2 previousFlightTarget = new(-1f, -1f);
            for (int frame = 0; frame < 120; frame++)
            {
                samplePitch._Process(0.05d);
                if (!samplePitch.IsBallInFlight ||
                    samplePitch.BallActionSourceTeamId != sample.home.team.id ||
                    samplePitch.BallFlightStart.IsEqualApprox(previousFlightStart) &&
                    samplePitch.BallFlightTarget.IsEqualApprox(previousFlightTarget))
                {
                    continue;
                }

                previousFlightStart = samplePitch.BallFlightStart;
                previousFlightTarget = samplePitch.BallFlightTarget;
                observedThreeVersusTwoFlights++;
                float forwardGainMeters = -(previousFlightTarget.X - previousFlightStart.X) *
                                          FootballPitchDimensions.LengthMeters;
                float flightDistanceMeters = FootballPitchDimensions.DistanceMeters(
                    previousFlightStart,
                    previousFlightTarget);
                bool isPass = samplePitch.BallActionType is "Pass" or "ThroughBall" or "Cross";
                if (isPass && flightDistanceMeters > 18f)
                {
                    float nearestTeammateDistanceMeters = sample.home.squad.starter_ids
                        .Where(samplePitch.CurrentPositions.ContainsKey)
                        .Min(playerId => FootballPitchDimensions.DistanceMeters(
                            samplePitch.CurrentPositions[playerId],
                            previousFlightTarget));
                    if (nearestTeammateDistanceMeters > 12f)
                    {
                        emptyLongPasses++;
                    }
                }
                if (forwardGainMeters < -15f && flightDistanceMeters > 30f)
                {
                    extremeBackwardFlights++;
                    extremeBackwardFlightTypes.TryGetValue(samplePitch.BallActionType, out int existingCount);
                    extremeBackwardFlightTypes[samplePitch.BallActionType] = existingCount + 1;
                }
            }
            samplePitch.QueueFree();
        }
        Check(
            observedThreeVersusTwoFlights > 0 && extremeBackwardFlights == 0,
            $"Trong 3v2 không được có đường chuyền lùi quá 15 m và dài hơn 30 m; " +
            $"đã thấy {extremeBackwardFlights}/{observedThreeVersusTwoFlights} quỹ đạo vi phạm " +
            $"({string.Join(", ", extremeBackwardFlightTypes.Select(pair => $"{pair.Key}: {pair.Value}"))}).");
        Check(
            emptyLongPasses == 0,
            $"Đường chuyền dài phải có đồng đội đủ gần điểm đến; đã thấy {emptyLongPasses} đường bóng vào vùng trống.");

        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 5150);
        simulation.use_live_pitch_events = true;
        MatchPitch2D pitch = new();
        AddChild(pitch);
        pitch.SetMatch(simulation);
        Check(
            pitch.StartScenario(MatchScenarioKind.ThroughBallBreakaway) &&
            pitch.ActiveScenario == MatchScenarioKind.ThroughBallBreakaway &&
            pitch.IsBallInFlight,
            "Pitch launcher phải bắt đầu scenario chọc khe bằng một đường bóng thật của engine.");
        float visibleLeadMeters = simulation.home.squad.starter_ids
            .Min(playerId => FootballPitchDimensions.DistanceMeters(
                pitch.CurrentPositions[playerId],
                pitch.BallFlightTarget));
        Check(
            visibleLeadMeters >= 3.8f,
            "Trên sân 2D, bóng chọc khe phải hướng vào khoảng trống đủ xa trước người nhận.");
        Check(
            pitch.StartScenario(MatchScenarioKind.TwoAttackersVersusOneDefender) &&
            pitch.CurrentBallOwnerId != new StringName() &&
            !pitch.IsBallInFlight,
            "Scenario 2 đánh 1 phải giao bóng cho một cầu thủ để engine tự quyết định.");
        Check(
            pitch.StartScenario(MatchScenarioKind.ThreeAttackersVersusTwoDefenders) &&
            pitch.ActiveScenario == MatchScenarioKind.ThreeAttackersVersusTwoDefenders,
            "Pitch launcher phải chuyển được sang scenario 3 đánh 2 mà không cần chờ trận mới.");
        pitch.QueueFree();

        GD.Print(
            $"PASS: sandbox dựng đúng chọc khe 35 m, 2 đánh 1 và 3 đánh 2; " +
            $"tiền đạo nhận được {completedThroughBallReceptions}/10 đường chọc khe mẫu.");
    }

    private void TestPitchPauseAndReset()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation firstSimulation = new FootballMatchSimulation().setup(teams[0], teams[1], 101);
        firstSimulation.use_live_pitch_events = true;
        MatchPitch2D pitch = new();
        AddChild(pitch);
        pitch.SetMatch(firstSimulation);

        var waitingPositions = pitch.CurrentPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        Vector2 waitingBall = pitch.BallPosition;
        pitch._Process(1d);
        Check(
            pitch.CurrentPositions.All(pair => pair.Value.IsEqualApprox(waitingPositions[pair.Key])) &&
            pitch.BallPosition.IsEqualApprox(waitingBall),
            "Tạo trận mới nhưng chưa bắt đầu phải giữ nguyên toàn bộ cầu thủ và bóng.");

        pitch.SetPlaying(true);
        pitch._Process(0.6d);
        pitch.SetPlaying(false);
        var pausedPositions = pitch.CurrentPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        Vector2 pausedBall = pitch.BallPosition;
        int pausedActions = pitch.CompletedPasses + pitch.Dribbles + pitch.Interceptions;
        pitch._Process(2d);
        Check(
            pitch.CurrentPositions.All(pair => pair.Value.IsEqualApprox(pausedPositions[pair.Key])) &&
            pitch.BallPosition.IsEqualApprox(pausedBall),
            "Tạm dừng phải đóng băng cầu thủ và bóng.");
        Check(
            pitch.CompletedPasses + pitch.Dribbles + pitch.Interceptions == pausedActions,
            "Tạm dừng không được tiếp tục giải quyết hành động ngầm.");

        FootballMatchSimulation secondSimulation = new FootballMatchSimulation().setup(teams[0], teams[1], 202);
        secondSimulation.use_live_pitch_events = true;
        pitch.SetMatch(secondSimulation);
        Check(!pitch.IsPlaying && pitch.LastActionName == "Chuẩn bị giao bóng", "Trận mới phải reset trạng thái thi đấu.");
        var resetPositions = pitch.CurrentPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        pitch._Process(1d);
        Check(
            pitch.CurrentPositions.All(pair => pair.Value.IsEqualApprox(resetPositions[pair.Key])),
            "Đường bóng từ trận trước không được tiếp tục sau khi tạo trận mới.");
        pitch.QueueFree();
        GD.Print("PASS: chưa bắt đầu, tạm dừng và tạo trận mới đều đóng băng/reset sân đúng cách.");
    }

    private void TestPlaybackSpeedDoesNotChangeFootball()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation realTimeSimulation = new FootballMatchSimulation().setup(teams[0], teams[1], 909);
        FootballMatchSimulation fastSimulation = new FootballMatchSimulation().setup(teams[0], teams[1], 909);
        realTimeSimulation.use_live_pitch_events = true;
        fastSimulation.use_live_pitch_events = true;

        LiveMatchRuntime realTimeRuntime = new();
        LiveMatchRuntime fastRuntime = new();
        realTimeRuntime.SetSpeed(MatchPlaybackSpeed.RealTime);
        fastRuntime.SetSpeed(MatchPlaybackSpeed.Fast);
        MatchPitch2D realTimePitch = new();
        MatchPitch2D fastPitch = new();
        AddChild(realTimePitch);
        AddChild(fastPitch);
        realTimePitch.AttachRuntime(realTimeRuntime);
        fastPitch.AttachRuntime(fastRuntime);
        realTimePitch.SetMatch(realTimeSimulation);
        fastPitch.SetMatch(fastSimulation);
        realTimePitch.SetPlaying(true);
        fastPitch.SetPlaying(true);
        realTimeRuntime.Start();
        fastRuntime.Start();

        realTimeRuntime.Advance(0.60d);
        fastRuntime.Advance(0.0028d);
        realTimePitch.AdvanceGameTime(realTimeRuntime.LastAdvancedGameSeconds);
        fastPitch.AdvanceGameTime(fastRuntime.LastAdvancedGameSeconds);
        Check(
            Math.Abs(realTimeRuntime.LastAdvancedGameSeconds - fastRuntime.LastAdvancedGameSeconds) < 0.0001d,
            "Hai tốc độ phải truyền cùng lượng game-time khi quy đổi tương đương.");
        Check(
            realTimePitch.CurrentPositions.All(pair =>
                pair.Value.DistanceTo(fastPitch.CurrentPositions[pair.Key]) < 0.0001f) &&
            realTimePitch.BallPosition.DistanceTo(fastPitch.BallPosition) < 0.0001f,
            "Tăng tốc chỉ được thay thời gian chờ ngoài đời, không được thay kết quả bóng đá của cùng một seed.");
        realTimePitch.QueueFree();
        fastPitch.QueueFree();
        GD.Print("PASS: realtime và fast-forward dùng chung game-time nên cho cùng diễn biến với cùng seed.");
    }

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

    private static void TestPitchScaleAndMovementSpeed()
    {
        Rect2 field = MatchPitch2D.CalculateFieldRect(new Vector2(1900f, 500f));
        float aspectRatio = field.Size.X / field.Size.Y;
        Check(
            Math.Abs(aspectRatio - FootballPitchDimensions.AspectRatio) < 0.001f,
            "Sân 2D phải giữ đúng tỷ lệ 105:68 khi panel quá rộng.");
        MatchPitch2D displayPitch = new();
        displayPitch.SetExpandedDisplay(false);
        Vector2 compactSize = displayPitch.CustomMinimumSize;
        displayPitch.SetExpandedDisplay(true);
        Check(
            compactSize.Y > 0f && displayPitch.CustomMinimumSize == Vector2.Zero,
            "Sân lớn phải dùng vùng trống của panel thay vì ép chiều cao làm cắt mất đáy sân.");
        displayPitch.SetMarkerLabelMode(PlayerMarkerLabelMode.SquadNumber);
        Check(
            displayPitch.MarkerLabelMode == PlayerMarkerLabelMode.SquadNumber,
            "Sân phải đổi được giữa nhãn vị trí và số áo để quan sát/debug.");
        displayPitch.Free();

        StringName homeTeamId = "orientation_home";
        StringName awayTeamId = "orientation_away";
        MatchSideController sideController = new();
        Vector2 homeLeftBack = sideController.FormationPosition(0.14f, 0.70f, homeTeamId, homeTeamId);
        Vector2 homeRightBack = sideController.FormationPosition(0.86f, 0.70f, homeTeamId, homeTeamId);
        Vector2 awayLeftBack = sideController.FormationPosition(0.14f, 0.70f, awayTeamId, homeTeamId);
        Check(
            homeLeftBack.Y > 0.5f && homeRightBack.Y < 0.5f,
            "Đội tấn công sang trái phải đặt LB/LW phía dưới và RB/RW phía trên theo hướng nhìn của cầu thủ.");
        Check(
            awayLeftBack.Y < 0.5f,
            "Đội tấn công sang phải phải đặt LB/LW phía trên, đối xứng đúng với đội còn lại.");
        sideController.SwitchEnds();
        Vector2 switchedHomeLeftBack = sideController.FormationPosition(0.14f, 0.70f, homeTeamId, homeTeamId);
        Check(
            switchedHomeLeftBack.Y < 0.5f,
            "Sau khi đổi sân, cánh trái/phải phải đổi phía cùng hướng tấn công của đội.");

        StringName homeLeftWingId = "orientation_home_lw";
        StringName awayLeftWingId = "orientation_away_lw";
        var orientationPositions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [homeLeftWingId] = new Vector2(0.62f, 0.82f),
            [awayLeftWingId] = new Vector2(0.38f, 0.18f)
        };
        var orientationTeams = new System.Collections.Generic.Dictionary<StringName, StringName>
        {
            [homeLeftWingId] = homeTeamId,
            [awayLeftWingId] = awayTeamId
        };
        var orientationRoles = new System.Collections.Generic.Dictionary<StringName, string>
        {
            [homeLeftWingId] = "LW",
            [awayLeftWingId] = "LW"
        };
        FootballWorldSnapshot orientationWorld = new(
            orientationPositions,
            orientationPositions,
            orientationTeams,
            orientationRoles,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new StringName(),
            new StringName(),
            homeTeamId,
            homeTeamId,
            false,
            false,
            true);
        Vector2 homeLeftWingRun = AttackingRoleTargeter.RunnerTarget(
            orientationWorld,
            homeLeftWingId,
            homeTeamId);
        Vector2 awayLeftWingRun = AttackingRoleTargeter.RunnerTarget(
            orientationWorld,
            awayLeftWingId,
            awayTeamId);
        Check(
            homeLeftWingRun.Y > 0.5f && awayLeftWingRun.Y < 0.5f,
            "AI chạy chỗ phải giữ đúng cánh trái theo hướng tấn công, không kéo LW hai đội về cùng một phía màn hình.");

        StringName playerId = "movement_test";
        var positions = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [playerId] = new Vector2(0.5f, 0.5f)
        };
        var targets = new System.Collections.Generic.Dictionary<StringName, Vector2>
        {
            [playerId] = new Vector2(0.025f, 0.5f)
        };
        var intents = new System.Collections.Generic.Dictionary<StringName, PlayerIntent>
        {
            [playerId] = new PlayerIntent(
                PlayerIntentKind.RunIntoSpace,
                targets[playerId],
                LiveTeamPhase.InPossession)
        };
        var paceRatings = new System.Collections.Generic.Dictionary<StringName, int>
        {
            [playerId] = 99
        };
        FootballMovementController movement = new();
        Vector2 start = positions[playerId];
        for (int frame = 0; frame < 60; frame++)
        {
            movement.Advance(positions, targets, intents, paceRatings, 1f / 60f);
        }

        float travelledMeters = FootballPitchDimensions.DistanceMeters(start, positions[playerId]);
        Check(travelledMeters is > 1f and < 9.5f, "Một cầu thủ không được chạy hết nửa sân chỉ trong một giây.");
        GD.Print("PASS: tỷ lệ sân 105:68 và tốc độ cầu thủ theo mét/giây được giữ đúng.");
    }

    private static void TestLiveMatchClock()
    {
        LiveMatchClock clock = new();
        clock.SetSpeed(MatchPlaybackSpeed.RealTime);
        clock.Start();
        Check(clock.Advance(1d) == 0, "Một giây thực chưa được tăng một phút trận đấu.");
        Check(Math.Abs(clock.ElapsedGameSeconds - 1d) < 0.001d, "Thời gian thực phải chạy đúng một giây game mỗi giây ngoài đời.");
        Check(clock.DisplayTime == "00:01", "Đồng hồ thời gian thực phải hiển thị cả phút và giây.");
        Check(clock.Advance(59d) == 1 && clock.DisplayTime == "01:00", "Đủ 60 giây mới được tăng một phút trận đấu.");
        clock.Pause();
        clock.Advance(15d);
        Check(clock.DisplayTime == "01:00", "Tạm dừng không được làm trôi thời gian trận đấu.");
        clock.SetSpeed(MatchPlaybackSpeed.Fast);
        clock.Start();
        Check(clock.Advance(0.28d) == 1, "Chế độ nhanh cũ phải tiếp tục tăng xấp xỉ một phút mỗi 0,28 giây.");
        GD.Print("PASS: đồng hồ trận đấu hỗ trợ thời gian thực, pause/resume và các tốc độ nhanh.");
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
        Check(players.All(player => player.passing is >= 1 and <= 99 && player.tackling is >= 1 and <= 99), "Thuộc tính chuyên môn phải nằm trong thang 1-99.");
        foreach (FormationDefinition item in catalog.all()) Check(item.slots.Count == 11, "Mỗi sơ đồ phải có 11 vị trí.");

        Array<FootballTeam> sampleTeams = new SampleDataFactory().create_teams();
        FootballTeam sainoo = sampleTeams.Single(team => team.id == "sainoo_fc");
        Check(
            sainoo.players.Count == 11 &&
            sainoo.match_squad.starter_ids.Count == 11 &&
            sainoo.match_squad.substitute_ids.Count == 0,
            "Sainoo FC phải có đúng 11 huyền thoại đá chính và chưa có cầu thủ dự bị.");
        Check(
            sainoo.match_squad.formation_id == "4_1_2_3" &&
            sainoo.match_squad.starter_slots.Count == 11 &&
            sainoo.match_squad.validate_against(sainoo.players).Length == 0,
            "Sainoo FC phải được xếp đủ đội hình 4-1-2-3 hợp lệ.");
        Check(
            StarterName("gk") == "Iker Casillas" &&
            StarterName("dm") == "Frank Rijkaard" &&
            StarterName("am") == "Johan Cruyff" &&
            StarterName("st") == "Cristiano Ronaldo",
            "Các cầu thủ Sainoo FC phải đứng đúng vai trò GK, DM, AM và ST đã yêu cầu.");
        Check(
            sainoo.players.Select(player => player.SquadNumber).Distinct().Count() == 11,
            "Mười một cầu thủ Sainoo FC phải có số áo không trùng nhau.");
        GD.Print("PASS: quân số không giới hạn, danh sách trận 11 + 12.");

        string StarterName(string slotId)
        {
            StringName playerId = sainoo.match_squad.starter_slots[new StringName(slotId)].AsStringName();
            return sainoo.get_player(playerId)?.display_name ?? string.Empty;
        }
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

    private static void TestLiveMatchRules()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 77);
        simulation.use_live_pitch_events = true;
        StringName yellowOffender = simulation.home.squad.starter_ids[1];
        StringName redOffender = simulation.home.squad.starter_ids[2];
        StringName victim = simulation.away.squad.starter_ids[1];
        Check(simulation.register_live_foul(teams[0].id, yellowOffender, victim, "yellow")?.event_type == "yellow_card", "Phạm lỗi trực tiếp phải tạo thẻ vàng.");
        Check(simulation.register_live_restart(teams[1].id, "corner")?.event_type == "corner", "Sân 2D phải đăng ký được phạt góc.");
        Check(
            simulation.RegisterLiveOffside(teams[1].id, victim)?.event_type == "offside",
            "Sân 2D phải ghi nhận được lỗi việt vị.");
        Check(simulation.register_live_shot(teams[1].id, simulation.away.squad.starter_ids[8], "parried", simulation.home.squad.starter_ids[0]) is not null, "Thủ môn đẩy bóng phải được ghi nhận là cú sút trúng đích.");
        simulation.RegisterLivePassAttempt(teams[1].id);
        simulation.RegisterLivePassAttempt(teams[1].id);
        simulation.RegisterLivePassCompletion(teams[1].id);
        simulation.RegisterLiveFirstTouchError(teams[1].id);
        Check(simulation.register_live_foul(teams[0].id, redOffender, victim, "red")?.event_type == "red_card", "Phạm lỗi ngăn cơ hội phải tạo thẻ đỏ.");
        Check(simulation.home.stats["fouls"].AsInt32() == 2, "Thống kê phải nhận phạm lỗi từ sân 2D.");
        Check(simulation.home.stats["yellow_cards"].AsInt32() == 1 && simulation.home.stats["red_cards"].AsInt32() == 1, "Thẻ vàng và đỏ phải được thống kê.");
        Check(simulation.away.stats["corners"].AsInt32() == 1, "Phạt góc phải được cộng cho đúng đội.");
        Check(
            simulation.away.stats["passes_attempted"].AsInt32() == 2 &&
            simulation.away.stats["passes_completed"].AsInt32() == 1 &&
            simulation.away.stats["first_touch_errors"].AsInt32() == 1,
            "Live engine phải ghi được số đường chuyền, chuyền thành công và lỗi đỡ bước một để hiệu chỉnh trận đấu.");
        Check(simulation.home.squad.starter_ids.Count == 10, "Cầu thủ nhận thẻ đỏ phải rời sân.");
        int liveFouls = simulation.home.stats["fouls"].AsInt32() + simulation.away.stats["fouls"].AsInt32();
        for (int minute = 0; minute < 10; minute++) simulation.advance_minute();
        Check(simulation.home.stats["fouls"].AsInt32() + simulation.away.stats["fouls"].AsInt32() == liveFouls, "Chế độ trực tiếp không được sinh phạm lỗi ngẫu nhiên ngoài sân 2D.");
        GD.Print("PASS: phạm lỗi, thẻ đỏ, phạt góc và cú sút bật ra được đồng bộ từ sân 2D.");
    }

    private void TestPitchMovement()
    {
        Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 42);
        simulation.use_live_pitch_events = true;
        var pitch = new MatchPitch2D();
        AddChild(pitch);
        pitch.SetMatch(simulation);
        pitch.SetPlaying(true);
        Check(pitch.CurrentPositions.Count == 22, "Sân 2D phải hiển thị đủ 22 cầu thủ.");
        Check(pitch.CurrentIntents.Count == 22, "Mỗi cầu thủ trên sân phải có một ý định thi đấu riêng.");
        Check(
            pitch.CurrentIntents.Values.Count(intent => intent.Kind == PlayerIntentKind.CarryBall) == 1,
            "Đội có bóng phải xác định đúng một người đang dẫn bóng.");
        Check(
            pitch.CurrentIntents.Values.Count(intent => intent.Kind == PlayerIntentKind.SupportBall) >= 2,
            "Cầu thủ gần bóng phải chủ động mở góc hỗ trợ.");
        Check(
            pitch.CurrentIntents.Values.Count(intent => intent.Kind == PlayerIntentKind.PressBall) == 1,
            "Đội phòng ngự phải có một cầu thủ pressing bóng.");
        Check(
            pitch.CurrentIntents.Values.Count(intent => intent.Kind == PlayerIntentKind.CoverPress) == 1,
            "Cầu thủ pressing phải có đồng đội bọc lót.");
        Check(
            pitch.CurrentIntents.Values.Count(intent => intent.Kind == PlayerIntentKind.MarkOpponent) >= 2,
            "Hàng phòng ngự phải theo các mối đe dọa thay vì chạy ngẫu nhiên.");
        var initial = pitch.CurrentPositions.ToDictionary(pair => pair.Key, pair => pair.Value);
        Vector2 initialBall = pitch.BallPosition;
        Array<FootballMatchEvent> events = simulation.advance_minute();
        pitch.AnimateMinute(events);
        pitch._Process(0.35);
        bool observedStraightFlight = false;
        for (int frame = 0; frame < 140 && !observedStraightFlight; frame++)
        {
            if (pitch.IsBallInFlight && pitch.BallFlightStart.DistanceTo(pitch.BallFlightTarget) > 0.01f)
            {
                Vector2 flightStart = pitch.BallFlightStart;
                Vector2 flightTarget = pitch.BallFlightTarget;
                pitch._Process(0.025d);
                if (pitch.IsBallInFlight &&
                    pitch.BallFlightStart.IsEqualApprox(flightStart) &&
                    pitch.BallFlightTarget.IsEqualApprox(flightTarget))
                {
                    Check(
                        DistanceFromSegment(pitch.BallPosition, flightStart, flightTarget) < 0.0001f,
                        "Bóng đang bay không được tự uốn hình chữ L hoặc chữ U khi chưa chạm cầu thủ.");
                    observedStraightFlight = true;
                }
            }
            else
            {
                pitch._Process(0.05d);
            }
        }
        Check(observedStraightFlight, "Kiểm thử phải quan sát được ít nhất một quỹ đạo bóng đang bay.");
        int moving = pitch.CurrentPositions.Count(pair => pair.Value.DistanceTo(initial[pair.Key]) > 0.001f);
        Check(moving >= 18, "Phần lớn cầu thủ phải chuyển động liên tục.");
        int leavingZones = pitch.TargetPositions.Count(pair => pair.Value.DistanceTo(pitch.BasePositions[pair.Key]) > 0.10f);
        float longestRun = pitch.TargetPositions.Max(pair => pair.Value.DistanceTo(pitch.BasePositions[pair.Key]));
        Check(leavingZones >= 10, "Cả hai khối đội hình phải dịch chuyển theo pha bóng, không neo trong vùng gốc.");
        Check(longestRun >= 0.18f, "Phải có cầu thủ thực hiện một pha chạy chỗ dài.");
        Check(pitch.BallPosition.DistanceTo(initialBall) > 0.01f, "Bóng phải được chuyền hoặc dẫn theo pha bóng.");
        Check(simulation.last_possession_team_id != new StringName(), "Engine phải truyền đội kiểm soát bóng cho sân 2D.");
        Check(pitch.BallPosition.X is >= 0 and <= 1 && pitch.BallPosition.Y is >= 0 and <= 1, "Bóng phải nằm trong vùng mô phỏng.");
        for (int step = 0; step < 420; step++)
        {
            if (step % 5 == 0 && !simulation.is_finished)
                pitch.AnimateMinute(simulation.advance_minute());
            pitch._Process(0.1);
        }
        for (int settle = 0; settle < 200; settle++) pitch._Process(0.1);
        int resolvedActions = pitch.CompletedPasses + pitch.Dribbles + pitch.Interceptions +
                              pitch.Clearances + pitch.LooseBallRecoveries;
        Check(
            resolvedActions >= 3,
            $"Một pha sở hữu bóng phải tạo ra nhiều quyết định; hiện có chuyền={pitch.CompletedPasses}, " +
            $"dẫn={pitch.Dribbles}, cắt={pitch.Interceptions}, phá={pitch.Clearances}, bóng hai={pitch.LooseBallRecoveries}, " +
            $"hành động={pitch.LastActionName}, loose={pitch.IsLooseBall}, owner={pitch.CurrentBallOwnerId}, " +
            $"restart={pitch.PendingRestartType}, flight={pitch.IsBallInFlight}.");
        Check(
            pitch.LooseBallRecoveries >= 1 || pitch.GroundDuelExchanges >= 2,
            "Trận mẫu phải tạo bóng tự do hoặc một pha tranh chấp mặt đất nhiều nhịp; " +
            "không được phụ thuộc vào đúng một kết quả ngẫu nhiên của đường chuyền.");
        Check(pitch.LastActionName != "Chuẩn bị giao bóng", "Sân 2D phải công bố hành động cầu thủ vừa lựa chọn.");
        pitch.QueueFree();
        GD.Print("PASS: 22 cầu thủ có ý định riêng, hỗ trợ, chạy chỗ, pressing, bọc lót và chơi bóng.");
    }

    private void TestUiIntegration()
    {
        var scene = GD.Load<PackedScene>("res://scenes/main.tscn");
        var main = scene.Instantiate<Main>();
        AddChild(main);
        Check(main.teams.Count == 5, "UI phải nhận đủ năm đội, bao gồm Sainoo FC, từ C#.");
        main.ChooseSelectedTeam();
        Check(main.managed_team is not null, "UI phải chọn được CLB.");
        main.ShowMatchView();
        main.MatchView.PrepareScenario(MatchScenarioKind.TwoAttackersVersusOneDefender);
        Check(
            main.MatchView.ActiveScenario == MatchScenarioKind.TwoAttackersVersusOneDefender,
            "Menu cạnh nút tạo trận phải khởi chạy được sandbox tình huống.");
        main.MatchView.PauseMatch();
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

    private static float DistanceFromSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f)
        {
            return point.DistanceTo(start);
        }

        float progress = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * progress);
    }
}
