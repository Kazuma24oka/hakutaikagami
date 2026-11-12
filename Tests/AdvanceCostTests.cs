using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 進行の重さの見直し 第1段（2026-11-12、v107）。
public sealed class AdvanceCostTests
{
    static WorldSession Large(int n)
    {
        var raw=WorldNetwork.WithCastles(NewGameWorld.Scenario());var sites=CastleRoutes.Catalog.Nodes.Select(x=>"site:"+x.Id).ToArray();
        var records=NewGameWorld.Normalize(Enumerable.Range(0,n).Select(i=>new RegisteredOfficer("created:cost-"+i,null,"鏡山 宗久",1520,new(60,50,70,40),StartTemplate.Ronin,sites[i*7%sites.Length],null,null,AppearanceCatalog.Default(1))).ToArray());
        var request=new WorldSetupRequest(records,[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return new WorldSession(NewGameWorld.Create(setup,records,"person:nobunaga",20261112,NewGameWorld.CompactPersonalAuthority,networkPolicy:WorldNetwork.CastlePolicy));
    }

    [Fact]
    public void LargeStartIsVerifiedOnceAcrossAdvances()
    {
        var s=Large(4200);Assert.True(ContractJson.Write(s.DiagnosticSnapshot().NewGame!).Length>4*1024*1024);
        NewGameWorldTests.At(s,60);int before=NewGameWorld.StartVerifications;
        NewGameWorldTests.At(s,3600);NewGameWorldTests.At(s,86400);
        Assert.Equal(before,NewGameWorld.StartVerifications);
    }

    [Fact]
    public void TurnSnapshotIsSharedUntilTheWorldChangesAndRewritesAreCaught()
    {
        var s=Large(3);var a=s.TurnSnapshot();Assert.Same(a,s.TurnSnapshot());
        NewGameWorldTests.At(s,60);var b=s.TurnSnapshot();Assert.NotSame(a,b);Assert.Equal(60,b.ElapsedSeconds);
        b.ElapsedSeconds=1;Assert.Throws<InvalidOperationException>(()=>s.TurnSnapshot());
    }
}
