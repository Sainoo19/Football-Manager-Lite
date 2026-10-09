using System;
using System.Collections.Generic;
using Godot;

public static class FootballFundamentalsRegressionTests
{
    public static void Run()
    {
        VerifyShotContactRequiresReachAndChecksTravel();
        VerifyReboundStartsAwayFromTheGoal();
        VerifyLatestTouchReplacesPreviousTeam();
        VerifyTouchlineRestartReadinessAndPlacement();
        VerifyPlayersDoNotCrossThroughEachOther();
        VerifyNearTargetReversalDoesNotTeleport();
        GD.Print("PASS: regression tiếp xúc sút, chạm cuối, restart và tránh chồng người.");
    }

    private static void VerifyShotContactRequiresReachAndChecksTravel()
    {
        ShotContactResolver resolver = new();
        Vector2 start = AtMeters(80f, 34f);
        Vector2 end = AtMeters(85f, 34f);
        Check(resolver.TryContact(start, end, AtMeters(83f, 35f),
                1.5f, out Vector2 contact, out float progress) &&
              Math.Abs(progress - (3f - Mathf.Sqrt(1.25f)) / 5f) < 0.001f &&
              Math.Abs(FootballPitchDimensions.DistanceMeters(contact, AtMeters(83f, 35f)) - 1.5f) < 0.01f,
            "Tiếp xúc phải xét cả đoạn bóng bay giữa hai tick, không bỏ qua bóng đi nhanh.");
        Check(!resolver.TryContact(start, end, AtMeters(81.5f, 40f),
                ShotContactResolver.GoalkeeperReachMeters, out _, out _),
            "Thủ môn ở xa đường bóng không được cản phá.");
        Check(!resolver.TryContact(start, end, AtMeters(81.5f, 35f),
                ShotContactResolver.OutfieldReachMeters, out _, out _),
            "Tầm với của hậu vệ không được bằng tầm với thủ môn.");
    }

    private static void VerifyReboundStartsAwayFromTheGoal()
    {
        ShotContactResolver resolver = new();
        Vector2 rightShotRebound = resolver.ReboundVelocity(new Vector2(28f, 0f), AtMeters(80f, 34f), AtMeters(83f, 34f), true);
        Vector2 leftShotRebound = resolver.ReboundVelocity(new Vector2(-28f, 0f), AtMeters(83f, 34f), AtMeters(80f, 34f), true);
        Check(rightShotRebound.X < 0f && leftShotRebound.X > 0f,
            "Bóng đẩy ra thông thường phải rời cầu môn ở cả hai hướng sân.");
    }

    private static void VerifyLatestTouchReplacesPreviousTeam()
    {
        BallTouchLedger ledger = new();
        ledger.Record("passer", "home");
        ledger.Record("defender", "away");
        Check(ledger.PlayerId == "defender" && ledger.TeamId == "away",
            "Chạm bóng của hậu vệ phải thay người thực hiện đường chuyền trong dữ liệu chạm cuối.");
        ledger.Record(new StringName(), "home");
        Check(ledger.TeamId == "away", "Dữ liệu người chạm không hợp lệ không được xóa chạm cuối.");
        ledger.Reset();
        Check(ledger.TeamId == new StringName(), "Trận mới phải xóa dữ liệu chạm cuối.");
    }

