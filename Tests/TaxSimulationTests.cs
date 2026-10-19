using System.Text.Json.Nodes;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class TaxSimulationTests
{
    private static TaxSimulation Create(int rice = 60, int acceptChance = 100) => TaxSimulation.Create(
        PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prototype.json"))), rice, acceptChance);
    private static void Submit(TaxSimulation sim, string id = "tax-001", int amount = 100) => sim.Submit(id, "character-001", "castle-001", amount);
    private static void Advance(TaxSimulation sim, int count) { for (int i = 0; i < count; i++) sim.AdvanceOneTick(); }

    [Fact] public void OrderMustArriveBeforeNpcLearnsOrDecides()
    {
        var sim = Create(); Submit(sim);
        Assert.Empty(sim.KnowledgeFor("character-002")); Assert.Equal(0, sim.ReservedRice);
        sim.AdvanceOneTick();
        Assert.Equal(TaxPhase.Received, Assert.Single(sim.Orders).Phase);
        Assert.Equal(100, Assert.Single(sim.KnowledgeFor("character-002")).Amount);
        Assert.Empty(sim.KnowledgeFor("character-001")); Assert.Empty(sim.KnowledgeFor("character-003"));
        Assert.Equal(0, sim.ReservedRice); Assert.Equal(100, sim.TreasuryRice);
    }
    [Theory] [InlineData(60, 100, 60, TaxPhase.Partial)] [InlineData(100, 100, 100, TaxPhase.Completed)]
    [InlineData(100, 40, 40, TaxPhase.Completed)]
    public void CollectionConservesRiceAndReportArrivesLater(int stock, int requested, int collected, TaxPhase phase)
    {
        var sim = Create(stock); Submit(sim, amount: requested); Advance(sim, 2);
        Assert.Equal(collected, sim.ReservedRice); Assert.Equal(stock, sim.SourceRice); Assert.Equal(100, sim.TreasuryRice);
        sim.AdvanceOneTick();
        Assert.Equal(phase, Assert.Single(sim.Orders).Phase); Assert.Equal(collected, sim.Orders[0].Collected);
        Assert.Equal(100 + collected, sim.TreasuryRice); Assert.Equal(stock - collected, sim.SourceRice); Assert.Equal(0, sim.ReservedRice);
        Assert.False(sim.Orders[0].Reported); Assert.Empty(sim.KnowledgeFor("character-001"));
        sim.AdvanceOneTick(); Assert.True(sim.Orders[0].Reported);
        Assert.Equal(collected, Assert.Single(sim.KnowledgeFor("character-001")).Amount);
        Assert.Equal(100 + stock, sim.TreasuryRice + sim.SourceRice);
    }
    [Fact] public void NpcCanRefuseEvenAnAuthorizedOrder()
    {
        var sim = Create(acceptChance: 0); Submit(sim); Advance(sim, 3);
        Assert.Equal(TaxPhase.Refused, sim.Orders[0].Phase); Assert.True(sim.Orders[0].Reported);
        Assert.Equal(100, sim.TreasuryRice); Assert.Equal(60, sim.SourceRice);
    }
    [Fact] public void EmptySourceFailsWithoutCreatingRice()
    {
        var sim = Create(0); Submit(sim); Advance(sim, 3);
        Assert.Equal(TaxPhase.Failed, sim.Orders[0].Phase); Assert.Equal(100, sim.TreasuryRice);
    }
    [Fact] public void UnauthorizedActorAndUnknownTargetCannotMutateState()
    {
        var sim = Create(); var before = sim.SaveJson();
        Assert.Throws<UnauthorizedAccessException>(() => sim.Submit("bad", "character-003", "castle-001", 100));
        Assert.Throws<UnauthorizedAccessException>(() => sim.Submit("bad", "character-001", "unknown", 100));
        Assert.Equal(before, sim.SaveJson());
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void InvalidAmountsCannotMutateState(int amount)
    {
        var sim = Create(); var before = sim.SaveJson();
        Assert.Throws<ArgumentException>(() => Submit(sim, amount: amount)); Assert.Equal(before, sim.SaveJson());
    }
    [Fact] public void RevokeBeforeDecisionCausesRefusal()
    {
        var sim = Create(); Submit(sim); sim.AdvanceOneTick(); sim.Revoke("character-001", "tax-order"); Advance(sim, 2);
        Assert.Equal(TaxPhase.Refused, sim.Orders[0].Phase); Assert.Equal(0, sim.ReservedRice);
    }
    [Fact] public void RevokeAfterReservationReleasesItWithoutCollection()
    {
        var sim = Create(); Submit(sim); Advance(sim, 2); sim.Revoke("character-002", "collect-tax"); Advance(sim, 2);
        Assert.Equal(TaxPhase.Failed, sim.Orders[0].Phase); Assert.Equal(0, sim.ReservedRice);
        Assert.Equal(100, sim.TreasuryRice); Assert.Equal(60, sim.SourceRice);
    }
    [Fact] public void ConcurrentConflictsAreRejectedWithoutIdOrderPriority()
    {
        var sim = Create(100); Submit(sim, "z", 70); Submit(sim, "a", 60); Advance(sim, 3);
        Assert.All(sim.Orders, o => Assert.Equal(TaxPhase.Failed, o.Phase));
        Assert.Equal(100, sim.SourceRice); Assert.Equal(100, sim.TreasuryRice);
    }
    [Fact] public void ReservedStockCannotBeReservedTwiceByLaterOrder()
    {
        var sim = Create(100); Submit(sim, "a", 70); sim.AdvanceOneTick(); Submit(sim, "b", 60); Advance(sim, 5);
        Assert.Equal(70, sim.Orders.Single(o => o.RequestId == "a").Collected);
        Assert.Equal(30, sim.Orders.Single(o => o.RequestId == "b").Collected);
        Assert.Equal(0, sim.SourceRice); Assert.Equal(200, sim.TreasuryRice);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void LoadAtEveryBoundaryMatchesUninterruptedRun(int boundary)
    {
        var original = Create(); Submit(original); Advance(original, boundary);
        var restored = TaxSimulation.LoadJson(original.SaveJson());
        Assert.Equal(original.SaveJson(), restored.SaveJson());
        Advance(original, 5 - boundary); Advance(restored, 5 - boundary);
        Assert.Equal(original.SaveJson(), restored.SaveJson()); Assert.Equal(160, restored.TreasuryRice);
        Assert.Equal(4, restored.AppliedCount); Assert.Equal(0, restored.PendingCount);
    }
    [Fact] public void DuplicateRequestsRemainIdempotentAfterLoadAndCompletion()
    {
        var sim = Create(); Submit(sim); Submit(sim); Advance(sim, 4);
        sim = TaxSimulation.LoadJson(sim.SaveJson()); var before = sim.SaveJson(); Submit(sim);
        Assert.Equal(before, sim.SaveJson()); Advance(sim, 5);
        Assert.Equal(160, sim.TreasuryRice); Assert.Single(sim.Orders); Assert.Equal(4, sim.AppliedCount);
        Assert.Throws<InvalidOperationException>(() => Submit(sim, amount: 101));
    }
    [Fact] public void PauseSurvivesLoadAndDoesNotAdvanceAnything()
    {
        var sim = Create(); Submit(sim); sim.SetPaused(true); var before = sim.SaveJson(); Advance(sim, 3);
        Assert.Equal(before, sim.SaveJson()); sim = TaxSimulation.LoadJson(before); Advance(sim, 2); Assert.Equal(0, sim.Tick);
        sim.SetPaused(false); sim.AdvanceOneTick(); Assert.Equal(1, sim.Tick);
    }
    [Fact] public void InjectedFailureCannotPublishPartialResourceOrEventChanges()
    {
        var sim = Create(); Submit(sim); Advance(sim, 2); var before = sim.SaveJson();
        Assert.Throws<IOException>(() => sim.AdvanceOneTick(() => throw new IOException("injected failure")));
        Assert.Equal(before, sim.SaveJson()); sim.AdvanceOneTick(); Assert.Equal(160, sim.TreasuryRice);
        Assert.Equal(0, sim.ReservedRice); Assert.Equal(3, sim.AppliedCount);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CancellationReleasesReservationAndReports(int boundary)
    {
        var sim = Create(); Submit(sim); Advance(sim, boundary); sim.Cancel("tax-001"); sim.AdvanceOneTick();
        Assert.Equal(TaxPhase.Cancelled, sim.Orders[0].Phase); Assert.True(sim.Orders[0].Reported);
        Assert.Equal(0, sim.ReservedRice); Assert.Equal(100, sim.TreasuryRice);
    }
    [Fact] public void CommittedOutcomeCannotBeCancelled()
    {
        var sim = Create(); Submit(sim); Advance(sim, 3); var before = sim.SaveJson();
        Assert.Throws<InvalidOperationException>(() => sim.Cancel("tax-001")); Assert.Equal(before, sim.SaveJson());
    }
    [Theory] [InlineData("version")] [InlineData("rice")] [InlineData("event")] [InlineData("knowledge")] [InlineData("phase")]
    public void CorruptSavesFailSafely(string corruption)
    {
        var sim = Create(); Submit(sim); sim.AdvanceOneTick(); var before = sim.SaveJson();
        var json = JsonNode.Parse(before)!;
        switch (corruption)
        {
            case "version": json["Version"] = 99; break;
            case "rice": json["Accounts"]![0]!["Balance"] = -1; break;
            case "event": json["Applied"]!.AsArray().Add("unknown-event"); break;
            case "knowledge": json["Knowledge"]![0]!["CharacterId"] = "character-003"; break;
            case "phase": json["Orders"]![0]!["Phase"] = "Completed"; break;
        }
        Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(json.ToJsonString())); Assert.Equal(before, sim.SaveJson());
    }
    [Theory] [InlineData("")] [InlineData("{")] [InlineData("null")] [InlineData("{}")]
    public void MalformedSavesAreRejected(string json) => Assert.Throws<InvalidDataException>(() => TaxSimulation.LoadJson(json));
    [Fact] public void SavedRandomStateContinuesWithoutRerollingNpcDecision()
    {
        var original = Create(100, 50); Submit(original); Advance(original, 2);
        var loaded = TaxSimulation.LoadJson(original.SaveJson()); var roll = loaded.Orders[0].DecisionRoll;
        Advance(original, 3); Advance(loaded, 3); Submit(original, "second", 20); Submit(loaded, "second", 20);
        Advance(original, 4); Advance(loaded, 4); Assert.Equal(original.SaveJson(), loaded.SaveJson());
        Assert.Equal(roll, loaded.Orders[0].DecisionRoll);
    }
    [Fact] public void DiskSaveLoadAndAtomicReplacementRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hakutai-save-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "tax.json");
        var sim = Create(); Submit(sim); sim.SaveFile(path); Advance(sim, 2); sim.SaveFile(path);
        var loaded = TaxSimulation.LoadFile(path); Assert.Equal(sim.SaveJson(), loaded.SaveJson());
        // Only files created by this test are removed; no recursive deletion.
        File.Delete(path); Directory.Delete(directory);
    }
    [Fact] public void CurrentWorldProjectionMatchesClockResourcesAndRandomAfterLoad()
    {
        var sim = Create(); Submit(sim); Advance(sim, 4); sim = TaxSimulation.LoadJson(sim.SaveJson());
        var world = sim.RestoreWorld(); world.Validate();
        Assert.Equal(4, world.Tick);
        Assert.Equal(160, world.Resources.Single(r => r.OwnerOrganizationId == "org-mirror").Rice);
        Assert.Equal(0, world.Resources.Single(r => r.OwnerOrganizationId == "org-fixture-village").Rice);
        Assert.Equal(JsonNode.Parse(sim.SaveJson())!["RandomState"]!.GetValue<ulong>(), world.Random.State);
    }
    [Fact] public void FailedFileReplacementLeavesPreviousSaveIntact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hakutai-save-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "tax.json"); var sim = Create(); sim.SaveFile(path); var previous = File.ReadAllText(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var failure = Record.Exception(() => sim.SaveFile(path));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "Expected locked-save failure.");
        }
        Assert.Equal(previous, File.ReadAllText(path));
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
}
