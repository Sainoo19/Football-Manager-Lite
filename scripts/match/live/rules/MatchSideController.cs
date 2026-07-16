using Godot;

public sealed class MatchSideController
{
    public bool AreSidesSwitched { get; private set; }

    public bool HomeAttacksLeft => !AreSidesSwitched;

    public void Reset()
    {
        AreSidesSwitched = false;
    }

    public void SwitchEnds()
    {
        AreSidesSwitched = !AreSidesSwitched;
    }

    public float AttackDirection(StringName teamId, StringName homeTeamId)
    {
        bool isHomeTeam = teamId == homeTeamId;
        return isHomeTeam == HomeAttacksLeft ? -1f : 1f;
    }

    public float OwnGoalX(StringName teamId, StringName homeTeamId)
    {
        return AttackDirection(teamId, homeTeamId) > 0f ? 0.015f : 0.985f;
    }

    public float AttackingGoalX(StringName teamId, StringName homeTeamId)
    {
        return AttackDirection(teamId, homeTeamId) > 0f ? 0.994f : 0.006f;
    }

    public bool ShouldMirrorFormation(StringName teamId, StringName homeTeamId)
    {
        bool isHomeTeam = teamId == homeTeamId;
        return isHomeTeam ? AreSidesSwitched : !AreSidesSwitched;
    }
}
