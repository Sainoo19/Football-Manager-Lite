using Godot;

public static class PixelPitchLayout
{
    public const string TexturePath = "res://assets/football/pixel_pitch/pitch_2_5d_v1.png";

    private static readonly Vector2 ImageSize = new(1774f, 887f);
    private static readonly Vector2 TopLeft = new(170f, 130f);
    private static readonly Vector2 TopRight = new(1602f, 130f);
    private static readonly Vector2 BottomLeft = new(105f, 720f);
    private static readonly Vector2 BottomRight = new(1670f, 720f);
    private static readonly Vector2 CenterSpot = new(886f, 405f);
    private const float Margin = 8f;

    public static Rect2 CalculateTextureRect(Vector2 controlSize)
    {
        Vector2 available = new(
            Mathf.Max(controlSize.X - Margin * 2f, 1f),
            Mathf.Max(controlSize.Y - Margin * 2f, 1f));
        float scale = Mathf.Min(available.X / ImageSize.X, available.Y / ImageSize.Y);
        Vector2 size = ImageSize * scale;
        return new Rect2((controlSize - size) * 0.5f, size);
    }

    public static Rect2 CalculateFieldRect(Rect2 textureRect)
    {
        Vector2 position = new(BottomLeft.X, TopLeft.Y);
        Vector2 size = new(BottomRight.X - BottomLeft.X, BottomLeft.Y - TopLeft.Y);
        return new Rect2(
            textureRect.Position + position / ImageSize * textureRect.Size,
            size / ImageSize * textureRect.Size);
    }

    public static Vector2 ToScreenPoint(Vector2 normalized, Rect2 textureRect)
    {
        // Calibrate to the painted boundaries and center spot rather than the PNG's outer fence.
        // The two halves account for the generated artwork's slightly asymmetric foreshortening.
        float imageY = normalized.Y <= 0.5f
            ? Mathf.Lerp(TopLeft.Y, CenterSpot.Y, normalized.Y * 2f)
            : Mathf.Lerp(CenterSpot.Y, BottomLeft.Y, (normalized.Y - 0.5f) * 2f);
        float depth = (imageY - TopLeft.Y) / (BottomLeft.Y - TopLeft.Y);
        float left = Mathf.Lerp(TopLeft.X, BottomLeft.X, depth);
        float right = Mathf.Lerp(TopRight.X, BottomRight.X, depth);
        float imageX = normalized.X <= 0.5f
            ? Mathf.Lerp(left, CenterSpot.X, normalized.X * 2f)
            : Mathf.Lerp(CenterSpot.X, right, (normalized.X - 0.5f) * 2f);
        return textureRect.Position + new Vector2(imageX, imageY) / ImageSize * textureRect.Size;
    }
}
