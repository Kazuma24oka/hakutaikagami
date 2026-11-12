using HakutaiKagami.Core.World;

namespace HakutaiKagami.Core.Contracts;

public sealed record TerrainCastleRouteSource(string Name,string Credit,string Terms,string Elevation,string Rivers,string InlandWater,string Roads,string Raster,string ElevationZipSha256,string VectorZipSha256,string InlandWaterSha256);
public sealed record TerrainCastleRouteLink(string Id,string From,string To,string LengthMm,int FootSpeed,int CourierSpeed,string Mode,string Basis,string BaseLengthMm,int TerrainFactorPpm,int EstimatedRiverCrossings,int EstimatedInlandWaterCrossings);
public sealed record TerrainCastleRouteCatalog(int Schema,string Id,int WorldYear,string BasedOn,string Database,string DatabaseSource,TerrainCastleRouteSource TerrainSource,string Policy,CastleRouteNode[] Nodes,TerrainCastleRouteLink[] Edges);

// v1を読み替えず、次に開始する1560年全国版だけが利用する地形コスト版。
// 旧worldのNetworkPolicyがcastle-routes-1560-v1なら、従来の距離・到着時刻を使い続ける。
public static class TerrainCastleRoutes
{
    public const string Ruleset="castle-routes-1560-v2";
    static readonly Lazy<TerrainCastleRouteCatalog> catalog=new(()=>{var c=Read();Validate(c);return c;});
    public static TerrainCastleRouteCatalog Catalog=>ContractJson.Copy(catalog.Value);
    static TerrainCastleRouteCatalog Read()
    {
        using var s=typeof(TerrainCastleRoutes).Assembly.GetManifestResourceStream("CastleRoutes1560V2")??throw new InvalidDataException("全国地形移動網がありません");
        using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<TerrainCastleRouteCatalog>(m.ToArray());
    }
    public static void Validate(TerrainCastleRouteCatalog c)
    {
        void Need(bool ok,string message){if(!ok)throw new InvalidDataException("全国地形移動網の検査："+message);}
        Need(c.Schema==1&&c.Id==Ruleset&&c.WorldYear==1560&&c.BasedOn==CastleRoutes.Ruleset&&c.Database==HakutaiDb.Edition1560&&c.DatabaseSource==HakutaiDb.Source.Commit,"版・元データ");
        Need(c.TerrainSource.Name=="地球地図日本（国土地理院）"&&c.TerrainSource.Terms=="国土地理院コンテンツ利用規約"&&c.TerrainSource.ElevationZipSha256.Length==64&&c.TerrainSource.VectorZipSha256.Length==64&&c.TerrainSource.InlandWaterSha256.Length==64&&!string.IsNullOrWhiteSpace(c.TerrainSource.InlandWater),"地形データの出典");
        Need(c.Nodes.SequenceEqual(CastleRoutes.Catalog.Nodes),"旧版の拠点を変更している");
        var original=CastleRoutes.Edges.ToDictionary(e=>e.Id,StringComparer.Ordinal);
        Need(c.Edges.Length==original.Count&&c.Edges.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()==c.Edges.Length&&c.Edges.All(e=>original.ContainsKey(e.Id)),"旧版の道路構成と一致しない");
        foreach(var e in c.Edges)
        {
            var old=original[e.Id];
            Need(e.From==old.From&&e.To==old.To&&e.FootSpeed==old.FootSpeed&&e.CourierSpeed==old.CourierSpeed,"道路の接続と速さが違う");
            long baseLength=0,length=0;
            Need(long.TryParse(e.BaseLengthMm,out baseLength)&&baseLength==old.LengthMm&&long.TryParse(e.LengthMm,out length)&&length>0,"基準距離");
            Need(e.Mode is "land" or "sea"&&e.TerrainFactorPpm is >=1_000_000 and <=1_500_000&&e.EstimatedRiverCrossings>=0&&e.EstimatedInlandWaterCrossings>=0&&!string.IsNullOrWhiteSpace(e.Basis),"地形係数・経路の根拠");
            var expected=checked((long)Math.Round(baseLength*(double)e.TerrainFactorPpm/1_000_000+(e.EstimatedRiverCrossings+e.EstimatedInlandWaterCrossings)*312_500.0,MidpointRounding.ToEven));
            Need(length==expected&&(e.Mode!="sea"||e.TerrainFactorPpm==1_000_000&&e.EstimatedRiverCrossings==0&&e.EstimatedInlandWaterCrossings==0),"移動距離の地形補正");
        }
        Need(c.Edges.Any(e=>e.Mode=="land"&&e.TerrainFactorPpm>1_000_000),"標高による減速がない");
        Need(c.Edges.Any(e=>e.EstimatedRiverCrossings>0),"河川横断の推定コストがない");
        Need(c.Edges.Any(e=>e.EstimatedInlandWaterCrossings>0),"湖・広い川面の推定コストがない");
    }
    public static BranchNode[] Nodes=>CastleRoutes.Nodes;
    public static SiteDefinition[] Sites=>CastleRoutes.Sites;
    public static BranchEdge[] Edges=>[..catalog.Value.Edges.Select(e=>new BranchEdge(e.Id,e.From,e.To,long.Parse(e.LengthMm,System.Globalization.CultureInfo.InvariantCulture),e.FootSpeed,e.CourierSpeed))];
}
