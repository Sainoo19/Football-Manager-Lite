using Godot;

public enum DefenderEngagementType
{
    CloseDown,
    Jockey,
    Contain,
    Tackle,
    ShoulderChallenge,
    Recover
}

public readonly struct DefenderEngagementContext
{
    public DefenderEngagementContext(
        Vector2 defenderPosition,
        Vector2 carrierPosition,
        Vector2 ownGoal,
        float distanceMeters,
        DribbleTouchType touchType,
        int exchangeCount,
        int tackling,
        int positioning,
        int strength,
        int balance,
        float carrierSpeedMetersPerSecond,
        float defenderSpeedMetersPerSecond,
        bool isChallengeOnCooldown,
        bool isCarrierBackToGoal,
        bool hasCover,
        float decisionRoll,
        bool isInsideOwnPenaltyArea = false,
        float penaltyAreaChallengeProbability = 0.18f,
        bool hasYellowCard = false,
        float bookedPlayerChallengeProbability = 1f,
        float distanceToOwnGoalMeters = float.PositiveInfinity,
        bool isGoalkeeper = false)
    {
        DefenderPosition = defenderPosition;
        CarrierPosition = carrierPosition;
        OwnGoal = ownGoal;
        DistanceMeters = distanceMeters;
        TouchType = touchType;
        ExchangeCount = exchangeCount;
        Tackling = tackling;
        Positioning = positioning;
        Strength = strength;
        Balance = balance;
        CarrierSpeedMetersPerSecond = carrierSpeedMetersPerSecond;
        DefenderSpeedMetersPerSecond = defenderSpeedMetersPerSecond;
        IsChallengeOnCooldown = isChallengeOnCooldown;
        IsCarrierBackToGoal = isCarrierBackToGoal;
        HasCover = hasCover;
        DecisionRoll = decisionRoll;
        IsInsideOwnPenaltyArea = isInsideOwnPenaltyArea;
        PenaltyAreaChallengeProbability = penaltyAreaChallengeProbability;
        HasYellowCard = hasYellowCard;
        BookedPlayerChallengeProbability = bookedPlayerChallengeProbability;
        DistanceToOwnGoalMeters = distanceToOwnGoalMeters;
        IsGoalkeeper = isGoalkeeper;
    }

    public Vector2 DefenderPosition { get; }
    public Vector2 CarrierPosition { get; }
    public Vector2 OwnGoal { get; }
    public float DistanceMeters { get; }
    public DribbleTouchType TouchType { get; }
    public int ExchangeCount { get; }
    public int Tackling { get; }
    public int Positioning { get; }
    public int Strength { get; }
    public int Balance { get; }
    public float CarrierSpeedMetersPerSecond { get; }
    public float DefenderSpeedMetersPerSecond { get; }
    public bool IsChallengeOnCooldown { get; }
    public bool IsCarrierBackToGoal { get; }
    public bool HasCover { get; }
    public float DecisionRoll { get; }
    public bool IsInsideOwnPenaltyArea { get; }
    public float PenaltyAreaChallengeProbability { get; }
    public bool HasYellowCard { get; }
    public float BookedPlayerChallengeProbability { get; }
    public float DistanceToOwnGoalMeters { get; }
    public bool IsGoalkeeper { get; }
}

public readonly struct DefenderEngagementPlan
{
    public DefenderEngagementPlan(DefenderEngagementType type, Vector2 target, bool attemptsChallenge)
    {
        Type = type;
        Target = target;
        AttemptsChallenge = attemptsChallenge;
    }

    public DefenderEngagementType Type { get; }
    public Vector2 Target { get; }
    public bool AttemptsChallenge { get; }
}

