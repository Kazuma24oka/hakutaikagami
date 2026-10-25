namespace HakutaiKagami.Core.Contracts;

// Garrison identity is a property of its owner and site, never of the current player,
// the current service agreement, or a renewed military case.
public sealed record MilitaryLedgerIdentity(string Contract, EntityId Owner, EntityId Site,
    EntityId Ledger, EntityId Army, EntityId Depot, EntityId Carry, EntityId[] Units);

public static partial class NewGameMilitary
{
    public const string LedgerIdentityContract="owner-site-garrison-stable-ledger-identities-v1";
    public static MilitaryLedgerIdentity LedgerIdentity(EntityId owner,EntityId site)
    {
        Need(EntityId.IsValid(owner.Value)&&EntityId.IsValid(site.Value),"軍台帳の所有者と拠点IDが不正です");
        // Structured JSON prevents ambiguous owner/site delimiter concatenation.
        var key=Hash(Text(new[]{owner.Value,site.Value}));
        return new(LedgerIdentityContract,owner,site,new("ledger:military/"+key),
            new("army:military/"+key),new("lot:military/"+key+"/depot"),
            new("lot:military/"+key+"/carry"),Enumerable.Range(0,3).Select(i=>new EntityId("unit:military/"+key+"/"+i)).ToArray());
    }
    // A legacy save keeps its actual IDs. This descriptor never migrates or resets it.
    // The future multi-ledger adapter must retain this binding rather than regenerate troops.
    public static MilitaryLedgerIdentity? ExistingLedgerIdentity(ContractWorld w)
    {
        if(w.NewMilitary is not {} s)return null;
        var identity=LedgerIdentity(s.Terms.Owner,s.Terms.Site);
        return identity with {Army=s.Terms.Army,Depot=Depot,Carry=Carry,Units=[..UnitIds]};
    }
}
