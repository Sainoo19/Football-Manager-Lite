using Godot;

public sealed class ShotContactResolver
{
    // Includes arm extension during a dive, without moving the goalkeeper's body to the ball.
    public const float GoalkeeperReachMeters = 2.6f;
    public const float OutfieldReachMeters = 0.8f;
    private const float GoalkeeperRestitution = 0.50f;
    private const float DefenderRestitution = 0.62f;

    public bool TryContact(
        Vector2 ballFrom,
        Vector2 ballTo,
        Vector2 playerPosition,
        float reachMeters,
        out Vector2 contactPosition,
        out float progress)
    {
        Vector2 fromMeters = FootballPitchDimensions.ToMeters(ballFrom);
        Vector2 travelMeters = FootballPitchDimensions.ToMeters(ballTo) - fromMeters;
        Vector2 playerMeters = FootballPitchDimensions.ToMeters(playerPosition);
        Vector2 offset = fromMeters - playerMeters;
        float c = offset.LengthSquared() - reachMeters * reachMeters;
        progress = 0f;
        contactPosition = ballFrom;
        if (c <= 0f)
        {
            return true;
        }
        float a = travelMeters.LengthSquared();
        if (a <= 0.0001f)
        {
            return false;
        }
        float b = 2f * offset.Dot(travelMeters);
        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return false;
        }
        float entry = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
        if (entry < 0f || entry > 1f)
        {
            return false;
        }
        progress = entry;
        contactPosition = FootballPitchDimensions.ToNormalized(fromMeters + travelMeters * entry);
        return true;
    }

    public float LaneDistanceMeters(Vector2 ballFrom, Vector2 ballTo, Vector2 playerPosition)
    {
        Vector2 origin = FootballPitchDimensions.ToMeters(ballFrom);
        Vector2 travel = FootballPitchDimensions.ToMeters(ballTo) - origin;
        Vector2 player = FootballPitchDimensions.ToMeters(playerPosition);
        float progress = travel.LengthSquared() > 0.0001f
            ? Mathf.Clamp((player - origin).Dot(travel) / travel.LengthSquared(), 0f, 1f)
            : 0f;
        return player.DistanceTo(origin + travel * progress);
    }

    public Vector2 ReboundVelocity(
        Vector2 incomingVelocity,
        Vector2 contactPosition,
        Vector2 playerPosition,
        bool isGoalkeeper)
    {
        if (incomingVelocity.LengthSquared() <= 0.0001f)
        {
            return Vector2.Zero;
        }
        Vector2 offset = FootballPitchDimensions.ToMeters(contactPosition) -
                         FootballPitchDimensions.ToMeters(playerPosition);
        Vector2 normal = offset.LengthSquared() > 0.0001f
            ? offset.Normalized()
            : -incomingVelocity.Normalized();
        if (normal.Dot(incomingVelocity) > 0f)
        {
            normal = -normal;
        }
        Vector2 reflected = incomingVelocity - 2f * incomingVelocity.Dot(normal) * normal;
        return reflected * (isGoalkeeper ? GoalkeeperRestitution : DefenderRestitution);
    }
}
