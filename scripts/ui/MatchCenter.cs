using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchCenter : Control
{
    [Signal] public delegate void BackRequestedEventHandler();

    private static readonly Color PanelColor = new("182231");
    private static readonly Color PanelAltColor = new("202d3e");
    private static readonly Color TextColor = new("edf3fb");
    private static readonly Color MutedColor = new("91a2b8");
    private static readonly Color AccentColor = new("42d392");

    public Array<FootballTeam> teams { get; private set; } = new();
    public FootballTeam? managed_team { get; private set; }
    public Array<FootballTeam> opponents { get; } = new();
    public FootballMatchSimulation? simulation { get; private set; }

    private OptionButton _opponentOption = null!;
    private Label _homeNameLabel = null!;
    private Label _awayNameLabel = null!;
    private Label _scoreLabel = null!;
    private Label _minuteLabel = null!;
    private Label _statusLabel = null!;
    private Label _pitchActionLabel = null!;
    private MatchPitch2D _pitchView = null!;
    private ScrollContainer _eventScroll = null!;
    private VBoxContainer _eventContainer = null!;
    private Label _homeStatsLabel = null!;
    private Label _awayStatsLabel = null!;
    private Button _playButton = null!;
    private Button _finishButton = null!;
    private OptionButton _speedOption = null!;
    private OptionButton _mentalityOption = null!;
    private OptionButton _outgoingOption = null!;
    private OptionButton _incomingOption = null!;
    private Button _substitutionButton = null!;
    private Label _substitutionStatusLabel = null!;
    private Label _substitutionPreviewLabel = null!;
    private Timer _matchTimer = null!;
    private readonly LineupManager _lineupManager = new();

    public override void _Ready() => BuildInterface();

    public void Configure(Array<FootballTeam> allTeams, FootballTeam userTeam)
    {
        bool changedTeam = managed_team is null || managed_team.id != userTeam.id;
        teams = allTeams;
        managed_team = userTeam;
        if (!IsNodeReady())
            return;
        if (changedTeam)
        {
            simulation = null;
            RebuildOpponents();
            PrepareScoreboard();
        }
    }

    public void PauseMatch()
    {
        _matchTimer?.Stop();
        _pitchView?.SetPlaying(false);
        if (_playButton is not null && simulation is not null && !simulation.is_finished)
            _playButton.Text = "▶ Tiếp tục";
    }

    private void BuildInterface()
    {
        var page = new VBoxContainer();
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.AddThemeConstantOverride("separation", 12);
        AddChild(page);
        page.AddChild(BuildToolbar());
        page.AddChild(BuildScoreboard());

        var body = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 14);
        page.AddChild(body);
        body.AddChild(BuildEventPanel());
        body.AddChild(BuildControlPanel());

        _matchTimer = new Timer { WaitTime = 0.28 };
        _matchTimer.Timeout += OnMatchTick;
        AddChild(_matchTimer);
        SetControlsEnabled(false);

        if (managed_team is not null)
        {
            RebuildOpponents();
            PrepareScoreboard();
        }
    }

    private Control BuildToolbar()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 12, 12));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        var backButton = new Button { Text = "← Đội hình", CustomMinimumSize = new Vector2(120, 40) };
        backButton.Pressed += OnBackPressed;
        row.AddChild(backButton);

        var title = new Label { Text = "MATCH CENTER", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", TextColor);
        row.AddChild(title);
        row.AddChild(CaptionLabel("Đối thủ"));

        _opponentOption = new OptionButton { CustomMinimumSize = new Vector2(205, 40) };
        _opponentOption.ItemSelected += OnOpponentSelected;
        row.AddChild(_opponentOption);

        var newMatchButton = new Button { Text = "Tạo trận mới", CustomMinimumSize = new Vector2(140, 40) };
        newMatchButton.Pressed += PrepareNewMatch;
        row.AddChild(newMatchButton);
        return panel;
    }

    private Control BuildScoreboard()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 112) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color("13202c"), 14, 14));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 28);
        panel.AddChild(row);

        _homeNameLabel = ScoreboardTeamLabel("CLB của bạn");
        _homeNameLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_homeNameLabel);

        var scoreBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(190, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddChild(scoreBox);
        _minuteLabel = new Label { Text = "CHƯA BẮT ĐẦU", HorizontalAlignment = HorizontalAlignment.Center };
        _minuteLabel.AddThemeFontSizeOverride("font_size", 12);
        _minuteLabel.AddThemeColorOverride("font_color", AccentColor);
        scoreBox.AddChild(_minuteLabel);
        _scoreLabel = new Label { Text = "0  –  0", HorizontalAlignment = HorizontalAlignment.Center };
        _scoreLabel.AddThemeFontSizeOverride("font_size", 38);
        _scoreLabel.AddThemeColorOverride("font_color", TextColor);
        scoreBox.AddChild(_scoreLabel);

        _awayNameLabel = ScoreboardTeamLabel("Đối thủ");
        _awayNameLabel.HorizontalAlignment = HorizontalAlignment.Left;
        row.AddChild(_awayNameLabel);
        return panel;
    }

    private Control BuildEventPanel()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14, 12));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var header = new HBoxContainer();
        box.AddChild(header);
        var title = new Label { Text = "DIỄN BIẾN TRẬN ĐẤU", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", MutedColor);
        header.AddChild(title);
        _statusLabel = new Label { Text = "Hãy tạo một trận mới" };
        _statusLabel.AddThemeColorOverride("font_color", AccentColor);
        header.AddChild(_statusLabel);

        var pitchHeader = new HBoxContainer();
        pitchHeader.AddThemeConstantOverride("separation", 14);
        box.AddChild(pitchHeader);
        var caption = new Label { Text = "MÔ PHỎNG 2D" };
        caption.AddThemeFontSizeOverride("font_size", 11);
        caption.AddThemeColorOverride("font_color", MutedColor);
        pitchHeader.AddChild(caption);
        _pitchActionLabel = new Label
        {
            Text = "• Chờ bắt đầu pha bóng",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _pitchActionLabel.AddThemeFontSizeOverride("font_size", 11);
        _pitchActionLabel.AddThemeColorOverride("font_color", new Color("f0d36c"));
        pitchHeader.AddChild(_pitchActionLabel);
        var homeLegend = new Label { Text = "● Đội bạn" };
        homeLegend.AddThemeColorOverride("font_color", new Color("4f8cff"));
        pitchHeader.AddChild(homeLegend);
        var awayLegend = new Label { Text = "● Đối thủ" };
        awayLegend.AddThemeColorOverride("font_color", new Color("ff5d73"));
        pitchHeader.AddChild(awayLegend);

        _pitchView = new MatchPitch2D { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pitchView.ActionChanged += description => _pitchActionLabel.Text = $"• {description}";
        _pitchView.LiveMatchEvent += OnLiveMatchEvent;
        box.AddChild(_pitchView);
        _eventScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(_eventScroll);
        _eventContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _eventContainer.AddThemeConstantOverride("separation", 6);
        _eventScroll.AddChild(_eventContainer);
        return panel;
    }

    private Control BuildControlPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(390, 0) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14, 12));
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        panel.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(box);

        box.AddChild(SectionLabel("ĐIỀU KHIỂN TRẬN ĐẤU", 13));
        var playRow = new HBoxContainer();
        playRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(playRow);
        _playButton = new Button
        {
            Text = "▶ Bắt đầu",
            CustomMinimumSize = new Vector2(145, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _playButton.Pressed += TogglePlay;
        playRow.AddChild(_playButton);
        _finishButton = new Button { Text = "Mô phỏng hết", CustomMinimumSize = new Vector2(135, 44) };
        _finishButton.Pressed += SimulateToEnd;
        playRow.AddChild(_finishButton);

        var speedRow = new HBoxContainer();
        speedRow.AddChild(CaptionLabel("Tốc độ", true));
        _speedOption = new OptionButton();
        foreach (string label in new[] { "1×", "4×", "8×" }) _speedOption.AddItem(label);
        _speedOption.Select(1);
        _speedOption.ItemSelected += OnSpeedSelected;
        speedRow.AddChild(_speedOption);
        box.AddChild(speedRow);

        box.AddChild(new HSeparator());
        box.AddChild(SectionLabel("THỐNG KÊ"));
        var statsRow = new HBoxContainer();
        _homeStatsLabel = StatsValueLabel(HorizontalAlignment.Left);
        statsRow.AddChild(_homeStatsLabel);
        var names = new Label
        {
            Text = "Kiểm soát bóng\nCú sút\nTrúng đích\nPhạt góc\nPhạm lỗi\nThẻ vàng\nThẻ đỏ",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        names.AddThemeColorOverride("font_color", MutedColor);
        names.AddThemeFontSizeOverride("font_size", 12);
        statsRow.AddChild(names);
        _awayStatsLabel = StatsValueLabel(HorizontalAlignment.Right);
        statsRow.AddChild(_awayStatsLabel);
        box.AddChild(statsRow);

        box.AddChild(new HSeparator());
        box.AddChild(SectionLabel("CHIẾN THUẬT TRONG TRẬN"));
        var mentalityRow = new HBoxContainer();
        mentalityRow.AddChild(CaptionLabel("Tâm lý", true));
        _mentalityOption = new OptionButton();
        foreach (string label in new[] { "Phòng ngự", "Cân bằng", "Tấn công" }) _mentalityOption.AddItem(label);
        _mentalityOption.Select(1);
        _mentalityOption.ItemSelected += OnMentalitySelected;
        mentalityRow.AddChild(_mentalityOption);
        box.AddChild(mentalityRow);

        box.AddChild(SectionLabel("THAY NGƯỜI (TỐI ĐA 5)"));
        var help = new Label
        {
            Text = "Làm theo 3 bước: chọn người ra → chọn người vào → xác nhận. Game sẽ gợi ý cầu thủ cùng vị trí.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        help.AddThemeFontSizeOverride("font_size", 11);
        help.AddThemeColorOverride("font_color", new Color("f0d36c"));
        box.AddChild(help);

        var outgoingRow = new HBoxContainer();
        outgoingRow.AddChild(CaptionLabel("1  Chọn người RA", true));
        _outgoingOption = new OptionButton
        {
            CustomMinimumSize = new Vector2(220, 0),
            TooltipText = "Chọn một cầu thủ đang thi đấu để đưa ra sân"
        };
        _outgoingOption.ItemSelected += OnOutgoingSelected;
        outgoingRow.AddChild(_outgoingOption);
        box.AddChild(outgoingRow);

        var incomingRow = new HBoxContainer();
        incomingRow.AddChild(CaptionLabel("2  Chọn người VÀO", true));
        _incomingOption = new OptionButton
        {
            CustomMinimumSize = new Vector2(220, 0),
            TooltipText = "Chọn một cầu thủ trên ghế dự bị để đưa vào sân"
        };
        _incomingOption.ItemSelected += OnIncomingSelected;
        incomingRow.AddChild(_incomingOption);
        box.AddChild(incomingRow);

        var previewPanel = new PanelContainer();
        previewPanel.AddThemeStyleboxOverride("panel", PanelStyle(PanelAltColor, 8, 9));
        _substitutionPreviewLabel = new Label
        {
            Text = "Chưa chọn phương án thay người",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        previewPanel.AddChild(_substitutionPreviewLabel);
        box.AddChild(previewPanel);

        _substitutionButton = new Button
        {
            Text = "3  XÁC NHẬN THAY NGƯỜI",
            CustomMinimumSize = new Vector2(0, 40)
        };
        _substitutionButton.Pressed += MakeSubstitution;
        box.AddChild(_substitutionButton);
        _substitutionStatusLabel = new Label { Text = "Đã dùng: 0/5" };
        _substitutionStatusLabel.AddThemeFontSizeOverride("font_size", 12);
        _substitutionStatusLabel.AddThemeColorOverride("font_color", MutedColor);
        box.AddChild(_substitutionStatusLabel);
        return panel;
    }

    private void RebuildOpponents()
    {
        opponents.Clear();
        _opponentOption.Clear();
        if (managed_team is null) return;
        foreach (FootballTeam team in teams)
        {
            if (team.id == managed_team.id) continue;
            opponents.Add(team);
            _opponentOption.AddItem(team.display_name);
        }
        if (opponents.Count > 0) _opponentOption.Select(0);
    }

    private void PrepareScoreboard()
    {
        if (managed_team is null) return;
        _homeNameLabel.Text = managed_team.display_name;
        _awayNameLabel.Text = opponents.Count > 0 ? opponents[0].display_name : "Chưa có đối thủ";
        _scoreLabel.Text = "0  –  0";
        _minuteLabel.Text = "CHƯA BẮT ĐẦU";
        _statusLabel.Text = "Hãy tạo một trận mới";
        ClearEvents();
        UpdateStatsLabels();
    }

    public void PrepareNewMatch()
    {
        if (managed_team is null || opponents.Count == 0) return;
        _matchTimer.Stop();
        FootballTeam opponent = opponents[_opponentOption.Selected];
        long seed = unchecked((long)Time.GetTicksMsec()) + managed_team.id.GetHashCode() + opponent.id.GetHashCode();
        simulation = new FootballMatchSimulation().setup(managed_team, opponent, seed);
        simulation.use_live_pitch_events = true;
        _pitchView.SetMatch(simulation);
        _homeNameLabel.Text = managed_team.display_name;
        _awayNameLabel.Text = opponent.display_name;
        ClearEvents();
        AppendEvent(simulation.events[0]);
        _minuteLabel.Text = "SẴN SÀNG";
        _scoreLabel.Text = simulation.score_text();
        _statusLabel.Text = "Trận giao hữu";
        _playButton.Text = "▶ Bắt đầu";
        _mentalityOption.Select(1);
        SetControlsEnabled(true);
        RefreshSubstitutionOptions();
        UpdateStatsLabels();
    }

    private void TogglePlay()
    {
        if (simulation is null) PrepareNewMatch();
        if (simulation is null || simulation.is_finished) return;
        if (_matchTimer.IsStopped())
        {
            _matchTimer.Start();
            _pitchView.SetPlaying(true);
            _playButton.Text = "Ⅱ Tạm dừng";
            _statusLabel.Text = "Đang thi đấu";
        }
        else
        {
            _matchTimer.Stop();
            _pitchView.SetPlaying(false);
            _playButton.Text = "▶ Tiếp tục";
            _statusLabel.Text = "Đã tạm dừng";
        }
    }

    private void OnMatchTick()
    {
        if (simulation is null) return;
        Array<FootballMatchEvent> newEvents = simulation.advance_minute();
        foreach (FootballMatchEvent matchEvent in newEvents) AppendEvent(matchEvent);
        _pitchView.AnimateMinute(newEvents);
        RefreshMatchState();
    }

    public void SimulateToEnd()
    {
        if (simulation is null) PrepareNewMatch();
        if (simulation is null) return;
        _matchTimer.Stop();
        _pitchView.SetPlaying(false);
        simulation.use_live_pitch_events = false;
        Array<FootballMatchEvent> remaining = simulation.simulate_to_end();
        foreach (FootballMatchEvent matchEvent in remaining) AppendEvent(matchEvent);
        _pitchView.AnimateMinute(remaining);
        RefreshMatchState();
    }

    private void RefreshMatchState()
    {
        if (simulation is null) return;
        _scoreLabel.Text = simulation.score_text();
        _minuteLabel.Text = simulation.is_finished ? "HẾT TRẬN" : $"{simulation.current_minute}'";
        UpdateStatsLabels();
        if (!simulation.is_finished) return;
        _matchTimer.Stop();
        _pitchView.SetPlaying(false);
        _playButton.Text = "Đã kết thúc";
        _statusLabel.Text = "Trận đấu kết thúc";
        SetControlsEnabled(false);
    }

    private void UpdateStatsLabels()
    {
        if (simulation is null)
        {
            _homeStatsLabel.Text = "50%\n0\n0\n0\n0\n0\n0";
            _awayStatsLabel.Text = "50%\n0\n0\n0\n0\n0\n0";
            return;
        }
        _homeStatsLabel.Text = StatsText(simulation.home);
        _awayStatsLabel.Text = StatsText(simulation.away);
    }

    private void OnLiveMatchEvent(FootballMatchEvent matchEvent)
    {
        AppendEvent(matchEvent);
        RefreshMatchState();
    }

    private string StatsText(MatchTeamState state) =>
        $"{simulation!.get_possession(state)}%\n{state.stats["shots"].AsInt32()}\n{state.stats["shots_on_target"].AsInt32()}\n{state.stats["corners"].AsInt32()}\n{state.stats["fouls"].AsInt32()}\n{state.stats["yellow_cards"].AsInt32()}\n{state.stats["red_cards"].AsInt32()}";

    private void RefreshSubstitutionOptions()
    {
        _outgoingOption.Clear();
        _incomingOption.Clear();
        if (simulation is null) return;
        foreach (FootballPlayer player in simulation.home.get_starter_players())
        {
            _outgoingOption.AddItem($"{player.display_name} ({player.position})");
            _outgoingOption.SetItemMetadata(_outgoingOption.ItemCount - 1, player.id);
        }
        foreach (FootballPlayer player in simulation.home.get_substitute_players())
        {
            _incomingOption.AddItem($"{player.display_name} ({player.position})");
            _incomingOption.SetItemMetadata(_incomingOption.ItemCount - 1, player.id);
        }
        SuggestIncomingPlayer();
        UpdateSubstitutionPreview();
        _substitutionStatusLabel.Text = $"Còn lại: {5 - simulation.home.substitutions_used}/5 lượt thay người";
        _substitutionButton.Disabled = simulation.is_finished || _outgoingOption.ItemCount == 0 ||
                                       _incomingOption.ItemCount == 0 || simulation.home.substitutions_used >= 5;
    }

    private void MakeSubstitution()
    {
        if (simulation is null || managed_team is null || _outgoingOption.ItemCount == 0 || _incomingOption.ItemCount == 0) return;
        StringName outgoingId = _outgoingOption.GetItemMetadata(_outgoingOption.Selected).AsStringName();
        StringName incomingId = _incomingOption.GetItemMetadata(_incomingOption.Selected).AsStringName();
        FootballMatchEvent? matchEvent = simulation.make_substitution(managed_team.id, outgoingId, incomingId);
        if (matchEvent is null)
        {
            _substitutionStatusLabel.Text = simulation.last_error;
            _substitutionStatusLabel.AddThemeColorOverride("font_color", new Color("ff8a8a"));
            return;
        }
        AppendEvent(matchEvent);
        _pitchView.AnimateMinute(new Array<FootballMatchEvent> { matchEvent });
        _substitutionStatusLabel.AddThemeColorOverride("font_color", AccentColor);
        RefreshSubstitutionOptions();
    }

    private void OnOutgoingSelected(long index)
    {
        SuggestIncomingPlayer();
        UpdateSubstitutionPreview();
    }

    private void OnIncomingSelected(long index) => UpdateSubstitutionPreview();

    private void SuggestIncomingPlayer()
    {
        if (simulation is null || managed_team is null || _outgoingOption.ItemCount == 0 || _incomingOption.ItemCount == 0) return;
        StringName outgoingId = _outgoingOption.GetItemMetadata(_outgoingOption.Selected).AsStringName();
        FootballPlayer? outgoing = managed_team.get_player(outgoingId);
        if (outgoing is null) return;
        StringName slotId = simulation.home.squad.get_slot_for_player(outgoingId);
        Dictionary slot = simulation.home.formation.get_slot(slotId);
        string targetRole = slot.ContainsKey("role") ? slot["role"].AsString() : outgoing.position.ToString();
        int bestIndex = 0;
        int bestScore = -1;
        for (int index = 0; index < _incomingOption.ItemCount; index++)
        {
            StringName incomingId = _incomingOption.GetItemMetadata(index).AsStringName();
            FootballPlayer? incoming = managed_team.get_player(incomingId);
            if (incoming is null) continue;
            int score = _lineupManager.position_fit(incoming.position, targetRole) + incoming.overall;
            if (score <= bestScore) continue;
            bestScore = score;
            bestIndex = index;
        }
        _incomingOption.Select(bestIndex);
    }

    private void UpdateSubstitutionPreview()
    {
        if (simulation is null || managed_team is null || _outgoingOption.ItemCount == 0 || _incomingOption.ItemCount == 0)
        {
            _substitutionPreviewLabel.Text = "Không có phương án thay người khả dụng.";
            _substitutionButton.Text = "3  XÁC NHẬN THAY NGƯỜI";
            return;
        }
        StringName outgoingId = _outgoingOption.GetItemMetadata(_outgoingOption.Selected).AsStringName();
        StringName incomingId = _incomingOption.GetItemMetadata(_incomingOption.Selected).AsStringName();
        FootballPlayer? outgoing = managed_team.get_player(outgoingId);
        FootballPlayer? incoming = managed_team.get_player(incomingId);
        if (outgoing is null || incoming is null) return;
        StringName slotId = simulation.home.squad.get_slot_for_player(outgoingId);
        Dictionary slot = simulation.home.formation.get_slot(slotId);
        string role = slot.ContainsKey("role") ? slot["role"].AsString() : outgoing.position.ToString();
        int fit = _lineupManager.position_fit(incoming.position, role);
        string fitText = fit >= 90 ? "đúng sở trường" : fit >= 70 ? "chơi được" : "trái sở trường";
        _substitutionPreviewLabel.Text = $"↓ RA: {outgoing.display_name} ({outgoing.position})    →    ↑ VÀO: {incoming.display_name} ({incoming.position})\nMức phù hợp: {fitText}";
        _substitutionPreviewLabel.AddThemeColorOverride("font_color", fit >= 70 ? AccentColor : new Color("e7a06a"));
        _substitutionButton.Text = $"3  THAY NGAY: {ShortName(outgoing.display_name)} → {ShortName(incoming.display_name)}";
    }

    private void OnMentalitySelected(long index)
    {
        if (simulation is null || managed_team is null) return;
        var mentalities = new[] { new StringName("defensive"), new StringName("balanced"), new StringName("attacking") };
        FootballMatchEvent? matchEvent = simulation.change_mentality(managed_team.id, mentalities[(int)index]);
        if (matchEvent is null) return;
        AppendEvent(matchEvent);
        _pitchView.AnimateMinute(new Array<FootballMatchEvent> { matchEvent });
    }

    private void OnSpeedSelected(long index)
    {
        double[] speeds = { 1.0, 4.0, 8.0 };
        _matchTimer.WaitTime = 0.28 / speeds[(int)index];
    }

    private void OnOpponentSelected(long index)
    {
        if (simulation is null && opponents.Count > 0)
            _awayNameLabel.Text = opponents[_opponentOption.Selected].display_name;
    }

    private void SetControlsEnabled(bool enabled)
    {
        _playButton.Disabled = !enabled;
        _finishButton.Disabled = !enabled;
        _mentalityOption.Disabled = !enabled;
        _outgoingOption.Disabled = !enabled;
        _incomingOption.Disabled = !enabled;
        if (!enabled) _substitutionButton.Disabled = true;
    }

    private void AppendEvent(FootballMatchEvent matchEvent)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelAltColor, 7, 8));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);
        var minute = new Label
        {
            Text = matchEvent.minute > 0 ? $"{matchEvent.minute}'" : "KO",
            CustomMinimumSize = new Vector2(42, 0)
        };
        minute.AddThemeColorOverride("font_color", EventColor(matchEvent.event_type));
        row.AddChild(minute);
        var description = new Label
        {
            Text = matchEvent.text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        description.AddThemeColorOverride("font_color", TextColor);
        row.AddChild(description);
        _eventContainer.AddChild(panel);
        Callable.From(ScrollToBottom).CallDeferred();
    }

    private void ClearEvents()
    {
        foreach (Node child in _eventContainer.GetChildren()) child.QueueFree();
    }

    private void ScrollToBottom()
    {
        VScrollBar bar = _eventScroll.GetVScrollBar();
        bar.Value = bar.MaxValue;
    }

    private void OnBackPressed()
    {
        PauseMatch();
        EmitSignal(SignalName.BackRequested);
    }

    private static Label ScoreboardTeamLabel(string value)
    {
        var label = new Label
        {
            Text = value,
            CustomMinimumSize = new Vector2(300, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", TextColor);
        return label;
    }

    private static Label StatsValueLabel(HorizontalAlignment alignment)
    {
        var label = new Label
        {
            Text = "50%\n0\n0\n0\n0\n0\n0",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = alignment
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", TextColor);
        return label;
    }

    private static Label CaptionLabel(string value, bool expand = false)
    {
        var label = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", MutedColor);
        if (expand) label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static Label SectionLabel(string value, int size = 12)
    {
        var label = new Label { Text = value };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", MutedColor);
        return label;
    }

    private static Color EventColor(StringName eventType)
    {
        string type = eventType.ToString();
        return type switch
        {
            "goal" => AccentColor,
            "yellow_card" => new Color("f0d36c"),
            "red_card" => new Color("ff4d5f"),
            "half_time" or "full_time" => new Color("62a8ff"),
            "substitution" or "tactic" => new Color("bb8cff"),
            _ => MutedColor
        };
    }

    private static string ShortName(string fullName)
    {
        string[] parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : fullName;
    }

    private static StyleBoxFlat PanelStyle(Color color, int radius, int padding = 12) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = padding,
        ContentMarginBottom = padding
    };
}
