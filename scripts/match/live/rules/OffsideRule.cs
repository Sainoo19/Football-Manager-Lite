using System.Collections.Generic;
using Godot;

public sealed class OffsideRule
{
    private const float LevelTolerance = 0.005f;

    public bool IsOffside(
        StringName receiverId,
        StringName attackingTeamId,
        Vector2 ballPosition,
        float attackDirection,
        IReadOnlyDictionary<StringName, Vector2> playerPositions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        if (!playerPositions.TryGetValue(receiverId, out Vector2 receiverPosition) ||
            !playerTeams.TryGetValue(receiverId, out StringName? receiverTeamId) ||
            receiverTeamId is null ||
            receiverTeamId != attackingTeamId)
        {
            return false;
        }

        bool isInOpponentHalf = attackDirection > 0f
            ? receiverPosition.X > 0.5f + LevelTolerance
            : receiverPosition.X < 0.5f - LevelTolerance;
        bool isAheadOfBall = attackDirection > 0f
            ? receiverPosition.X > ballPosition.X + LevelTolerance
            : receiverPosition.X < ballPosition.X - LevelTolerance;
        if (!isInOpponentHalf || !isAheadOfBall)
        {
            return false;
        }

        List<float> defenderPositions = new();
        foreach ((StringName playerId, Vector2 position) in playerPositions)
        {
            if (playerTeams.TryGetValue(playerId, out StringName? teamId) &&
                teamId is not null &&
                teamId != attackingTeamId)
            {
                defenderPositions.Add(position.X);
            }
        }

        if (defenderPositions.Count < 2)
        {
            return false;
        }

        defenderPositions.Sort();
        float secondLastDefenderX = attackDirection > 0f
            ? defenderPositions[^2]
            : defenderPositions[1];
        return attackDirection > 0f
            ? receiverPosition.X > secondLastDefenderX + LevelTolerance
            : receiverPosition.X < secondLastDefenderX - LevelTolerance;
    }
}
