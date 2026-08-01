using Godot;

public readonly struct TeamPhaseState
{
    public TeamPhaseState(
        StringName teamId,
        LiveTeamPhase phase,
        float phaseEnteredAtSeconds,
        bool hasPossession,
        int requiredRestDefencePlayers)
    {
        TeamId = teamId;
        Phase = phase;
        PhaseEnteredAtSeconds = phaseEnteredAtSeconds;
        HasPossession = hasPossession;
        RequiredRestDefencePlayers = requiredRestDefencePlayers;
    }

    public StringName TeamId { get; }
    public LiveTeamPhase Phase { get; }
    public float PhaseEnteredAtSeconds { get; }
    public bool HasPossession { get; }
    public int RequiredRestDefencePlayers { get; }
}
