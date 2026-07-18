using Godot;

public partial class MatchCenter
{
    private Control BuildScenarioMenu()
    {
        MenuButton menu = new()
        {
            Text = "Test tình huống ▾",
            CustomMinimumSize = new Vector2(175, 40),
            TooltipText = "Dựng nhanh một pha bóng để quan sát engine tự xử lý"
        };
        PopupMenu popup = menu.GetPopup();
        popup.AddItem("Chọc khe — nhận bóng cách gôn 35 m", (int)MatchScenarioKind.ThroughBallBreakaway);
        popup.AddItem("Phản công 2 đánh 1", (int)MatchScenarioKind.TwoAttackersVersusOneDefender);
        popup.AddItem("Phản công 3 đánh 2", (int)MatchScenarioKind.ThreeAttackersVersusTwoDefenders);
        popup.AddSeparator("Tranh chấp mặt đất");
        popup.AddItem("1 đấu 1 trung lộ", (int)MatchScenarioKind.CentralOneVersusOne);
        popup.AddItem("1 đấu 1 ngoài biên", (int)MatchScenarioKind.WideOneVersusOne);
        popup.AddItem("Tiền đạo quay lưng che bóng", (int)MatchScenarioKind.StrikerBackToGoalOneVersusOne);
        popup.AddSeparator("Bóng bổng");
        popup.AddItem("Chuyền bổng — tranh điểm rơi", (int)MatchScenarioKind.LoftedPassAerialDuel);
        popup.AddItem("Tạt bóng bổng — tranh chấp điểm rơi", (int)MatchScenarioKind.AerialCrossIntoBox);
        popup.AddItem("Phá bóng bổng dưới áp lực", (int)MatchScenarioKind.AerialClearanceUnderPressure);
        popup.IdPressed += id => PrepareScenario((MatchScenarioKind)id);
        return menu;
    }

    public void PrepareScenario(MatchScenarioKind kind)
    {
        PrepareNewMatch();
        if (simulation is null || !_pitchView.StartScenario(kind))
        {
            _statusLabel.Text = "Không thể dựng tình huống với đội hình hiện tại";
            return;
        }

        _speedOption.Select((int)MatchPlaybackSpeed.RealTime);
        _matchClock.SetSpeed(MatchPlaybackSpeed.RealTime);
        _matchClock.Start();
        _pitchView.SetPlaying(true);
        string scenarioName = MatchScenarioFactory.DisplayName(kind);
        _minuteLabel.Text = "TEST TÌNH HUỐNG";
        _statusLabel.Text = $"Đang test · {scenarioName}";
        _playButton.Text = "Ⅱ Tạm dừng";
        AppendEvent(new FootballMatchEvent().setup(
            0,
            "scenario",
            $"Bắt đầu test tình huống: {scenarioName}. Engine tự quyết định phần còn lại."));
    }
}
