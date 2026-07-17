using Godot;

public sealed class ShotDecisionEvaluator
{
    public const float MaximumShootingDistanceMeters = 33f;

    public bool ShouldShoot(
        string role,
        int finishing,
        float distanceMeters,
        float pressureDistanceMeters,
        int decisionsSinceShot,
        float decisionRoll)
    {
        if (role is "GK" or "CB" or "LB" or "RB" or "DM" ||
            distanceMeters > MaximumShootingDistanceMeters)
        {
            return false;
        }

        float chance = distanceMeters switch
        {
            <= 12f => 0.48f,
            <= 18f => 0.34f,
            <= 24f => 0.24f,
            <= 29f => 0.10f,
            _ => 0.025f
        };
        chance += (finishing - 65) / 260f;
        if (role == "ST")
        {
            chance += 0.08f;
        }
        if (pressureDistanceMeters < 2.2f)
        {
            chance -= 0.10f;
        }
        if (decisionsSinceShot >= 12 && distanceMeters <= 24f)
        {
            chance += 0.18f;
        }
        else if (decisionsSinceShot >= 20 && distanceMeters <= 30f)
        {
            chance += 0.14f;
        }

        return decisionRoll < Mathf.Clamp(chance, 0.01f, 0.68f);
    }
}
