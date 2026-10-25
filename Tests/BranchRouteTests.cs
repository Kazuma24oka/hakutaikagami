using HakutaiKagami.Core.Contracts;
using System.Text.Json.Nodes;
using System.Text;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class BranchRouteTests
{
    static WorldSession New(bool blocked=true)=>new(BranchRoutes.Create(blocked));
    static void Dispatch(WorldSession s)=>Assert.Equal(ResultCode.Accepted,s.Submit(BranchRoutes.Order(s.DiagnosticSnapshot(),"request:branch"),s.Revision).Code);
    [Fact] public void FastestPathAndKnownBlockChooseDifferentRoutes()
    {Assert.Equal(new[]{"road","pass"},BranchRoutes.Plan("castle","village",[]));Assert.Equal(new[]{"road","bypass-east","bypass-north","bypass-west"},BranchRoutes.Plan("castle","village",["pass"]));Assert.Equal(100,BranchRoutes.Duration(BranchRoutes.Plan("castle","village",[])));Assert.Equal(140,BranchRoutes.Duration(BranchRoutes.Plan("castle","village",["pass"])));}
    [Fact] public void ReverseCourierRouteAndUnreachableGraphAreExplicit()
    {Assert.Equal(70,BranchRoutes.Duration(BranchRoutes.Plan("village","castle",["pass"],true),true));Assert.Throws<InvalidDataException>(()=>BranchRoutes.Plan("castle","village",["road"]));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(10)][InlineData(20)]
    public void HiddenBlockDoesNotAlterPersonalViewsOrQuotedArrival(int seconds)
    {var a=New(true);var b=New(false);Dispatch(a);Dispatch(b);a.AdvanceTo(seconds);b.AdvanceTo(seconds);
        foreach(var observer in new[]{ContractFixture.Officer,ContractFixture.Lord,ContractFixture.Clerk})Assert.Equal(ContractJson.Write(a.GetBranchView(observer)),ContractJson.Write(b.GetBranchView(observer)));}
    [Fact] public void DiscoveryOnlyAtJunctionThenDetourWithoutTeleport()
    {var s=New();Dispatch(s);s.AdvanceTo(20);Assert.Empty(s.GetBranchView(ContractFixture.Officer).KnownBlockedEdges);s.AdvanceTo(21);var view=s.GetBranchView(ContractFixture.Officer);
        Assert.Equal(new RoutePoint(40000,0),view.Position);Assert.Equal(141,view.ArrivalAt);Assert.Equal(new[]{"pass"},view.KnownBlockedEdges);
        Assert.Equal(new[]{"road","bypass-east","bypass-north","bypass-west"},view.PlannedEdges);s.AdvanceTo(51);Assert.Equal(new RoutePoint(80000,10000),s.GetBranchView(ContractFixture.Officer).Position);}
    [Fact] public void IssuerLearnsBlockOnlyFromDelayedMessage()
    {var s=New();Dispatch(s);s.AdvanceTo(30);Assert.Empty(s.GetBranchView(ContractFixture.Lord).KnownBlockedEdges);s.AdvanceTo(31);
        Assert.Equal(new[]{"pass"},s.GetBranchView(ContractFixture.Lord).KnownBlockedEdges);var evidence=s.GetActorView(ContractFixture.Lord).Evidence.Single(e=>e.Claim=="route-block:pass");Assert.Equal(21,evidence.ObservedAt);Assert.Equal(31,evidence.RecordedAt);}
    [Fact] public void OpenPassUsesOriginalRouteAndReportsEarlier()
    {var s=New(false);Dispatch(s);s.AdvanceTo(101);Assert.Equal(ContractFixture.Village,s.GetActorView(ContractFixture.Officer).Location.Site);s.AdvanceTo(151);Assert.Equal("任務完了",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);}
    [Fact] public void DetourReportUsesKnownPassClosureAndArrivesAt211()
    {var s=New();Dispatch(s);s.AdvanceTo(141);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);s.AdvanceTo(210);Assert.Equal("発送済・結果未確認",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);s.AdvanceTo(211);Assert.Equal("任務完了",Assert.Single(s.GetActorView(ContractFixture.Lord).Tasks).KnownStatus);Assert.Single(s.DiagnosticSnapshot().EffectLedger,e=>e.BusinessKey.EndsWith("/finish/1"));}
    [Fact] public void BatchedAndSingleSecondAdvanceProduceSamePhysicalOutcome()
    {var a=New();var b=New();Dispatch(a);Dispatch(b);a.AdvanceTo(211);for(int i=1;i<=211;i++)b.AdvanceTo(i);
        Assert.Equal(a.GetBranchView(ContractFixture.Officer).Position,b.GetBranchView(ContractFixture.Officer).Position);Assert.Equal(a.DiagnosticSnapshot().EffectLedger,b.DiagnosticSnapshot().EffectLedger);
        Assert.Equal(ContractJson.Write(a.DiagnosticSnapshot().Branches),ContractJson.Write(b.DiagnosticSnapshot().Branches));}
    [Theory][InlineData(20)][InlineData(21)][InlineData(51)][InlineData(140)]
    public void SaveBeforeAndAfterDiscoveryPreservesKnowledgeRouteAndContinuation(int at)
    {var s=New();Dispatch(s);s.AdvanceTo(at);var root=Path.Combine(Path.GetTempPath(),"hakutai-branch-test-"+Guid.NewGuid().ToString("N"));var store=new GenerationSaveStore(root,BranchRoutes.Create().Definitions);
        store.Save(s,"test",s.DiagnosticSnapshot().WorldId,new(.25,1));var prepared=store.Prepare("test");var host=new WorldSessionHost(New());Assert.Equal(ResultCode.Accepted,host.CommitLoad(prepared,host.Current,host.Current.Revision));
        var before=s.GetBranchView(ContractFixture.Officer);var after=host.Current.GetBranchView(ContractFixture.Officer);Assert.Equal(before.Position,after.Position);Assert.Equal(before.KnownBlockedEdges,after.KnownBlockedEdges);Assert.Equal(before.PlannedEdges,after.PlannedEdges);Assert.True(host.Current.Paused);
        s.AdvanceTo(211);host.Current.SetPaused(false);host.Current.AdvanceTo(211);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot().Branches),ContractJson.Write(host.Current.DiagnosticSnapshot().Branches));Assert.Equal(s.DiagnosticSnapshot().EffectLedger,host.Current.DiagnosticSnapshot().EffectLedger);}
    [Fact] public void SchemaOnePayloadRemainsByteCompatibleWithoutGraph()
    {var bytes=ContractJson.Write(ContractFixture.Create());Assert.DoesNotContain("Branches",Encoding.UTF8.GetString(bytes));Assert.Null(ContractJson.Read<ContractWorld>(bytes).Branches);}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void BrokenGraphCannotBecomeLive(int fault)
    {var s=New();Dispatch(s);s.AdvanceTo(51);var w=s.DiagnosticSnapshot();switch(fault){case 0:w.Branches=null;break;case 1:w.Schema=1;break;case 2:w.Branches=w.Branches! with{BlockedEdges=["missing"]};break;case 3:w.Locations[1]=w.Locations[1] with{ProgressMm=999};break;}Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact] public void MissingGraphInSchemaTwoAndUnknownModuleVersionAreRejected()
    {var node=JsonNode.Parse(ContractJson.Write(BranchRoutes.Create()))!;node.AsObject().Remove("Branches");var w=ContractJson.Read<ContractWorld>(Encoding.UTF8.GetBytes(node.ToJsonString()));Assert.Throws<InvalidDataException>(()=>new WorldSession(w));w=BranchRoutes.Create();w.Schema=3;Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact] public void RenderingAndPlanningDoNotDrawRandomnessOrMutateKnowledge()
    {var s=New();Dispatch(s);s.AdvanceTo(20);var before=ContractJson.Write(s.DiagnosticSnapshot());for(int i=0;i<20;i++)s.GetBranchView(ContractFixture.Officer);Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));}
    [Fact] public void IssuerCounterfactualViewStillMatchesBeforeClosureReport()
    {var a=New();var b=New(false);Dispatch(a);Dispatch(b);a.AdvanceTo(30);b.AdvanceTo(30);Assert.Equal(ContractJson.Write(a.GetBranchView(ContractFixture.Lord)),ContractJson.Write(b.GetBranchView(ContractFixture.Lord)));}
    [Fact] public void UnknownImpassableFirstEdgeStopsAtOriginAndReports()
    {var w=BranchRoutes.Create();w.Branches=w.Branches! with{BlockedEdges=["road"]};var s=new WorldSession(w);Dispatch(s);s.AdvanceTo(3);var view=s.GetBranchView(ContractFixture.Officer);Assert.False(view.Walking);Assert.Equal(new RoutePoint(0,0),view.Position);Assert.Equal(ExecutionPhase.Stopped,s.DiagnosticSnapshot().Tasks[0].Execution);}
    [Fact] public void LongerDetourCanExpireWithoutTeleportOrCompletionEffect()
    {var s=New();var i=BranchRoutes.Order(s.DiagnosticSnapshot(),"request:deadline");s.Submit(i with{Deadline=i.Deadline with{At=110,AbsoluteLatestAt=110}},0);s.AdvanceTo(111);var view=s.GetBranchView(ContractFixture.Officer);Assert.False(view.Walking);Assert.Equal(new RoutePoint(80000,70000),view.Position);s.AdvanceTo(211);Assert.Equal(view.Position,s.GetBranchView(ContractFixture.Officer).Position);Assert.DoesNotContain(s.DiagnosticSnapshot().EffectLedger,e=>e.BusinessKey.EndsWith("/finish/1"));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ForgedObservationOrProgressCannotBeLoaded(int fault)
    {var s=New();Dispatch(s);s.AdvanceTo(51);var w=s.DiagnosticSnapshot();switch(fault){case 0:w.Actions[0]=w.Actions[0] with{Progress=0};break;case 1:var local=w.Evidence.Single(e=>e.Observer==ContractFixture.Officer&&e.Claim=="route-block:pass");w.Evidence=w.Evidence.Select(e=>e.Id==local.Id?e with{ObservedAt=20}:e).ToArray();break;case 2:var id=w.Evidence.Single(e=>e.Observer==ContractFixture.Officer&&e.Claim=="route-block:pass").Id;w.Knowledge=w.Knowledge.Where(k=>k.Evidence!=id).ToArray();break;}Assert.Throws<InvalidDataException>(()=>new WorldSession(w));}
    [Fact] public void GraphSaveUsesWorldModuleTwoAndRejectsMismatchedManifest()
    {var s=New();Dispatch(s);s.AdvanceTo(51);var root=Path.Combine(Path.GetTempPath(),"hakutai-branch-manifest-"+Guid.NewGuid().ToString("N"));var store=new GenerationSaveStore(root,BranchRoutes.Create().Definitions);var saved=store.Save(s,"test",s.DiagnosticSnapshot().WorldId);var path=Path.Combine(root,"test",saved.GenerationId,"manifest.json");var json=JsonNode.Parse(File.ReadAllBytes(path))!;Assert.Equal(2,json["SchemaVersions"]!["World"]!.GetValue<int>());json["SchemaVersions"]!["World"]=1;File.WriteAllText(path,json.ToJsonString());Assert.Equal(ResultCode.UnsupportedVersion,Assert.Throws<ContractLoadException>(()=>store.Prepare("test",saved.GenerationId)).Code);}

}
