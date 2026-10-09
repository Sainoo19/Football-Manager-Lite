using System;
using System.Linq;

public sealed class FootballActionEvaluator
{
    private const float InitialControlSeconds = 1.2f;
    private const float ProtectPersistenceCostPerSecond = 0.22f;
    private const float CarryPersistenceCostPerSecond = 0.10f;
    private const float MaximumPersistenceCost = 0.75f;
    private const float MeaningfulShotValue = 0.12f;
    private readonly FootballActionSelectionConfiguration _configuration;

    public FootballActionEvaluator(FootballActionSelectionConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public FootballActionCandidate Evaluate(FootballActionContext context, FootballActionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);
        float baseValue = BaseValue(candidate.ActionType);
        float progression = Math.Clamp(candidate.ExpectedProgressionMeters / 22f, -0.32f, 0.52f);
        float possessionSecurity = candidate.ReceiverControlProbability * 0.62f;
        float threat = candidate.ThreatValue * ThreatWeight(candidate.ActionType);
        float pressureRelief = PressureRelief(context, candidate);
        float turnoverRisk = candidate.TurnoverRisk * RiskWeight(candidate.ActionType);
        float executionDifficulty = candidate.ExecutionDifficulty * 0.34f;
        float phaseFit = PhaseFit(context, candidate);

        if (candidate.ActionType == FootballActionType.Hold && !context.IsGoalkeeper)
        {
            // A short pause controls the ball; waiting indefinitely cannot remain the safest action.
            float heldBeyondControl = Math.Max(0f, context.OwnerHeldSeconds - InitialControlSeconds);
            phaseFit -= Math.Min(MaximumPersistenceCost, heldBeyondControl * ProtectPersistenceCostPerSecond);
        }

        if (candidate.ActionType is FootballActionType.Carry or FootballActionType.ProtectBall)
        {
            // Keeping the ball is uncertain under pressure; it is not a guaranteed reception.
            possessionSecurity *= 1f - candidate.TurnoverRisk;
            bool hasOutlet = context.PassOptions.Any(option => option.HasTarget &&
                option.Selection.LaneRisk <= 0.65f && option.Selection.ReceiverSpaceMeters >= 2.5f);
            bool blockedCarry = candidate.ActionType == FootballActionType.Carry &&
                (context.ForwardSpaceMeters < 2f ||
                 Math.Abs(context.ActorPosition.X - context.AttackingGoal.X) * FootballPitchDimensions.LengthMeters < 3f);
            if (hasOutlet || context.ShotValue >= MeaningfulShotValue || blockedCarry)
            {
                float heldBeyondControl = Math.Max(0f, context.OwnerHeldSeconds - InitialControlSeconds);
                float persistenceCost = candidate.ActionType == FootballActionType.ProtectBall
                    ? ProtectPersistenceCostPerSecond
                    : CarryPersistenceCostPerSecond;
                phaseFit -= Math.Min(MaximumPersistenceCost, heldBeyondControl * persistenceCost);
            }
        }

        if (candidate.ActionType == FootballActionType.Carry && HasClearAdvantageOutlet(context))
        {
            turnoverRisk += 0.24f;
        }
        if (candidate.ActionType is FootballActionType.GroundPass or FootballActionType.ThroughBall or
            FootballActionType.LoftedPass && candidate.ExpectedProgressionMeters < -1.5f)
        {
            possessionSecurity += candidate.ReceiverControlProbability * 0.22f;
        }
        if (candidate.ActionType == FootballActionType.Shot)
        {
            possessionSecurity = 0f;
            progression = 0f;
        }

        float variation = StableVariation(context, candidate);
        return candidate.WithScore(new FootballActionScoreBreakdown(
            baseValue,
            progression,
            possessionSecurity,
            threat,
            pressureRelief,
            turnoverRisk,
            executionDifficulty,
            phaseFit,
            variation,
            0f));
    }

    public FootballActionCandidate AddCommitment(FootballActionCandidate candidate)
    {
        FootballActionScoreBreakdown score = candidate.Score;
        return candidate.WithScore(new FootballActionScoreBreakdown(
            score.BaseValue,
            score.Progression,
            score.PossessionSecurity,
            score.Threat,
            score.PressureRelief,
            score.TurnoverRisk,
            score.ExecutionDifficulty,
            score.PhaseFit,
            score.DeterministicVariation,
            _configuration.CommitmentBonus));
    }

