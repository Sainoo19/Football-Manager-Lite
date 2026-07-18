using Godot;

public sealed partial class LiveMatchEngine
{
    private bool TryAdvanceGroundDuel(
        StringName carrierId,
        StringName nearestDefenderId,
        float distanceMeters)
    {
        GroundDuelSequenceState sequence = _state.GroundDuel;
        if (sequence.HasCarrier && sequence.CarrierId != carrierId)
        {
            sequence.Reset();
        }
        if (nearestDefenderId == new StringName())
        {
            return false;
        }

        if (!sequence.HasCarrier && distanceMeters > DuelDistanceRules.TackleAttemptDistanceMeters)
        {
            return false;
        }
        if (!sequence.HasCarrier)
        {
            sequence.Begin(carrierId, nearestDefenderId, IsCarrierBackToGoal(carrierId, nearestDefenderId));
            RecordNextDribbleTouch(carrierId, nearestDefenderId, distanceMeters);
        }
        else if (!sequence.HasDefender || sequence.DefenderId != nearestDefenderId)
        {
            sequence.AttachDefender(nearestDefenderId);
        }

        if (distanceMeters > DuelDistanceRules.EngagementExitDistanceMeters)
        {
            sequence.AttachDefender(new StringName());
            return false;
        }
        if (sequence.ExchangeCount >= _configuration.MaximumUnresolvedGroundDuelExchanges)
        {
            sequence.Reset();
            StringName outletId = ChoosePassTarget(true);
            if (outletId != new StringName())
            {
                StartPass(outletId, BallActionKind.Pass);
                return true;
            }
            return false;
        }

        FootballPlayer? defender = GetPlayer(nearestDefenderId);
        float carrierSpeed = PlayerSpeed(carrierId);
        float defenderSpeed = PlayerSpeed(nearestDefenderId);
        bool challengeOnCooldown =
            _state.DefenderChallengeReadyTimes.TryGetValue(nearestDefenderId, out float challengeReadyTime) &&
            challengeReadyTime > _state.VisualTime;
        DefenderEngagementPlan engagement = _defenderEngagementPlanner.Plan(
            new DefenderEngagementContext(
                CurrentPositions[nearestDefenderId],
                CurrentPositions[carrierId],
                new Vector2(OwnGoalX(_playerTeams[nearestDefenderId]), 0.5f),
                distanceMeters,
                sequence.CurrentTouch.Type,
                sequence.ExchangeCount,
                defender?.tackling ?? 50,
                defender?.positioning ?? 50,
                defender?.Strength ?? 50,
                defender?.Balance ?? 50,
                carrierSpeed,
                defenderSpeed,
                challengeOnCooldown,
                sequence.IsBackToGoal,
                HasCoverFor(nearestDefenderId),
                DecisionRoll(nearestDefenderId, carrierId, _decisionSerial + 601),
                IsInsideOwnPenaltyArea(CurrentPositions[nearestDefenderId], _playerTeams[nearestDefenderId]),
                _configuration.PenaltyAreaChallengeProbability,
                Simulation?.get_state(_playerTeams[nearestDefenderId])?.YellowCardCount(nearestDefenderId) > 0,
                _configuration.BookedPlayerChallengeProbability));
        sequence.RecordEngagement(engagement);
        GroundDuelExchanges++;
        if (engagement.AttemptsChallenge && sequence.TouchCount >= 2)
        {
            ResolveGroundDuelChallenge(carrierId, nearestDefenderId, distanceMeters, engagement);
            return true;
        }

        if (sequence.ExchangeCount > 1)
        {
            RecordNextDribbleTouch(carrierId, nearestDefenderId, distanceMeters);
        }
        ApplyGroundDuelTargets();
        _nextDecisionTime = _state.VisualTime + sequence.CurrentTouch.DurationSeconds;
        SetAction(EngagementDescription(nearestDefenderId, carrierId, engagement.Type));
        return true;
    }

