using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public readonly struct LiveMatchMetrics
{
    public LiveMatchMetrics(
        int completedPasses,
        int passAttempts,
        int interceptions,
        int dribbles,
        int foulsCommitted,
        int restarts,
        int looseBallRecoveries,
        int clearances,
        int firstTouchErrors,
        int groundDuelExchanges,
        int tackleAttempts,
        int shoulderChallenges,
        int groundDuelLooseBalls,
        int maxGroundDuelTouches,
        int carrierEscapes,
        int tacklesWon,
        float minimumObservedGroundDuelSeparationMeters,
        int aerialDuels,
        int headersWon,
        int defensiveHeaders,
        int headerShots,
        int goalkeeperAerialCatches,
        int goalkeeperPunches,
        int aerialSecondBalls)
    {
        CompletedPasses = completedPasses;
        PassAttempts = passAttempts;
        Interceptions = interceptions;
        Dribbles = dribbles;
        FoulsCommitted = foulsCommitted;
        Restarts = restarts;
        LooseBallRecoveries = looseBallRecoveries;
        Clearances = clearances;
        FirstTouchErrors = firstTouchErrors;
        GroundDuelExchanges = groundDuelExchanges;
        TackleAttempts = tackleAttempts;
        ShoulderChallenges = shoulderChallenges;
        GroundDuelLooseBalls = groundDuelLooseBalls;
        MaxGroundDuelTouches = maxGroundDuelTouches;
        CarrierEscapes = carrierEscapes;
        TacklesWon = tacklesWon;
        MinimumObservedGroundDuelSeparationMeters = minimumObservedGroundDuelSeparationMeters;
        AerialDuels = aerialDuels;
        HeadersWon = headersWon;
        DefensiveHeaders = defensiveHeaders;
        HeaderShots = headerShots;
        GoalkeeperAerialCatches = goalkeeperAerialCatches;
        GoalkeeperPunches = goalkeeperPunches;
        AerialSecondBalls = aerialSecondBalls;
    }

    public int CompletedPasses { get; }
    public int PassAttempts { get; }
    public int Interceptions { get; }
    public int Dribbles { get; }
    public int FoulsCommitted { get; }
    public int Restarts { get; }
    public int LooseBallRecoveries { get; }
    public int Clearances { get; }
    public int FirstTouchErrors { get; }
    public int GroundDuelExchanges { get; }
    public int TackleAttempts { get; }
    public int ShoulderChallenges { get; }
    public int GroundDuelLooseBalls { get; }
    public int MaxGroundDuelTouches { get; }
    public int CarrierEscapes { get; }
    public int TacklesWon { get; }
    public float MinimumObservedGroundDuelSeparationMeters { get; }
    public int AerialDuels { get; }
    public int HeadersWon { get; }
    public int DefensiveHeaders { get; }
    public int HeaderShots { get; }
    public int GoalkeeperAerialCatches { get; }
    public int GoalkeeperPunches { get; }
    public int AerialSecondBalls { get; }
    public int ResolvedActions => CompletedPasses + Interceptions + Dribbles + LooseBallRecoveries + Clearances;
}

public sealed class LiveMatchSnapshot
{
    public LiveMatchSnapshot(
        IReadOnlyDictionary<StringName, Vector2> positions,
        IReadOnlyDictionary<StringName, Vector2> targetPositions,
        Vector2 ballPosition,
        StringName ballOwnerId,
        StringName activeTeamId,
        LiveMatchPhase phase,
        double elapsedGameSeconds,
        string lastActionName,
        bool isPlaying,
        bool isBallInFlight,
        bool isLooseBall,
        StringName pendingRestartType,
        LiveMatchMetrics metrics,
        LiveMatchAnalyticsSnapshot analytics)
    {
        Positions = CopyPositions(positions);
        TargetPositions = CopyPositions(targetPositions);
        BallPosition = ballPosition;
        BallOwnerId = ballOwnerId;
        ActiveTeamId = activeTeamId;
        Phase = phase;
        ElapsedGameSeconds = elapsedGameSeconds;
        LastActionName = lastActionName;
        IsPlaying = isPlaying;
        IsBallInFlight = isBallInFlight;
        IsLooseBall = isLooseBall;
        PendingRestartType = pendingRestartType;
        Metrics = metrics;
        Analytics = analytics;
    }

    public IReadOnlyDictionary<StringName, Vector2> Positions { get; }
    public IReadOnlyDictionary<StringName, Vector2> TargetPositions { get; }
    public Vector2 BallPosition { get; }
    public StringName BallOwnerId { get; }
    public StringName ActiveTeamId { get; }
    public LiveMatchPhase Phase { get; }
    public double ElapsedGameSeconds { get; }
    public string LastActionName { get; }
    public bool IsPlaying { get; }
    public bool IsBallInFlight { get; }
    public bool IsLooseBall { get; }
    public StringName PendingRestartType { get; }
    public LiveMatchMetrics Metrics { get; }
    public LiveMatchAnalyticsSnapshot Analytics { get; }

    private static IReadOnlyDictionary<StringName, Vector2> CopyPositions(
        IReadOnlyDictionary<StringName, Vector2> source)
    {
        return new ReadOnlyDictionary<StringName, Vector2>(new Dictionary<StringName, Vector2>(source));
    }
}
