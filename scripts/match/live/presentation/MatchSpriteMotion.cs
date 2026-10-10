using Godot;

public sealed class MatchSpriteMotion
{
    private const float MovementThresholdMeters = 0.002f;
    private const float TeleportThresholdMeters = 3f;
    private const float StrideLengthMeters = 1.6f;

    private Vector2 _lastPosition;

    public int Facing { get; private set; }
    public float StridePhase { get; private set; }
    public bool IsMoving { get; private set; }

    public MatchSpriteMotion(Vector2 position, int initialFacing)
    {
        Reset(position, initialFacing);
    }

    public void Reset(Vector2 position, int facing)
    {
        if (facing is < 0 or > 7)
        {
            throw new System.ArgumentOutOfRangeException(nameof(facing));
        }
        _lastPosition = position;
        Facing = facing;
        StridePhase = 0f;
        IsMoving = false;
    }

    public void Capture(Vector2 position, int resetFacing)
    {
        Vector2 travel = FootballPitchDimensions.ToMeters(position - _lastPosition);
        float distance = travel.Length();
        _lastPosition = position;
        if (distance > TeleportThresholdMeters)
        {
            Reset(position, resetFacing);
            return;
        }

        IsMoving = distance > MovementThresholdMeters;
        if (!IsMoving)
        {
            return;
        }
        Facing = FacingFromTravel(travel);
        StridePhase = Mathf.PosMod(StridePhase + distance / StrideLengthMeters * Mathf.Tau, Mathf.Tau);
    }

    public static int FacingFromTravel(Vector2 travel)
    {
        if (travel.IsZeroApprox())
        {
            return 0;
        }
        // Atlas order: down, down-left, left, up-left, up, up-right, right, down-right.
        float angle = Mathf.Atan2(-travel.X, travel.Y);
        return (Mathf.RoundToInt(angle / (Mathf.Pi / 4f)) + 8) % 8;
    }
}
