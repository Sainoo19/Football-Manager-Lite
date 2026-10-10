using System;
using Godot;

public partial class PitchAssetGenerator : Node
{
    private static readonly Rect2 NativeRect = new(Vector2.Zero, PixelPitchLayout.ImageSize);
    private static readonly Color Paint = new("eff5c7");

    public override void _Ready()
    {
        try
        {
            using Image ground = GenerateGround();
            Save(ground, PixelPitchLayout.TexturePath);
            GenerateGoals();
            GD.Print("PASS: deterministic ground and transparent goal layers generated.");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static Image GenerateGround()
    {
        Image image = Image.CreateEmpty(1024, 512, false, Image.Format.Rgba8);
        for (int y = 28; y <= 477; y++)
        {
            float depth = (y - 64f) / 376f;
            float fieldLeft = Mathf.Lerp(100f, 64f, depth);
            float halfWidth = 512f - fieldLeft;
            float apronHalfWidth = Mathf.Lerp(456f, 492f, (y - 28f) / 448f);
            for (int x = Mathf.CeilToInt(512f - apronHalfWidth); x <= 512f + apronHalfWidth && x < 1024; x++)
            {
                float normalizedX = (x - fieldLeft) / (halfWidth * 2f);
                bool onField = depth >= 0f && depth <= 1f && normalizedX >= 0f && normalizedX <= 1f;
                int stripe = Mathf.Clamp(Mathf.FloorToInt(normalizedX * 13f), 0, 12);
                stripe = Mathf.Min(stripe, 12 - stripe);
                Color grass = onField ? new Color(stripe % 2 == 0 ? "52914d" : "5b9b52") : new Color("3b7546");
                int hash = (Math.Abs(x - 512) * 73856093 ^ y * 19349663) & 255;
                float shade = hash < 13 ? 0.92f : hash > 243 ? 1.055f : 1f;
                image.SetPixel(x, y, new Color(grass.R * shade, grass.G * shade, grass.B * shade));
            }
        }
        PaintBox(image, 0f, 0f, 1f, 1f);
        PaintLine(image, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        PaintCircle(image, new Vector2(0.5f, 0.5f), 0f, Mathf.Tau);
        PixelArtRaster.Dot(image, Project(new Vector2(0.5f, 0.5f)), Paint, 3);
        PaintEnd(image, false);
        PaintEnd(image, true);
        PaintCorners(image);
        // Mirror integer pixels as well as geometry to avoid floating-point rounding at arc edges.
        for (int y = 0; y < 512; y++)
        {
            for (int x = 1; x < 512; x++)
            {
                image.SetPixel(1024 - x, y, image.GetPixel(x, y));
            }
        }
        return image;
    }

    private static void PaintEnd(Image image, bool right)
    {
        float penaltyDepth = FootballPitchDimensions.PenaltyAreaDepthMeters / FootballPitchDimensions.LengthMeters;
        float penaltyHalf = FootballPitchDimensions.PenaltyAreaWidthMeters / FootballPitchDimensions.WidthMeters / 2f;
        float areaDepth = FootballPitchDimensions.GoalAreaDepthMeters / FootballPitchDimensions.LengthMeters;
        float areaHalf = FootballPitchDimensions.GoalAreaWidthMeters / FootballPitchDimensions.WidthMeters / 2f;
        PaintBox(image, right ? 1f - penaltyDepth : 0f, 0.5f - penaltyHalf,
            right ? 1f : penaltyDepth, 0.5f + penaltyHalf);
        PaintBox(image, right ? 1f - areaDepth : 0f, 0.5f - areaHalf,
            right ? 1f : areaDepth, 0.5f + areaHalf);
        float spotX = FootballPitchDimensions.PenaltySpotDistanceMeters / FootballPitchDimensions.LengthMeters;
        Vector2 spot = new(right ? 1f - spotX : spotX, 0.5f);
        PixelArtRaster.Dot(image, Project(spot), Paint, 3);
        float angle = Mathf.Acos((FootballPitchDimensions.PenaltyAreaDepthMeters
            - FootballPitchDimensions.PenaltySpotDistanceMeters) / FootballPitchDimensions.CenterCircleRadiusMeters);
        PaintCircle(image, spot, right ? Mathf.Pi - angle : -angle, right ? Mathf.Pi + angle : angle);
    }

    private static void PaintCorners(Image image)
    {
        for (int side = 0; side < 2; side++)
        {
            for (int end = 0; end < 2; end++)
            {
                float start = side == 0 ? (end == 0 ? 0f : -Mathf.Pi / 2f)
                    : (end == 0 ? Mathf.Pi / 2f : Mathf.Pi);
                PaintCircle(image, new Vector2(side, end), start, start + Mathf.Pi / 2f, 1f);
            }
        }
    }

    private static void PaintCircle(Image image, Vector2 center, float start, float end,
        float radius = FootballPitchDimensions.CenterCircleRadiusMeters)
    {
        Vector2 previous = Vector2.Zero;
        for (int i = 0; i <= 128; i++)
        {
            float angle = Mathf.Lerp(start, end, i / 128f);
            Vector2 position = center + new Vector2(Mathf.Cos(angle) * radius / FootballPitchDimensions.LengthMeters,
                Mathf.Sin(angle) * radius / FootballPitchDimensions.WidthMeters);
            if (i > 0)
            {
                PaintLine(image, previous, position);
            }
            previous = position;
        }
    }

    private static void PaintBox(Image image, float left, float top, float right, float bottom)
    {
        PaintLine(image, new Vector2(left, top), new Vector2(right, top));
        PaintLine(image, new Vector2(right, top), new Vector2(right, bottom));
        PaintLine(image, new Vector2(right, bottom), new Vector2(left, bottom));
        PaintLine(image, new Vector2(left, bottom), new Vector2(left, top));
    }

    private static void PaintLine(Image image, Vector2 start, Vector2 end) =>
        PixelArtRaster.Line(image, Project(start), Project(end), Paint, 3);

    private static Vector2 Project(Vector2 normalized) => PixelPitchLayout.ToScreenPoint(normalized, NativeRect);

    private static void GenerateGoals()
    {
        using Image rear = GoalImage();
        using Image net = GoalImage();
        using Image front = GoalImage();
        float far = 0.5f - PixelGoalLayout.HalfWidth;
        float near = 0.5f + PixelGoalLayout.HalfWidth;
        float back = -PixelGoalLayout.DepthMeters / FootballPitchDimensions.LengthMeters;
        Vector2 farFoot = PixelGoalLayout.SpritePoint(0f, far, false);
        Vector2 nearFoot = PixelGoalLayout.SpritePoint(0f, near, false);
        Vector2 farTop = PixelGoalLayout.SpritePoint(0f, far, true);
        Vector2 nearTop = PixelGoalLayout.SpritePoint(0f, near, true);
        Vector2 backFarFoot = PixelGoalLayout.SpritePoint(back, far, false);
        Vector2 backNearFoot = PixelGoalLayout.SpritePoint(back, near, false);
        Vector2 backFarTop = PixelGoalLayout.SpritePoint(back, far, true);
        Vector2 backNearTop = PixelGoalLayout.SpritePoint(back, near, true);
        FrameLine(rear, backFarFoot, backFarTop);
        FrameLine(rear, backFarTop, backNearTop);
        FrameLine(rear, backNearTop, backNearFoot);
        FrameLine(rear, backFarTop, farTop);
        FrameLine(rear, backNearFoot, nearFoot);
        NetPanel(net, backFarTop, backNearTop, backFarFoot, backNearFoot, 9, 7);
        NetPanel(net, backFarTop, backNearTop, farTop, nearTop, 9, 5);
        NetPanel(net, backNearTop, nearTop, backNearFoot, nearFoot, 5, 7);
        FrameLine(front, farFoot, farTop);
        FrameLine(front, farTop, nearTop);
        FrameLine(front, nearTop, nearFoot);
        Save(rear, $"{PixelGoalLayout.AssetDirectory}/goal_rear.png");
        Save(net, $"{PixelGoalLayout.AssetDirectory}/goal_net.png");
        Save(front, $"{PixelGoalLayout.AssetDirectory}/goal_front.png");
    }

    private static Image GoalImage() =>
        Image.CreateEmpty(PixelGoalLayout.TextureSize, PixelGoalLayout.TextureSize, false, Image.Format.Rgba8);

    private static void FrameLine(Image image, Vector2 start, Vector2 end)
    {
        PixelArtRaster.Line(image, start + Vector2.One, end + Vector2.One, new Color("385552"), 3);
        PixelArtRaster.Line(image, start, end, new Color("f4f1d8"), 3);
        PixelArtRaster.Line(image, start - new Vector2(0f, 1f), end - new Vector2(0f, 1f), Colors.White);
    }

    private static void NetPanel(Image image, Vector2 topLeft, Vector2 topRight,
        Vector2 bottomLeft, Vector2 bottomRight, int columns, int rows)
    {
        Color thread = new(0.83f, 0.9f, 0.82f, 0.57f);
        for (int column = 0; column <= columns; column++)
        {
            float t = column / (float)columns;
            PixelArtRaster.Line(image, topLeft.Lerp(topRight, t), bottomLeft.Lerp(bottomRight, t), thread);
        }
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            PixelArtRaster.Line(image, topLeft.Lerp(bottomLeft, t), topRight.Lerp(bottomRight, t), thread);
        }
    }

    private static void Save(Image image, string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(absolutePath.GetBaseDir());
        Error saveError = directoryError == Error.Ok ? image.SavePng(absolutePath) : directoryError;
        if (saveError != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot save {path}: {saveError}");
        }
    }
}
