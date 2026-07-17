using System.Linq;
using Godot;

public partial class MatchPitch2D
{
    private void StartLooseBall(
        string description = "Bóng bật ra — hai đội tranh bóng hai",
        Vector2 initialVelocityMetersPerSecond = default)
    {
        ClearDirectAttack();
        _ballOwnerId = new StringName();
        _looseBallActive = true;
        _restartPending = false;
        _looseBallVelocityMetersPerSecond = initialVelocityMetersPerSecond;
        _looseBallResolveTime = _visualTime + 0.12f;
        SetAction(description);
    }

    private void ResolveLooseBall()
    {
        if (Simulation is null)
            return;
        StringName claimingGoalkeeperId = FindClaimingGoalkeeper();
        if (claimingGoalkeeperId != new StringName())
        {
            _looseBallActive = false;
            LooseBallRecoveries++;
            GivePossessionTo(claimingGoalkeeperId, 0.75f);
            SetAction($"{PlayerName(claimingGoalkeeperId)} lao ra ôm gọn bóng tự do");
            return;
        }

        var candidates = CurrentPositions.Keys
            .Select(id => new
            {
                Id = id,
                DistanceSquaredMeters = PlayerProximity.DistanceSquaredMeters(CurrentPositions[id], BallPosition)
            })
            .OrderBy(candidate => candidate.DistanceSquaredMeters)
            .ToList();
        if (candidates.Count == 0)
            return;
        StringName winnerId = candidates[0].Id;
        const float controlDistanceMeters = 1.4f;
        float winnerDistanceMeters = FootballPitchDimensions.DistanceMeters(CurrentPositions[winnerId], BallPosition);
        if (winnerDistanceMeters > controlDistanceMeters)
        {
            _looseBallResolveTime = _visualTime + 0.10f;
            return;
        }
        _looseBallActive = false;
        LooseBallRecoveries++;
        GivePossessionTo(winnerId, 0.26f);
        SetAction($"{PlayerName(winnerId)} giành được bóng hai");
    }

    private StringName FindClaimingGoalkeeper()
    {
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerRoles[playerId] != "GK")
            {
                continue;
            }

            Vector2 ownGoal = new(OwnGoalX(_playerTeams[playerId]), 0.5f);
            if (_traditionalGoalkeeperPlanner.CanCollectLooseBall(
                    BallPosition,
                    CurrentPositions[playerId],
                    ownGoal))
            {
                return playerId;
            }
        }

        return new StringName();
    }

    private Vector2 RollingVelocityAfterFlight(BallActionKind completedKind)
    {
        Vector2 flightDirectionMeters = FootballPitchDimensions.ToMeters(_ballActionTo) -
                                        FootballPitchDimensions.ToMeters(_ballActionFrom);
        if (flightDirectionMeters.LengthSquared() <= 0.001f)
        {
            return Vector2.Zero;
        }

        float rollingSpeed = completedKind switch
        {
            BallActionKind.ThroughBall => 6.8f,
            BallActionKind.Cross => 6.2f,
            BallActionKind.Clearance => 7.4f,
            BallActionKind.Shot => 6.5f,
            _ => 5.6f
        };
        return flightDirectionMeters.Normalized() * rollingSpeed;
    }

    private void AdvanceRollingBall(float delta)
    {
        RollingBallStep step = _rollingBallPhysics.Advance(
            BallPosition,
            _looseBallVelocityMetersPerSecond,
            delta);
        _looseBallVelocityMetersPerSecond = step.VelocityMetersPerSecond;
        if (step.Position.X is < 0f or > 1f || step.Position.Y is < 0f or > 1f)
        {
            ResolveRollingBallOut(step.Position);
            return;
        }

        BallPosition = step.Position;
    }

    private void ResolveRollingBallOut(Vector2 outPosition)
    {
        if (Simulation is null || _actionSourceTeamId == new StringName())
        {
            BallPosition = ClampToPitch(outPosition);
            _looseBallVelocityMetersPerSecond = Vector2.Zero;
            return;
        }

        StringName opposingTeamId = _actionSourceTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        if (outPosition.Y is < 0f or > 1f)
        {
            Vector2 throwPosition = new(
                Mathf.Clamp(outPosition.X, 0.025f, 0.975f),
                outPosition.Y < 0f ? 0.035f : 0.965f);
            ScheduleRestart("throw_in", opposingTeamId, throwPosition);
            SetAction("Bóng lăn hết đường biên — đối phương được ném biên");
            return;
        }

        bool leftGoalLine = outPosition.X < 0f;
        StringName defendingGoalTeamId = OwnGoalX(Simulation.home.team.id) < 0.5f == leftGoalLine
            ? Simulation.home.team.id
            : Simulation.away.team.id;
        Vector2 goalLinePosition = new(
            leftGoalLine ? 0.018f : 0.982f,
            Mathf.Clamp(outPosition.Y, 0.035f, 0.965f));
        if (_actionSourceTeamId == defendingGoalTeamId)
        {
            StringName cornerTeamId = defendingGoalTeamId == Simulation.home.team.id
                ? Simulation.away.team.id
                : Simulation.home.team.id;
            ScheduleRestart("corner", cornerTeamId, goalLinePosition);
            SetAction("Bóng chạm đội phòng ngự đi hết biên ngang — phạt góc");
        }
        else
        {
            ScheduleRestart("goal_kick", defendingGoalTeamId, GoalKickPosition(defendingGoalTeamId));
            SetAction("Bóng lăn hết biên ngang — phát bóng lên");
        }
    }
}