    private static float BaseValue(FootballActionType actionType)
    {
        return actionType switch
        {
            FootballActionType.Hold => 0.08f,
            FootballActionType.Carry => 0.34f,
            FootballActionType.ProtectBall => 0.38f,
            FootballActionType.GroundPass => 0.28f,
            FootballActionType.ThroughBall => 0.38f,
            FootballActionType.LoftedPass => 0.22f,
            FootballActionType.Cross => 0.27f,
            FootballActionType.Shot => 0.78f,
            FootballActionType.Clearance => 0.20f,
            FootballActionType.GoalkeeperDistribution => 0.32f,
            _ => 0f
        };
    }

    private static float ThreatWeight(FootballActionType actionType)
    {
        return actionType switch
        {
            FootballActionType.Shot => 2.60f,
            FootballActionType.ThroughBall => 1.08f,
            FootballActionType.Cross => 0.92f,
            FootballActionType.Clearance => 1.05f,
            _ => 0.68f
        };
    }

    private static float RiskWeight(FootballActionType actionType)
    {
        return actionType switch
        {
            FootballActionType.Shot => 0.66f,
            FootballActionType.Clearance => 0.38f,
            FootballActionType.Hold => 0.82f,
            _ => 0.88f
        };
    }

    private static float PressureRelief(FootballActionContext context, FootballActionCandidate candidate)
    {
        if (!context.IsUnderPressure)
        {
            return 0f;
        }
        return candidate.ActionType switch
        {
            FootballActionType.GroundPass or FootballActionType.ThroughBall or
                FootballActionType.GoalkeeperDistribution => candidate.ReceiverControlProbability * 0.34f,
            FootballActionType.Clearance => 0.42f,
            FootballActionType.ProtectBall => 0.24f,
            FootballActionType.Carry => -0.18f,
            FootballActionType.Hold => -0.28f,
            _ => 0f
        };
    }

    private float PhaseFit(FootballActionContext context, FootballActionCandidate candidate)
    {
        return context.TeamPhase switch
        {
            LiveTeamPhase.CounterAttack when
                (candidate.ActionType is FootballActionType.Carry or
                    FootballActionType.ThroughBall or
                    FootballActionType.GroundPass) &&
                candidate.ExpectedProgressionMeters >= 4f =>
                _configuration.CounterAttackProgressionBonus,
            LiveTeamPhase.CounterAttack when candidate.ExpectedProgressionMeters < -3f =>
                -_configuration.CounterAttackProgressionBonus,
            LiveTeamPhase.BuildUp when
                (candidate.ActionType is FootballActionType.GroundPass or
                    FootballActionType.GoalkeeperDistribution) &&
                candidate.ReceiverControlProbability >= 0.60f =>
                _configuration.BuildUpSecurityBonus,
            LiveTeamPhase.FinalThird when
                candidate.ActionType is FootballActionType.Shot or
                    FootballActionType.Cross or
                    FootballActionType.ThroughBall =>
                _configuration.FinalThirdThreatBonus,
            LiveTeamPhase.TransitionToAttack when
                candidate.ActionType is FootballActionType.GroundPass or
                    FootballActionType.ProtectBall or
                    FootballActionType.Hold =>
                _configuration.TransitionSecurityBonus,
            _ => 0f
        };
    }

    private static bool HasClearAdvantageOutlet(FootballActionContext context)
    {
        return context.PassOptions.Any(option =>
            option.Selection.ReceiverSpaceMeters >= 6f &&
            option.Selection.LaneRisk <= 0.38f &&
            option.Selection.ForwardGainMeters >= -1.5f);
    }

    private float StableVariation(FootballActionContext context, FootballActionCandidate candidate)
    {
        uint value = context.DecisionSeed ^ unchecked((uint)context.DecisionSerial * 3266489917u);
        value ^= StableHash(context.ActorId.ToString());
        value ^= RotateLeft(StableHash(candidate.StableKey), 13);
        value ^= value >> 16;
        value *= 0x85ebca6bu;
        value ^= value >> 13;
        float normalized = (value & 0x00ffffff) / 16777215f;
        return (normalized - 0.5f) * 2f * _configuration.DeterministicVariationAmplitude;
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261u;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return hash;
    }

    private static uint RotateLeft(uint value, int count)
    {
        return value << count | value >> (32 - count);
    }
}
