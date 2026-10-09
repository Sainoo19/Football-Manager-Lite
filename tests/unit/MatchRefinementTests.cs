using System;
using System.Collections.Generic;
using Godot;

public static class MatchRefinementTests
{
    public static void Run()
    {
        VerifyTouchlineBallCanBeReached();
        VerifyKeeperCoversTheGoalAngle();
        VerifyReboundsRetainIncidentEnergyAndContactAngle();
        Check(GoalkeeperResponseRules.ReactionDelaySeconds(90) < GoalkeeperResponseRules.ReactionDelaySeconds(30),
            "Thủ môn tốt phải phản ứng sớm hơn, nhưng vẫn có thời gian phản ứng.");
        Check(GoalkeeperResponseRules.CanHoldContact(0.4f, 28f, 80) &&
              !GoalkeeperResponseRules.CanHoldContact(2.2f, 28f, 80),
            "Cú sút sát thân có thể bắt; pha với tay ở mép tầm với phải đẩy ra.");
        GD.Print("PASS: ranh giới vật lý, góc đứng thủ môn, phản ứng và động lượng bóng bật ra.");
    }

    private static void VerifyTouchlineBallCanBeReached()
    {
        FootballMovementController movement = new();
        Vector2 ball = AtMeters(50f, 0.1f);
        Dictionary<StringName, Vector2> positions = new() { ["runner"] = AtMeters(50f, 3f) };
        Dictionary<StringName, Vector2> targets = new() { ["runner"] = ball };
        Dictionary<StringName, PlayerIntent> intents = new()
        {
            ["runner"] = new PlayerIntent(PlayerIntentKind.ChaseLooseBall, ball, LiveTeamPhase.LooseBall)
        };
        Dictionary<StringName, int> pace = new() { ["runner"] = 70 };
        for (int step = 0; step < 100; step++)
        {
            movement.Advance(positions, targets, intents, pace, 0.1f);
        }
        Check(FootballPitchDimensions.DistanceMeters(positions["runner"], ball) < 1.4f &&
              positions["runner"].Y >= PlayerPitchBoundary.BodyRadiusMeters / FootballPitchDimensions.WidthMeters - 0.001f,
            "Bóng dừng trong sân sát biên phải nằm trong tầm kiểm soát của cầu thủ.");
    }

    private static void VerifyKeeperCoversTheGoalAngle()
    {
        FootballWorldSnapshot world = new FootballWorldSnapshotBuilder()
            .AddPlayer("keeper", "away", "GK", new Vector2(0.045f, 0.5f))
            .AddPlayer("winger", "home", "LW", new Vector2(0.25f, 0.08f))
            .WithPossession("home", "winger", new Vector2(0.25f, 0.08f)).Build();
        Vector2 target = new TraditionalGoalkeeperPlanner().PositionTarget(world, "keeper", "away");
        Check(target.Y < 0.5f && Mathf.Abs(target.Y - 0.5f) * FootballPitchDimensions.WidthMeters <= 4.46f,
            "Thủ môn phải khép góc ở cầu môn, không chạy ngang theo cầu thủ sát biên.");
    }

    private static void VerifyReboundsRetainIncidentEnergyAndContactAngle()
    {
        ShotContactResolver resolver = new();
        Vector2 player = AtMeters(103f, 34f);
        Vector2 headOn = resolver.ReboundVelocity(new Vector2(28f, 0f), AtMeters(100.4f, 34f), player, true);
        Vector2 slower = resolver.ReboundVelocity(new Vector2(14f, 0f), AtMeters(100.4f, 34f), player, true);
        Vector2 glancing = resolver.ReboundVelocity(new Vector2(28f, 0f), AtMeters(102.3f, 31.5f), player, true);
        Check(headOn.X < 0f && headOn.Length() > slower.Length() && headOn.Length() < 28f,
            "Bóng bật chính diện phải đổi hướng, giảm năng lượng và phụ thuộc tốc độ cú sút.");
        Check(glancing.X > 0f && glancing.Y < 0f,
            "Chạm lệch phải đổi góc bóng, có thể tiếp tục hướng về biên ngang.");
        Vector2 position = AtMeters(102.3f, 31.5f);
        Vector2 velocity = glancing;
        for (int step = 0; step < 20; step++)
        {
            RollingBallStep result = new RollingBallPhysics().Advance(position, velocity, 0.1f);
            position = result.Position;
            velocity = result.VelocityMetersPerSecond;
        }
        Check(position.X > 1f, "Bóng chạm lệch có đủ động lượng phải thật sự lăn qua biên ngang.");
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
