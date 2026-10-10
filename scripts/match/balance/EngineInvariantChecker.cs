using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

public sealed record EngineInvariantViolation(string Code, int Count, double FirstGameSeconds, string FirstDetail);

// Observes a live match after every simulation step and records states that must never occur.
// It only reads engine state, so attaching it cannot change how a match unfolds.
public sealed class EngineInvariantChecker
{
    private const float RequiredRestartDistanceMeters = 9.15f;
    // One step of movement after the kick plus ball placement rounding.
    private const float RestartDistanceToleranceMeters = 1.0f;
    private const double MaximumRestartWaitSeconds = 60d;
    private const double MaximumOwnerHoldSeconds = 30d;
    private const double MaximumGoalkeeperHoldSeconds = 8d;
    private const double MaximumConfinedBallSeconds = 45d;
    private const float ConfinedBallRadiusMeters = 6f;
    private const double MaximumUnownedBallSeconds = 2d;

    private sealed class Tally
    {
        public int Count;
        public double FirstGameSeconds;
        public string FirstDetail = string.Empty;
    }

    private readonly Dictionary<string, Tally> _violations = new(StringComparer.Ordinal);
    private LiveMatchEngine? _engine;
    private HashSet<StringName> _previousPlayers = new();
    private LiveMatchInvariantView _previous;
    private bool _hasPrevious;
    private StringName _heldOwnerId = new();
    private double _heldSince;
    private bool _ownerHoldReported;
    private bool _goalkeeperHoldReported;
    private double _restartSince;
    private bool _restartStallReported;
    private Vector2 _confinedAnchor;
    private double _confinedSince;
    private bool _confinedReported;
    private readonly Dictionary<string, int> _confinedActions = new(StringComparer.Ordinal);
    private string _lastObservedAction = string.Empty;
    private double _unownedSince = double.NaN;
    private bool _unownedReported;
    private int _knownExemptOffsides;

