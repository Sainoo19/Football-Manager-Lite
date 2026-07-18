using Godot;

public enum DribbleTouchType
{
    CloseControl,
    KnockOn,
    ChangeDirection,
    Shield,
    HoldUp
}

public readonly struct DribbleTouchContext
{
    public DribbleTouchContext(
        Vector2 carrierPosition,
        Vector2 defenderPosition,
        float attackDirection,
        float pressureDistanceMeters,
        float carrierSpeedMetersPerSecond,
        int dribbling,
        int technique,
        int pace,
        int strength,
        int balance,
        int agility,
        bool isBackToGoal,
        bool hasSupportOption,
        int previousTouchCount,
        float decisionRoll)
    {
        CarrierPosition = carrierPosition;
        DefenderPosition = defenderPosition;
        AttackDirection = attackDirection;
        PressureDistanceMeters = pressureDistanceMeters;
        CarrierSpeedMetersPerSecond = carrierSpeedMetersPerSecond;
        Dribbling = dribbling;
        Technique = technique;
        Pace = pace;
        Strength = strength;
        Balance = balance;
        Agility = agility;
        IsBackToGoal = isBackToGoal;
        HasSupportOption = hasSupportOption;
        PreviousTouchCount = previousTouchCount;
        DecisionRoll = decisionRoll;
    }

    public Vector2 CarrierPosition { get; }
    public Vector2 DefenderPosition { get; }
    public float AttackDirection { get; }
    public float PressureDistanceMeters { get; }
    public float CarrierSpeedMetersPerSecond { get; }
    public int Dribbling { get; }
    public int Technique { get; }
    public int Pace { get; }
    public int Strength { get; }
    public int Balance { get; }
    public int Agility { get; }
    public bool IsBackToGoal { get; }
    public bool HasSupportOption { get; }
    public int PreviousTouchCount { get; }
    public float DecisionRoll { get; }
}

public readonly struct DribbleTouchPlan
{
    public DribbleTouchPlan(
        DribbleTouchType type,
        Vector2 target,
        float ballLeadMeters,
        float durationSeconds,
        float exposure)
    {
        Type = type;
        Target = target;
        BallLeadMeters = ballLeadMeters;
        DurationSeconds = durationSeconds;
        Exposure = exposure;
    }

    public DribbleTouchType Type { get; }
    public Vector2 Target { get; }
    public float BallLeadMeters { get; }
    public float DurationSeconds { get; }
    public float Exposure { get; }
}

public sealed class DribbleTouchPlanner
{
    public DribbleTouchPlan Plan(DribbleTouchContext context)
    {
        DribbleTouchType type = SelectTouch(context);
        Vector2 forward = new(context.AttackDirection, 0f);
        Vector2 lateral = LateralEscapeDirection(context);
        Vector2 displacementMeters = type switch
        {
            DribbleTouchType.KnockOn => forward * Mathf.Lerp(4.6f, 6.4f, context.Pace / 99f) + lateral * 0.5f,
            DribbleTouchType.ChangeDirection => forward * 2.2f + lateral * Mathf.Lerp(2.8f, 4.2f, context.Agility / 99f),
            DribbleTouchType.Shield => forward * -0.45f + lateral * 1.15f,
            DribbleTouchType.HoldUp => forward * -0.25f + lateral * 0.65f,
            _ => forward * 2.0f + lateral * 0.45f
        };
        Vector2 target = SpaceEvaluator.ClampToPitch(
            context.CarrierPosition + FootballPitchDimensions.ToNormalized(displacementMeters));
        float leadMeters = type switch
        {
            DribbleTouchType.KnockOn => 2.8f,
            DribbleTouchType.ChangeDirection => 1.25f,
            DribbleTouchType.Shield => 0.55f,
            DribbleTouchType.HoldUp => 0.48f,
            _ => 0.75f
        };
        float durationSeconds = type switch
        {
            DribbleTouchType.KnockOn => 0.62f,
            DribbleTouchType.ChangeDirection => 0.52f,
            DribbleTouchType.Shield => 0.48f,
            DribbleTouchType.HoldUp => 0.58f,
            _ => 0.44f
        };
        float exposure = type switch
        {
            DribbleTouchType.KnockOn => 0.92f,
            DribbleTouchType.ChangeDirection => 0.52f,
            DribbleTouchType.Shield => 0.16f,
            DribbleTouchType.HoldUp => 0.20f,
            _ => 0.34f
        };
        return new DribbleTouchPlan(type, target, leadMeters, durationSeconds, exposure);
    }

    private static DribbleTouchType SelectTouch(DribbleTouchContext context)
    {
        if (context.IsBackToGoal)
        {
            return context.PressureDistanceMeters <= 2.4f
                ? DribbleTouchType.Shield
                : DribbleTouchType.HoldUp;
        }

        if (context.PressureDistanceMeters >= 4.2f &&
            context.CarrierSpeedMetersPerSecond >= 2.5f &&
            context.Pace + context.Agility >= 132 &&
            context.DecisionRoll < 0.68f)
        {
            return DribbleTouchType.KnockOn;
        }

        bool defenderBlocksForwardPath = context.AttackDirection *
            (context.DefenderPosition.X - context.CarrierPosition.X) > 0f;
        if (context.PressureDistanceMeters <= 2.5f &&
            (defenderBlocksForwardPath || context.DecisionRoll < 0.62f))
        {
            return context.Agility + context.Dribbling >= context.Strength + context.Balance
                ? DribbleTouchType.ChangeDirection
                : DribbleTouchType.Shield;
        }

        if (context.HasSupportOption && context.PreviousTouchCount >= 3 && context.DecisionRoll > 0.72f)
        {
            return DribbleTouchType.HoldUp;
        }

        return DribbleTouchType.CloseControl;
    }

    private static Vector2 LateralEscapeDirection(DribbleTouchContext context)
    {
        float awayFromDefender = context.DefenderPosition.Y >= context.CarrierPosition.Y ? -1f : 1f;
        if (Mathf.IsEqualApprox(context.DefenderPosition.Y, context.CarrierPosition.Y))
        {
            awayFromDefender = context.DecisionRoll < 0.5f ? -1f : 1f;
        }
        if (context.CarrierPosition.Y < 0.10f)
        {
            awayFromDefender = 1f;
        }
        else if (context.CarrierPosition.Y > 0.90f)
        {
            awayFromDefender = -1f;
        }
        return new Vector2(0f, awayFromDefender);
    }
}
