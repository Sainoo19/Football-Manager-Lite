using Godot;

public partial class MatchCenter
{
    private Control BuildPitchDisplayControls()
    {
        var controls = new VBoxContainer();
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
            TooltipText = "Đổi nhãn số áo hoặc vị trí cạnh cầu thủ"
        };
        markerMode.AddItem("Số áo");
        markerMode.AddItem("Vị trí");
        markerMode.Select(1);
        markerMode.ItemSelected += index => _pitchView.SetMarkerLabelMode(
            index == 0 ? PlayerMarkerLabelMode.SquadNumber : PlayerMarkerLabelMode.Position);
        row.AddChild(markerMode);

        var pixelPitch = new CheckButton
        {
            Text = "Sân pixel 2.5D",
            ButtonPressed = true,
            TooltipText = "Đổi giữa sân pixel góc nhìn nghiêng và sân 2D đơn giản"
        };
        pixelPitch.Toggled += _pitchView.SetPixelPitchEnabled;
        row.AddChild(pixelPitch);

        var expandedDisplay = new CheckButton
        {
            Text = "Sân lớn",
            ButtonPressed = true,
            TooltipText = "Phóng lớn sân theo vùng hiển thị, giữ nguyên tỷ lệ hình ảnh"
        };
        expandedDisplay.Toggled += SetPitchDisplayExpanded;
        row.AddChild(expandedDisplay);
        controls.AddChild(row);

        var spriteRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        spriteRow.AddThemeConstantOverride("separation", 8);
        var sprites = new CheckButton
        {
            Text = "Sprite 2.5D",
            ButtonPressed = true,
            TooltipText = "Hiển thị cầu thủ và bóng bằng sprite; tắt để xem các chấm cũ"
        };
        sprites.Toggled += _pitchView.SetSpriteDisplayEnabled;
        spriteRow.AddChild(sprites);
        spriteRow.AddChild(new Label { Text = "Áo đội bạn:" });
        var kit = new OptionButton { CustomMinimumSize = new Vector2(130f, 32f) };
        kit.AddItem("Sân nhà");
        kit.AddItem("Sân khách");
        kit.ItemSelected += index =>
        {
            _pitchView.SetHomeAlternativeKitEnabled(index == 1);
            RefreshPitchLegend();
        };
        spriteRow.AddChild(kit);
        controls.AddChild(spriteRow);
        return controls;
    }

    private void RefreshPitchLegend()
    {
        _homeLegend?.AddThemeColorOverride("font_color", _pitchView.HomeKit.Shirt);
        _awayLegend?.AddThemeColorOverride("font_color", _pitchView.AwayKit.Shirt);
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
