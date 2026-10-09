using System.Collections.Generic;
using Godot;

// Offside position is judged when a team-mate plays or touches the ball. A player in an offside position at that
// moment is penalised when he later becomes involved, for example by collecting a rebound from a save or a block.
// Deliberate play by an opponent (other than a save) ends the exposure.
public sealed class OffsideExposureLedger
{
    private readonly HashSet<StringName> _exposedPlayers = new();

    public StringName AttackingTeamId { get; private set; } = new();

    public void Capture(
        OffsideRule rule,
        StringName attackingTeamId,
        StringName touchingPlayerId,
        Vector2 ballPosition,
        float attackDirection,
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, StringName> playerTeams)
    {
        _exposedPlayers.Clear();
        AttackingTeamId = attackingTeamId;
        foreach ((StringName playerId, StringName teamId) in playerTeams)
        {
            if (teamId != attackingTeamId || playerId == touchingPlayerId)
            {
                continue;
            }
            if (rule.IsOffside(playerId, attackingTeamId, ballPosition, attackDirection, positions, playerTeams))
            {
                _exposedPlayers.Add(playerId);
            }
        }
    }

    public void Clear()
    {
        _exposedPlayers.Clear();
        AttackingTeamId = new StringName();
    }

    public bool IsExposed(StringName playerId) => _exposedPlayers.Contains(playerId);
}
