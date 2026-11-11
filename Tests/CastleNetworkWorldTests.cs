using System.Text;
using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CastleNetworkWorldTests
{
    const string Edition=HistoricalStartCatalog.ImagawaEdition;
    static ContractWorld World(string? network)
    {
        var raw=NewGameWorld.Scenario(Edition);if(network!=null)raw=WorldNetwork.WithCastles(raw);
        var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,[],"person:imagawa-yoshimoto",20261112,historicalCatalog:Edition,networkPolicy:network);
    }
    static string ImagawaHomeNode()=>CastleRoutes.NodeOf(HakutaiDb.House(HakutaiDb.GameLinks["org:imagawa"])!.Home!);

    [Fact]public void WorldsWithoutAVersionKeepTheCurrentNetwork()
    {
        var w=World(null);
        Assert.Null(w.NewGame!.NetworkPolicy);Assert.False(WorldNetwork.Castle(w));
        Assert.Equal(NationalRoutes.Sites,w.Definitions.Sites);
        Assert.Equal(NationalRoutes.Nodes.Length,BranchRoutes.GraphNodes(w).Length);
        Assert.DoesNotContain("NetworkPolicy",Encoding.UTF8.GetString(ContractJson.Write(w)));
        Assert.Equal(ContractJson.CanonicalHash(w),ContractJson.CanonicalHash(World(null)));
    }

    [Fact]public void CastleWorldsUseCastleSitesRoutesAndLookups()
    {
        var w=World(WorldNetwork.CastlePolicy);
        Assert.True(WorldNetwork.Castle(w));
        Assert.Equal(CastleRoutes.Sites.Length,w.Definitions.Sites.Length);
        Assert.Equal(CastleRoutes.Nodes.Length,BranchRoutes.GraphNodes(w).Length);
        Assert.Equal(CastleRoutes.Edges.Length,BranchRoutes.GraphEdges(w).Length);
        Assert.Equal(w.Definitions.Sites.Length,w.NewGame!.Start.Castles.Length);
        string site="site:"+ImagawaHomeNode();
        Assert.Equal("今川館",NewGameWorld.SiteName(site));
        Assert.False(RegionalWeather.North(new EntityId(site)));
        Assert.Throws<InvalidDataException>(()=>World("castle-routes-unknown"));
    }

    [Fact]public void PlayerWalksToACastleAndRecoveredSaveGivesTheSameFuture()
    {
        var s=new WorldSession(World(WorldNetwork.CastlePolicy));string goal=ImagawaHomeNode();
        var move=TravelWorld.Move(s.DiagnosticSnapshot(),"request:castle-walk",goal);
        Assert.Equal(ResultCode.Accepted,s.Submit(move,s.Revision).Code);
        NewGameWorldTests.At(s,60);s.SetPaused(true);var original=s.DiagnosticSnapshot();
        var dir=Path.Combine(Path.GetTempPath(),"hakutai-castle-network-tests",Guid.NewGuid().ToString("N"));
        var store=new GenerationSaveStore(dir,original.Definitions);store.Save(s,"manual",original.WorldId);
        var host=new WorldSessionHost(new WorldSession(original));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("manual"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.CanonicalHash(original),ContractJson.CanonicalHash(host.Current.DiagnosticSnapshot()));
        long end=Math.Max(3600*24*3,s.Seconds+3600);
        NewGameWorldTests.At(s,end);NewGameWorldTests.At(host.Current,end);
        Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(host.Current.DiagnosticSnapshot()));
        var at=s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==s.PlayerControl);
        Assert.Equal(LocationKind.AtSite,at.Kind);Assert.Equal(new EntityId("site:"+goal),at.Site);
    }
}
