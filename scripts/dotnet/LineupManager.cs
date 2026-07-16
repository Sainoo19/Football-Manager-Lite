using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class LineupManager : RefCounted
{
    private static readonly HashSet<string> Defenders = ["CB", "LB", "RB"];
    private static readonly HashSet<string> Midfielders = ["DM", "CM", "AM"];
    private static readonly HashSet<string> Attackers = ["LW", "RW", "ST"];

    public void auto_build(MatchSquad squad, FormationDefinition formation, Array<FootballPlayer> players)
    {
        squad.clear();
        squad.formation_id = formation.id;
        var available = players.ToList();
        foreach (Dictionary slot in formation.slots)
        {
            FootballPlayer? player = BestPlayerForRole(available, slot["role"].AsString());
            if (player is null)
                continue;
            squad.starter_ids.Add(player.id);
            squad.starter_slots[slot["id"].AsStringName()] = player.id;
            available.Remove(player);
        }

        foreach (FootballPlayer player in available.OrderByDescending(player => player.overall).Take(MatchSquad.MaxSubstitutes))
            squad.substitute_ids.Add(player.id);
    }

    public bool assign_player_to_slot(
        MatchSquad squad,
        FormationDefinition formation,
        StringName playerId,
        StringName targetSlotId)
    {
        if (!formation.has_slot(targetSlotId))
            return false;

        StringName currentSlotId = squad.get_slot_for_player(playerId);
        StringName outgoingId = squad.starter_slots.TryGetValue(targetSlotId, out Variant outgoing)
            ? outgoing.AsStringName()
            : new StringName();
        if (currentSlotId == targetSlotId)
            return true;

        if (currentSlotId != new StringName())
        {
            squad.starter_slots[targetSlotId] = playerId;
            if (outgoingId != new StringName())
                squad.starter_slots[currentSlotId] = outgoingId;
            else
                squad.starter_slots.Remove(currentSlotId);
            return true;
        }

        squad.substitute_ids.Remove(playerId);
        if (outgoingId != new StringName())
        {
            squad.starter_ids.Remove(outgoingId);
            if (squad.substitute_ids.Count < MatchSquad.MaxSubstitutes)
                squad.substitute_ids.Add(outgoingId);
        }
        if (!squad.starter_ids.Contains(playerId))
            squad.starter_ids.Add(playerId);
        squad.starter_slots[targetSlotId] = playerId;
        return true;
    }

    public bool toggle_substitute(MatchSquad squad, StringName playerId)
    {
        if (squad.starter_ids.Contains(playerId))
            return false;
        if (squad.substitute_ids.Contains(playerId))
        {
            squad.substitute_ids.Remove(playerId);
            return true;
        }
        if (squad.substitute_ids.Count >= MatchSquad.MaxSubstitutes)
            return false;
        squad.substitute_ids.Add(playerId);
        return true;
    }

    public int position_fit(string playerPosition, string role)
    {
        if (playerPosition == role)
            return 100;
        if (Defenders.Contains(playerPosition) && Defenders.Contains(role))
            return 72;
        if (Midfielders.Contains(playerPosition) && Midfielders.Contains(role))
            return 78;
        if (Attackers.Contains(playerPosition) && Attackers.Contains(role))
            return 76;
        if ((Midfielders.Contains(playerPosition) && Attackers.Contains(role)) ||
            (Attackers.Contains(playerPosition) && Midfielders.Contains(role)))
            return 48;
        return 20;
    }

    public Dictionary calculate_strengths(
        MatchSquad squad,
        FormationDefinition formation,
        Array<FootballPlayer> players)
    {
        var totals = new System.Collections.Generic.Dictionary<string, float>
        {
            ["defense"] = 0,
            ["midfield"] = 0,
            ["attack"] = 0
        };
        var counts = new System.Collections.Generic.Dictionary<string, int>
        {
            ["defense"] = 0,
            ["midfield"] = 0,
            ["attack"] = 0
        };

        foreach (Dictionary slot in formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!squad.starter_slots.TryGetValue(slotId, out Variant value))
                continue;
            FootballPlayer? player = FindPlayer(players, value.AsStringName());
            if (player is null)
                continue;
            string role = slot["role"].AsString();
            string line = LineForRole(role);
            float fitMultiplier = Mathf.Lerp(0.72f, 1.0f, position_fit(player.position, role) / 100.0f);
            totals[line] += player.overall * fitMultiplier;
            counts[line]++;
        }

        return new Dictionary
        {
            { "defense", Mathf.RoundToInt(totals["defense"] / Mathf.Max(counts["defense"], 1)) },
            { "midfield", Mathf.RoundToInt(totals["midfield"] / Mathf.Max(counts["midfield"], 1)) },
            { "attack", Mathf.RoundToInt(totals["attack"] / Mathf.Max(counts["attack"], 1)) }
        };
    }

    private FootballPlayer? BestPlayerForRole(List<FootballPlayer> players, string role) =>
        players.MaxBy(player => player.overall + position_fit(player.position, role) * 0.24f);

    private static FootballPlayer? FindPlayer(Array<FootballPlayer> players, StringName playerId) =>
        players.FirstOrDefault(player => player.id == playerId);

    private static string LineForRole(string role)
    {
        if (role is "GK" or "CB" or "LB" or "RB")
            return "defense";
        if (role is "DM" or "CM" or "AM")
            return "midfield";
        return "attack";
    }
}
