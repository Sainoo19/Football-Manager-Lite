using System;
using Godot;

public sealed class OffBallParticipationSizer
{
    public int SupportCount(
        FootballWorldSnapshot world,
        LiveTeamPhase phase,
        int regionalAdvantage,
        int availableCount)
    {
        int count = phase switch
        {
            LiveTeamPhase.BuildUp => 4,
            LiveTeamPhase.CounterAttack => regionalAdvantage > 0 ? 2 : 3,
            LiveTeamPhase.FinalThird => 2,
            LiveTeamPhase.TransitionToAttack => 4,
            _ => 3
        };
        float pressure = SpaceEvaluator.OpponentPressure(
            world.BallPosition,
            world.PossessionTeamId,
            world.Positions,
            world.PlayerTeams);
        if (pressure > 0.65f)
        {
            count++;
        }
        return Math.Min(count, availableCount);
    }

    public int RunnerCount(LiveTeamPhase phase, int regionalAdvantage, int availableCount)
    {
        int count = phase switch
        {
            LiveTeamPhase.CounterAttack => regionalAdvantage >= 0 ? 3 : 2,
            LiveTeamPhase.FinalThird => 4,
            LiveTeamPhase.Progression => regionalAdvantage > 0 ? 3 : 2,
            LiveTeamPhase.BuildUp => 1,
            _ => 2
        };
        return Math.Min(count, Math.Max(availableCount, 0));
    }

    public int RegionalAdvantage(FootballWorldSnapshot world, StringName teamId)
    {
        int teammates = 0;
        int opponents = 0;
        foreach ((StringName playerId, Vector2 position) in world.Positions)
        {
            if (world.PlayerRoles[playerId] == "GK" ||
                FootballPitchDimensions.DistanceMeters(position, world.BallPosition) > 24f)
            {
                continue;
            }
            if (world.PlayerTeams[playerId] == teamId)
            {
                teammates++;
            }
            else
            {
                opponents++;
            }
        }
        return teammates - opponents;
    }
}
