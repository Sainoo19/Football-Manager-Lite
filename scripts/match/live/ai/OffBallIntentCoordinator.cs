using System;
using System.Collections.Generic;
using Godot;

public sealed class OffBallIntentCoordinator
{
    private readonly FootballIntentPlanner _planner;

    public OffBallIntentCoordinator(FootballIntentPlanner planner)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public Dictionary<StringName, PlayerIntent> Plan(FootballWorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return _planner.Plan(world);
    }
}
