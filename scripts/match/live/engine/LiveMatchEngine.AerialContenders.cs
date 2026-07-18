using System.Collections.Generic;
using Godot;

public sealed partial class LiveMatchEngine
{
    private const int AerialContendersPerTeam = 2;

    private void PrepareAerialContenders()
    {
        _aerialContenderIds.Clear();
        if (!_aerialFlightActive || Simulation is null)
        {
            return;
        }

        AddTeamAerialContenders(Simulation.home.team.id);
        AddTeamAerialContenders(Simulation.away.team.id);
    }

    private void AddTeamAerialContenders(StringName teamId)
    {
        List<(StringName PlayerId, AerialArrivalEstimate Arrival)> candidates = new();
        StringName goalkeeperId = new();
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] != teamId)
            {
                continue;
            }
            if (_playerRoles[playerId] == "GK")
            {
                goalkeeperId = playerId;
                continue;
            }

            AerialArrivalEstimate arrival = _aerialLandingPredictor.Estimate(
                CurrentPositions[playerId],
                _aerialTrajectory.LandingPoint,
                _playerPaces[playerId],
                _aerialTrajectory.FlightTimeSeconds);
            candidates.Add((playerId, arrival));
        }
        candidates.Sort((first, second) =>
            first.Arrival.ArrivalTimeSeconds.CompareTo(second.Arrival.ArrivalTimeSeconds));

        int added = 0;
        foreach ((StringName playerId, AerialArrivalEstimate arrival) in candidates)
        {
            if (!arrival.CanContest && added > 0)
            {
                continue;
            }
            _aerialContenderIds.Add(playerId);
            added++;
            if (added >= AerialContendersPerTeam)
            {
                break;
            }
        }

        if (goalkeeperId == new StringName() ||
            !IsInsideOwnPenaltyArea(_aerialTrajectory.LandingPoint, teamId))
        {
            return;
        }
        AerialArrivalEstimate goalkeeperArrival = _aerialLandingPredictor.Estimate(
            CurrentPositions[goalkeeperId],
            _aerialTrajectory.LandingPoint,
            _playerPaces[goalkeeperId],
            _aerialTrajectory.FlightTimeSeconds);
        if (goalkeeperArrival.CanContest)
        {
            _aerialContenderIds.Add(goalkeeperId);
        }
    }

    private void ApplyAerialContestTargets()
    {
        if (!_aerialFlightActive)
        {
            return;
        }

        foreach (StringName playerId in _aerialContenderIds)
        {
            if (!CurrentPositions.ContainsKey(playerId))
            {
                continue;
            }
            PlayerIntentKind kind = _playerRoles[playerId] == "GK"
                ? PlayerIntentKind.ClaimAerialBall
                : PlayerIntentKind.ContestAerialBall;
            LiveTeamPhase phase = _playerTeams[playerId] == _actionSourceTeamId
                ? LiveTeamPhase.BallInFlight
                : LiveTeamPhase.Defending;
            _playerIntents[playerId] = new PlayerIntent(
                kind,
                _aerialTrajectory.LandingPoint,
                phase,
                _actionSourceId);
            TargetPositions[playerId] = _aerialTrajectory.LandingPoint;
        }
    }
}
