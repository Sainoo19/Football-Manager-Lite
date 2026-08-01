using System;
using System.Collections.Generic;
using Godot;

public sealed partial class LiveMatchEngine
{
    private Vector2 _lastPhaseBallPosition = new(0.5f, 0.5f);
    private float _lastPhaseObservationTime;

    public IReadOnlyDictionary<StringName, TeamPhaseState> CurrentTeamPhaseStates =>
        _teamPhaseCoordinator.CreateStateSnapshot();

    public TeamPhaseMetricsSnapshot TeamPhaseMetrics =>
        _teamPhaseCoordinator.CreateMetricsSnapshot((float)_simulationTimeSeconds);

    private void ResetTeamPhases()
    {
        if (Simulation is null)
        {
            return;
        }
        _lastPhaseBallPosition = BallPosition;
        _lastPhaseObservationTime = (float)_simulationTimeSeconds;
        _teamPhaseCoordinator.Reset(
            new[] { Simulation.home.team.id, Simulation.away.team.id },
            _state.ActiveTeamId,
            (float)_simulationTimeSeconds);
        UpdateTeamPhases();
    }

    private void UpdateTeamPhases()
    {
        if (Simulation is null || CurrentPositions.Count == 0)
        {
            return;
        }

        float currentTime = (float)_simulationTimeSeconds;
        float elapsed = Math.Max(currentTime - _lastPhaseObservationTime, 0f);
        Vector2 previousBallMeters = FootballPitchDimensions.ToMeters(_lastPhaseBallPosition);
        Vector2 currentBallMeters = FootballPitchDimensions.ToMeters(BallPosition);
        foreach (StringName teamId in new[] { Simulation.home.team.id, Simulation.away.team.id })
        {
            float direction = AttackDirection(teamId);
            float progressionSpeed = elapsed <= 0.0001f
                ? 0f
                : direction * (currentBallMeters.X - previousBallMeters.X) / elapsed;
            _teamPhaseCoordinator.Update(CreateTeamPhaseContext(teamId, progressionSpeed));
        }
        _lastPhaseBallPosition = BallPosition;
        _lastPhaseObservationTime = currentTime;
    }

    private TeamPhaseContext CreateTeamPhaseContext(StringName teamId, float progressionSpeed)
    {
        bool hasPossession = _state.ActiveTeamId == teamId && !_state.IsLooseBallActive;
        float direction = AttackDirection(teamId);
        int ahead = 0;
        int behind = 0;
        int nearbyTeammates = 0;
        int nearbyOpponents = 0;
        float minimumX = 1f;
        float maximumX = 0f;
        float minimumY = 1f;
        float maximumY = 0f;
        foreach ((StringName playerId, Vector2 position) in CurrentPositions)
        {
            bool isTeammate = _playerTeams[playerId] == teamId;
            if (isTeammate)
            {
                if (_playerRoles[playerId] != "GK")
                {
                    if (direction * (position.X - BallPosition.X) > 0f)
                    {
                        ahead++;
                    }
                    else
                    {
                        behind++;
                    }
                }
                minimumX = Math.Min(minimumX, position.X);
                maximumX = Math.Max(maximumX, position.X);
                minimumY = Math.Min(minimumY, position.Y);
                maximumY = Math.Max(maximumY, position.Y);
            }

            if (FootballPitchDimensions.DistanceMeters(position, BallPosition) <= 18f &&
                _playerRoles[playerId] != "GK")
            {
                if (isTeammate)
                {
                    nearbyTeammates++;
                }
                else
                {
                    nearbyOpponents++;
                }
            }
        }

        Vector2 ownGoal = new(OwnGoalX(teamId), 0.5f);
        Vector2 forwardProbeMeters = FootballPitchDimensions.ToMeters(BallPosition) +
                                     new Vector2(direction * 8f, 0f);
        Vector2 forwardProbe = SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(forwardProbeMeters));
        float forwardSpace = SpaceEvaluator.NearestOpponentDistanceMeters(
            forwardProbe,
            teamId,
            CurrentPositions,
            _playerTeams);
        float compactness = FootballPitchDimensions.DistanceMeters(
            new Vector2(minimumX, minimumY),
            new Vector2(maximumX, maximumY));
        float possessionDuration = hasPossession && _possessionTeamId == teamId
            ? Math.Max(_state.VisualTime - _possessionSpellStartTime, 0f)
            : 0f;
        return new TeamPhaseContext(
            teamId,
            hasPossession,
            _state.IsRestartPending || _kickoffPassPending,
            _state.IsLooseBallActive,
            (float)_simulationTimeSeconds,
            AttackProgress(teamId, BallPosition),
            possessionDuration,
            ahead,
            behind,
            nearbyTeammates - nearbyOpponents,
            FootballPitchDimensions.DistanceMeters(BallPosition, ownGoal),
            forwardSpace,
            progressionSpeed,
            compactness);
    }

}
