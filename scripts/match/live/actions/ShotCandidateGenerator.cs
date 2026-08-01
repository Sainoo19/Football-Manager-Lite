using System;
using System.Collections.Generic;

public sealed class ShotCandidateGenerator : IFootballActionCandidateGenerator
{
    private readonly FootballActionSelectionConfiguration _configuration;

    public ShotCandidateGenerator(FootballActionSelectionConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        if (context.IsGoalkeeper || context.ShotValue < _configuration.MinimumShotValue)
        {
            return;
        }

        float distanceMeters = FootballPitchDimensions.DistanceMeters(
            context.ActorPosition,
            context.AttackingGoal);
        candidates.Add(new FootballActionCandidate(
            FootballActionType.Shot,
            context.ActorId,
            new Godot.StringName(),
            context.AttackingGoal,
            Math.Max(0f, distanceMeters),
            Math.Clamp(0.78f - context.ShotValue * 0.58f, 0.35f, 0.95f),
            context.IsUnderPressure ? 1f : 0f,
            1f,
            Math.Clamp(context.ShotValue + (context.IsDirectAttack ? 0.12f : 0f), 0f, 1f),
            Math.Clamp(distanceMeters / 48f + (context.IsUnderPressure ? 0.18f : 0f), 0.08f, 1f),
            true,
            context.IsDirectAttack ? "direct_attack_shot_window" : "relative_shot_value"));
    }
}
