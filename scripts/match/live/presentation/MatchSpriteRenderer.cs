using System;
using System.Collections.Generic;
using Godot;

public partial class MatchSpriteRenderer : Node2D
{
    private sealed class PlayerVisual
    {
        public required Sprite2D Sprite { get; init; }
        public required ShaderMaterial Material { get; init; }
        public required Label Label { get; init; }
        public required MatchSpriteMotion Motion { get; init; }
        public int Facing { get; set; } = -1;
        public MatchSpriteKitPalette? Palette { get; set; }
        public PlayerMarkerLabelMode? LabelMode { get; set; }
        public int SquadNumber { get; set; }
        public string Role { get; set; } = "";
        public bool IsMarker { get; set; }
    }

    private readonly Dictionary<StringName, PlayerVisual> _players = new();
    private MatchSpriteAtlas? _atlas;
    private Sprite2D? _ballSprite;
    private Sprite2D? _ballShadowSprite;
    private Texture2D? _markerTexture;
    private Texture2D? _plainBallTexture;
    private Vector2 _previousBallPosition;
    private float _ballTravelMeters;

    public void Initialize(MatchSpriteAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        if (_atlas is not null)
        {
            throw new InvalidOperationException("Sprite renderer is already initialized.");
        }
        _atlas = atlas;
        YSortEnabled = true;
        TextureFilter = TextureFilterEnum.Nearest;
        _markerTexture = CreateCircleTexture(32);
        _plainBallTexture = CreateCircleTexture(16);
        _ballShadowSprite = new Sprite2D
        {
            Name = "BallShadowSprite",
            Texture = atlas.BallShadow,
            Centered = true,
            Visible = false
        };
        AddChild(_ballShadowSprite);
        _ballSprite = new Sprite2D { Name = "BallSprite", Texture = atlas.Ball(0), Visible = false };
        AddChild(_ballSprite);
    }

