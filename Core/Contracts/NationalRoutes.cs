using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;
public sealed record NationalNode(string Id,string Name,int LatitudeE7,int LongitudeE7,string Region,string Kind,string Source,string CoordinateBasis,string Certainty);
public sealed record NationalLink(string Id,string From,string To,long LengthMm,int FootSpeed,int CourierSpeed,string Mode,string Corridor,string Basis,string HistoricalCertainty,string[] SourceIds);
public sealed record NationalSource(string Id,string Url,string Role);
public sealed record NationalCatalog(int Schema,int WorldYear,string Policy,NationalNode[] Nodes,NationalLink[] Edges,NationalSource[] Sources,string Caveat);
// User-authorized playable inference is separate from surveyed historical adoption. Old definitions stay immutable.
public static class NationalRoutes
{
    public const string Ruleset="national-inferred-routes-v1";
    static readonly NationalCatalog catalog=Read();
    public static NationalCatalog Catalog=>ContractJson.Copy(catalog);
    static NationalCatalog Read(){using var s=typeof(NationalRoutes).Assembly.GetManifestResourceStream("NationalRoutesV1")??throw new InvalidDataException("Missing national network");using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<NationalCatalog>(m.ToArray());}
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{RegionalCampaign.Version,ContractJson.CanonicalHash(catalog),"user-approved-playable-inference-v1"});
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static BranchNode[] Nodes=>[..RegionalCampaign.Nodes,..catalog.Nodes.Select(n=>{var p=JapanGeography.Project(new(n.LatitudeE7,n.LongitudeE7));return new BranchNode(n.Id,new(p.EastMm,p.NorthMm));})];
    public static BranchEdge[] Edges=>[..RegionalCampaign.Edges,..catalog.Edges.Select(e=>new BranchEdge(e.Id,e.From,e.To,e.LengthMm,e.FootSpeed,e.CourierSpeed))];
    public static SiteDefinition[] Sites=>[..RegionalCampaign.Sites,..catalog.Nodes.Select(n=>new SiteDefinition(new("site:"+n.Id),n.Name,n.LatitudeE7,n.LongitudeE7,0))];
    public static bool Sea(string edge)=>catalog.Edges.Any(e=>e.Id==edge&&e.Mode=="sea");
    public static string Name(string edge)=>catalog.Edges.FirstOrDefault(e=>e.Id==edge)?.Corridor??"尾張周辺（概略）";
    static void ValidateCatalog(){var ns=Nodes;var es=Edges;var ids=ns.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);if(catalog.Schema!=1||catalog.WorldYear!=1560||ids.Count!=ns.Length||es.Select(e=>e.Id).Distinct().Count()!=es.Length||es.Any(e=>!ids.Contains(e.From)||!ids.Contains(e.To)||e.LengthMm<=0||e.FootSpeed<=0||e.CourierSpeed<=0)||catalog.Edges.Any(e=>e.Mode is not("land" or "sea")||e.SourceIds.Length==0||e.SourceIds.Any(id=>!catalog.Sources.Any(s=>s.Id==id))))throw new InvalidDataException("Invalid national graph/provenance");var reached=new HashSet<string>{"castle"};bool changed;do{changed=false;foreach(var e in es)if(reached.Contains(e.From)||reached.Contains(e.To)){changed|=reached.Add(e.From);changed|=reached.Add(e.To);}}while(changed);if(reached.Count!=ns.Length)throw new InvalidDataException("Disconnected national network");}
    public static ContractWorld Create(){ValidateCatalog();var w=RegionalCampaign.Create(initiallyBlocked:false);w.WorldId=new("world:national-inferred-routes-v1");w.Definitions=w.Definitions with{Id=new("scenario:national-inferred-routes-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};ContractValidation.Validate(w);return w;}
    public static Intent Move(ContractWorld w,string id,string destination,EntityId? executor=null){var i=RegionalCampaign.Move(w,id,destination,executor);var latest=checked(w.ElapsedSeconds+100000000);return i with{Deadline=new(DeadlineCriterion.Completion,latest,false,null,latest)};}
    public static Intent Order(ContractWorld w,string id)=>Move(w,id,"shuri");
}
