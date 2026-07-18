using System.Collections.Generic;
using Godot;

public sealed class PlayerPositionInterpolator
{
    private const float MaximumInterpolatedTravelMeters = 3f;
    private const float PositionChangeToleranceSquared = 0.0000000001f;

    private readonly Dictionary<StringName, Vector2> _previousPositions = new();
    private readonly Dictionary<StringName, Vector2> _currentPositions = new();
    private bool _hasFrame;

    public void Reset(IReadOnlyDictionary<StringName, Vector2> positions)
    {
        System.ArgumentNullException.ThrowIfNull(positions);

        _previousPositions.Clear();
        _currentPositions.Clear();
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            _previousPositions[playerId] = position;
            _currentPositions[playerId] = position;
        }
        _hasFrame = true;
    }

    public bool Capture(IReadOnlyDictionary<StringName, Vector2> positions)
    {
        System.ArgumentNullException.ThrowIfNull(positions);

        if (!_hasFrame)
        {
            Reset(positions);
            return true;
        }
        if (!HasChanged(positions))
        {
            return false;
        }

        _previousPositions.Clear();
        foreach ((StringName playerId, Vector2 position) in _currentPositions)
        {
            _previousPositions[playerId] = position;
        }

        _currentPositions.Clear();
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            _currentPositions[playerId] = position;
            if (!_previousPositions.TryGetValue(playerId, out Vector2 previousPosition) ||
                FootballPitchDimensions.DistanceMeters(previousPosition, position) > MaximumInterpolatedTravelMeters)
            {
                _previousPositions[playerId] = position;
            }
        }
        return true;
    }

    public Vector2 Interpolate(StringName playerId, Vector2 fallbackPosition, float alpha)
    {
        if (!_currentPositions.TryGetValue(playerId, out Vector2 currentPosition) ||
            !_previousPositions.TryGetValue(playerId, out Vector2 previousPosition))
        {
            return fallbackPosition;
        }

        return previousPosition.Lerp(currentPosition, Mathf.Clamp(alpha, 0f, 1f));
    }

    private bool HasChanged(IReadOnlyDictionary<StringName, Vector2> positions)
    {
        if (positions.Count != _currentPositions.Count)
        {
            return true;
        }

        foreach ((StringName playerId, Vector2 position) in positions)
        {
            if (!_currentPositions.TryGetValue(playerId, out Vector2 currentPosition) ||
                currentPosition.DistanceSquaredTo(position) > PositionChangeToleranceSquared)
            {
                return true;
            }
        }
        return false;
    }
}
