using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;

public sealed record RegionalStop(string Id,string Name,int LatitudeE7,int LongitudeE7,string Revision,string Precision);
public sealed record RegionalCatalog(int Schema,RegionalStop[] Stops,long[] LegLengthsMm,int FootSpeed,int CourierSpeed,string Algorithm,string[] SourceUrls,string Caveat);

public sealed record RegionalBinding(RegionalCatalog Catalog,string Projection);

// Explicit new scenario binding; never migrates a legacy fictional save or asserts historic road geometry.
public static class RegionalRoute
{
    public const string Ruleset="owari-geographic-corridor-v1";
    static readonly byte[] bytes=ReadResource();
    static readonly RegionalCatalog catalog=ContractJson.Read<RegionalCatalog>(bytes);
    public static RegionalCatalog Catalog=>ContractJson.Copy(catalog);
    public static string Version {get;}=ContractJson.CanonicalHash(new RegionalBinding(catalog,JapanGeography.Projection));
    static byte[] ReadResource(){using var stream=typeof(RegionalRoute).Assembly.GetManifestResourceStream("OwariRouteV1")??throw new InvalidDataException("Missing geographic catalog");using var m=new MemoryStream();stream.CopyTo(m);return m.ToArray();}
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static RouteLeg[] Legs=>catalog.Stops.Zip(catalog.Stops.Skip(1),(a,b)=>(a,b)).Select((p,i)=>new RouteLeg("owari-"+i,Point(p.a),Point(p.b),catalog.LegLengthsMm[i],catalog.FootSpeed,catalog.CourierSpeed)).ToArray();
    static RoutePoint Point(RegionalStop p){var xy=JapanGeography.Project(new(p.LatitudeE7,p.LongitudeE7));return new(xy.EastMm,xy.NorthMm);}
    public static SiteDefinition[] Sites=>catalog.Stops.Select((p,i)=>new SiteDefinition(i==0?ContractFixture.Castle:i==2?ContractFixture.Village:new("site:atsuta"),p.Name,p.LatitudeE7,p.LongitudeE7,0)).ToArray();
    public static ContractWorld Create()
    {
        var w=ContractFixture.Create();w.WorldId=new("world:owari-route-v1");
        w.Definitions=w.Definitions with{Id=new("scenario:owari-route-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};
        ContractValidation.Validate(w);return w;
    }
    public static void ValidateSites(ContractWorld w)
    {
        if(catalog.Schema!=1||catalog.Stops.Length!=3||catalog.LegLengthsMm.Length!=2||catalog.LegLengthsMm.Any(l=>l<=0)||catalog.FootSpeed<=0||catalog.CourierSpeed<=0||!w.Definitions.Sites.SequenceEqual(Sites))throw new InvalidDataException("Geographic catalog changed; explicit migration required");
    }
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)=>RouteContract.Order(w,id,action,executor);
}
