using System.Text.Json.Nodes;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class DeadlineCancellationTests
{
    private static TaxSimulation Create() => TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json"))));
    private static void Submit(TaxSimulation s, long? deadline = null) => s.Submit("tax-001", "character-001", "castle-001", 100, deadline);
    private static void Advance(TaxSimulation s, int count) { for (int i = 0; i < count; i++) s.AdvanceOneTick(); }
    private static void Cancel(TaxSimulation s, int delay = 1, long? effect = null, string id = "cancel-001") => s.DispatchCancellation(id, "tax-001", "character-001", delay, effect);

    [Theory] [InlineData(0, 100, TaxPhase.Expired)] [InlineData(1, 100, TaxPhase.Expired)]
    [InlineData(2, 100, TaxPhase.Expired)] [InlineData(3, 160, TaxPhase.Partial)] [InlineData(4, 160, TaxPhase.Partial)]
    public void CompletionDeadlineIsInclusiveAndExpiryReleasesOnlyUncommittedReservations(long deadline, int treasury, TaxPhase phase)
    {
        var s = Create(); Submit(s, deadline); Advance(s, 7);
        Assert.Equal(phase, s.Orders.Single().Phase); Assert.Equal(treasury, s.TreasuryRice);
        Assert.Equal(160 - treasury, s.SourceRice); Assert.Equal(0, s.ReservedRice); Assert.True(s.Orders.Single().Reported);
        if (phase == TaxPhase.Expired) Assert.Equal(deadline + 1, s.Orders[0].StoppedAt);
    }
    [Theory] [InlineData(1, CancellationStatus.Applied, 100)] [InlineData(2, CancellationStatus.Applied, 100)]
    [InlineData(3, CancellationStatus.Applied, 100)] [InlineData(4, CancellationStatus.TooLate, 160)]
    public void EffectiveCancellationWinsBeforeSameTickWorkButCannotUndoCommittedRice(long effective, CancellationStatus status, int treasury)
    {
        var s = Create(); Submit(s); Cancel(s, effect: effective); Advance(s, 5);
        Assert.Equal(status, s.Cancellations.Single().Status); Assert.Equal(effective, s.Cancellations[0].ResolvedAt);
        Assert.Equal(treasury, s.TreasuryRice); Assert.Equal(0, s.ReservedRice);
    }
    [Fact] public void DispatchDoesNotCancelAndReceiptCanPrecedeEffect()
    {
        var s = Create(); Submit(s); Cancel(s, effect: 3);
        Assert.Equal(TaxPhase.Dispatched, s.Orders[0].Phase); s.AdvanceOneTick();
        Assert.Equal(CancellationStatus.AwaitingEffect, s.Cancellations.Single().Status);
        Assert.Equal(TaxPhase.Received, s.Orders[0].Phase); s.AdvanceOneTick(); Assert.Equal(60, s.ReservedRice);
        s.AdvanceOneTick(); Assert.Equal(TaxPhase.Cancelled, s.Orders[0].Phase); Assert.Equal(100, s.TreasuryRice);
    }
    [Fact] public void InclusiveDeadlineAndCancellationAtSameCompletionTickUseExplicitPolicy()
    {
        var s = Create(); Submit(s, 3); Cancel(s, effect: 3); Advance(s, 5);
        Assert.Equal(TaxPhase.Cancelled, s.Orders[0].Phase); Assert.Equal(100, s.TreasuryRice);
        Assert.DoesNotContain("tax-001/deadline", JsonNode.Parse(s.SaveJson())!["Applied"]!.AsArray().Select(n => n!.GetValue<string>()));
    }
    [Fact] public void AlreadyExpiredDeadlineWinsOverCancellationAtNextTick()
    {
        var s = Create(); Submit(s, 2); Cancel(s, effect: 3); Advance(s, 5);
        Assert.Equal(TaxPhase.Expired, s.Orders[0].Phase); Assert.Equal(CancellationStatus.AlreadyStopped, s.Cancellations[0].Status);
        Assert.Equal(100, s.TreasuryRice);
    }
    [Fact] public void LostCancellationDoesNotStopWork()
    {
        var s = Create(); Submit(s); Cancel(s); s.FailCancellation("cancel-001"); Advance(s, 5);
        Assert.Equal(CancellationStatus.Lost, s.Cancellations[0].Status); Assert.False(s.Cancellations[0].Delivered);
        Assert.Equal(160, s.TreasuryRice); Assert.Equal(TaxPhase.Partial, s.Orders[0].Phase);
    }
    [Theory] [InlineData(1, CancellationStatus.Applied, 100)] [InlineData(3, CancellationStatus.TooLate, 160)]
    public void LateCancellationCannotApplyRetroactively(int lateDelay, CancellationStatus status, int treasury)
    {
        var s = Create(); Submit(s); Cancel(s); s.FailCancellation("cancel-001"); s.AdvanceOneTick();
        s.ScheduleLateCancellation("cancel-001", lateDelay); Advance(s, 6);
        var c = s.Cancellations.Single(); Assert.Equal(status, c.Status); Assert.Equal(1 + lateDelay, c.ReceivedAt);
        Assert.Equal(c.ReceivedAt, c.ResolvedAt); Assert.Equal(1, c.EffectiveAt); Assert.Equal(treasury, s.TreasuryRice);
    }
    [Fact] public void DispatchAndEffectiveAuthorityAreBothRequired()
    {
        var s = Create(); Submit(s); var before = s.SaveJson();
        Assert.Throws<UnauthorizedAccessException>(() => s.DispatchCancellation("bad", "tax-001", "character-003")); Assert.Equal(before, s.SaveJson());
        Cancel(s, effect: 2); s.AdvanceOneTick(); s.Revoke("character-001", "cancel-tax"); Advance(s, 4);
        Assert.Equal(CancellationStatus.Unauthorized, s.Cancellations.Single().Status); Assert.Equal(160, s.TreasuryRice);
    }
    [Fact] public void DeadlinePayloadCannotBeChangedByDuplicateRequestId()
    {
        var s = Create(); Submit(s, 3); Submit(s, 3); var before = s.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Submit(s, 4)); Assert.Equal(before, s.SaveJson());
    }
    [Fact] public void CancellationIdIsIdempotentAcrossLoadAndDoesNotIssueMultipleReports()
    {
        var s = Create(); Submit(s); Cancel(s, effect: 3); s = TaxSimulation.LoadJson(s.SaveJson());
        var before = s.SaveJson(); Cancel(s, effect: 3); Assert.Equal(before, s.SaveJson()); Advance(s, 5);
        Assert.Single(s.Cancellations); Assert.Single(s.Deliveries, d => d.Kind == DeliveryKind.Report);
        Assert.Single(s.KnowledgeFor("character-001"));
    }
    [Fact] public void DuplicateCancellationPayloadIsRejectedWithoutMutation()
    {
        var s = Create(); Submit(s); Cancel(s, effect: 3); var before = s.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Cancel(s, effect: 4)); Assert.Equal(before, s.SaveJson());
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void MultipleEffectiveCancellationsDoNotDependOnRegistrationOrder(int reverse)
    {
        var s = Create(); Submit(s);
        foreach (var id in reverse == 0 ? new[] { "a", "z" } : new[] { "z", "a" }) Cancel(s, effect: 3, id: id);
        Advance(s, 5); Assert.All(s.Cancellations, c => Assert.Equal(CancellationStatus.Applied, c.Status));
        Assert.Equal(100, s.TreasuryRice);
        Assert.Single(s.Deliveries, d => d.Kind == DeliveryKind.Report);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ExpiredOrCancelledOrderCanPhysicallyArriveLateWithoutRestarting(bool cancellation)
    {
        var s = Create(); Submit(s, cancellation ? null : 2);
        s.FailNextDelivery("tax-001", DeliveryKind.Order); s.AdvanceOneTick();
        s.DispatchFailureReport("tax-001/letter", "character-003"); s.AdvanceOneTick();
        s.ScheduleLateArrival("tax-001/letter", 4); s.RetryDelivery("tax-001", DeliveryKind.Order, "character-001");
        if (cancellation) Cancel(s); // Effective at 3, same tick as retried arrival.
        Advance(s, 7);
        Assert.Equal(cancellation ? TaxPhase.Cancelled : TaxPhase.Expired, s.Orders[0].Phase);
        Assert.Equal(2, s.Deliveries.Count(d => d.Kind == DeliveryKind.Order && d.Delivered));
        Assert.Single(s.KnowledgeFor("character-002")); Assert.Null(s.Orders[0].DecisionRoll);
        Assert.Equal(100, s.TreasuryRice); Assert.Equal(0, s.ReservedRice);
    }
    [Fact] public void FailedCommitAtCancellationCollectConflictPublishesNeitherEffect()
    {
        var s = Create(); Submit(s); Cancel(s, effect: 3); Advance(s, 2); var before = s.SaveJson();
        Assert.Equal(60, s.ReservedRice);
        Assert.Throws<IOException>(() => s.AdvanceOneTick(() => throw new IOException("injected"))); Assert.Equal(before, s.SaveJson());
        s.AdvanceOneTick(); Assert.Equal(100, s.TreasuryRice); Assert.Equal(0, s.ReservedRice); Assert.Equal(CancellationStatus.Applied, s.Cancellations[0].Status);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void CancellationEverySaveBoundaryMatchesContinuousExecution(int boundary)
    {
        var s = Create(); Submit(s); Cancel(s, effect: 3); Advance(s, boundary); var loaded = TaxSimulation.LoadJson(s.SaveJson());
        Advance(s, 6); Advance(loaded, 6); Assert.Equal(s.SaveJson(), loaded.SaveJson()); Assert.Equal(100, loaded.TreasuryRice);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ExpiryEverySaveBoundaryMatchesContinuousExecution(int boundary)
    {
        var s = Create(); Submit(s, 2); Advance(s, boundary); var loaded = TaxSimulation.LoadJson(s.SaveJson());
        Advance(s, 5); Advance(loaded, 5); Assert.Equal(s.SaveJson(), loaded.SaveJson()); Assert.Equal(100, loaded.TreasuryRice);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void LostLateCancellationSaveBoundariesMatchContinuousExecution(int boundary)
    {
        var s = Create(); Submit(s); Cancel(s); s.FailCancellation("cancel-001"); s.AdvanceOneTick(); s.ScheduleLateCancellation("cancel-001", 3);
        Advance(s, boundary); var loaded = TaxSimulation.LoadJson(s.SaveJson()); Advance(s, 6); Advance(loaded, 6);
        Assert.Equal(s.SaveJson(), loaded.SaveJson()); Assert.Equal(160, loaded.TreasuryRice); Assert.Equal(CancellationStatus.TooLate, loaded.Cancellations[0].Status);
    }
    [Fact] public void PausePreservesDeadlineAndCancellationWaitingEffectAcrossLoad()
    {
        var s = Create(); Submit(s, 3); Cancel(s, effect: 3); s.AdvanceOneTick(); s.SetPaused(true);
        s = TaxSimulation.LoadJson(s.SaveJson()); var before = s.SaveJson(); Advance(s, 10); Assert.Equal(before, s.SaveJson());
        s.SetPaused(false); Advance(s, 3); Assert.Equal(TaxPhase.Cancelled, s.Orders[0].Phase);
    }
    [Theory] [InlineData("tick-0")] [InlineData("tick-1")] [InlineData("tick-2")] [InlineData("tick-3")]
    [InlineData("tick-4")] [InlineData("failure-transit")] [InlineData("late-transit")] [InlineData("admin-cancelled")]
    public void ActualV3SavesMigrateWithoutInventingDeadlineOrCancellation(string suffix)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tax-v3-" + suffix + ".json"));
        var s = TaxSimulation.LoadJson(text); Assert.Empty(s.Cancellations); Assert.All(s.Orders, o => Assert.Null(o.ExecuteBy));
        Assert.Equal(5, JsonNode.Parse(s.SaveJson())!["Version"]!.GetValue<int>());
        if (suffix == "failure-transit") { s.AdvanceOneTick(); s.RetryDelivery("tax-001", DeliveryKind.Order, "character-001"); }
        Advance(s, 8); Assert.Equal(suffix == "admin-cancelled" ? 100 : 160, s.TreasuryRice);
    }
    [Theory] [InlineData("effect")] [InlineData("receipt")] [InlineData("resolved")] [InlineData("deadline")] [InlineData("version")]
    public void CorruptedDeadlineOrCancellationStateIsRejected(string corruption)
    {
        var s = Create(); Submit(s, 3); Cancel(s, effect: 3); s.AdvanceOneTick(); var before = s.SaveJson(); var json = JsonNode.Parse(before)!;
        switch (corruption)
        {
            case "effect": json["Cancellations"]![0]!["EffectiveAt"] = 0; break;
            case "receipt": json["Cancellations"]![0]!["ReceivedAt"] = 0; break;
            case "resolved": json["Cancellations"]![0]!["Status"] = "Applied"; break;
            case "deadline": json["Orders"]![0]!["ExecuteBy"] = 0; break;
            case "version": json["Version"] = 3; break;
        }
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(json.ToJsonString())); Assert.Equal(before, s.SaveJson());
    }
}
