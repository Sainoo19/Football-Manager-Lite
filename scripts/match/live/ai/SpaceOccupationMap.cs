using System;
using System.Collections.Generic;
using Godot;

public readonly struct PitchSpaceZone : IEquatable<PitchSpaceZone>
{
    public PitchSpaceZone(int column, int lane)
    {
        Column = column;
        Lane = lane;
    }

    public int Column { get; }
    public int Lane { get; }
    public string Key => $"{Column}:{Lane}";

    public bool Equals(PitchSpaceZone other) => Column == other.Column && Lane == other.Lane;
    public override bool Equals(object? value) => value is PitchSpaceZone other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Column, Lane);
}

public sealed class SpaceOccupationMap
{
    public const int ColumnCount = 6;
    public const int LaneCount = 5;

    private readonly Dictionary<(StringName TeamId, PitchSpaceZone Zone), int> _occupation = new();

    public SpaceOccupationMap(FootballWorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach ((StringName playerId, Vector2 position) in world.Positions)
        {
            StringName teamId = world.PlayerTeams[playerId];
            PitchSpaceZone zone = ZoneFor(position);
            (StringName TeamId, PitchSpaceZone Zone) key = (teamId, zone);
            _occupation[key] = _occupation.GetValueOrDefault(key) + 1;
        }
    }

    public int Occupation(StringName teamId, Vector2 point)
    {
        return _occupation.GetValueOrDefault((teamId, ZoneFor(point)));
    }

    public float SpaceScore(StringName teamId, Vector2 point, FootballWorldSnapshot world)
    {
        int teammates = Occupation(teamId, point);
        float opponentDistance = SpaceEvaluator.NearestOpponentDistanceMeters(
            point,
            teamId,
            world.Positions,
            world.PlayerTeams);
        return Mathf.Clamp(opponentDistance / 12f, 0f, 1f) - Mathf.Max(teammates - 1, 0) * 0.28f;
    }

    public static PitchSpaceZone ZoneFor(Vector2 point)
    {
        int column = Mathf.Clamp(Mathf.FloorToInt(point.X * ColumnCount), 0, ColumnCount - 1);
        int lane = Mathf.Clamp(Mathf.FloorToInt(point.Y * LaneCount), 0, LaneCount - 1);
        return new PitchSpaceZone(column, lane);
    }
}
