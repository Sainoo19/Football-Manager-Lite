using System;
using System.Collections.Generic;
using Godot;

public static class M1ActionSelectionTests
{
    public static void Run()
    {
        VerifyTwoVersusOneSelectsAdvantagePass();
        VerifySpaceSelectsCarryOverPoorBackwardPass();
        VerifyStrikerFacingGoalRejectsBadBackPass();
        VerifyDefenderUnderPressureSelectsSafeRelief();
        VerifyGoalkeeperWithoutOptionCanHold();
        VerifyDeterminismAndCandidateOrderIndependence();
        VerifyCommitmentSurvivesNormalPlayerMovement();
        VerifyClearShotIsSelectedWithoutWaitingForADuel();
        VerifyProlongedProtectionUsesAnAvailableOutlet();
        VerifyPassCreatesAUsableShootingChance();
        VerifyBlockedCarryDoesNotBecomeAnIndefiniteHold();
        GD.Print("PASS: M1 unified action selection chấm mọi candidate trên cùng pipeline deterministic.");
    }

    private static void VerifyTwoVersusOneSelectsAdvantagePass()
    {
        FootballPassOption advantagePass = Pass(
            "free_runner",
            forwardGainMeters: 14f,
            distanceMeters: 20f,
            laneRisk: 0.16f,
            receiverSpaceMeters: 8f,
            FootballActionType.ThroughBall);
        FootballActionDecision decision = Decide(CreateContext(
            role: "CM",
            isUnderPressure: true,
            pressureDistanceMeters: 1.4f,
            forwardSpaceMeters: 2f,
            passOptions: new[] { advantagePass }));
        Check(
            decision.Selected.ActionType == FootballActionType.ThroughBall &&
            decision.Selected.TargetPlayerId == "free_runner",
            "2v1 phải chọn đường chuyền tạo đối mặt thay vì carry vào hậu vệ.");
    }

    private static void VerifySpaceSelectsCarryOverPoorBackwardPass()
    {
        FootballPassOption poorBackwardPass = Pass(
            "marked_defender",
            forwardGainMeters: -18f,
            distanceMeters: 32f,
            laneRisk: 0.62f,
            receiverSpaceMeters: 2.2f,
            FootballActionType.GroundPass);
        FootballActionDecision decision = Decide(CreateContext(
            role: "LW",
            forwardSpaceMeters: 11f,
            dribbling: 88,
            passOptions: new[] { poorBackwardPass }));
        Check(
            decision.Selected.ActionType == FootballActionType.Carry,
            "1v1 có khoảng trống phải carry thay vì chuyền lùi xa cho người bị kèm.");
    }

    private static void VerifyStrikerFacingGoalRejectsBadBackPass()
    {
        FootballPassOption backPass = Pass(
            "centre_back",
            forwardGainMeters: -28f,
            distanceMeters: 39f,
            laneRisk: 0.44f,
            receiverSpaceMeters: 4f,
            FootballActionType.GroundPass);
        FootballActionDecision decision = Decide(CreateContext(
            role: "ST",
            attackProgress: 0.84f,
            shotValue: 0.56f,
            forwardSpaceMeters: 5f,
            finishing: 86,
            isDirectAttack: true,
            passOptions: new[] { backPass }));
        Check(
            decision.Selected.ActionType is FootballActionType.Shot or FootballActionType.Carry,
            "Tiền đạo đối mặt phải dứt điểm hoặc dẫn tiếp, không chuyền về hậu vệ.");
    }

    private static void VerifyDefenderUnderPressureSelectsSafeRelief()
    {
        FootballPassOption safePass = Pass(
            "goalkeeper",
            forwardGainMeters: -7f,
            distanceMeters: 15f,
            laneRisk: 0.12f,
            receiverSpaceMeters: 9f,
            FootballActionType.GroundPass);
        FootballActionDecision decision = Decide(CreateContext(
            role: "CB",
            attackProgress: 0.12f,
            isUnderPressure: true,
            pressureDistanceMeters: 1.1f,
            defensiveDanger: 0.82f,
            passOptions: new[] { safePass }));
        Check(
            decision.Selected.ActionType is FootballActionType.GroundPass or FootballActionType.Clearance,
            "Hậu vệ quay về khung thành dưới pressure phải chuyền an toàn hoặc phá bóng.");
    }

    private static void VerifyGoalkeeperWithoutOptionCanHold()
    {
        FootballActionDecision decision = Decide(CreateContext(
            role: "GK",
            passOptions: Array.Empty<FootballPassOption>()));
        Check(
            decision.Selected.ActionType == FootballActionType.Hold,
            "Không có candidate hợp lệ phải rơi về hold, không deadlock hoặc action giả.");
    }

    private static void VerifyDeterminismAndCandidateOrderIndependence()
    {
        FootballPassOption first = Pass("left", 8f, 18f, 0.24f, 7f, FootballActionType.GroundPass);
        FootballPassOption second = Pass("right", 10f, 20f, 0.28f, 8f, FootballActionType.GroundPass);
        FootballActionContext ordered = CreateContext(passOptions: new[] { first, second });
        FootballActionContext reversed = CreateContext(passOptions: new[] { second, first });
        FootballActionDecision firstDecision = Decide(ordered);
        FootballActionDecision repeatedDecision = Decide(ordered);
        FootballActionDecision reversedDecision = Decide(reversed);
        Check(
            firstDecision.Selected.StableKey == repeatedDecision.Selected.StableKey &&
            firstDecision.Selected.StableKey == reversedDecision.Selected.StableKey &&
            Math.Abs(firstDecision.Selected.Score.Total - repeatedDecision.Selected.Score.Total) < 0.000001f,
            "Cùng input/seed và thứ tự candidate khác nhau phải cho cùng score lẫn quyết định.");
    }

