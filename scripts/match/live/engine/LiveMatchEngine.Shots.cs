using System.Linq;
using Godot;

public sealed partial class LiveMatchEngine
{
    private bool ShouldShoot(StringName shooterId, float pressureDistanceMeters)
    {
        if (Simulation is null)
        {
            return false;
        }

        FootballPlayer? shooter = GetPlayer(shooterId);
        Vector2 shooterPosition = CurrentPositions[shooterId];
        Vector2 goalCenter = new(AttackingGoalX(_playerTeams[shooterId]), 0.5f);
        float distanceMeters = FootballPitchDimensions.DistanceMeters(shooterPosition, goalCenter);
        return _shotDecisionEvaluator.ShouldShoot(
            _playerRoles[shooterId],
            shooter?.finishing ?? 50,
            distanceMeters,
            pressureDistanceMeters,
            _decisionsSinceShot,
            DecisionRoll(shooterId, _pressingPlayerId, _decisionSerial + 73));
    }

    private void StartLiveShot(
        StringName shooterId,
        float pressureDistanceMeters,
        bool isHeader = false)
    {
        if (Simulation is null)
        {
            return;
        }

        ResetCarrySequence();
        StringName shootingTeamId = _playerTeams[shooterId];
        StringName defendingTeamId = shootingTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        Vector2 shooterPosition = CurrentPositions[shooterId];
        float attackDirection = AttackDirection(shootingTeamId);
        float goalX = AttackingGoalX(shootingTeamId);
        _pendingShotDistanceMeters = FootballPitchDimensions.DistanceMeters(
            shooterPosition,
            new Vector2(goalX, 0.5f));
        _pendingShotSituation = isHeader ? "header" : "open_play";
        Vector2 goalkeeperPosition = CurrentPositions.TryGetValue(goalkeeperId, out Vector2 currentGoalkeeperPosition)
            ? currentGoalkeeperPosition
            : new Vector2(goalX, 0.5f);
        Vector2 goalTarget = _shotTargetPlanner.ChooseGoalTarget(
            goalX,
            goalkeeperPosition,
            DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 101));
        float targetY = goalTarget.Y;
        FootballPlayer? shooter = GetPlayer(shooterId);
        FootballPlayer? goalkeeper = GetPlayer(goalkeeperId);

        StringName blockerId = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == defendingTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => DistanceToSegment(
                FootballPitchDimensions.ToMeters(CurrentPositions[id]),
                FootballPitchDimensions.ToMeters(shooterPosition),
                FootballPitchDimensions.ToMeters(goalTarget)))
            .FirstOrDefault() ?? new StringName();
        float blockerDistanceMeters = blockerId != new StringName()
            ? DistanceToSegment(
                FootballPitchDimensions.ToMeters(CurrentPositions[blockerId]),
                FootballPitchDimensions.ToMeters(shooterPosition),
                FootballPitchDimensions.ToMeters(goalTarget))
            : float.PositiveInfinity;
        float blockChance = blockerId == new StringName()
            ? 0f
            : Mathf.Clamp(
                0.06f + (2.2f - blockerDistanceMeters) * 0.16f +
                ((GetPlayer(blockerId)?.positioning ?? 50) - 65) / 220f,
                0f,
                0.48f);

        string outcome;
        Vector2 destination;
        StringName nextOwner = new();
        if (DecisionRoll(shooterId, blockerId, _decisionSerial + 131) < blockChance)
        {
            bool deflectsForCorner = DecisionRoll(shooterId, blockerId, _decisionSerial + 139) <
                                     _configuration.BlockedShotCornerProbability;
            outcome = deflectsForCorner ? "blocked_corner" : "blocked";
            destination = deflectsForCorner
                ? new Vector2(goalX, CurrentPositions[blockerId].Y < 0.5f ? 0.03f : 0.97f)
                : CurrentPositions[blockerId] + new Vector2(attackDirection * 0.025f, 0.015f);
        }
        else
        {
            float distanceMeters = FootballPitchDimensions.DistanceMeters(shooterPosition, goalTarget);
            float angleFactor = Mathf.Clamp(Mathf.Abs(shooterPosition.Y - 0.5f) * 2f, 0f, 1f);
            float goalkeeperCoverage = _shotTargetPlanner.GoalkeeperCoverage(
                shooterPosition,
                goalTarget,
                goalkeeperPosition);
            float accuracyRoll = DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 151);
            int finishingRating = isHeader
                ? Mathf.RoundToInt((shooter?.Heading ?? 50) * 0.68f +
                                   (shooter?.finishing ?? 50) * 0.32f)
                : shooter?.finishing ?? 50;
            ShotOutcome resolution = _shotOutcomeResolver.Resolve(
                finishingRating,
                shooter?.positioning ?? 50,
                shooter?.form ?? 50,
                goalkeeper?.goalkeeping ?? 55,
                goalkeeper?.form ?? 50,
                distanceMeters,
                angleFactor,
                pressureDistanceMeters,
                goalkeeperCoverage,
                accuracyRoll,
                DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 181),
                DecisionRoll(goalkeeperId, shooterId, _decisionSerial + 197),
                DecisionRoll(goalkeeperId, shooterId, _decisionSerial + 211));
            outcome = resolution switch
            {
                ShotOutcome.Goal => "goal",
                ShotOutcome.Saved => "saved",
                ShotOutcome.Parried => "parried",
                ShotOutcome.ParriedCorner => "parried_corner",
                _ => "off_target"
            };
            destination = resolution switch
            {
                ShotOutcome.Goal => goalTarget,
                ShotOutcome.Saved => new Vector2(goalX - attackDirection * (1.5f / FootballPitchDimensions.LengthMeters), targetY),
                ShotOutcome.ParriedCorner => new Vector2(goalX, targetY < 0.5f ? 0.03f : 0.97f),
                ShotOutcome.Parried => new Vector2(
                    attackDirection < 0f ? 0.13f : 0.87f,
                    Mathf.Clamp(targetY + (targetY < 0.5f ? 0.12f : -0.12f), 0.18f, 0.82f)),
                _ => _shotTargetPlanner.ChooseOffTargetDestination(
                    goalX,
                    targetY,
                    shooter?.finishing ?? 50,
                    distanceMeters,
                    accuracyRoll)
            };
            nextOwner = resolution == ShotOutcome.Saved ? goalkeeperId : new StringName();
        }

        float shotDistanceMeters = FootballPitchDimensions.DistanceMeters(shooterPosition, destination);
        _pendingShotOutcome = outcome;
        _decisionsSinceShot = 0;
        _pendingShotShooterId = shooterId;
        _pendingShotGoalkeeperId = goalkeeperId;
        _pendingShotBlockerId = blockerId;
        StartBallAction(
            destination,
            Mathf.Clamp(shotDistanceMeters / 28f, 0.36f, 1.05f),
            0.012f,
            nextOwner,
            BallActionKind.Shot);
        SetAction(isHeader
            ? $"{PlayerName(shooterId)} bật cao đánh đầu dứt điểm"
            : $"{PlayerName(shooterId)} tung cú sút");
    }
}
