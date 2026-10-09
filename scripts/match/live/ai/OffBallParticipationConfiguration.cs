using System;

public sealed class OffBallParticipationConfiguration
{
    public OffBallParticipationConfiguration(
        float minimumAssignmentSeconds,
        float assignmentHandoffDistanceMeters,
        float targetReservationDistanceMeters,
        float shortSupportMinimumDistanceMeters,
        float shortSupportMaximumDistanceMeters,
        int maximumSupportPlayers,
        int maximumForwardRunners,
        int maximumLooseBallChasers,
        float dangerousReceiverDistanceMeters,
        float markerControlDistanceMeters,
        float pressApproachDistanceMeters = 1.5f,
        float shotLineBlockActivationDistanceMeters = 25f,
        float shotLineBlockOffsetMeters = 5f,
        float earlyRunProbability = 0f,
        float earlyRunMaximumOvershootMeters = 0f,
        float earlyRunWindowSeconds = 4f)
    {
        if (minimumAssignmentSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAssignmentSeconds));
        }
        if (maximumSupportPlayers < 1 || maximumForwardRunners < 1 || maximumLooseBallChasers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSupportPlayers));
        }

        MinimumAssignmentSeconds = minimumAssignmentSeconds;
        AssignmentHandoffDistanceMeters = assignmentHandoffDistanceMeters;
        TargetReservationDistanceMeters = targetReservationDistanceMeters;
        ShortSupportMinimumDistanceMeters = shortSupportMinimumDistanceMeters;
        ShortSupportMaximumDistanceMeters = shortSupportMaximumDistanceMeters;
        MaximumSupportPlayers = maximumSupportPlayers;
        MaximumForwardRunners = maximumForwardRunners;
        MaximumLooseBallChasers = maximumLooseBallChasers;
        DangerousReceiverDistanceMeters = dangerousReceiverDistanceMeters;
        MarkerControlDistanceMeters = markerControlDistanceMeters;
        PressApproachDistanceMeters = pressApproachDistanceMeters;
        ShotLineBlockActivationDistanceMeters = shotLineBlockActivationDistanceMeters;
        ShotLineBlockOffsetMeters = shotLineBlockOffsetMeters;
        EarlyRunProbability = earlyRunProbability;
        EarlyRunMaximumOvershootMeters = earlyRunMaximumOvershootMeters;
        EarlyRunWindowSeconds = earlyRunWindowSeconds;
    }

    public float MinimumAssignmentSeconds { get; }
    public float AssignmentHandoffDistanceMeters { get; }
    public float TargetReservationDistanceMeters { get; }
    public float ShortSupportMinimumDistanceMeters { get; }
    public float ShortSupportMaximumDistanceMeters { get; }
    public int MaximumSupportPlayers { get; }
    public int MaximumForwardRunners { get; }
    public int MaximumLooseBallChasers { get; }
    public float DangerousReceiverDistanceMeters { get; }
    public float MarkerControlDistanceMeters { get; }
    public float PressApproachDistanceMeters { get; }
    public float ShotLineBlockActivationDistanceMeters { get; }
    public float ShotLineBlockOffsetMeters { get; }
    public float EarlyRunProbability { get; }
    public float EarlyRunMaximumOvershootMeters { get; }
    public float EarlyRunWindowSeconds { get; }

    public static OffBallParticipationConfiguration CreateM3Defaults()
    {
        return new OffBallParticipationConfiguration(
            minimumAssignmentSeconds: 1.15f,
            assignmentHandoffDistanceMeters: 14f,
            targetReservationDistanceMeters: 5.5f,
            shortSupportMinimumDistanceMeters: 5f,
            shortSupportMaximumDistanceMeters: 22f,
            maximumSupportPlayers: 5,
            maximumForwardRunners: 4,
            maximumLooseBallChasers: 2,
            dangerousReceiverDistanceMeters: 24f,
            markerControlDistanceMeters: 7.5f,
            pressApproachDistanceMeters: 1.5f,
            shotLineBlockActivationDistanceMeters: 25f,
            shotLineBlockOffsetMeters: 5f,
            earlyRunProbability: 0.35f,
            earlyRunMaximumOvershootMeters: 2.5f,
            earlyRunWindowSeconds: 4f);
    }
}
