using Godot;

public static class PixelGoalLayout
{
    public const string AssetDirectory = "res://assets/football/pixel_pitch/goals";
    public const float DepthMeters = 2.5f;
    public const float HeightMeters = 2.44f;
    public const float HeightPixels = 28f;
    public const int TextureSize = 128;
    public static readonly Vector2 Pivot = new(64f, 96f);
    public static float HalfWidth => FootballPitchDimensions.GoalWidthMeters / FootballPitchDimensions.WidthMeters / 2f;

    public static Vector2 SpritePoint(float x, float y, bool raised)
    {
        Rect2 nativeRect = new(Vector2.Zero, PixelPitchLayout.ImageSize);
        Vector2 center = PixelPitchLayout.ToScreenPoint(new Vector2(0f, 0.5f), nativeRect);
        Vector2 point = PixelPitchLayout.ToScreenPoint(new Vector2(x, y), nativeRect);
        return Pivot + point - center - (raised ? new Vector2(0f, HeightPixels) : Vector2.Zero);
    }

    public static Vector2 BallAirPoint(Vector2 groundPoint, float heightMeters, Rect2 textureRect)
    {
        float height = heightMeters * HeightPixels / HeightMeters * textureRect.Size.X / PixelPitchLayout.ImageSize.X;
        return groundPoint - new Vector2(height * 0.1f, height);
    }
}
