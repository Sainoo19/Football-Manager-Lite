using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MatchPitch2D
{
    private void DecideNextAction()
    {
        if (Simulation is null || Simulation.is_finished || _ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return;
        _decisionSerial++;
        _decisionsSinceShot++;
        StringName ownerId = _ballOwnerId;
        if (_playerTeams[ownerId] != _activeTeamId)
        {
            _activeTeamId = _playerTeams[ownerId];
            Simulation.set_live_possession(_activeTeamId);
            SelectPhasePlayers();
        }

        StringName nearestOpponent = NearestOpponent(ownerId);
        float pressureDistance = nearestOpponent != new StringName()
            ? CurrentPositions[ownerId].DistanceTo(CurrentPositions[nearestOpponent])
            : 1f;
        if (pressureDistance < 0.068f && TryResolveTackle(ownerId, nearestOpponent, pressureDistance))
            return;

        FootballPlayer? owner = GetPlayer(ownerId);
        bool underPressure = pressureDistance < 0.105f;
        if (ShouldClearBall(ownerId, underPressure))
        {
            StartClearance(ownerId);
            return;
        }
        if (Simulation.use_live_pitch_events && ShouldShoot(ownerId, pressureDistance))
        {
            StartLiveShot(ownerId, pressureDistance);
            return;
        }
        if (_attackProgress > 0.62f &&
            _decisionsSinceShot >= 9 &&
            _primaryRunnerId != new StringName() &&
            ownerId != _primaryRunnerId &&
            !IsCurrentlyOffside(_primaryRunnerId))
        {
            StartPass(_primaryRunnerId, BallActionKind.ThroughBall);
            return;
        }
        bool widePlayer = _playerRoles[ownerId] is "LB" or "RB" or "LW" or "RW";
        if (_attackProgress > 0.68f && widePlayer)
        {
            StringName receiver = _primaryRunnerId != new StringName() && !IsCurrentlyOffside(_primaryRunnerId)
                ? _primaryRunnerId
                : ChoosePassTarget(false);
            StartPass(receiver, BallActionKind.Cross);
            return;
        }

        float dribbleIntent = DecisionRoll(ownerId, nearestOpponent, _decisionSerial);
        int dribbling = owner?.dribbling ?? 50;
        if ((!underPressure && dribbling >= 67 && dribbleIntent < 0.34f) ||
            (underPressure && dribbling >= 75 && dribbleIntent < 0.22f))
        {
            StartDribble(ownerId, underPressure);
            return;
        }

        StringName target = ChoosePassTarget(underPressure);
        if (target == new StringName())
        {
            StartDribble(ownerId, underPressure);
            return;
        }

        int creativeSkill = ((owner?.passing ?? 50) + (owner?.vision ?? 50)) / 2;
        BallActionKind kind = target == _primaryRunnerId && creativeSkill >= 68 && !underPressure
            ? BallActionKind.ThroughBall
            : BallActionKind.Pass;
        StartPass(target, kind);
    }

    private bool ShouldClearBall(StringName ownerId, bool underPressure)
    {
        string role = _playerRoles[ownerId];
        bool defensiveRole = role is "GK" or "CB" or "LB" or "RB" or "DM";
        if (!defensiveRole || _attackProgress > 0.40f)
        {
            return false;
        }

        float clearanceIntent = DecisionRoll(ownerId, _pressingPlayerId, _decisionSerial + 307);
        float threshold = underPressure ? 0.62f : role == "GK" ? 0.34f : 0.18f;
        return clearanceIntent < threshold;
    }

    private void StartClearance(StringName playerId)
    {
        Vector2 start = CurrentPositions[playerId];
        float direction = AttackDirection(_playerTeams[playerId]);
        float distance = Mathf.Lerp(0.28f, 0.46f, DecisionRoll(playerId, _pressingPlayerId, _decisionSerial + 311));
        float targetLane = Mathf.Lerp(0.16f, 0.84f, DecisionRoll(playerId, _activeTeamId, _decisionSerial + 313));
        Vector2 destination = ClampToPitch(new Vector2(start.X + direction * distance, targetLane));
        float flightDistance = FootballPitchDimensions.DistanceMeters(start, destination);
        float duration = Mathf.Clamp(flightDistance / 30f, 0.55f, 1.35f);
        Clearances++;
        StartBallAction(destination, duration, 0.075f, new StringName(), BallActionKind.Clearance);
        SetAction($"{PlayerName(playerId)} phá bóng lên khoảng trống");
    }

    private void StartDribble(StringName ownerId, bool escapingPressure)
    {
        FootballPlayer? player = GetPlayer(ownerId);
        float quality = ((player?.dribbling ?? 50) + (player?.pace ?? 50)) / 198f;
        _attackProgress = Mathf.Clamp(_attackProgress + Mathf.Lerp(0.035f, 0.075f, quality), 0.12f, 0.94f);
        _phaseLane = Mathf.Lerp(_phaseLane, CurrentPositions[ownerId].Y, 0.55f);
        Dribbles++;
        _nextDecisionTime = _visualTime + (escapingPressure ? 0.48f : 0.62f);
        SetAction(escapingPressure
            ? $"{PlayerName(ownerId)} thoát pressing"
            : $"{PlayerName(ownerId)} dẫn bóng lên phía trước");
    }

    private bool ShouldShoot(StringName shooterId, float pressureDistance)
    {
        if (_attackProgress < 0.66f)
            return false;
        string role = _playerRoles[shooterId];
        if (role is "GK" or "CB" or "LB" or "RB" or "DM")
            return false;
        if (_decisionsSinceShot >= 10)
            return true;
        FootballPlayer? shooter = GetPlayer(shooterId);
        float chance = 0.16f + (_attackProgress - 0.66f) * 1.35f + ((shooter?.finishing ?? 50) - 65) / 180f;
        if (pressureDistance < 0.08f) chance -= 0.09f;
        if (role == "ST") chance += 0.12f;
        return DecisionRoll(shooterId, _pressingPlayerId, _decisionSerial + 73) < Mathf.Clamp(chance, 0.10f, 0.72f);
    }

    private void StartLiveShot(StringName shooterId, float pressureDistance)
    {
        if (Simulation is null)
            return;
        StringName shootingTeamId = _playerTeams[shooterId];
        bool shootingHome = shootingTeamId == Simulation.home.team.id;
        StringName defendingTeamId = shootingHome ? Simulation.away.team.id : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        Vector2 shooterPosition = CurrentPositions[shooterId];
        float attackDirection = AttackDirection(shootingTeamId);
        float goalX = AttackingGoalX(shootingTeamId);
        float targetY = 0.42f + DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 101) * 0.16f;
        Vector2 goalTarget = new(goalX, targetY);
        FootballPlayer? shooter = GetPlayer(shooterId);
        FootballPlayer? goalkeeper = GetPlayer(goalkeeperId);

        StringName blockerId = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == defendingTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => DistanceToSegment(CurrentPositions[id], shooterPosition, goalTarget))
            .FirstOrDefault() ?? new StringName();
        float blockerDistance = blockerId != new StringName()
            ? DistanceToSegment(CurrentPositions[blockerId], shooterPosition, goalTarget)
            : 1f;
        float blockChance = blockerId == new StringName() ? 0 : Mathf.Clamp(
            0.08f + (0.075f - blockerDistance) * 5.2f + ((GetPlayer(blockerId)?.positioning ?? 50) - 65) / 210f,
            0, 0.58f);

        string outcome;
        Vector2 destination;
        StringName nextOwner = goalkeeperId;
        if (DecisionRoll(shooterId, blockerId, _decisionSerial + 131) < blockChance)
        {
            bool deflectsForCorner = DecisionRoll(shooterId, blockerId, _decisionSerial + 139) < 0.22f;
            outcome = deflectsForCorner ? "blocked_corner" : "blocked";
            destination = deflectsForCorner
                ? new Vector2(goalX, CurrentPositions[blockerId].Y < 0.5f ? 0.03f : 0.97f)
                : CurrentPositions[blockerId] + new Vector2(attackDirection * 0.055f, 0.035f);
            nextOwner = new StringName();
        }
        else
        {
            float goalDistance = Math.Abs(shooterPosition.X - goalX);
            float anglePenalty = Math.Abs(shooterPosition.Y - 0.5f) * 0.42f;
            float accuracy = Mathf.Clamp(
                0.52f + ((shooter?.finishing ?? 50) - 65) / 115f - goalDistance * 0.28f -
                anglePenalty - (pressureDistance < 0.08f ? 0.10f : 0),
                0.24f, 0.86f);
            if (DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 151) > accuracy)
            {
                outcome = "off_target";
                destination = new Vector2(goalX, targetY < 0.5f ? 0.27f : 0.73f);
                nextOwner = new StringName();
            }
            else
            {
                float shotQuality = ((shooter?.finishing ?? 50) * 0.58f + (shooter?.positioning ?? 50) * 0.22f +
                                     (shooter?.form ?? 50) * 0.20f);
                float keeperQuality = (goalkeeper?.goalkeeping ?? 55) * 0.78f + (goalkeeper?.form ?? 50) * 0.22f;
                float goalChance = Mathf.Clamp(
                    0.30f + (shotQuality - keeperQuality) / 125f + (0.30f - goalDistance) * 0.42f -
                    (pressureDistance < 0.08f ? 0.08f : 0),
                    0.10f, 0.68f);
                bool goal = DecisionRoll(shooterId, goalkeeperId, _decisionSerial + 181) < goalChance;
                if (goal)
                {
                    outcome = "goal";
                    destination = goalTarget;
                    nextOwner = new StringName();
                }
                else
                {
                    float handling = Mathf.Clamp(0.48f + ((goalkeeper?.goalkeeping ?? 55) - 65) / 120f, 0.30f, 0.78f);
                    bool holdsBall = DecisionRoll(goalkeeperId, shooterId, _decisionSerial + 197) < handling;
                    bool parriesForCorner = !holdsBall && DecisionRoll(goalkeeperId, shooterId, _decisionSerial + 211) < 0.24f;
                    outcome = holdsBall ? "saved" : parriesForCorner ? "parried_corner" : "parried";
                    destination = holdsBall
                        ? CurrentPositions.GetValueOrDefault(goalkeeperId, goalTarget)
                        : parriesForCorner
                            ? new Vector2(goalX, targetY < 0.5f ? 0.03f : 0.97f)
                            : new Vector2(
                                attackDirection < 0f ? 0.15f : 0.85f,
                                Mathf.Clamp(targetY + (targetY < 0.5f ? 0.13f : -0.13f), 0.16f, 0.84f));
                    nextOwner = holdsBall ? goalkeeperId : new StringName();
                }
            }
        }

        _pendingShotOutcome = outcome;
        _decisionsSinceShot = 0;
        _pendingShotShooterId = shooterId;
        _pendingShotGoalkeeperId = goalkeeperId;
        _pendingShotBlockerId = blockerId;
        StartBallAction(destination, 0.46f, 0.012f, nextOwner, BallActionKind.Shot);
        SetAction($"{PlayerName(shooterId)} tung cú sút");
    }

    private bool TryResolveTackle(StringName ownerId, StringName defenderId, float distance)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float tackleSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float controlSkill = ((owner?.dribbling ?? 50) + (owner?.pace ?? 50)) * 0.5f;
        float contactBonus = Mathf.Clamp((0.068f - distance) / 0.068f, 0, 1) * 0.28f;
        float chance = Mathf.Clamp(0.22f + (tackleSkill - controlSkill) / 145f + contactBonus, 0.08f, 0.72f);
        float tackleRoll = DecisionRoll(ownerId, defenderId, _decisionSerial + 41);
        if (tackleRoll >= chance)
        {
            float foulChance = Mathf.Clamp(
                0.10f + (controlSkill - tackleSkill) / 210f + contactBonus * 0.30f,
                0.05f, 0.42f);
            if (DecisionRoll(defenderId, ownerId, _decisionSerial + 59) < foulChance)
            {
                ResolveLiveFoul(defenderId, ownerId);
                return true;
            }
            return false;
        }

        _ballOwnerId = defenderId;
        _activeTeamId = _playerTeams[defenderId];
        Simulation!.set_live_possession(_activeTeamId);
        _attackProgress = Mathf.Clamp(AttackProgress(_activeTeamId, BallPosition), 0.16f, 0.62f);
        _phaseLane = CurrentPositions[defenderId].Y;
        Interceptions++;
        SelectPhasePlayers();
        _nextDecisionTime = _visualTime + 0.38f;
        SetAction($"{PlayerName(defenderId)} đoạt bóng từ {PlayerName(ownerId)}");
        return true;
    }

    private void ResolveLiveFoul(StringName offenderId, StringName victimId)
    {
        if (Simulation is null)
            return;
        StringName foulingTeamId = _playerTeams[offenderId];
        StringName victimTeamId = _playerTeams[victimId];
        FootballPlayer? offender = GetPlayer(offenderId);
        float cardRoll = DecisionRoll(offenderId, victimId, _decisionSerial + 83);
        bool stopsClearChance = _attackProgress > 0.78f && _playerRoles[victimId] is "ST" or "LW" or "RW" or "AM";
        StringName card = stopsClearChance && cardRoll < 0.18f
            ? "red"
            : cardRoll < Mathf.Clamp(0.30f + (68 - (offender?.tackling ?? 50)) / 120f, 0.18f, 0.58f)
                ? "yellow"
                : new StringName();

        FootballMatchEvent? foulEvent = Simulation.register_live_foul(
            foulingTeamId, offenderId, victimId, card);
        if (foulEvent is not null)
            EmitSignal(SignalName.LiveMatchEvent, foulEvent);
        FoulsCommitted++;
        if (card == "red")
            SyncLineups(false);
        ScheduleRestart("free_kick", victimTeamId, BallPosition);
        SetAction(card == "red"
            ? $"{PlayerName(offenderId)} bị truất quyền thi đấu"
            : $"{PlayerName(offenderId)} phạm lỗi với {PlayerName(victimId)}");
    }

    private StringName ChoosePassTarget(bool preferSafe = false)
    {
        if (Simulation is null)
            return new StringName();
        if (_ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return new StringName();

        float direction = AttackDirection(_activeTeamId);
        Vector2 owner = CurrentPositions[_ballOwnerId];
        int ownerVision = GetPlayer(_ballOwnerId)?.vision ?? 50;
        StringName bestId = new();
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == _ballOwnerId || _playerTeams[candidateId] != _activeTeamId || _playerRoles[candidateId] == "GK")
                continue;
            bool candidateIsOffside = IsCurrentlyOffside(candidateId);
            Vector2 candidate = TargetPositions.GetValueOrDefault(candidateId, CurrentPositions[candidateId]);
            float distance = owner.DistanceTo(candidate);
            if (distance > 0.48f || distance < 0.045f)
                continue;
            float forwardGain = direction * (candidate.X - owner.X);
            float laneRisk = PassingLaneRisk(owner, candidate, _activeTeamId);
            float receivingPressure = SpaceEvaluator.OpponentPressure(
                candidate,
                _activeTeamId,
                CurrentPositions,
                _playerTeams);
            float forwardWeight = preferSafe ? 1.35f : 2.7f;
            float score = forwardGain * forwardWeight - distance * 0.42f -
                          receivingPressure * (preferSafe ? 0.72f : 0.46f) -
                          laneRisk * (preferSafe ? 1.45f : 0.82f);
            if (candidateIsOffside)
            {
                score -= Mathf.Lerp(0.62f, 1.25f, ownerVision / 99f);
            }
            if (_playerIntents.TryGetValue(candidateId, out PlayerIntent? intent))
            {
                score += intent.Kind switch
                {
                    PlayerIntentKind.ReceivePass => 0.34f,
                    PlayerIntentKind.RunIntoSpace => _attackProgress > 0.52f ? 0.28f : 0.10f,
                    PlayerIntentKind.SupportBall => preferSafe ? 0.24f : 0.12f,
                    _ => 0f
                };
            }
            if (score <= bestScore) continue;
            bestScore = score;
            bestId = candidateId;
        }
        return bestId;
    }

    private bool IsCurrentlyOffside(StringName playerId)
    {
        return _offsideRule.IsOffside(
            playerId,
            _activeTeamId,
            BallPosition,
            AttackDirection(_activeTeamId),
            CurrentPositions,
            _playerTeams);
    }
}
