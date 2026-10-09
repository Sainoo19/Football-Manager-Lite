using System;
using System.Collections.Generic;

public sealed class PassCandidateGenerator : IFootballActionCandidateGenerator
{
    private const float ChanceCreationDistanceMeters = 30f;
    private const float ChanceCreationWeight = 0.45f;
    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        foreach (FootballPassOption option in context.PassOptions)
        {
            if (!option.HasTarget)
            {
                continue;
            }

            PassSelection pass = option.Selection;
            float receiverControl = Math.Clamp(
                0.48f + pass.ReceiverSpaceMeters / 18f - pass.LaneRisk * 0.48f -
                pass.DistanceMeters / 180f,
                0.05f,
                0.98f);
            float turnoverRisk = Math.Clamp(
                pass.LaneRisk * 0.72f + (1f - receiverControl) * 0.38f,
                0.02f,
                0.98f);
            float difficulty = Math.Clamp(
                pass.DistanceMeters / 72f + pass.LaneRisk * 0.35f,
                0.05f,
                1f);
            float threat = Math.Clamp(
                context.AttackProgress * 0.22f + pass.ForwardGainMeters / 42f +
                (option.ActionType == FootballActionType.ThroughBall ? 0.22f : 0f),
                0f,
                1f);
            float receiverGoalDistance = FootballPitchDimensions.DistanceMeters(option.TargetPoint, context.AttackingGoal);
            float goalProximity = Math.Clamp((ChanceCreationDistanceMeters - receiverGoalDistance) / 22f, 0f, 1f);
            float usableSpace = Math.Clamp(pass.ReceiverSpaceMeters / 6f, 0f, 1f);
            threat = Math.Clamp(threat + goalProximity * usableSpace * (1f - pass.LaneRisk) * ChanceCreationWeight, 0f, 1f);
            candidates.Add(new FootballActionCandidate(
                option.ActionType,
                context.ActorId,
                pass.ReceiverId,
                option.TargetPoint,
                pass.ForwardGainMeters,
                turnoverRisk,
                context.IsUnderPressure ? 1f : 0f,
                receiverControl,
                threat,
                difficulty,
                true,
                option.ActionType == FootballActionType.ThroughBall
                    ? "runner_attacks_space"
                    : option.ActionType == FootballActionType.LoftedPass
                        ? "lofted_route_available"
                        : "ground_pass_available"));
        }
    }
}
