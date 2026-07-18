using Godot;

public enum GroundDuelOutcome
{
    NoChallenge,
    CarrierRetains,
    CarrierEscapes,
    DefenderWins,
    LooseBall,
    Foul
}

public readonly struct GroundDuelContext
{
    public GroundDuelContext(
        DefenderEngagementType engagementType,
        DribbleTouchPlan touch,
        int carrierDribbling,
        int carrierTechnique,
        int carrierStrength,
        int carrierBalance,
        int carrierAgility,
        int defenderTackling,
        int defenderPositioning,
        int defenderStrength,
        int defenderBalance,
        float carrierSpeedMetersPerSecond,
        float defenderSpeedMetersPerSecond,
        float challengeDistanceMeters,
        float movementAlignment,
        bool isChallengeFromBehind,
        float timingRoll,
        float outcomeRoll,
        float foulRoll)
    {
        EngagementType = engagementType;
        Touch = touch;
        CarrierDribbling = carrierDribbling;
        CarrierTechnique = carrierTechnique;
        CarrierStrength = carrierStrength;
        CarrierBalance = carrierBalance;
        CarrierAgility = carrierAgility;
        DefenderTackling = defenderTackling;
        DefenderPositioning = defenderPositioning;
        DefenderStrength = defenderStrength;
        DefenderBalance = defenderBalance;
        CarrierSpeedMetersPerSecond = carrierSpeedMetersPerSecond;
        DefenderSpeedMetersPerSecond = defenderSpeedMetersPerSecond;
        ChallengeDistanceMeters = challengeDistanceMeters;
        MovementAlignment = Mathf.Clamp(movementAlignment, -1f, 1f);
        IsChallengeFromBehind = isChallengeFromBehind;
        TimingRoll = timingRoll;
        OutcomeRoll = outcomeRoll;
        FoulRoll = foulRoll;
    }

    public DefenderEngagementType EngagementType { get; }
    public DribbleTouchPlan Touch { get; }
    public int CarrierDribbling { get; }
    public int CarrierTechnique { get; }
    public int CarrierStrength { get; }
    public int CarrierBalance { get; }
    public int CarrierAgility { get; }
    public int DefenderTackling { get; }
    public int DefenderPositioning { get; }
    public int DefenderStrength { get; }
    public int DefenderBalance { get; }
    public float CarrierSpeedMetersPerSecond { get; }
    public float DefenderSpeedMetersPerSecond { get; }
    public float ChallengeDistanceMeters { get; }
    public float MovementAlignment { get; }
    public bool IsChallengeFromBehind { get; }
    public float TimingRoll { get; }
    public float OutcomeRoll { get; }
    public float FoulRoll { get; }
}

public readonly struct GroundDuelResolution
{
    public GroundDuelResolution(GroundDuelOutcome outcome, float looseBallSpeedMetersPerSecond)
    {
        Outcome = outcome;
        LooseBallSpeedMetersPerSecond = looseBallSpeedMetersPerSecond;
    }

    public GroundDuelOutcome Outcome { get; }
    public float LooseBallSpeedMetersPerSecond { get; }
}

public sealed class GroundDuelResolver
{
    public GroundDuelResolution Resolve(GroundDuelContext context)
    {
        if (context.EngagementType is not (
                DefenderEngagementType.Tackle or DefenderEngagementType.ShoulderChallenge))
        {
            return new GroundDuelResolution(GroundDuelOutcome.NoChallenge, 0f);
        }

        float overreach = Mathf.Clamp((context.ChallengeDistanceMeters - 1.0f) / 0.85f, 0f, 1f);
        float foulChance = context.EngagementType == DefenderEngagementType.Tackle ? 0.07f : 0.05f;
        foulChance += context.IsChallengeFromBehind ? 0.20f : 0f;
        foulChance += overreach * 0.06f;
        foulChance += Mathf.Clamp((context.TimingRoll - 0.62f) * 0.42f, 0f, 0.16f);
        if (context.FoulRoll < foulChance)
        {
            return new GroundDuelResolution(GroundDuelOutcome.Foul, 0f);
        }

        float carrierControl = context.CarrierDribbling * 0.30f +
                               context.CarrierTechnique * 0.20f +
                               context.CarrierBalance * 0.20f +
                               context.CarrierAgility * 0.18f +
                               context.CarrierStrength * 0.12f;
        float defenderQuality = context.DefenderTackling * 0.38f +
                                context.DefenderPositioning * 0.20f +
                                context.DefenderStrength * 0.24f +
                                context.DefenderBalance * 0.18f;
        float relativeSpeedMetersPerSecond = Mathf.Sqrt(Mathf.Max(
            0f,
            context.CarrierSpeedMetersPerSecond * context.CarrierSpeedMetersPerSecond +
            context.DefenderSpeedMetersPerSecond * context.DefenderSpeedMetersPerSecond -
            2f * context.CarrierSpeedMetersPerSecond * context.DefenderSpeedMetersPerSecond *
            context.MovementAlignment));
        float speedCollision = Mathf.Clamp(
            (relativeSpeedMetersPerSecond - 1.5f) / 9f,
            0f,
            1f);
        float winChance = 0.28f +
                          (defenderQuality - carrierControl) / 190f +
                          context.Touch.Exposure * 0.25f +
                          (0.5f - context.TimingRoll) * 0.18f -
                          overreach * 0.10f;
        if (context.EngagementType == DefenderEngagementType.ShoulderChallenge)
        {
            winChance -= 0.08f;
            winChance += Mathf.Max(context.MovementAlignment, 0f) * 0.05f;
        }
        else
        {
            winChance += Mathf.Max(-context.MovementAlignment, 0f) * 0.04f;
        }
        winChance = Mathf.Clamp(winChance, 0.08f, 0.72f);
        if (context.OutcomeRoll < winChance)
        {
            return new GroundDuelResolution(GroundDuelOutcome.DefenderWins, 0f);
        }

        float looseBallChance = 0.08f + speedCollision * 0.18f + context.Touch.Exposure * 0.12f;
        if (context.EngagementType == DefenderEngagementType.ShoulderChallenge)
        {
            looseBallChance += 0.12f;
        }
        if (context.OutcomeRoll < winChance + Mathf.Clamp(looseBallChance, 0.08f, 0.34f))
        {
            return new GroundDuelResolution(
                GroundDuelOutcome.LooseBall,
                Mathf.Lerp(2.4f, 5.2f, speedCollision));
        }

        bool isEscapeTouch = context.Touch.Type is
            DribbleTouchType.ChangeDirection or DribbleTouchType.KnockOn;
        bool carrierEscapes = isEscapeTouch &&
                              carrierControl + context.CarrierSpeedMetersPerSecond * 2f > defenderQuality;
        return new GroundDuelResolution(
            carrierEscapes ? GroundDuelOutcome.CarrierEscapes : GroundDuelOutcome.CarrierRetains,
            0f);
    }
}
