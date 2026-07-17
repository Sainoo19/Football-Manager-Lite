using Godot;

public sealed class SainooTeamFactory
{
    private sealed record PlayerSpec(
        string Id,
        string Name,
        string Position,
        string SlotId,
        int SquadNumber,
        int Age,
        string Nationality,
        int Overall);

    private static readonly PlayerSpec[] StartingEleven =
    [
        new("iker_casillas", "Iker Casillas", "GK", "gk", 1, 29, "Tây Ban Nha", 92),
        new("paolo_maldini", "Paolo Maldini", "LB", "lb", 3, 28, "Ý", 94),
        new("franz_beckenbauer", "Franz Beckenbauer", "CB", "lcb", 5, 29, "Đức", 95),
        new("tony_adams", "Tony Adams", "CB", "rcb", 6, 29, "Anh", 89),
        new("javier_zanetti", "Javier Zanetti", "RB", "rb", 4, 28, "Argentina", 92),
        new("frank_rijkaard", "Frank Rijkaard", "DM", "dm", 8, 28, "Hà Lan", 93),
        new("xabi_alonso", "Xabi Alonso", "CM", "cm", 14, 29, "Tây Ban Nha", 91),
        new("johan_cruyff", "Johan Cruyff", "AM", "am", 9, 27, "Hà Lan", 96),
        new("neymar", "Neymar", "LW", "lw", 11, 25, "Brazil", 93),
        new("cristiano_ronaldo", "Cristiano Ronaldo", "ST", "st", 7, 28, "Bồ Đào Nha", 96),
        new("lionel_messi", "Lionel Messi", "RW", "rw", 10, 25, "Argentina", 97)
    ];

    public FootballTeam Create(FormationDefinition formation)
    {
        FootballTeam team = new FootballTeam().setup(
            "sainoo_fc",
            "Sainoo FC",
            "SNF",
            "Quốc tế",
            new Color("111827"),
            new Color("e8b84a"));
        team.match_squad.clear();
        team.match_squad.formation_id = formation.id;

        foreach (PlayerSpec spec in StartingEleven)
        {
            FootballPlayer player = new FootballPlayer().setup(
                $"sainoo_fc_{spec.Id}",
                spec.Name,
                spec.Position,
                spec.Age,
                spec.Nationality,
                spec.Overall);
            player.SquadNumber = spec.SquadNumber;
            player.fitness = 100;
            player.form = 82;
            team.add_player(player);
            team.match_squad.register_starter(player.id);
            team.match_squad.starter_slots[new StringName(spec.SlotId)] = player.id;
        }

        return team;
    }
}
