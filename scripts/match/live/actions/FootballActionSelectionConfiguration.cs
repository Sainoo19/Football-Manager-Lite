public sealed class FootballActionSelectionConfiguration
{
    public FootballActionSelectionConfiguration(
        float deterministicVariationAmplitude,
        float commitmentBonus,
        int commitmentDecisionCount,
        float commitmentCancellationPressureDeltaMeters,
        float minimumShotValue,
        float minimumClearanceDanger,
        float maximumStalledDuelSeconds,
        int maximumStalledDuelDecisions,
        int maximumRejectedDiagnostics)
    {
        DeterministicVariationAmplitude = deterministicVariationAmplitude;
        CommitmentBonus = commitmentBonus;
        CommitmentDecisionCount = commitmentDecisionCount;
        CommitmentCancellationPressureDeltaMeters = commitmentCancellationPressureDeltaMeters;
        MinimumShotValue = minimumShotValue;
        MinimumClearanceDanger = minimumClearanceDanger;
        MaximumStalledDuelSeconds = maximumStalledDuelSeconds;
        MaximumStalledDuelDecisions = maximumStalledDuelDecisions;
        MaximumRejectedDiagnostics = maximumRejectedDiagnostics;
    }

    public float DeterministicVariationAmplitude { get; }
    public float CommitmentBonus { get; }
    public int CommitmentDecisionCount { get; }
    public float CommitmentCancellationPressureDeltaMeters { get; }
    public float MinimumShotValue { get; }
    public float MinimumClearanceDanger { get; }
    public float MaximumStalledDuelSeconds { get; }
    public int MaximumStalledDuelDecisions { get; }
    public int MaximumRejectedDiagnostics { get; }

    public static FootballActionSelectionConfiguration CreateM1Defaults()
    {
        return new FootballActionSelectionConfiguration(
            deterministicVariationAmplitude: 0.035f,
            commitmentBonus: 0.12f,
            commitmentDecisionCount: 2,
            commitmentCancellationPressureDeltaMeters: 1.8f,
            minimumShotValue: 0.045f,
            minimumClearanceDanger: 0.52f,
            maximumStalledDuelSeconds: 4.5f,
            maximumStalledDuelDecisions: 6,
            maximumRejectedDiagnostics: 5);
    }
}
