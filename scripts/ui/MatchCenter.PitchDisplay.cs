using Godot;

public partial class MatchCenter
{
    private Control BuildPitchDisplayControls()
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        row.AddThemeConstantOverride("separation", 8);

        var hint = new Label { Text = "Quan sát cầu thủ:" };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", MutedColor);
        row.AddChild(hint);

        var markerMode = new OptionButton
        {
            CustomMinimumSize = new Vector2(120f, 32f),
            TooltipText = "Đổi nhãn bên trong chấm cầu thủ để quan sát và debug"
        };
        markerMode.AddItem("Số áo");
        markerMode.AddItem("Vị trí");
        markerMode.Select(1);
        markerMode.ItemSelected += index => _pitchView.SetMarkerLabelMode(
            index == 0 ? PlayerMarkerLabelMode.SquadNumber : PlayerMarkerLabelMode.Position);
        row.AddChild(markerMode);

        var expandedDisplay = new CheckButton
        {
            Text = "Sân lớn",
            ButtonPressed = true,
            TooltipText = "Phóng lớn sân nhưng luôn giữ đúng tỷ lệ 105 × 68"
        };
        expandedDisplay.Toggled += SetPitchDisplayExpanded;
        row.AddChild(expandedDisplay);
        return row;
    }

    private void SetPitchDisplayExpanded(bool expanded)
    {
        _pitchView.SetExpandedDisplay(expanded);
        if (_eventScroll is not null)
        {
            _eventScroll.Visible = !expanded;
        }
    }
}
