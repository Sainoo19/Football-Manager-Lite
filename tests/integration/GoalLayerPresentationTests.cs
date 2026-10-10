using System;
using Godot;

public static class GoalLayerPresentationTests
{
    public static void Run()
    {
        foreach (string name in new[] { "rear", "net", "front" })
        {
            using Image image = GD.Load<Texture2D>($"{PixelGoalLayout.AssetDirectory}/goal_{name}.png").GetImage();
            if (image.IsCompressed()) image.Decompress();
            Check(image.GetSize() == new Vector2(128f, 128f) && image.GetPixel(0, 0).A == 0f,
                "Các lớp gôn phải có canvas chuẩn và nền trong suốt.");
            int visible = 0;
            int transparent = 0;
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    if (image.GetPixel(x, y).A > 0f) visible++;
                    else transparent++;
                }
            }
            Check(visible > 50 && transparent > 14000, "Lớp lưới phải giữ ô trong suốt để thấy cầu thủ bên trong.");
        }
        MatchGoalRenderer goals = new();
        goals.Initialize();
        foreach (float scale in new[] { 0.4f, 1f, 2f })
        {
            Rect2 rect = new(new Vector2(10f, 20f), PixelPitchLayout.ImageSize * scale);
            goals.UpdateLayout(rect);
            Vector2 ballGround = PixelPitchLayout.ToScreenPoint(new Vector2(0f, 0.5f), rect);
            Vector2 atBar = PixelGoalLayout.BallAirPoint(ballGround, PixelGoalLayout.HeightMeters, rect);
            Check(Mathf.Abs(ballGround.Y - atBar.Y - PixelGoalLayout.HeightPixels * scale) < 0.001f,
                "Độ cao render của bóng phải dùng cùng tỷ lệ đứng với xà ngang.");
            for (int side = 0; side < 2; side++)
            {
                Sprite2D rear = goals.GetNode<Sprite2D>($"Goal_{side}_rear");
                Sprite2D net = goals.GetNode<Sprite2D>($"Goal_{side}_net");
                Sprite2D front = goals.GetNode<Sprite2D>($"Goal_{side}_front");
                float direction = side == 0 ? -1f : 1f;
                Vector2 inside = new(side + direction / FootballPitchDimensions.LengthMeters, 0.5f);
                Vector2 field = new(side - direction / FootballPitchDimensions.LengthMeters, 0.5f);
                Vector2 behind = new(side + direction * 4f / FootballPitchDimensions.LengthMeters, 0.5f);
                int insideDepth = goals.ActorDepth(inside, PixelPitchLayout.ToScreenPoint(inside, rect));
                Check(rear.ZIndex < insideDepth && insideDepth < net.ZIndex && net.ZIndex < front.ZIndex,
                    "Cầu thủ/bóng trong gôn phải sau lưới và cột trước, trước khung sau.");
                Check(goals.ActorDepth(field, PixelPitchLayout.ToScreenPoint(field, rect)) > front.ZIndex,
                    "Cầu thủ trước miệng gôn phải hiển thị trước khung.");
                Check(goals.ActorDepth(behind, PixelPitchLayout.ToScreenPoint(behind, rect)) < rear.ZIndex,
                    "Cầu thủ phía sau gôn phải bị cả khung và lưới che.");
                Check(goals.ActorDepth(inside, PixelPitchLayout.ToScreenPoint(inside, rect), 1.5f, true) < net.ZIndex &&
                    goals.ActorDepth(inside, PixelPitchLayout.ToScreenPoint(inside, rect), 2.6f, true) > front.ZIndex,
                    "Bóng dưới xà phải vào sau lưới; bóng qua xà phải ở trên gôn.");
                Check(front.FlipH == (side == 1), "Gôn phải phải mirror cùng asset với gôn trái.");
            }
        }
        Check(goals.YSortEnabled, "Nhóm gôn phải tham gia sắp xếp theo Y trong nhóm actor chung.");
        goals.Free();
        GD.Print("PASS: gôn tách lớp, che khuất hai phía, bóng dưới/trên xà và resize đúng.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
