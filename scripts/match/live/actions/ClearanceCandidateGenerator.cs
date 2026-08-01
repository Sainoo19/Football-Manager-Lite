using System;
using System.Collections.Generic;

public sealed class ClearanceCandidateGenerator : IFootballActionCandidateGenerator
{
    private readonly FootballActionSelectionConfiguration _configuration;

    public ClearanceCandidateGenerator(FootballActionSelectionConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Generate(FootballActionContext context, ICollection<FootballActionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        bool hasReliableControlledOption = context.GoalkeeperDistributionOption.HasTarget ||
            System.Linq.Enumerable.Any(context.PassOptions, option =>
                option.Selection.LaneRisk <= 0.45f &&
                option.Selection.ReceiverSpaceMeters >= 4f);
        if (context.DefensiveDanger < _configuration.MinimumClearanceDanger ||
            !context.IsUnderPressure && hasReliableControlledOption)
        {
            return;
        }

        candidates.Add(new FootballActionCandidate(
            FootballActionType.Clearance,
            context.ActorId,
            new Godot.StringName(),
            context.ActorPosition,
            18f,
            0.42f,
            context.IsUnderPressure ? 1f : 0f,
            0.38f,
            context.DefensiveDanger,
            0.34f,
            true,
            "defensive_danger_requires_relief"));
    }
}
