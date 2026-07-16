using Godot;
using Godot.Collections;

[GlobalClass]
public partial class FootballTeam : Resource
{
    [Export] public StringName id { get; set; } = new();
    [Export] public string display_name { get; set; } = "";
    [Export] public string short_name { get; set; } = "";
    [Export] public string country { get; set; } = "";
    [Export] public Color primary_color { get; set; } = new("2457d6");
    [Export] public Color secondary_color { get; set; } = Colors.White;
    [Export] public Array<FootballPlayer> players { get; set; } = new();
    [Export] public MatchSquad match_squad { get; set; } = new();

    public FootballTeam setup(
        StringName teamId,
        string teamName,
        string teamShortName,
        string teamCountry,
        Color teamPrimaryColor,
        Color teamSecondaryColor)
    {
        id = teamId;
        display_name = teamName;
        short_name = teamShortName;
        country = teamCountry;
        primary_color = teamPrimaryColor;
        secondary_color = teamSecondaryColor;
        return this;
    }

    public void add_player(FootballPlayer player) => players.Add(player);

    public FootballPlayer? get_player(StringName playerId)
    {
        foreach (FootballPlayer player in players)
        {
            if (player.id == playerId)
                return player;
        }
        return null;
    }

    public int outside_match_squad_count() =>
        Mathf.Max(players.Count - match_squad.registered_count(), 0);
}
