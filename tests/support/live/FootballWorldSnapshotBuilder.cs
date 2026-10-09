using System.Collections.Generic;
using Godot;

public sealed class FootballWorldSnapshotBuilder
{
    private readonly Dictionary<StringName, Vector2> _positions = new();
    private readonly Dictionary<StringName, Vector2> _basePositions = new();
    private readonly Dictionary<StringName, StringName> _teams = new();
    private readonly Dictionary<StringName, string> _roles = new();
    private Vector2 _ballPosition = new(0.5f, 0.5f);
    private StringName _ballOwnerId = new();
    private StringName _possessionTeamId = new("home");
    private StringName _homeTeamId = new("home");
    private float _gameTimeSeconds;

    public FootballWorldSnapshotBuilder AddPlayer(
        StringName playerId,
        StringName teamId,
        string role,
        Vector2 position)
    {
        _positions[playerId] = position;
        _basePositions[playerId] = position;
        _teams[playerId] = teamId;
        _roles[playerId] = role;
        return this;
    }

    public FootballWorldSnapshotBuilder WithPossession(
        StringName teamId,
        StringName ownerId,
        Vector2 ballPosition)
    {
        _possessionTeamId = teamId;
        _ballOwnerId = ownerId;
        _ballPosition = ballPosition;
        return this;
    }

    public FootballWorldSnapshotBuilder WithHomeTeam(StringName homeTeamId)
    {
        _homeTeamId = homeTeamId;
        return this;
    }

    public FootballWorldSnapshotBuilder AtTime(float gameTimeSeconds)
    {
        _gameTimeSeconds = gameTimeSeconds;
        return this;
    }

    public FootballWorldSnapshot Build()
    {
        return new FootballWorldSnapshot(
            _positions,
            _basePositions,
            _teams,
            _roles,
            _ballPosition,
            _ballPosition,
            _ballOwnerId,
            new StringName(),
            _possessionTeamId,
            _homeTeamId,
            false,
            false,
            true,
            false,
            false,
            null,
            null,
            _gameTimeSeconds);
    }
}
