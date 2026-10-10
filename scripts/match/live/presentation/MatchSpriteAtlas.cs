using System;
using Godot;

// Loads the pre-normalised sprite sheets. All cropping, scaling and mask generation happens offline in
// tools/assets/PlayerSpriteSheetExporter; nothing is processed when a match opens.
public sealed class MatchSpriteAtlas
{
    public const string PlayerSheetPath = "res://assets/football/pixel_sprites/runtime/player_sheet_48x72.png";
    public const string KitMaskSheetPath = "res://assets/football/pixel_sprites/runtime/player_kit_mask_48x72.png";
    public const string BallSheetPath = "res://assets/football/pixel_sprites/runtime/ball_sheet_16x16.png";
    public const string BallShadowPath = "res://assets/football/pixel_sprites/runtime/ball_shadow_16x8.png";
    public const string ShaderPath = "res://assets/football/pixel_sprites/kit_recolor.gdshader";
    public static readonly Vector2 FootPivot = new(24f, 70f);
    public const int FrameWidth = 48;
    public const int FrameHeight = 72;
    public const int BodyHeight = 64;
    // Frame index equals facing: column = facing % SheetColumns, row = facing / SheetColumns.
    public const int FacingCount = 8;
    public const int SheetColumns = 4;
    public const int SheetRows = 2;
    public const int BallSize = 16;
    public const int BallFrameCount = 2;
    public const int BallShadowWidth = 16;
    public const int BallShadowHeight = 8;

    private readonly Texture2D[] _players = new Texture2D[FacingCount];
    private readonly Texture2D[] _masks = new Texture2D[FacingCount];
    private readonly Texture2D[] _balls = new Texture2D[BallFrameCount];

    public Texture2D PlayerSheet { get; }
    // The kit shader samples this whole sheet with the player sheet's UV, so it is never sliced.
    public Texture2D KitMaskSheet { get; }
    public Texture2D BallSheet { get; }
    public Texture2D BallShadow { get; }
    public Shader KitShader { get; }

    public MatchSpriteAtlas()
    {
        KitShader = GD.Load<Shader>(ShaderPath);
        Vector2 playerSheetSize = new(FrameWidth * SheetColumns, FrameHeight * SheetRows);
        PlayerSheet = LoadSheet(PlayerSheetPath, playerSheetSize);
        KitMaskSheet = LoadSheet(KitMaskSheetPath, playerSheetSize);
        BallSheet = LoadSheet(BallSheetPath, new Vector2(BallSize * BallFrameCount, BallSize));
        BallShadow = LoadSheet(BallShadowPath, new Vector2(BallShadowWidth, BallShadowHeight));
        // Frames are regions of the loaded sheets; no pixels are copied or processed.
        for (int facing = 0; facing < FacingCount; facing++)
        {
            _players[facing] = new AtlasTexture { Atlas = PlayerSheet, Region = FrameRegion(facing) };
            _masks[facing] = new AtlasTexture { Atlas = KitMaskSheet, Region = FrameRegion(facing) };
        }
        for (int frame = 0; frame < BallFrameCount; frame++)
        {
            _balls[frame] = new AtlasTexture
            {
                Atlas = BallSheet,
                Region = new Rect2(frame * BallSize, 0f, BallSize, BallSize)
            };
        }
    }

    public Texture2D Player(int facing) => _players[ValidateFacing(facing)];
    public Texture2D Mask(int facing) => _masks[ValidateFacing(facing)];
    public Texture2D Ball(int frame) => _balls[frame is >= 0 and < BallFrameCount
        ? frame : throw new ArgumentOutOfRangeException(nameof(frame))];

    private static int ValidateFacing(int facing) => facing is >= 0 and < FacingCount
        ? facing : throw new ArgumentOutOfRangeException(nameof(facing));

    public static Rect2I FrameRegion(int facing)
    {
        ValidateFacing(facing);
        return new Rect2I(facing % SheetColumns * FrameWidth, facing / SheetColumns * FrameHeight,
            FrameWidth, FrameHeight);
    }

    private static Texture2D LoadSheet(string path, Vector2 expectedSize)
    {
        Texture2D texture = GD.Load<Texture2D>(path)
            ?? throw new InvalidOperationException($"Cannot load sprite sheet: {path}");
        if (texture.GetSize() != expectedSize)
        {
            throw new InvalidOperationException(
                $"Sprite sheet {path} is {texture.GetSize()}, expected {expectedSize}. Re-run the exporter.");
        }
        return texture;
    }
}
