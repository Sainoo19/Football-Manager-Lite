using Godot;

public sealed class GroundDuelSequenceState
{
    public StringName CarrierId { get; private set; } = new();
    public StringName DefenderId { get; private set; } = new();
    public int TouchCount { get; private set; }
    public int ExchangeCount { get; private set; }
    public DribbleTouchPlan CurrentTouch { get; private set; }
    public DefenderEngagementPlan CurrentEngagement { get; private set; }
    public bool IsBackToGoal { get; private set; }

    public bool HasCarrier => CarrierId != new StringName();
    public bool HasDefender => DefenderId != new StringName();

    public void Begin(StringName carrierId, StringName defenderId, bool isBackToGoal)
    {
        CarrierId = carrierId;
        DefenderId = defenderId;
        TouchCount = 0;
        ExchangeCount = 0;
        CurrentTouch = default;
        CurrentEngagement = default;
        IsBackToGoal = isBackToGoal;
    }

    public void AttachDefender(StringName defenderId)
    {
        DefenderId = defenderId;
    }

    public void RecordTouch(DribbleTouchPlan touch)
    {
        CurrentTouch = touch;
        TouchCount++;
    }

    public void RecordEngagement(DefenderEngagementPlan engagement)
    {
        CurrentEngagement = engagement;
        if (engagement.Type is not (DefenderEngagementType.CloseDown or DefenderEngagementType.Recover))
        {
            ExchangeCount++;
        }
    }

    public void Reset()
    {
        CarrierId = new StringName();
        DefenderId = new StringName();
        TouchCount = 0;
        ExchangeCount = 0;
        CurrentTouch = default;
        CurrentEngagement = default;
        IsBackToGoal = false;
    }
}
