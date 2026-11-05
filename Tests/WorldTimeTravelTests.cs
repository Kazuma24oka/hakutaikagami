using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class WorldTimeTravelTests
{
    static WorldSession Start(){return new(NewGameWorld.Create(NewGameWorld.Scenario(),[],"person:nobunaga",timePolicy:WorldTimeControl.Policy));}
    static void Go(WorldSession s){Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:time-trip","fork"),s.Revision).Code);s.SetPaused(false);}
    [Fact]public void FastTripStopsOnExactArrivalWithSlowResponse(){var s=Start();Go(s);s.AdvanceTo(259200);var w=s.DiagnosticSnapshot();Assert.True(s.Paused);Assert.Equal(TimeNoticeKind.Arrival,WorldTimeControl.Pending(w)!.Kind);Assert.Equal(TimeResponse.Slow,WorldTimeControl.Pending(w)!.Response);Assert.Equal(w.Branches!.Journeys.Single().Legs[^1].EndAt,s.Seconds);Assert.Equal(LocationKind.AtSite,s.GetActorView(s.PlayerControl).Location.Kind);}
    [Fact]public void OneSecondAndBatchedTripReachSamePhysicalPointAndNotices(){var a=Start();var b=Start();Go(a);Go(b);a.AdvanceTo(120);for(long t=1;t<=120;t++)b.AdvanceTo(t);Assert.Equal(a.Seconds,b.Seconds);Assert.Equal(a.GetBranchView(a.PlayerControl).Position,b.GetBranchView(b.PlayerControl).Position);Assert.Equal(ContractJson.Write(a.DiagnosticSnapshot().TimeControl),ContractJson.Write(b.DiagnosticSnapshot().TimeControl));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]public void MovingWorldSaveRestores3dInputAndFuture(int phase){var s=Start();Go(s);s.AdvanceTo(phase==0?1:phase==1?3:259200);s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"time-travel-"+Guid.NewGuid().ToString("N")),w.Definitions);store.Save(s,"time",w.WorldId,new(0,43200));var host=new WorldSessionHost(Start());Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("time"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(w),ContractJson.Write(host.Current.DiagnosticSnapshot()));var dir=Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR");if(!string.IsNullOrEmpty(dir)){Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,$"time-travel-{phase}.before.json"),ContractJson.Write(w));File.WriteAllBytes(Path.Combine(dir,$"time-travel-{phase}.restored.json"),ContractJson.Write(host.Current.DiagnosticSnapshot()));}foreach(var x in new[]{s,host.Current}){x.SetPaused(false);x.AdvanceTo(x.Seconds+100);}Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));}

    [Fact]public void FastRoadApproachStopsAtObservedEnemyBeforeDestination()
    {
        var w=NewGameWorld.Create(NewGameWorld.Scenario(),[],"person:shingen",worldArmyPolicy:NpcArmies.Policy,timePolicy:WorldTimeControl.Policy);w.CommitSequence++;
        NpcArmies.Muster(w,new("decision:time-army"),new("person:nobunaga"),new("org:oda"));var s=new WorldSession(w);
        Assert.Equal(ResultCode.Accepted,s.DecideFactionStance(new(new("request:time-hostility"),FactionStanceAction.DeclareHostile,new("org:takeda"),new("org:oda"),0,10000000),s.PlayerControl,s.Revision).Code);
        Assert.Empty(NpcArmies.Sights(s.DiagnosticSnapshot(),s.PlayerControl));Assert.Empty(s.DiagnosticSnapshot().TimeControl!.Notices);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:time-approach","castle"),s.Revision).Code);s.SetPaused(false);
        for(int n=0;n<20&&!s.Paused;n++)s.AdvanceTo(10000000);
        var end=s.DiagnosticSnapshot();Assert.Equal(TimeNoticeKind.EnemyApproach,WorldTimeControl.Pending(end)!.Kind);Assert.NotEmpty(NpcArmies.Sights(end,s.PlayerControl));Assert.True(s.GetBranchView(s.PlayerControl).Walking);
    }
}
