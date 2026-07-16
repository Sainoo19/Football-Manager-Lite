using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchPitch2D : Control
{
    [Signal] public delegate void ActionChangedEventHandler(string description);
    [Signal] public delegate void LiveMatchEventEventHandler(FootballMatchEvent matchEvent);

    private enum BallActionKind
    {
        None,
        Pass,
        ThroughBall,
        Cross,
        Shot,
        Interception
    }

    private static readonly Color HomeColor = new("4f8cff");
    private static readonly Color AwayColor = new("ff5d73");
    private static readonly Color BallColor = new("f7fbff");

    public FootballMatchSimulation? Simulation { get; private set; }
    public readonly System.Collections.Generic.Dictionary<StringName, Vector2> BasePositions = new();
    public readonly System.Collections.Generic.Dictionary<StringName, Vector2> CurrentPositions = new();
    public readonly System.Collections.Generic.Dictionary<StringName, Vector2> TargetPositions = new();
    private readonly System.Collections.Generic.Dictionary<StringName, StringName> _playerTeams = new();
    private readonly System.Collections.Generic.Dictionary<StringName, string> _playerRoles = new();
    private readonly System.Collections.Generic.Dictionary<StringName, StringName> _playerSlotIds = new();

    public Vector2 BallPosition { get; private set; } = new(0.5f, 0.5f);
    private StringName _ballOwnerId = new();
    private StringName _activeTeamId = new();
    private float _visualTime;
    private float _lastPassTime = -10;
    private float _attackProgress = 0.22f;
    private float _phaseLane = 0.5f;
    private int _phaseSerial;
    private StringName _primaryRunnerId = new();
    private StringName _secondaryRunnerId = new();
    private StringName _pressingPlayerId = new();
    private BallActionKind _ballActionKind;
    private StringName _actionSourceId = new();
    private StringName _actionSourceTeamId = new();
    private bool _interceptionChecked;
    private float _nextDecisionTime;
    private int _decisionSerial;
    private StringName _pendingShotOutcome = new();
    private StringName _pendingShotShooterId = new();
    private StringName _pendingShotGoalkeeperId = new();
    private StringName _pendingShotBlockerId = new();

    public string LastActionName { get; private set; } = "Chuẩn bị giao bóng";
    public int CompletedPasses { get; private set; }
    public int Interceptions { get; private set; }
    public int Dribbles { get; private set; }
    public StringName CurrentBallOwnerId => _ballOwnerId;
    public bool IsPlaying { get; private set; }

    private bool _ballActionActive;
    private Vector2 _ballActionFrom = new(0.5f, 0.5f);
    private Vector2 _ballActionTo = new(0.5f, 0.5f);
    private float _ballActionElapsed;
    private float _ballActionDuration = 0.65f;
    private float _ballActionArc;
    private StringName _ballNextOwnerId = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(600, 270);
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public void SetMatch(FootballMatchSimulation simulation)
    {
        Simulation = simulation;
        BasePositions.Clear();
        CurrentPositions.Clear();
        TargetPositions.Clear();
        _playerTeams.Clear();
        _playerRoles.Clear();
        _playerSlotIds.Clear();
        _visualTime = 0;
        _lastPassTime = -10;
        _attackProgress = 0.22f;
        _phaseLane = 0.5f;
        _phaseSerial = 0;
        _decisionSerial = 0;
        _nextDecisionTime = 0.35f;
        _ballActionKind = BallActionKind.None;
        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
        CompletedPasses = 0;
        Interceptions = 0;
        Dribbles = 0;
        IsPlaying = false;
        SetAction("Chuẩn bị giao bóng");
        BallPosition = new Vector2(0.5f, 0.5f);
        _activeTeamId = simulation.home.team.id;
        simulation.set_live_possession(_activeTeamId);
        SyncLineups(true);
        SelectPhasePlayers();
        _ballOwnerId = ChooseOwner(_activeTeamId, false);
        QueueRedraw();
    }

    public void SetPlaying(bool playing)
    {
        IsPlaying = playing && Simulation is not null && !Simulation.is_finished;
        if (IsPlaying)
            _nextDecisionTime = Mathf.Max(_nextDecisionTime, _visualTime + 0.15f);
    }

    public void AnimateMinute(Array<FootballMatchEvent> newEvents)
    {
        if (Simulation is null)
            return;
        SyncLineups(false);
        FootballMatchEvent? focusEvent = null;
        foreach (FootballMatchEvent matchEvent in newEvents)
        {
            string type = matchEvent.event_type.ToString();
            if (type is "goal" or "shot_on_target" or "shot_off_target" or "corner" or
                "yellow_card" or "substitution" or "half_time" or "full_time")
                focusEvent = matchEvent;
        }

        StringName possessionTeam = Simulation.last_possession_team_id;
        if (focusEvent is not null && IsAttackingEvent(focusEvent.event_type) && focusEvent.team_id != new StringName())
            possessionTeam = focusEvent.team_id;
        if (possessionTeam == new StringName())
            possessionTeam = _activeTeamId;

        bool turnover = possessionTeam != _activeTeamId;
        _activeTeamId = possessionTeam;
        AdvancePhase(turnover, focusEvent);
        SelectPhasePlayers();

        if (focusEvent is not null)
            AnimateEvent(focusEvent);
        else if (!_ballActionActive && _ballOwnerId != new StringName() && _visualTime >= _nextDecisionTime)
            DecideNextAction();
        QueueRedraw();
    }

    public override void _Process(double deltaValue)
    {
        if (Simulation is null)
            return;
        float delta = (float)deltaValue;
        _visualTime += delta;
        UpdatePlayerTargets();
        float weight = 1f - Mathf.Exp(-delta * 3.6f);
        foreach (StringName playerId in CurrentPositions.Keys.ToArray())
        {
            if (TargetPositions.TryGetValue(playerId, out Vector2 target))
            {
                float roleSpeed = RoleSpeed(_playerRoles[playerId]);
                CurrentPositions[playerId] = CurrentPositions[playerId].Lerp(target, Mathf.Clamp(weight * roleSpeed, 0, 1));
            }
        }
        UpdateBall(delta);
        if (IsPlaying && !_ballActionActive && _ballOwnerId != new StringName() && _visualTime >= _nextDecisionTime)
            DecideNextAction();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Simulation is null)
            return;
        Rect2 field = new(new Vector2(18, 10), Size - new Vector2(36, 20));
        DrawRect(field, new Color("176b45"));
        float stripeWidth = field.Size.X / 12f;
        for (int i = 0; i < 12; i++)
        {
            if (i % 2 == 0)
                DrawRect(new Rect2(field.Position + new Vector2(stripeWidth * i, 0), new Vector2(stripeWidth, field.Size.Y)), new Color("1c764e"));
        }

        Color line = new(1, 1, 1, 0.66f);
        Vector2 center = field.GetCenter();
        DrawRect(field, line, false, 2);
        DrawLine(new Vector2(center.X, field.Position.Y), new Vector2(center.X, field.End.Y), line, 2);
        DrawArc(center, field.Size.Y * 0.18f, 0, Mathf.Tau, 48, line, 2);
        DrawCircle(center, 3, line);

        float penaltyWidth = field.Size.X * 0.165f;
        float penaltyHeight = field.Size.Y * 0.62f;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - penaltyWidth, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);

        float goalAreaWidth = field.Size.X * 0.065f;
        float goalAreaHeight = field.Size.Y * 0.32f;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - goalAreaWidth, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);

        Vector2 leftSpot = new(field.Position.X + field.Size.X * 0.115f, center.Y);
        Vector2 rightSpot = new(field.End.X - field.Size.X * 0.115f, center.Y);
        DrawCircle(leftSpot, 2.5f, line);
        DrawCircle(rightSpot, 2.5f, line);
        Vector2 arcRadius = new(field.Size.X * 0.087f, field.Size.Y * 0.135f);
        DrawEllipticalArc(leftSpot, arcRadius, -0.93f, 0.93f, line);
        DrawEllipticalArc(rightSpot, arcRadius, Mathf.Pi - 0.93f, Mathf.Pi + 0.93f, line);

        float goalHeight = field.Size.Y * 0.26f;
        DrawRect(new Rect2(new Vector2(field.Position.X - 8, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);

        foreach ((StringName playerId, Vector2 normalized) in CurrentPositions)
        {
            Vector2 point = ToFieldPoint(normalized, field);
            bool isHome = _playerTeams[playerId] == Simulation.home.team.id;
            Color color = isHome ? HomeColor : AwayColor;
            if (_playerRoles[playerId] == "GK")
                color = isHome ? new Color("f1c75b") : new Color("ec9f45");
            DrawCircle(point + new Vector2(1.5f, 2), 8.5f, new Color(0, 0, 0, 0.32f));
            DrawCircle(point, 8, color);
            DrawArc(point, 8, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.84f), 1.5f);
        }

        Vector2 ballPoint = ToFieldPoint(BallPosition, field);
        if (_ballActionActive)
            DrawLine(ballPoint, ToFieldPoint(_ballActionTo, field), new Color(1, 1, 1, 0.13f), 1);
        DrawCircle(ballPoint + new Vector2(1.5f, 2), 5, new Color(0, 0, 0, 0.38f));
        DrawCircle(ballPoint, 4.5f, BallColor);
        DrawArc(ballPoint, 4.5f, 0, Mathf.Tau, 20, new Color("27313d"), 1);
    }

    private void UpdatePlayerTargets()
    {
        if (Simulation is null)
            return;

        Vector2 ballAnchor = PhaseBallAnchor();
        var proposed = new System.Collections.Generic.Dictionary<StringName, Vector2>();

        foreach (StringName playerId in BasePositions.Keys)
        {
            if (_playerTeams[playerId] != _activeTeamId)
                continue;
            proposed[playerId] = AttackingTarget(playerId, ballAnchor);
        }

        foreach (StringName playerId in BasePositions.Keys)
        {
            if (_playerTeams[playerId] == _activeTeamId)
                continue;
            proposed[playerId] = DefensiveTarget(playerId, ballAnchor, proposed);
        }

        ApplyTeamSeparation(proposed);
        foreach ((StringName playerId, Vector2 target) in proposed)
            TargetPositions[playerId] = ClampToPitch(target);
    }

    private Vector2 AttackingTarget(StringName playerId, Vector2 ballAnchor)
    {
        bool isHome = _playerTeams[playerId] == Simulation!.home.team.id;
        float direction = isHome ? -1f : 1f;
        string role = _playerRoles[playerId];
        Vector2 basePosition = BasePositions[playerId];
        float ahead = role switch
        {
            "GK" => -0.52f,
            "CB" => -0.28f,
            "LB" or "RB" => -0.13f,
            "DM" => -0.14f,
            "CM" => -0.025f,
            "AM" => 0.10f,
            "LW" or "RW" => 0.16f,
            "ST" => 0.22f,
            _ => 0f
        };

        MatchTeamState state = _activeTeamId == Simulation.home.team.id ? Simulation.home : Simulation.away;
        if (state.mentality == "attacking" && role != "GK") ahead += 0.045f;
        if (state.mentality == "defensive" && role != "GK") ahead -= 0.035f;

        float targetY = Mathf.Lerp(basePosition.Y, _phaseLane, role is "CM" or "AM" or "ST" ? 0.34f : 0.12f);
        if (role is "LB" or "LW") targetY = Mathf.Min(targetY, 0.22f);
        if (role is "RB" or "RW") targetY = Mathf.Max(targetY, 0.78f);

        if (playerId == _primaryRunnerId)
        {
            ahead += 0.19f;
            targetY = Mathf.Lerp(targetY, _phaseLane, 0.58f);
        }
        else if (playerId == _secondaryRunnerId)
        {
            ahead += role is "LB" or "RB" ? 0.23f : 0.12f;
            if (role == "LB") targetY = 0.09f;
            if (role == "RB") targetY = 0.91f;
        }

        if (playerId == _ballOwnerId)
        {
            ahead = -0.005f;
            targetY = _phaseLane;
        }

        float supportOffset = ((Math.Abs(playerId.GetHashCode()) % 3) - 1) * 0.018f;
        return new Vector2(ballAnchor.X + direction * ahead, targetY + supportOffset);
    }

    private Vector2 DefensiveTarget(
        StringName playerId,
        Vector2 ballAnchor,
        System.Collections.Generic.Dictionary<StringName, Vector2> attackingTargets)
    {
        bool isHome = _playerTeams[playerId] == Simulation!.home.team.id;
        float attackDirection = isHome ? -1f : 1f;
        float goalSide = -attackDirection;
        string role = _playerRoles[playerId];
        Vector2 basePosition = BasePositions[playerId];

        if (role == "GK")
        {
            float goalX = isHome ? 0.945f : 0.055f;
            return new Vector2(goalX, Mathf.Lerp(0.5f, ballAnchor.Y, 0.18f));
        }

        if (playerId == _pressingPlayerId)
            return ballAnchor + new Vector2(goalSide * 0.022f, 0);

        if (role is "CB" or "LB" or "RB")
        {
            var marks = attackingTargets
                .Where(pair => _playerRoles[pair.Key] is "ST" or "LW" or "RW" or "AM")
                .OrderBy(pair => Math.Abs(pair.Value.Y - basePosition.Y))
                .ToList();
            Vector2 reference = marks.Count > 0 ? marks[0].Value : ballAnchor;
            float lineX = ballAnchor.X + goalSide * 0.14f;
            float markedX = reference.X + goalSide * 0.035f;
            return new Vector2(Mathf.Lerp(lineX, markedX, 0.62f), Mathf.Lerp(basePosition.Y, reference.Y, 0.62f));
        }

        float distance = role switch
        {
            "DM" => 0.09f,
            "CM" => 0.065f,
            "AM" => 0.04f,
            _ => 0.025f
        };
        float compactness = role is "DM" or "CM" ? 0.48f : 0.28f;
        return new Vector2(
            ballAnchor.X + goalSide * distance,
            Mathf.Lerp(basePosition.Y, ballAnchor.Y, compactness));
    }

    private void UpdateBall(float delta)
    {
        if (Simulation is null)
            return;
        if (_ballActionActive)
        {
            _ballActionElapsed += delta;
            float progress = Mathf.Clamp(_ballActionElapsed / Mathf.Max(_ballActionDuration, 0.01f), 0, 1);
            float eased = progress * progress * (3 - 2 * progress);
            BallPosition = _ballActionFrom.Lerp(_ballActionTo, eased);
            BallPosition = new Vector2(BallPosition.X, BallPosition.Y - Mathf.Sin(progress * Mathf.Pi) * _ballActionArc);
            if (!_interceptionChecked && progress >= 0.44f && _ballActionKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross)
            {
                _interceptionChecked = true;
                TryInterceptMovingBall();
            }
            if (progress >= 1)
            {
                _ballActionActive = false;
                _ballOwnerId = _ballNextOwnerId;
                _ballNextOwnerId = new StringName();
                if (_ballActionKind is BallActionKind.Pass or BallActionKind.ThroughBall or BallActionKind.Cross)
                    CompletedPasses++;
                if (_ballActionKind == BallActionKind.Interception)
                {
                    Interceptions++;
                    SetAction($"{PlayerName(_ballOwnerId)} cắt được đường bóng");
                }
                if (_ballActionKind == BallActionKind.Shot)
                    CompleteLiveShot();
                _ballActionKind = BallActionKind.None;
                _nextDecisionTime = _visualTime + 0.32f;
            }
            return;
        }

        if (_ballOwnerId != new StringName() && CurrentPositions.TryGetValue(_ballOwnerId, out Vector2 owner))
        {
            bool isHome = _playerTeams[_ballOwnerId] == Simulation.home.team.id;
            Vector2 offset = new(isHome ? -0.012f : 0.012f, 0.012f);
            BallPosition = BallPosition.Lerp(owner + offset, 1f - Mathf.Exp(-delta * 8f));
        }
    }

    private void AnimateEvent(FootballMatchEvent matchEvent)
    {
        if (Simulation is null)
            return;
        string type = matchEvent.event_type.ToString();
        if (type is "goal" or "shot_on_target" or "shot_off_target")
        {
            bool homeAttack = matchEvent.team_id == Simulation.home.team.id;
            StringName defending = homeAttack ? Simulation.away.team.id : Simulation.home.team.id;
            StartBallAction(new Vector2(homeAttack ? 0.006f : 0.994f, 0.5f), 0.72f, 0.045f, ChooseGoalkeeper(defending), BallActionKind.Shot);
            SetAction($"{PlayerName(matchEvent.player_id)} dứt điểm");
            return;
        }
        if (type == "corner")
        {
            bool homeCorner = matchEvent.team_id == Simulation.home.team.id;
            Vector2 corner = new(homeCorner ? 0.018f : 0.982f, Simulation.current_minute % 2 == 0 ? 0.035f : 0.965f);
            BallPosition = corner;
            Vector2 crossTarget = new(homeCorner ? 0.12f : 0.88f, 0.5f);
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChooseOwner(matchEvent.team_id, true);
            StartBallAction(crossTarget, 0.68f, 0.055f, receiver, BallActionKind.Cross);
            SetAction("Quả tạt từ chấm phạt góc");
            return;
        }
        if (type is "half_time" or "full_time")
        {
            StartBallAction(new Vector2(0.5f, 0.5f), 0.5f, 0, new StringName(), BallActionKind.None);
            return;
        }
        if (type is "substitution" or "tactic")
            return;
        if (type == "yellow_card")
            StartPass(ChoosePassTarget());
        else if (matchEvent.player_id != new StringName() && CurrentPositions.ContainsKey(matchEvent.player_id))
            StartPass(matchEvent.player_id);
        else if (_visualTime - _lastPassTime >= 0.4f)
            StartPass(ChoosePassTarget());
    }

    private void StartPass(StringName ownerId, BallActionKind requestedKind = BallActionKind.Pass)
    {
        if (ownerId == new StringName() || !CurrentPositions.TryGetValue(ownerId, out Vector2 current))
            return;
        Vector2 runTarget = TargetPositions.GetValueOrDefault(ownerId, current);
        float lead = ownerId == _primaryRunnerId ? 0.78f : ownerId == _secondaryRunnerId ? 0.58f : 0.34f;
        Vector2 target = current.Lerp(runTarget, lead);
        float distance = BallPosition.DistanceTo(target);
        BallActionKind kind = requestedKind;
        if (kind == BallActionKind.Pass && ownerId == _primaryRunnerId && distance > 0.16f)
            kind = BallActionKind.ThroughBall;
        _lastPassTime = _visualTime;
        StartBallAction(target, Mathf.Clamp(0.28f + distance * 0.72f, 0.34f, 0.68f), 0.018f + distance * 0.035f, ownerId, kind);
        string action = kind switch
        {
            BallActionKind.ThroughBall => "chọc khe cho",
            BallActionKind.Cross => "tạt bóng tới",
            _ => "chuyền cho"
        };
        SetAction($"{PlayerName(_actionSourceId)} {action} {PlayerName(ownerId)}");
    }

    private void StartBallAction(
        Vector2 destination,
        float duration,
        float arc,
        StringName nextOwner,
        BallActionKind kind = BallActionKind.Pass)
    {
        _actionSourceId = _ballOwnerId;
        _actionSourceTeamId = _actionSourceId != new StringName() && _playerTeams.ContainsKey(_actionSourceId)
            ? _playerTeams[_actionSourceId]
            : _activeTeamId;
        _ballActionActive = true;
        _ballActionFrom = BallPosition;
        _ballActionTo = destination;
        _ballActionElapsed = 0;
        _ballActionDuration = duration;
        _ballActionArc = arc;
        _ballNextOwnerId = nextOwner;
        _ballActionKind = kind;
        _interceptionChecked = kind is BallActionKind.None or BallActionKind.Shot;
        _ballOwnerId = new StringName();
    }

    private StringName ChooseOwner(StringName teamId, bool preferAttackers)
    {
        if (Simulation is null)
            return new StringName();
        var preferred = new List<StringName>();
        var fallback = new List<StringName>();
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] != teamId) continue;
            fallback.Add(playerId);
            string role = _playerRoles[playerId];
            bool preferredRole = preferAttackers
                ? role is "AM" or "LW" or "RW" or "ST"
                : role is "DM" or "CM" or "AM" or "LB" or "RB";
            if (preferredRole) preferred.Add(playerId);
        }
        List<StringName> candidates = preferred.Count > 0 ? preferred : fallback;
        if (candidates.Count == 0) return new StringName();
        int index = (Simulation.current_minute + (int)(_visualTime * 3)) % candidates.Count;
        return candidates[index];
    }

    private StringName ChooseGoalkeeper(StringName teamId)
    {
        foreach (StringName playerId in CurrentPositions.Keys)
            if (_playerTeams[playerId] == teamId && _playerRoles[playerId] == "GK")
                return playerId;
        return ChooseOwner(teamId, false);
    }

    private void DecideNextAction()
    {
        if (Simulation is null || Simulation.is_finished || _ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return;
        _decisionSerial++;
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
        if (Simulation.use_live_pitch_events && ShouldShoot(ownerId, pressureDistance))
        {
            StartLiveShot(ownerId, pressureDistance);
            return;
        }
        bool widePlayer = _playerRoles[ownerId] is "LB" or "RB" or "LW" or "RW";
        if (_attackProgress > 0.68f && widePlayer)
        {
            StringName receiver = _primaryRunnerId != new StringName() ? _primaryRunnerId : ChoosePassTarget(false);
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
        bool homeAttack = _playerTeams[shooterId] == Simulation.home.team.id;
        StringName defendingTeamId = homeAttack ? Simulation.away.team.id : Simulation.home.team.id;
        StringName goalkeeperId = ChooseGoalkeeper(defendingTeamId);
        Vector2 shooterPosition = CurrentPositions[shooterId];
        float goalX = homeAttack ? 0.006f : 0.994f;
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
            outcome = "blocked";
            destination = CurrentPositions[blockerId];
            nextOwner = blockerId;
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
                outcome = goal ? "goal" : "saved";
                destination = goal ? goalTarget : CurrentPositions.GetValueOrDefault(goalkeeperId, goalTarget);
            }
        }

        _pendingShotOutcome = outcome;
        _pendingShotShooterId = shooterId;
        _pendingShotGoalkeeperId = goalkeeperId;
        _pendingShotBlockerId = blockerId;
        StartBallAction(destination, 0.46f, 0.012f, nextOwner, BallActionKind.Shot);
        SetAction($"{PlayerName(shooterId)} tung cú sút");
    }

    private void CompleteLiveShot()
    {
        if (Simulation is null || _pendingShotOutcome == new StringName())
            return;
        FootballMatchEvent? matchEvent = Simulation.register_live_shot(
            _actionSourceTeamId,
            _pendingShotShooterId,
            _pendingShotOutcome,
            _pendingShotGoalkeeperId,
            _pendingShotBlockerId);
        if (matchEvent is not null)
            EmitSignal(SignalName.LiveMatchEvent, matchEvent);

        string resultText = _pendingShotOutcome.ToString() switch
        {
            "goal" => $"BÀN THẮNG — {PlayerName(_pendingShotShooterId)}",
            "saved" => $"{PlayerName(_pendingShotGoalkeeperId)} cản phá",
            "blocked" => $"{PlayerName(_pendingShotBlockerId)} chắn cú sút",
            _ => $"{PlayerName(_pendingShotShooterId)} sút chệch khung thành"
        };
        SetAction(resultText);

        StringName defendingTeamId = _actionSourceTeamId == Simulation.home.team.id
            ? Simulation.away.team.id
            : Simulation.home.team.id;
        _activeTeamId = defendingTeamId;
        Simulation.set_live_possession(defendingTeamId);
        _attackProgress = 0.16f;
        _phaseLane = 0.5f;
        SelectPhasePlayers();
        _pendingShotOutcome = new StringName();
        _pendingShotShooterId = new StringName();
        _pendingShotGoalkeeperId = new StringName();
        _pendingShotBlockerId = new StringName();
    }

    private bool TryResolveTackle(StringName ownerId, StringName defenderId, float distance)
    {
        FootballPlayer? owner = GetPlayer(ownerId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float tackleSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float controlSkill = ((owner?.dribbling ?? 50) + (owner?.pace ?? 50)) * 0.5f;
        float contactBonus = Mathf.Clamp((0.068f - distance) / 0.068f, 0, 1) * 0.28f;
        float chance = Mathf.Clamp(0.22f + (tackleSkill - controlSkill) / 145f + contactBonus, 0.08f, 0.72f);
        if (DecisionRoll(ownerId, defenderId, _decisionSerial + 41) >= chance)
            return false;

        _ballOwnerId = defenderId;
        _activeTeamId = _playerTeams[defenderId];
        Simulation!.set_live_possession(_activeTeamId);
        bool homeRecovery = _activeTeamId == Simulation!.home.team.id;
        _attackProgress = Mathf.Clamp(homeRecovery ? 1f - BallPosition.X : BallPosition.X, 0.16f, 0.62f);
        _phaseLane = CurrentPositions[defenderId].Y;
        Interceptions++;
        SelectPhasePlayers();
        _nextDecisionTime = _visualTime + 0.38f;
        SetAction($"{PlayerName(defenderId)} đoạt bóng từ {PlayerName(ownerId)}");
        return true;
    }

    private void TryInterceptMovingBall()
    {
        if (Simulation is null || _actionSourceTeamId == new StringName())
            return;
        var defenders = CurrentPositions.Keys
            .Where(id => _playerTeams[id] != _actionSourceTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(BallPosition))
            .ToList();
        if (defenders.Count == 0)
            return;
        StringName defenderId = defenders[0];
        float distance = CurrentPositions[defenderId].DistanceTo(BallPosition);
        float threshold = _ballActionKind switch
        {
            BallActionKind.Cross => 0.115f,
            BallActionKind.ThroughBall => 0.095f,
            _ => 0.075f
        };
        if (distance > threshold)
            return;

        FootballPlayer? passer = GetPlayer(_actionSourceId);
        FootballPlayer? defender = GetPlayer(defenderId);
        float passingSkill = ((passer?.passing ?? 50) + (passer?.vision ?? 50)) * 0.5f;
        float defensiveSkill = ((defender?.tackling ?? 50) + (defender?.positioning ?? 50)) * 0.5f;
        float laneBonus = (threshold - distance) / threshold * 0.38f;
        float chance = Mathf.Clamp(0.12f + laneBonus + (defensiveSkill - passingSkill) / 190f, 0.06f, 0.68f);
        if (DecisionRoll(_actionSourceId, defenderId, _decisionSerial + _phaseSerial * 13) >= chance)
            return;

        _ballActionFrom = BallPosition;
        _ballActionTo = CurrentPositions[defenderId];
        _ballActionElapsed = 0;
        _ballActionDuration = 0.18f;
        _ballActionArc = 0.004f;
        _ballNextOwnerId = defenderId;
        _ballActionKind = BallActionKind.Interception;
        _activeTeamId = _playerTeams[defenderId];
        Simulation.set_live_possession(_activeTeamId);
        bool homeRecovery = _activeTeamId == Simulation.home.team.id;
        _attackProgress = Mathf.Clamp(homeRecovery ? 1f - BallPosition.X : BallPosition.X, 0.16f, 0.60f);
        _phaseLane = CurrentPositions[defenderId].Y;
        SelectPhasePlayers();
    }

    private StringName NearestOpponent(StringName playerId)
    {
        if (!CurrentPositions.TryGetValue(playerId, out Vector2 position))
            return new StringName();
        return CurrentPositions.Keys
            .Where(id => _playerTeams[id] != _playerTeams[playerId] && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(position))
            .FirstOrDefault() ?? new StringName();
    }

    private float PassingLaneRisk(Vector2 from, Vector2 to, StringName passingTeamId)
    {
        float highestRisk = 0;
        foreach (StringName playerId in CurrentPositions.Keys)
        {
            if (_playerTeams[playerId] == passingTeamId || _playerRoles[playerId] == "GK")
                continue;
            float distance = DistanceToSegment(CurrentPositions[playerId], from, to);
            highestRisk = Mathf.Max(highestRisk, 1f - Mathf.Clamp(distance / 0.13f, 0, 1));
        }
        return highestRisk;
    }

    private FootballPlayer? GetPlayer(StringName playerId)
    {
        if (Simulation is null || playerId == new StringName())
            return null;
        return Simulation.home.team.get_player(playerId) ?? Simulation.away.team.get_player(playerId);
    }

    private string PlayerName(StringName playerId) => GetPlayer(playerId)?.display_name ?? "Một cầu thủ";

    private void SetAction(string description)
    {
        LastActionName = description;
        EmitSignal(SignalName.ActionChanged, description);
    }

    private static float DecisionRoll(StringName firstId, StringName secondId, int serial)
    {
        uint value = unchecked((uint)firstId.GetHashCode() * 2654435761u);
        value ^= unchecked((uint)secondId.GetHashCode() * 2246822519u);
        value ^= unchecked((uint)serial * 3266489917u);
        value ^= value >> 16;
        return (value & 0x00ffffff) / 16777215f;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f)
            return point.DistanceTo(start);
        float progress = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0, 1);
        return point.DistanceTo(start + segment * progress);
    }

    private StringName ChoosePassTarget(bool preferSafe = false)
    {
        if (Simulation is null)
            return new StringName();
        if (_ballOwnerId == new StringName() || !CurrentPositions.ContainsKey(_ballOwnerId))
            return ChooseOwner(_activeTeamId, _attackProgress > 0.55f);

        bool homeAttack = _activeTeamId == Simulation.home.team.id;
        float direction = homeAttack ? -1f : 1f;
        Vector2 owner = CurrentPositions[_ballOwnerId];
        StringName bestId = new();
        float bestScore = float.NegativeInfinity;
        foreach (StringName candidateId in CurrentPositions.Keys)
        {
            if (candidateId == _ballOwnerId || _playerTeams[candidateId] != _activeTeamId || _playerRoles[candidateId] == "GK")
                continue;
            Vector2 candidate = TargetPositions.GetValueOrDefault(candidateId, CurrentPositions[candidateId]);
            float distance = owner.DistanceTo(candidate);
            if (distance > 0.48f || distance < 0.045f)
                continue;
            float forwardGain = direction * (candidate.X - owner.X);
            float laneRisk = PassingLaneRisk(owner, candidate, _activeTeamId);
            float forwardWeight = preferSafe ? 1.35f : 2.7f;
            float score = forwardGain * forwardWeight - distance * 0.42f -
                          Math.Abs(candidate.Y - _phaseLane) * 0.12f - laneRisk * (preferSafe ? 1.45f : 0.82f);
            if (candidateId == _primaryRunnerId) score += _attackProgress > 0.52f ? 0.28f : 0.06f;
            if (candidateId == _secondaryRunnerId) score += 0.09f;
            if (score <= bestScore) continue;
            bestScore = score;
            bestId = candidateId;
        }
        return bestId != new StringName() ? bestId : ChooseOwner(_activeTeamId, false);
    }

    private void AdvancePhase(bool turnover, FootballMatchEvent? focusEvent)
    {
        if (Simulation is null)
            return;
        _phaseSerial++;
        string eventType = focusEvent?.event_type.ToString() ?? "";
        if (eventType is "half_time" or "full_time")
        {
            _attackProgress = 0.48f;
            _phaseLane = 0.5f;
            return;
        }

        if (turnover)
        {
            bool homeAttack = _activeTeamId == Simulation.home.team.id;
            float recoveredProgress = homeAttack ? 1f - BallPosition.X : BallPosition.X;
            _attackProgress = Mathf.Clamp(recoveredProgress, 0.18f, 0.58f);
        }
        else
        {
            _attackProgress += 0.075f;
            if (_attackProgress > 0.88f && !IsAttackingEvent(focusEvent?.event_type ?? new StringName()))
                _attackProgress = 0.34f;
        }

        if (eventType is "goal" or "shot_on_target" or "shot_off_target")
            _attackProgress = 0.96f;
        else if (eventType == "corner")
            _attackProgress = 0.91f;

        if (turnover || _phaseSerial % 2 == 0 || eventType == "corner")
        {
            float[] lanes = { 0.18f, 0.5f, 0.82f, 0.34f, 0.66f };
            int teamSalt = Math.Abs(_activeTeamId.GetHashCode()) % lanes.Length;
            _phaseLane = lanes[(Simulation.current_minute + _phaseSerial + teamSalt) % lanes.Length];
        }
    }

    private void SelectPhasePlayers()
    {
        if (Simulation is null || CurrentPositions.Count == 0)
            return;
        Vector2 anchor = PhaseBallAnchor();
        var runners = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == _activeTeamId && _playerRoles[id] is "ST" or "LW" or "RW" or "AM" or "CM")
            .OrderBy(id => Math.Abs(id.GetHashCode() + _phaseSerial * 17))
            .ToList();
        _primaryRunnerId = runners.Count > 0 ? runners[_phaseSerial % runners.Count] : new StringName();

        var supportRunners = CurrentPositions.Keys
            .Where(id => _playerTeams[id] == _activeTeamId && id != _primaryRunnerId && _playerRoles[id] is "LB" or "RB" or "CM" or "LW" or "RW")
            .OrderBy(id => Math.Abs(id.GetHashCode() - _phaseSerial * 11))
            .ToList();
        _secondaryRunnerId = supportRunners.Count > 0 ? supportRunners[_phaseSerial % supportRunners.Count] : new StringName();

        _pressingPlayerId = CurrentPositions.Keys
            .Where(id => _playerTeams[id] != _activeTeamId && _playerRoles[id] != "GK")
            .OrderBy(id => CurrentPositions[id].DistanceSquaredTo(anchor))
            .FirstOrDefault() ?? new StringName();
    }

    private Vector2 PhaseBallAnchor()
    {
        if (Simulation is null)
            return new Vector2(0.5f, 0.5f);
        bool homeAttack = _activeTeamId == Simulation.home.team.id;
        float startX = homeAttack ? 0.86f : 0.14f;
        float endX = homeAttack ? 0.08f : 0.92f;
        return new Vector2(Mathf.Lerp(startX, endX, _attackProgress), _phaseLane);
    }

    private void ApplyTeamSeparation(System.Collections.Generic.Dictionary<StringName, Vector2> positions)
    {
        StringName[] ids = positions.Keys.ToArray();
        for (int first = 0; first < ids.Length; first++)
        {
            for (int second = first + 1; second < ids.Length; second++)
            {
                if (_playerTeams[ids[first]] != _playerTeams[ids[second]])
                    continue;
                Vector2 delta = positions[ids[second]] - positions[ids[first]];
                if (delta.LengthSquared() >= 0.0016f)
                    continue;
                float push = delta.Y >= 0 ? 0.022f : -0.022f;
                positions[ids[first]] += new Vector2(0, -push);
                positions[ids[second]] += new Vector2(0, push);
            }
        }
    }

    private static bool IsAttackingEvent(StringName eventType) =>
        eventType.ToString() is "goal" or "shot_on_target" or "shot_off_target" or "corner";

    private static Vector2 ClampToPitch(Vector2 position) => new(
        Mathf.Clamp(position.X, 0.025f, 0.975f),
        Mathf.Clamp(position.Y, 0.035f, 0.965f));

    private void SyncLineups(bool reset)
    {
        if (Simulation is null) return;
        var valid = new HashSet<StringName>();
        SyncTeam(Simulation.home, false, valid, reset);
        SyncTeam(Simulation.away, true, valid, reset);
        foreach (StringName playerId in CurrentPositions.Keys.Where(id => !valid.Contains(id)).ToArray())
        {
            BasePositions.Remove(playerId);
            CurrentPositions.Remove(playerId);
            TargetPositions.Remove(playerId);
            _playerTeams.Remove(playerId);
            _playerRoles.Remove(playerId);
            _playerSlotIds.Remove(playerId);
        }
    }

    private void SyncTeam(MatchTeamState state, bool mirrored, HashSet<StringName> valid, bool reset)
    {
        foreach (Dictionary slot in state.formation.slots)
        {
            StringName slotId = slot["id"].AsStringName();
            if (!state.squad.starter_slots.TryGetValue(slotId, out Variant value)) continue;
            StringName playerId = value.AsStringName();
            Vector2 basePosition = new(slot["y"].AsSingle(), slot["x"].AsSingle());
            if (mirrored) basePosition = new Vector2(1 - basePosition.X, 1 - basePosition.Y);
            valid.Add(playerId);
            BasePositions[playerId] = basePosition;
            TargetPositions[playerId] = basePosition;
            _playerTeams[playerId] = state.team.id;
            _playerRoles[playerId] = slot["role"].AsString();
            _playerSlotIds[playerId] = slotId;
            if (reset || !CurrentPositions.ContainsKey(playerId))
            {
                Vector2 previous = PositionForReplacedSlot(state.team.id, slotId);
                CurrentPositions[playerId] = previous == Vector2.Zero ? basePosition : previous;
            }
        }
    }

    private Vector2 PositionForReplacedSlot(StringName teamId, StringName slotId)
    {
        foreach (StringName oldId in CurrentPositions.Keys)
        {
            if (_playerTeams.GetValueOrDefault(oldId) == teamId && _playerSlotIds.GetValueOrDefault(oldId) == slotId)
                return CurrentPositions[oldId];
        }
        return Vector2.Zero;
    }

    private static float RoleSpeed(string role) => role switch
    {
        "GK" => 0.72f,
        "CB" => 0.86f,
        "LB" or "RB" => 1.08f,
        "DM" or "CM" or "AM" => 1.02f,
        _ => 1.14f
    };

    private void DrawEllipticalArc(Vector2 center, Vector2 radius, float start, float end, Color color)
    {
        var points = new Vector2[25];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Lerp(start, end, i / 24f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
        }
        DrawPolyline(points, color, 2, true);
    }

    private static Vector2 ToFieldPoint(Vector2 normalized, Rect2 field) =>
        field.Position + new Vector2(normalized.X * field.Size.X, normalized.Y * field.Size.Y);
}