    private void StartDribbleTouch(StringName carrierId, bool escapingPressure)
    {
        StringName defenderId = NearestOpponent(carrierId);
        float distanceMeters = defenderId == new StringName()
            ? float.PositiveInfinity
            : FootballPitchDimensions.DistanceMeters(
                CurrentPositions[carrierId],
                CurrentPositions[defenderId]);
        GroundDuelSequenceState sequence = _state.GroundDuel;
        if (!sequence.HasCarrier || sequence.CarrierId != carrierId)
        {
            StringName engagedDefenderId = distanceMeters <= DuelDistanceRules.EngagementStartDistanceMeters
                ? defenderId
                : new StringName();
            sequence.Begin(
                carrierId,
                engagedDefenderId,
                engagedDefenderId != new StringName() && IsCarrierBackToGoal(carrierId, engagedDefenderId));
        }
        RecordNextDribbleTouch(carrierId, defenderId, distanceMeters);
        ApplyGroundDuelTargets();
        _nextDecisionTime = _state.VisualTime + sequence.CurrentTouch.DurationSeconds;
        SetAction(DribbleDescription(carrierId, sequence.CurrentTouch.Type, escapingPressure));
    }

    private void RecordNextDribbleTouch(
        StringName carrierId,
        StringName defenderId,
        float pressureDistanceMeters)
    {
        FootballPlayer? carrier = GetPlayer(carrierId);
        Vector2 defenderPosition = defenderId != new StringName() && CurrentPositions.ContainsKey(defenderId)
            ? CurrentPositions[defenderId]
            : CurrentPositions[carrierId] + new Vector2(AttackDirection(_playerTeams[carrierId]) * 0.10f, 0f);
        GroundDuelSequenceState sequence = _state.GroundDuel;
        DribbleTouchPlan touch = _dribbleTouchPlanner.Plan(
            new DribbleTouchContext(
                CurrentPositions[carrierId],
                defenderPosition,
                AttackDirection(_playerTeams[carrierId]),
                pressureDistanceMeters,
                PlayerSpeed(carrierId),
                carrier?.dribbling ?? 50,
                carrier?.Technique ?? 50,
                carrier?.pace ?? 50,
                carrier?.Strength ?? 50,
                carrier?.Balance ?? 50,
                carrier?.Agility ?? 50,
                sequence.IsBackToGoal,
                HasSupportOption(carrierId),
                sequence.TouchCount,
                DecisionRoll(carrierId, defenderId, _decisionSerial + 577 + sequence.TouchCount * 17)));
        sequence.RecordTouch(touch);
        MaxGroundDuelTouches = Mathf.Max(MaxGroundDuelTouches, sequence.TouchCount);
        if (_carryOwnerId == carrierId)
        {
            _consecutiveCarries++;
        }
        else
        {
            _carryOwnerId = carrierId;
            _consecutiveCarries = 1;
        }
        _attackProgress = Mathf.Clamp(AttackProgress(_playerTeams[carrierId], touch.Target), 0.08f, 0.96f);
        _phaseLane = touch.Target.Y;
    }

