using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class WorldTimeControlTests
{
    static WorldSession Start(string epoch="1560-01-01") {var w=ContractFixture.Create();w.EpochDate=epoch;w.TimeControl=WorldTimeControl.Initial();return new(w);}
    static void Good(CommandResult r)=>Assert.Equal(ResultCode.Accepted,r.Code);
    static void Off(WorldSession s)=>Good(s.ConfigureTime(s.DiagnosticSnapshot().TimeControl!.Rules.Select(r=>r with{Response=TimeResponse.Off}).ToArray(),s.Revision));
    [Fact]public void PhysicalImportantReceiptStopsBeforeOpening(){var s=Start();Good(s.Submit(ContractFixture.Order("request:letter",important:true),s.Revision));s.AdvanceTo(20);Assert.Equal(2,s.Seconds);Assert.True(s.Paused);Assert.Equal(TimeNoticeKind.ImportantLetter,WorldTimeControl.Pending(s.DiagnosticSnapshot())!.Kind);Assert.DoesNotContain(s.DiagnosticSnapshot().Messaging.Knowledge,k=>k.CharacterId==s.PlayerControl.Value);}
    [Fact]public void RemoteImportantReceiptDoesNotStopPlayer(){var s=Start();Good(s.Submit(ContractFixture.Order("request:remote",important:true,executor:ContractFixture.Clerk),s.Revision));s.AdvanceTo(20);Assert.Equal(20,s.Seconds);Assert.Empty(s.DiagnosticSnapshot().TimeControl!.Notices);}
    [Fact]public void TurningStopsOffKeepsDeliveryAndUnknownBody(){var s=Start();Off(s);Good(s.Submit(ContractFixture.Order("request:off",important:true),s.Revision));s.AdvanceTo(20);Assert.Equal(20,s.Seconds);Assert.False(s.Paused);Assert.Contains(s.DiagnosticSnapshot().Messaging.Receipts,r=>r.ReceiverId==s.PlayerControl.Value);Assert.Null(WorldTimeControl.Pending(s.DiagnosticSnapshot()));}
    [Fact]public void NextOwnNoticeWorksEvenWhenAutomaticStopsAreOff(){var s=Start();Off(s);Good(s.Submit(ContractFixture.Order("request:wait",important:true),s.Revision));var r=s.AdvanceTo(20,stopAtNotice:true);Assert.Equal("Notice",r.StopReason);Assert.Equal(2,s.Seconds);}
    [Fact]public void ResumeDoesNotRepeatSameReceipt(){var s=Start();Good(s.Submit(ContractFixture.Order("request:resume",important:true),s.Revision));s.AdvanceTo(20);s.SetPaused(false);s.AdvanceTo(20);Assert.Equal(20,s.Seconds);Assert.Single(s.DiagnosticSnapshot().TimeControl!.Notices);}
    [Fact]public void KnownDeadlineStopsBeforeExpiry(){var s=Start();Good(s.Submit(ContractFixture.Order("request:deadline",deadline:10) with{WorkSeconds=20},s.Revision));s.AdvanceTo(20);Assert.Equal(9,s.Seconds);Assert.Equal(TimeNoticeKind.Deadline,WorldTimeControl.Pending(s.DiagnosticSnapshot())!.Kind);}
    [Fact]public void SummaryRequiresExplicitContinuationAndOccursOnce(){var s=Start("1649-12-31");s.AdvanceTo(200000);Assert.Equal(86400,s.Seconds);Assert.Equal(TimeNoticeKind.HistoricalEnd,WorldTimeControl.Pending(s.DiagnosticSnapshot())!.Kind);s.SetPaused(false);Assert.True(s.Paused);Good(s.ContinueAfterHistory(s.Revision));s.SetPaused(false);s.AdvanceTo(200000);Assert.Equal(200000,s.Seconds);Assert.Single(s.DiagnosticSnapshot().TimeControl!.Notices);}
    [Fact]public void InvalidSettingsDoNotMutateWorld(){var s=Start();var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.InvalidInput,s.ConfigureTime([],s.Revision).Code);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));}
    [Fact]public void OldWorldRemainsWithoutNewPolicy(){var s=new WorldSession(ContractFixture.Create());Assert.Equal(ResultCode.NotAvailable,s.ConfigureTime(WorldTimeControl.Initial().Rules,s.Revision).Code);Assert.Null(s.DiagnosticSnapshot().TimeControl);Assert.DoesNotContain("TimeControl",System.Text.Encoding.UTF8.GetString(ContractJson.Write(s.DiagnosticSnapshot())));}
    [Fact]public void QuietYearSkipsEmptySeconds(){var s=Start();var r=s.AdvanceTo(365L*86400);Assert.Equal(365L*86400,s.Seconds);Assert.Equal(1,r.Processed);}

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void RealSaveRestoresPendingSettingsAndSameFuture(int phase)
    {
        var s=Start(phase>=4?"1649-12-31":"1560-01-01");
        if(phase<4){Good(s.Submit(ContractFixture.Order("request:save-time",important:true),s.Revision));if(phase==1)s.AdvanceTo(1);if(phase>=2)s.AdvanceTo(20);if(phase==3){s.SetPaused(false);Off(s);s.AdvanceTo(20);}}
        else{s.AdvanceTo(86400);if(phase==5)Good(s.ContinueAfterHistory(s.Revision));}
        s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"time-control-"+Guid.NewGuid().ToString("N")),w.Definitions);
        Assert.True(store.Save(s,"clock",w.WorldId,new(0,86400)).Published);
        var restored=new WorldSessionHost(Start());var prepared=store.Prepare("clock");Assert.Equal(86400,prepared.Presentation.DisplaySpeed);
        Assert.Equal(ResultCode.Accepted,restored.CommitLoad(prepared,restored.Current,restored.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(restored.Current.DiagnosticSnapshot()));
        var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(!string.IsNullOrEmpty(dir)){Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,$"time-control-{phase}.before.json"),ContractJson.Write(w));File.WriteAllBytes(Path.Combine(dir,$"time-control-{phase}.restored.json"),ContractJson.Write(restored.Current.DiagnosticSnapshot()));}
        foreach(var x in new[]{s,restored.Current}){if(x.DiagnosticSnapshot().TimeControl is{SummaryAt:not null,ContinuedAt:null})Good(x.ContinueAfterHistory(x.Revision));x.SetPaused(false);x.AdvanceTo(x.Seconds+30);}
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.Current.DiagnosticSnapshot()));
    }
    [Fact]public void DifferentSpeedPartitionsKeepKnownEventsAndWorldOutcomes()
    {
        var a=Start();var b=Start();foreach(var s in new[]{a,b}){Off(s);Good(s.Submit(ContractFixture.Order("request:partition"),s.Revision));}
        a.AdvanceTo(120);for(int t=1;t<=120;t++)b.AdvanceTo(t);
        var x=a.DiagnosticSnapshot();var y=b.DiagnosticSnapshot();Assert.Equal(ContractJson.Write(x.TimeControl),ContractJson.Write(y.TimeControl));Assert.Equal(x.Tasks.Select(t=>(t.Id,t.Execution,t.FinishedAt)),y.Tasks.Select(t=>(t.Id,t.Execution,t.FinishedAt)));Assert.Equal(x.ResourceLots.Select(l=>(l.Id,l.Quantity)),y.ResourceLots.Select(l=>(l.Id,l.Quantity)));Assert.Equal(ContractJson.Write(x.RandomStreams),ContractJson.Write(y.RandomStreams));
    }
    [Fact]public void NearPassCannotSkipObservationWindow()
    {
        Assert.Equal(59,TimeObservationSweep.FirstCandidate(new(0,0),new(0,0),new(-200000,0),new(200000,0),200));
        Assert.Null(TimeObservationSweep.FirstCandidate(new(0,0),new(0,0),new(-200000,90000),new(200000,90000),200));
        Assert.Null(TimeObservationSweep.FirstCandidate(new(0,0),new(0,0),new(1000,0),new(200000,0),200));
    }
    [Fact]public void StaleNextNoticeFutureCannotOverwriteChangedSettings()
    {
        var s=Start();Good(s.Submit(ContractFixture.Order("request:stale",important:true),s.Revision));var host=new WorldSessionHost(s);var capture=s.CaptureAdvance(100,s.Revision,stopAtNotice:true);var future=capture.Prepare();Off(s);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.StateChanged,host.CommitAdvance(future,out _));Assert.Equal(before,ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
    [Fact]public void NewGameOptInIsReproducibleAndOldProfileDoesNotMigrate()
    {
        var setup=NewGameWorld.Scenario();var store=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"time-profile-"+Guid.NewGuid().ToString("N")));
        var old=store.New(setup,[],"person:nobunaga",1);Assert.Null(NewCampaignStore.Build(old).TimeControl);
        var current=store.New(setup,[],"person:nobunaga",1,timePolicy:WorldTimeControl.Policy);Assert.NotNull(NewCampaignStore.Build(current).TimeControl);
        var forged=NewCampaignStore.Build(old);forged.TimeControl=WorldTimeControl.Initial();Assert.Throws<InvalidDataException>(()=>new WorldSession(forged));
    }

    [Fact]public void PausePreferenceCannotDelayNpcDecisionAtSameWorldSecond()
    {
        var old=NpcPurposeTests.Start();var d=old.NewGame!;var w=NewGameWorld.Create(d.Start,d.Registered,d.Selected.Value,old.Seed,personalAuthorityLayout:d.PersonalAuthorityLayout,historicalCatalog:d.HistoricalCatalog,npcRoutinePolicy:d.NpcRoutinePolicy,inspectionPolicy:d.InspectionPolicy,workPolicy:d.WorkPolicy,demandPolicy:d.DemandPolicy,developmentPolicy:d.DevelopmentPolicy,timePolicy:WorldTimeControl.Policy);
        var a=new WorldSession(w);var b=new WorldSession(w);Off(b);
        foreach(var x in new[]{a,b}){var intent=NewGameWorld.LocalWork(x.DiagnosticSnapshot(),"request:clock-known");Assert.Equal(ResultCode.Accepted,x.Submit(intent,x.Revision).Code);x.SetPaused(false);}
        a.AdvanceTo(1);b.AdvanceTo(1);
        Assert.True(a.Paused);Assert.Equal(TimeNoticeKind.ImportantLetter,WorldTimeControl.Pending(a.DiagnosticSnapshot())!.Kind);
        var aa=a.DiagnosticSnapshot();var bb=b.DiagnosticSnapshot();Assert.NotEmpty(aa.NpcWork!.Decisions);
        Assert.Equal(aa.NpcWork!.Decisions.Select(d=>(d.Actor,d.At,d.Reason,d.Request)),bb.NpcWork!.Decisions.Select(d=>(d.Actor,d.At,d.Reason,d.Request)));
        a.SetPaused(false);a.AdvanceTo(20);b.AdvanceTo(20);
        Assert.Equal(a.DiagnosticSnapshot().NpcWork!.Decisions.Select(d=>(d.Actor,d.At,d.Reason,d.Request)),b.DiagnosticSnapshot().NpcWork!.Decisions.Select(d=>(d.Actor,d.At,d.Reason,d.Request)));
    }
}
