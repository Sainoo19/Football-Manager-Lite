using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Main : Control
{
    private static readonly Color BgColor = new("101722");
    private static readonly Color PanelColor = new("182231");
    private static readonly Color PanelAltColor = new("202d3e");
    private static readonly Color TextColor = new("edf3fb");
    private static readonly Color MutedColor = new("91a2b8");
    private static readonly Color AccentColor = new("42d392");

    public Array<FootballTeam> teams { get; private set; } = new();
    public FootballTeam? selected_team { get; private set; }
    public FootballTeam? managed_team { get; private set; }
    public MatchCenter MatchView => _matchView;

    private VBoxContainer _teamList = null!;
    private Label _teamName = null!;
    private Label _teamMeta = null!;
    private Label _squadSummary = null!;
    private VBoxContainer _roster = null!;
    private Button _chooseButton = null!;
    private Label _managedTeamLabel = null!;
    private OptionButton _filterOption = null!;
    private Control _clubView = null!;
    private Control _lineupView = null!;
    private MatchCenter _matchView = null!;
    private Button _lineupNavButton = null!;
    private Button _matchNavButton = null!;
    private OptionButton _formationOption = null!;
    private PitchBoard _pitchBoard = null!;
    private VBoxContainer _lineupPlayers = null!;
    private Label _lineupSummary = null!;
    private Label _lineupInstruction = null!;
    private Label _lineupValidation = null!;
    private Label _lineupTeam = null!;
    private Array<FormationDefinition> _formations = new();
    private FormationDefinition? _activeFormation;
    private StringName _selectedSlotId = new();
    private readonly SampleDataFactory _dataFactory = new();
    private readonly FormationCatalog _formationCatalog = new();
    private readonly LineupManager _lineupManager = new();

    public override void _Ready()
    {
        teams = _dataFactory.create_teams();
        _formations = _formationCatalog.all();
        BuildInterface();
        if (teams.Count > 0) SelectTeam(teams[0]);
    }

    private void BuildInterface()
    {
        var background = new ColorRect { Color = BgColor };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var page = new VBoxContainer();
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.AddThemeConstantOverride("separation", 18);
        page.OffsetLeft = 28;
        page.OffsetTop = 24;
        page.OffsetRight = -28;
        page.OffsetBottom = -24;
        AddChild(page);
        page.AddChild(BuildHeader());

        var stack = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(stack);
        var clubs = new HBoxContainer();
        clubs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        clubs.AddThemeConstantOverride("separation", 18);
        stack.AddChild(clubs);
        _clubView = clubs;
        clubs.AddChild(BuildClubPanel());
        clubs.AddChild(BuildRosterPanel());

        _lineupView = BuildLineupView();
        _lineupView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _lineupView.Visible = false;
        stack.AddChild(_lineupView);
        _matchView = new MatchCenter { Visible = false };
        _matchView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _matchView.BackRequested += ShowLineupView;
        stack.AddChild(_matchView);
    }

    private Control BuildHeader()
    {
        var header = new PanelContainer { CustomMinimumSize = new Vector2(0, 82) };
        header.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        header.AddChild(row);
        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(titleBox);
        var title = new Label { Text = "PURE FOOTBALL MANAGER" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", TextColor);
        titleBox.AddChild(title);
        var subtitle = new Label { Text = "Xây đội hình  •  Thi đấu  •  Chinh phục giải đấu" };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        subtitle.AddThemeColorOverride("font_color", MutedColor);
        titleBox.AddChild(subtitle);

        var clubsButton = new Button { Text = "CLB", CustomMinimumSize = new Vector2(92, 40) };
        clubsButton.Pressed += ShowClubView;
        row.AddChild(clubsButton);
        _lineupNavButton = new Button { Text = "ĐỘI HÌNH", CustomMinimumSize = new Vector2(120, 40), Disabled = true };
        _lineupNavButton.Pressed += ShowLineupView;
        row.AddChild(_lineupNavButton);
        _matchNavButton = new Button { Text = "TRẬN ĐẤU", CustomMinimumSize = new Vector2(125, 40), Disabled = true };
        _matchNavButton.Pressed += ShowMatchView;
        row.AddChild(_matchNavButton);
        var managedBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(managedBox);
        var caption = new Label { Text = "CLB CỦA BẠN", HorizontalAlignment = HorizontalAlignment.Right };
        caption.AddThemeFontSizeOverride("font_size", 11);
        caption.AddThemeColorOverride("font_color", MutedColor);
        managedBox.AddChild(caption);
        _managedTeamLabel = new Label { Text = "Chưa lựa chọn", HorizontalAlignment = HorizontalAlignment.Right };
        _managedTeamLabel.AddThemeFontSizeOverride("font_size", 17);
        _managedTeamLabel.AddThemeColorOverride("font_color", AccentColor);
        managedBox.AddChild(_managedTeamLabel);
        return header;
    }

    private Control BuildClubPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(290, 0) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        box.AddChild(SectionLabel("CHỌN CÂU LẠC BỘ", 13));
        box.AddChild(new HSeparator());
        _teamList = new VBoxContainer();
        _teamList.AddThemeConstantOverride("separation", 8);
        box.AddChild(_teamList);
        foreach (FootballTeam team in teams)
        {
            var button = new Button
            {
                Text = $"{team.short_name}   {team.display_name}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 48)
            };
            button.AddThemeFontSizeOverride("font_size", 15);
            button.Pressed += () => SelectTeam(team);
            _teamList.AddChild(button);
        }
        return panel;
    }

    private Control BuildRosterPanel()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var heading = new HBoxContainer();
        box.AddChild(heading);
        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(identity);
        _teamName = new Label();
        _teamName.AddThemeFontSizeOverride("font_size", 28);
        _teamName.AddThemeColorOverride("font_color", TextColor);
        identity.AddChild(_teamName);
        _teamMeta = new Label();
        _teamMeta.AddThemeFontSizeOverride("font_size", 13);
        _teamMeta.AddThemeColorOverride("font_color", MutedColor);
        identity.AddChild(_teamMeta);
        _chooseButton = new Button { Text = "Chọn làm CLB của bạn", CustomMinimumSize = new Vector2(205, 46) };
        _chooseButton.AddThemeColorOverride("font_color", new Color("092118"));
        _chooseButton.AddThemeStyleboxOverride("normal", PanelStyle(AccentColor, 9));
        _chooseButton.AddThemeStyleboxOverride("hover", PanelStyle(new Color("62e8ac"), 9));
        _chooseButton.Pressed += ChooseSelectedTeam;
        heading.AddChild(_chooseButton);

        var summaryPanel = new PanelContainer();
        summaryPanel.AddThemeStyleboxOverride("panel", PanelStyle(PanelAltColor, 10));
        box.AddChild(summaryPanel);
        var summaryRow = new HBoxContainer();
        summaryPanel.AddChild(summaryRow);
        _squadSummary = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _squadSummary.AddThemeFontSizeOverride("font_size", 15);
        _squadSummary.AddThemeColorOverride("font_color", TextColor);
        summaryRow.AddChild(_squadSummary);
        _filterOption = new OptionButton();
        _filterOption.AddItem("Tất cả cầu thủ", 0);
        _filterOption.AddItem("Đăng ký trận", 1);
        _filterOption.AddItem("Ngoài danh sách", 2);
        _filterOption.ItemSelected += _ => RefreshRoster();
        summaryRow.AddChild(_filterOption);
        box.AddChild(BuildTableHeader());
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(scroll);
        _roster = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _roster.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_roster);
        return panel;
    }

    private static Control BuildTableHeader()
    {
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        header.AddThemeConstantOverride("separation", 8);
        header.AddChild(TableLabel("#", 42, MutedColor));
        header.AddChild(TableLabel("CẦU THỦ", 260, MutedColor, true));
        header.AddChild(TableLabel("VT", 64, MutedColor));
        header.AddChild(TableLabel("TUỔI", 64, MutedColor));
        header.AddChild(TableLabel("OVR", 70, MutedColor));
        header.AddChild(TableLabel("TRẠNG THÁI", 150, MutedColor, true));
        return header;
    }

    private Control BuildLineupView()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        var toolbar = new PanelContainer();
        toolbar.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 12, 12));
        root.AddChild(toolbar);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        toolbar.AddChild(row);
        var back = new Button { Text = "← Danh sách CLB", CustomMinimumSize = new Vector2(150, 0) };
        back.Pressed += ShowClubView;
        row.AddChild(back);
        _lineupTeam = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _lineupTeam.AddThemeFontSizeOverride("font_size", 20);
        _lineupTeam.AddThemeColorOverride("font_color", TextColor);
        row.AddChild(_lineupTeam);
        row.AddChild(CaptionLabel("Sơ đồ"));
        _formationOption = new OptionButton { CustomMinimumSize = new Vector2(145, 40) };
        foreach (FormationDefinition formation in _formations) _formationOption.AddItem(formation.display_name);
        _formationOption.ItemSelected += OnFormationSelected;
        row.AddChild(_formationOption);
        var auto = new Button { Text = "Tự động xếp", CustomMinimumSize = new Vector2(140, 40) };
        auto.Pressed += AutoBuildLineup;
        row.AddChild(auto);
        var match = new Button { Text = "Vào Match Center →", CustomMinimumSize = new Vector2(175, 40) };
        match.Pressed += ShowMatchView;
        row.AddChild(match);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var pitchPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        pitchPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color("13202c"), 14, 10));
        body.AddChild(pitchPanel);
        _pitchBoard = new PitchBoard
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _pitchBoard.SlotSelected += OnPitchSlotSelected;
        pitchPanel.AddChild(_pitchBoard);

        var squadPanel = new PanelContainer { CustomMinimumSize = new Vector2(435, 0) };
        squadPanel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14, 12));
        body.AddChild(squadPanel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        squadPanel.AddChild(box);
        box.AddChild(SectionLabel("DANH SÁCH THI ĐẤU", 13));
        _lineupSummary = new Label();
        _lineupSummary.AddThemeFontSizeOverride("font_size", 15);
        _lineupSummary.AddThemeColorOverride("font_color", TextColor);
        box.AddChild(_lineupSummary);
        _lineupInstruction = new Label
        {
            Text = "Chọn một vị trí trên sân, sau đó chọn cầu thủ.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 38)
        };
        _lineupInstruction.AddThemeFontSizeOverride("font_size", 12);
        _lineupInstruction.AddThemeColorOverride("font_color", new Color("f0d36c"));
        box.AddChild(_lineupInstruction);
        var playerHeader = new HBoxContainer();
        playerHeader.AddThemeConstantOverride("separation", 6);
        playerHeader.AddChild(TableLabel("CẦU THỦ", 165, MutedColor, true));
        playerHeader.AddChild(TableLabel("VT", 40, MutedColor));
        playerHeader.AddChild(TableLabel("TRẠNG THÁI", 82, MutedColor));
        playerHeader.AddChild(TableLabel("", 74, MutedColor));
        box.AddChild(playerHeader);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(scroll);
        _lineupPlayers = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _lineupPlayers.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_lineupPlayers);
        _lineupValidation = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 42)
        };
        _lineupValidation.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_lineupValidation);
        return root;
    }

    public void ShowClubView()
    {
        _clubView.Visible = true;
        _lineupView.Visible = false;
        _matchView.Visible = false;
        _matchView.PauseMatch();
    }

    public void ShowLineupView()
    {
        if (managed_team is null) return;
        _clubView.Visible = false;
        _lineupView.Visible = true;
        _matchView.Visible = false;
        _matchView.PauseMatch();
        _activeFormation = _formationCatalog.find(managed_team.match_squad.formation_id);
        _selectedSlotId = new StringName();
        for (int i = 0; i < _formations.Count; i++)
            if (_formations[i].id == _activeFormation.id) { _formationOption.Select(i); break; }
        RefreshLineup();
    }

    public void ShowMatchView()
    {
        if (managed_team is null) return;
        _clubView.Visible = false;
        _lineupView.Visible = false;
        _matchView.Visible = true;
        _matchView.Configure(teams, managed_team);
    }

    private void OnFormationSelected(long index)
    {
        if (managed_team is null || index < 0 || index >= _formations.Count) return;
        _activeFormation = _formations[(int)index];
        _selectedSlotId = new StringName();
        _lineupManager.auto_build(managed_team.match_squad, _activeFormation, managed_team.players);
        _lineupInstruction.Text = $"Đã chuyển sang {_activeFormation.display_name} và tự động xếp lại đội.";
        RefreshLineup();
    }

    private void AutoBuildLineup()
    {
        if (managed_team is null || _activeFormation is null) return;
        _selectedSlotId = new StringName();
        _lineupManager.auto_build(managed_team.match_squad, _activeFormation, managed_team.players);
        _lineupInstruction.Text = "Đã chọn đội hình mạnh và phù hợp vị trí nhất.";
        RefreshLineup();
    }

    private void OnPitchSlotSelected(StringName slotId)
    {
        if (_activeFormation is null) return;
        _selectedSlotId = slotId;
        _lineupInstruction.Text = $"Đang chọn vị trí {SlotRole(_activeFormation.get_slot(slotId))} — nhấp vào một cầu thủ bên phải.";
        _pitchBoard.SetSelectedSlot(slotId);
        RefreshLineupPlayers();
    }

    private void OnLineupPlayerPressed(FootballPlayer player)
    {
        if (managed_team is null || _activeFormation is null) return;
        if (_selectedSlotId == new StringName())
        {
            _lineupInstruction.Text = "Hãy chọn một vị trí trên sân trước.";
            return;
        }
        _lineupManager.assign_player_to_slot(managed_team.match_squad, _activeFormation, player.id, _selectedSlotId);
        _lineupInstruction.Text = $"Đã xếp {player.display_name} vào vị trí {SlotRole(_activeFormation.get_slot(_selectedSlotId))}.";
        _selectedSlotId = new StringName();
        RefreshLineup();
    }

    private void ToggleSubstitute(FootballPlayer player)
    {
        if (managed_team is null) return;
        bool wasSubstitute = managed_team.match_squad.substitute_ids.Contains(player.id);
        bool changed = _lineupManager.toggle_substitute(managed_team.match_squad, player.id);
        _lineupInstruction.Text = !changed
            ? "Ghế dự bị đã đủ 12 người hoặc cầu thủ đang đá chính."
            : wasSubstitute ? $"Đã đưa {player.display_name} ra ngoài danh sách trận." : $"Đã thêm {player.display_name} vào ghế dự bị.";
        RefreshLineup();
    }

    private void RefreshLineup()
    {
        if (managed_team is null || _activeFormation is null) return;
        _lineupTeam.Text = $"{managed_team.display_name}  •  Phòng chiến thuật";
        _pitchBoard.SetLineup(managed_team, _activeFormation, _selectedSlotId);
        MatchSquad squad = managed_team.match_squad;
        Dictionary strengths = _lineupManager.calculate_strengths(squad, _activeFormation, managed_team.players);
        _lineupSummary.Text = $"11 chính: {squad.starter_ids.Count}/11   •   Dự bị: {squad.substitute_ids.Count}/12\nPhòng ngự {strengths["defense"].AsInt32()}   |   Tuyến giữa {strengths["midfield"].AsInt32()}   |   Tấn công {strengths["attack"].AsInt32()}";
        string[] errors = squad.validate_against(managed_team.players);
        int mismatches = CountPositionMismatches();
        if (errors.Length == 0 && mismatches == 0)
        {
            _lineupValidation.Text = "✓ Đội hình hợp lệ và sẵn sàng thi đấu.";
            _lineupValidation.AddThemeColorOverride("font_color", AccentColor);
        }
        else if (errors.Length == 0)
        {
            _lineupValidation.Text = $"⚠ Đội hình hợp lệ, nhưng có {mismatches} cầu thủ đá trái sở trường.";
            _lineupValidation.AddThemeColorOverride("font_color", new Color("f0d36c"));
        }
        else
        {
            _lineupValidation.Text = $"⚠ {string.Join("  ", errors)}";
            _lineupValidation.AddThemeColorOverride("font_color", new Color("ff8a8a"));
        }
        RefreshLineupPlayers();
        RefreshRoster();
    }

    private int CountPositionMismatches()
    {
        if (managed_team is null || _activeFormation is null) return 0;
        int count = 0;
        foreach (Dictionary slot in _activeFormation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            StringName playerId = managed_team.match_squad.starter_slots.ContainsKey(slotId)
                ? managed_team.match_squad.starter_slots[slotId].AsStringName() : new StringName();
            FootballPlayer? player = managed_team.get_player(playerId);
            if (player is not null && _lineupManager.position_fit(player.position, SlotRole(slot)) < 70) count++;
        }
        return count;
    }

    private void RefreshLineupPlayers()
    {
        foreach (Node child in _lineupPlayers.GetChildren()) child.QueueFree();
        if (managed_team is null) return;
        foreach (FootballPlayer player in managed_team.players.OrderBy(player => SquadRank(player.id)).ThenByDescending(player => player.overall))
            _lineupPlayers.AddChild(BuildLineupPlayerRow(player));
    }

    private Control BuildLineupPlayerRow(FootballPlayer player)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color("1b2736"), 6, 5));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);
        var playerButton = new Button
        {
            Text = $"{player.display_name}  {player.overall}",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(165, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        playerButton.Pressed += () => OnLineupPlayerPressed(player);
        if (_selectedSlotId != new StringName() && _activeFormation is not null)
        {
            int fit = _lineupManager.position_fit(player.position, SlotRole(_activeFormation.get_slot(_selectedSlotId)));
            playerButton.AddThemeColorOverride("font_color", fit >= 72 ? AccentColor : new Color("e7a06a"));
        }
        row.AddChild(playerButton);
        row.AddChild(TableLabel(player.position, 40, PositionColor(player.position)));
        string status = "Ngoài DS";
        if (managed_team!.match_squad.starter_ids.Contains(player.id) && _activeFormation is not null)
            status = $"Đá chính {SlotRole(_activeFormation.get_slot(managed_team.match_squad.get_slot_for_player(player.id)))}";
        else if (managed_team.match_squad.substitute_ids.Contains(player.id)) status = "Dự bị";
        row.AddChild(TableLabel(status, 82, status != "Ngoài DS" ? AccentColor : MutedColor));
        var action = new Button { CustomMinimumSize = new Vector2(74, 0) };
        if (managed_team.match_squad.starter_ids.Contains(player.id))
        {
            action.Text = "Đang đá";
            action.Disabled = true;
        }
        else
        {
            action.Text = managed_team.match_squad.substitute_ids.Contains(player.id) ? "Bỏ DB" : "+ Dự bị";
            action.Pressed += () => ToggleSubstitute(player);
        }
        row.AddChild(action);
        return panel;
    }

    private int SquadRank(StringName playerId)
    {
        if (managed_team!.match_squad.starter_ids.Contains(playerId)) return 0;
        return managed_team.match_squad.substitute_ids.Contains(playerId) ? 1 : 2;
    }

    public void SelectTeam(FootballTeam team)
    {
        selected_team = team;
        _teamName.Text = team.display_name;
        _teamName.AddThemeColorOverride("font_color", team.primary_color.Lightened(0.3f));
        _teamMeta.Text = $"{team.short_name}  •  {team.country}";
        UpdateChooseButton();
        RefreshRoster();
    }

    public void ChooseSelectedTeam()
    {
        if (selected_team is null) return;
        managed_team = selected_team;
        _managedTeamLabel.Text = managed_team.display_name;
        _lineupNavButton.Disabled = false;
        _matchNavButton.Disabled = false;
        _activeFormation = _formationCatalog.find(managed_team.match_squad.formation_id);
        UpdateChooseButton();
        ShowLineupView();
    }

    private void UpdateChooseButton()
    {
        bool chosen = managed_team is not null && selected_team is not null && managed_team.id == selected_team.id;
        _chooseButton.Disabled = chosen;
        _chooseButton.Text = chosen ? "Đã chọn" : "Chọn làm CLB của bạn";
    }

    private void RefreshRoster()
    {
        foreach (Node child in _roster.GetChildren()) child.QueueFree();
        if (selected_team is null) return;
        int registered = selected_team.match_squad.registered_count();
        _squadSummary.Text = $"Quân số: {selected_team.players.Count} (không giới hạn)   •   Đăng ký trận: {registered}/23   •   Ngoài danh sách: {selected_team.outside_match_squad_count()}";
        int visibleNumber = 0;
        foreach (FootballPlayer player in selected_team.players)
        {
            bool isRegistered = selected_team.match_squad.is_registered(player.id);
            if (_filterOption.Selected == 1 && !isRegistered || _filterOption.Selected == 2 && isRegistered) continue;
            _roster.AddChild(BuildPlayerRow(player, ++visibleNumber, isRegistered));
        }
    }

    private static Control BuildPlayerRow(FootballPlayer player, int number, bool registered)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 44) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(number % 2 == 1 ? PanelAltColor : new Color("1b2736"), 7, 8));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);
        row.AddChild(TableLabel(number.ToString(), 42, MutedColor));
        row.AddChild(TableLabel(player.display_name, 260, TextColor, true));
        row.AddChild(TableLabel(player.position, 64, PositionColor(player.position)));
        row.AddChild(TableLabel(player.age.ToString(), 64, TextColor));
        row.AddChild(TableLabel(player.overall.ToString(), 70, RatingColor(player.overall)));
        row.AddChild(TableLabel(registered ? "Đăng ký trận" : "Ngoài danh sách", 150, registered ? AccentColor : MutedColor, true));
        return panel;
    }

    private static Label TableLabel(string value, float width, Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = value,
            CustomMinimumSize = new Vector2(width, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", color);
        if (expand) label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static Label CaptionLabel(string value) => new()
    {
        Text = value,
        VerticalAlignment = VerticalAlignment.Center,
        Modulate = MutedColor
    };

    private static Label SectionLabel(string value, int size)
    {
        var label = new Label { Text = value };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", MutedColor);
        return label;
    }

    private static string SlotRole(Dictionary slot) => slot.ContainsKey("role") ? slot["role"].AsString() : "";

    private static StyleBoxFlat PanelStyle(Color color, int radius, int padding = 16) => new()
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

    private static Color PositionColor(string position) => position switch
    {
        "GK" => new Color("f0c75e"),
        "CB" or "LB" or "RB" => new Color("62a8ff"),
        "DM" or "CM" or "AM" => new Color("7fe0bd"),
        _ => new Color("ff8a8a")
    };

    private static Color RatingColor(int rating) => rating >= 76 ? new Color("62e8ac") : rating >= 70 ? new Color("f0d36c") : new Color("e7a06a");
}
