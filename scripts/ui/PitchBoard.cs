using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PitchBoard : Control
{
    [Signal] public delegate void SlotSelectedEventHandler(StringName slotId);

    private static readonly Vector2 SlotSize = new(126, 56);

    public FootballTeam? Team { get; private set; }
    public FormationDefinition? Formation { get; private set; }
    public StringName SelectedSlotId { get; private set; } = new();

    private readonly System.Collections.Generic.Dictionary<StringName, Button> _slotButtons = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(600, 610);
        ClipContents = true;
    }

    public void SetLineup(FootballTeam team, FormationDefinition formation, StringName? selectedSlot = null)
    {
        Team = team;
        Formation = formation;
        SelectedSlotId = selectedSlot ?? new StringName();
        RebuildSlots();
        QueueRedraw();
    }

    public void SetSelectedSlot(StringName slotId)
    {
        SelectedSlotId = slotId;
        UpdateSlotStyles();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            LayoutSlots();
    }

    public override void _Draw()
    {
        Rect2 field = new(new Vector2(18, 12), Size - new Vector2(36, 24));
        DrawRect(field, new Color("176b45"));
        float stripeHeight = field.Size.Y / 10f;
        for (int index = 0; index < 10; index++)
        {
            if (index % 2 == 0)
                DrawRect(new Rect2(field.Position + new Vector2(0, stripeHeight * index), new Vector2(field.Size.X, stripeHeight)), new Color("1b754d"));
        }

        Color lineColor = new(1, 1, 1, 0.62f);
        DrawRect(field, lineColor, false, 2);
        float centerY = field.Position.Y + field.Size.Y * 0.5f;
        DrawLine(new Vector2(field.Position.X, centerY), new Vector2(field.End.X, centerY), lineColor, 2);
        DrawArc(new Vector2(field.GetCenter().X, centerY), Mathf.Min(field.Size.X, field.Size.Y) * 0.11f, 0, Mathf.Tau, 48, lineColor, 2);
        DrawCircle(new Vector2(field.GetCenter().X, centerY), 3.5f, lineColor);

        float penaltyWidth = field.Size.X * 0.48f;
        float penaltyHeight = field.Size.Y * 0.14f;
        DrawRect(new Rect2(new Vector2(field.GetCenter().X - penaltyWidth * 0.5f, field.Position.Y), new Vector2(penaltyWidth, penaltyHeight)), lineColor, false, 2);
        DrawRect(new Rect2(new Vector2(field.GetCenter().X - penaltyWidth * 0.5f, field.End.Y - penaltyHeight), new Vector2(penaltyWidth, penaltyHeight)), lineColor, false, 2);
    }

    private void RebuildSlots()
    {
        foreach (Node child in GetChildren())
            child.QueueFree();
        _slotButtons.Clear();
        if (Formation is null || Team is null)
            return;

        foreach (Dictionary slot in Formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            var button = new Button
            {
                CustomMinimumSize = SlotSize,
                Size = SlotSize,
                TooltipText = $"Chọn vị trí {slot["role"].AsString()}"
            };
            button.Pressed += () => OnSlotPressed(slotId);
            AddChild(button);
            _slotButtons[slotId] = button;
        }
        UpdateSlotLabels();
        LayoutSlots();
    }

    private void LayoutSlots()
    {
        if (Formation is null)
            return;
        Vector2 fieldPosition = new(18, 12);
        Vector2 fieldSize = Size - new Vector2(36, 24);
        foreach (Dictionary slot in Formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!_slotButtons.TryGetValue(slotId, out Button? button))
                continue;
            Vector2 center = fieldPosition + new Vector2(fieldSize.X * slot["x"].AsSingle(), fieldSize.Y * slot["y"].AsSingle());
            button.Position = center - SlotSize * 0.5f;
        }
    }

    private void UpdateSlotLabels()
    {
        if (Formation is null || Team is null)
            return;
        foreach (Dictionary slot in Formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            Button button = _slotButtons[slotId];
            StringName playerId = Team.match_squad.starter_slots.TryGetValue(slotId, out Variant value)
                ? value.AsStringName()
                : new StringName();
            FootballPlayer? player = Team.get_player(playerId);
            button.Text = player is null
                ? $"{slot["role"].AsString()}\nTrống"
                : $"{slot["role"].AsString()}  {ShortName(player.display_name)}\n{player.position}  •  {player.overall}";
        }
        UpdateSlotStyles();
    }

    private void UpdateSlotStyles()
    {
        foreach ((StringName slotId, Button button) in _slotButtons)
            button.Modulate = slotId == SelectedSlotId ? new Color("ffe08a") : Colors.White;
    }

    private void OnSlotPressed(StringName slotId)
    {
        SelectedSlotId = slotId;
        UpdateSlotStyles();
        EmitSignal(SignalName.SlotSelected, slotId);
    }

    private static string ShortName(string fullName)
    {
        string[] parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? fullName : parts[^1];
    }
}
