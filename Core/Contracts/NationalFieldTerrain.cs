using System.Collections.Concurrent;
namespace HakutaiKagami.Core.Contracts;

// 新しい全国布陣の版だけ。既存の試作地形を全国の実在拠点へ配置する。
// 地形・当たり判定・描画は同じ三角形を使う。史実の城郭測量を意味しない。
public static class NationalFieldTerrain
{
    public const string Policy="national-castle-field-ground-v1";
    public static string Version {get;}=ContractJson.CanonicalHash(new[]{Policy,ContractJson.CanonicalHash(CastleRoutes.Nodes),ContinuousWorld.Version});
    const long Extent=ContinuousWorld.Extent;
    sealed record Anchor(string Id,FieldPoint Point,long BaseHeight);
    static readonly Lazy<Anchor[]> anchors=new(()=>CastleRoutes.Nodes.Select(n=>{var p=FieldWorld.FromWorld(n.Position);return new Anchor(n.Id,p,ContinuousWorld.Height(p));}).ToArray());
    static readonly Lazy<ILookup<(long,long),Anchor>> buckets=new(()=>anchors.Value.ToLookup(a=>Cell(a.Point,Extent)));
    static readonly ConcurrentDictionary<(long,long),string> kinds=new();
    static readonly ConcurrentDictionary<FieldPoint,long> heights=new();
    static readonly ConcurrentDictionary<string,SceneryObstacle[]> obstacles=new(StringComparer.Ordinal);
    static (long,long) Cell(FieldPoint p,long size)=>((long)Math.Floor(p.X/(double)size),(long)Math.Floor(p.Y/(double)size));
    static IEnumerable<Anchor> Nearby(FieldPoint p){var c=Cell(p,Extent);for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)foreach(var a in buckets.Value[(c.Item1+x,c.Item2+y)])yield return a;}
    static Anchor? At(FieldPoint p)=>Nearby(p).Where(a=>Math.Abs(p.X-a.Point.X)<Extent&&Math.Abs(p.Y-a.Point.Y)<Extent).OrderBy(a=>RegionalGround.Distance(a.Point,p)).ThenBy(a=>a.Id,StringComparer.Ordinal).FirstOrDefault();
    static FieldPoint Reference(FieldPoint p,Anchor a)=>ContinuousWorld.Point("castle",p.X-a.Point.X,p.Y-a.Point.Y);
    static long Vertex(FieldPoint p){if(heights.Count>=65536)heights.Clear();return heights.GetOrAdd(p,ComputeVertex);}
    static long ComputeVertex(FieldPoint p)
    {
        var a=At(p);if(a==null)return ContinuousWorld.Height(p);
        long x=p.X-a.Point.X,y=p.Y-a.Point.Y;double blend=Math.Clamp((Math.Max(Math.Abs(x),Math.Abs(y))-300000)/100000.0,0,1);
        double local=a.BaseHeight+ContinuousWorld.Height(Reference(p,a))-ContinuousWorld.Height(ContinuousWorld.Origin("castle"));
        return (long)Math.Round(local*(1-blend)+ContinuousWorld.Height(p)*blend);
    }
    public static long Height(FieldPoint p){var c=Cell(p,ContinuousWorld.Cell);long x=c.Item1*ContinuousWorld.Cell,y=c.Item2*ContinuousWorld.Cell,fx=p.X-x,fy=p.Y-y;long a=Vertex(new(x,y)),b=Vertex(new(x+ContinuousWorld.Cell,y)),cc=Vertex(new(x+ContinuousWorld.Cell,y+ContinuousWorld.Cell)),d=Vertex(new(x,y+ContinuousWorld.Cell));return fx>=fy?a+((b-a)*fx+(cc-b)*fy)/ContinuousWorld.Cell:a+((cc-d)*fx+(d-a)*fy)/ContinuousWorld.Cell;}
    public static string KindAt(FieldPoint p){var c=Cell(p,ContinuousWorld.Cell);if(kinds.Count>=65536)kinds.Clear();return kinds.GetOrAdd(c,c=>{var q=new FieldPoint(c.Item1*ContinuousWorld.Cell+ContinuousWorld.Cell/2,c.Item2*ContinuousWorld.Cell+ContinuousWorld.Cell/2);var a=At(q);return ContinuousWorld.KindAt(a==null?q:Reference(q,a));});}
    static SceneryObstacle[] Objects(Anchor a)=>obstacles.GetOrAdd(a.Id,_=>ContinuousWorld.Obstacles("castle").Select(o=>{var origin=ContinuousWorld.Origin("castle");FieldPoint Move(FieldPoint p)=>new(p.X-origin.X+a.Point.X,p.Y-origin.Y+a.Point.Y);return o with{Id=a.Id+o.Id["castle".Length..],Min=Move(o.Min),Max=Move(o.Max)};}).Where(o=>!Nearby(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2)).Any(a=>{long dx=a.Point.X-Math.Clamp(a.Point.X,o.Min.X,o.Max.X),dy=a.Point.Y-Math.Clamp(a.Point.Y,o.Min.Y,o.Max.Y);return dx*dx+dy*dy<20000L*20000;})).ToArray());
    public static SceneryObstacle[] ObstaclesNear(FieldPoint p)=>Nearby(p).SelectMany(Objects).ToArray();
    public static bool Walkable(FieldPoint p)
    {
        if(FieldWorld.Speed(KindAt(p))<=0)return false;
        foreach(var o in ObstaclesNear(p)){long dx=p.X-Math.Clamp(p.X,o.Min.X,o.Max.X),dy=p.Y-Math.Clamp(p.Y,o.Min.Y,o.Max.Y);if(dx*dx+dy*dy<1000000)return false;}
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(FieldWorld.Speed(KindAt(new(p.X+x*1000,p.Y+y*1000)))<=0)return false;
        return true;
    }
    public static SceneryTriangle[] Surfaces(TerrainTile t)
    {
        var rows=new List<SceneryTriangle>();var origin=ContinuousWorld.Origin(t.Node);
        FieldPoint P(long x,long y)=>new(origin.X+x*ContinuousWorld.Cell,origin.Y+y*ContinuousWorld.Cell);
        SceneryVertex V(long x,long y){var p=P(x,y);return new(p,Height(p));}
        for(long x=t.X*8;x<(t.X+1)*8;x++)for(long y=t.Y*8;y<(t.Y+1)*8;y++){var a=V(x,y);var b=V(x+1,y);var c=V(x+1,y+1);var d=V(x,y+1);var kind=KindAt(new(a.Point.X+5000,a.Point.Y+5000));rows.Add(new(a,b,c,kind));rows.Add(new(a,c,d,kind));}
        return rows.ToArray();
    }
}
public static class WorldGround
{
    public static bool National(ContractWorld w)=>FieldWorld.National(w);
    public static long Height(ContractWorld w,FieldPoint p)=>National(w)?NationalFieldTerrain.Height(p):ContinuousWorld.Height(p);
    public static string KindAt(ContractWorld w,FieldPoint p)=>National(w)?NationalFieldTerrain.KindAt(p):ContinuousWorld.KindAt(p);
    public static bool Walkable(ContractWorld w,FieldPoint p)=>National(w)?NationalFieldTerrain.Walkable(p):ContinuousWorld.Walkable(p);
}
