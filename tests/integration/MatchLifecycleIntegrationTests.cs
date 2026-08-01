using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
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

