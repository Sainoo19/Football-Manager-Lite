using System;
using Godot;

public readonly record struct MatchSpriteKitPalette(Color Shirt, Color Shorts, Color Socks)
{
    public static MatchSpriteKitPalette ForTeam(FootballTeam team, bool alternative)
    {
        ArgumentNullException.ThrowIfNull(team);
        return alternative
            ? new MatchSpriteKitPalette(team.secondary_color, team.primary_color, team.secondary_color)
            : new MatchSpriteKitPalette(team.primary_color, team.secondary_color, team.primary_color);
    }

    public static MatchSpriteKitPalette ForGoalkeeper(bool home)
    {
        Color shirt = home ? new Color("f1c75b") : new Color("ec9f45");
        return new MatchSpriteKitPalette(shirt, new Color("263444"), shirt);
    }

    public bool HasSimilarShirt(MatchSpriteKitPalette other)
    {
        Vector3 difference = new(Shirt.R - other.Shirt.R, Shirt.G - other.Shirt.G, Shirt.B - other.Shirt.B);
        return difference.LengthSquared() < 0.12f;
    }
}
