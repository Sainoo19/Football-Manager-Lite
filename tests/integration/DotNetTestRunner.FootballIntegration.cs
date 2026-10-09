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
            if (OS.GetCmdlineUserArgs().Contains("--suite=core-behaviour"))
            {
                CoreBehaviourTests.Run();
                Phase1RulesTests.Run();
                GroundDuelTests.Run();
                M2PhaseIntegrationTests.Run();
                OffBallRoleAllocatorTests.Run();
                M3OffBallIntegrationTests.Run();
                AerialBallScenarioIntegrationTests.Run();
                GetTree().Quit(0);
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--suite=football-fundamentals"))
            {
                M1ActionSelectionTests.Run();
                FootballFundamentalsRegressionTests.Run();
                MatchRefinementTests.Run();
                CoreBehaviourTests.Run();
                MatchFlowRegressionTests.Run();
                FootballFundamentalsIntegrationTests.Run();
                LiveMatchEngineIntegrationTests.RunGoalkeeperConfrontation();
                GroundDuelScenarioIntegrationTests.Run();
                AerialBallScenarioIntegrationTests.Run();
                GD.Print("PASS: football fundamentals focused regression suite.");
                GetTree().Quit(0);
                return;
            }
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
            FootballFundamentalsRegressionTests.Run();
            MatchRefinementTests.Run();
            CoreBehaviourTests.Run();
            Phase1RulesTests.Run();
            MatchFlowRegressionTests.Run();
            FootballFundamentalsIntegrationTests.Run();
            M1ProductionPipelineIntegrationTests.Run();
            TeamPhaseCoordinatorTests.Run();
            M2PhaseIntegrationTests.Run();
            OffBallRoleAllocatorTests.Run();
            M3OffBallIntegrationTests.Run();
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
