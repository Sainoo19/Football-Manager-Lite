using System;
using System.Collections.Generic;
using Godot;

public static class PlayerPositionInterpolatorTests
{
    public static void Run()
    {
        VerifyInterpolationUsesRenderAlpha();
        VerifyRepeatedCaptureKeepsTheInterpolationWindow();
        VerifyTeleportsAndNewPlayersSnapImmediately();
        GD.Print("PASS: nội suy presentation làm mượt cầu thủ mà không thay đổi vị trí simulation.");
    }

    private static void VerifyInterpolationUsesRenderAlpha()
    {
        StringName playerId = "player";
        PlayerPositionInterpolator interpolator = new();
        interpolator.Reset(Frame(playerId, new Vector2(0.20f, 0.40f)));
        interpolator.Capture(Frame(playerId, new Vector2(0.21f, 0.42f)));

        CheckPosition(
            interpolator.Interpolate(playerId, Vector2.Zero, 0.5f),
            new Vector2(0.205f, 0.41f),
            "Render phải lấy vị trí nằm giữa hai simulation frame theo alpha, không nhảy thẳng tới frame mới.");
        CheckPosition(
            interpolator.Interpolate(playerId, Vector2.Zero, 1f),
            new Vector2(0.21f, 0.42f),
            "Khi alpha đạt 1, presentation phải tới đúng vị trí authoritative hiện tại.");
    }

    private static void VerifyRepeatedCaptureKeepsTheInterpolationWindow()
    {
        StringName playerId = "player";
        PlayerPositionInterpolator interpolator = new();
        interpolator.Reset(Frame(playerId, new Vector2(0.30f, 0.50f)));
        IReadOnlyDictionary<StringName, Vector2> nextFrame = Frame(playerId, new Vector2(0.31f, 0.50f));
        interpolator.Capture(nextFrame);
        bool changed = interpolator.Capture(nextFrame);

        Check(!changed, "Nhiều render frame giữa hai simulation tick không được tạo simulation frame giả.");
        CheckPosition(
            interpolator.Interpolate(playerId, Vector2.Zero, 0.5f),
            new Vector2(0.305f, 0.50f),
            "Render 30, 60 hay 120 FPS phải dùng cùng một cặp simulation frame để nội suy.");
    }

    private static void VerifyTeleportsAndNewPlayersSnapImmediately()
    {
        StringName playerId = "player";
        PlayerPositionInterpolator interpolator = new();
        interpolator.Reset(Frame(playerId, new Vector2(0.10f, 0.50f)));
        interpolator.Capture(Frame(playerId, new Vector2(0.80f, 0.50f)));

        CheckPosition(
            interpolator.Interpolate(playerId, Vector2.Zero, 0f),
            new Vector2(0.80f, 0.50f),
            "Reset, đổi sân hoặc scenario không được làm cầu thủ trượt xuyên cả sân.");

        var frameWithSubstitute = new Dictionary<StringName, Vector2>
        {
            [playerId] = new Vector2(0.80f, 0.50f),
            ["substitute"] = new Vector2(0.25f, 0.25f)
        };
        interpolator.Capture(frameWithSubstitute);
        CheckPosition(
            interpolator.Interpolate("substitute", Vector2.Zero, 0f),
            new Vector2(0.25f, 0.25f),
            "Cầu thủ mới vào sân phải xuất hiện đúng vị trí thay vì nội suy từ Vector2.Zero.");
    }

    private static IReadOnlyDictionary<StringName, Vector2> Frame(StringName playerId, Vector2 position)
    {
        return new Dictionary<StringName, Vector2>
        {
            [playerId] = position
        };
    }

    private static void CheckPosition(Vector2 actual, Vector2 expected, string message)
    {
        Check(actual.DistanceTo(expected) < 0.00001f, message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
