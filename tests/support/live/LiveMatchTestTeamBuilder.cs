using Godot;

public sealed class LiveMatchTestTeamBuilder
{
    private static readonly string[] StandardRoles =
    {
        "GK", "LB", "CB", "CB", "RB", "DM", "CM", "AM", "LW", "ST", "RW"
    };

    private readonly FootballTeam _team;
    private int _playerSerial;

    public LiveMatchTestTeamBuilder(StringName teamId)
    {
        _team = new FootballTeam().setup(
            teamId,
            $"{teamId} Test Team",
            teamId.ToString().ToUpperInvariant(),
            "Test",
            Colors.Blue,
            Colors.White);
    }

    public LiveMatchTestTeamBuilder AddPlayer(FootballPlayer player)
    {
        _team.add_player(player);
        return this;
    }

    public LiveMatchTestTeamBuilder AddPlayer(string role, int overall = 70)
    {
        StringName playerId = $"{_team.id}_{role.ToLowerInvariant()}_{_playerSerial++:00}";
        return AddPlayer(new LiveMatchTestPlayerBuilder(playerId, role, overall).Build());
    }

    public LiveMatchTestTeamBuilder AddStandardEleven(int overall = 70)
    {
        foreach (string role in StandardRoles)
        {
            AddPlayer(role, overall);
        }
        return this;
    }

    public FootballTeam Build(string formationId = "4_3_3")
    {
        _team.match_squad.formation_id = formationId;
        FormationDefinition formation = new FormationCatalog().find(formationId);
        new LineupManager().auto_build(_team.match_squad, formation, _team.players);
        return _team;
    }
}
