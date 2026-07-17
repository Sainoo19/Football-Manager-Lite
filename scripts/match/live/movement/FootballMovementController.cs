using System.Collections.Generic;
using Godot;

public sealed class FootballMovementController
{
    private const float NormalAccelerationMetersPerSecondSquared = 3.2f;
    private const float SprintAccelerationMetersPerSecondSquared = 4.2f;
    private const float ArrivalRadiusMeters = 0.8f;

    private readonly Dictionary<StringName, Vector2> _velocitiesMetersPerSecond = new();

    public IReadOnlyDictionary<StringName, Vector2> VelocitiesMetersPerSecond => _velocitiesMetersPerSecond;

    public void Reset() => _velocitiesMetersPerSecond.Clear();

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
        float delta)
    {
        if (delta <= 0f)
        {
            return;
        }

        foreach (StringName playerId in positions.Keys)
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
            positions[playerId] = AdvancePlayer(
                positions[playerId],
                target,
                _velocitiesMetersPerSecond[playerId],
                intentKind,
                pace,
                delta,
                out Vector2 updatedVelocity);
            _velocitiesMetersPerSecond[playerId] = updatedVelocity;
        }
    }

    private static Vector2 AdvancePlayer(
        Vector2 normalizedPosition,
        Vector2 normalizedTarget,
        Vector2 currentVelocity,
        PlayerIntentKind intentKind,
        int paceRating,
        float delta,
        out Vector2 updatedVelocity)
    {
        Vector2 positionMeters = FootballPitchDimensions.ToMeters(normalizedPosition);
        Vector2 targetMeters = FootballPitchDimensions.ToMeters(normalizedTarget);
        Vector2 displacement = targetMeters - positionMeters;
        float distance = displacement.Length();
        if (distance <= 0.05f)
        {
            updatedVelocity = currentVelocity.MoveToward(Vector2.Zero, NormalAccelerationMetersPerSecondSquared * delta);
            return normalizedTarget;
        }

        float acceleration = IsSprintIntent(intentKind)
            ? SprintAccelerationMetersPerSecondSquared
            : NormalAccelerationMetersPerSecondSquared;
        float maximumSpeed = MaximumSpeed(intentKind, paceRating);
        float brakingSpeed = Mathf.Sqrt(2f * acceleration * distance);
        float desiredSpeed = Mathf.Min(maximumSpeed, brakingSpeed);
        if (distance < ArrivalRadiusMeters)
        {
            desiredSpeed *= distance / ArrivalRadiusMeters;
        }

        Vector2 desiredVelocity = displacement.Normalized() * desiredSpeed;
        updatedVelocity = currentVelocity.MoveToward(desiredVelocity, acceleration * delta);
        Vector2 step = updatedVelocity * delta;
        if (step.Length() > distance)
        {
            updatedVelocity = Vector2.Zero;
            return normalizedTarget;
        }

        return SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(positionMeters + step));
    }

    private static float MaximumSpeed(PlayerIntentKind intentKind, int paceRating)
    {
        float baseSpeed = intentKind switch
        {
            PlayerIntentKind.Goalkeep => 4.2f,
            PlayerIntentKind.HoldShape => 4.6f,
            PlayerIntentKind.CarryBall => 5.8f,
            PlayerIntentKind.SupportBall => 5.6f,
            PlayerIntentKind.CoverPress => 6.4f,
            PlayerIntentKind.MarkOpponent => 6.2f,
            PlayerIntentKind.ReceivePass => 7.2f,
            PlayerIntentKind.RunIntoSpace => 7.8f,
            PlayerIntentKind.PressBall => 8.0f,
            PlayerIntentKind.ChaseLooseBall => 8.2f,
            PlayerIntentKind.RepositionForRestart => 6.0f,
            _ => 5f
        };
        float paceFactor = Mathf.Lerp(0.82f, 1.10f, Mathf.Clamp(paceRating, 1, 99) / 99f);
        return baseSpeed * paceFactor;
    }

    private static bool IsSprintIntent(PlayerIntentKind intentKind) => intentKind is
        PlayerIntentKind.ReceivePass or
        PlayerIntentKind.RunIntoSpace or
        PlayerIntentKind.PressBall or
        PlayerIntentKind.ChaseLooseBall;
}
