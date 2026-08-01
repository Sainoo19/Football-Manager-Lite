using System;
using Godot;

public static class TeamPhaseCoordinatorTests
{
    private static readonly StringName Home = "home";
    private static readonly StringName Away = "away";

    public static void Run()
    {
        VerifyTurnoverWithSpaceStartsCounterAttack();
        VerifySurroundedTurnoverOrganizesBeforeBuildUp();
        VerifyLostBallStartsCollectiveDefensiveTransition();
        VerifyFinalThirdHysteresisPreventsFlicker();
        VerifyRestartUsesSetPiecePhase();
        VerifyDifferentObservationDeltasReachSamePhase();
        VerifyActionEvaluationUsesTeamPhase();
        GD.Print("PASS: M2 phase coordinator xử lý turnover, hysteresis, set piece và phase ổn định deterministic.");
    }

    private static void VerifyTurnoverWithSpaceStartsCounterAttack()
    {
        TeamPhaseCoordinator coordinator = CreateCoordinator();
        coordinator.Update(Context(Home, false, 1f, 0.55f, 4, 6, 0, 60f, 5f));
        TeamPhaseState winner = coordinator.Update(Context(Away, true, 1f, 0.45f, 3, 7, 1, 58f, 12f));

        Check(winner.Phase == LiveTeamPhase.CounterAttack,
            "Đội đoạt bóng có khoảng trống và đủ người phía trước phải vào CounterAttack.");
        Check(coordinator.PhaseFor(Home) == LiveTeamPhase.TransitionToDefence,
            "Đội vừa mất bóng phải phản ứng bằng TransitionToDefence.");
    }

    private static void VerifySurroundedTurnoverOrganizesBeforeBuildUp()
    {
        TeamPhaseCoordinator coordinator = CreateCoordinator();
        coordinator.Update(Context(Home, false, 1f, 0.58f, 3, 7, 0, 62f, 4f));
        TeamPhaseState won = coordinator.Update(Context(Away, true, 1f, 0.28f, 1, 9, -2, 72f, 2f));
        TeamPhaseState organizing = coordinator.Update(Context(Away, true, 3f, 0.29f, 2, 8, -1, 70f, 3f));
        TeamPhaseState organized = coordinator.Update(Context(Away, true, 6f, 0.30f, 3, 7, 0, 68f, 6f));

        Check(won.Phase == LiveTeamPhase.TransitionToAttack &&
              organizing.Phase == LiveTeamPhase.TransitionToAttack &&
              organized.Phase == LiveTeamPhase.BuildUp,
            "Đoạt bóng khi bị vây phải tổ chức lại trước khi chuyển sang BuildUp.");
    }

    private static void VerifyLostBallStartsCollectiveDefensiveTransition()
    {
        TeamPhaseCoordinator coordinator = CreateCoordinator();
        TeamPhaseState state = coordinator.Update(Context(
            Home, false, 1f, 0.82f, 5, 5, 0, 84f, 6f));
        Check(state.Phase == LiveTeamPhase.TransitionToDefence,
            "Mất bóng gần gôn đối phương không được kéo cả đội về EmergencyDefence ngay lập tức.");
    }

    private static void VerifyFinalThirdHysteresisPreventsFlicker()
    {
        TeamPhaseCoordinator coordinator = CreateCoordinator();
        TeamPhaseState entered = coordinator.Update(Context(Home, true, 3f, 0.72f, 5, 5, 1, 82f, 8f));
        TeamPhaseState retained = coordinator.Update(Context(Home, true, 6f, 0.64f, 5, 5, 0, 74f, 6f));
        TeamPhaseState exited = coordinator.Update(Context(Home, true, 9f, 0.56f, 4, 6, 0, 66f, 5f));

        Check(entered.Phase == LiveTeamPhase.FinalThird &&
              retained.Phase == LiveTeamPhase.FinalThird &&
              exited.Phase == LiveTeamPhase.Progression,
            "FinalThird phải có ngưỡng thoát riêng để bóng lùi nhẹ không gây phase flicker.");
    }

