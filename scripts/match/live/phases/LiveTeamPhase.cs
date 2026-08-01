public enum LiveTeamPhase
{
    InPossession,
    BallInFlight,
    Defending,
    LooseBall,
    BuildUp,
    Progression,
    FinalThird,
    CounterAttack,
    TransitionToAttack,
    TransitionToDefence,
    RestDefence,
    DefensiveBlock,
    EmergencyDefence,
    SetPiece
}

public static class LiveTeamPhaseRules
{
    public static bool IsPossessionPhase(LiveTeamPhase phase)
    {
        return phase is LiveTeamPhase.InPossession or
            LiveTeamPhase.BallInFlight or
            LiveTeamPhase.BuildUp or
            LiveTeamPhase.Progression or
            LiveTeamPhase.FinalThird or
            LiveTeamPhase.CounterAttack or
            LiveTeamPhase.TransitionToAttack;
    }

    public static bool IsDefensivePhase(LiveTeamPhase phase)
    {
        return phase is LiveTeamPhase.Defending or
            LiveTeamPhase.TransitionToDefence or
            LiveTeamPhase.DefensiveBlock or
            LiveTeamPhase.EmergencyDefence;
    }
}
