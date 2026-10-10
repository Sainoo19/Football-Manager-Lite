using System;
using Godot;

public static class PixelPitchPresentationTests
{
    public static void Run()
    {
        VerifyAssetLoads();
        VerifyProjectionMatchesPaintedLandmarks();
        VerifyLayoutFitsDifferentPanelSizes();
        VerifyDisplayCanSwitchWithoutChangingMatchState();
        GD.Print("PASS: sân pixel tải được, tọa độ bám mặt sân và đổi giao diện không đổi simulation.");
    }

    private static void VerifyAssetLoads()
    {
        Texture2D texture = GD.Load<Texture2D>(PixelPitchLayout.TexturePath);
        Check(texture is not null && texture.GetSize() == new Vector2(1774f, 887f),
            "Sân pixel phải được import và tải đúng kích thước.");
    }

    private static void VerifyProjectionMatchesPaintedLandmarks()
    {
        Rect2 textureRect = new(Vector2.Zero, new Vector2(1774f, 887f));
        CheckPoint(Vector2.Zero, new Vector2(170f, 130f), textureRect);
        CheckPoint(new Vector2(1f, 0f), new Vector2(1602f, 130f), textureRect);
        CheckPoint(new Vector2(0f, 1f), new Vector2(105f, 720f), textureRect);
        CheckPoint(Vector2.One, new Vector2(1670f, 720f), textureRect);
        CheckPoint(new Vector2(0.5f, 0.5f), new Vector2(886f, 405f), textureRect);
        CheckPoint(new Vector2(0.5f, 0f), new Vector2(886f, 130f), textureRect);
        CheckPoint(new Vector2(0.5f, 1f), new Vector2(886f, 720f), textureRect);

        Vector2 outside = PixelPitchLayout.ToScreenPoint(new Vector2(-0.01f, 0.5f), textureRect);
        Vector2 boundary = PixelPitchLayout.ToScreenPoint(new Vector2(0f, 0.5f), textureRect);
        Check(outside.X < boundary.X, "Bóng ra ngoài biên phải tiếp tục hiển thị ngoài đường biên.");
    }

    private static void VerifyLayoutFitsDifferentPanelSizes()
    {
        Vector2[] panelSizes = { new(1900f, 500f), new(600f, 360f), new(500f, 900f) };
        foreach (Vector2 panelSize in panelSizes)
        {
            Rect2 textureRect = PixelPitchLayout.CalculateTextureRect(panelSize);
            Check(Math.Abs(textureRect.Size.X / textureRect.Size.Y - 2f) < 0.001f,
                "Sân pixel phải giữ tỷ lệ ảnh khi panel đổi kích thước.");
            Check(textureRect.Position.X >= 0f && textureRect.Position.Y >= 0f &&
                textureRect.End.X <= panelSize.X && textureRect.End.Y <= panelSize.Y,
                "Toàn bộ ảnh sân và hàng rào phải nằm trong panel.");
            Vector2 center = PixelPitchLayout.ToScreenPoint(new Vector2(0.5f, 0.5f), textureRect);
            Rect2 field = PixelPitchLayout.CalculateFieldRect(textureRect);
            Check(field.HasPoint(center), "Tâm sân phải nằm trong vùng mặt cỏ ở mọi kích thước.");
        }
    }

    private static void VerifyDisplayCanSwitchWithoutChangingMatchState()
    {
        MatchPitch2D pitch = new();
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 20261010);
        pitch.SetMatch(simulation);
        Check(pitch.IsPixelPitchEnabled, "Sân pixel phải được bật mặc định.");
        Vector2 ballPosition = pitch.BallPosition;
        int playerCount = pitch.CurrentPositions.Count;
        pitch.SetPixelPitchEnabled(false);
        Check(!pitch.IsPixelPitchEnabled && pitch.BallPosition == ballPosition &&
            pitch.Simulation == simulation && pitch.CurrentPositions.Count == playerCount,
            "Đổi sân chỉ thay presentation.");
        pitch.SetPixelPitchEnabled(true);
        Check(pitch.IsPixelPitchEnabled, "Có thể bật lại sân pixel.");
        pitch.Free();
        simulation.Dispose();
    }

    private static void CheckPoint(Vector2 normalized, Vector2 expected, Rect2 textureRect)
    {
        Check(PixelPitchLayout.ToScreenPoint(normalized, textureRect).DistanceTo(expected) < 0.001f,
            "Tọa độ simulation phải trùng mốc trên ảnh sân.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
