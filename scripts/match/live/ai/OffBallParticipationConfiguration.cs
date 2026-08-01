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
        float markerControlDistanceMeters)
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
            markerControlDistanceMeters: 7.5f);
    }
}
