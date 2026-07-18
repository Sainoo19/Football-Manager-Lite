using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class LiveMatchBalanceBatchRunner : Node
{
    public override void _Ready()
    {
        Callable.From(RunBatch).CallDeferred();
    }

    private void RunBatch()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        BalanceIssueJournal journal = new();
        try
        {
            IReadOnlyDictionary<string, string> arguments = ParseArguments(OS.GetCmdlineUserArgs());
            LiveMatchBalanceConfiguration configuration =
                LiveMatchBalanceConfiguration.CreateFootballFundamentalsV1();
            int matchCount = ParsePositiveInt(arguments, "matches", configuration.BatchMatchCount);
            int determinismAuditCount = ParseNonNegativeInt(
                arguments,
                "determinism-audits",
                configuration.DeterminismAuditCount);
            int speedParityAuditCount = ParseNonNegativeInt(
                arguments,
                "speed-audits",
                configuration.SpeedParityAuditCount);
            long firstSeed = ParseLong(arguments, "seed", 202607180000L);
            string outputDirectory = arguments.GetValueOrDefault(
                "output",
                ProjectSettings.GlobalizePath(
                    "res://.artifacts/test-reports/live-match-balance/football-fundamentals-v1-500"));
            Array<FootballTeam> teams = new SampleDataFactory().create_teams();
            if (teams.Count < 2)
            {
                throw new InvalidOperationException("At least two sample teams are required for a batch.");
            }

            LiveMatchBalanceAnalyzer analyzer = new();
            HeadlessLiveMatchRunner runner = new();
            List<LiveMatchBalanceRecord> records = new(matchCount);
            GD.Print($"BALANCE_BATCH_START matches={matchCount} seed={firstSeed}");
            for (int index = 0; index < matchCount; index++)
            {
                long seed = firstSeed + index;
                (FootballTeam home, FootballTeam away) = SelectTeams(teams, index);
                try
                {
                    HeadlessLiveMatchResult result = runner.RunToFullTime(
                        new FootballMatchSimulation().setup(home, away, seed),
                        MatchPlaybackSpeed.Fastest);
                    LiveMatchBalanceRecord record = analyzer.CreateRecord(index + 1, result);
                    analyzer.ValidateMatch(result, record, journal);
                    records.Add(record);
                }
                catch (Exception exception)
                {
                    journal.AddCodeBug(
                        BalanceIssueSeverity.Error,
                        "BATCH_MATCH_EXCEPTION",
                        $"Trận batch phát sinh exception: {exception.GetType().Name}: {exception.Message}",
                        seed);
                }

                if ((index + 1) % 25 == 0 || index + 1 == matchCount)
                {
                    GD.Print($"BALANCE_BATCH_PROGRESS completed={index + 1}/{matchCount}");
                }
            }

            RunDeterminismAudit(records, teams, runner, analyzer, determinismAuditCount, journal);
            RunSpeedParityAudit(records, teams, runner, analyzer, speedParityAuditCount, journal);
            LiveMatchBatchSummary summary = analyzer.AnalyzeBatch(
                records,
                matchCount,
                configuration,
                journal);
            new BalanceReportWriter().Write(outputDirectory, configuration, summary, records, journal);
            stopwatch.Stop();
            GD.Print(
                $"BALANCE_BATCH_COMPLETE completed={records.Count}/{matchCount} " +
                $"issues={journal.Issues.Count} elapsed_seconds={stopwatch.Elapsed.TotalSeconds:0.00} " +
                $"output={outputDirectory}");
            GetTree().Quit(journal.Issues.Any(issue =>
                issue.Category == BalanceIssueCategory.CodeBug &&
                issue.Severity == BalanceIssueSeverity.Error) ? 1 : 0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BALANCE_BATCH_FATAL {exception}");
            GetTree().Quit(2);
        }
    }

    private static void RunDeterminismAudit(
        IReadOnlyList<LiveMatchBalanceRecord> records,
        Array<FootballTeam> teams,
        HeadlessLiveMatchRunner runner,
        LiveMatchBalanceAnalyzer analyzer,
        int requestedAuditCount,
        BalanceIssueJournal journal)
    {
        int auditCount = Math.Min(requestedAuditCount, records.Count);
        for (int index = 0; index < auditCount; index++)
        {
            LiveMatchBalanceRecord expected = records[index];
            (FootballTeam home, FootballTeam away) = SelectTeams(teams, expected.MatchIndex - 1);
            HeadlessLiveMatchResult repeatedResult = runner.RunToFullTime(
                new FootballMatchSimulation().setup(home, away, expected.Seed),
                MatchPlaybackSpeed.Fastest);
            LiveMatchBalanceRecord repeated = analyzer.CreateRecord(expected.MatchIndex, repeatedResult);
            if (!analyzer.AreEquivalent(expected, repeated))
            {
                journal.AddCodeBug(
                    BalanceIssueSeverity.Error,
                    "NON_DETERMINISTIC_SEED",
                    "Hai lần chạy cùng seed không cho cùng diễn biến và thống kê.",
                    expected.Seed);
            }
        }
    }

    private static void RunSpeedParityAudit(
        IReadOnlyList<LiveMatchBalanceRecord> records,
        Array<FootballTeam> teams,
        HeadlessLiveMatchRunner runner,
        LiveMatchBalanceAnalyzer analyzer,
        int requestedAuditCount,
        BalanceIssueJournal journal)
    {
        int auditCount = Math.Min(requestedAuditCount, records.Count);
        for (int index = 0; index < auditCount; index++)
        {
            LiveMatchBalanceRecord expected = records[index];
            (FootballTeam home, FootballTeam away) = SelectTeams(teams, expected.MatchIndex - 1);
            HeadlessLiveMatchResult realTimeResult = runner.RunToFullTime(
                new FootballMatchSimulation().setup(home, away, expected.Seed),
                MatchPlaybackSpeed.RealTime,
                realStepSeconds: 60d);
            LiveMatchBalanceRecord realTime = analyzer.CreateRecord(expected.MatchIndex, realTimeResult);
            if (!analyzer.AreEquivalent(expected, realTime))
            {
                journal.AddCodeBug(
                    BalanceIssueSeverity.Error,
                    "PLAYBACK_SPEED_DIVERGENCE",
                    "Realtime và tăng tốc tạo ra diễn biến khác nhau với cùng seed.",
                    expected.Seed);
            }
        }
    }

    private static (FootballTeam Home, FootballTeam Away) SelectTeams(Array<FootballTeam> teams, int index)
    {
        int homeIndex = index % teams.Count;
        int awayIndex = (index * 2 + 1) % teams.Count;
        if (awayIndex == homeIndex)
        {
            awayIndex = (awayIndex + 1) % teams.Count;
        }
        return (teams[homeIndex], teams[awayIndex]);
    }

    private static IReadOnlyDictionary<string, string> ParseArguments(string[] rawArguments)
    {
        System.Collections.Generic.Dictionary<string, string> arguments =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawArgument in rawArguments)
        {
            if (!rawArgument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }
            int separator = rawArgument.IndexOf('=');
            if (separator <= 2 || separator == rawArgument.Length - 1)
            {
                continue;
            }
            arguments[rawArgument[2..separator]] = rawArgument[(separator + 1)..];
        }
        return arguments;
    }

    private static int ParsePositiveInt(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        int fallback)
    {
        return arguments.TryGetValue(key, out string? text) &&
               int.TryParse(text, out int value) &&
               value > 0
            ? value
            : fallback;
    }

    private static long ParseLong(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        long fallback)
    {
        return arguments.TryGetValue(key, out string? text) && long.TryParse(text, out long value)
            ? value
            : fallback;
    }

    private static int ParseNonNegativeInt(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        int fallback)
    {
        return arguments.TryGetValue(key, out string? text) &&
               int.TryParse(text, out int value) &&
               value >= 0
            ? value
            : fallback;
    }
}
