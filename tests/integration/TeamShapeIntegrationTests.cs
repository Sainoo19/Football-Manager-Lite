using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
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
}

