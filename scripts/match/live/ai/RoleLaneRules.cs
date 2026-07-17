using Godot;

public static class RoleLaneRules
{
    public static float ConstrainAttackingLane(
        string role,
        float lane,
        bool isFinalThird,
        float attackDirection)
    {
        return role switch
        {
            "LB" when attackDirection > 0f => Mathf.Clamp(lane, 0.07f, 0.46f),
            "LB" => Mathf.Clamp(lane, 0.54f, 0.93f),
            "RB" when attackDirection > 0f => Mathf.Clamp(lane, 0.54f, 0.93f),
            "RB" => Mathf.Clamp(lane, 0.07f, 0.46f),
            "LW" when !isFinalThird && attackDirection > 0f => Mathf.Clamp(lane, 0.08f, 0.44f),
            "LW" when !isFinalThird => Mathf.Clamp(lane, 0.56f, 0.92f),
            "RW" when !isFinalThird && attackDirection > 0f => Mathf.Clamp(lane, 0.56f, 0.92f),
            "RW" when !isFinalThird => Mathf.Clamp(lane, 0.08f, 0.44f),
            "LW" or "RW" => Mathf.Clamp(lane, 0.32f, 0.68f),
            "ST" => Mathf.Clamp(lane, 0.30f, 0.70f),
            "CM" or "AM" or "DM" => Mathf.Clamp(lane, 0.16f, 0.84f),
            _ => Mathf.Clamp(lane, 0.06f, 0.94f)
        };
    }
}
