using System;
using System.Collections.Generic;

public sealed class CarryCandidateGenerator : IFootballActionCandidateGenerator
{
    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        candidates.Add(new FootballActionCandidate(
            FootballActionType.Hold,
            context.ActorId,
            new Godot.StringName(),
            context.ActorPosition,
            0f,
            context.IsUnderPressure ? 0.52f : 0.10f,
            context.IsUnderPressure ? 1f : 0f,
            1f,
            0f,
            0.05f,
            true,
            context.IsUnderPressure ? "pause_under_pressure" : "retain_control"));

        if (context.IsGoalkeeper)
        {
            return;
        }

        float carryProgression = Math.Clamp(context.ForwardSpaceMeters * 0.62f, 1.5f, 9f);
        float pressureRisk = context.IsUnderPressure
            ? Math.Clamp(1f - context.PressureDistanceMeters / 4f, 0.28f, 0.92f)
            : 0.08f;
        candidates.Add(new FootballActionCandidate(
            FootballActionType.Carry,
            context.ActorId,
            new Godot.StringName(),
            context.ActorPosition,
            carryProgression,
            pressureRisk,
            context.IsUnderPressure ? 1f : 0f,
            1f,
            Math.Clamp(context.AttackProgress * 0.30f + carryProgression / 28f, 0f, 1f),
            Math.Clamp((100 - context.Dribbling) / 130f + pressureRisk * 0.25f, 0.05f, 0.85f),
            true,
            context.IsUnderPressure ? "carry_against_pressure" : "space_ahead"));

        if (context.IsUnderPressure || context.IsStalledDuel)
        {
            candidates.Add(new FootballActionCandidate(
                FootballActionType.ProtectBall,
                context.ActorId,
                new Godot.StringName(),
                context.ActorPosition,
                0f,
                Math.Clamp(0.38f - context.Composure / 330f, 0.08f, 0.35f),
                1f,
                1f,
                0f,
                Math.Clamp((100 - context.Dribbling) / 180f, 0.05f, 0.55f),
                true,
                context.IsStalledDuel ? "stalled_duel_control" : "shield_from_pressure"));
        }
    }
}
