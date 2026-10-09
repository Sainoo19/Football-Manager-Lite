using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed record LiveShotPlayer(string Id, string TeamId, string Role, Vector2 Position,
    Vector2 Target, string Assignment, string RelatedPlayerId);

public sealed record LiveShotRecord(
    double GameSeconds,
    string ShooterId,
    string TeamId,
    Vector2 Origin,
    Vector2 Destination,
    Vector2 GoalkeeperStart,
    float DistanceMeters,
    string Outcome,
    bool GoalkeeperAttemptedContact,
    float GoalkeeperContactDistanceMeters,
    Vector2 ReboundVelocity,
    string Situation = "",
    float? PressureMeters = null,
    int Finishing = 0,
    Vector2 GoalkeeperAtResolution = default,
    IReadOnlyList<LiveShotPlayer>? PlayersAtLaunch = null);

public sealed class LiveShotDiagnostics
{
    private readonly List<LiveShotRecord> _records = new();
    private readonly ReadOnlyCollection<LiveShotRecord> _view;

    public LiveShotDiagnostics() => _view = _records.AsReadOnly();
    public IReadOnlyList<LiveShotRecord> Records => _view;
    public void Reset() => _records.Clear();

    public void Record(LiveShotRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records.Add(record);
    }
}
