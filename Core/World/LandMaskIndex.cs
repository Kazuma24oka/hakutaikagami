namespace HakutaiKagami.Core.World;

// Immutable latitude-strip index. Every candidate uses the original ray predicate.
public sealed class LandMaskIndex
{
    const int Strip=250000;
    static int Bucket(int latitude)=>(int)Math.Floor(latitude/(double)Strip);
    readonly Dictionary<int,Polygon[]> latitudePolygons;
    public LandMaskIndex(IEnumerable<GeoPolygon> source)
    {
        var entries=new Dictionary<int,List<Polygon>>();
        foreach(var p in source){var polygon=new Polygon(new Ring(p.Outer),p.Holes.Select(h=>new Ring(h)).ToArray());for(int key=Bucket(polygon.Outer.Minimum);key<=Bucket(polygon.Outer.Maximum);key++){if(!entries.TryGetValue(key,out var list)){list=new();entries[key]=list;}list.Add(polygon);}}
        latitudePolygons=entries.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
    }
    public bool Contains(GeoCoordinate point)
    {
        int latitude=point.LatitudeE7,longitude=point.LongitudeE7;if(!latitudePolygons.TryGetValue(Bucket(latitude),out var candidates))return false;
        foreach(var p in candidates){if(!p.Outer.Contains(latitude,longitude))continue;bool hole=false;foreach(var h in p.Holes)if(h.Contains(latitude,longitude)){hole=true;break;}if(!hole)return true;}return false;
    }
    sealed record Polygon(Ring Outer,Ring[] Holes);
    readonly struct Edge
    {
        public readonly int ALat,ALong,BLat,BLong;
        public Edge(GeoCoordinate a,GeoCoordinate b){ALat=a.LatitudeE7;ALong=a.LongitudeE7;BLat=b.LatitudeE7;BLong=b.LongitudeE7;}
    }
    sealed class Ring
    {
        readonly int minimum,maximum;readonly Dictionary<int,Edge[]> strips;
        public int Minimum=>minimum;public int Maximum=>maximum;
        public Ring(GeoCoordinate[] points)
        {
            if(points.Length<3)throw new ArgumentException("Ring needs at least three points");
            minimum=points.Min(p=>p.LatitudeE7);maximum=points.Max(p=>p.LatitudeE7);var entries=new Dictionary<int,List<Edge>>();
            for(int i=0,j=points.Length-1;i<points.Length;j=i++){
                var a=points[i];var b=points[j];if(a.LatitudeE7==b.LatitudeE7)continue;
                var edge=new Edge(a,b);for(int key=Bucket(Math.Min(a.LatitudeE7,b.LatitudeE7));key<=Bucket(Math.Max(a.LatitudeE7,b.LatitudeE7));key++){
                    if(!entries.TryGetValue(key,out var list)){list=new();entries[key]=list;}list.Add(edge);
                }
            }
            strips=entries.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        }
        public bool Contains(int latitude,int longitude)
        {
            if(latitude<minimum||latitude>maximum||!strips.TryGetValue(Bucket(latitude),out var candidates))return false;
            bool inside=false;foreach(var edge in candidates){
                if((edge.ALat>latitude)!=(edge.BLat>latitude)&&longitude<(double)(edge.BLong-edge.ALong)*(latitude-edge.ALat)/(edge.BLat-edge.ALat)+edge.ALong)inside=!inside;
            }return inside;
        }
    }
}
