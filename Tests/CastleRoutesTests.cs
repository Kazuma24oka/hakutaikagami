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
        Assert.Equal(HakutaiDb.PublicGeography().Count,c.Nodes.Length);
        Assert.Equal(c.Nodes.Length+NationalRoutes.Nodes.Length,CastleRoutes.Nodes.Length);
        Assert.Equal(CastleRoutes.Sites.Length,CastleRoutes.Sites.Select(s=>s.Id).Distinct().Count());
        // 登場する武将の所属の城は、すべて拠点になっている
        var nodes=c.Nodes.Select(n=>n.Castle).ToHashSet();
        foreach(var h in HakutaiDb.Houses)foreach(var o in HakutaiDbStart.Roster(h.Id).Officers.Where(o=>o.Castle!=null))Assert.Contains(o.Castle!,nodes);
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
    }

    [Fact]public void BrokenCatalogsAreRejected()
    {
        var c=CastleRoutes.Catalog;
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Nodes=c.Nodes.Skip(1).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Edges=c.Edges.Where(e=>e.From!=c.Nodes[0].Id&&e.To!=c.Nodes[0].Id).ToArray()}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{Edges=[..c.Edges,c.Edges[0] with{Id="x",Mode="air"}]}));
        Assert.Throws<InvalidDataException>(()=>CastleRoutes.Validate(c with{DatabaseSource="0000000000000000000000000000000000000000"}));
    }
}