    public IReadOnlyList<EngineInvariantViolation> Violations => _violations
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new EngineInvariantViolation(
            pair.Key, pair.Value.Count, pair.Value.FirstGameSeconds, pair.Value.FirstDetail))
        .ToList();

    public void Attach(LiveMatchEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        engine.SimulationStepCompleted += Observe;
    }

    private void Observe()
    {
        if (_engine is null)
        {
            return;
        }
        LiveMatchInvariantView view = _engine.CreateInvariantView();
        try
        {
            CheckReferences(view);
            CheckLineups(view);
            CheckBallState(view);
            CheckCoordinates(view);
            CheckStalls(view);
            CheckLaws(view);
        }
        catch (Exception exception)
        {
            Report("CHECKER_ERROR", view, $"{exception.GetType().Name}: {exception.Message}");
        }
        _previousPlayers = new HashSet<StringName>(_engine.PositionView.Keys);
        _previous = view;
        _hasPrevious = true;
    }

    // I2: every player the engine refers to must be on the pitch.
    private void CheckReferences(LiveMatchInvariantView view)
    {
        RequireOnPitch(view, view.BallOwnerId, "người giữ bóng");
        RequireOnPitch(view, view.ExpectedReceiverId, "người nhận dự kiến");
        RequireOnPitch(view, view.RestartTakerId, "người thực hiện đá lại");
        RequireOnPitch(view, view.DuelCarrierId, "người dẫn bóng trong tranh chấp");
        RequireOnPitch(view, view.DuelDefenderId, "hậu vệ trong tranh chấp");
        RequireOnPitch(view, view.ShotShooterId, "người sút");
        RequireOnPitch(view, view.ShotGoalkeeperId, "thủ môn đối mặt cú sút");
    }

    private void RequireOnPitch(LiveMatchInvariantView view, StringName playerId, string role)
    {
        if (playerId != new StringName() && !_engine!.PositionView.ContainsKey(playerId))
        {
            Report("I2_DANGLING_PLAYER", view, $"{role} {playerId} không có mặt trên sân");
        }
    }

    // I3: one goalkeeper and 7–11 players per team, matching the simulation's line-up.
    private void CheckLineups(LiveMatchInvariantView view)
    {
        FootballMatchSimulation? simulation = _engine!.Simulation;
        if (simulation is null)
        {
            return;
        }
        foreach (MatchTeamState state in new[] { simulation.home, simulation.away })
        {
            StringName teamId = state.team.id;
            int players = 0;
            int goalkeepers = 0;
            foreach ((StringName playerId, StringName playerTeamId) in _engine.PlayerTeams)
            {
                if (playerTeamId != teamId)
                {
                    continue;
                }
                players++;
                goalkeepers += _engine.PlayerRoles[playerId] == "GK" ? 1 : 0;
            }
            if (goalkeepers != 1)
            {
                Report("I3_GOALKEEPER_COUNT", view, $"{state.team.short_name} có {goalkeepers} thủ môn");
            }
            if (players is < 7 or > 11)
            {
                Report("I3_PLAYER_COUNT", view, $"{state.team.short_name} có {players} cầu thủ");
            }
            if (view.IsLineupSyncPending)
            {
                continue;
            }
            foreach (StringName starterId in state.squad.starter_ids)
            {
                if (!_engine.PositionView.ContainsKey(starterId))
                {
                    Report("I3_LINEUP_MISMATCH", view,
                        $"{state.team.short_name}: {starterId} thuộc đội hình nhưng không có trên sân");
                    break;
                }
            }
            if (players != state.squad.starter_ids.Count)
            {
                Report("I3_LINEUP_MISMATCH", view,
                    $"{state.team.short_name}: sân có {players}, đội hình có {state.squad.starter_ids.Count}");
            }
        }
    }

    // I4: the ball is owned, in flight, loose or waiting for a restart; never several at once, never none.
    private void CheckBallState(LiveMatchInvariantView view)
    {
        if (view.IsRestartPending && (view.IsBallInFlight || view.IsLooseBall))
        {
            Report("I4_BALL_STATE", view, "đang chờ đá lại nhưng bóng vẫn bay hoặc tự do");
        }
        if (view.IsLooseBall && view.IsBallInFlight)
        {
            Report("I4_BALL_STATE", view, "bóng vừa tự do vừa đang bay");
        }
        if (view.IsLooseBall && view.BallOwnerId != new StringName())
        {
            Report("I4_BALL_STATE", view, $"bóng tự do nhưng vẫn có người giữ {view.BallOwnerId}");
        }

        bool matchIsLive = view.Phase is not (LiveMatchPhase.AwaitingKickoff or LiveMatchPhase.HalfTime or
            LiveMatchPhase.FullTime);
        bool nobodyHasTheBall = matchIsLive && !view.IsRestartPending && !view.IsBallInFlight &&
                                !view.IsLooseBall && view.BallOwnerId == new StringName();
        if (!nobodyHasTheBall)
        {
            _unownedSince = double.NaN;
            _unownedReported = false;
            return;
        }
        if (double.IsNaN(_unownedSince))
        {
            _unownedSince = view.SimulationSeconds;
        }
        if (!_unownedReported && view.SimulationSeconds - _unownedSince > MaximumUnownedBallSeconds)
        {
            _unownedReported = true;
            Report("I4_BALL_WITHOUT_STATE", view, "bóng không có người giữ, không bay, không tự do, không chờ đá lại");
        }
    }

    // I5: finite coordinates and players inside the pitch.
    private void CheckCoordinates(LiveMatchInvariantView view)
    {
        Vector2 ball = _engine!.BallPosition;
        if (!float.IsFinite(ball.X) || !float.IsFinite(ball.Y))
        {
            Report("I5_INVALID_COORDINATE", view, "tọa độ bóng không hữu hạn");
        }
        foreach ((StringName playerId, Vector2 position) in _engine.PositionView)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            {
                Report("I5_INVALID_COORDINATE", view, $"tọa độ của {playerId} không hữu hạn");
            }
            else if (position.X is < -0.001f or > 1.001f || position.Y is < -0.001f or > 1.001f)
            {
                Report("I5_PLAYER_OUTSIDE_PITCH", view,
                    $"{playerId} ở ({Format(position.X)}, {Format(position.Y)})");
            }
        }
    }

    // I6: play must keep moving.
    private void CheckStalls(LiveMatchInvariantView view)
    {
        if (view.IsRestartPending)
        {
            if (!_hasPrevious || !_previous.IsRestartPending || _previous.RestartType != view.RestartType)
            {
                _restartSince = view.SimulationSeconds;
                _restartStallReported = false;
            }
            if (!_restartStallReported && view.SimulationSeconds - _restartSince > MaximumRestartWaitSeconds)
            {
                _restartStallReported = true;
                Report("I6_RESTART_STALL", view, $"{view.RestartType} chờ quá {MaximumRestartWaitSeconds:0} giây");
            }
        }

        bool holding = view.BallOwnerId != new StringName() && !view.IsBallInFlight && !view.IsRestartPending;
        if (!holding || view.BallOwnerId != _heldOwnerId)
        {
            _heldOwnerId = holding ? view.BallOwnerId : new StringName();
            _heldSince = view.SimulationSeconds;
            _ownerHoldReported = false;
            _goalkeeperHoldReported = false;
        }
        else
        {
            double heldSeconds = view.SimulationSeconds - _heldSince;
            if (!_ownerHoldReported && heldSeconds > MaximumOwnerHoldSeconds)
            {
                _ownerHoldReported = true;
                Report("I6_OWNER_HOLD", view, $"{view.BallOwnerId} giữ bóng quá {MaximumOwnerHoldSeconds:0} giây");
            }
            if (!_goalkeeperHoldReported && heldSeconds > MaximumGoalkeeperHoldSeconds &&
                _engine!.PlayerRoles.TryGetValue(view.BallOwnerId, out string? role) && role == "GK")
            {
                _goalkeeperHoldReported = true;
                Report("I6_GOALKEEPER_HOLD", view,
                    $"thủ môn {view.BallOwnerId} giữ bóng quá {MaximumGoalkeeperHoldSeconds:0} giây");
            }
        }

        Vector2 ball = _engine!.BallPosition;
        bool ballIsLive = !view.IsRestartPending && view.Phase is not (LiveMatchPhase.AwaitingKickoff or
            LiveMatchPhase.HalfTime or LiveMatchPhase.FullTime);
        if (!ballIsLive || !_hasPrevious ||
            FootballPitchDimensions.DistanceMeters(ball, _confinedAnchor) > ConfinedBallRadiusMeters)
        {
            _confinedAnchor = ball;
            _confinedSince = view.SimulationSeconds;
            _confinedReported = false;
            _confinedActions.Clear();
        }
        else
        {
            string action = _engine.LastActionName;
            if (action != _lastObservedAction)
            {
                _confinedActions[action] = _confinedActions.GetValueOrDefault(action) + 1;
            }
            if (!_confinedReported && view.SimulationSeconds - _confinedSince > MaximumConfinedBallSeconds)
            {
                _confinedReported = true;
                string repeated = string.Join(" / ", _confinedActions
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(4)
                    .Select(pair => $"{pair.Value}× {pair.Key}"));
                Report("I6_BALL_CONFINED", view,
                    $"bóng sống quanh quẩn trong bán kính {ConfinedBallRadiusMeters:0} m quá " +
                    $"{MaximumConfinedBallSeconds:0} giây; lặp lại: {repeated}");
            }
        }
        _lastObservedAction = _engine.LastActionName;
    }

    // I8: laws that can be verified from positions and state transitions.
    private void CheckLaws(LiveMatchInvariantView view)
    {
        if (view.OffsidesFromExemptRestarts > _knownExemptOffsides)
        {
            _knownExemptOffsides = view.OffsidesFromExemptRestarts;
            Report("I8_OFFSIDE_FROM_EXEMPT_RESTART", view, "thổi việt vị trực tiếp từ ném biên, phạt góc hoặc phát bóng");
        }
        if (!_hasPrevious)
        {
            return;
        }

        bool stoppage = view.IsRestartPending || view.Phase is LiveMatchPhase.AwaitingKickoff or
            LiveMatchPhase.HalfTime or LiveMatchPhase.FullTime;
        if (!stoppage && !_previousPlayers.SetEquals(_engine!.PositionView.Keys))
        {
            Report("I8_LINEUP_CHANGE_IN_LIVE_PLAY", view, "cầu thủ vào hoặc rời sân khi bóng đang sống");
        }

        if (_previous.IsRestartPending && !view.IsRestartPending)
        {
            CheckRestartDistances(view);
        }
    }

    private void CheckRestartDistances(LiveMatchInvariantView view)
    {
        string restartType = _previous.RestartType.ToString();
        StringName kickingTeamId = _previous.RestartTeamId;
        Vector2 spot = _previous.RestartPosition;
        float minimumDistance = RequiredRestartDistanceMeters - RestartDistanceToleranceMeters;
        if (restartType == "kickoff")
        {
            foreach ((StringName playerId, Vector2 position) in _engine!.PositionView)
            {
                StringName teamId = _engine.PlayerTeams[playerId];
                bool ownsLeftHalf = _engine.OwnGoalXOf(teamId) < 0.5f;
                float intoOpponentHalfMeters = (ownsLeftHalf ? position.X - 0.5f : 0.5f - position.X) *
                                               FootballPitchDimensions.LengthMeters;
                if (intoOpponentHalfMeters > RestartDistanceToleranceMeters)
                {
                    Report("I8_KICKOFF_POSITION", view,
                        $"{playerId} ở phần sân đối phương {Format(intoOpponentHalfMeters)} m lúc giao bóng");
                }
                if (teamId != kickingTeamId &&
                    FootballPitchDimensions.DistanceMeters(position, new Vector2(0.5f, 0.5f)) < minimumDistance)
                {
                    Report("I8_KICKOFF_POSITION", view, $"{playerId} đứng trong vòng tròn giữa sân lúc giao bóng");
                }
            }
            return;
        }

        bool checksDistance = restartType is "corner" or "penalty" ||
                              restartType == "free_kick" && !_previous.IsQuickFreeKick;
        if (!checksDistance)
        {
            return;
        }
        foreach ((StringName playerId, Vector2 position) in _engine!.PositionView)
        {
            StringName teamId = _engine.PlayerTeams[playerId];
            bool isGoalkeeper = _engine.PlayerRoles[playerId] == "GK";
            bool mustKeepDistance = restartType == "penalty"
                ? playerId != _previous.RestartTakerId && !(isGoalkeeper && teamId != kickingTeamId)
                : teamId != kickingTeamId;
            if (!mustKeepDistance)
            {
                continue;
            }
            float distance = FootballPitchDimensions.DistanceMeters(position, spot);
            if (distance < minimumDistance)
            {
                Report("I8_RESTART_DISTANCE", view,
                    $"{restartType}: {playerId} cách bóng {Format(distance)} m (cần {RequiredRestartDistanceMeters} m)");
            }
        }
    }

    private void Report(string code, LiveMatchInvariantView view, string detail)
    {
        if (!_violations.TryGetValue(code, out Tally? tally))
        {
            tally = new Tally { FirstGameSeconds = view.SimulationSeconds, FirstDetail = detail };
            _violations.Add(code, tally);
        }
        tally.Count++;
    }

    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
