using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchSquad : Resource
{
    public const int MaxStarters = 11;
    public const int MaxSubstitutes = 12;

    [Export] public Array<StringName> starter_ids { get; set; } = new();
    [Export] public Array<StringName> substitute_ids { get; set; } = new();
    [Export] public StringName formation_id { get; set; } = new("4_3_3");
    [Export] public Dictionary starter_slots { get; set; } = new();

    public void clear()
    {
        starter_ids.Clear();
        substitute_ids.Clear();
        starter_slots.Clear();
    }

    public bool register_starter(StringName playerId)
    {
        if (starter_ids.Contains(playerId) || substitute_ids.Contains(playerId) || starter_ids.Count >= MaxStarters)
            return false;
        starter_ids.Add(playerId);
        return true;
    }

    public bool register_substitute(StringName playerId)
    {
        if (starter_ids.Contains(playerId) || substitute_ids.Contains(playerId) || substitute_ids.Count >= MaxSubstitutes)
            return false;
        substitute_ids.Add(playerId);
        return true;
    }

    public void unregister(StringName playerId)
    {
        starter_ids.Remove(playerId);
        substitute_ids.Remove(playerId);
        foreach (Variant slotKey in starter_slots.Keys)
        {
            if (starter_slots[slotKey].AsStringName() == playerId)
            {
                starter_slots.Remove(slotKey);
                break;
            }
        }
    }

    public bool is_registered(StringName playerId) =>
        starter_ids.Contains(playerId) || substitute_ids.Contains(playerId);

    public int registered_count() => starter_ids.Count + substitute_ids.Count;

    public StringName get_slot_for_player(StringName playerId)
    {
        foreach (Variant slotKey in starter_slots.Keys)
        {
            if (starter_slots[slotKey].AsStringName() == playerId)
                return slotKey.AsStringName();
        }
        return new StringName();
    }

    public void auto_select(Array<FootballPlayer> players)
    {
        clear();
        foreach (FootballPlayer player in players.OrderByDescending(player => player.overall))
        {
            if (starter_ids.Count < MaxStarters)
                register_starter(player.id);
            else if (substitute_ids.Count < MaxSubstitutes)
                register_substitute(player.id);
            else
                break;
        }
    }

    public string[] validate_against(Array<FootballPlayer> players)
    {
        var errors = new List<string>();
        if (starter_ids.Count != MaxStarters)
            errors.Add("Danh sách đá chính phải có đúng 11 cầu thủ.");
        if (substitute_ids.Count > MaxSubstitutes)
            errors.Add("Danh sách dự bị không được vượt quá 12 cầu thủ.");
        if (starter_slots.Count > 0 && starter_slots.Count != MaxStarters)
            errors.Add("Sân đấu phải có đủ 11 vị trí được xếp cầu thủ.");

        var validIds = players.Select(player => player.id).ToHashSet();
        var seenIds = new HashSet<StringName>();
        foreach (StringName playerId in starter_ids.Concat(substitute_ids))
        {
            if (!validIds.Contains(playerId))
                errors.Add($"Cầu thủ '{playerId}' không thuộc đội.");
            else if (!seenIds.Add(playerId))
                errors.Add($"Cầu thủ '{playerId}' bị đăng ký trùng.");
        }

        var assignedStarters = new HashSet<StringName>();
        foreach (Variant value in starter_slots.Values)
        {
            StringName playerId = value.AsStringName();
            if (!assignedStarters.Add(playerId))
                errors.Add("Một cầu thủ đang đứng ở nhiều vị trí trên sân.");
            if (!starter_ids.Contains(playerId))
                errors.Add("Cầu thủ trên sân chưa thuộc danh sách đá chính.");
        }
        foreach (StringName playerId in starter_ids)
        {
            if (starter_slots.Count > 0 && !assignedStarters.Contains(playerId))
                errors.Add("Một cầu thủ đá chính chưa được xếp vị trí.");
        }
        return errors.ToArray();
    }
}
