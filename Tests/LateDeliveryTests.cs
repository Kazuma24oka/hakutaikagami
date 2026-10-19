using System.Text.Json.Nodes;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class LateDeliveryTests
{
    private static TaxSimulation Create() => TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json"))));
    private static void Advance(TaxSimulation sim, int count) { for (int i = 0; i < count; i++) sim.AdvanceOneTick(); }
    private static TaxDelivery Latest(TaxSimulation sim, DeliveryKind kind) => sim.Deliveries.Where(d => d.Kind == kind).OrderBy(d => d.Attempt).Last();
    private static TaxSimulation Lost(DeliveryKind kind)
    {
        var sim = Create(); sim.Submit("tax-001", "character-001", "castle-001", 100);
        if (kind == DeliveryKind.Report) Advance(sim, 3);
        sim.FailNextDelivery("tax-001", kind); sim.AdvanceOneTick(); return sim;
    }
    private static string Sender(DeliveryKind kind) => kind == DeliveryKind.Order ? "character-001" : "character-002";
    private static void Notify(TaxSimulation sim, DeliveryKind kind)
    {
        sim.DispatchFailureReport(Latest(sim, kind).Id, "character-003"); sim.AdvanceOneTick();
    }

    [Theory] [InlineData(DeliveryKind.Order)] [InlineData(DeliveryKind.Report)]
    public void FailureReportMustTravelBeforeLossIsKnownAndRetryAllowed(DeliveryKind kind)
    {
        var sim = Lost(kind); var original = Latest(sim, kind);
        sim.DispatchFailureReport(original.Id, "character-003", 3);
        Assert.False(sim.FailureKnown(original.Id)); Assert.Equal(1, sim.PendingCount);
        Assert.Throws<InvalidOperationException>(() => sim.RetryDelivery(original.RequestId, kind, Sender(kind)));
        Advance(sim, 2); Assert.False(sim.FailureKnown(original.Id));
        sim.AdvanceOneTick(); Assert.True(sim.FailureKnown(original.Id));
        sim.RetryDelivery(original.RequestId, kind, Sender(kind)); Advance(sim, 5); Assert.Equal(160, sim.TreasuryRice);
    }
    [Fact] public void LostFailureReportDoesNotInformSenderOrResendAnything()
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003");
        sim.FailFailureReport(sim.FailureReports.Single().Id); Advance(sim, 5);
        Assert.False(sim.FailureKnown("tax-001/letter")); Assert.Single(sim.Deliveries); Assert.Equal(100, sim.TreasuryRice);
        Assert.True(sim.FailureReports.Single().Lost); Assert.Equal(0, sim.PendingCount);
        sim.DispatchFailureReport("tax-001/letter", "character-003"); sim.AdvanceOneTick();
        Assert.True(sim.FailureKnown("tax-001/letter"));
    }
    [Fact] public void DuplicateOutOfOrderFailureReportsRegisterKnowledgeOnlyOnce()
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003", 3);
        sim.DispatchFailureReport("tax-001/letter", "character-003", 1); sim.AdvanceOneTick();
        var learned = JsonNode.Parse(sim.SaveJson())!["FailureNotices"]!.ToJsonString(); Advance(sim, 2);
        Assert.Equal(learned, JsonNode.Parse(sim.SaveJson())!["FailureNotices"]!.ToJsonString());
        Assert.Equal(2, sim.FailureReports.Count(r => r.Delivered)); Assert.Single(sim.Deliveries);
    }
    [Fact] public void PausedFailureReportSurvivesLoadWithoutPrematureRecognition()
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003"); sim.SetPaused(true);
        sim = TaxSimulation.LoadJson(sim.SaveJson()); var before = sim.SaveJson(); Advance(sim, 3); Assert.Equal(before, sim.SaveJson());
        Assert.False(sim.FailureKnown("tax-001/letter")); sim.SetPaused(false); sim.AdvanceOneTick(); Assert.True(sim.FailureKnown("tax-001/letter"));
    }
    [Fact] public void FailureReportArrivalExceptionDoesNotPublishRecognitionOrAppliedMarker()
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003"); var before = sim.SaveJson();
        Assert.Throws<IOException>(() => sim.AdvanceOneTick(() => throw new IOException("injected"))); Assert.Equal(before, sim.SaveJson());
        sim.AdvanceOneTick(); Assert.True(sim.FailureKnown("tax-001/letter")); Assert.Single(sim.FailureReports, r => r.Delivered);
    }
    [Fact] public void FailureReportCopyLimitIsBoundedAndPreservedAfterLoad()
    {
        var sim = Lost(DeliveryKind.Order);
        for (int i = 0; i < TaxSimulation.MaxDeliveryAttempts; i++) sim.DispatchFailureReport("tax-001/letter", "character-003", 3);
        sim = TaxSimulation.LoadJson(sim.SaveJson()); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => sim.DispatchFailureReport("tax-001/letter", "character-003")); Assert.Equal(before, sim.SaveJson());
        Advance(sim, 3); Assert.Equal(3, sim.FailureReports.Count(r => r.Delivered));
        Assert.Single(JsonNode.Parse(sim.SaveJson())!["FailureNotices"]!.AsArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FailureReportSaveBoundariesMatchContinuousExecution(int boundary)
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003", 3); Advance(sim, boundary);
        var loaded = TaxSimulation.LoadJson(sim.SaveJson()); Advance(sim, 4); Advance(loaded, 4);
        Assert.Equal(sim.SaveJson(), loaded.SaveJson()); Assert.True(loaded.FailureKnown("tax-001/letter"));
    }
    [Theory] [InlineData(DeliveryKind.Order, 0)] [InlineData(DeliveryKind.Order, 1)] [InlineData(DeliveryKind.Order, 4)]
    [InlineData(DeliveryKind.Report, 0)] [InlineData(DeliveryKind.Report, 1)] [InlineData(DeliveryKind.Report, 4)]
    public void OriginalAndRetryArrivingBeforeSameTickOrAfterEachOtherHaveOneLogicalEffect(DeliveryKind kind, int lateDelay)
    {
        var sim = Lost(kind); var original = Latest(sim, kind); Notify(sim, kind);
        // delay=0 means the original wins; send a later failure report before retry to stage the race.
        if (lateDelay == 0)
        {
            sim.ScheduleLateArrival(original.Id, 1); sim.RetryDelivery(original.RequestId, kind, Sender(kind));
            // Both are due together; delay the retried copy via a loss and eventual late arrival.
            sim.FailNextDelivery(original.RequestId, kind); sim.AdvanceOneTick();
            sim.ScheduleLateArrival(Latest(sim, kind).Id, 2);
        }
        else
        {
            sim.ScheduleLateArrival(original.Id, lateDelay);
            sim.RetryDelivery(original.RequestId, kind, Sender(kind));
        }
        Advance(sim, 8);
        Assert.Equal(2, sim.Deliveries.Count(d => d.Kind == kind && d.Delivered));
        Assert.Single(sim.KnowledgeFor("character-002")); Assert.Single(sim.KnowledgeFor("character-001"));
        Assert.Equal(60, sim.Orders.Single().Collected); Assert.Equal(160, sim.TreasuryRice); Assert.Equal(0, sim.SourceRice);
        Assert.Equal(1, JsonNode.Parse(sim.SaveJson())!["Applied"]!.AsArray().Count(n => n!.GetValue<string>() == "tax-001/event/Decide"));
        Assert.Equal(1, JsonNode.Parse(sim.SaveJson())!["Applied"]!.AsArray().Count(n => n!.GetValue<string>() == "tax-001/event/Collect"));
    }
    [Theory] [InlineData(DeliveryKind.Order, 0)] [InlineData(DeliveryKind.Order, 1)] [InlineData(DeliveryKind.Order, 2)]
    [InlineData(DeliveryKind.Order, 3)] [InlineData(DeliveryKind.Order, 4)] [InlineData(DeliveryKind.Order, 5)]
    [InlineData(DeliveryKind.Report, 0)] [InlineData(DeliveryKind.Report, 1)] [InlineData(DeliveryKind.Report, 2)]
    [InlineData(DeliveryKind.Report, 3)] [InlineData(DeliveryKind.Report, 4)] [InlineData(DeliveryKind.Report, 5)]
    public void DuplicateArrivalSaveAtEveryBoundaryPreservesDeduplication(DeliveryKind kind, int boundary)
    {
        var sim = Lost(kind); var original = Latest(sim, kind); Notify(sim, kind);
        sim.ScheduleLateArrival(original.Id, 4); sim.RetryDelivery(original.RequestId, kind, Sender(kind)); Advance(sim, boundary);
        var loaded = TaxSimulation.LoadJson(sim.SaveJson()); Advance(sim, 8); Advance(loaded, 8);
        Assert.Equal(sim.SaveJson(), loaded.SaveJson()); Assert.Equal(160, loaded.TreasuryRice);
        Assert.Equal(2, loaded.Deliveries.Count(d => d.Kind == kind && d.Delivered));
        Assert.Single(loaded.KnowledgeFor("character-002")); Assert.Single(loaded.KnowledgeFor("character-001"));
    }
    [Fact] public void OriginalArrivalPreventsFurtherRetryEvenIfLossNoticeArrivesLater()
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003", 3);
        sim.ScheduleLateArrival("tax-001/letter", 1); Advance(sim, 3);
        var before = sim.SaveJson(); Assert.True(sim.FailureKnown("tax-001/letter"));
        Assert.Throws<InvalidOperationException>(() => sim.RetryDelivery("tax-001", DeliveryKind.Order, "character-001"));
        Assert.Equal(before, sim.SaveJson()); Advance(sim, 4); Assert.Equal(160, sim.TreasuryRice);
    }
    [Fact] public void DuplicateArrivalsDoNotConsumeAnAdditionalNpcRandomDraw()
    {
        var sim = Lost(DeliveryKind.Order); Notify(sim, DeliveryKind.Order);
        sim.ScheduleLateArrival("tax-001/letter", 6); sim.RetryDelivery("tax-001", DeliveryKind.Order, "character-001"); Advance(sim, 3);
        ulong state = sim.RestoreWorld().Random.State; Advance(sim, 6); Assert.Equal(state, sim.RestoreWorld().Random.State);
    }
    [Fact] public void FailureOnDuplicateArrivalRollsBackThenRetriesOnlyPhysicalArrival()
    {
        var sim = Lost(DeliveryKind.Order); Notify(sim, DeliveryKind.Order); sim.ScheduleLateArrival("tax-001/letter", 4);
        sim.RetryDelivery("tax-001", DeliveryKind.Order, "character-001"); Advance(sim, 3); var before = sim.SaveJson();
        Assert.Throws<IOException>(() => sim.AdvanceOneTick(() => throw new IOException("injected"))); Assert.Equal(before, sim.SaveJson());
        sim.AdvanceOneTick(); Assert.Equal(160, sim.TreasuryRice); Assert.Single(sim.KnowledgeFor("character-002"));
    }
    [Fact] public void CancellationCancelsQueuedLateOriginalAndKeepsResourcesIntact()
    {
        var sim = Lost(DeliveryKind.Order); Notify(sim, DeliveryKind.Order); sim.ScheduleLateArrival("tax-001/letter", 3);
        sim.RetryDelivery("tax-001", DeliveryKind.Order, "character-001"); sim.Cancel("tax-001"); Advance(sim, 5);
        Assert.Equal(TaxPhase.Cancelled, sim.Orders.Single().Phase); Assert.Equal(100, sim.TreasuryRice);
        Assert.Empty(sim.KnowledgeFor("character-002"));
    }
    [Theory] [InlineData("tick-0")] [InlineData("tick-1")] [InlineData("tick-2")] [InlineData("tick-3")]
    [InlineData("tick-4")] [InlineData("lost")] [InlineData("known")] [InlineData("retry")]
    public void ActualV2SavesMigrateWithoutFabricatingFailureReportTransport(string suffix)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tax-v2-" + suffix + ".json"));
        var sim = TaxSimulation.LoadJson(text); Assert.Empty(sim.FailureReports);
        Assert.Equal(5, JsonNode.Parse(sim.SaveJson())!["Version"]!.GetValue<int>());
        Assert.Equal(2, JsonNode.Parse(sim.SaveJson())!["MigrationOrigin"]!.GetValue<int>());
        if (suffix == "lost") { sim.DispatchFailureReport("tax-001/letter", "character-003"); sim.AdvanceOneTick(); }
        if (suffix is "lost" or "known") sim.RetryDelivery("tax-001", DeliveryKind.Order, "character-001");
        Advance(sim, 6); Assert.Equal(160, sim.TreasuryRice); Assert.True(sim.Orders.Single().Reported);
    }
    [Theory] [InlineData("report")] [InlineData("notice")] [InlineData("late")] [InlineData("version")]
    public void CorruptTransportAndLateStateAreRejected(string corruption)
    {
        var sim = Lost(DeliveryKind.Order); sim.DispatchFailureReport("tax-001/letter", "character-003");
        sim.ScheduleLateArrival("tax-001/letter", 4); var before = sim.SaveJson(); var json = JsonNode.Parse(before)!;
        switch (corruption)
        {
            case "report": json["FailureReports"]![0]!["Delivered"] = true; break;
            case "notice": json["FailureNotices"]!.AsArray().Add(JsonNode.Parse("{\"DeliveryId\":\"tax-001/letter\",\"ObserverId\":\"character-001\",\"LearnedAt\":1}")); break;
            case "late": json["Deliveries"]![0]!["ArrivedLate"] = true; break;
            case "version": json["Version"] = 2; break;
        }
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(json.ToJsonString())); Assert.Equal(before, sim.SaveJson());
    }
}