    private static void VerifyRestartUsesSetPiecePhase()
    {
        TeamPhaseCoordinator coordinator = CreateCoordinator();
        TeamPhaseState home = coordinator.Update(Context(Home, true, 1f, 0.50f, 4, 6, 0, 50f, 5f, true));
        TeamPhaseState away = coordinator.Update(Context(Away, false, 1f, 0.50f, 4, 6, 0, 50f, 5f, true));
        Check(home.Phase == LiveTeamPhase.SetPiece && away.Phase == LiveTeamPhase.SetPiece,
            "Cả hai đội phải nhận phase SetPiece trong thời gian chuẩn bị restart.");
    }

    private static void VerifyDifferentObservationDeltasReachSamePhase()
    {
        TeamPhaseCoordinator fine = CreateCoordinator();
        TeamPhaseCoordinator coarse = CreateCoordinator();
        ObserveTrajectory(fine, 0.1f);
        ObserveTrajectory(coarse, 0.5f);
        Check(fine.PhaseFor(Home) == coarse.PhaseFor(Home) &&
              fine.PhaseFor(Away) == coarse.PhaseFor(Away),
            "Cùng trajectory không được đổi phase cuối chỉ vì observation delta khác nhau.");
    }

    private static void VerifyActionEvaluationUsesTeamPhase()
    {
        FootballActionSelectionConfiguration configuration =
            FootballActionSelectionConfiguration.CreateM1Defaults();
        FootballActionEvaluator evaluator = new(configuration);
        FootballActionCandidate progressivePass = new(
            FootballActionType.ThroughBall,
            "actor",
            "runner",
            new Vector2(0.72f, 0.5f),
            12f,
            0.24f,
            0f,
            0.82f,
            0.58f,
            0.28f,
            true,
            "phase_test");
        FootballActionCandidate progressionScore = evaluator.Evaluate(
            ActionContext(LiveTeamPhase.Progression), progressivePass);
        FootballActionCandidate counterScore = evaluator.Evaluate(
            ActionContext(LiveTeamPhase.CounterAttack), progressivePass);

        Check(counterScore.Score.PhaseFit == configuration.CounterAttackProgressionBonus &&
              counterScore.Score.Total > progressionScore.Score.Total,
            "Action evaluator phải dùng phase context để thưởng phương án progressive trong CounterAttack.");
    }

    private static TeamPhaseCoordinator CreateCoordinator()
    {
        TeamPhaseCoordinator coordinator = new(TeamPhaseConfiguration.CreateM2Defaults());
        coordinator.Reset(new[] { Home, Away }, Home);
        return coordinator;
    }

    private static void ObserveTrajectory(TeamPhaseCoordinator coordinator, float delta)
    {
        for (float time = delta; time <= 10.0001f; time += delta)
        {
            float progress = time < 3f ? 0.30f : time < 6f ? 0.50f : 0.72f;
            coordinator.Update(Context(Home, true, time, progress, 4, 6, 0, 70f, 7f));
            coordinator.Update(Context(Away, false, time, 1f - progress, 3, 7, 0, 45f, 4f));
        }
    }

    private static TeamPhaseContext Context(
        StringName teamId,
        bool hasPossession,
        float time,
        float attackProgress,
        int ahead,
        int behind,
        int regionalAdvantage,
        float goalDistance,
        float forwardSpace,
        bool isRestart = false)
    {
        return new TeamPhaseContext(
            teamId,
            hasPossession,
            isRestart,
            false,
            time,
            attackProgress,
            hasPossession ? time : 0f,
            ahead,
            behind,
            regionalAdvantage,
            goalDistance,
            forwardSpace,
            0f,
            42f);
    }

    private static FootballActionContext ActionContext(LiveTeamPhase phase)
    {
        return new FootballActionContext(
            "actor",
            Home,
            "CM",
            new Vector2(0.52f, 0.5f),
            new Vector2(0.99f, 0.5f),
            phase,
            0.52f,
            false,
            8f,
            phase == LiveTeamPhase.CounterAttack,
            false,
            1f,
            4f,
            1,
            76,
            78,
            74,
            72,
            68,
            10f,
            0f,
            0.03f,
            Array.Empty<FootballPassOption>(),
            default,
            default,
            null,
            new StringName(),
            20260802u,
            1);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