    private static void VerifyCommitmentSurvivesNormalPlayerMovement()
    {
        FootballActionCoordinator coordinator = new(
            FootballActionSelectionConfiguration.CreateM1Defaults());
        FootballActionDecision first = coordinator.Decide(CreateContext(
            isUnderPressure: true,
            pressureDistanceMeters: 1.3f,
            forwardSpaceMeters: 2f,
            actorPosition: new Vector2(0.42f, 0.48f)));
        FootballActionDecision second = coordinator.Decide(CreateContext(
            isUnderPressure: true,
            pressureDistanceMeters: 1.3f,
            forwardSpaceMeters: 2f,
            actorPosition: new Vector2(0.425f, 0.482f)));

        Check(
            first.Selected.ActionType == FootballActionType.ProtectBall &&
            second.Selected.ActionType == FootballActionType.ProtectBall &&
            second.UsedCommitment &&
            !second.CancelledCommitment,
            "Cầu thủ dịch chuyển bình thường không được làm mất commitment của cùng một ý định bóng đá.");
    }

    private static void VerifyClearShotIsSelectedWithoutWaitingForADuel()
    {
        FootballActionDecision decision = Decide(CreateContext(
            role: "ST", attackProgress: 0.86f, shotValue: 0.45f,
            forwardSpaceMeters: 3f, finishing: 85, isDirectAttack: true));
        Check(decision.Selected.ActionType == FootballActionType.Shot,
            "Một cơ hội sút rõ ràng phải được chọn trực tiếp, không chờ đủ số nhịp tranh chấp.");
    }

    private static void VerifyProlongedProtectionUsesAnAvailableOutlet()
    {
        FootballPassOption outlet = Pass("outlet", 0f, 8f, 0.42f, 3.5f, FootballActionType.GroundPass);
        FootballActionDecision decision = Decide(CreateContext(
            isUnderPressure: true, pressureDistanceMeters: 1.3f,
            forwardSpaceMeters: 1f, passOptions: new[] { outlet }, ownerHeldSeconds: 6f));
        Check(decision.Selected.ActionType == FootballActionType.GroundPass,
            "Che bóng lâu dưới áp lực phải nhường chỗ cho phương án chuyền khả thi.");
    }

    private static FootballActionDecision Decide(FootballActionContext context)
    {
        return new FootballActionCoordinator(
            FootballActionSelectionConfiguration.CreateM1Defaults()).Decide(context);
    }

    private static void VerifyPassCreatesAUsableShootingChance()
    {
        PassSelection central = new("central_receiver", 0f, 12f, 18f, 0.15f, 7f);
        PassSelection wide = new("wide_receiver", 0f, 12f, 18f, 0.15f, 7f);
        FootballActionDecision decision = Decide(CreateContext(
            isUnderPressure: true, pressureDistanceMeters: 1.5f, forwardSpaceMeters: 1f,
            ownerHeldSeconds: 5f,
            passOptions: new[]
            {
                new FootballPassOption(central, new Vector2(0.88f, 0.5f), FootballActionType.GroundPass),
                new FootballPassOption(wide, new Vector2(0.88f, 0.9f), FootballActionType.GroundPass)
            }));
        Check(decision.Selected.ActionType == FootballActionType.GroundPass &&
              decision.Selected.TargetPlayerId == "central_receiver",
            "Khi độ an toàn tương đương, đường chuyền cho người trống gần cầu môn phải tạo giá trị hơn đường ra xa góc sút.");
    }

    private static void VerifyBlockedCarryDoesNotBecomeAnIndefiniteHold()
    {
        FootballActionDecision decision = Decide(CreateContext(
            role: "LW", forwardSpaceMeters: 0f, ownerHeldSeconds: 20f));
        Check(decision.Selected.ActionType != FootballActionType.Hold,
            "Không còn khoảng tiến lên vẫn phải tìm hành động chơi bóng thay vì giữ bóng vô hạn.");
    }

    private static FootballActionContext CreateContext(
        string role = "CM",
        float attackProgress = 0.5f,
        bool isUnderPressure = false,
        float pressureDistanceMeters = 7f,
        bool isDirectAttack = false,
        float forwardSpaceMeters = 7f,
        float defensiveDanger = 0f,
        float shotValue = 0.02f,
        int dribbling = 72,
        int finishing = 68,
        Vector2? actorPosition = null,
        IReadOnlyList<FootballPassOption>? passOptions = null,
        float ownerHeldSeconds = 0.8f)
    {
        return new FootballActionContext(
            "actor",
            "team",
            role,
            actorPosition ?? new Vector2(0.58f, 0.5f),
            new Vector2(0.99f, 0.5f),
            LiveTeamPhase.InPossession,
            attackProgress,
            isUnderPressure,
            pressureDistanceMeters,
            isDirectAttack,
            false,
            ownerHeldSeconds,
            3f,
            1,
            75,
            76,
            72,
            dribbling,
            finishing,
            forwardSpaceMeters,
            defensiveDanger,
            shotValue,
            passOptions ?? Array.Empty<FootballPassOption>(),
            default,
            default,
            null,
            new StringName(),
            20260801u,
            17);
    }

    private static FootballPassOption Pass(
        StringName receiverId,
        float forwardGainMeters,
        float distanceMeters,
        float laneRisk,
        float receiverSpaceMeters,
        FootballActionType actionType)
    {
        return new FootballPassOption(
            new PassSelection(
                receiverId,
                0f,
                forwardGainMeters,
                distanceMeters,
                laneRisk,
                receiverSpaceMeters),
            new Vector2(0.7f, 0.5f),
            actionType);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
