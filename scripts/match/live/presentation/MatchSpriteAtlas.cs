using System;
using Godot;

public sealed class MatchSpriteAtlas
{
    public const string PlayerTexturePath =
        "res://assets/football/pixel_sprites/modular_preview/neutral_eight_directions_v1.png";
    public const string BallTexturePath = "res://assets/football/pixel_sprites/sprite_preview_v1.png";
    public const string ShaderPath = "res://assets/football/pixel_sprites/kit_recolor.gdshader";
    public static readonly Vector2 FootPivot = new(24f, 70f);
    public const int FrameWidth = 48;
    public const int FrameHeight = 72;
    public const int BodyHeight = 64;

    private readonly Texture2D[] _players = new Texture2D[8];
    private readonly Texture2D[] _masks = new Texture2D[8];
    private readonly Texture2D[] _balls = new Texture2D[2];
    private static readonly Vector3[] KitBoundaries =
    {
        new(0.62f, 0.77f, 0.88f), new(0.61f, 0.74f, 0.85f),
        new(0.67f, 0.78f, 0.88f), new(0.63f, 0.76f, 0.87f),
        new(0.65f, 0.78f, 0.88f), new(0.65f, 0.76f, 0.87f),
        new(0.66f, 0.78f, 0.89f), new(0.62f, 0.76f, 0.86f)
    };

    public Shader KitShader { get; }

    public MatchSpriteAtlas()
    {
        KitShader = GD.Load<Shader>(ShaderPath);
        using Image sheet = LoadImage(PlayerTexturePath);
        for (int facing = 0; facing < 8; facing++)
        {
            using Image sprite = FitPlayer(CropCell(sheet, facing % 4, facing / 4));
            using Image mask = CreateMask(sprite, facing);
            _players[facing] = ImageTexture.CreateFromImage(sprite);
            _masks[facing] = ImageTexture.CreateFromImage(mask);
        }

        using Image ballSheet = LoadImage(BallTexturePath);
        for (int frame = 0; frame < 2; frame++)
        {
            using Image ball = CropCell(ballSheet, frame + 2, 1);
            ball.Resize(16, 16, Image.Interpolation.Nearest);
            _balls[frame] = ImageTexture.CreateFromImage(ball);
        }
    }

    public Texture2D Player(int facing) => _players[ValidateFacing(facing)];
    public Texture2D Mask(int facing) => _masks[ValidateFacing(facing)];
    public Texture2D Ball(int frame) => _balls[frame is >= 0 and < 2
        ? frame : throw new ArgumentOutOfRangeException(nameof(frame))];

    private static int ValidateFacing(int facing) => facing is >= 0 and < 8
        ? facing : throw new ArgumentOutOfRangeException(nameof(facing));

    private static Image LoadImage(string path)
    {
        Texture2D texture = GD.Load<Texture2D>(path);
        Image image = texture.GetImage();
        if (image is null || image.IsEmpty())
        {
            throw new InvalidOperationException($"Cannot read sprite texture: {path}");
        }
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot decompress sprite texture: {path}");
        }
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    private static Image CropCell(Image sheet, int column, int row)
    {
        int left = sheet.GetWidth() * column / 4;
        int top = sheet.GetHeight() * row / 2;
        int right = sheet.GetWidth() * (column + 1) / 4;
        int bottom = sheet.GetHeight() * (row + 1) / 2;
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
                if (pixels[(y * cell.GetWidth() + x) * 4 + 3] < 160)
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
            throw new InvalidOperationException("A sprite atlas cell is empty.");
        }
        return cell.GetRegion(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1));
    }

    private static Image FitPlayer(Image source)
    {
        using (source)
        {
            float scale = Mathf.Min(44f / source.GetWidth(), BodyHeight / (float)source.GetHeight());
            int width = Mathf.Max(1, Mathf.RoundToInt(source.GetWidth() * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.GetHeight() * scale));
            source.Resize(width, height, Image.Interpolation.Nearest);
            Image canvas = Image.CreateEmpty(FrameWidth, FrameHeight, false, Image.Format.Rgba8);
            canvas.Fill(Colors.Transparent);
            canvas.BlitRect(source, new Rect2I(0, 0, width, height),
                new Vector2I((FrameWidth - width) / 2, (int)FootPivot.Y - height));
            return canvas;
        }
    }

    private static Image CreateMask(Image sprite, int facing)
    {
        Image mask = Image.CreateEmpty(FrameWidth, FrameHeight, false, Image.Format.Rgba8);
        mask.Fill(Colors.Transparent);
        Vector3 boundaries = KitBoundaries[facing];
        float top = FootPivot.Y - BodyHeight;
        for (int y = 0; y < FrameHeight; y++)
        {
            float bodyY = (y - top) / BodyHeight;
            for (int x = 0; x < FrameWidth; x++)
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
