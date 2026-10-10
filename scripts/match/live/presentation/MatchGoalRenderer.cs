using Godot;

public partial class MatchGoalRenderer : Node2D
{
    private readonly Sprite2D[,] _layers = new Sprite2D[2, 3];
    private readonly int[] _frontDepths = new int[2];
    private readonly int[] _rearDepths = new int[2];

    public void Initialize()
    {
        YSortEnabled = true;
        TextureFilter = TextureFilterEnum.Nearest;
        string[] names = { "rear", "net", "front" };
        for (int side = 0; side < 2; side++)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                Sprite2D sprite = new()
                {
                    Name = $"Goal_{side}_{names[layer]}",
                    Texture = GD.Load<Texture2D>($"{PixelGoalLayout.AssetDirectory}/goal_{names[layer]}.png"),
                    FlipH = side == 1
                };
                _layers[side, layer] = sprite;
                AddChild(sprite);
            }
        }
    }

    public void UpdateLayout(Rect2 textureRect)
    {
        float scale = textureRect.Size.X / PixelPitchLayout.ImageSize.X;
        for (int side = 0; side < 2; side++)
        {
            Vector2 center = PixelPitchLayout.ToScreenPoint(new Vector2(side, 0.5f), textureRect);
            Vector2 nearFoot = PixelPitchLayout.ToScreenPoint(
                new Vector2(side, 0.5f + PixelGoalLayout.HalfWidth), textureRect);
            _frontDepths[side] = BaseDepth(nearFoot) + 2;
            _rearDepths[side] = BaseDepth(nearFoot) - 2;
            for (int layer = 0; layer < 3; layer++)
            {
                Sprite2D sprite = _layers[side, layer];
                sprite.Scale = Vector2.One * scale;
                sprite.Position = nearFoot;
                Vector2 pivot = side == 0 ? PixelGoalLayout.Pivot
                    : new Vector2(PixelGoalLayout.TextureSize - 1f - PixelGoalLayout.Pivot.X, PixelGoalLayout.Pivot.Y);
                sprite.Offset = new Vector2(PixelGoalLayout.TextureSize / 2f, PixelGoalLayout.TextureSize / 2f)
                    - pivot - (nearFoot - center) / scale;
                sprite.ZIndex = layer switch
                {
                    0 => _rearDepths[side],
                    1 => _frontDepths[side] - 1,
                    _ => _frontDepths[side]
                };
            }
        }
    }

    public int ActorDepth(Vector2 normalized, Vector2 groundPoint, float heightMeters = 0f, bool isBall = false)
    {
        float goalBand = PixelGoalLayout.HalfWidth + 1.2f / FootballPitchDimensions.WidthMeters;
        if (Mathf.Abs(normalized.Y - 0.5f) <= goalBand)
        {
            for (int side = 0; side < 2; side++)
            {
                float outwardMeters = (side == 0 ? -normalized.X : normalized.X - 1f)
                    * FootballPitchDimensions.LengthMeters;
                if (outwardMeters < -3f || outwardMeters > PixelGoalLayout.DepthMeters + 2f)
                {
                    continue;
                }
                // Y alone cannot distinguish an actor in the goal volume from one in front of its mouth.
                if (heightMeters >= PixelGoalLayout.HeightMeters || outwardMeters <= 0f)
                {
                    return _frontDepths[side] + 1;
                }
                return outwardMeters <= PixelGoalLayout.DepthMeters
                    ? _rearDepths[side] + 1 : _rearDepths[side] - 1;
            }
        }
        return isBall && heightMeters > 0.8f ? 2100 : BaseDepth(groundPoint) + (isBall ? 5 : 0);
    }

    private static int BaseDepth(Vector2 point) => Mathf.Clamp(Mathf.RoundToInt(point.Y) + 100, 1, 1500);
}
