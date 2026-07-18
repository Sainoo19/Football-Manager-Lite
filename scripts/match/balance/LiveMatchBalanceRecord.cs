using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    public IReadOnlyList<BalanceGoalRecord> GoalRecords { get; }
    public double ShotConversion => Shots == 0 ? 0d : (double)Goals / Shots;
    public double PassCompletion => PassAttempts == 0 ? 0d : (double)CompletedPasses / PassAttempts;

    public IReadOnlyDictionary<string, double> GetMetricValues()
    {
        return new Dictionary<string, double>
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
    }
}
