public sealed class OffBallMetricsSnapshot
{
    public OffBallMetricsSnapshot(
        int observations,
        float totalTeamWidthMeters,
        float totalTeamLengthMeters,
        float totalCompactnessMeters,
        int sameTargetCollisions,
        int totalPassingOptions,
        int possessionObservations,
        int totalRunnerLaneDiversity,
        int runnerObservations,
        int totalRestDefencePlayers,
        int totalUnmarkedDangerousReceivers,
        int nearPostOccupations,
        int farPostOccupations,
        int cutBackOccupations)
    {
        Observations = observations;
        TotalTeamWidthMeters = totalTeamWidthMeters;
        TotalTeamLengthMeters = totalTeamLengthMeters;
        TotalCompactnessMeters = totalCompactnessMeters;
        SameTargetCollisions = sameTargetCollisions;
        TotalPassingOptions = totalPassingOptions;
        PossessionObservations = possessionObservations;
        TotalRunnerLaneDiversity = totalRunnerLaneDiversity;
        RunnerObservations = runnerObservations;
        TotalRestDefencePlayers = totalRestDefencePlayers;
        TotalUnmarkedDangerousReceivers = totalUnmarkedDangerousReceivers;
        NearPostOccupations = nearPostOccupations;
        FarPostOccupations = farPostOccupations;
        CutBackOccupations = cutBackOccupations;
    }

    public int Observations { get; }
    public float TotalTeamWidthMeters { get; }
    public float TotalTeamLengthMeters { get; }
    public float TotalCompactnessMeters { get; }
    public int SameTargetCollisions { get; }
    public int TotalPassingOptions { get; }
    public int PossessionObservations { get; }
    public int TotalRunnerLaneDiversity { get; }
    public int RunnerObservations { get; }
    public int TotalRestDefencePlayers { get; }
    public int TotalUnmarkedDangerousReceivers { get; }
    public int NearPostOccupations { get; }
    public int FarPostOccupations { get; }
    public int CutBackOccupations { get; }
    public float AverageTeamWidthMeters => Observations == 0 ? 0f : TotalTeamWidthMeters / Observations;
    public float AverageTeamLengthMeters => Observations == 0 ? 0f : TotalTeamLengthMeters / Observations;
    public float AverageCompactnessMeters => Observations == 0 ? 0f : TotalCompactnessMeters / Observations;
    public float AveragePassingOptions => PossessionObservations == 0
        ? 0f
        : (float)TotalPassingOptions / PossessionObservations;
    public float AverageRunnerLaneDiversity => RunnerObservations == 0
        ? 0f
        : (float)TotalRunnerLaneDiversity / RunnerObservations;
    public float AverageRestDefencePlayers => PossessionObservations == 0
        ? 0f
        : (float)TotalRestDefencePlayers / PossessionObservations;
    public float AverageUnmarkedDangerousReceivers => Observations == 0
        ? 0f
        : (float)TotalUnmarkedDangerousReceivers / Observations;
}
