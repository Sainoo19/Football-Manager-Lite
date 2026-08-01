using System;
using System.Collections.Generic;

public sealed class CrossCandidateGenerator : IFootballActionCandidateGenerator
{
    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!context.CrossOption.HasTarget)
        {
            return;
        }

        PassSelection cross = context.CrossOption.Selection;
        float control = Math.Clamp(
            0.42f + cross.ReceiverSpaceMeters / 20f - cross.LaneRisk * 0.42f,
            0.08f,
            0.90f);
        candidates.Add(new FootballActionCandidate(
            FootballActionType.Cross,
            context.ActorId,
            cross.ReceiverId,
            context.CrossOption.TargetPoint,
            cross.ForwardGainMeters,
            Math.Clamp(cross.LaneRisk * 0.62f + (1f - control) * 0.42f, 0.08f, 0.95f),
            context.IsUnderPressure ? 1f : 0f,
            control,
            Math.Clamp(context.AttackProgress * 0.52f + cross.ForwardGainMeters / 55f, 0f, 1f),
            Math.Clamp(cross.DistanceMeters / 62f + 0.16f, 0.12f, 1f),
            true,
            "wide_delivery_option"));
    }
}
