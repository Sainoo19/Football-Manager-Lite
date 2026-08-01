using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class BalanceGoalRecord
{
    public BalanceGoalRecord(float distanceMeters, string situation)
    {
        DistanceMeters = distanceMeters;
        Situation = situation;
    }

    public float DistanceMeters { get; }
    public string Situation { get; }
}

public sealed class LiveMatchBalanceRecord
{
    public LiveMatchBalanceRecord(
        int matchIndex,
        long seed,
        string homeTeam,
        string awayTeam,
        int goals,
        int shots,
        int shotsOnTarget,
        int passAttempts,
        int completedPasses,
        int dribbles,
        int successfulDribbles,
        int groundDuelWins,
        int groundDuelExchanges,
        int aerialDuels,
        int headersWon,
        int fouls,
        int yellowCards,
        int redCards,
        int offsides,
        int penalties,
        int corners,
        int goalKicks,
        int throwIns,
        int freeKicks,
        float averagePossessionSpellSeconds,
        int possessionChanges,
        string eventSequenceSignature,
        string finalSnapshotSignature,
        FootballActionMetricsSnapshot actionMetrics,
        TeamPhaseMetricsSnapshot teamPhaseMetrics,
        OffBallMetricsSnapshot offBallMetrics,
        IReadOnlyList<BalanceGoalRecord> goalRecords)
    {
        MatchIndex = matchIndex;
        Seed = seed;
        HomeTeam = homeTeam;
        AwayTeam = awayTeam;
        Goals = goals;
        Shots = shots;
        ShotsOnTarget = shotsOnTarget;
        PassAttempts = passAttempts;
        CompletedPasses = completedPasses;
        Dribbles = dribbles;
        SuccessfulDribbles = successfulDribbles;
        GroundDuelWins = groundDuelWins;
        GroundDuelExchanges = groundDuelExchanges;
        AerialDuels = aerialDuels;
        HeadersWon = headersWon;
        Fouls = fouls;
        YellowCards = yellowCards;
        RedCards = redCards;
        Offsides = offsides;
        Penalties = penalties;
        Corners = corners;
        GoalKicks = goalKicks;
        ThrowIns = throwIns;
        FreeKicks = freeKicks;
        AveragePossessionSpellSeconds = averagePossessionSpellSeconds;
        PossessionChanges = possessionChanges;
        EventSequenceSignature = eventSequenceSignature;
        FinalSnapshotSignature = finalSnapshotSignature;
        ActionMetrics = actionMetrics;
        TeamPhaseMetrics = teamPhaseMetrics;
        OffBallMetrics = offBallMetrics;
        GoalRecords = new ReadOnlyCollection<BalanceGoalRecord>(new List<BalanceGoalRecord>(goalRecords));
    }

    public int MatchIndex { get; }
    public long Seed { get; }
    public string HomeTeam { get; }
    public string AwayTeam { get; }
    public int Goals { get; }
    public int Shots { get; }
    public int ShotsOnTarget { get; }
    public int PassAttempts { get; }
    public int CompletedPasses { get; }
    public int Dribbles { get; }
    public int SuccessfulDribbles { get; }
    public int GroundDuelWins { get; }
    public int GroundDuelExchanges { get; }
    public int AerialDuels { get; }
    public int HeadersWon { get; }
    public int Fouls { get; }
    public int YellowCards { get; }
    public int RedCards { get; }
    public int Offsides { get; }
    public int Penalties { get; }
    public int Corners { get; }
    public int GoalKicks { get; }
    public int ThrowIns { get; }
    public int FreeKicks { get; }
    public float AveragePossessionSpellSeconds { get; }
    public int PossessionChanges { get; }
    public string EventSequenceSignature { get; }
    public string FinalSnapshotSignature { get; }
    public FootballActionMetricsSnapshot ActionMetrics { get; }
    public TeamPhaseMetricsSnapshot TeamPhaseMetrics { get; }
    public OffBallMetricsSnapshot OffBallMetrics { get; }
    public IReadOnlyList<BalanceGoalRecord> GoalRecords { get; }
    public double ShotConversion => Shots == 0 ? 0d : (double)Goals / Shots;
    public double PassCompletion => PassAttempts == 0 ? 0d : (double)CompletedPasses / PassAttempts;

