using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class TeamPhaseMetricsSnapshot
{
    public TeamPhaseMetricsSnapshot(
        IReadOnlyDictionary<LiveTeamPhase, float> durationSecondsByPhase,
        IReadOnlyDictionary<string, int> transitionCounts,
        int counterAttacks,
        int counterAttackShots,
        float totalOrganizationSeconds,
        int organizationSamples,
        int finalThirdRestDefenceObservations,
        int finalThirdRestDefencePlayerTotal,
        int emergencyDefenceEntries)
    {
        DurationSecondsByPhase = new ReadOnlyDictionary<LiveTeamPhase, float>(
            new Dictionary<LiveTeamPhase, float>(durationSecondsByPhase));
        TransitionCounts = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(transitionCounts));
        CounterAttacks = counterAttacks;
        CounterAttackShots = counterAttackShots;
        TotalOrganizationSeconds = totalOrganizationSeconds;
        OrganizationSamples = organizationSamples;
        FinalThirdRestDefenceObservations = finalThirdRestDefenceObservations;
        FinalThirdRestDefencePlayerTotal = finalThirdRestDefencePlayerTotal;
        EmergencyDefenceEntries = emergencyDefenceEntries;
    }

    public IReadOnlyDictionary<LiveTeamPhase, float> DurationSecondsByPhase { get; }
    public IReadOnlyDictionary<string, int> TransitionCounts { get; }
    public int CounterAttacks { get; }
    public int CounterAttackShots { get; }
    public float CounterAttackConversion => CounterAttacks == 0 ? 0f : (float)CounterAttackShots / CounterAttacks;
    public float TotalOrganizationSeconds { get; }
    public int OrganizationSamples { get; }
    public float AverageOrganizationSeconds => OrganizationSamples == 0
        ? 0f
        : TotalOrganizationSeconds / OrganizationSamples;
    public int FinalThirdRestDefenceObservations { get; }
    public int FinalThirdRestDefencePlayerTotal { get; }
    public float AverageFinalThirdRestDefencePlayers => FinalThirdRestDefenceObservations == 0
        ? 0f
        : (float)FinalThirdRestDefencePlayerTotal / FinalThirdRestDefenceObservations;
    public int EmergencyDefenceEntries { get; }
}
