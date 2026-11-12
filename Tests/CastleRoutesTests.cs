using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class CastleRoutesTests
{
    static LandMaskIndex Land(){using var stream=typeof(ContinuousWorld).Assembly.GetManifestResourceStream("JapanTerrainMaskV1")!;using var memory=new MemoryStream();stream.CopyTo(memory);return new LandMaskIndex(JapanGeography.Read(memory.ToArray()).Polygons);}

    [Fact]public void EveryDbCastleIsANodeConnectedToTheNationalNetwork()
    {
        var c=CastleRoutes.Catalog;CastleRoutes.Validate(c);
        Assert.Equal(HakutaiDb.PublicGeography().Count(p=>HakutaiDb.Castle(p.Id)!.Region==null),c.Nodes.Length);
        Assert.Equal(c.Nodes.Length+NationalRoutes.Nodes.Length-CastleRoutes.Outside.Count,CastleRoutes.Nodes.Length);
        Assert.Equal(["amami","naha","shuri"],CastleRoutes.Outside.Order());Assert.DoesNotContain(CastleRoutes.Edges,e=>NationalRoutes.Catalog.Edges.Any(c=>c.Id==e.Id&&c.Mode=="land"&&NationalRoutes.Catalog.Nodes.Any(n=>n.Id==c.From)&&NationalRoutes.Catalog.Nodes.Any(n=>n.Id==c.To)));Assert.DoesNotContain(CastleRoutes.Edges,e=>CastleRoutes.Outside.Contains(e.From)||CastleRoutes.Outside.Contains(e.To));
        Assert.Equal(CastleRoutes.Sites.Length,CastleRoutes.Sites.Select(s=>s.Id).Distinct().Count());
        // 登場する武将の所属の城は、すべて拠点になっている
        var nodes=c.Nodes.Select(n=>n.Castle).ToHashSet();
        foreach(var h in HakutaiDb.Houses)foreach(var o in HakutaiDbStart.Roster(h.Id).Officers.Where(o=>o.Castle!=null&&HakutaiDb.Castle(o.Castle)!.Region==null))Assert.Contains(o.Castle!,nodes);
        Assert.Equal("清洲城",c.Nodes.Single(n=>n.Id==CastleRoutes.NodeOf("L-500772")).Name);
    }

    [Fact]public void LandLinksStayOnLandAndCopyTheNationalCosts()
    {
        var c=CastleRoutes.Catalog;var land=Land();
        var at=c.Nodes.ToDictionary(n=>n.Id,n=>new GeoCoordinate(n.LatitudeE7,n.LongitudeE7));
        foreach(var n in NationalRoutes.Catalog.Nodes)at[n.Id]=new(n.LatitudeE7,n.LongitudeE7);
        foreach(var e in c.Edges.Where(e=>e.Mode=="land"))
        {
            var a=at[e.From];var b=at[e.To];
            for(int i=1;i<10;i++)Assert.True(land.Contains(new(a.LatitudeE7+(b.LatitudeE7-a.LatitudeE7)*i/10,a.LongitudeE7+(b.LongitudeE7-a.LongitudeE7)*i/10)),e.Id);
        }
        Assert.True(c.Edges.Count(e=>e.Mode=="sea")<c.Edges.Length/20);
        // 城はすべて地図の陸の上。朝鮮半島の城はない
        Assert.All(c.Nodes,n=>Assert.True(land.Contains(new(n.LatitudeE7,n.LongitudeE7)),n.Name));
        Assert.DoesNotContain(HakutaiDb.PublicGeography(),p=>p.LatitudeE7>347500000&&p.LongitudeE7<1305000000||p.LatitudeE7>339000000&&p.LongitudeE7<1290000000);
    }

    [Fact]public void BrokenCatalogsAreRejected()
    {
        var c=CastleRoutes.Catalog;
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Nodes=c.Nodes.Skip(1).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Edges=c.Edges.Where(e=>e.From!=c.Nodes[0].Id&&e.To!=c.Nodes[0].Id).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Edges=[..c.Edges,c.Edges[0] with{Id="x",Mode="air"}]}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{DatabaseSource="0000000000000000000000000000000000000000"}));
    }

    [Fact]public void TerrainNetworkAddsOnlyVersionedMovementCostsAndKeepsV1Immutable()
    {
        var old=CastleRoutes.Catalog;var terrain=TerrainCastleRoutes.Catalog;
        TerrainCastleRoutes.Validate(terrain);
        Assert.Equal(old.Nodes,terrain.Nodes);
        Assert.Equal(CastleRoutes.Edges.Select(e=>e.Id).Order(),terrain.Edges.Select(e=>e.Id).Order());
        var v1=CastleRoutes.Edges.ToDictionary(e=>e.Id);
        Assert.All(terrain.Edges,e=>Assert.Equal(v1[e.Id].LengthMm,long.Parse(e.BaseLengthMm)));
        Assert.Contains(terrain.Edges,e=>e.Mode=="land"&&e.TerrainFactorPpm>1_000_000);
        Assert.Contains(terrain.Edges,e=>e.EstimatedRiverCrossings>0);
        Assert.Contains(terrain.Edges,e=>e.EstimatedInlandWaterCrossings>0);
        Assert.All(terrain.Edges.Where(e=>e.Mode=="sea"),e=>{Assert.Equal(0,e.EstimatedRiverCrossings);Assert.Equal(0,e.EstimatedInlandWaterCrossings);});
        Assert.Equal("castle-routes-1560-v1",old.Id);
        Assert.Contains(terrain.Edges,e=>long.Parse(e.LengthMm)!=long.Parse(e.BaseLengthMm));
        Assert.Equal(CastleRoutes.Nodes.Length,TerrainCastleRoutes.Nodes.Length);
        Assert.True(terrain.TerrainSource.ElevationZipSha256.All(Uri.IsHexDigit));
        Assert.True(terrain.TerrainSource.VectorZipSha256.All(Uri.IsHexDigit));
        Assert.Throws<InvalidDataException>(()=>TerrainCastleRoutes.Validate(terrain with{Edges=terrain.Edges.Skip(1).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>TerrainCastleRoutes.Validate(terrain with{Edges=[..terrain.Edges,terrain.Edges[0] with{TerrainFactorPpm=3_000_000}]}));
    }
}
