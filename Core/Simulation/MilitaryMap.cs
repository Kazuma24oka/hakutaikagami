namespace HakutaiKagami.Core.Simulation;

public readonly record struct MapCell(int X,int Z)
{
    public int Distance(MapCell other)=>Math.Abs(X-other.X)+Math.Abs(Z-other.Z);
}
public enum MilitaryTerrain { Plain, Road, Forest, River, Bridge, Rock }

public static class MilitaryMap
{
    public const string Id="river-crossing-v1";
    public const int Width=11,Height=7,SightRange=3;
    public static bool Contains(MapCell c)=>c.X>=0&&c.X<Width&&c.Z>=0&&c.Z<Height;
    public static MilitaryTerrain Terrain(MapCell c)
    {
        if(!Contains(c))throw new ArgumentOutOfRangeException(nameof(c));
        if(c.X==5)return c.Z==3?MilitaryTerrain.Bridge:MilitaryTerrain.River;
        if((c.X==4||c.X==6)&&(c.Z==1||c.Z==5))return MilitaryTerrain.Rock;
        if(c.Z==3)return MilitaryTerrain.Road;
        if((c.X==3||c.X==7)&&c.Z is 1 or 2 or 4 or 5)return MilitaryTerrain.Forest;
        return MilitaryTerrain.Plain;
    }
    public static bool Walkable(MapCell c)=>Contains(c)&&Terrain(c) is not (MilitaryTerrain.River or MilitaryTerrain.Rock);
    public static int Cost(MapCell c)=>Terrain(c) switch{MilitaryTerrain.Road or MilitaryTerrain.Bridge=>1,MilitaryTerrain.Forest=>3,_=>2};
    public static IEnumerable<MapCell> Neighbours(MapCell c)=>new[]{new MapCell(c.X-1,c.Z),new(c.X,c.Z-1),new(c.X,c.Z+1),new(c.X+1,c.Z)}.Where(Walkable).OrderBy(x=>x.X).ThenBy(x=>x.Z);
    // Stable weighted Dijkstra; no diagonal movement or hidden enemy positions in pathfinding.
    public static MapCell[] Route(MapCell from,MapCell to)
    {
        if(!Walkable(from)||!Walkable(to))throw new ArgumentException("Destination is not walkable.");
        var distance=new Dictionary<MapCell,int>{{from,0}};var previous=new Dictionary<MapCell,MapCell>();var open=new HashSet<MapCell>{from};
        while(open.Count>0)
        {
            var cell=open.OrderBy(c=>distance[c]).ThenBy(c=>c.X).ThenBy(c=>c.Z).First();open.Remove(cell);if(cell==to)break;
            foreach(var next in Neighbours(cell))
            {
                int cost=distance[cell]+Cost(next);if(distance.TryGetValue(next,out var old)&&old<=cost)continue;
                distance[next]=cost;previous[next]=cell;open.Add(next);
            }
        }
        if(!distance.ContainsKey(to))throw new InvalidOperationException("No route to destination.");
        var path=new List<MapCell>();for(var c=to;c!=from;c=previous[c])path.Add(c);path.Reverse();return path.ToArray();
    }
    public static bool CanSee(MapCell from,MapCell to)
    {
        if(!Contains(from)||!Contains(to)||from.Distance(to)>SightRange)return false;
        // Conservative supercover-like sample: rocks and woods obstruct sight beyond their tile.
        int steps=Math.Max(Math.Abs(to.X-from.X),Math.Abs(to.Z-from.Z))*4;
        for(int i=1;i<steps;i++)
        {
            var c=new MapCell((int)Math.Round(from.X+(to.X-from.X)*i/(double)steps),(int)Math.Round(from.Z+(to.Z-from.Z)*i/(double)steps));
            if(c!=from&&c!=to&&Terrain(c) is MilitaryTerrain.Rock or MilitaryTerrain.Forest)return false;
        }
        return true;
    }
    public static bool InContact(MapCell a,MapCell b)=>a.Distance(b)<=2&&Route(a,b).Length<=2;
}
