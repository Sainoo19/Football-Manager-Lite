using Godot;

public sealed class BallTouchLedger
{
    public StringName PlayerId { get; private set; } = new();
    public StringName TeamId { get; private set; } = new();

    public void Record(StringName playerId, StringName teamId)
    {
        if (playerId == new StringName() || teamId == new StringName())
        {
            return;
        }
        PlayerId = playerId;
        TeamId = teamId;
    }

    public void Reset()
    {
        PlayerId = new StringName();
        TeamId = new StringName();
    }
}
