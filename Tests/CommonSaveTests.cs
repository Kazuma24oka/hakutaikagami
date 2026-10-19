using System.Text.Json.Nodes;
using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

public sealed class CommonSaveTests
{
    private sealed class TestFolder : IDisposable
    {
        private readonly string temporaryRoot=Path.GetFullPath(Path.GetTempPath());
        public string PathValue { get; }=Path.Combine(Path.GetTempPath(),"hakutaikagami-contract-tests-"+Guid.NewGuid().ToString("N"));
        public TestFolder()=>Directory.CreateDirectory(PathValue);
        public void Dispose()
        {
            var target=Path.GetFullPath(PathValue);
            if(!target.StartsWith(temporaryRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                !Path.GetFileName(target).StartsWith("hakutaikagami-contract-tests-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe test cleanup path.");
            if(Directory.Exists(target))Directory.Delete(target,recursive:true);
        }
    }
    private static WorldSession Session()=>new(ContractFixture.Create());
    private static GenerationSaveStore Store(string root)=>new(root,ContractFixture.Create().Definitions);
    [Fact] public void InFlightTaskAndReservedResourceRoundTrip()
    {
        using var f=new TestFolder();var s=Session();s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),0);s.AdvanceTo(4);
        var before=ContractJson.Write(s.DiagnosticSnapshot());var store=Store(f.PathValue);var save=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId,new(.25,60));
        var candidate=store.Prepare("manual");Assert.Equal(4,candidate.Seconds);Assert.Equal(.25,candidate.Presentation.DisplayFraction);
        var host=new WorldSessionHost(Session());var original=host.Current;
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(candidate,original,original.Revision));Assert.True(host.Current.DiagnosticSnapshot().Paused);
        var actual=host.Current.DiagnosticSnapshot();actual.Paused=false;Assert.Equal(before,ContractJson.Write(actual));
        host.Current.SetPaused(false);host.Current.AdvanceTo(8);Assert.Equal(40,host.Current.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity);
        Assert.Single(host.Current.DiagnosticSnapshot().Transfers);Assert.True(save.Published);
    }
    [Theory] [InlineData(SaveStage.PayloadWritten)] [InlineData(SaveStage.GenerationVerified)] [InlineData(SaveStage.PreviousPublished)]
    public void InterruptedSaveKeepsPreviousPublishedGeneration(SaveStage stage)
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var first=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);
        s.AdvanceTo(5);Assert.Throws<IOException>(()=>store.Save(s,"manual",s.DiagnosticSnapshot().WorldId,fault:x=>{if(x==stage)throw new IOException("injected");}));
        Assert.Equal(first.GenerationId,store.Prepare("manual").GenerationId);Assert.Equal(0,store.Prepare("manual").Seconds);
    }
    [Fact] public void FailureAfterPublicationLeavesNewGenerationReadable()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);s.AdvanceTo(5);
        Assert.Throws<IOException>(()=>store.Save(s,"manual",s.DiagnosticSnapshot().WorldId,fault:x=>{if(x==SaveStage.CurrentPublished)throw new IOException("injected");}));
        Assert.Equal(5,store.Prepare("manual").Seconds);
    }
    [Fact] public void CorruptCurrentDoesNotSilentlyLoadBackupOrMutateActiveWorld()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var first=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);s.AdvanceTo(5);
        var second=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);File.WriteAllText(Path.Combine(f.PathValue,"manual",second.GenerationId,"payload.json"),"{}");
        var before=ContractJson.Write(s.DiagnosticSnapshot());var host=new WorldSessionHost(s);
        Assert.Equal(ResultCode.CorruptSave,Assert.Throws<ContractLoadException>(()=>store.Prepare("manual")).Code);
        Assert.Same(s,host.Current);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));
        Assert.Contains(store.RecoveryOptions("manual"),c=>c.GenerationId==first.GenerationId&&c.Valid&&c.Seconds==0);
        Assert.Contains(store.RecoveryOptions("manual"),c=>c.GenerationId==second.GenerationId&&!c.Valid);
        var chosen=store.Prepare("manual",first.GenerationId);Assert.Equal(ResultCode.Accepted,host.CommitLoad(chosen,s,s.Revision));Assert.Equal(0,host.Current.Seconds);
    }
    [Fact] public void MissingRuleDependenciesAreRejectedBeforeLiveSwap()
    {
        using var f=new TestFolder();var s=Session();Store(f.PathValue).Save(s,"manual",s.DiagnosticSnapshot().WorldId);
        var defs=ContractFixture.Create().Definitions with{Version="different"};var other=new GenerationSaveStore(f.PathValue,defs);
        Assert.Equal(ResultCode.DependencyMismatch,Assert.Throws<ContractLoadException>(()=>other.Prepare("manual")).Code);
    }
    [Fact] public void FutureEnvelopeVersionIsNotSilentlyDowngraded()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var saved=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);
        var path=Path.Combine(f.PathValue,"manual",saved.GenerationId,"manifest.json");var json=JsonNode.Parse(File.ReadAllBytes(path))!;json["EnvelopeVersion"]=99;File.WriteAllText(path,json.ToJsonString());
        Assert.Equal(ResultCode.UnsupportedVersion,Assert.Throws<ContractLoadException>(()=>store.Prepare("manual")).Code);
    }
    [Fact] public void StalePreparedLoadIsRejectedWhenCurrentSessionHasProgressed()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);var c=store.Prepare("manual");var rev=s.Revision;
        var host=new WorldSessionHost(s);s.AdvanceTo(1);Assert.Equal(ResultCode.StateChanged,host.CommitLoad(c,s,rev));Assert.Same(s,host.Current);
    }
    [Fact] public void PreparedLoadIsOneShotEvenForAnEqualRevision()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);var c=store.Prepare("manual");var host=new WorldSessionHost(s);
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(c,s,s.Revision));Assert.Equal(ResultCode.StateChanged,host.CommitLoad(c,host.Current,host.Current.Revision));
    }
    [Fact] public void ModifyingPreparedFilesCannotLoadStaleValidatedBytes()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var saved=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);var c=store.Prepare("manual");var host=new WorldSessionHost(s);
        File.WriteAllText(Path.Combine(f.PathValue,"manual",saved.GenerationId,"presentation.json"),"{}");
        Assert.Throws<ContractLoadException>(()=>host.CommitLoad(c,s,s.Revision));Assert.Same(s,host.Current);
    }
    [Theory] [InlineData("../escape")] [InlineData("C:\\outside")] [InlineData("bad/name")] [InlineData("")]
    public void SaveSlotCannotEscapeRoot(string slot)
    {using var f=new TestFolder();Assert.Throws<ArgumentException>(()=>Store(f.PathValue).Save(Session(),slot,new("world:common-fixture")));Assert.Empty(Directory.EnumerateFileSystemEntries(f.PathValue));}
    [Fact] public void DisplayPhaseAndNewSpeedMustValidate()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);
        Assert.Throws<InvalidDataException>(()=>store.Save(s,"manual",s.DiagnosticSnapshot().WorldId,new(double.NaN,1)));
        Assert.Throws<InvalidDataException>(()=>store.Save(s,"manual",s.DiagnosticSnapshot().WorldId,new(0,4)));
    }
    [Fact] public void OlderBackgroundSnapshotCannotReplaceNewerCurrent()
    {
        using var f=new TestFolder();var s=Session();var old=new WorldSession(s.DiagnosticSnapshot());var store=Store(f.PathValue);s.AdvanceTo(10);var saved=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);
        Assert.False(store.Save(old,"manual",old.DiagnosticSnapshot().WorldId).Published);Assert.Equal(saved.GenerationId,store.Prepare("manual").GenerationId);
    }
    [Fact] public void LegacyTickPayloadReturnsExplicitMigrationRequirement()
    {
        var p=Path.Combine(AppContext.BaseDirectory,"Fixtures","tax-v1-tick-0.json");
        Assert.Equal(ResultCode.MigrationRequired,Assert.Throws<ContractLoadException>(()=>GenerationSaveStore.ReadPayload(File.ReadAllBytes(p))).Code);
    }
    [Fact] public void PayloadHashDoesNotReplaceSemanticValidation()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var saved=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);var dir=Path.Combine(f.PathValue,"manual",saved.GenerationId);
        var world=s.DiagnosticSnapshot();world.ResourceLots[0]=world.ResourceLots[0] with{Quantity=999};var bytes=ContractJson.Write(world);File.WriteAllBytes(Path.Combine(dir,"payload.json"),bytes);
        var manifest=ContractJson.Read<SaveManifest>(File.ReadAllBytes(Path.Combine(dir,"manifest.json"))) with{PayloadHash=ContractJson.Hash(bytes),PayloadByteLength=bytes.LongLength};
        File.WriteAllBytes(Path.Combine(dir,"manifest.json"),ContractJson.Write(manifest));
        Assert.Equal(ResultCode.CorruptSave,Assert.Throws<ContractLoadException>(()=>store.Prepare("manual",saved.GenerationId)).Code);
    }
    [Fact] public void RandomDrawsAndCancellationAfterReloadAreNotRepeated()
    {
        using var f=new TestFolder();var s=Session();s.Draw(new("decision:first"),"decisions");s.Submit(ContractFixture.Order("request:tax",ActionKind.Transfer,40),s.Revision);s.AdvanceTo(4);
        var store=Store(f.PathValue);store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);var host=new WorldSessionHost(s);var c=store.Prepare("manual");host.CommitLoad(c,s,s.Revision);var loaded=host.Current;
        Assert.Equal(s.Draw(new("decision:next"),"decisions"),loaded.Draw(new("decision:next"),"decisions"));
        var target=Assert.Single(loaded.DiagnosticSnapshot().Tasks).Id;
        loaded.Submit(ContractFixture.Order("request:cancel",ActionKind.Cancel) with{TargetTask=target},loaded.Revision);loaded.SetPaused(false);loaded.AdvanceTo(8);
        Assert.Empty(loaded.DiagnosticSnapshot().Transfers);Assert.Equal(ExecutionPhase.Stopped,loaded.DiagnosticSnapshot().Tasks.Single(t=>t.Id==target).Execution);
    }
    [Theory] [InlineData("[]")] [InlineData("null")] [InlineData("17")]
    public void NonObjectProductPayloadIsRejected(string payload)=>Assert.Throws<InvalidDataException>(()=>GenerationSaveStore.ReadPayload(System.Text.Encoding.UTF8.GetBytes(payload)));
    [Fact] public void NullRequiredManifestMapIsClassifiedAsCorrupt()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);var saved=store.Save(s,"manual",s.DiagnosticSnapshot().WorldId);
        var path=Path.Combine(f.PathValue,"manual",saved.GenerationId,"manifest.json");var node=JsonNode.Parse(File.ReadAllBytes(path))!;node["SchemaVersions"]=null;File.WriteAllText(path,node.ToJsonString());
        Assert.Equal(ResultCode.CorruptSave,Assert.Throws<ContractLoadException>(()=>store.Prepare("manual",saved.GenerationId)).Code);
    }
    [Fact] public async Task BackgroundSaveCapturesOneRevisionWhileGameplayContinues()
    {
        using var f=new TestFolder();var s=Session();var store=Store(f.PathValue);
        using var captured=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var job=store.SaveAsync(s,"background",s.DiagnosticSnapshot().WorldId,fault:stage=>{
            if(stage==SaveStage.SnapshotCaptured){captured.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();}
        });
        try{Assert.True(captured.Wait(TimeSpan.FromSeconds(10)));s.AdvanceTo(5);Assert.Equal(5,s.Seconds);}
        finally{release.Set();}
        var result=await job;Assert.Equal(0,result.CapturedRevision);Assert.Equal(0,store.Prepare("background").Seconds);Assert.Equal(5,s.Seconds);
    }
}
