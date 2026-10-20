namespace HakutaiKagami.Core.Contracts;
// New rules, existing geographic graph and transport. A rendezvous is not live pursuit.
public static class RegionalControl
{
    public const string Ruleset="owari-movement-control-v1";
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{RegionalBranch.Version,"atsuta-rendezvous-courier-v1","read-effective-stop-v1","redirect-next-junction-v1"});
    public static ContractWorld Create(){var w=RegionalBranch.Create();w.WorldId=new("world:owari-control-v1");w.Definitions=w.Definitions with{Id=new("scenario:owari-control-v1"),Ruleset=Ruleset,Version=Version};w.Grants=w.Grants.Select(g=>g.Actions.Contains(ActionKind.Cancel)?g with{Actions=[..g.Actions,ActionKind.Redirect]}:g).ToArray();ContractValidation.Validate(w);return w;}
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static int CourierToFork(ContractWorld w)=>checked((int)BranchRoutes.Duration(["road"],true,w));
    public static Intent Order(ContractWorld w,string id,EntityId target,ActionKind kind,long effectiveAt=11660,bool important=true)=>MovementControl.Order(w,id,target,kind,effectiveAt,important);
}
