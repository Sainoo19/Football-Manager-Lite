using Godot;

public static class PixelPitchLayout
{
    public const string TexturePath = "res://assets/football/pixel_pitch/pitch_ground_v2.png";

    public static readonly Vector2 ImageSize = new(1024f, 512f);
    private static readonly Vector2 TopLeft = new(100f, 64f);
    private static readonly Vector2 TopRight = new(924f, 64f);
    private static readonly Vector2 BottomLeft = new(64f, 440f);
    private static readonly Vector2 BottomRight = new(960f, 440f);
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
        // The asset generator and actors use this same symmetric projection, including off-pitch positions.
        float imageY = Mathf.Lerp(TopLeft.Y, BottomLeft.Y, normalized.Y);
        float left = Mathf.Lerp(TopLeft.X, BottomLeft.X, normalized.Y);
        float right = Mathf.Lerp(TopRight.X, BottomRight.X, normalized.Y);
        float imageX = Mathf.Lerp(left, right, normalized.X);
        return textureRect.Position + new Vector2(imageX, imageY) / ImageSize * textureRect.Size;
    }
}