    public void Reset(
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> teams,
        StringName homeTeamId,
        bool homeAttacksLeft,
        Vector2 ballPosition)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(teams);
        foreach (PlayerVisual player in _players.Values)
        {
            RemoveChild(player.Sprite);
            RemoveChild(player.Label);
            player.Sprite.QueueFree();
            player.Label.QueueFree();
        }
        _players.Clear();
        Capture(positions, teams, homeTeamId, homeAttacksLeft, ballPosition);
        _previousBallPosition = ballPosition;
        _ballTravelMeters = 0f;
    }

    public void Capture(
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> teams,
        StringName homeTeamId,
        bool homeAttacksLeft,
        Vector2 ballPosition,
        bool updatePlayerMotion = true)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(teams);
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            bool attacksLeft = (teams[playerId] == homeTeamId) == homeAttacksLeft;
            int initialFacing = attacksLeft ? 2 : 6;
            if (!_players.TryGetValue(playerId, out PlayerVisual? player))
            {
                _players[playerId] = CreatePlayer(playerId, position, initialFacing);
                continue;
            }
            if (updatePlayerMotion)
            {
                player.Motion.Capture(position, initialFacing);
            }
        }
        foreach ((StringName playerId, PlayerVisual player) in _players)
        {
            bool active = positions.ContainsKey(playerId);
            player.Sprite.Visible = active;
            player.Label.Visible = active;
        }

        float ballDistance = FootballPitchDimensions.DistanceMeters(_previousBallPosition, ballPosition);
        _ballTravelMeters = ballDistance > 5f ? 0f : _ballTravelMeters + ballDistance;
        _previousBallPosition = ballPosition;
    }

    private PlayerVisual CreatePlayer(StringName playerId, Vector2 position, int facing)
    {
        MatchSpriteAtlas atlas = _atlas ?? throw new InvalidOperationException("Sprite atlas is not initialized.");
        ShaderMaterial material = new() { Shader = atlas.KitShader };
        // The mask sheet shares the player sheet's layout, so one texture serves every facing.
        material.SetShaderParameter("kit_mask", atlas.KitMaskSheet);
        material.SetShaderParameter("sheet_grid",
            new Vector2(MatchSpriteAtlas.SheetColumns, MatchSpriteAtlas.SheetRows));
        Sprite2D sprite = new()
        {
            Name = $"Player_{playerId}",
            Centered = false,
            Offset = -MatchSpriteAtlas.FootPivot,
            Texture = atlas.Player(facing),
            Material = material
        };
        AddChild(sprite);
        Label label = new()
        {
            Name = $"Label_{playerId}",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(38f, 16f),
            ZIndex = 2000
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color("15212c"));
        label.AddThemeConstantOverride("outline_size", 3);
        AddChild(label);
        return new PlayerVisual
        {
            Sprite = sprite,
            Material = material,
            Label = label,
            Motion = new MatchSpriteMotion(position, facing)
        };
    }

    public void SetPlayerVisual(
        StringName playerId,
        Vector2 footPoint,
        float bodyHeight,
        MatchSpriteKitPalette palette,
        PlayerMarkerLabelMode labelMode,
        string role,
        int squadNumber,
        int? depthIndex = null)
    {
        if (!_players.TryGetValue(playerId, out PlayerVisual? player) || _atlas is null)
        {
            return;
        }
        if (player.IsMarker)
        {
            player.IsMarker = false;
            player.Facing = -1;
            player.LabelMode = null;
            player.Sprite.Material = player.Material;
            player.Sprite.SelfModulate = Colors.White;
            player.Sprite.Offset = -MatchSpriteAtlas.FootPivot;
        }
        if (player.Facing != player.Motion.Facing)
        {
            player.Facing = player.Motion.Facing;
            player.Sprite.Texture = _atlas.Player(player.Facing);
        }
        if (player.Palette != palette)
        {
            player.Palette = palette;
            player.Material.SetShaderParameter("shirt_color", palette.Shirt);
            player.Material.SetShaderParameter("shorts_color", palette.Shorts);
            player.Material.SetShaderParameter("socks_color", palette.Socks);
        }
        float step = player.Motion.IsMoving ? Mathf.Sin(player.Motion.StridePhase) : 0f;
        player.Material.SetShaderParameter("step_offset", step * 1.5f);
        float scale = bodyHeight / MatchSpriteAtlas.BodyHeight;
        player.Sprite.Scale = Vector2.One * scale;
        player.Sprite.Position = footPoint;
        player.Sprite.ZIndex = depthIndex ?? Mathf.Clamp(Mathf.RoundToInt(footPoint.Y) + 100, 1, 1500);
        player.Label.Position = footPoint + new Vector2(-19f, 3f);
        if (player.LabelMode != labelMode || player.SquadNumber != squadNumber || player.Role != role)
        {
            player.LabelMode = labelMode;
            player.SquadNumber = squadNumber;
            player.Role = role;
            player.Label.Text = labelMode == PlayerMarkerLabelMode.Position
                ? role : squadNumber > 0 ? squadNumber.ToString() : "?";
        }
    }

    public void SetPlayerMarkerVisual(StringName playerId, Vector2 point, float diameter, Color color,
        string text, int depthIndex)
    {
        if (!_players.TryGetValue(playerId, out PlayerVisual? player))
        {
            return;
        }
        player.IsMarker = true;
        player.Sprite.Material = null;
        player.Sprite.Texture = _markerTexture;
        player.Sprite.Offset = new Vector2(-16f, -16f);
        player.Sprite.SelfModulate = color;
        player.Sprite.Position = point;
        player.Sprite.Scale = Vector2.One * (diameter / 32f);
        player.Sprite.ZIndex = depthIndex;
        player.Label.Position = point + new Vector2(-19f, -7f);
        player.Label.Text = text;
    }

    public void SetBallVisual(Vector2 groundPoint, Vector2 airPoint, float diameter, bool visible,
        int? depthIndex = null, bool spriteEnabled = true)
    {
        if (_ballSprite is null || _atlas is null)
        {
            return;
        }
        _ballSprite.Visible = visible;
        if (_ballShadowSprite is not null)
        {
            _ballShadowSprite.Visible = visible && spriteEnabled;
        }
        if (!visible)
        {
            return;
        }
        if (_ballShadowSprite is not null && spriteEnabled)
        {
            float heightOffset = groundPoint.DistanceTo(airPoint);
            float shadowScale = Mathf.Clamp(1f - heightOffset * 0.008f, 0.35f, 1.2f) * (diameter / 16f);
            _ballShadowSprite.Scale = new Vector2(shadowScale, shadowScale * 0.8f);
            _ballShadowSprite.Position = groundPoint + new Vector2(0f, 1f);
            _ballShadowSprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - heightOffset * 0.012f, 0.25f, 0.9f));
            _ballShadowSprite.ZIndex = Mathf.Clamp(Mathf.RoundToInt(groundPoint.Y) + 50, 1, 1500);
        }
        _ballSprite.Texture = spriteEnabled ? _atlas.Ball((int)(_ballTravelMeters / 0.18f) % 2) : _plainBallTexture;
        _ballSprite.Scale = Vector2.One * (diameter / 16f);
        _ballSprite.Rotation = _ballTravelMeters * 2f;
        _ballSprite.Position = airPoint;
        _ballSprite.ZIndex = depthIndex ?? (groundPoint.DistanceTo(airPoint) > 4f
            ? 2100 : Mathf.Clamp(Mathf.RoundToInt(groundPoint.Y) + 105, 1, 1505));
    }

    private static Texture2D CreateCircleTexture(int size)
    {
        using Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float radius = size / 2f - 1f;
        Vector2 center = new((size - 1f) / 2f, (size - 1f) / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x, y).DistanceTo(center);
                if (distance <= radius)
                {
                    image.SetPixel(x, y, distance >= radius - 1f ? new Color("b7c2ca") : Colors.White);
                }
            }
        }
        return ImageTexture.CreateFromImage(image);
    }
}
