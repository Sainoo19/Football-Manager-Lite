using System;
using Godot;

// Offline normalisation of the generated concept boards into the fixed-size sheets the match view loads.
// The game never runs this: MatchSpriteAtlas only loads the exported files.
public static class PlayerSpriteSheetBuilder
{
    public const string PlayerSourcePath =
        "res://assets/football/pixel_sprites/modular_preview/neutral_eight_directions_v1.png";
    public const string BallSourcePath = "res://assets/football/pixel_sprites/sprite_preview_v1.png";
    private const int SourceColumns = 4;
    private const int SourceRows = 2;
    private const int MaximumBodyWidth = 44;
    private const byte OpaqueAlphaThreshold = 160;

    // Shirt, shorts and socks end at these fractions of the body height, per facing.
    private static readonly Vector3[] KitBoundaries =
    {
        new(0.62f, 0.77f, 0.88f), new(0.61f, 0.74f, 0.85f),
        new(0.67f, 0.78f, 0.88f), new(0.63f, 0.76f, 0.87f),
        new(0.65f, 0.78f, 0.88f), new(0.65f, 0.76f, 0.87f),
        new(0.66f, 0.78f, 0.89f), new(0.62f, 0.76f, 0.86f)
    };

    public sealed class Sheets : IDisposable
    {
        public Sheets(Image players, Image kitMask, Image ball, Image ballShadow)
        {
            Players = players;
            KitMask = kitMask;
            Ball = ball;
            BallShadow = ballShadow;
        }

        public Image Players { get; }
        public Image KitMask { get; }
        public Image Ball { get; }
        public Image BallShadow { get; }

        public void Dispose()
        {
            Players.Dispose();
            KitMask.Dispose();
            Ball.Dispose();
            BallShadow.Dispose();
        }
    }

    public static Sheets Build()
    {
        int frameWidth = MatchSpriteAtlas.FrameWidth;
        int frameHeight = MatchSpriteAtlas.FrameHeight;
        Image players = CreateCanvas(
            frameWidth * MatchSpriteAtlas.SheetColumns, frameHeight * MatchSpriteAtlas.SheetRows);
        Image kitMask = CreateCanvas(players.GetWidth(), players.GetHeight());
        using (Image source = LoadSource(PlayerSourcePath))
        {
            for (int facing = 0; facing < MatchSpriteAtlas.FacingCount; facing++)
            {
                using Image sprite = FitPlayer(
                    CropCell(source, facing % SourceColumns, facing / SourceColumns));
                using Image mask = CreateMask(sprite, facing);
                Rect2I frame = new(0, 0, frameWidth, frameHeight);
                Vector2I cell = new(
                    facing % MatchSpriteAtlas.SheetColumns * frameWidth,
                    facing / MatchSpriteAtlas.SheetColumns * frameHeight);
                players.BlitRect(sprite, frame, cell);
                kitMask.BlitRect(mask, frame, cell);
            }
        }

        int ballSize = MatchSpriteAtlas.BallSize;
        Image ball = CreateCanvas(ballSize * MatchSpriteAtlas.BallFrameCount, ballSize);
        using (Image source = LoadSource(BallSourcePath))
        {
            for (int frame = 0; frame < MatchSpriteAtlas.BallFrameCount; frame++)
            {
                using Image cell = CropCell(source, frame + 2, 1);
                cell.Resize(ballSize, ballSize, Image.Interpolation.Nearest);
                ball.BlitRect(cell, new Rect2I(0, 0, ballSize, ballSize), new Vector2I(frame * ballSize, 0));
            }
        }
        Image ballShadow = CreateBallShadow();
        return new Sheets(players, kitMask, ball, ballShadow);
    }

