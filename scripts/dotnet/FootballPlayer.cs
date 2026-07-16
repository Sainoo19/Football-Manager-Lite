using Godot;

[GlobalClass]
public partial class FootballPlayer : Resource
{
    [Export] public StringName id { get; set; } = new();
    [Export] public string display_name { get; set; } = "";
    [Export] public string position { get; set; } = "CM";
    [Export] public int age { get; set; } = 18;
    [Export] public string nationality { get; set; } = "";
    [Export(PropertyHint.Range, "1,99")] public int overall { get; set; } = 50;
    [Export(PropertyHint.Range, "0,100")] public int fitness { get; set; } = 100;
    [Export(PropertyHint.Range, "0,100")] public int form { get; set; } = 50;

    public FootballPlayer setup(
        StringName playerId,
        string playerName,
        string playerPosition,
        int playerAge,
        string playerNationality,
        int playerOverall)
    {
        id = playerId;
        display_name = playerName;
        position = playerPosition;
        age = playerAge;
        nationality = playerNationality;
        overall = Mathf.Clamp(playerOverall, 1, 99);
        return this;
    }
}
