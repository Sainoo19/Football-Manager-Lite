using Godot;

public static class PixelArtRaster
{
    public static void Line(Image image, Vector2 start, Vector2 end, Color color, int width = 1)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y)));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = start.Lerp(end, steps == 0 ? 0f : i / (float)steps);
            Dot(image, point, color, width);
        }
    }

    public static void Dot(Image image, Vector2 point, Color color, int width)
    {
        int offset = width / 2;
        int centerX = Mathf.RoundToInt(point.X);
        int centerY = Mathf.RoundToInt(point.Y);
        for (int y = centerY - offset; y <= centerY + offset; y++)
        {
            for (int x = centerX - offset; x <= centerX + offset; x++)
            {
                if (x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight())
                {
                    image.SetPixel(x, y, color);
                }
            }
        }
    }
}