    public IReadOnlyDictionary<string, double> GetMetricValues()
    {
        Dictionary<string, double> metrics = new()
        {
            { "goals", Goals },
            { "shots", Shots },
            { "shots_on_target", ShotsOnTarget },
            { "shot_conversion", ShotConversion },
            { "pass_completion", PassCompletion },
            { "dribbles", Dribbles },
            { "successful_dribbles", SuccessfulDribbles },
            { "ground_duel_wins", GroundDuelWins },
            { "aerial_duels", AerialDuels },
            { "fouls", Fouls },
            { "yellow_cards", YellowCards },
            { "red_cards", RedCards },
            { "offsides", Offsides },
            { "penalties", Penalties },
            { "corners", Corners },
            { "goal_kicks", GoalKicks },
            { "throw_ins", ThrowIns },
            { "possession_spell_seconds", AveragePossessionSpellSeconds },
            { "possession_changes", PossessionChanges }
        };
        foreach (FootballActionType actionType in System.Enum.GetValues<FootballActionType>())
        {
            metrics[$"action_{actionType.ToString().ToLowerInvariant()}"] =
                ActionMetrics.AttemptsByType.GetValueOrDefault(actionType);
        }
        metrics["action_score_margin"] = ActionMetrics.AverageScoreMargin;
        metrics["backward_passes"] = ActionMetrics.BackwardPasses;
        metrics["sideways_passes"] = ActionMetrics.SidewaysPasses;
        metrics["forward_passes"] = ActionMetrics.ForwardPasses;
        metrics["progressive_action_rate"] = ActionMetrics.ProgressiveActionRate;
        metrics["forced_actions"] = ActionMetrics.ForcedActions;
        metrics["decision_cancellations"] = ActionMetrics.DecisionCancellations;
        metrics["no_valid_actions"] = ActionMetrics.NoValidActions;
        foreach (LiveTeamPhase phase in System.Enum.GetValues<LiveTeamPhase>())
        {
            metrics[$"phase_seconds_{phase.ToString().ToLowerInvariant()}"] =
                TeamPhaseMetrics.DurationSecondsByPhase.GetValueOrDefault(phase);
        }
        metrics["phase_transitions"] = TeamPhaseMetrics.TransitionCounts.Values.Sum();
        metrics["counter_attacks"] = TeamPhaseMetrics.CounterAttacks;
        metrics["counter_attack_conversion"] = TeamPhaseMetrics.CounterAttackConversion;
        metrics["time_to_organize_seconds"] = TeamPhaseMetrics.AverageOrganizationSeconds;
        metrics["final_third_rest_defence_players"] = TeamPhaseMetrics.AverageFinalThirdRestDefencePlayers;
        metrics["emergency_defence_entries"] = TeamPhaseMetrics.EmergencyDefenceEntries;
        metrics["team_width_meters"] = OffBallMetrics.AverageTeamWidthMeters;
        metrics["team_length_meters"] = OffBallMetrics.AverageTeamLengthMeters;
        metrics["team_compactness_meters"] = OffBallMetrics.AverageCompactnessMeters;
        metrics["same_target_collisions"] = OffBallMetrics.SameTargetCollisions;
        metrics["passing_options"] = OffBallMetrics.AveragePassingOptions;
        metrics["runner_lane_diversity"] = OffBallMetrics.AverageRunnerLaneDiversity;
        metrics["off_ball_rest_defence_players"] = OffBallMetrics.AverageRestDefencePlayers;
        metrics["unmarked_dangerous_receivers"] = OffBallMetrics.AverageUnmarkedDangerousReceivers;
        metrics["box_near_post_occupations"] = OffBallMetrics.NearPostOccupations;
        metrics["box_far_post_occupations"] = OffBallMetrics.FarPostOccupations;
        metrics["box_cutback_occupations"] = OffBallMetrics.CutBackOccupations;
        return metrics;
    }
}
