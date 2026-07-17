using System.Collections.Generic;
using Godot;

public readonly struct KickoffSetup
{
    public KickoffSetup(
        StringName takerId,
        StringName receiverId,
        Vector2 takerPosition,
        Vector2 receiverPosition)
    {
        TakerId = takerId;
        ReceiverId = receiverId;
        TakerPosition = takerPosition;
        ReceiverPosition = receiverPosition;
    }

    public StringName TakerId { get; }
    public StringName ReceiverId { get; }
    public Vector2 TakerPosition { get; }
    public Vector2 ReceiverPosition { get; }
    public bool IsValid => TakerId != new StringName() && ReceiverId != new StringName();
}

public sealed class KickoffRestartPlanner
{
    private const float ReceiverDepthMeters = 8f;
    private const float ReceiverLaneOffsetMeters = 3.5f;

    public KickoffSetup Plan(
        StringName teamId,
        float attackDirection,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams,
        IReadOnlyDictionary<StringName, string> playerRoles)
    {
        StringName takerId = SelectPlayer(teamId, positions, playerTeams, playerRoles, true, new StringName());
        StringName receiverId = SelectPlayer(teamId, positions, playerTeams, playerRoles, false, takerId);
        if (takerId == new StringName() || receiverId == new StringName())
        {
            return default;
        }

        Vector2 takerPosition = new(0.5f - attackDirection * 0.004f, 0.5f);
        Vector2 receiverPosition = new(
            0.5f - attackDirection * (ReceiverDepthMeters / FootballPitchDimensions.LengthMeters),
            0.5f + ReceiverLaneOffsetMeters / FootballPitchDimensions.WidthMeters);
        return new KickoffSetup(takerId, receiverId, takerPosition, receiverPosition);
    }

    private static StringName SelectPlayer(
        StringName teamId,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams,
        IReadOnlyDictionary<StringName, string> playerRoles,
        bool selectsTaker,
        StringName excludedId)
    {
        StringName bestId = new();
        int bestPriority = int.MaxValue;
        float bestCenterDistance = float.PositiveInfinity;
        foreach (StringName playerId in positions.Keys)
        {
            if (playerId == excludedId ||
                playerTeams[playerId] != teamId ||
                playerRoles[playerId] == "GK")
            {
                continue;
            }

            int priority = selectsTaker
                ? TakerPriority(playerRoles[playerId])
                : ReceiverPriority(playerRoles[playerId]);
            float centerDistance = positions[playerId].DistanceSquaredTo(new Vector2(0.5f, 0.5f));
            if (priority > bestPriority || priority == bestPriority && centerDistance >= bestCenterDistance)
            {
                continue;
            }

            bestId = playerId;
            bestPriority = priority;
            bestCenterDistance = centerDistance;
        }

        return bestId;
    }

    private static int TakerPriority(string role)
    {
        return role switch
        {
            "ST" => 0,
            "AM" or "CM" => 1,
            "LW" or "RW" => 2,
            _ => 3
        };
    }

    private static int ReceiverPriority(string role)
    {
        return role switch
        {
            "CM" => 0,
            "DM" => 1,
            "AM" => 2,
            "ST" => 3,
            "LB" or "RB" => 4,
            _ => 5
        };
    }
}
