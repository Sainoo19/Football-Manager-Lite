using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class MatchFlowDiagnosticRunner : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        try
        {
            Dictionary<string, string> arguments = OS.GetCmdlineUserArgs()
                .Where(value => value.StartsWith("--") && value.Contains('='))
                .Select(value => value[2..].Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1]);
            string output = arguments.GetValueOrDefault("output", ".artifacts/test-reports/match-flow/before");
            Directory.CreateDirectory(output);
            if (arguments.TryGetValue("scenario", out string? scenarioName))
            {
                RunScenario(Enum.Parse<MatchScenarioKind>(scenarioName), output);
                GetTree().Quit();
                return;
            }
            Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
            string quality = arguments.GetValueOrDefault("quality", "original");
            if (quality == "equal")
            {
                foreach (FootballTeam team in teams)
                {
                    foreach (FootballPlayer player in team.players)
                    {
                        player.setup(player.id, player.display_name, player.position,
                            player.age, player.nationality, 72);
                        player.fitness = 100;
                        player.form = 60;
                    }
                }
            }
            foreach (int index in arguments.GetValueOrDefault("indices", "3,6,14").Split(',').Select(int.Parse))
            {
                int homeIndex = index % teams.Count;
                int awayIndex = (index * 2 + 1) % teams.Count;
                if (awayIndex == homeIndex)
                {
                    awayIndex = (awayIndex + 1) % teams.Count;
                }
                long seed = 202610080000L + index;
                FootballMatchSimulation simulation = new FootballMatchSimulation().setup(
                    teams[homeIndex], teams[awayIndex], seed);
                List<object> delays = new();
                StringName previousRestart = new();
                double startedAt = 0d;
                double maximumRestartSeconds = 0d;
                float maximumOwnerHoldSeconds = 0f;
                double nextSample = 0d;
                List<object> samples = new();
                bool captureReplay = arguments.GetValueOrDefault("replay", "false") == "true";
                List<object> frames = new();
                double nextFrame = 0d;
                HeadlessLiveMatchResult result = new HeadlessLiveMatchRunner().RunToFullTime(
                    simulation, realStepSeconds: captureReplay ? 0.0001d : 0.0005d, observe: engine =>
                    {
                        double time = engine.ElapsedGameSeconds;
                        if (captureReplay && time >= nextFrame &&
                            (time < 180d || time is >= 2640d and < 2820d || time > 5100d))
                        {
                            nextFrame = time + 0.2d;
                            frames.Add(new
                            {
                                time, ball = new { engine.BallPosition.X, engine.BallPosition.Y },
                                height = engine.BallVisualHeight,
                                owner = engine.CurrentBallOwnerId.ToString(),
                                restart = engine.PendingRestartType.ToString(),
                                players = engine.PositionView.Select(pair => new
                                {
                                    id = pair.Key.ToString(), team = engine.PlayerTeams[pair.Key].ToString(),
                                    role = engine.PlayerRoles[pair.Key], x = pair.Value.X, y = pair.Value.Y,
                                    targetX = engine.TargetPositionView[pair.Key].X,
                                    targetY = engine.TargetPositionView[pair.Key].Y,
                                    assignment = engine.CurrentIntents.TryGetValue(pair.Key, out PlayerIntent? intent)
                                        ? intent.Assignment.ToString() : ""
                                }).ToArray()
                            });
                        }
                        StringName restart = engine.PendingRestartType;
                        maximumOwnerHoldSeconds = engine.MaximumObservedOwnerHoldSeconds;
                        if (restart != previousRestart)
                        {
                            startedAt = engine.ElapsedGameSeconds;
                            previousRestart = restart;
                        }
                        double waiting = restart == new StringName() ? 0d : engine.ElapsedGameSeconds - startedAt;
                        maximumRestartSeconds = Math.Max(maximumRestartSeconds, waiting);
                        if (waiting > 60d && delays.Count == 0)
                        {
                            delays.Add(new
                            {
                                time = engine.ElapsedGameSeconds, type = restart.ToString(),
                                ball = new { engine.BallPosition.X, engine.BallPosition.Y },
                                positions = engine.PositionView.Select(pair => new
                                {
                                    id = pair.Key.ToString(), role = engine.PlayerRoles[pair.Key],
                                    team = engine.PlayerTeams[pair.Key].ToString(),
                                    ballDistance = FootballPitchDimensions.DistanceMeters(pair.Value, engine.BallPosition),
                                    targetDistance = FootballPitchDimensions.DistanceMeters(pair.Value, engine.TargetPositionView[pair.Key])
                                }).ToArray()
                            });
                        }
                        if (engine.ElapsedGameSeconds >= nextSample)
                        {
                            nextSample += 60d;
                            samples.Add(new
                            {
                                time = engine.ElapsedGameSeconds, restart = restart.ToString(),
                                owner = engine.CurrentBallOwnerId.ToString(),
                                action = engine.LastActionDecision?.Selected.ActionType.ToString(),
                                looseBall = engine.IsLooseBall,
                                passes = engine.PassAttempts, engine.MaximumObservedOwnerHoldSeconds,
                                ball = new { engine.BallPosition.X, engine.BallPosition.Y }
                            });
                        }
                    });
                LiveMatchBalanceRecord record = new LiveMatchBalanceAnalyzer().CreateRecord(index + 1, result);
                string json = JsonSerializer.Serialize(new
                {
                    seed, quality, metrics = record.GetMetricValues(), maximumRestartSeconds,
                    maximumOwnerHoldSeconds,
                    delays, samples, shots = result.Shots, aerial = result.Aerial, frames
                }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
                File.WriteAllText(Path.Combine(output, $"{seed}.json"), json);
                GD.Print($"FLOW_DIAGNOSTIC seed={seed} max_restart={maximumRestartSeconds:0.0} delays={delays.Count}");
                simulation.Dispose();
            }
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"FLOW_DIAGNOSTIC_FAILED {exception}");
            GetTree().Quit(1);
        }
    }

    private static void RunScenario(MatchScenarioKind scenario, string output)
    {
        Godot.Collections.Array<FootballTeam> teams = new SampleDataFactory().create_teams();
        FootballMatchSimulation simulation = new FootballMatchSimulation().setup(teams[0], teams[1], 2026080205);
        simulation.use_live_pitch_events = true;
        LiveMatchEngine engine = new();
        engine.SetMatch(simulation);
        if (!engine.StartScenario(scenario))
        {
            throw new InvalidOperationException("Unable to start diagnostic scenario.");
        }
        engine.SetPlaying(true);
        List<object> samples = new();
        LiveTeamPhase previous = LiveTeamPhase.SetPiece;
        for (int step = 0; step < 2400; step++)
        {
            engine.AdvanceGameTime(0.05d);
            LiveTeamPhase phase = engine.CurrentTeamPhaseStates[teams[0].id].Phase;
            if (phase != previous || step % 100 == 0)
            {
                samples.Add(new
                {
                    time = step * 0.05d, phase = phase.ToString(), engine.CompletedPasses,
                    owner = engine.CurrentBallOwnerId.ToString(), engine.BallPosition.X,
                    engine.BallPosition.Y, action = engine.LastActionDecision?.Selected.ActionType.ToString()
                });
                previous = phase;
            }
        }
        File.WriteAllText(Path.Combine(output, $"{scenario}.json"), JsonSerializer.Serialize(new
        {
            samples, transitions = engine.TeamPhaseMetrics.TransitionCounts,
            shots = engine.ShotRecords
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        simulation.Dispose();
    }
}
