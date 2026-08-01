using System;

public sealed class TeamPhaseCoordinator
{
    public float PlanningInterval(
        LiveMatchEngineConfiguration configuration,
        bool isLooseBall,
        bool isBallInFlight)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return isLooseBall
            ? configuration.LooseBallPlanningIntervalSeconds
            : isBallInFlight
                ? configuration.BallInFlightPlanningIntervalSeconds
                : configuration.PossessionIntentPlanningIntervalSeconds;
    }
}
