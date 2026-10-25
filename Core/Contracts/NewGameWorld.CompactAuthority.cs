namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameWorld
{
    public const string CompactPersonalAuthority="explicit-personal-all-authored-inspection-sites-v1";
    static EntityId CompactInspectId(EntityId actor)=>new("grant:new-game/inspect-authored-sites/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(actor.Value)));
    static Grant[] CompactPersonalGrants(NewGameState data)=>data.Start.People.SelectMany(p=>new[]{
        new Grant(new("grant:new-game/move/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id))),new(p.Id),new(p.Id),ContractFixture.Castle,[ActionKind.Move],Basis,0,long.MaxValue,0,[],false,0),
        new Grant(CompactInspectId(new(p.Id)),new(p.Id),new(p.Id),ContractFixture.Castle,[ActionKind.Inspect],Basis,0,long.MaxValue,0,[],false,0)
    }).ToArray();
    // Alternate encoding of the same fixed authored-site inspection set. It is
    // never a wildcard: only this versioned world and exact personal grant ID,
    // owner, basis, action and a site in its saved definition qualify. Existing
    // amount/time/adult checks remain in WorldSession.Permits.
    internal static bool CompactPersonalScope(ContractWorld w,Grant grant,EntityId actor,Intent intent)=>
        Enabled(w)&&w.NewGame?.PersonalAuthorityLayout==CompactPersonalAuthority&&intent.Action==ActionKind.Inspect&&
        grant.Id==CompactInspectId(actor)&&grant.Principal==actor&&grant.Agent==actor&&grant.Basis==Basis&&
        grant.Scope==ContractFixture.Castle&&grant.Actions is [ActionKind.Inspect]&&
        w.Definitions.Sites.Any(s=>s.Id==intent.Scope);
}