public sealed class DefenderEngagementPlanner
{
    public DefenderEngagementPlan Plan(DefenderEngagementContext context)
    {
        DefenderEngagementType type = SelectEngagement(context);
        float desiredDistanceMeters = type switch
        {
            DefenderEngagementType.CloseDown => 1.35f,
            DefenderEngagementType.Jockey => 1.55f,
            DefenderEngagementType.Contain => 1.85f,
            DefenderEngagementType.Recover => 2.05f,
            DefenderEngagementType.ShoulderChallenge => 0.58f,
            _ => 0.42f
        };
        Vector2 target = GoalSideTarget(context, desiredDistanceMeters);
        bool attemptsChallenge = type is DefenderEngagementType.Tackle or DefenderEngagementType.ShoulderChallenge;
        return new DefenderEngagementPlan(type, target, attemptsChallenge);
    }

    private static DefenderEngagementType SelectEngagement(DefenderEngagementContext context)
    {
        if (context.IsChallengeOnCooldown)
        {
            return DefenderEngagementType.Recover;
        }
        if (context.IsGoalkeeper)
        {
            if (context.DistanceMeters > 2.2f)
            {
                return DefenderEngagementType.CloseDown;
            }
            if (context.DistanceMeters <= 1.45f)
            {
                return DefenderEngagementType.Tackle;
            }

            return context.ExchangeCount < 1
                ? DefenderEngagementType.Jockey
                : DefenderEngagementType.Tackle;
        }
        if (context.DistanceMeters > 3.0f)
        {
            return DefenderEngagementType.CloseDown;
        }
        if (context.ExchangeCount < 2)
        {
            return DefenderEngagementType.Jockey;
        }
        if (context.IsInsideOwnPenaltyArea &&
            context.DecisionRoll >= PenaltyAreaChallengeChance(context))
        {
            return context.DistanceMeters <= 1.55f
                ? DefenderEngagementType.Contain
                : DefenderEngagementType.Jockey;
        }
        if (context.HasYellowCard &&
            context.DecisionRoll >= context.BookedPlayerChallengeProbability)
        {
            return context.DistanceMeters <= 1.55f
                ? DefenderEngagementType.Contain
                : DefenderEngagementType.Jockey;
        }
        if (context.IsCarrierBackToGoal &&
            context.DistanceMeters <= 1.65f &&
            context.Strength + context.Balance >= 112 &&
            context.DecisionRoll < 0.58f)
        {
            return DefenderEngagementType.ShoulderChallenge;
        }

        float tackleIntent = 0.18f +
                             (context.Tackling - 55) / 180f +
                             (context.Positioning - 55) / 260f +
                             (context.HasCover ? 0.10f : 0f) +
                             (context.TouchType == DribbleTouchType.KnockOn ? 0.32f : 0f);
        if (context.DistanceMeters <= 1.65f && context.DecisionRoll < Mathf.Clamp(tackleIntent, 0.12f, 0.68f))
        {
            return DefenderEngagementType.Tackle;
        }

        return context.DistanceMeters <= 1.55f
            ? DefenderEngagementType.Contain
            : DefenderEngagementType.Jockey;
    }

    private static float PenaltyAreaChallengeChance(DefenderEngagementContext context)
    {
        float dangerBonus = Mathf.Clamp((14f - context.DistanceToOwnGoalMeters) / 14f, 0f, 1f) * 0.08f;
        float touchBonus = context.TouchType == DribbleTouchType.KnockOn ? 0.14f : 0f;
        float coverBonus = context.HasCover ? 0.04f : 0f;
        float patienceBonus = context.ExchangeCount >= 2 ? 0.04f : 0f;
        return Mathf.Clamp(
            context.PenaltyAreaChallengeProbability + dangerBonus + touchBonus + coverBonus + patienceBonus,
            context.PenaltyAreaChallengeProbability,
            0.28f);
    }

    private static Vector2 GoalSideTarget(DefenderEngagementContext context, float distanceMeters)
    {
        Vector2 carrierMeters = FootballPitchDimensions.ToMeters(context.CarrierPosition);
        Vector2 ownGoalMeters = FootballPitchDimensions.ToMeters(context.OwnGoal);
        Vector2 towardGoal = ownGoalMeters - carrierMeters;
        Vector2 direction = towardGoal.LengthSquared() > 0.001f ? towardGoal.Normalized() : Vector2.Left;
        Vector2 targetMeters = carrierMeters + direction * distanceMeters;
        return SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(targetMeters));
    }
}
