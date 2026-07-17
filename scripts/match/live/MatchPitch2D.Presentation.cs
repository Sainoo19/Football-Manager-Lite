using Godot;

public partial class MatchPitch2D
{
    private static readonly Vector2 CompactMinimumSize = new(600f, 360f);

    public PlayerMarkerLabelMode MarkerLabelMode { get; private set; } = PlayerMarkerLabelMode.Position;
    public bool IsExpandedDisplay { get; private set; } = true;

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
