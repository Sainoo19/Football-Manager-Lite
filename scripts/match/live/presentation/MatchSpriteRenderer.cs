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
    }

    private readonly Dictionary<StringName, PlayerVisual> _players = new();
    private MatchSpriteAtlas? _atlas;
    private Sprite2D? _ballSprite;
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
        TextureFilter = TextureFilterEnum.Nearest;
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
        int squadNumber)
    {
        if (!_players.TryGetValue(playerId, out PlayerVisual? player) || _atlas is null)
        {
            return;
        }
        if (player.Facing != player.Motion.Facing)
        {
            player.Facing = player.Motion.Facing;
            player.Sprite.Texture = _atlas.Player(player.Facing);
            player.Material.SetShaderParameter("kit_mask", _atlas.Mask(player.Facing));
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
        player.Sprite.ZIndex = Mathf.Clamp(Mathf.RoundToInt(footPoint.Y) + 100, 1, 1500);
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

    public void SetBallVisual(Vector2 groundPoint, Vector2 airPoint, float diameter, bool visible)
    {
        if (_ballSprite is null || _atlas is null)
        {
            return;
        }
        _ballSprite.Visible = visible;
        if (!visible)
        {
            return;
        }
        _ballSprite.Texture = _atlas.Ball((int)(_ballTravelMeters / 0.18f) % 2);
        _ballSprite.Scale = Vector2.One * (diameter / 16f);
        _ballSprite.Rotation = _ballTravelMeters * 2f;
        _ballSprite.Position = airPoint;
        _ballSprite.ZIndex = groundPoint.DistanceTo(airPoint) > 4f
            ? 2100 : Mathf.Clamp(Mathf.RoundToInt(groundPoint.Y) + 105, 1, 1505);
    }
}