    private static void VerifyTouchlineRestartReadinessAndPlacement()
    {
        TouchlineRestartPlanner planner = new();
        Vector2 corner = planner.PlaceCorner(new Vector2(0.99f, 0.4f));
        Check(corner.X > 0.95f && corner.Y < 0.05f,
            "Bóng ra biên ngang phải được đặt ở góc sân thay vì ở giữa biên ngang.");
        Check(!planner.IsTakerReady(AtMeters(90f, 34f), corner) && planner.IsTakerReady(corner, corner),
            "Không thể thực hiện restart khi người thực hiện còn xa bóng.");
        Vector2 legalDefender = planner.KeepDefenderAway(corner, corner, true);
        Check(FootballPitchDimensions.DistanceMeters(legalDefender, corner) >= 9.15f,
            "Đối phương phải có điểm đứng ngoài khoảng cách phạt góc.");
        FreeKickRestartPlanner freeKickPlanner = new();
        Vector2 freeKickPosition = new(0.975f, 0.94f);
        Vector2 defenderAtBoundary = new(0.975f, 0.965f);
        Vector2 legalFreeKickDefender = freeKickPlanner.EnsureRequiredDefenderDistance(
            defenderAtBoundary, freeKickPosition, false);
        Check(FootballPitchDimensions.DistanceMeters(legalFreeKickDefender, freeKickPosition) >= 9.15f,
            "Giới hạn sân không được đẩy hậu vệ trở lại vùng cấm đá phạt và làm restart chờ vô hạn.");
        Check(float.IsNegativeInfinity(planner.ThrowReceptionScore(corner, AtMeters(40f, 34f), 8f)) &&
              float.IsFinite(planner.ThrowReceptionScore(corner, corner + new Vector2(-0.08f, 0.08f), 8f)),
            "Ném biên phải chọn người trong tầm ném, không đưa bóng xuyên nửa sân.");
    }

    private static void VerifyPlayersDoNotCrossThroughEachOther()
    {
        Dictionary<StringName, Vector2> positions = new()
        {
            ["first"] = AtMeters(49.5f, 34f),
            ["second"] = AtMeters(50.5f, 34f)
        };
        Dictionary<StringName, Vector2> previous = new()
        {
            ["first"] = positions["first"],
            ["second"] = positions["second"]
        };
        Dictionary<StringName, Vector2> velocities = new()
        {
            ["first"] = new Vector2(8f, 0f),
            ["second"] = new Vector2(-8f, 0f)
        };
        positions["first"] = AtMeters(50.7f, 34f);
        positions["second"] = AtMeters(49.3f, 34f);
        new PlayerCollisionResolver().Resolve(positions, previous, velocities, new StringName[] { "first", "second" });
        Check(positions["first"].X < positions["second"].X &&
              FootballPitchDimensions.DistanceMeters(positions["first"], positions["second"]) >= 0.69f,
            "Hai cầu thủ không được đổi chỗ xuyên qua nhau trong một bước mô phỏng.");
    }

    private static void VerifyNearTargetReversalDoesNotTeleport()
    {
        FootballMovementController movement = new();
        Dictionary<StringName, Vector2> positions = new() { ["runner"] = AtMeters(50f, 34f) };
        Dictionary<StringName, Vector2> targets = new() { ["runner"] = AtMeters(80f, 34f) };
        Dictionary<StringName, PlayerIntent> intents = new()
        {
            ["runner"] = new PlayerIntent(PlayerIntentKind.RunIntoSpace, targets["runner"], LiveTeamPhase.InPossession)
        };
        Dictionary<StringName, int> paces = new() { ["runner"] = 90 };
        for (int step = 0; step < 15; step++)
        {
            movement.Advance(positions, targets, intents, paces, 0.1f);
        }
        float velocityBefore = movement.VelocitiesMetersPerSecond["runner"].X;
        Vector2 previous = positions["runner"];
        targets["runner"] = previous - new Vector2(0.2f / FootballPitchDimensions.LengthMeters, 0f);
        movement.Advance(positions, targets, intents, paces, 0.1f);
        Check(positions["runner"].X > previous.X &&
              movement.VelocitiesMetersPerSecond["runner"].X >= velocityBefore - 0.43f,
            "Đổi đích về phía sau phải hãm theo gia tốc, không teleport tới đích vì độ dài bước lớn hơn cự ly.");
    }

    private static Vector2 AtMeters(float x, float y) => FootballPitchDimensions.ToNormalized(new Vector2(x, y));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
