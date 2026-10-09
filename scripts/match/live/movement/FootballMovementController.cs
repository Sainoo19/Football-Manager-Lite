using System.Collections.Generic;
using Godot;

public sealed class FootballMovementController
{
    private const float NormalAccelerationMetersPerSecondSquared = 3.2f;
    private const float SprintAccelerationMetersPerSecondSquared = 4.2f;
    private const float ArrivalRadiusMeters = 0.8f;

    private readonly Dictionary<StringName, Vector2> _velocitiesMetersPerSecond = new();
    private readonly Dictionary<StringName, Vector2> _previousPositions = new();
    private readonly Dictionary<StringName, Vector2> _previousVelocities = new();
    private readonly List<StringName> _orderedPlayers = new();
    private readonly PlayerCollisionResolver _collisionResolver = new();

    public IReadOnlyDictionary<StringName, Vector2> VelocitiesMetersPerSecond => _velocitiesMetersPerSecond;

    public void Reset()
    {
        _velocitiesMetersPerSecond.Clear();
        _previousPositions.Clear();
        _previousVelocities.Clear();
        _orderedPlayers.Clear();
    }

    public void EnsurePlayer(StringName playerId)
    {
        if (!_velocitiesMetersPerSecond.ContainsKey(playerId))
        {
            _velocitiesMetersPerSecond[playerId] = Vector2.Zero;
        }
    }

    public void RemovePlayer(StringName playerId) => _velocitiesMetersPerSecond.Remove(playerId);

    public void Advance(
        IDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> targets,
        IReadOnlyDictionary<StringName, PlayerIntent> intents,
        IReadOnlyDictionary<StringName, int> paceRatings,
        float delta,
        IReadOnlySet<StringName>? recoverySprinters = null,
        float recoverySprintSpeedMetersPerSecond = 0f)
    {
        if (delta <= 0f)
        {
            return;
        }

        _orderedPlayers.Clear();
        _previousPositions.Clear();
        _previousVelocities.Clear();
        foreach ((StringName playerId, Vector2 position) in positions)
        {
            EnsurePlayer(playerId);
            _orderedPlayers.Add(playerId);
            _previousPositions[playerId] = position;
            _previousVelocities[playerId] = _velocitiesMetersPerSecond[playerId];
        }
        _orderedPlayers.Sort(FootballIntentPlanner.ComparePlayerIds);
        foreach (StringName playerId in _orderedPlayers)
        {
            if (!targets.TryGetValue(playerId, out Vector2 target))
            {
                continue;
            }

            EnsurePlayer(playerId);
            PlayerIntentKind intentKind = intents.TryGetValue(playerId, out PlayerIntent? intent)
                ? intent.Kind
                : PlayerIntentKind.HoldShape;
            int pace = paceRatings.TryGetValue(playerId, out int value) ? value : 50;
            bool recoverySprint = recoverySprinters?.Contains(playerId) == true;
            positions[playerId] = AdvancePlayer(
                positions[playerId],
                target,
                _velocitiesMetersPerSecond[playerId],
                intentKind,
                pace,
                recoverySprint ? recoverySprintSpeedMetersPerSecond : 0f,
                delta,
                playerId,
                out Vector2 updatedVelocity);
            _velocitiesMetersPerSecond[playerId] = updatedVelocity;
        }
        _collisionResolver.Resolve(positions, _previousPositions, _velocitiesMetersPerSecond, _orderedPlayers);
    }

