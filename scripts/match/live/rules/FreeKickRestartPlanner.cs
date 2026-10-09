using Godot;

public readonly struct FreeKickRestartPlan
{
    public FreeKickRestartPlan(
        bool isQuick,
        float preparationDurationSeconds,
        float ballMovementStartsAfterSeconds,
        float ballPlacedAfterSeconds,
        Vector2 ballStart,
        Vector2 restartPosition)
    {
        IsQuick = isQuick;
        PreparationDurationSeconds = preparationDurationSeconds;
        BallMovementStartsAfterSeconds = ballMovementStartsAfterSeconds;
        BallPlacedAfterSeconds = ballPlacedAfterSeconds;
        BallStart = ballStart;
        RestartPosition = restartPosition;
    }

    public bool IsQuick { get; }
    public float PreparationDurationSeconds { get; }
    public float BallMovementStartsAfterSeconds { get; }
    public float BallPlacedAfterSeconds { get; }
    public Vector2 BallStart { get; }
    public Vector2 RestartPosition { get; }

    public bool IsBallPlaced(float elapsedSeconds)
    {
        return elapsedSeconds >= BallPlacedAfterSeconds;
    }

    public Vector2 BallPositionAt(float elapsedSeconds)
    {
        if (elapsedSeconds <= BallMovementStartsAfterSeconds)
        {
            return BallStart;
        }
        if (elapsedSeconds >= BallPlacedAfterSeconds)
        {
            return RestartPosition;
        }

        float movementDuration = Mathf.Max(
            BallPlacedAfterSeconds - BallMovementStartsAfterSeconds,
            0.01f);
        float progress = Mathf.Clamp(
            (elapsedSeconds - BallMovementStartsAfterSeconds) / movementDuration,
            0f,
            1f);
        return BallStart.Lerp(RestartPosition, progress);
    }
}

public sealed class FreeKickRestartPlanner
{
    public const float RequiredDefenderDistanceMeters = 9.15f;
    public const float QuickPreparationDurationSeconds = 1.8f;
    public const float CeremonialPreparationDurationSeconds = 5.5f;

    public FreeKickRestartPlan CreatePlan(
        Vector2 ballStart,
        Vector2 restartPosition,
        bool allowsQuickRestart,
        float quickRestartRoll)
    {
        bool isQuick = allowsQuickRestart && quickRestartRoll < 0.35f;
        return isQuick
            ? new FreeKickRestartPlan(
                true,
                QuickPreparationDurationSeconds,
                0.20f,
                0.85f,
                ballStart,
                restartPosition)
            : new FreeKickRestartPlan(
                false,
                CeremonialPreparationDurationSeconds,
                0.80f,
                3.20f,
                ballStart,
                restartPosition);
    }

    public Vector2 EnsureRequiredDefenderDistance(
        Vector2 defenderPosition,
        Vector2 restartPosition,
        bool isQuickRestart)
    {
        if (isQuickRestart)
        {
            return defenderPosition;
        }

        Vector2 defenderMeters = FootballPitchDimensions.ToMeters(defenderPosition);
        Vector2 restartMeters = FootballPitchDimensions.ToMeters(restartPosition);
        Vector2 fromBall = defenderMeters - restartMeters;
        float distanceMeters = fromBall.Length();
        if (distanceMeters >= RequiredDefenderDistanceMeters + 0.2f)
        {
            return defenderPosition;
        }

        Vector2 direction = distanceMeters > 0.05f ? fromBall / distanceMeters : Vector2.Down;
        Vector2 legalPositionMeters = restartMeters + direction * (RequiredDefenderDistanceMeters + 0.3f);
        Vector2 legalPosition = SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(legalPositionMeters));
        if (FootballPitchDimensions.DistanceMeters(legalPosition, restartPosition) <
            RequiredDefenderDistanceMeters + 0.2f)
        {
            // A radial push outside the pitch cannot satisfy the exclusion zone after clamping.
            Vector2 towardCenter = FootballPitchDimensions.ToMeters(new Vector2(0.5f, 0.5f)) - restartMeters;
            legalPosition = SpaceEvaluator.ClampToPitch(FootballPitchDimensions.ToNormalized(
                restartMeters + towardCenter.Normalized() * (RequiredDefenderDistanceMeters + 0.3f)));
        }
        return legalPosition;
    }
}
