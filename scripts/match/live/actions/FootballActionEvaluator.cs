using System;
using System.Linq;

public sealed class FootballActionEvaluator
{
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
            FootballActionType.Shot => 0.62f,
            FootballActionType.Clearance => 0.20f,
            FootballActionType.GoalkeeperDistribution => 0.32f,
            _ => 0f
        };
    }

    private static float ThreatWeight(FootballActionType actionType)
    {
        return actionType switch
        {
            FootballActionType.Shot => 1.55f,
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
