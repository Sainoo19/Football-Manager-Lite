using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchTeamState : RefCounted
{
    public const int MaxSubstitutions = 5;

    public FootballTeam team { get; set; } = null!;
    public FormationDefinition formation { get; set; } = null!;
    public MatchSquad squad { get; set; } = null!;
    public StringName mentality { get; set; } = new("balanced");
    public int substitutions_used { get; set; }
    public Dictionary stats { get; set; } = new();

    public MatchTeamState setup(FootballTeam sourceTeam)
    {
        team = sourceTeam;
        formation = new FormationCatalog().find(team.match_squad.formation_id);
        squad = new MatchSquad
        {
            formation_id = team.match_squad.formation_id,
            starter_ids = new Array<StringName>(team.match_squad.starter_ids),
            substitute_ids = new Array<StringName>(team.match_squad.substitute_ids),
            starter_slots = team.match_squad.starter_slots.Duplicate(true)
        };
        stats = new Dictionary
        {
            { "goals", 0 },
            { "shots", 0 },
            { "shots_on_target", 0 },
            { "corners", 0 },
            { "fouls", 0 },
            { "yellow_cards", 0 },
            { "red_cards", 0 },
            { "possession_ticks", 0 }
        };
        return this;
    }

    public Dictionary strengths() =>
        new LineupManager().calculate_strengths(squad, formation, team.players);

    public bool make_substitution(StringName outgoingId, StringName incomingId)
    {
        if (substitutions_used >= MaxSubstitutions ||
            !squad.starter_ids.Contains(outgoingId) ||
            !squad.substitute_ids.Contains(incomingId))
            return false;

        StringName slotId = squad.get_slot_for_player(outgoingId);
        if (slotId == new StringName())
            return false;

        squad.starter_ids.Remove(outgoingId);
        squad.starter_ids.Add(incomingId);
        squad.substitute_ids.Remove(incomingId);
        squad.starter_slots[slotId] = incomingId;
        substitutions_used++;
        return true;
    }

    public bool send_off(StringName playerId)
    {
        if (!squad.starter_ids.Contains(playerId))
            return false;
        StringName slotId = squad.get_slot_for_player(playerId);
        squad.starter_ids.Remove(playerId);
        if (slotId != new StringName())
            squad.starter_slots.Remove(slotId);
        return true;
    }

    public Array<FootballPlayer> get_starter_players()
    {
        var result = new Array<FootballPlayer>();
        foreach (StringName playerId in squad.starter_ids)
        {
            FootballPlayer? player = team.get_player(playerId);
            if (player is not null)
                result.Add(player);
        }
        return result;
    }

    public Array<FootballPlayer> get_substitute_players()
    {
        var result = new Array<FootballPlayer>();
        foreach (StringName playerId in squad.substitute_ids)
        {
            FootballPlayer? player = team.get_player(playerId);
            if (player is not null)
                result.Add(player);
        }
        return result;
    }
}
