using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GoalOcclusionPreviewRunner : Node
{
    private const string OutputPath = "res://.artifacts/test-reports/pitch-layers/v2/occlusion.png";

    public override void _Ready()
    {
        Callable.From(() => { _ = CaptureAsync(); }).CallDeferred();
    }

    private async Task CaptureAsync()
    {
        try
        {
            Vector2 canvasSize = GetViewport().GetVisibleRect().Size;
            ColorRect background = new() { Color = new Color("15222c"), Size = canvasSize };
            AddChild(background);
            MatchSpriteAtlas atlas = new();
            string[] titles = { "Trước miệng gôn", "Bên trong lưới", "Phía sau gôn", "Bóng bay qua xà" };
            float[] positions = { 1.8f, -1.4f, -2.8f, -1.4f };
            for (int i = 0; i < 4; i++)
            {
                CreatePanel(background, atlas, i, titles[i], positions[i], i == 3 ? 2.8f : 0f, canvasSize);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string path = ProjectSettings.GlobalizePath(OutputPath);
            DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
            using Image screenshot = GetViewport().GetTexture().GetImage();
            Error result = screenshot.SavePng(path);
            if (result != Error.Ok) throw new InvalidOperationException(result.ToString());
            GD.Print($"PASS: goal occlusion rendered to {path}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static void CreatePanel(Node parent, MatchSpriteAtlas atlas, int index,
        string title, float xMeters, float ballHeight, Vector2 viewportSize)
    {
        Control panel = new()
        {
            Position = new Vector2(index * viewportSize.X / 4f + 4f, 80f),
            Size = new Vector2(viewportSize.X / 4f - 8f, viewportSize.Y - 160f), ClipContents = true
        };
        parent.AddChild(panel);
        Label label = new() { Text = title, Position = new Vector2(12f, 8f), ZIndex = 2200 };
        label.AddThemeFontSizeOverride("font_size", 20);
        panel.AddChild(label);
        Vector2 canvasSize = PixelPitchLayout.ImageSize * 4f;
        Rect2 rect = new(Vector2.Zero, canvasSize);
        rect.Position = new Vector2(panel.Size.X * 0.65f, 350f)
            - PixelPitchLayout.ToScreenPoint(new Vector2(0f, 0.5f), rect);
        Sprite2D ground = new()
        {
            Texture = GD.Load<Texture2D>(PixelPitchLayout.TexturePath), Centered = false,
            Position = rect.Position, Scale = Vector2.One * 4f, TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        panel.AddChild(ground);
        Node2D actors = new() { YSortEnabled = true };
        panel.AddChild(actors);
        MatchGoalRenderer goals = new();
        goals.Initialize();
        actors.AddChild(goals);
        goals.UpdateLayout(rect);
        MatchSpriteRenderer sprites = new();
        sprites.Initialize(atlas);
        actors.AddChild(sprites);
        Vector2 playerPosition = new(xMeters / FootballPitchDimensions.LengthMeters, 0.49f);
        Vector2 ballPosition = new(xMeters / FootballPitchDimensions.LengthMeters, 0.53f);
        sprites.Reset(new Dictionary<StringName, Vector2> { ["preview"] = playerPosition },
            new Dictionary<StringName, StringName> { ["preview"] = "home" }, "home", false, ballPosition);
        Vector2 foot = PixelPitchLayout.ToScreenPoint(playerPosition, rect);
        sprites.SetPlayerVisual("preview", foot, 128f,
            new MatchSpriteKitPalette(new Color("e33232"), Colors.White, new Color("e33232")),
            PlayerMarkerLabelMode.SquadNumber, "CM", 8, goals.ActorDepth(playerPosition, foot));
        sprites.GetNode<Label>("Label_preview").Visible = false;
        Vector2 ballGround = PixelPitchLayout.ToScreenPoint(ballPosition, rect);
        Vector2 air = PixelGoalLayout.BallAirPoint(ballGround, ballHeight, rect);
        sprites.SetBallVisual(ballGround, air, 24f, true, goals.ActorDepth(ballPosition, ballGround, ballHeight, true));
        Label description = new()
        {
            Text = index == 3 ? "Bóng cao 2,8 m" : "Cầu thủ và bóng cùng vị trí",
            Position = new Vector2(12f, panel.Size.Y - 60f), ZIndex = 2200
        };
        description.AddThemeFontSizeOverride("font_size", 16);
        panel.AddChild(description);
    }
}
