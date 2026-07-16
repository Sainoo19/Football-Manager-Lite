using Godot;

[GlobalClass]
public partial class FootballMatchEvent : RefCounted
{
    public int minute { get; set; }
    public StringName event_type { get; set; } = new();
    public StringName team_id { get; set; } = new();
    public StringName player_id { get; set; } = new();
    public string text { get; set; } = "";

    public FootballMatchEvent setup(
        int eventMinute,
        StringName type,
        string eventText,
        StringName eventTeamId = null!,
        StringName eventPlayerId = null!)
    {
        minute = eventMinute;
        event_type = type;
        text = eventText;
        team_id = eventTeamId ?? new StringName();
        player_id = eventPlayerId ?? new StringName();
        return this;
    }
}
