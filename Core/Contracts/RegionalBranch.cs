using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;
public sealed record RegionalBranchNode(string Id,RegionalStop Stop);
public sealed record RegionalBranchCatalog(int Schema,RegionalBranchNode[] Nodes,BranchEdge[] Edges,string Algorithm,string[] Sources,string Credit,string Caveat);
public sealed record RegionalBranchBinding(RegionalBranchCatalog Catalog,string Projection);
public static class RegionalBranch
{
    public const string Ruleset="owari-geographic-branches-v1";
    static readonly RegionalBranchCatalog catalog=Read();
    public static RegionalBranchCatalog Catalog=>ContractJson.Copy(catalog);
    public static string Version{get;}=ContractJson.CanonicalHash(new RegionalBranchBinding(catalog,JapanGeography.Projection));
    static RegionalBranchCatalog Read(){using var s=typeof(RegionalBranch).Assembly.GetManifestResourceStream("OwariBranchesV1")??throw new InvalidDataException("Missing branch catalog");using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<RegionalBranchCatalog>(m.ToArray());}
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||w.Definitions.Ruleset==RegionalControl.Ruleset||RegionalCampaign.Enabled(w);
    public static BranchNode[] Nodes=>catalog.Nodes.Select(n=>{var p=JapanGeography.Project(new(n.Stop.LatitudeE7,n.Stop.LongitudeE7));return new BranchNode(n.Id,new(p.EastMm,p.NorthMm));}).ToArray();
    public static BranchEdge[] Edges=>catalog.Edges.Select(e=>e with{}).ToArray();
    public static SiteDefinition[] Sites=>catalog.Nodes.Select(n=>new SiteDefinition(n.Id=="castle"?ContractFixture.Castle:n.Id=="village"?ContractFixture.Village:new("site:"+n.Id),n.Stop.Name,n.Stop.LatitudeE7,n.Stop.LongitudeE7,0)).ToArray();
    public static ContractWorld Create(bool blocked=true)
    {
        var w=ContractFixture.Create();w.Schema=2;w.WorldId=new("world:owari-branches-v1");w.Definitions=w.Definitions with{Id=new("scenario:owari-branches-v1"),Ruleset=Ruleset,Version=Version,Sites=Sites};w.Branches=new(blocked?["pass"]:[],[]);ContractValidation.Validate(w);return w;
    }
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)
    {var i=ContractFixture.Order(id,action,action==ActionKind.Transfer?40:0,executor:executor,deadline:1000000);return i with{DeliverySeconds=1,WorkSeconds=action==ActionKind.Move?checked((int)BranchRoutes.Duration(BranchRoutes.Plan("castle","village",BranchRoutes.KnownBlocked(w,i.Executor),w:w),w:w)):4,ReportSeconds=1};}
    public static void ValidateOrder(ContractWorld w,Intent i)
    {if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.Scope!=ContractFixture.Castle||i.DeliverySeconds!=1||i.ReportSeconds!=1||w.Locations.Single(l=>l.Actor==i.Executor).Site!=ContractFixture.Castle||i.Action==ActionKind.Move&&i.DestinationSite!=ContractFixture.Village||i.Action is not(ActionKind.Move or ActionKind.Transfer or ActionKind.Inspect)||i.WorkSeconds!=(i.Action==ActionKind.Move?BranchRoutes.Duration(BranchRoutes.Plan("castle","village",BranchRoutes.KnownBlocked(w,i.Executor),w:w),w:w):4))throw new InvalidDataException("Unsupported regional branch order");}
    public static void ValidateSites(ContractWorld w){if(catalog.Schema!=1||!w.Definitions.Sites.SequenceEqual(RiverRoads.Enabled(w)?RiverRoads.Sites:RegionalCampaign.Enabled(w)?RegionalCampaign.Sites:Sites))throw new InvalidDataException("Regional graph anchors changed; explicit migration required");}
}
