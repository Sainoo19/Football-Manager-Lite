public readonly struct FootballActionScoreBreakdown
{
    public FootballActionScoreBreakdown(
        float baseValue,
        float progression,
        float possessionSecurity,
        float threat,
        float pressureRelief,
        float turnoverRisk,
        float executionDifficulty,
        float deterministicVariation,
        float commitment)
    {
        BaseValue = baseValue;
        Progression = progression;
        PossessionSecurity = possessionSecurity;
        Threat = threat;
        PressureRelief = pressureRelief;
        TurnoverRisk = turnoverRisk;
        ExecutionDifficulty = executionDifficulty;
        DeterministicVariation = deterministicVariation;
        Commitment = commitment;
    }

    public float BaseValue { get; }
    public float Progression { get; }
    public float PossessionSecurity { get; }
    public float Threat { get; }
    public float PressureRelief { get; }
    public float TurnoverRisk { get; }
    public float ExecutionDifficulty { get; }
    public float DeterministicVariation { get; }
    public float Commitment { get; }
    public float Total => BaseValue + Progression + PossessionSecurity + Threat + PressureRelief -
                          TurnoverRisk - ExecutionDifficulty + DeterministicVariation + Commitment;
}
