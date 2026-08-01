using Godot;

public readonly struct TeamPhaseContext
{
    public TeamPhaseContext(
        StringName teamId,
        bool hasPossession,
        bool isRestart,
        bool isPossessionContested,
        float gameTimeSeconds,
        float attackProgress,
        float possessionDurationSeconds,
        int playersAheadOfBall,
        int playersBehindBall,
        int regionalNumericalAdvantage,
        float goalDistanceMeters,
        float forwardSpaceMeters,
        float ballProgressionMetersPerSecond,
        float compactnessMeters)
    {
        TeamId = teamId;
        HasPossession = hasPossession;
        IsRestart = isRestart;
        IsPossessionContested = isPossessionContested;
        GameTimeSeconds = gameTimeSeconds;
        AttackProgress = attackProgress;
        PossessionDurationSeconds = possessionDurationSeconds;
        PlayersAheadOfBall = playersAheadOfBall;
        PlayersBehindBall = playersBehindBall;
        RegionalNumericalAdvantage = regionalNumericalAdvantage;
        GoalDistanceMeters = goalDistanceMeters;
        ForwardSpaceMeters = forwardSpaceMeters;
        BallProgressionMetersPerSecond = ballProgressionMetersPerSecond;
        CompactnessMeters = compactnessMeters;
    }

    public StringName TeamId { get; }
    public bool HasPossession { get; }
    public bool IsRestart { get; }
    public bool IsPossessionContested { get; }
    public float GameTimeSeconds { get; }
    public float AttackProgress { get; }
    public float PossessionDurationSeconds { get; }
    public int PlayersAheadOfBall { get; }
    public int PlayersBehindBall { get; }
    public int RegionalNumericalAdvantage { get; }
    public float GoalDistanceMeters { get; }
    public float ForwardSpaceMeters { get; }
    public float BallProgressionMetersPerSecond { get; }
    public float CompactnessMeters { get; }
}
