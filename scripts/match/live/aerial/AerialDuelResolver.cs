using System.Collections.Generic;
using Godot;

public enum AerialDuelOutcome
{
    LooseSecondBall,
    HeaderPass,
    HeaderShot,
    DefensiveHeaderClearance,
    GoalkeeperCatch,
    GoalkeeperPunch
}

public readonly struct AerialDuelCandidate
{
    public AerialDuelCandidate(
        StringName playerId,
        bool isAttackingTeam,
        bool isGoalkeeper,
        string role,
        int heading,
        int jumpingReach,
        int strength,
        int positioning,
        int composure,
        int goalkeeping,
        float distanceToLandingMeters,
        float arrivalMarginSeconds,
        float distanceToAttackingGoalMeters,
        bool hasTeammateOption,
        float contestRoll)
    {
        PlayerId = playerId;
        IsAttackingTeam = isAttackingTeam;
        IsGoalkeeper = isGoalkeeper;
        Role = role;
        Heading = heading;
        JumpingReach = jumpingReach;
        Strength = strength;
        Positioning = positioning;
        Composure = composure;
        Goalkeeping = goalkeeping;
        DistanceToLandingMeters = distanceToLandingMeters;
        ArrivalMarginSeconds = arrivalMarginSeconds;
        DistanceToAttackingGoalMeters = distanceToAttackingGoalMeters;
        HasTeammateOption = hasTeammateOption;
        ContestRoll = contestRoll;
    }

    public StringName PlayerId { get; }
    public bool IsAttackingTeam { get; }
    public bool IsGoalkeeper { get; }
    public string Role { get; }
    public int Heading { get; }
    public int JumpingReach { get; }
    public int Strength { get; }
    public int Positioning { get; }
    public int Composure { get; }
    public int Goalkeeping { get; }
    public float DistanceToLandingMeters { get; }
    public float ArrivalMarginSeconds { get; }
    public float DistanceToAttackingGoalMeters { get; }
    public bool HasTeammateOption { get; }
    public float ContestRoll { get; }
}

public readonly struct AerialDuelResolution
{
    public AerialDuelResolution(StringName winnerId, AerialDuelOutcome outcome)
    {
        WinnerId = winnerId;
        Outcome = outcome;
    }

    public StringName WinnerId { get; }
    public AerialDuelOutcome Outcome { get; }
    public bool HasWinner => WinnerId != new StringName();
}

public sealed class AerialDuelResolver
{
    private const float MaximumContestDistanceMeters = 3.2f;
    private readonly float _headerShotProbability;

    public AerialDuelResolver(float headerShotProbability = 0.74f)
    {
        _headerShotProbability = headerShotProbability;
    }

    public AerialDuelResolution Resolve(
        IReadOnlyList<AerialDuelCandidate> candidates,
        int nearbyOpponentCount,
        float actionRoll)
    {
        AerialDuelCandidate? winner = null;
        float winningScore = float.NegativeInfinity;
        foreach (AerialDuelCandidate candidate in candidates)
        {
            if (candidate.DistanceToLandingMeters > MaximumContestDistanceMeters)
            {
                continue;
            }

            float score = ContestScore(candidate);
            if (score > winningScore)
            {
                winningScore = score;
                winner = candidate;
            }
        }

        if (!winner.HasValue)
        {
            return new AerialDuelResolution(new StringName(), AerialDuelOutcome.LooseSecondBall);
        }

        AerialDuelCandidate selected = winner.Value;
        if (selected.IsGoalkeeper)
        {
            float catchChance = Mathf.Clamp(
                0.42f + (selected.Goalkeeping - 60) / 150f +
                (selected.Composure - 60) / 320f - nearbyOpponentCount * 0.07f,
                0.24f,
                0.84f);
            return new AerialDuelResolution(
                selected.PlayerId,
                actionRoll < catchChance
                    ? AerialDuelOutcome.GoalkeeperCatch
                    : AerialDuelOutcome.GoalkeeperPunch);
        }

        if (!selected.IsAttackingTeam)
        {
            return new AerialDuelResolution(
                selected.PlayerId,
                AerialDuelOutcome.DefensiveHeaderClearance);
        }

        bool attackingRole = selected.Role is "ST" or "LW" or "RW" or "AM";
        if (attackingRole && selected.DistanceToAttackingGoalMeters <= 16f &&
            actionRoll < _headerShotProbability)
        {
            return new AerialDuelResolution(selected.PlayerId, AerialDuelOutcome.HeaderShot);
        }
        if (selected.HasTeammateOption && actionRoll < 0.86f)
        {
            return new AerialDuelResolution(selected.PlayerId, AerialDuelOutcome.HeaderPass);
        }
        return new AerialDuelResolution(selected.PlayerId, AerialDuelOutcome.LooseSecondBall);
    }

    private static float ContestScore(AerialDuelCandidate candidate)
    {
        float technicalScore = candidate.IsGoalkeeper
            ? candidate.Goalkeeping * 0.42f +
              candidate.JumpingReach * 0.22f +
              candidate.Positioning * 0.18f +
              candidate.Composure * 0.12f +
              candidate.Strength * 0.06f
            : candidate.Heading * 0.30f +
              candidate.JumpingReach * 0.25f +
              candidate.Strength * 0.18f +
              candidate.Positioning * 0.17f +
              candidate.Composure * 0.10f;
        float arrivalScore = Mathf.Clamp(candidate.ArrivalMarginSeconds, -0.5f, 0.5f) * 12f;
        float distancePenalty = candidate.DistanceToLandingMeters * 3.2f;
        float variation = (Mathf.Clamp(candidate.ContestRoll, 0f, 1f) - 0.5f) * 14f;
        return technicalScore + arrivalScore + variation - distancePenalty;
    }
}
