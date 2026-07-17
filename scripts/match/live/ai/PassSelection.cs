using Godot;

public readonly struct PassSelection
{
    public PassSelection(
        StringName receiverId,
        float score,
        float forwardGainMeters,
        float distanceMeters,
        float laneRisk,
        float receiverSpaceMeters)
    {
        ReceiverId = receiverId;
        Score = score;
        ForwardGainMeters = forwardGainMeters;
        DistanceMeters = distanceMeters;
        LaneRisk = laneRisk;
        ReceiverSpaceMeters = receiverSpaceMeters;
        HasTarget = receiverId != new StringName();
    }

    public StringName ReceiverId { get; }
    public float Score { get; }
    public float ForwardGainMeters { get; }
    public float DistanceMeters { get; }
    public float LaneRisk { get; }
    public float ReceiverSpaceMeters { get; }
    public bool HasTarget { get; }
}
