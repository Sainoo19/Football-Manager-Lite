using System;
using Godot;

[GlobalClass]
public partial class FootballPlayer : Resource
{
    [Export] public StringName id { get; set; } = new();
    [Export] public string display_name { get; set; } = "";
    [Export] public string position { get; set; } = "CM";
    [Export] public int age { get; set; } = 18;
    [Export] public string nationality { get; set; } = "";
    [Export(PropertyHint.Range, "1,99")] public int SquadNumber { get; set; }
    [Export(PropertyHint.Range, "1,99")] public int overall { get; set; } = 50;
    [Export(PropertyHint.Range, "0,100")] public int fitness { get; set; } = 100;
    [Export(PropertyHint.Range, "0,100")] public int form { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int pace { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int passing { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int vision { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int dribbling { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int tackling { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int finishing { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int positioning { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int goalkeeping { get; set; } = 10;
    [Export(PropertyHint.Range, "1,99")] public int FirstTouch { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Technique { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Composure { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Strength { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Balance { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Agility { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int Heading { get; set; } = 50;
    [Export(PropertyHint.Range, "1,99")] public int JumpingReach { get; set; } = 50;

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
        ApplyRoleAttributes();
        return this;
    }

    private void ApplyRoleAttributes()
    {
        int variation = Math.Abs(id.GetHashCode()) % 9 - 4;
        pace = Attribute(overall + variation);
        passing = Attribute(overall + (position is "CM" or "AM" or "DM" ? 5 : 0) - (position == "GK" ? 12 : 0));
        vision = Attribute(overall + (position is "CM" or "AM" ? 7 : position == "DM" ? 3 : -2));
        dribbling = Attribute(overall + (position is "LW" or "RW" or "AM" ? 6 : position is "CB" or "GK" ? -9 : 0));
        tackling = Attribute(overall + (position is "CB" or "LB" or "RB" or "DM" ? 7 : position is "ST" or "LW" or "RW" ? -10 : 0));
        finishing = Attribute(overall + (position == "ST" ? 8 : position is "LW" or "RW" or "AM" ? 3 : -12));
        positioning = Attribute(overall + (position is "CB" or "DM" or "ST" ? 5 : 0));
        goalkeeping = position == "GK" ? Attribute(overall + 5) : Attribute(8 + variation);
        FirstTouch = Attribute(overall + (position is "CM" or "AM" or "LW" or "RW" or "ST" ? 3 : -2));
        Technique = Attribute(overall + (position is "CM" or "AM" or "LW" or "RW" ? 4 : position == "GK" ? -8 : 0));
        Composure = Attribute(overall + (position is "GK" or "CB" or "DM" or "ST" ? 3 : 0));
        Strength = Attribute(overall + (position is "CB" or "DM" or "ST" ? 5 : position is "LW" or "RW" ? -2 : 0));
        Balance = Attribute(overall + (position is "CM" or "AM" or "LW" or "RW" or "ST" ? 3 : 0));
        Agility = Attribute(overall + (position is "AM" or "LW" or "RW" ? 6 : position is "CB" or "GK" ? -5 : 1));
        Heading = Attribute(overall + (position is "CB" or "ST" ? 6 : position is "LW" or "RW" ? -2 : position == "GK" ? -12 : 0));
        JumpingReach = Attribute(overall + (position is "GK" or "CB" or "ST" ? 6 : position is "LW" or "RW" or "AM" ? -2 : 1));
    }

    private static int Attribute(int value) => Mathf.Clamp(value, 1, 99);
}