    public static Image CreateBallShadow()
    {
        int width = MatchSpriteAtlas.BallShadowWidth;
        int height = MatchSpriteAtlas.BallShadowHeight;
        Image shadow = CreateCanvas(width, height);
        float cx = 7.5f;
        float cy = 3.5f;
        float rx = 6.5f;
        float ry = 2.8f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d2 = dx * dx + dy * dy;
                if (d2 <= 0.45f)
                {
                    shadow.SetPixel(x, y, new Color(0f, 0f, 0f, 0.42f));
                }
                else if (d2 <= 0.85f)
                {
                    shadow.SetPixel(x, y, new Color(0f, 0f, 0f, 0.28f));
                }
                else if (d2 <= 1.05f)
                {
                    shadow.SetPixel(x, y, new Color(0f, 0f, 0f, 0.14f));
                }
            }
        }
        return shadow;
    }

    // Fully transparent pixels carry no visible colour, so only their alpha is compared.
    public static bool HasSamePixels(Image expected, Image actual)
    {
        using (actual)
        {
            if (actual.IsEmpty() || actual.GetSize() != expected.GetSize())
            {
                return false;
            }
            actual.Convert(Image.Format.Rgba8);
            byte[] first = expected.GetData();
            byte[] second = actual.GetData();
            for (int index = 0; index < first.Length; index += 4)
            {
                if (first[index + 3] != second[index + 3])
                {
                    return false;
                }
                if (first[index + 3] != 0 &&
                    (first[index] != second[index] || first[index + 1] != second[index + 1] ||
                     first[index + 2] != second[index + 2]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    private static Image CreateCanvas(int width, int height)
    {
        Image canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        // Transparent black. Colors.Transparent is transparent white, and the kit shader reads mask RGB without
        // alpha, so a white background would recolour hair, skin and boots as if they were kit.
        canvas.Fill(new Color(0f, 0f, 0f, 0f));
        return canvas;
    }

    // Reads the PNG itself so the export does not depend on the import cache, then applies the same alpha-edge
    // fix Godot's importer applied when the game still built these sprites from the imported board.
    private static Image LoadSource(string path)
    {
        Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image is null || image.IsEmpty())
        {
            throw new InvalidOperationException($"Cannot read sprite source: {path}");
        }
        image.Convert(Image.Format.Rgba8);
        image.FixAlphaEdges();
        return image;
    }

    private static Image CropCell(Image sheet, int column, int row)
    {
        int left = sheet.GetWidth() * column / SourceColumns;
        int top = sheet.GetHeight() * row / SourceRows;
        int right = sheet.GetWidth() * (column + 1) / SourceColumns;
        int bottom = sheet.GetHeight() * (row + 1) / SourceRows;
        using Image cell = sheet.GetRegion(new Rect2I(left, top, right - left, bottom - top));
        // Ignore almost-transparent generated edge noise when aligning feet and sprite scale.
        byte[] pixels = cell.GetData();
        int minX = cell.GetWidth();
        int minY = cell.GetHeight();
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < cell.GetHeight(); y++)
        {
            for (int x = 0; x < cell.GetWidth(); x++)
            {
                if (pixels[(y * cell.GetWidth() + x) * 4 + 3] < OpaqueAlphaThreshold)
                {
                    continue;
                }
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
        if (maxX < minX || maxY < minY)
        {
            throw new InvalidOperationException("A sprite source cell is empty.");
        }
        return cell.GetRegion(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1));
    }

    private static Image FitPlayer(Image source)
    {
        using (source)
        {
            float scale = Mathf.Min(
                MaximumBodyWidth / (float)source.GetWidth(),
                MatchSpriteAtlas.BodyHeight / (float)source.GetHeight());
            int width = Mathf.Max(1, Mathf.RoundToInt(source.GetWidth() * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.GetHeight() * scale));
            source.Resize(width, height, Image.Interpolation.Nearest);
            Image canvas = CreateCanvas(MatchSpriteAtlas.FrameWidth, MatchSpriteAtlas.FrameHeight);
            canvas.BlitRect(source, new Rect2I(0, 0, width, height),
                new Vector2I((MatchSpriteAtlas.FrameWidth - width) / 2, (int)MatchSpriteAtlas.FootPivot.Y - height));
            return canvas;
        }
    }

    private static Image CreateMask(Image sprite, int facing)
    {
        Image mask = CreateCanvas(MatchSpriteAtlas.FrameWidth, MatchSpriteAtlas.FrameHeight);
        Vector3 boundaries = KitBoundaries[facing];
        float top = MatchSpriteAtlas.FootPivot.Y - MatchSpriteAtlas.BodyHeight;
        for (int y = 0; y < MatchSpriteAtlas.FrameHeight; y++)
        {
            float bodyY = (y - top) / MatchSpriteAtlas.BodyHeight;
            for (int x = 0; x < MatchSpriteAtlas.FrameWidth; x++)
            {
                Color color = sprite.GetPixel(x, y);
                float brightest = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
                float darkest = Mathf.Min(color.R, Mathf.Min(color.G, color.B));
                // Neutral cloth is gray. Skin/hair, fixed white trim, dark outlines and boots remain untouched.
                if (color.A < 0.5f || brightest - darkest > 0.085f || brightest < 0.20f ||
                    brightest > 0.87f || bodyY < 0.30f || bodyY >= boundaries.Z)
                {
                    continue;
                }
                Color region = bodyY < boundaries.X ? Colors.Red
                    : bodyY < boundaries.Y ? new Color(0f, 1f, 0f) : Colors.Blue;
                mask.SetPixel(x, y, region);
            }
        }
        return mask;
    }
}
