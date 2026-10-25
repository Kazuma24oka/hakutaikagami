using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class RouteContractTests
{
    static WorldSession New()=>new(RouteContract.Create());
    static Intent Order(WorldSession s,string id="request:move",ActionKind action=ActionKind.Move,EntityId? executor=null)=>RouteContract.Order(s.DiagnosticSnapshot(),id,action,executor);
    [Theory][InlineData(0,0,0)][InlineData(10,20000,0)][InlineData(20,40000,0)][InlineData(50,40000,30000)][InlineData(100,40000,80000)]
    public void IntegerDistanceRespectsEachTerrainSpeed(long elapsed,long x,long z)
    {Assert.Equal(new RoutePoint(x,z),RouteContract.Point(RouteContract.DistanceAt(elapsed)));}
    [Fact] public void RoadAndPassHaveSeparateTravelDurations()
    {Assert.Equal(100,RouteContract.Travel(0,120000,false));Assert.Equal(50,RouteContract.Travel(120000,0,true));Assert.Equal(1,RouteContract.Travel(0,0,true));Assert.Equal(11,RouteContract.Travel(39000,60000,true));}
    [Fact] public void CallerCannotInventSpeedOrDeliveryDelay()
    {var s=New();var i=Order(s);Assert.Equal(ResultCode.InvalidInput,s.Submit(i with{WorkSeconds=4},0).Code);Assert.Equal(ResultCode.InvalidInput,s.Submit(i with{DeliverySeconds=2},0).Code);Assert.Empty(s.DiagnosticSnapshot().Tasks);}
    [Theory][InlineData(true)][InlineData(false)]
    public void UnknownRequestActorIsRejectedWithoutThrowing(bool issuer)
    {var s=New();var i=Order(s);var missing=new EntityId("person:missing");
        Assert.Equal(ResultCode.InvalidInput,s.Submit(issuer?i with{Actor=missing}:i with{Executor=missing},0).Code);Assert.Empty(s.DiagnosticSnapshot().Tasks);}
    [Fact] public void ArrivalAndReportAreSeparateBoundaries()
    {var s=New();s.Submit(Order(s),0);s.AdvanceTo(100);Assert.True(s.GetRouteActorView(ContractFixture.Officer).Walking);
        Assert.Equal("発送済・結果未確認",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);
        s.AdvanceTo(101);Assert.False(s.GetRouteActorView(ContractFixture.Officer).Walking);Assert.Equal(ContractFixture.Village,s.GetActorView(ContractFixture.Officer).Location.Site);
        s.AdvanceTo(150);Assert.Equal("発送済・結果未確認",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);
        s.AdvanceTo(151);Assert.Equal("任務完了",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);}
    [Fact] public void DelegationRunsIndependentlyAndConservesResources()
    {var s=New();s.Submit(Order(s),s.Revision);s.Submit(Order(s,"request:delegate",ActionKind.Transfer,ContractFixture.Clerk),s.Revision);s.AdvanceTo(6);
        Assert.True(s.GetRouteActorView(ContractFixture.Officer).Walking);Assert.Equal("任務完了",s.GetActorView(ContractFixture.Lord).Tasks.Last().KnownStatus);
        Assert.Equal(40,s.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity);Assert.Single(s.DiagnosticSnapshot().Transfers);}
    [Fact] public void MovingRecipientInterceptionIsExplicitlyUnsupported()
    {var s=New();s.Submit(Order(s),0);s.AdvanceTo(31);var cancel=Order(s,"request:cancel",ActionKind.Cancel) with{TargetTask=s.DiagnosticSnapshot().Tasks[0].Id};
        Assert.Equal(ResultCode.InvalidInput,s.Submit(cancel,s.Revision).Code);Assert.Single(s.DiagnosticSnapshot().Tasks);}
    [Fact] public void PausedWorldDoesNotChangePoseOrKnowledge()
    {var s=New();s.Submit(Order(s),0);s.AdvanceTo(51);s.SetPaused(true);var view=s.GetRouteActorView(ContractFixture.Officer);s.AdvanceTo(200);Assert.Equal(ContractJson.Write(view),ContractJson.Write(s.GetRouteActorView(ContractFixture.Officer)));}
    [Fact] public void ForgedRouteProgressAndChangedCatalogAreRejected()
    {var s=New();s.Submit(Order(s),0);s.AdvanceTo(51);var w=s.DiagnosticSnapshot();w.Locations[1]=w.Locations[1] with{ProgressMm=1};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
        w=s.DiagnosticSnapshot();w.Definitions=w.Definitions with{Version="other"};Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact] public void RenderingReadsDoNotMutateStateOrDrawRandomness()
    {var s=New();s.Submit(Order(s),0);s.AdvanceTo(51);var before=ContractJson.Write(s.DiagnosticSnapshot());for(int i=0;i<100;i++)s.GetRouteActorView(ContractFixture.Officer);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));}
    [Fact] public void SaveRestoresRouteAndContinuesWithSameEffects()
    {var s=New();s.Submit(Order(s),0);s.Submit(Order(s,"request:delegate",ActionKind.Transfer,ContractFixture.Clerk),s.Revision);s.AdvanceTo(51);
        var path=Path.Combine(Path.GetTempPath(),"hakutaikagami-route-"+Guid.NewGuid().ToString("N"));var store=new GenerationSaveStore(path,RouteContract.Create().Definitions);
        store.Save(s,"test",s.DiagnosticSnapshot().WorldId,new(.25,10));var candidate=store.Prepare("test");var host=new WorldSessionHost(New());
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(candidate,host.Current,host.Current.Revision));Assert.True(host.Current.Paused);
        Assert.Equal(s.GetRouteActorView(ContractFixture.Officer).Position,host.Current.GetRouteActorView(ContractFixture.Officer).Position);
        Assert.Equal(.25,candidate.Presentation.DisplayFraction);host.Current.SetPaused(false);s.AdvanceTo(151);host.Current.AdvanceTo(151);
        var a=s.DiagnosticSnapshot();var b=host.Current.DiagnosticSnapshot();Assert.Equal(a.Locations.Select(l=>l with{Revision=0}),b.Locations.Select(l=>l with{Revision=0}));Assert.Equal(a.EffectLedger,b.EffectLedger);Assert.Equal(a.Transfers,b.Transfers);Assert.Equal(a.Messaging.Messages,b.Messaging.Messages);}
}
