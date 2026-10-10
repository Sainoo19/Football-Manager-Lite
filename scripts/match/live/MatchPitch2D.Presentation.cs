using Godot;

public partial class MatchPitch2D
{
    private static readonly Vector2 CompactMinimumSize = new(600f, 360f);
    private Texture2D? _pixelPitchTexture;
    private Rect2 _pixelPitchRect;
    private MatchSpriteRenderer? _spriteRenderer;
    private MatchGoalRenderer? _goalRenderer;

    public PlayerMarkerLabelMode MarkerLabelMode { get; private set; } = PlayerMarkerLabelMode.Position;
    public bool IsExpandedDisplay { get; private set; } = true;
    public bool IsPixelPitchEnabled { get; private set; } = true;
    public bool IsSpriteDisplayEnabled { get; private set; } = true;
    public bool IsHomeAlternativeKitEnabled { get; private set; }

    public MatchSpriteKitPalette HomeKit => Simulation is null
        ? new MatchSpriteKitPalette(HomeColor, Colors.White, HomeColor)
        : MatchSpriteKitPalette.ForTeam(Simulation.home.team, IsHomeAlternativeKitEnabled);

    public MatchSpriteKitPalette AwayKit
    {
        get
        {
            if (Simulation is null)
            {
                return new MatchSpriteKitPalette(AwayColor, Colors.White, AwayColor);
            }
            MatchSpriteKitPalette palette = MatchSpriteKitPalette.ForTeam(Simulation.away.team, false);
            return HomeKit.HasSimilarShirt(palette)
                ? MatchSpriteKitPalette.ForTeam(Simulation.away.team, true) : palette;
        }
    }

    public void SetSpriteDisplayEnabled(bool enabled)
    {
        IsSpriteDisplayEnabled = enabled;
        if (_spriteRenderer is not null)
        {
            _spriteRenderer.Visible = Simulation is not null;
        }
        QueueRedraw();
    }

    public void SetHomeAlternativeKitEnabled(bool enabled)
    {
        IsHomeAlternativeKitEnabled = enabled;
        QueueRedraw();
    }

    private void ResetSpritePresentation()
    {
        if (_spriteRenderer is null || Simulation is null)
        {
            return;
        }
        _spriteRenderer.Reset(CurrentPositions, PlayerTeams, Simulation.home.team.id, !AreSidesSwitched, BallPosition);
    }

    private void CapturePresentationPositions()
    {
        bool playersChanged = _playerPositionInterpolator.Capture(CurrentPositions);
        if (_spriteRenderer is not null && Simulation is not null)
        {
            // Ball-only movement must also advance the rolling pose between player updates.
            _spriteRenderer.Capture(CurrentPositions, PlayerTeams, Simulation.home.team.id,
                !AreSidesSwitched, BallPosition, playersChanged);
        }
    }

    public void SetPixelPitchEnabled(bool enabled)
    {
        IsPixelPitchEnabled = enabled;
        QueueRedraw();
    }

    public void SetMarkerLabelMode(PlayerMarkerLabelMode mode)
    {
        MarkerLabelMode = mode;
        QueueRedraw();
    }

    public void SetExpandedDisplay(bool expanded)
    {
        IsExpandedDisplay = expanded;
        // Expanded mode takes the space offered by its container. A large fixed
        // minimum height would push the field below shorter desktop windows.
        CustomMinimumSize = expanded ? Vector2.Zero : CompactMinimumSize;
        QueueRedraw();
    }
}
