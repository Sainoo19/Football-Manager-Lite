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
        int maximumRejectedDiagnostics,
        float counterAttackProgressionBonus,
        float buildUpSecurityBonus,
        float finalThirdThreatBonus,
        float transitionSecurityBonus)
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
        CounterAttackProgressionBonus = counterAttackProgressionBonus;
        BuildUpSecurityBonus = buildUpSecurityBonus;
        FinalThirdThreatBonus = finalThirdThreatBonus;
        TransitionSecurityBonus = transitionSecurityBonus;
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
    public float CounterAttackProgressionBonus { get; }
    public float BuildUpSecurityBonus { get; }
    public float FinalThirdThreatBonus { get; }
    public float TransitionSecurityBonus { get; }

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
            maximumRejectedDiagnostics: 5,
            counterAttackProgressionBonus: 0.16f,
            buildUpSecurityBonus: 0.10f,
            finalThirdThreatBonus: 0.14f,
            transitionSecurityBonus: 0.12f);
    }
}
