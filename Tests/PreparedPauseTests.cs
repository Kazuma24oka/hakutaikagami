using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class PreparedPauseTests
{
    static string Hash(WorldSession s)=>ContractJson.CanonicalHash(s.DiagnosticSnapshot());
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PreparationIsPrivateAndMatchesExistingSetPausedExactly(bool desired)
    {
        var source=new WorldSession(ContractFixture.Create());var before=Hash(source);var expected=new WorldSession(source.DiagnosticSnapshot());expected.SetPaused(desired);
        var host=new WorldSessionHost(source);var ready=await source.CapturePause(desired,source.Revision).PrepareAsync();Assert.Equal(before,Hash(source));
        Assert.Equal(ResultCode.Accepted,host.CommitPause(ready,out var display));Assert.Equal(Hash(expected),Hash(host.Current));Assert.Equal(Hash(expected),ContractJson.CanonicalHash(display!));
        string committed=Hash(host.Current);display!.Paused=!display.Paused;display.IdCounters.Clear();Assert.Equal(committed,Hash(host.Current));Assert.Equal(ResultCode.StateChanged,host.CommitPause(ready,out var duplicate));Assert.Null(duplicate);
    }
    [Fact]public void ChangedGenerationRejectsPreparedControl()
    {
        var source=new WorldSession(ContractFixture.Create());var host=new WorldSessionHost(source);var ready=source.CapturePause(false,source.Revision).Prepare();source.SetPaused(!source.Paused);string retained=Hash(source);Assert.Equal(ResultCode.StateChanged,host.CommitPause(ready,out var display));Assert.Null(display);Assert.Equal(retained,Hash(source));
    }
    [Fact]public void CancellationBeforePublicationKeepsSource()
    {
        var source=new WorldSession(ContractFixture.Create());var before=Hash(source);var host=new WorldSessionHost(source);using var stop=new CancellationTokenSource();var ready=source.CapturePause(false,source.Revision).Prepare(stop.Token);stop.Cancel();Assert.Equal(ResultCode.NotAvailable,host.CommitPause(ready,out var display));Assert.Null(display);Assert.Equal(before,Hash(host.Current));
    }
    [Fact]public void FailureHasNoPartialPauseOrRevision()
    {
        var source=new WorldSession(ContractFixture.Create());string before=Hash(source);Assert.Throws<IOException>(()=>source.CapturePause(false,source.Revision).Prepare(beforePreparation:()=>throw new IOException("declared-pause-fault")));Assert.Equal(before,Hash(source));
    }
    [Fact]public void DifferentSessionWithIdenticalBytesCannotAcceptControl()
    {
        var source=new WorldSession(ContractFixture.Create());var host=new WorldSessionHost(new WorldSession(source.DiagnosticSnapshot()));var ready=source.CapturePause(false,source.Revision).Prepare();Assert.Equal(ResultCode.StateChanged,host.CommitPause(ready,out var display));Assert.Null(display);
    }
    [Fact]public void StaleRevisionCannotCaptureControl()
    {
        var source=new WorldSession(ContractFixture.Create());Assert.Throws<InvalidOperationException>(()=>source.CapturePause(false,source.Revision+1));
    }
    [Fact]public async Task ConcurrentPauseAndNextSecondCannotBothOverwriteOneSource()
    {
        var source=new WorldSession(ContractFixture.Create());source.SetPaused(false);var host=new WorldSessionHost(source);var pause=source.CapturePause(true,source.Revision).Prepare();var advance=await source.CaptureAdvance(1,source.Revision).PrepareAsync();Assert.Equal(ResultCode.Accepted,host.CommitPause(pause,out _));string retained=Hash(host.Current);Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(advance,out _));Assert.Equal(retained,Hash(host.Current));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExistingNpcAndRegionalProfilesKeepSetPausedEffects(bool npc)
    {
        var source=new WorldSession(npc?NpcWork.Create():RegionalWork.Create());var expected=new WorldSession(source.DiagnosticSnapshot());expected.SetPaused(false);var host=new WorldSessionHost(source);var ready=await source.CapturePause(false,source.Revision).PrepareAsync();Assert.Equal(ResultCode.Accepted,host.CommitPause(ready,out _));Assert.Equal(Hash(expected),Hash(host.Current));
    }
    [Fact]public void CancellationBeforePreparationDoesNotReadOrChangeSource()
    {
        var source=new WorldSession(ContractFixture.Create());string before=Hash(source);using var stop=new CancellationTokenSource();stop.Cancel();Assert.Throws<OperationCanceledException>(()=>source.CapturePause(true,source.Revision).Prepare(stop.Token));Assert.Equal(before,Hash(source));
    }
    [Fact]public void LoadedSameRevisionGenerationRejectsOldControl()
    {
        var source=new WorldSession(ContractFixture.Create());var host=new WorldSessionHost(source);var ready=source.CapturePause(true,source.Revision).Prepare();var world=source.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"hakutai-pause-load",Guid.NewGuid().ToString("N")),world.Definitions);store.Save(source,"manual",world.WorldId);Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),source,source.Revision));string retained=Hash(host.Current);Assert.Equal(ResultCode.StateChanged,host.CommitPause(ready,out _));Assert.Equal(retained,Hash(host.Current));
    }
    [Fact]public async Task NextSecondPublicationRejectsOldPause()
    {
        var source=new WorldSession(ContractFixture.Create());source.SetPaused(false);var host=new WorldSessionHost(source);var pause=source.CapturePause(true,source.Revision).Prepare();var advance=await source.CaptureAdvance(1,source.Revision).PrepareAsync();Assert.Equal(ResultCode.Accepted,host.CommitAdvance(advance,out _));string retained=Hash(host.Current);Assert.Equal(ResultCode.StateChanged,host.CommitPause(pause,out _));Assert.Equal(retained,Hash(host.Current));
    }
    [Fact]public async Task ActualSourceChangesWhilePrivateControlIsHeld()
    {
        var source=new WorldSession(ContractFixture.Create());using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var capture=source.CapturePause(true,source.Revision);var job=Task.Run(()=>capture.Prepare(beforePreparation:()=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));}));Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));string retained;
        try{source.SetPaused(!source.Paused);retained=Hash(source);}finally{release.Set();}
        var ready=await job;var host=new WorldSessionHost(source);Assert.Equal(ResultCode.StateChanged,host.CommitPause(ready,out _));Assert.Equal(retained,Hash(source));
    }
    [Fact]public async Task CompactThousandPersonControlMatchesExistingFullEffects()
    {
        var records=Enumerable.Range(0,1000).Select(i=>NewGameWorldTests.Officer("created:pause-pop-"+i,site:NationalRoutes.Sites[i%NationalRoutes.Sites.Length].Id.Value)).ToArray();var setup=NewGameWorldTests.Setup(records);var source=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",personalAuthorityLayout:NewGameWorld.CompactPersonalAuthority));var expected=new WorldSession(source.DiagnosticSnapshot());expected.SetPaused(false);var host=new WorldSessionHost(source);var ready=await source.CapturePause(false,source.Revision).PrepareAsync();Assert.Equal(ResultCode.Accepted,host.CommitPause(ready,out _));Assert.Equal(Hash(expected),Hash(host.Current));
    }
}
