using System;

public sealed class TeamPhaseConfiguration
{
    public TeamPhaseConfiguration(
        float minimumPhaseDurationSeconds,
        float maximumCounterAttackDurationSeconds,
        float transitionOrganizationSeconds,
        float buildUpEnterProgress,
        float buildUpExitProgress,
        float finalThirdEnterProgress,
        float finalThirdExitProgress,
        float minimumCounterForwardSpaceMeters,
        int minimumCounterPlayersAhead,
        float emergencyDefenceGoalDistanceMeters,
        int minimumRestDefencePlayers)
    {
        if (minimumPhaseDurationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPhaseDurationSeconds));
        }
        if (buildUpEnterProgress >= buildUpExitProgress ||
            finalThirdExitProgress >= finalThirdEnterProgress)
        {
            throw new ArgumentException("Phase entry and exit thresholds must provide hysteresis.");
        }

        MinimumPhaseDurationSeconds = minimumPhaseDurationSeconds;
        MaximumCounterAttackDurationSeconds = maximumCounterAttackDurationSeconds;
        TransitionOrganizationSeconds = transitionOrganizationSeconds;
        BuildUpEnterProgress = buildUpEnterProgress;
        BuildUpExitProgress = buildUpExitProgress;
        FinalThirdEnterProgress = finalThirdEnterProgress;
        FinalThirdExitProgress = finalThirdExitProgress;
        MinimumCounterForwardSpaceMeters = minimumCounterForwardSpaceMeters;
        MinimumCounterPlayersAhead = minimumCounterPlayersAhead;
        EmergencyDefenceGoalDistanceMeters = emergencyDefenceGoalDistanceMeters;
        MinimumRestDefencePlayers = minimumRestDefencePlayers;
    }

    public float MinimumPhaseDurationSeconds { get; }
    public float MaximumCounterAttackDurationSeconds { get; }
    public float TransitionOrganizationSeconds { get; }
    public float BuildUpEnterProgress { get; }
    public float BuildUpExitProgress { get; }
    public float FinalThirdEnterProgress { get; }
    public float FinalThirdExitProgress { get; }
    public float MinimumCounterForwardSpaceMeters { get; }
    public int MinimumCounterPlayersAhead { get; }
    public float EmergencyDefenceGoalDistanceMeters { get; }
    public int MinimumRestDefencePlayers { get; }

    public static TeamPhaseConfiguration CreateM2Defaults()
    {
        return new TeamPhaseConfiguration(
            minimumPhaseDurationSeconds: 2.4f,
            maximumCounterAttackDurationSeconds: 8f,
            transitionOrganizationSeconds: 4.5f,
            buildUpEnterProgress: 0.34f,
            buildUpExitProgress: 0.40f,
            finalThirdEnterProgress: 0.67f,
            finalThirdExitProgress: 0.61f,
            minimumCounterForwardSpaceMeters: 7.5f,
            minimumCounterPlayersAhead: 2,
            emergencyDefenceGoalDistanceMeters: 24f,
            minimumRestDefencePlayers: 3);
    }
}
