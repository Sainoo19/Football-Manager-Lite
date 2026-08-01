using System.Collections.Generic;
using Godot;

public sealed class PossessionSequenceState
{
    private readonly HashSet<StringName> _participants = new();
    private Vector2 _lastOwnerPosition;
    private float _lastObservedTime;

    public StringName TeamId { get; private set; } = new();
    public StringName OwnerId { get; private set; } = new();
    public StringName PreviousOwnerId { get; private set; } = new();
    public StringName EngagedDefenderId { get; private set; } = new();
    public float OwnerHeldSeconds { get; private set; }
    public float OwnerCarriedDistanceMeters { get; private set; }
    public int OwnerDecisionCount { get; private set; }
    public float DuelPairSeconds { get; private set; }
    public int DuelPairDecisionCount { get; private set; }
    public int ParticipantCount => _participants.Count;

    public void ObserveOwner(
        StringName teamId,
        StringName ownerId,
        Vector2 position,
        float visualTime)
    {
        if (teamId == new StringName() || ownerId == new StringName())
        {
            return;
        }

        if (TeamId != teamId)
        {
            Reset();
            TeamId = teamId;
        }

        if (OwnerId != ownerId)
        {
            if (OwnerId != new StringName())
            {
                PreviousOwnerId = OwnerId;
            }
            OwnerId = ownerId;
            OwnerHeldSeconds = 0f;
            OwnerCarriedDistanceMeters = 0f;
            OwnerDecisionCount = 0;
            _lastOwnerPosition = position;
            _lastObservedTime = visualTime;
            ClearDuel();
            _participants.Add(ownerId);
            return;
        }

        float deltaSeconds = Mathf.Max(visualTime - _lastObservedTime, 0f);
        OwnerHeldSeconds += deltaSeconds;
        OwnerCarriedDistanceMeters += FootballPitchDimensions.DistanceMeters(
            _lastOwnerPosition,
            position);
        _lastOwnerPosition = position;
        _lastObservedTime = visualTime;
        if (EngagedDefenderId != new StringName())
        {
            DuelPairSeconds += deltaSeconds;
        }
    }

    public void RecordOwnerDecision()
    {
        if (OwnerId != new StringName())
        {
            OwnerDecisionCount++;
        }
    }

    public void RecordCompletedPass(
        StringName teamId,
        StringName passerId,
        StringName receiverId)
    {
        if (TeamId != teamId)
        {
            Reset();
            TeamId = teamId;
        }
        if (passerId != new StringName())
        {
            _participants.Add(passerId);
            PreviousOwnerId = passerId;
        }
        if (receiverId != new StringName())
        {
            _participants.Add(receiverId);
        }
    }

    public void ObserveDuel(StringName defenderId)
    {
        if (defenderId == new StringName())
        {
            ClearDuel();
            return;
        }

        if (EngagedDefenderId != defenderId)
        {
            EngagedDefenderId = defenderId;
            DuelPairSeconds = 0f;
            DuelPairDecisionCount = 0;
        }
        DuelPairDecisionCount++;
    }

    public void ReleaseOwner()
    {
        PreviousOwnerId = OwnerId;
        ClearDuel();
    }

    public void ClearDuel()
    {
        EngagedDefenderId = new StringName();
        DuelPairSeconds = 0f;
        DuelPairDecisionCount = 0;
    }

    public void Reset()
    {
        TeamId = new StringName();
        OwnerId = new StringName();
        PreviousOwnerId = new StringName();
        EngagedDefenderId = new StringName();
        OwnerHeldSeconds = 0f;
        OwnerCarriedDistanceMeters = 0f;
        OwnerDecisionCount = 0;
        DuelPairSeconds = 0f;
        DuelPairDecisionCount = 0;
        _lastOwnerPosition = Vector2.Zero;
        _lastObservedTime = 0f;
        _participants.Clear();
    }
}
