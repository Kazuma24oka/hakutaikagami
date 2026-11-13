using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NationalFieldTerrainTests
{
    [Fact]public void AllCastleAssemblySlotsHaveWalkableGroundWithoutChangingTheLegacyMap()
    {
        Assert.True(CastleRoutes.Nodes.Length>1000);
        foreach(var node in CastleRoutes.Nodes)
        {
            var home=FieldWorld.FromWorld(node.Position);
            foreach(long dy in new long[]{0,8000,-8000})
            {
                var p=new FieldPoint(home.X,home.Y+dy);
                Assert.True(NationalFieldTerrain.Walkable(p),node.Id+"/"+dy);
            }
        }
        var castle=FieldWorld.FromWorld(CastleRoutes.Nodes.First(n=>n.Id.StartsWith("c-",StringComparison.Ordinal)).Position);
        var tile=ContinuousWorld.TileAt(castle);
        foreach(var t in NationalFieldTerrain.Surfaces(tile))foreach(var v in new[]{t.A,t.B,t.C})Assert.Equal(v.HeightMm,NationalFieldTerrain.Height(v.Point));
        var before=ContractJson.CanonicalHash(ContinuousWorld.Surfaces(tile));
        NationalFieldTerrain.Height(castle);Assert.Equal(before,ContractJson.CanonicalHash(ContinuousWorld.Surfaces(tile)));
    }
}
