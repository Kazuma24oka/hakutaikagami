using System.Text.Json.Nodes;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class DeliveryRecoveryTests
{
    private static TaxSimulation Create() => TaxSimulation.Create(PrototypeScenario.Load(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json"))));
    private static void Submit(TaxSimulation sim) => sim.Submit("tax-001", "character-001", "castle-001", 100);
    private static void Advance(TaxSimulation sim, int count) { for (int i = 0; i < count; i++) sim.AdvanceOneTick(); }
    private static TaxDelivery Latest(TaxSimulation sim, DeliveryKind kind) => sim.Deliveries.Where(d => d.Kind == kind).OrderBy(d => d.Attempt).Last();
    private static string Sender(DeliveryKind kind) => kind == DeliveryKind.Order ? "character-001" : "character-002";
    private static void Notice(TaxSimulation sim, DeliveryKind kind)
    {
        sim.DispatchFailureReport(Latest(sim, kind).Id, "character-003");
        sim.AdvanceOneTick();
    }
    private static void Retry(TaxSimulation sim, DeliveryKind kind) => sim.RetryDelivery("tax-001", kind, Sender(kind));
    private static TaxSimulation LostOrder()
    {
        var sim = Create(); Submit(sim); sim.FailNextDelivery("tax-001", DeliveryKind.Order); sim.AdvanceOneTick(); return sim;
    }
    private static TaxSimulation LostReport()
    {
        var sim = Create(); Submit(sim); Advance(sim, 3); sim.FailNextDelivery("tax-001", DeliveryKind.Report); sim.AdvanceOneTick(); return sim;
    }

    [Fact] public void PhysicalLossDoesNotGrantKnowledgeConsumeRandomOrCreateResources()
    {
        var sim = Create(); Submit(sim); var random = JsonNode.Parse(sim.SaveJson())!["RandomState"]!.GetValue<ulong>();
        sim.FailNextDelivery("tax-001", DeliveryKind.Order); sim.AdvanceOneTick();
        Assert.True(Latest(sim, DeliveryKind.Order).Lost); Assert.False(sim.FailureKnown(Latest(sim, DeliveryKind.Order).Id));
        Assert.Empty(sim.KnowledgeFor("character-002")); Assert.Equal(TaxPhase.Dispatched, sim.Orders.Single().Phase);
        Assert.Equal(random, JsonNode.Parse(sim.SaveJson())!["RandomState"]!.GetValue<ulong>());
        Advance(sim, 10); Assert.Single(sim.Deliveries); Assert.Equal(0, sim.PendingCount);
        Assert.Equal(60, sim.SourceRice); Assert.Equal(100, sim.TreasuryRice); Assert.Equal(0, sim.ReservedRice);
    }
    [Fact] public void RetryBeforeLossRecognitionIsRejectedWithoutMutation()
    {
        var sim = LostOrder(); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Retry(sim, DeliveryKind.Order)); Assert.Equal(before, sim.SaveJson());
    }
    [Fact] public void FailureNoticeRequiresActualLossAndCorrectObserver()
    {
        var sim = Create(); Submit(sim); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => sim.DispatchFailureReport("tax-001/letter", "character-003"));
        Assert.Equal(before, sim.SaveJson()); sim.FailNextDelivery("tax-001", DeliveryKind.Order); sim.AdvanceOneTick(); before = sim.SaveJson();
        Assert.Throws<UnauthorizedAccessException>(() => sim.DispatchFailureReport("tax-001/letter", "unknown")); Assert.Equal(before, sim.SaveJson());
    }
    [Fact] public void FailureNoticeIsIdempotentAndDoesNotInformRecipient()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); var notice = JsonNode.Parse(sim.SaveJson())!["FailureNotices"]!.ToJsonString(); Notice(sim, DeliveryKind.Order);
        Assert.Equal(notice, JsonNode.Parse(sim.SaveJson())!["FailureNotices"]!.ToJsonString()); Assert.Empty(sim.KnowledgeFor("character-002"));
    }
    [Fact] public void OrderRetryKeepsRequestCaseMessageAndSingleLogicalExecution()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); Retry(sim, DeliveryKind.Order);
        Assert.Equal(2, Latest(sim, DeliveryKind.Order).Attempt); Assert.Equal("tax-001/letter", Latest(sim, DeliveryKind.Order).MessageId);
        Advance(sim, 5); Assert.Single(sim.Orders); Assert.Equal("tax-001/case", sim.Orders[0].CaseId);
        Assert.True(sim.Orders[0].Reported); Assert.Equal(60, sim.Orders[0].Collected); Assert.Equal(160, sim.TreasuryRice);
        Assert.Single(sim.KnowledgeFor("character-002")); Assert.Single(sim.KnowledgeFor("character-001"));
        Assert.Equal(6, sim.AppliedCount);
    }
    [Fact] public void DoubleClickRetryCannotScheduleAnotherConcurrentAttempt()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); Retry(sim, DeliveryKind.Order); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Retry(sim, DeliveryKind.Order)); Assert.Equal(before, sim.SaveJson()); Assert.Equal(1, sim.PendingCount);
    }
    [Fact] public void RevokedSenderCannotResendKnownLostOrder()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); sim.Revoke("character-001", "tax-order"); var before = sim.SaveJson();
        Assert.Throws<UnauthorizedAccessException>(() => Retry(sim, DeliveryKind.Order)); Assert.Equal(before, sim.SaveJson());
    }
    [Fact] public void RetriedArrivalStillRechecksExecutorAuthorityBeforeEffects()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); Retry(sim, DeliveryKind.Order); sim.Revoke("character-002", "collect-tax"); Advance(sim, 4);
        Assert.Equal(TaxPhase.Refused, sim.Orders[0].Phase); Assert.Equal(100, sim.TreasuryRice);
    }
    [Fact] public void LostReportDoesNotUndoOrRepeatCollection()
    {
        var sim = LostReport(); Assert.Equal(160, sim.TreasuryRice); Assert.False(sim.Orders[0].Reported);
        Assert.Empty(sim.KnowledgeFor("character-001")); Advance(sim, 5); Assert.Equal(160, sim.TreasuryRice);
        Notice(sim, DeliveryKind.Report); Retry(sim, DeliveryKind.Report); sim.AdvanceOneTick();
        Assert.True(sim.Orders[0].Reported); Assert.Equal(160, sim.TreasuryRice); Assert.Equal(0, sim.SourceRice);
        Assert.Single(sim.KnowledgeFor("character-001")); Assert.Single(sim.KnowledgeFor("character-002"));
    }
    [Fact] public void ReportRetryDoesNotRequireRepeatingTaxAuthorityOrNpcDecision()
    {
        var sim = LostReport(); Notice(sim, DeliveryKind.Report); sim.Revoke("character-001", "tax-order");
        var random = JsonNode.Parse(sim.SaveJson())!["RandomState"]!.GetValue<ulong>();
        Retry(sim, DeliveryKind.Report); sim.AdvanceOneTick(); Assert.True(sim.Orders[0].Reported);
        Assert.Equal(random, JsonNode.Parse(sim.SaveJson())!["RandomState"]!.GetValue<ulong>());
    }
    [Theory] [InlineData(DeliveryKind.Order)] [InlineData(DeliveryKind.Report)]
    public void RetryLimitPersistsAndHasNoAutomaticFallback(DeliveryKind kind)
    {
        var sim = kind == DeliveryKind.Order ? LostOrder() : LostReport();
        for (int attempt = 1; attempt < TaxSimulation.MaxDeliveryAttempts; attempt++)
        {
            Notice(sim, kind); Retry(sim, kind); sim.FailNextDelivery("tax-001", kind); sim.AdvanceOneTick();
            sim = TaxSimulation.LoadJson(sim.SaveJson());
        }
        Notice(sim, kind); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Retry(sim, kind)); Assert.Equal(before, sim.SaveJson());
        Assert.Equal(3, Latest(sim, kind).Attempt); Assert.Equal(0, sim.PendingCount);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void OrderLossRecoveryAtEveryBoundaryMatchesUninterruptedContinuation(int boundary)
    {
        var sim = Create(); Submit(sim); sim.FailNextDelivery("tax-001", DeliveryKind.Order);
        if (boundary >= 1) sim.AdvanceOneTick();
        if (boundary >= 2) Notice(sim, DeliveryKind.Order);
        if (boundary >= 3) Retry(sim, DeliveryKind.Order);
        if (boundary >= 4) Advance(sim, boundary - 3);
        var loaded = TaxSimulation.LoadJson(sim.SaveJson());
        void Finish(TaxSimulation current)
        {
            if (boundary < 1) current.AdvanceOneTick();
            if (boundary < 2) Notice(current, DeliveryKind.Order);
            if (boundary < 3) Retry(current, DeliveryKind.Order);
            Advance(current, 5);
        }
        Finish(sim); Finish(loaded); Assert.Equal(sim.SaveJson(), loaded.SaveJson()); Assert.Equal(160, loaded.TreasuryRice);
        Assert.Single(loaded.KnowledgeFor("character-002"));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ReportLossSaveRestoreNeverReappliesTax(int boundary)
    {
        var sim = Create(); Submit(sim); Advance(sim, 3); sim.FailNextDelivery("tax-001", DeliveryKind.Report);
        if (boundary >= 1) sim.AdvanceOneTick();
        if (boundary >= 2) Notice(sim, DeliveryKind.Report);
        if (boundary >= 3) Retry(sim, DeliveryKind.Report);
        var loaded = TaxSimulation.LoadJson(sim.SaveJson());
        void Finish(TaxSimulation current)
        {
            if (boundary < 1) current.AdvanceOneTick();
            if (boundary < 2) Notice(current, DeliveryKind.Report);
            if (boundary < 3) Retry(current, DeliveryKind.Report);
            Advance(current, 5);
        }
        Finish(sim); Finish(loaded); Assert.Equal(sim.SaveJson(), loaded.SaveJson());
        Assert.Equal(160, loaded.TreasuryRice); Assert.Single(loaded.KnowledgeFor("character-001"));
    }
    [Fact] public void FailedCommitOfDeliveryLossRollsBackAttemptAndClock()
    {
        var sim = Create(); Submit(sim); sim.FailNextDelivery("tax-001", DeliveryKind.Order); var before = sim.SaveJson();
        Assert.Throws<IOException>(() => sim.AdvanceOneTick(() => throw new IOException("injected"))); Assert.Equal(before, sim.SaveJson());
        sim.AdvanceOneTick(); Assert.True(Latest(sim, DeliveryKind.Order).Lost);
    }
    [Fact] public void PausedRetriedDeliveryDoesNotArriveUntilResumedAfterLoad()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); Retry(sim, DeliveryKind.Order); sim.SetPaused(true);
        sim = TaxSimulation.LoadJson(sim.SaveJson()); var before = sim.SaveJson(); Advance(sim, 3); Assert.Equal(before, sim.SaveJson());
        sim.SetPaused(false); sim.AdvanceOneTick(); Assert.Equal(TaxPhase.Received, sim.Orders[0].Phase);
    }
    [Fact] public void CancelLostOrderPreventsRetryAndCollection()
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); sim.Cancel("tax-001"); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => Retry(sim, DeliveryKind.Order)); Assert.Equal(before, sim.SaveJson());
        Advance(sim, 3); Assert.Equal(TaxPhase.Cancelled, sim.Orders[0].Phase); Assert.Equal(100, sim.TreasuryRice);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ActualV1SavesMigrateAndContinueWithoutDuplicatingRice(int tick)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", $"tax-v1-tick-{tick}.json"); var text = File.ReadAllText(path);
        Assert.Equal(1, JsonNode.Parse(text)!["Version"]!.GetValue<int>());
        var sim = TaxSimulation.LoadJson(text); Assert.Equal(4, JsonNode.Parse(sim.SaveJson())!["Version"]!.GetValue<int>());
        Assert.Equal(tick, sim.Tick); Advance(sim, 5); Assert.Equal(160, sim.TreasuryRice); Assert.True(sim.Orders[0].Reported);
        Assert.Equal(text, File.ReadAllText(path));
    }
    [Theory] [InlineData("attempt")] [InlineData("notice")] [InlineData("pending")] [InlineData("delivered")] [InlineData("version")]
    public void CorruptRetryStateIsRejected(string corruption)
    {
        var sim = LostOrder(); Notice(sim, DeliveryKind.Order); Retry(sim, DeliveryKind.Order); var before = sim.SaveJson();
        var json = JsonNode.Parse(before)!;
        switch (corruption)
        {
            case "attempt": json["Deliveries"]![1]!["Attempt"] = 3; break;
            case "notice": json["FailureNotices"]!.AsArray().Clear(); break;
            case "pending": json["Pending"]![0]!["Attempt"] = 1; break;
            case "delivered": json["Deliveries"]![0]!["Delivered"] = true; break;
            case "version": json["Version"] = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(json.ToJsonString())); Assert.Equal(before, sim.SaveJson());
    }
}
