using System;
using System.Collections.Generic;

public sealed class GoalkeeperActionCandidateGenerator : IFootballActionCandidateGenerator
{
    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!context.IsGoalkeeper || !context.GoalkeeperDistributionOption.HasTarget)
        {
            return;
        }

        PassSelection distribution = context.GoalkeeperDistributionOption.Selection;
        float control = Math.Clamp(
            0.62f + distribution.ReceiverSpaceMeters / 24f - distribution.LaneRisk * 0.45f,
            0.12f,
            0.98f);
        candidates.Add(new FootballActionCandidate(
            FootballActionType.GoalkeeperDistribution,
            context.ActorId,
            distribution.ReceiverId,
            context.GoalkeeperDistributionOption.TargetPoint,
            distribution.ForwardGainMeters,
            Math.Clamp(distribution.LaneRisk * 0.65f + (1f - control) * 0.35f, 0.04f, 0.92f),
            context.IsUnderPressure ? 1f : 0f,
            control,
            Math.Clamp(distribution.ForwardGainMeters / 45f, 0f, 0.72f),
            Math.Clamp(distribution.DistanceMeters / 70f, 0.06f, 0.85f),
            true,
            context.IsUnderPressure ? "goalkeeper_pressure_release" : "goalkeeper_build_up"));
    }
}
