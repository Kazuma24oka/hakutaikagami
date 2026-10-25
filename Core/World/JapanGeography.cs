using HakutaiKagami.Core.Contracts;
namespace HakutaiKagami.Core.World;

public sealed record GeoCoordinate(int LatitudeE7,int LongitudeE7);
public sealed record GeoMillimeters(long EastMm,long NorthMm);
public sealed record GeoPolygon(GeoCoordinate[] Outer,GeoCoordinate[][] Holes);
public sealed record GeoLandmark(string Id,string Name,GeoCoordinate Coordinate,string SourceUrl,string SourceRevision,string CoordinatePrecision,string HistoricalStatus);
public sealed record GeoSource(string Name,string Url,string License,string Revision,string Sha256);
public sealed record JapanCatalog(int Schema,string Projection,GeoPolygon[] Polygons,GeoLandmark[] Landmarks,GeoSource[] Sources,string[] Notices);

// Presentation geography only. Never converts legacy MapCell/Tick or assigns simulation entities.
public static class JapanGeography
{
    public const string Projection="display-equirectangular-36N-138E-R6371000-v1";
    const double R=6371000,Rad=Math.PI/180;
    public static GeoMillimeters Project(GeoCoordinate p)
    {
        ValidatePoint(p);return new(checked((long)Math.Round((p.LongitudeE7/1e7-138)*Rad*R*Math.Cos(36*Rad)*1000,MidpointRounding.AwayFromZero)),
            checked((long)Math.Round((p.LatitudeE7/1e7-36)*Rad*R*1000,MidpointRounding.AwayFromZero)));
    }
    public static GeoCoordinate Inverse(GeoMillimeters p)=>new(checked((int)Math.Round((36+p.NorthMm/1000.0/R/Rad)*1e7,MidpointRounding.AwayFromZero)),
        checked((int)Math.Round((138+p.EastMm/1000.0/R/Math.Cos(36*Rad)/Rad)*1e7,MidpointRounding.AwayFromZero)));
    public static (double EastMeters,double NorthMeters) Relative(GeoMillimeters point,GeoMillimeters origin)=>
        (checked(point.EastMm-origin.EastMm)/1000.0,checked(point.NorthMm-origin.NorthMm)/1000.0);
    static void ValidatePoint(GeoCoordinate p){if(p.LatitudeE7 is < -900000000 or > 900000000||p.LongitudeE7 is < -1800000000 or > 1800000000)throw new InvalidDataException("Invalid geographic coordinate");}
    public static JapanCatalog Read(byte[] bytes)
    {
        var c=ContractJson.Read<JapanCatalog>(bytes);
        if(c.Schema!=1||c.Projection!=Projection||c.Polygons.Length==0||c.Landmarks.Length==0||c.Sources.Length<2||c.Notices.Length<3)throw new InvalidDataException("Unsupported geography catalog");
        if(c.Landmarks.Select(l=>l.Id).Distinct().Count()!=c.Landmarks.Length)throw new InvalidDataException("Duplicate landmark");
        foreach(var poly in c.Polygons)foreach(var ring in new[]{poly.Outer}.Concat(poly.Holes)){if(ring.Length<3||ring[0]==ring[^1])throw new InvalidDataException("Invalid polygon ring");foreach(var p in ring)ValidatePoint(p);}
        foreach(var l in c.Landmarks){ValidatePoint(l.Coordinate);if(!l.Id.StartsWith("geo:Q",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(l.Name)||!long.TryParse(l.Id[5..],out var q)||q<=0||l.SourceUrl!="https://www.wikidata.org/wiki/"+l.Id[4..]||!long.TryParse(l.SourceRevision,out var revision)||revision<=0||!l.HistoricalStatus.StartsWith("DisplayCandidateOnly",StringComparison.Ordinal))throw new InvalidDataException("Unqualified geographic candidate");}
        if(c.Sources.Any(s=>!ContractValidation.IsHash(s.Sha256)||!Uri.TryCreate(s.Url,UriKind.Absolute,out _)||string.IsNullOrWhiteSpace(s.License)))throw new InvalidDataException("Missing source provenance");
        return c;
    }
}
