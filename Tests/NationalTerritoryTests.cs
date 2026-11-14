using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国の開始で領地の仕組み（普請・職人と交易・検地と布告・常設の奉行）が動き、自分から動くのは操作する人物の近くで起きている当主だけであること（2026-11-14）。
public sealed class NationalTerritoryTests(ITestOutputHelper o)
{
    [Fact]
    public void NearbyHeadsWorkTheirLandAndTheWorldRestoresToTheSameFuture()
    {
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.True(LiveTerritory.Enabled(w));Assert.True(CastleAdministration.Enabled(w));Assert.True(CastleHandover.Enabled(w));Assert.NotNull(w.Castellan);Assert.NotNull(CastleHandover.View(w));Assert.NotNull(w.PublicWorks);Assert.NotNull(w.Crafts);Assert.NotNull(w.Surveys);Assert.NotNull(w.Administration);
        var s=new WorldSession(w);
        // 月の初めの毎時の区切り（1時）に、近くの当主が定例の評定を開く。開始時の家の銭は0なので、検地（銭30）はまだ始められない。
        NewGameWorldTests.At(s,NpcDormancy.Interval+60);o.WriteLine($"1時間 {sw.ElapsedMilliseconds}ms");
        var mid=s.DiagnosticSnapshot();
        var councils=HouseholdCourt.Events(mid).Where(e=>e.Input.Action==CourtAction.Convene).ToArray();
        Assert.NotEmpty(councils);
        Assert.All(councils,e=>Assert.True(e.At==NpcDormancy.Interval&&NpcDormancy.Awake(mid,e.Actor,e.At),e.Actor.Value+" は遠方で休眠中なのに評定を開いた"));
        Assert.True(councils.Length<HouseSuccession.Factions(mid).Length/4,"遠方の家まで評定を開いている");
        Assert.All(mid.Surveys!.Entries,e=>Assert.True(NpcDormancy.Awake(mid,e.Actor,e.At),e.Actor.Value+" は遠方で休眠中なのに検地を始めた"));
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(mid)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,mid.ElapsedSeconds+600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
        o.WriteLine($"計 {sw.ElapsedMilliseconds}ms");
    }
}
