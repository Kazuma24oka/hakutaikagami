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
        return NewGameWorld.Create(setup,[],"person:imagawa-yoshimoto",20261112,network!=null?NewGameWorld.CompactPersonalAuthority:null,historicalCatalog:Edition,networkPolicy:network);
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
        Assert.Equal(TerrainCastleRoutes.Edges.Length,BranchRoutes.GraphEdges(w).Length);
        Assert.Equal(w.Definitions.Sites.Length,w.NewGame!.Start.Castles.Length);
        string site="site:"+ImagawaHomeNode();
        Assert.Equal("今川館",NewGameWorld.SiteName(site));
        Assert.False(RegionalWeather.North(new EntityId(site)));
        Assert.Throws<InvalidDataException>(()=>World("castle-routes-unknown"));
    }

    [Fact]public void PreviouslySavedCastlePolicyStillUsesItsOriginalUnadjustedRoutes()
    {
        var w=World(CastleRoutes.Ruleset);
        Assert.Equal(CastleRoutes.Ruleset,w.NewGame!.NetworkPolicy);
        Assert.Equal(CastleRoutes.Edges.Length,BranchRoutes.GraphEdges(w).Length);
        Assert.Equal(CastleRoutes.Edges,BranchRoutes.GraphEdges(w));
        var v2=World(TerrainCastleRoutes.Ruleset);
        Assert.Equal(TerrainCastleRoutes.Edges,BranchRoutes.GraphEdges(v2));
        var v1=CastleRoutes.Edges.ToDictionary(e=>e.Id);
        Assert.Contains(TerrainCastleRoutes.Edges,e=>e.LengthMm!=v1[e.Id].LengthMm);
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

    [Fact]public void CastleWorldsNeedCompactAuthorityAndCanInspectACastle()
    {
        var raw=WorldNetwork.WithCastles(NewGameWorld.Scenario(Edition));var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        Assert.Throws<InvalidDataException>(()=>NewGameWorld.Create(setup,[],"person:imagawa-yoshimoto",20261112,historicalCatalog:Edition,networkPolicy:WorldNetwork.CastlePolicy));
        var w=World(WorldNetwork.CastlePolicy);Assert.True(w.Grants.Length<=2*w.NewGame!.Start.People.Length);
        var s=new WorldSession(w);string goal=ImagawaHomeNode();
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:castle-walk",goal),s.Revision).Code);
        NewGameWorldTests.At(s,3600*24*3);
        Assert.Equal(new EntityId("site:"+goal),s.DiagnosticSnapshot().Locations.Single(l=>l.Actor==s.PlayerControl).Site);
        Assert.Equal(ResultCode.Accepted,s.Submit(NewGameWorld.Inspect(s.DiagnosticSnapshot(),"request:castle-inspect"),s.Revision).Code);
        NewGameWorldTests.At(s,s.Seconds+600);
        var w2=s.DiagnosticSnapshot();var req=w2.Requests.Single(r=>r.Id==new EntityId("request:castle-inspect"));Assert.Equal(ActionKind.Inspect,req.Intent.Action);Assert.Equal(new EntityId("site:"+goal),req.Intent.Scope);
        Assert.Equal(ExecutionPhase.Completed,w2.Tasks.Single(t=>t.RequestId==req.Id).Execution);
    }
}
