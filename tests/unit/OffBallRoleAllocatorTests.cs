using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public static class OffBallRoleAllocatorTests
{
    private static readonly StringName Home = "home";
    private static readonly StringName Away = "away";

    public static void Run()
    {
        VerifyParticipationChangesWithRegionalAdvantage();
        VerifyWideFinalThirdOccupiesDistinctBoxZones();
        VerifyWeakSidePlayerHoldsWidth();
        VerifyDangerousCentreBackDoesNotAbandonGoalToPress();
        VerifyAssignmentCommitmentPreventsTargetFlicker();
        GD.Print("PASS: M3 allocator cấp phát support/run/width/box/press động và giữ commitment ổn định.");
    }

    private static void VerifyParticipationChangesWithRegionalAdvantage()
    {
        FootballWorldSnapshot advantage = CreateWorld(
            LiveTeamPhase.CounterAttack,
            new Vector2(0.58f, 0.50f),
            clusteredDefenders: 1);
        FootballWorldSnapshot underload = CreateWorld(
            LiveTeamPhase.CounterAttack,
            new Vector2(0.58f, 0.50f),
            clusteredDefenders: 5);
        OffBallRoleAllocator allocator = new(OffBallParticipationConfiguration.CreateM3Defaults());
        Dictionary<StringName, PlayerIntent> advantagePlan = allocator.Allocate(advantage);
        Dictionary<StringName, PlayerIntent> underloadPlan = allocator.Allocate(underload);
        int advantageRunners = CountRunners(advantagePlan, advantage);
        int underloadRunners = CountRunners(underloadPlan, underload);
        int advantageSupport = CountSupport(advantagePlan, advantage);
        int underloadSupport = CountSupport(underloadPlan, underload);

        Check(advantageRunners > underloadRunners && advantageSupport < underloadSupport,
            $"Lợi thế quân số phải gửi thêm runner, underload phải tăng support; " +
            $"advantage={advantageRunners}/{advantageSupport}, underload={underloadRunners}/{underloadSupport}.");
    }

    private static void VerifyWideFinalThirdOccupiesDistinctBoxZones()
    {
        FootballWorldSnapshot world = CreateWorld(
            LiveTeamPhase.FinalThird,
            new Vector2(0.81f, 0.14f),
            clusteredDefenders: 3,
            ownerId: "home_lw");
        Dictionary<StringName, PlayerIntent> plan = new OffBallRoleAllocator(
            OffBallParticipationConfiguration.CreateM3Defaults()).Allocate(world);
        OffBallAssignmentKind[] boxAssignments = plan
            .Where(pair => world.PlayerTeams[pair.Key] == Home)
            .Select(pair => pair.Value.Assignment)
            .Where(assignment => assignment is OffBallAssignmentKind.AttackBoxNearPost or
                OffBallAssignmentKind.AttackBoxFarPost or OffBallAssignmentKind.AttackBoxCutBackZone)
            .ToArray();

        Check(boxAssignments.Contains(OffBallAssignmentKind.AttackBoxNearPost) &&
              boxAssignments.Contains(OffBallAssignmentKind.AttackBoxFarPost) &&
              boxAssignments.Contains(OffBallAssignmentKind.AttackBoxCutBackZone),
            "Tấn công biên ở final third phải chiếm near post, far post và cut-back bằng ba cầu thủ khác nhau.");
    }

    private static void VerifyWeakSidePlayerHoldsWidth()
    {
        FootballWorldSnapshot world = CreateWorld(
            LiveTeamPhase.Progression,
            new Vector2(0.62f, 0.14f),
            clusteredDefenders: 2,
            ownerId: "home_lw");
        Dictionary<StringName, PlayerIntent> plan = new OffBallRoleAllocator(
            OffBallParticipationConfiguration.CreateM3Defaults()).Allocate(world);
        Check(plan["home_rw"].Assignment == OffBallAssignmentKind.HoldWidth &&
              plan["home_rw"].Target.Y > 0.70f,
            "Cầu thủ weak-side phải giữ chiều rộng để chuẩn bị switch thay vì chạy vào bóng.");
    }

    private static void VerifyDangerousCentreBackDoesNotAbandonGoalToPress()
    {
        FootballWorldSnapshot world = CreateWorld(
            LiveTeamPhase.Progression,
            new Vector2(0.76f, 0.50f),
            clusteredDefenders: 3);
        Dictionary<StringName, Vector2> positions = (Dictionary<StringName, Vector2>)world.Positions;
        positions["away_cb1"] = new Vector2(0.80f, 0.50f);
        positions["away_dm"] = new Vector2(0.75f, 0.43f);
        Dictionary<StringName, PlayerIntent> plan = new OffBallRoleAllocator(
            OffBallParticipationConfiguration.CreateM3Defaults()).Allocate(world);
        Check(plan["away_dm"].Assignment == OffBallAssignmentKind.PressBall &&
              plan["away_cb1"].Assignment != OffBallAssignmentKind.PressBall,
            "CB bảo vệ trung lộ gần gôn không được bỏ vị trí khi DM có thể pressing với khoảng cách tương đương.");
    }

    private static void VerifyAssignmentCommitmentPreventsTargetFlicker()
    {
        FootballWorldSnapshot firstWorld = CreateWorld(
            LiveTeamPhase.Progression,
            new Vector2(0.55f, 0.48f),
            clusteredDefenders: 2,
            gameTime: 1f);
        FootballWorldSnapshot secondWorld = CreateWorld(
            LiveTeamPhase.Progression,
            new Vector2(0.56f, 0.50f),
            clusteredDefenders: 2,
            gameTime: 1.4f);
        OffBallParticipationConfiguration configuration = OffBallParticipationConfiguration.CreateM3Defaults();
        OffBallIntentCoordinator coordinator = new(
            new FootballIntentPlanner(new OffBallRoleAllocator(configuration)),
            configuration);
        Dictionary<StringName, PlayerIntent> first = coordinator.Plan(firstWorld);
        Dictionary<StringName, PlayerIntent> second = coordinator.Plan(secondWorld);
        StringName committedPlayer = first.Keys.First(id =>
            first[id].Assignment == OffBallAssignmentKind.OfferShortSupport);
        Check(first[committedPlayer].Target.IsEqualApprox(second[committedPlayer].Target),
            "Support assignment phải giữ target trong minimum commitment window để tránh giật hướng.");
    }

    private static FootballWorldSnapshot CreateWorld(
        LiveTeamPhase homePhase,
        Vector2 ballPosition,
        int clusteredDefenders,
        StringName? ownerId = null,
        float gameTime = 0f)
    {
        Dictionary<StringName, Vector2> positions = new();
        Dictionary<StringName, Vector2> bases = new();
        Dictionary<StringName, StringName> teams = new();
        Dictionary<StringName, string> roles = new();
        AddTeam(positions, bases, teams, roles, Home, "home", false);
        AddTeam(positions, bases, teams, roles, Away, "away", true);
        string[] clustered = { "away_dm", "away_cm", "away_lb", "away_rb", "away_cb1" };
        for (int index = 0; index < clusteredDefenders && index < clustered.Length; index++)
        {
            positions[clustered[index]] = new Vector2(
                ballPosition.X + 0.035f + index * 0.012f,
                ballPosition.Y - 0.12f + index * 0.06f);
        }
        StringName owner = ownerId ?? new StringName("home_cm");
        positions[owner] = ballPosition;
        Dictionary<StringName, TeamPhaseState> phases = new()
        {
            [Home] = new TeamPhaseState(Home, homePhase, 0f, true, 3),
            [Away] = new TeamPhaseState(Away, LiveTeamPhase.TransitionToDefence, 0f, false, 0)
        };
        return new FootballWorldSnapshot(
            positions,
            bases,
            teams,
            roles,
            ballPosition,
            ballPosition,
            owner,
            new StringName(),
            Home,
            Home,
            false,
            false,
            false,
            false,
            false,
            "home_dm",
            phases,
            gameTime);
    }

    private static void AddTeam(
        Dictionary<StringName, Vector2> positions,
        Dictionary<StringName, Vector2> bases,
        Dictionary<StringName, StringName> teams,
        Dictionary<StringName, string> roles,
        StringName teamId,
        string prefix,
        bool mirrored)
    {
        (string Id, string Role, Vector2 Position)[] players =
        {
            ("gk", "GK", new Vector2(0.05f, 0.50f)),
            ("lb", "LB", new Vector2(0.28f, 0.14f)),
            ("cb1", "CB", new Vector2(0.27f, 0.38f)),
            ("cb2", "CB", new Vector2(0.27f, 0.62f)),
            ("rb", "RB", new Vector2(0.28f, 0.86f)),
            ("dm", "DM", new Vector2(0.43f, 0.50f)),
            ("cm", "CM", new Vector2(0.55f, 0.36f)),
            ("am", "AM", new Vector2(0.61f, 0.50f)),
            ("lw", "LW", new Vector2(0.67f, 0.14f)),
            ("st", "ST", new Vector2(0.70f, 0.50f)),
            ("rw", "RW", new Vector2(0.67f, 0.86f))
        };
        foreach ((string id, string role, Vector2 position) in players)
        {
            StringName playerId = $"{prefix}_{id}";
            Vector2 placed = mirrored ? new Vector2(1f - position.X, position.Y) : position;
            positions[playerId] = placed;
            bases[playerId] = placed;
            teams[playerId] = teamId;
            roles[playerId] = role;
        }
    }

    private static int CountRunners(
        IReadOnlyDictionary<StringName, PlayerIntent> plan,
        FootballWorldSnapshot world)
    {
        return plan.Count(pair => world.PlayerTeams[pair.Key] == Home && pair.Value.Assignment is
            OffBallAssignmentKind.RunBehind or OffBallAssignmentKind.RunAcrossDefender or
            OffBallAssignmentKind.AttackBoxNearPost or OffBallAssignmentKind.AttackBoxFarPost or
            OffBallAssignmentKind.AttackBoxCutBackZone);
    }

    private static int CountSupport(
        IReadOnlyDictionary<StringName, PlayerIntent> plan,
        FootballWorldSnapshot world)
    {
        return plan.Count(pair => world.PlayerTeams[pair.Key] == Home && pair.Value.Assignment is
            OffBallAssignmentKind.OfferShortSupport or OffBallAssignmentKind.OfferThirdManSupport);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
