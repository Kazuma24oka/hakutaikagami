using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国1560年の開始の版（2026-11-12、v110）。
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
        Assert.True(WorldNetwork.Castle(w));Assert.True(NpcDormancy.Enabled(w));
        var s=new WorldSession(w);t.Restart();NewGameWorldTests.At(s,60);o.WriteLine($"最初の1分 {t.ElapsedMilliseconds}ms");
    }
    // 信長が近くの城へ移動して着き、途中の保存から復旧しても同じ未来になる。進める細かさを変えても判断と旅は同じ。
    [Fact]
    public void NobunagaTravelsAndTheNationalWorldRestoresToTheSameFuture()
    {
        var w0=NationalStart.Create(Nobunaga());var me=w0.PlayerControl;var start=NewGameWorld.InitialNode(w0,me);
        var here=BranchRoutes.GraphNodes(w0).Single(n=>n.Id==start).Position;
        var goal=BranchRoutes.GraphEdges(w0).Where(e=>e.From==start||e.To==start).Select(e=>e.From==start?e.To:e.From).Where(n=>n.StartsWith("c-",StringComparison.Ordinal)).OrderBy(n=>n,StringComparer.Ordinal).First();
        WorldSession Begin(){var s=new WorldSession(ContractJson.Copy(w0));NewGameWorldTests.At(s,60);Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:go",goal),s.Revision).Code);return s;}
        const long end=6*3600;
        var a=Begin();NewGameWorldTests.At(a,end);
        var b=Begin();NewGameWorldTests.At(b,end/2);var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(b.DiagnosticSnapshot())));NewGameWorldTests.At(b,end);NewGameWorldTests.At(restored,end);
        Assert.Equal(ContractJson.Write(b.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        string D(WorldSession x){var w=x.DiagnosticSnapshot();return string.Join(";",w.NpcWork!.Decisions.Select(d=>$"{d.Actor} {d.At} {d.Reason}"))+"|"+string.Join(";",w.Branches!.Journeys.Select(j=>$"{j.Actor} {j.Legs[0].StartAt} {j.Legs[^1].EndAt}"));}
        Assert.Equal(D(a),D(b));
        var wa=a.DiagnosticSnapshot();Assert.Equal(new EntityId("site:"+goal),wa.Locations.Single(l=>l.Actor==me).Site);
        o.WriteLine($"移動先 {NewGameWorld.SiteName("site:"+goal)} 判断 {wa.NpcWork!.Decisions.Length}");
    }
}
