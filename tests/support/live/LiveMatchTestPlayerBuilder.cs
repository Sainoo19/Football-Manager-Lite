using Godot;

public sealed class LiveMatchTestPlayerBuilder
{
    private readonly FootballPlayer _player;

    public LiveMatchTestPlayerBuilder(StringName id, string role, int overall = 70)
    {
        _player = new FootballPlayer().setup(
            id,
            id.ToString(),
            role,
            24,
            "Test",
            overall);
    }

    public LiveMatchTestPlayerBuilder WithTechnicalAttributes(
        int passing,
        int vision,
        int dribbling,
        int firstTouch,
        int technique,
        int composure)
    {
        _player.passing = passing;
        _player.vision = vision;
        _player.dribbling = dribbling;
        _player.FirstTouch = firstTouch;
        _player.Technique = technique;
        _player.Composure = composure;
        return this;
    }

    public LiveMatchTestPlayerBuilder WithPhysicalAttributes(
        int pace,
        int strength,
        int balance,
        int agility,
        int jumpingReach)
    {
        _player.pace = pace;
        _player.Strength = strength;
        _player.Balance = balance;
        _player.Agility = agility;
        _player.JumpingReach = jumpingReach;
        return this;
    }

    public LiveMatchTestPlayerBuilder WithDefensiveAttributes(int tackling, int positioning)
    {
        _player.tackling = tackling;
        _player.positioning = positioning;
        return this;
    }

    public FootballPlayer Build()
    {
        return _player;
    }
}
