using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class PreparedAdvanceTests
{
    static WorldSession New(){var s=new WorldSession(ContractFixture.Create());s.SetPaused(false);return s;}
    static string Hash(WorldSession s)=>ContractJson.CanonicalHash(s.DiagnosticSnapshot());
    [Fact]public void PreparingDoesNotAdvanceLiveWorldOrExposeFutureBeforePublication()
    {
        var s=New();var host=new WorldSessionHost(s);string before=Hash(s);var c=s.CaptureAdvance(5,s.Revision);var p=c.Prepare();Assert.Equal(before,Hash(s));Assert.Same(s,host.Current);Assert.DoesNotContain(typeof(PreparedAdvance).GetProperties(),p=>p.PropertyType==typeof(ContractWorld)||p.PropertyType==typeof(WorldSession)||p.PropertyType==typeof(AdvanceResult));Assert.Equal(ResultCode.Accepted,host.CommitAdvance(p,out var display));Assert.NotNull(display);Assert.Equal(5,display.Result.ReachedAt);Assert.Equal(5,host.Current.Seconds);Assert.Equal(Hash(host.Current),ContractJson.CanonicalHash(display.Display));
    }
    [Theory][InlineData("pause")][InlineData("draw")][InlineData("submit")][InlineData("advance")]
    public void LaterSourceTransactionRejectsThePreparedFuture(string change)
    {
        var s=New();var host=new WorldSessionHost(s);var c=s.CaptureAdvance(5,s.Revision);var p=c.Prepare();switch(change){case "pause":s.SetPaused(true);break;case "draw":s.Draw(new("decision:source-change"),"decisions");break;case "submit":s.Submit(ContractFixture.Order("request:source-change"),s.Revision);break;case "advance":s.AdvanceTo(1);break;}string current=Hash(s);Assert.False(c.Matches(s));Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(p,out var display));Assert.Null(display);Assert.Equal(current,Hash(host.Current));
    }
    [Fact]public void AnotherSessionWithSameWorldAndRevisionCannotAcceptTheFuture(){var s=New();var other=new WorldSession(s.DiagnosticSnapshot());var host=new WorldSessionHost(other);var p=s.CaptureAdvance(5,s.Revision).Prepare();string before=Hash(other);Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(p,out var display));Assert.Null(display);Assert.Equal(before,Hash(other));}
    [Fact]public void TwoReadyFuturesCannotBothPublishFromOneSource(){var s=New();var host=new WorldSessionHost(s);var c=s.CaptureAdvance(5,s.Revision);var a=c.Prepare();var b=c.Prepare();Assert.Equal(ResultCode.Accepted,host.CommitAdvance(a,out _));string first=Hash(host.Current);Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(b,out _));Assert.Equal(first,Hash(host.Current));}
    [Fact]public void SameFutureCannotPublishTwiceEvenOnAnotherHost(){var s=New();var a=new WorldSessionHost(s);var b=new WorldSessionHost(s);var p=s.CaptureAdvance(5,s.Revision).Prepare();Assert.Equal(ResultCode.Accepted,a.CommitAdvance(p,out _));Assert.Equal(ResultCode.StateChanged,b.CommitAdvance(p,out _));Assert.Same(s,b.Current);}
    [Fact]public async Task CancelBeforePreparationKeepsSourceUnchanged(){var s=New();string before=Hash(s);using var stop=new CancellationTokenSource();stop.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>s.CaptureAdvance(5,s.Revision).PrepareAsync(stop.Token));Assert.Equal(before,Hash(s));}
    [Fact]public void CancelAfterReadyCannotPublish(){var s=New();var host=new WorldSessionHost(s);string before=Hash(s);using var stop=new CancellationTokenSource();var p=s.CaptureAdvance(5,s.Revision).Prepare(stop.Token);stop.Cancel();Assert.Equal(ResultCode.NotAvailable,host.CommitAdvance(p,out var display));Assert.Null(display);Assert.Equal(before,Hash(s));}
    [Fact]public void FutureFaultNeverTouchesSourceOrHost(){var s=New();var host=new WorldSessionHost(s);string before=Hash(s);Assert.Throws<IOException>(()=>s.CaptureAdvance(5,s.Revision).Prepare(default,()=>throw new IOException("test-fault")));Assert.Same(s,host.Current);Assert.Equal(before,Hash(s));}
    [Fact]public void EditingPublishedDisplayCannotMutateCommittedSession(){var s=New();var host=new WorldSessionHost(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(s.CaptureAdvance(5,s.Revision).Prepare(),out var p));string before=Hash(host.Current);p!.Display.ElapsedSeconds=900;p.Display.Characters[0]=p.Display.Characters[0] with{Life=LifeState.Dead};Assert.Equal(before,Hash(host.Current));}
    [Fact]public async Task CommandsDuringWorkerPreparationInvalidateOnlyThatSpeculation()
    {
        var s=New();var host=new WorldSessionHost(s);var capture=s.CaptureAdvance(5,s.Revision);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var work=Task.Run(()=>capture.Prepare(default,()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();}));
        try{Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));s.SetPaused(true);string changed=Hash(s);release.Set();var ready=await work;Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(ready,out _));Assert.Equal(changed,Hash(host.Current));}finally{release.Set();}
    }
    [Fact]public async Task ObserverCanReadOldWorldWhileFutureIsBlocked()
    {
        var s=New();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var work=Task.Run(()=>s.CaptureAdvance(5,s.Revision).Prepare(default,()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();}));
        try{Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));var read=Task.Run(()=>s.GetActorView(ContractFixture.Officer));var view=await read.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(0,view.ElapsedSeconds);Assert.False(work.IsCompleted);}finally{release.Set();await work;}
    }
    [Theory][InlineData(false)][InlineData(true)]public void PausedSemanticsMatchTheSynchronousCore(bool paused)
    {
        var s=New();s.SetPaused(paused);var expected=new WorldSession(s.DiagnosticSnapshot());var result=expected.AdvanceTo(5);var host=new WorldSessionHost(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(s.CaptureAdvance(5,s.Revision).Prepare(),out var published));Assert.Equal(result,published!.Result);Assert.Equal(Hash(expected),Hash(host.Current));
    }
    [Theory][InlineData(1)][InlineData(10000)]public void BoundaryBudgetAndEventsMatchTheSynchronousCore(int budget)
    {
        var s=New();Assert.Equal(ResultCode.Accepted,s.Submit(ContractFixture.Order("request:budget"),s.Revision).Code);var expected=new WorldSession(s.DiagnosticSnapshot());var result=expected.AdvanceTo(20,budget);var host=new WorldSessionHost(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(s.CaptureAdvance(20,s.Revision,budget).Prepare(),out var publication));Assert.Equal(result,publication!.Result);Assert.Equal(Hash(expected),Hash(host.Current));
    }
    [Fact]public void ImportantPhysicalLetterStillStopsAtTheSameBoundary(){var s=New();Assert.Equal(ResultCode.Accepted,s.Submit(ContractFixture.Order("request:important",important:true),s.Revision).Code);var expected=new WorldSession(s.DiagnosticSnapshot());expected.AdvanceTo(100);var host=new WorldSessionHost(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(s.CaptureAdvance(100,s.Revision).Prepare(),out _));Assert.True(host.Current.Paused);Assert.True(host.Current.Seconds<100);Assert.Equal(Hash(expected),Hash(host.Current));}
    [Fact]public void SavingLiveWorldCannotSaveAnUnpublishedFuture(){var s=New();var host=new WorldSessionHost(s);var p=s.CaptureAdvance(5,s.Revision).Prepare();var before=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"hakutai-prepared-advance",Guid.NewGuid().ToString("N")),before.Definitions);string hash=Hash(s);store.Save(s,"manual",before.WorldId);Assert.Equal(0,store.Prepare("manual").Seconds);Assert.Equal(hash,Hash(s));Assert.Equal(ResultCode.Accepted,host.CommitAdvance(p,out _));Assert.Equal(5,host.Current.Seconds);Assert.Equal(0,store.Prepare("manual").Seconds);}
    [Fact]public void EditingAnExportedDiagnosticCopyCannotChangeCapturedPrivateGeneration(){var s=New();var c=s.CaptureAdvance(5,s.Revision);var exported=s.DiagnosticSnapshot();exported.Characters[0]=exported.Characters[0] with{Life=LifeState.Dead};var expected=new WorldSession(s.DiagnosticSnapshot());expected.AdvanceTo(5);var host=new WorldSessionHost(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(c.Prepare(),out _));Assert.Equal(Hash(expected),Hash(host.Current));}
    [Fact]public void ZeroLengthPreparationDoesNotInventTimeOrRevision(){var s=New();var host=new WorldSessionHost(s);string before=Hash(s);Assert.Equal(ResultCode.Accepted,host.CommitAdvance(s.CaptureAdvance(s.Seconds,s.Revision).Prepare(),out _));Assert.Equal(before,Hash(host.Current));}
    [Theory][InlineData("past")][InlineData("budget")][InlineData("revision")]
    public void InvalidCaptureDoesNotChangeSource(string kind){var s=New();string before=Hash(s);if(kind=="revision")Assert.Throws<InvalidOperationException>(()=>s.CaptureAdvance(5,s.Revision+1));else Assert.Throws<ArgumentOutOfRangeException>(()=>s.CaptureAdvance(kind=="past"?-1:5,s.Revision,kind=="budget"?0:10000));Assert.Equal(before,Hash(s));}
}
