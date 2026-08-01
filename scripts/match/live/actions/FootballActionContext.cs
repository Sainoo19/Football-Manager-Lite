using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public readonly struct FootballPassOption
{
    public FootballPassOption(
        PassSelection selection,
        Vector2 targetPoint,
        FootballActionType actionType)
    {
        Selection = selection;
        TargetPoint = targetPoint;
        ActionType = actionType;
    }

    public PassSelection Selection { get; }
    public Vector2 TargetPoint { get; }
    public FootballActionType ActionType { get; }
    public bool HasTarget => Selection.HasTarget;
}

public sealed class FootballActionContext
{
    public FootballActionContext(
        StringName actorId,
        StringName teamId,
        string actorRole,
        Vector2 actorPosition,
        Vector2 attackingGoal,
        LiveTeamPhase teamPhase,
        float attackProgress,
        bool isUnderPressure,
        float pressureDistanceMeters,
        bool isDirectAttack,
        bool isStalledDuel,
        float ownerHeldSeconds,
        float ownerCarriedDistanceMeters,
        int ownerDecisionCount,
        int passing,
        int vision,
        int composure,
        int dribbling,
        int finishing,
        float forwardSpaceMeters,
        float defensiveDanger,
        float shotValue,
        IReadOnlyList<FootballPassOption> passOptions,
        FootballPassOption crossOption,
        FootballPassOption goalkeeperDistributionOption,
        FootballActionType? previousActionType,
        StringName previousTargetId,
        uint decisionSeed,
        int decisionSerial)
    {
        ArgumentNullException.ThrowIfNull(passOptions);
        ActorId = actorId;
        TeamId = teamId;
        ActorRole = actorRole ?? throw new ArgumentNullException(nameof(actorRole));
        ActorPosition = actorPosition;
        AttackingGoal = attackingGoal;
        TeamPhase = teamPhase;
        AttackProgress = attackProgress;
        IsUnderPressure = isUnderPressure;
        PressureDistanceMeters = pressureDistanceMeters;
        IsDirectAttack = isDirectAttack;
        IsStalledDuel = isStalledDuel;
        OwnerHeldSeconds = ownerHeldSeconds;
        OwnerCarriedDistanceMeters = ownerCarriedDistanceMeters;
        OwnerDecisionCount = ownerDecisionCount;
        Passing = passing;
        Vision = vision;
        Composure = composure;
        Dribbling = dribbling;
        Finishing = finishing;
        ForwardSpaceMeters = forwardSpaceMeters;
        DefensiveDanger = defensiveDanger;
        ShotValue = shotValue;
        PassOptions = new ReadOnlyCollection<FootballPassOption>(new List<FootballPassOption>(passOptions));
        CrossOption = crossOption;
        GoalkeeperDistributionOption = goalkeeperDistributionOption;
        PreviousActionType = previousActionType;
        PreviousTargetId = previousTargetId;
        DecisionSeed = decisionSeed;
        DecisionSerial = decisionSerial;
    }

    public StringName ActorId { get; }
    public StringName TeamId { get; }
    public string ActorRole { get; }
    public Vector2 ActorPosition { get; }
    public Vector2 AttackingGoal { get; }
    public LiveTeamPhase TeamPhase { get; }
    public float AttackProgress { get; }
    public bool IsUnderPressure { get; }
    public float PressureDistanceMeters { get; }
    public bool IsDirectAttack { get; }
    public bool IsStalledDuel { get; }
    public float OwnerHeldSeconds { get; }
    public float OwnerCarriedDistanceMeters { get; }
    public int OwnerDecisionCount { get; }
    public int Passing { get; }
    public int Vision { get; }
    public int Composure { get; }
    public int Dribbling { get; }
    public int Finishing { get; }
    public float ForwardSpaceMeters { get; }
    public float DefensiveDanger { get; }
    public float ShotValue { get; }
    public IReadOnlyList<FootballPassOption> PassOptions { get; }
    public FootballPassOption CrossOption { get; }
    public FootballPassOption GoalkeeperDistributionOption { get; }
    public FootballActionType? PreviousActionType { get; }
    public StringName PreviousTargetId { get; }
    public uint DecisionSeed { get; }
    public int DecisionSerial { get; }
    public bool IsGoalkeeper => ActorRole == "GK";
}
