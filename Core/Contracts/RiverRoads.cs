using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;
// Separate definition pack: never reinterpret a campaign save with these inferred distances.
public static class RiverRoads
{
    public const string Ruleset="owari-river-roads-v1";
    static readonly RegionalBranchCatalog catalog=Read();
    public static RegionalBranchCatalog Catalog=>ContractJson.Copy(catalog);
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{RegionalCampaign.Version,ContractJson.CanonicalHash(catalog),"river-corridor-fixture-v1"});
    static RegionalBranchCatalog Read(){using var s=typeof(RiverRoads).Assembly.GetManifestResourceStream("RiverRoadsV1")??throw new InvalidDataException("Missing river catalog");using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<RegionalBranchCatalog>(m.ToArray());}
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static BranchNode[] Nodes=>[..RegionalCampaign.Nodes,..catalog.Nodes.Select(n=>{var p=JapanGeography.Project(new(n.Stop.LatitudeE7,n.Stop.LongitudeE7));return new BranchNode(n.Id,new(p.EastMm,p.NorthMm));})];
    public static BranchEdge[] Edges=>[..RegionalCampaign.Edges.Where(e=>e.Id!="mikawa-concept"),..catalog.Edges];
    public static SiteDefinition[] Sites=>[..RegionalCampaign.Sites,..catalog.Nodes.Select(n=>new SiteDefinition(new("site:"+n.Id),n.Stop.Name,n.Stop.LatitudeE7,n.Stop.LongitudeE7,0))];
    public static ContractWorld Create(bool bridgeBlocked=true)
    {
        var w=RegionalCampaign.Create(initiallyBlocked:false);w.WorldId=new("world:river-roads-v1");w.Definitions=w.Definitions with{Id=new("scenario:river-roads-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};
        w.Branches=w.Branches! with{BlockedEdges=bridgeBlocked?["nakajima-crossing"]:[]};w.Couriers=w.Couriers! with{InitialBlocked=w.Branches.BlockedEdges};ContractValidation.Validate(w);return w;
    }
    public static Intent Order(ContractWorld w,string id)=>RegionalCampaign.Move(w,id,"okazaki");
}
