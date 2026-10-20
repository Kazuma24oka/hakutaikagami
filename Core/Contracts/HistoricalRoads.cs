using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;
public sealed record HistoricalSource(string Id,string Url,string Role,int? PublishedYear,string? SnapshotSha256,string Access);
public sealed record HistoricalRoadBinding(string Edge,string DistanceBasis,string[] SourceIds,bool HistoricalAdoption,string Caveat);
public sealed record HistoricalRoadCatalog(int Schema,int WorldYear,HistoricalSource[] Sources,long RemnantLengthMm,int LocatorLatitudeE7,int LocatorLongitudeE7,long PreviewEndEastOffsetMm,long PreviewEndNorthOffsetMm,string Caveat,HistoricalRoadBinding[] Bindings);
public sealed record HistoricalFeature(string Kind,int? BuiltYear,int? RemovedYear,bool YearAttested,bool AlignmentVerified,string DistanceBasis,long LengthMm,string[] SourceIds);
// Evidence date is independent of the date described. Unknown era/alignment never becomes historical fact.
public static class HistoricalRoads
{
    public const string Ruleset="owari-historical-road-preview-v1";
    static readonly HistoricalRoadCatalog catalog=Read();
    public static HistoricalRoadCatalog Catalog=>ContractJson.Copy(catalog);
    static HistoricalRoadCatalog Read(){using var s=typeof(HistoricalRoads).Assembly.GetManifestResourceStream("HistoricalRoadsV1")??throw new InvalidDataException("Missing history catalog");using var m=new MemoryStream();s.CopyTo(m);var c=ContractJson.Read<HistoricalRoadCatalog>(m.ToArray());if(c.Schema!=1||c.WorldYear!=1560||c.RemnantLengthMm<=0||c.Sources.Select(x=>x.Id).Distinct().Count()!=c.Sources.Length)throw new InvalidDataException("Invalid history evidence");return c;}
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{RiverRoads.Version,ContractJson.CanonicalHash(catalog),"published-approx-preview-only-v1"});
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static HistoricalFeature Remnant=>new("road",null,null,false,false,"published-approximate",catalog.RemnantLengthMm,["bunka-futamura","toyoake-length","toyoake-history"]);
    public static bool CanAdoptAsHistorical(HistoricalFeature f,int year)=>f.Kind is "road" or "bridge" or "ford"&&f.BuiltYear is {} built&&built<=year&&(f.RemovedYear==null||f.RemovedYear>year)&&f.YearAttested&&f.AlignmentVerified&&f.LengthMm>0&&f.DistanceBasis is "surveyed-historical" or "published-approximate"&&f.SourceIds.Length>0&&f.SourceIds.All(id=>catalog.Sources.Any(s=>s.Id==id&&s.SnapshotSha256!=null));
    public static BranchNode[] Nodes{get{var p=JapanGeography.Project(new(catalog.LocatorLatitudeE7,catalog.LocatorLongitudeE7));return [..RiverRoads.Nodes,new("futamura-upper",new(p.EastMm,p.NorthMm)),new("futamura-lower",new(p.EastMm+catalog.PreviewEndEastOffsetMm,p.NorthMm+catalog.PreviewEndNorthOffsetMm))];}}
    public static BranchEdge[] Edges{get{var a=Nodes.Single(n=>n.Id=="village").Position;var b=Nodes.Single(n=>n.Id=="futamura-upper").Position;var dx=(double)(a.EastMm-b.EastMm);var dn=(double)(a.NorthMm-b.NorthMm);return [..RiverRoads.Edges,new("futamura-access-preview","village","futamura-upper",(long)Math.Ceiling(Math.Sqrt(dx*dx+dn*dn)),1000,2000),new("futamura-remnant","futamura-upper","futamura-lower",catalog.RemnantLengthMm,1000,2000)];}}
    public static SiteDefinition[] Sites=>[..RiverRoads.Sites,..Nodes.Where(n=>n.Id.StartsWith("futamura-",StringComparison.Ordinal)).Select(n=>{var p=JapanGeography.Inverse(new(n.Position.EastMm,n.Position.NorthMm));return new SiteDefinition(new("site:"+n.Id),n.Id=="futamura-upper"?"二村山・代表点（端点未確定）":"旧道区間終端（表示用仮点）",p.LatitudeE7,p.LongitudeE7,0);})];
    public static string DistanceBasis(string edge)=>catalog.Bindings.Single(b=>b.Edge==edge).DistanceBasis;
    static void ValidateBindings(){if(!catalog.Bindings.Select(b=>b.Edge).Order(StringComparer.Ordinal).SequenceEqual(Edges.Select(e=>e.Id).Order(StringComparer.Ordinal))||catalog.Bindings.Any(b=>b.HistoricalAdoption||b.SourceIds.Any(id=>!catalog.Sources.Any(s=>s.Id==id))))throw new InvalidDataException("Preview provenance coverage/adoption changed");if(catalog.Bindings.Single(b=>b.Edge=="futamura-remnant").DistanceBasis!="published-approximate-preview")throw new InvalidDataException("Unverified length cannot be promoted");}
    public static ContractWorld Create(){ValidateBindings();var w=RiverRoads.Create(false);w.WorldId=new("world:historical-road-preview-v1");w.Definitions=w.Definitions with{Id=new("scenario:historical-road-preview-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};ContractValidation.Validate(w);return w;}
    public static Intent Order(ContractWorld w,string id)=>RegionalCampaign.Move(w,id,"futamura-lower");
}
