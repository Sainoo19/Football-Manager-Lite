using System;
using System.Linq;
using Godot;
using Godot.Collections;

public partial class DotNetTestRunner : Node
{
    private void RunTests()
    {
        try
        {
            TestSquadLimits();
            TestLiveMatchClock();
            PlayerPositionInterpolatorTests.Run();
            TestFootballFundamentalsRuntimeAndTechnique();
            TestFreeKickRestartTimingAndDistance();
            TestPenaltyAdvantageAndDiscipline();
            TestMatchSimulation();
            TestLiveMatchRules();
            TestPitchScaleAndMovementSpeed();
            TestOffsideRule();
            TestPassTrajectoryAndNearestContest();
            TestDefensiveBlockSpacingAndRollingBall();
            TestShotSelectionAndTraditionalGoalkeeper();
            TestDirectAttackContinuationAndGoalkeeperLooseBallClaim();
            TestFinalThirdAttackDecisions();
            TestGoalKickShapeAndSeededDecisionVariety();
            TestScenarioFactoryAndPitchLauncher();
            TestWideAttackKeepsFootballShape();
            TestPitchPauseAndReset();
            TestPlaybackSpeedDoesNotChangeFootball();
            TestKickoffGoalResetAndHalfTimeSides();
            CollectivePossessionScenarioIntegrationTests.Run();
            GroundDuelTests.Run();
            GroundDuelScenarioIntegrationTests.Run();
            AerialBallTests.Run();
            BalanceBatchTests.Run();
            M0ArchitectureBoundaryTests.Run();
            M1ActionSelectionTests.Run();
            M1ProductionPipelineIntegrationTests.Run();
            TeamPhaseCoordinatorTests.Run();
            M2PhaseIntegrationTests.Run();
            AerialBallScenarioIntegrationTests.Run();
            TestPitchMovement();
            LiveMatchEngineIntegrationTests.Run();
            TestUiIntegration();
            GD.Print("PASS: toàn bộ logic, giao diện, sân 2D và kiểm thử đang chạy bằng C#/.NET.");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FAIL: {exception.Message}\n{exception.StackTrace}");
            GetTree().Quit(1);
        }
    }
}
