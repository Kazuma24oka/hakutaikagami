namespace HakutaiKagami.Core.Contracts;

public sealed record SceneryObstacle(string Id,string Kind,FieldPoint Min,FieldPoint Max,long HeightMm);
public sealed record SceneryVertex(FieldPoint Point,long HeightMm);
public sealed record SceneryTriangle(SceneryVertex A,SceneryVertex B,SceneryVertex C,string Kind);

// Synthetic landscape anchored at Kiyosu; authoritative geometry, not an artistic navigation approximation.
public static class RegionalScenery
{
    public const string Ruleset="regional-scenery-v1";
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    static readonly SceneryObstacle[] obstacles=BuildObstacles();
    public static SceneryObstacle[] Obstacles=>obstacles.ToArray();
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{FieldWorld.Version,
        ContractJson.CanonicalHash(Enumerable.Range(-6,17).SelectMany(x=>Enumerable.Range(-6,17).Select(y=>$"{x}/{y}/{VertexHeight(x,y)}/{Kind(x,y)}")).ToArray()),
        ContractJson.CanonicalHash(obstacles),ContractJson.CanonicalHash(Waypoints),"shared-triangular-ground-and-static-footprints-v1"});
    public static ContractWorld Create(int count=3,bool refuses=false,bool lowMorale=false)
    {
        var w=RegionalBattle.Create(count,refuses,lowMorale);
        w.Region=w.Region! with{Units=w.Region.Units.Select((u,i)=>{FieldPoint p=u.Home;if(u.Commander==RegionalBattle.Enemy){int n=i-count*2;p=count==3?new(-15000-n*10000,75000):new(-45000+(n%3)*10000,65000+(n/3)*10000);}else if(count==12&&u.Commander==ContractFixture.Officer){p=new(-15000+(i%3)*10000,-45000+(i/3)*10000);}return u with{Home=p,Position=p,RouteOrigin=p,Goal=p};}).ToArray(),Sights=[]};
        w.WorldId=new("world:regional-scenery-v1");
        w.Definitions=w.Definitions with{Id=new("scenario:regional-scenery-v1"),Ruleset=Ruleset};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};
        RegionalBattle.Sync(w);ContractValidation.Validate(w);return w;
    }
    public static long VertexHeight(int x,int y)
    {
        // Flat west valley, eastern foothill and a faceted43m northern ridge.
        long hill=x<=1?0:Math.Max(0,35000-Math.Abs(x-5)*6000-Math.Abs(y-7)*5000);
        long ground=Math.Clamp(x-1,0,7)*2000L+hill;
        // The tributary and its crossing have a shared level stream bed/deck datum.
        if(y is 5 or 6 && x<=4)return 0;
        if(x>=8)return 0;
        return ground;
    }
    public static string Kind(int x,int y)
    {
        var kind=FieldWorld.Terrain(x,y);if(kind is "water" or "outside" or "cliff")return kind;
        long[] h=[VertexHeight(x,y),VertexHeight(x+1,y),VertexHeight(x+1,y+1),VertexHeight(x,y+1)];
        if(h.Max()-h.Min()>8000)return "cliff";
        return kind;
    }
    public static string KindAt(FieldPoint p)=>Kind((int)Math.Floor(p.X/10000.0),(int)Math.Floor(p.Y/10000.0));
    public static long Height(FieldPoint p)
    {
        int x=(int)Math.Floor(p.X/10000.0),y=(int)Math.Floor(p.Y/10000.0);
        long fx=p.X-x*10000L,fy=p.Y-y*10000L;
        var a=VertexHeight(x,y);var b=VertexHeight(x+1,y);var c=VertexHeight(x+1,y+1);var d=VertexHeight(x,y+1);
        return fx>=fy?a+((b-a)*fx+(c-b)*fy)/10000:a+((c-d)*fx+(d-a)*fy)/10000;
    }
    public static SceneryTriangle[] Surfaces()
    {
        var list=new List<SceneryTriangle>();
        for(int x=-6;x<10;x++)for(int y=-6;y<10;y++)
        {
            SceneryVertex V(int ix,int iy)=>new(new(ix*10000L,iy*10000L),VertexHeight(ix,iy));
            var a=V(x,y);var b=V(x+1,y);var c=V(x+1,y+1);var d=V(x,y+1);var kind=Kind(x,y);
            list.Add(new(a,b,c,kind));list.Add(new(a,c,d,kind));
        }
        return list.ToArray();
    }
    public static FieldPoint[] Waypoints=>[new(-42000,-19000),new(-30000,-19000),new(-30000,-31000),new(-42000,-31000),new(-27000,-25000),new(-30000,-25000)];
    public static FieldPoint[] SurfaceCuts(FieldPoint a,FieldPoint b)
    {
        var cuts=new SortedSet<decimal>{0,1};
        void Add(long start,long end){if(start==end)return;for(long k=(long)Math.Floor(Math.Min(start,end)/10000.0);k<=(long)Math.Ceiling(Math.Max(start,end)/10000.0);k++){decimal t=(decimal)(k*10000-start)/(end-start);if(t>0&&t<1)cuts.Add(t);}}
        Add(a.X,b.X);Add(a.Y,b.Y);Add(a.X-a.Y,b.X-b.Y);
        return cuts.Select(t=>new FieldPoint(a.X+(long)((b.X-a.X)*t),a.Y+(long)((b.Y-a.Y)*t))).Distinct().ToArray();
    }
    static SceneryObstacle[] BuildObstacles()
    {
        var list=new List<SceneryObstacle>();
        void Rect(string id,string kind,long x1,long y1,long x2,long y2,long h)=>list.Add(new(id,kind,new(x1,y1),new(x2,y2),h));
        Rect("fort:keep","keep",-41000,-30000,-32000,-22000,8000);
        Rect("fort:west","wall",-46000,-36000,-44000,-14000,3000);
        Rect("fort:north","wall",-44000,-16000,-26000,-14000,3000);
        Rect("fort:south","wall",-44000,-36000,-26000,-34000,3000);
        Rect("fort:east-a","wall",-28000,-34000,-26000,-28000,3000);
        Rect("fort:east-b","wall",-28000,-22000,-26000,-16000,3000);
        Rect("bridge:west","rail",-250,50000,250,60000,1400);
        Rect("bridge:east","rail",9750,50000,10250,60000,1400);
        for(int x=2;x<=4;x++)for(int y=1;y<=4;y++)if(Kind(x,y)=="forest")
        for(int i=0;i<4;i++)
        {
            long px=x*10000L+(i%2==0?2500:7500),py=y*10000L+(i<2?2500:7500);
            Rect($"tree:{x}/{y}/{i}","tree",px-350,py-350,px+350,py+350,10000+(i%2)*2000);
        }
        return list.ToArray();
    }
    public static bool ClearFootprint(FieldPoint p,long radius)
    {
        foreach(var o in obstacles)
        {
            long dx=p.X-Math.Clamp(p.X,o.Min.X,o.Max.X),dy=p.Y-Math.Clamp(p.Y,o.Min.Y,o.Max.Y);
            if(dx*dx+dy*dy<radius*radius)return false;
        }
        return true;
    }
}
