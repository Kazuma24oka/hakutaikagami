namespace HakutaiKagami.Core.Contracts;

// Authoritative current facts for an issuer, never a player-knowledge/UI predicate.
// This does not create troops, transfer property, grant historical combat, or authorize a delegate.
public sealed record MilitaryIssuerAuthorityBasis(string Contract,EntityId Issuer,EntityId Agent,EntityId Owner,EntityId Site,EntityId Membership,EntityId Office,EntityId Control,EntityId? Service,long At,long Revision);
public static class NewGameMilitaryAuthority
{
    public const string HouseholdMilitaryAgreementContract="actual-head-and-household-member-case-specific-military-agreement-v1";
    public const string Contract="settled-head-membership-office-local-control-personal-service-basis-v1";
    static bool Active(TimedRelation r,ContractWorld w)=>r.From<=w.ElapsedSeconds&&(w.ElapsedSeconds<r.Until||w.ElapsedSeconds==r.Until&&w.CommitSequence<r.Revision);
    public static MilitaryIssuerAuthorityBasis? Resolve(ContractWorld w,EntityId issuer,EntityId agent,EntityId owner,EntityId site)
    {
        if(!NewGameWorld.Enabled(w)||w.Characters.SingleOrDefault(p=>p.Id==issuer)?.Life!=LifeState.Adult||w.Characters.SingleOrDefault(p=>p.Id==agent)?.Life!=LifeState.Adult)return null;
        var faction=HouseSuccession.Factions(w).SingleOrDefault(f=>f.Id==owner.Value&&f.Head==issuer.Value);if(faction==null)return null;
        var member=w.Memberships.SingleOrDefault(r=>r.Subject==issuer&&r.Target==owner&&r.Kind=="membership"&&Active(r,w));
        var office=w.Offices.SingleOrDefault(r=>r.Subject==issuer&&r.Kind=="house-head"&&r.Target.Value==faction.Home&&Active(r,w));
        var control=w.LandRights.SingleOrDefault(r=>r.Subject==owner&&r.Target==site&&r.Kind=="control"&&Active(r,w));
        if(member==null||office==null||control==null||!w.Locations.Any(l=>l.Actor==issuer&&l.Kind==LocationKind.AtSite&&l.Site==site))return null;
        TimedRelation? service=null;
        if(agent!=issuer){service=w.ServiceBonds.SingleOrDefault(r=>r.Subject==agent&&r.Target==issuer&&r.Kind=="service"&&Active(r,w));if(service==null&&!NewGameMilitary.SuccessorMilitaryAgreement(w,issuer,agent,owner,site)||!w.Memberships.Any(r=>r.Subject==agent&&r.Target==owner&&r.Kind=="membership"&&Active(r,w)))return null;if(agent==w.PlayerControl&&!NewGameService.BoundAt(w,issuer,w.ElapsedSeconds,w.CommitSequence)&&!NewGameMilitary.SuccessorMilitaryAgreement(w,issuer,agent,owner,site))return null;}
        return new(agent!=issuer&&service==null?HouseholdMilitaryAgreementContract:Contract,issuer,agent,owner,site,member.Id,office.Id,control.Id,service?.Id,w.ElapsedSeconds,w.CommitSequence);
    }
    public static bool MatchesCurrent(ContractWorld w,MilitaryIssuerAuthorityBasis basis)=>(basis.Contract==Contract||basis.Contract==HouseholdMilitaryAgreementContract)&&basis==Resolve(w,basis.Issuer,basis.Agent,basis.Owner,basis.Site);
}
