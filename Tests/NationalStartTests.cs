using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国1560年の開始の版（2026-11-12、v111）。
[Trait("Tier","Heavy")]
public sealed class NationalStartTests(ITestOutputHelper o)
{
    internal static string Nobunaga()=>NationalStart.Scenario().Factions.Select(f=>f.Head).Single(h=>NationalStart.Scenario().People.Single(p=>p.Id==h).Name.Contains("信長"));
    [Fact]
    public void ScenarioPlacesDbHousesOfficersAndCastles()
    {
        var s=NationalStart.Scenario();o.WriteLine($"家 {s.Factions.Length} 人 {s.People.Length} 城 {s.Castles.Length} 持ち主のある城 {s.Castles.Count(c=>c.Faction!=null)}");
        Assert.True(s.People.Length>3000);Assert.True(s.Factions.Length>200);
        Assert.All(s.People,p=>Assert.StartsWith("site:",p.Site));
        Assert.Contains(s.People,p=>p.Id.StartsWith("person:hdb/mob/",StringComparison.Ordinal));
        Assert.Equal(CastleRoutes.Sites.Length,s.Castles.Length);
    }
    [Fact]
    public void NobunagaCanStartTheNationalWorld()
    {
        var t=Stopwatch.StartNew();var w=NationalStart.Create(Nobunaga());o.WriteLine($"作成 {t.ElapsedMilliseconds}ms 保存 {ContractJson.Write(w).Length/1024}KB 人 {w.Characters.Length}");
        Assert.True(WorldNetwork.Castle(w));Assert.Equal(TerrainCastleRoutes.Ruleset,w.NewGame!.NetworkPolicy);Assert.True(NpcDormancy.Enabled(w));
        Assert.Equal(NationalDistrictControl.Policy,w.NewGame.DistrictControlPolicy);Assert.Equal(HakutaiDb.Districts.Count,w.DistrictControl!.Districts.Length);
        Assert.Equal(ContractJson.Write(w.DistrictControl),ContractJson.Write(ContractJson.Read<ContractWorld>(ContractJson.Write(w)).DistrictControl));
        var disputed=w.DistrictControl.Districts.Single(d=>d.Id=="陸奥国::津軽郡");Assert.Null(disputed.Owner);Assert.Equal(3,disputed.Shares.Length);
        var forged=ContractJson.Copy(w);forged.DistrictControl=forged.DistrictControl! with{Districts=forged.DistrictControl.Districts.Select(d=>d.Id==disputed.Id?d with{Shares=[]}:d).ToArray()};Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(forged));
        var s=new WorldSession(w);t.Restart();NewGameWorldTests.At(s,60);o.WriteLine($"最初の1分 {t.ElapsedMilliseconds}ms");
    }
    // 信長が近くの城へ移動して着き、途中の保存から復旧しても同じ未来になる。進める細かさを変えても判断と旅は同じ。
    [Fact]
    public void NobunagaTravelsAndTheNationalWorldRestoresToTheSameFuture()
    {
        var w0=NationalStart.Create(Nobunaga());Assert.Equal(TerrainCastleRoutes.Ruleset,w0.NewGame!.NetworkPolicy);var me=w0.PlayerControl;var start=NewGameWorld.InitialNode(w0,me);
        Assert.Equal(TerrainCastleRoutes.Edges.Select(e=>e.Id).Order(),BranchRoutes.GraphEdges(w0).Select(e=>e.Id).Order());
        var oldEdges=CastleRoutes.Edges.ToDictionary(e=>e.Id,StringComparer.Ordinal);
        var next=BranchRoutes.GraphEdges(w0).Where(e=>e.From==start||e.To==start).Select(e=>(Edge:e,Node:e.From==start?e.To:e.From))
            .Where(x=>x.Node.StartsWith("c-",StringComparison.Ordinal)).Select(x=>(x.Edge,x.Node,Seconds:(x.Edge.LengthMm+x.Edge.FootSpeed-1)/x.Edge.FootSpeed))
            .Where(x=>x.Seconds is >=60 and <=3600&&x.Edge.LengthMm!=oldEdges[x.Edge.Id].LengthMm)
            .OrderBy(x=>x.Seconds).ThenBy(x=>x.Node,StringComparer.Ordinal).First();
        var goal=next.Node;
        var activePath=BranchRoutes.Plan(start,goal,BranchRoutes.KnownBlocked(w0,me),w:w0);
        var duration=BranchRoutes.Duration(activePath,w:w0);
        WorldSession Begin(){var s=new WorldSession(ContractJson.Copy(w0));NewGameWorldTests.At(s,60);Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:go",goal),s.Revision).Code);return s;}
        const long departure=60;
        var checkpoint=departure+Math.Max(1,duration/2);var end=departure+duration+30;
        var b=Begin();NewGameWorldTests.At(b,checkpoint);
        var saveRoot=Path.Combine(Path.GetTempPath(),"national-terrain-"+Guid.NewGuid().ToString("N"));
        var store=new GenerationSaveStore(saveRoot,w0.Definitions);store.Save(b,"checkpoint",w0.WorldId,new(.375,1));
        var host=new WorldSessionHost(new WorldSession(ContractJson.Copy(w0)));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("checkpoint"),host.Current,host.Current.Revision));var restored=host.Current;
        Assert.Equal(ContractJson.Write(b.DiagnosticSnapshot().DistrictControl),ContractJson.Write(restored.DiagnosticSnapshot().DistrictControl));
        Assert.Equal(ContractJson.Write(b.GetBranchView(me)),ContractJson.Write(restored.GetBranchView(me)));
        NewGameWorldTests.At(b,end);NewGameWorldTests.At(restored,end);
        Assert.Equal(BranchRoutes.Duration(activePath,w:w0),BranchRoutes.GraphEdges(w0).Where(e=>activePath.Contains(e.Id)).Sum(e=>(long)((e.LengthMm+e.FootSpeed-1)/e.FootSpeed)));
        string D(WorldSession x){var w=x.DiagnosticSnapshot();return string.Join(";",w.NpcWork!.Decisions.Select(d=>$"{d.Actor} {d.At} {d.Reason}"))+"|"+string.Join(";",w.Branches!.Journeys.Select(j=>$"{j.Actor} {j.Legs[0].StartAt} {j.Legs[^1].EndAt}"));}
        Assert.Equal(D(b),D(restored));
        var wa=b.DiagnosticSnapshot();Assert.Equal(new EntityId("site:"+goal),wa.Locations.Single(l=>l.Actor==me).Site);
        static BranchView Comparable(BranchView v)=>v with{Knowledge=v.Knowledge with{Revision=0,Location=v.Knowledge.Location with{Revision=0}}};
        Assert.Equal(ContractJson.Write(Comparable(b.GetBranchView(me))),ContractJson.Write(Comparable(restored.GetBranchView(me))));
        o.WriteLine($"移動先 {NewGameWorld.SiteName("site:"+goal)} 判断 {wa.NpcWork!.Decisions.Length}");
    }
}
