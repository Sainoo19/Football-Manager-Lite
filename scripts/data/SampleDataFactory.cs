using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class SampleDataFactory : RefCounted
{
    private static readonly string[] Positions =
    [
        "GK", "GK", "GK", "RB", "RB", "CB", "CB", "CB", "CB", "LB", "LB",
        "DM", "DM", "CM", "CM", "CM", "AM", "AM", "RW", "RW", "LW", "LW",
        "ST", "ST", "ST"
    ];

    private static readonly string[] FirstNames =
    [
        "Minh", "Huy", "Nam", "Duy", "Khang", "Phong", "Tuấn", "Đức",
        "Quang", "Long", "Khôi", "Sơn", "Bảo", "Thành", "Trung", "Vũ"
    ];

    private static readonly string[] LastNames =
    [
        "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ",
        "Ngô", "Dương", "Lý", "Phan", "Mai", "Đinh", "Tạ", "Cao", "Châu", "Tô"
    ];

    private sealed record TeamSpec(
        string Id,
        string Name,
        string Short,
        string Country,
        Color Primary,
        Color Secondary,
        int PlayerCount,
        int BaseRating);

    public Array<FootballTeam> create_teams()
    {
        TeamSpec[] specs =
        [
            new("saigon_dragons", "Sài Gòn Dragons", "SGD", "Việt Nam", new Color("e23b3b"), new Color("f5c451"), 28, 72),
            new("hanoi_guardians", "Hà Nội Guardians", "HNG", "Việt Nam", new Color("3559d8"), new Color("f4f7ff"), 31, 74),
            new("danang_waves", "Đà Nẵng Waves", "DNW", "Việt Nam", new Color("14a384"), new Color("e6fff8"), 25, 70),
            new("mekong_stars", "Mekong Stars", "MKS", "Việt Nam", new Color("8f4bd8"), new Color("fff0c2"), 34, 68)
        ];

        var teams = new Array<FootballTeam>();
        var catalog = new FormationCatalog();
        var lineupManager = new LineupManager();
        for (int teamIndex = 0; teamIndex < specs.Length; teamIndex++)
        {
            TeamSpec spec = specs[teamIndex];
            var team = new FootballTeam().setup(
                spec.Id, spec.Name, spec.Short, spec.Country, spec.Primary, spec.Secondary);
            AddPlayers(team, spec.PlayerCount, spec.BaseRating, teamIndex);
            lineupManager.auto_build(team.match_squad, catalog.find("4_3_3"), team.players);
            teams.Add(team);
        }
        return teams;
    }

    private static void AddPlayers(FootballTeam team, int playerCount, int baseRating, int teamIndex)
    {
        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            string firstName = FirstNames[(playerIndex * 3 + teamIndex) % FirstNames.Length];
            string lastName = LastNames[(playerIndex + teamIndex * 5) % LastNames.Length];
            string playerName = $"{lastName} {firstName}";
            string position = Positions[playerIndex % Positions.Length];
            int ratingOffset = (playerIndex * 7 + teamIndex * 3) % 13 - 6;
            var player = new FootballPlayer().setup(
                $"{team.id}_p{playerIndex + 1:00}",
                playerName,
                position,
                18 + (playerIndex * 5 + teamIndex) % 18,
                "Việt Nam",
                baseRating + ratingOffset);
            player.fitness = 82 + playerIndex * 3 % 19;
            player.form = 42 + playerIndex * 11 % 38;
            team.add_player(player);
        }
    }
}