    private Vector2 AdvancePlayer(
        Vector2 normalizedPosition,
        Vector2 normalizedTarget,
        Vector2 currentVelocity,
        PlayerIntentKind intentKind,
        int paceRating,
        float recoverySprintSpeedMetersPerSecond,
        float delta,
        StringName playerId,
        out Vector2 updatedVelocity)
    {
        Vector2 positionMeters = FootballPitchDimensions.ToMeters(normalizedPosition);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(normalizedTarget);
        Vector2 displacement = targetMeters - positionMeters;
        float distance = displacement.Length();
        if (distance <= 0.05f && currentVelocity.Length() <= NormalAccelerationMetersPerSecondSquared * delta)
        {
            updatedVelocity = currentVelocity.MoveToward(Vector2.Zero, NormalAccelerationMetersPerSecondSquared * delta);
            return PlayerPitchBoundary.Clamp(normalizedTarget);
        }

        bool recoverySprint = recoverySprintSpeedMetersPerSecond > 0f;
        float acceleration = recoverySprint || IsSprintIntent(intentKind)
            ? SprintAccelerationMetersPerSecondSquared
            : NormalAccelerationMetersPerSecondSquared;
        float maximumSpeed = MaximumSpeed(intentKind, paceRating, recoverySprintSpeedMetersPerSecond);
        float brakingSpeed = Mathf.Sqrt(2f * acceleration * distance);
        float desiredSpeed = Mathf.Min(maximumSpeed, brakingSpeed);
        if (distance < ArrivalRadiusMeters)
        {
            desiredSpeed *= distance / ArrivalRadiusMeters;
        }

        Vector2 desiredVelocity = displacement.Normalized() * desiredSpeed;
        desiredVelocity = _collisionResolver.AvoidPlayers(playerId, normalizedPosition, desiredVelocity,
            _previousPositions, _previousVelocities);
        updatedVelocity = currentVelocity.MoveToward(desiredVelocity, acceleration * delta);
        Vector2 step = updatedVelocity * delta;
        if (step.Dot(displacement) > 0f && step.Length() >= distance &&
            step.Normalized().Dot(displacement.Normalized()) > 0.99f)
        {
            updatedVelocity = Vector2.Zero;
            return PlayerPitchBoundary.Clamp(normalizedTarget);
        }

        return PlayerPitchBoundary.Clamp(
            FootballPitchDimensions.ToNormalized(positionMeters + step));
    }

    private static float MaximumSpeed(
        PlayerIntentKind intentKind,
        int paceRating,
        float recoverySprintSpeedMetersPerSecond)
    {
        float baseSpeed = intentKind switch
        {
            PlayerIntentKind.Goalkeep => 4.2f,
            PlayerIntentKind.HoldShape => 4.6f,
            PlayerIntentKind.CarryBall => 5.8f,
            PlayerIntentKind.DribbleCloseControl => 5.2f,
            PlayerIntentKind.DribbleKnockOn => 7.4f,
            PlayerIntentKind.DribbleChangeDirection => 5.0f,
            PlayerIntentKind.ShieldBall => 2.4f,
            PlayerIntentKind.HoldUpBall => 2.8f,
            PlayerIntentKind.SupportBall => 5.6f,
            PlayerIntentKind.CoverPress => 6.4f,
            PlayerIntentKind.BlockPassingLane => 6.0f,
            PlayerIntentKind.CloseDownBall => 7.4f,
            PlayerIntentKind.JockeyBall => 4.8f,
            PlayerIntentKind.ContainBall => 4.4f,
            PlayerIntentKind.TackleBall => 6.6f,
            PlayerIntentKind.ShoulderChallenge => 6.2f,
            PlayerIntentKind.MarkOpponent => 6.2f,
            PlayerIntentKind.ReceivePass => 7.2f,
            PlayerIntentKind.RunIntoSpace => 7.8f,
            PlayerIntentKind.PressBall => 8.0f,
            PlayerIntentKind.ChaseLooseBall => 8.2f,
            PlayerIntentKind.ContestAerialBall => 7.8f,
            PlayerIntentKind.ClaimAerialBall => 7.2f,
            PlayerIntentKind.RecoverGoalSide => 6.4f,
            PlayerIntentKind.RepositionForRestart => 6.0f,
            _ => 5f
        };
        baseSpeed = Mathf.Max(baseSpeed, recoverySprintSpeedMetersPerSecond);
        float paceFactor = Mathf.Lerp(0.82f, 1.10f, Mathf.Clamp(paceRating, 1, 99) / 99f);
        return baseSpeed * paceFactor;
    }

    private static bool IsSprintIntent(PlayerIntentKind intentKind) => intentKind is
        PlayerIntentKind.ReceivePass or
        PlayerIntentKind.RunIntoSpace or
        PlayerIntentKind.PressBall or
        PlayerIntentKind.CloseDownBall or
        PlayerIntentKind.DribbleKnockOn or
        PlayerIntentKind.ChaseLooseBall or
        PlayerIntentKind.ContestAerialBall or
        PlayerIntentKind.ClaimAerialBall;
}