    private void ResolveGroundDuelChallenge(
        StringName carrierId,
        StringName defenderId,
        float distanceMeters,
        DefenderEngagementPlan engagement)
    {
        GroundDuelSequenceState sequence = _state.GroundDuel;
        FootballPlayer? carrier = GetPlayer(carrierId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float attackDirection = AttackDirection(_playerTeams[carrierId]);
        bool challengeFromBehind = attackDirection *
            (CurrentPositions[defenderId].X - CurrentPositions[carrierId].X) < 0f;
        GroundDuelResolution resolution = _groundDuelResolver.Resolve(
            new GroundDuelContext(
                engagement.Type,
                sequence.CurrentTouch,
                carrier?.dribbling ?? 50,
                carrier?.Technique ?? 50,
                carrier?.Strength ?? 50,
                carrier?.Balance ?? 50,
                carrier?.Agility ?? 50,
                defender?.tackling ?? 50,
                defender?.positioning ?? 50,
                defender?.Strength ?? 50,
                defender?.Balance ?? 50,
                PlayerSpeed(carrierId),
                PlayerSpeed(defenderId),
                distanceMeters,
                MovementAlignment(carrierId, defenderId),
                challengeFromBehind,
                DecisionRoll(defenderId, carrierId, _decisionSerial + 631),
                DecisionRoll(carrierId, defenderId, _decisionSerial + 647),
                DecisionRoll(defenderId, carrierId, _decisionSerial + 661)));
        if (engagement.Type == DefenderEngagementType.Tackle)
        {
            TackleAttempts++;
            _state.DefenderChallengeReadyTimes[defenderId] = _state.VisualTime + 1.20f;
        }
        else
        {
            ShoulderChallenges++;
            _state.DefenderChallengeReadyTimes[defenderId] = _state.VisualTime + 0.90f;
        }
        Dribbles++;

        switch (resolution.Outcome)
        {
            case GroundDuelOutcome.Foul:
                sequence.Reset();
                ResolveLiveFoul(defenderId, carrierId, distanceMeters);
                return;
            case GroundDuelOutcome.DefenderWins:
                CompleteDefenderWin(carrierId, defenderId);
                return;
            case GroundDuelOutcome.LooseBall:
                CompleteHeavyContactLooseBall(carrierId, defenderId, resolution.LooseBallSpeedMetersPerSecond);
                return;
            case GroundDuelOutcome.CarrierEscapes:
                CarrierEscapes++;
                sequence.AttachDefender(new StringName());
                RecordNextDribbleTouch(carrierId, defenderId, distanceMeters);
                ApplyGroundDuelTargets();
                _nextDecisionTime = _state.VisualTime + sequence.CurrentTouch.DurationSeconds;
                SetAction($"{PlayerName(carrierId)} đổi hướng vượt qua {PlayerName(defenderId)}");
                return;
            default:
                RecordNextDribbleTouch(carrierId, defenderId, distanceMeters);
                ApplyGroundDuelTargets();
                _nextDecisionTime = _state.VisualTime + sequence.CurrentTouch.DurationSeconds;
                SetAction($"{PlayerName(carrierId)} giữ được bóng sau pha tranh chấp");
                return;
        }
    }

    private void CompleteDefenderWin(StringName carrierId, StringName defenderId)
    {
        _state.GroundDuel.Reset();
        _carryOwnerId = new StringName();
        _consecutiveCarries = 0;
        _state.BallOwnerId = defenderId;
        SetTrackedPossession(_playerTeams[defenderId]);
        _attackProgress = Mathf.Clamp(AttackProgress(_state.ActiveTeamId, BallPosition), 0.16f, 0.62f);
        _phaseLane = CurrentPositions[defenderId].Y;
        Interceptions++;
        TacklesWon++;
        SelectPhasePlayers();
        _nextDecisionTime = _state.VisualTime + 0.42f;
        SetAction($"{PlayerName(defenderId)} canh đúng nhịp và lấy bóng từ {PlayerName(carrierId)}");
    }

    private void CompleteHeavyContactLooseBall(
        StringName carrierId,
        StringName defenderId,
        float looseBallSpeedMetersPerSecond)
    {
        Vector2 directionMeters = FootballPitchDimensions.ToMeters(_state.GroundDuel.CurrentTouch.Target) -
                                  FootballPitchDimensions.ToMeters(CurrentPositions[carrierId]);
        Vector2 direction = directionMeters.LengthSquared() > 0.001f
            ? directionMeters.Normalized()
            : new Vector2(AttackDirection(_playerTeams[carrierId]), 0f);
        GroundDuelLooseBalls++;
        ResetCarrySequence();
        StartLooseBall(
            $"{PlayerName(defenderId)} va chạm mạnh — bóng bật khỏi chân {PlayerName(carrierId)}",
            direction * looseBallSpeedMetersPerSecond);
    }

    private void ApplyGroundDuelTargets()
    {
        GroundDuelSequenceState sequence = _state.GroundDuel;
        if (!sequence.HasCarrier ||
            sequence.CarrierId != _state.BallOwnerId ||
            !CurrentPositions.ContainsKey(sequence.CarrierId))
        {
            return;
        }

        PlayerIntentKind carrierIntent = sequence.CurrentTouch.Type switch
        {
            DribbleTouchType.KnockOn => PlayerIntentKind.DribbleKnockOn,
            DribbleTouchType.ChangeDirection => PlayerIntentKind.DribbleChangeDirection,
            DribbleTouchType.Shield => PlayerIntentKind.ShieldBall,
            DribbleTouchType.HoldUp => PlayerIntentKind.HoldUpBall,
            _ => PlayerIntentKind.DribbleCloseControl
        };
        PlayerIntent carrierPlayerIntent = new(
            carrierIntent,
            sequence.CurrentTouch.Target,
            LiveTeamPhase.InPossession,
            sequence.DefenderId);
        _playerIntents[sequence.CarrierId] = carrierPlayerIntent;
        TargetPositions[sequence.CarrierId] = sequence.CurrentTouch.Target;

        if (!sequence.HasDefender || !CurrentPositions.ContainsKey(sequence.DefenderId))
        {
            return;
        }
        PlayerIntentKind defenderIntent = sequence.CurrentEngagement.Type switch
        {
            DefenderEngagementType.CloseDown => PlayerIntentKind.CloseDownBall,
            DefenderEngagementType.Jockey => PlayerIntentKind.JockeyBall,
            DefenderEngagementType.Contain => PlayerIntentKind.ContainBall,
            DefenderEngagementType.Tackle => PlayerIntentKind.TackleBall,
            DefenderEngagementType.ShoulderChallenge => PlayerIntentKind.ShoulderChallenge,
            _ => PlayerIntentKind.ContainBall
        };
        PlayerIntent defenderPlayerIntent = new(
            defenderIntent,
            sequence.CurrentEngagement.Target,
            LiveTeamPhase.Defending,
            sequence.CarrierId);
        _playerIntents[sequence.DefenderId] = defenderPlayerIntent;
        TargetPositions[sequence.DefenderId] = sequence.CurrentEngagement.Target;
    }

    private void EnforceGroundPlayerSeparation()
    {
        if (_state.BallOwnerId == new StringName() || !CurrentPositions.ContainsKey(_state.BallOwnerId))
        {
            return;
        }
        StringName defenderId = NearestOpponent(_state.BallOwnerId);
        if (defenderId == new StringName() || !CurrentPositions.ContainsKey(defenderId))
        {
            return;
        }

        Vector2 carrierMeters = FootballPitchDimensions.ToMeters(CurrentPositions[_state.BallOwnerId]);
        Vector2 defenderMeters = FootballPitchDimensions.ToMeters(CurrentPositions[defenderId]);
        Vector2 separation = defenderMeters - carrierMeters;
        float distanceMeters = separation.Length();
        if (distanceMeters >= DuelDistanceRules.MinimumPlayerSeparationMeters)
        {
            if (_state.GroundDuel.HasDefender && _state.GroundDuel.DefenderId == defenderId)
            {
                MinimumObservedGroundDuelSeparationMeters = Mathf.Min(
                    MinimumObservedGroundDuelSeparationMeters,
                    distanceMeters);
            }
            return;
        }
        if (distanceMeters <= 0.01f)
        {
            Vector2 ownGoalMeters = FootballPitchDimensions.ToMeters(
                new Vector2(OwnGoalX(_playerTeams[defenderId]), 0.5f));
            separation = ownGoalMeters - carrierMeters;
        }
        Vector2 direction = separation.LengthSquared() > 0.001f ? separation.Normalized() : Vector2.Down;
        Vector2 correctedDefenderMeters = carrierMeters + direction * DuelDistanceRules.MinimumPlayerSeparationMeters;
        CurrentPositions[defenderId] = SpaceEvaluator.ClampToPitch(
            FootballPitchDimensions.ToNormalized(correctedDefenderMeters));
        MinimumObservedGroundDuelSeparationMeters = Mathf.Min(
            MinimumObservedGroundDuelSeparationMeters,
            DuelDistanceRules.MinimumPlayerSeparationMeters);
    }

    private bool HasSupportOption(StringName carrierId)
    {
        foreach ((StringName playerId, PlayerIntent intent) in _playerIntents)
        {
            if (playerId != carrierId &&
                _playerTeams[playerId] == _playerTeams[carrierId] &&
                intent.Kind == PlayerIntentKind.SupportBall)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasCoverFor(StringName defenderId)
    {
        foreach ((StringName playerId, PlayerIntent intent) in _playerIntents)
        {
            if (playerId != defenderId &&
                _playerTeams[playerId] == _playerTeams[defenderId] &&
                intent.Kind == PlayerIntentKind.CoverPress &&
                FootballPitchDimensions.DistanceMeters(
                    CurrentPositions[playerId],
                    CurrentPositions[defenderId]) <= 12f)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsCarrierBackToGoal(StringName carrierId, StringName defenderId)
    {
        if (ActiveScenario == MatchScenarioKind.StrikerBackToGoalOneVersusOne)
        {
            return true;
        }
        if (_playerRoles[carrierId] != "ST")
        {
            return false;
        }
        float direction = AttackDirection(_playerTeams[carrierId]);
        bool defenderIsGoalSide = direction *
            (CurrentPositions[defenderId].X - CurrentPositions[carrierId].X) > 0f;
        return defenderIsGoalSide && PlayerSpeed(carrierId) < 1.4f;
    }

    private float PlayerSpeed(StringName playerId)
    {
        return PlayerVelocity(playerId).Length();
    }

    private Vector2 PlayerVelocity(StringName playerId)
    {
        return _movementController.VelocitiesMetersPerSecond.TryGetValue(playerId, out Vector2 velocity)
            ? velocity
            : Vector2.Zero;
    }

    private float MovementAlignment(StringName carrierId, StringName defenderId)
    {
        Vector2 carrierVelocity = PlayerVelocity(carrierId);
        Vector2 defenderVelocity = PlayerVelocity(defenderId);
        if (carrierVelocity.LengthSquared() <= 0.01f || defenderVelocity.LengthSquared() <= 0.01f)
        {
            return 0f;
        }
        return carrierVelocity.Normalized().Dot(defenderVelocity.Normalized());
    }

    private string EngagementDescription(
        StringName defenderId,
        StringName carrierId,
        DefenderEngagementType engagement)
    {
        return engagement switch
        {
            DefenderEngagementType.CloseDown => $"{PlayerName(defenderId)} áp sát người có bóng",
            DefenderEngagementType.Jockey =>
                $"{PlayerName(defenderId)} chạy kèm và ép hướng {PlayerName(carrierId)}",
            DefenderEngagementType.Contain =>
                $"{PlayerName(defenderId)} giữ khoảng cách, chờ thời điểm tranh bóng",
            DefenderEngagementType.Recover =>
                $"{PlayerName(defenderId)} lùi lại sau lần tranh chấp trước",
            _ => $"{PlayerName(defenderId)} đối đầu với {PlayerName(carrierId)}"
        };
    }

    private string DribbleDescription(
        StringName carrierId,
        DribbleTouchType touch,
        bool escapingPressure)
    {
        return touch switch
        {
            DribbleTouchType.KnockOn => $"{PlayerName(carrierId)} đẩy bóng dài rồi tăng tốc",
            DribbleTouchType.ChangeDirection => $"{PlayerName(carrierId)} đổi hướng tránh hậu vệ",
            DribbleTouchType.Shield => $"{PlayerName(carrierId)} xoay người che bóng",
            DribbleTouchType.HoldUp => $"{PlayerName(carrierId)} giảm tốc chờ đồng đội hỗ trợ",
            _ when escapingPressure => $"{PlayerName(carrierId)} chạm ngắn để thoát pressing",
            _ => $"{PlayerName(carrierId)} giữ bóng sát chân và tiến lên"
        };
    }
}
