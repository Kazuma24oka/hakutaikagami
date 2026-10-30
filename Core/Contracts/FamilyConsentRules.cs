namespace HakutaiKagami.Core.Contracts;
internal static class FamilyConsentRules
{
    internal static bool Present(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&PersonalCompanions.SiteAt(w,actor,at,rev)==site&&!NpcArmies.CommanderBusyAt(w,actor,at,rev);
    internal static string? AuthoredHouse(ContractWorld w,EntityId person)=>w.NewGame!.Start.People.Single(p=>p.Id==person.Value).Faction;
    internal static bool SameHouse(ContractWorld w,EntityId person,long at,long rev)=>w.Memberships.Where(m=>m.Subject==person&&m.From<=at&&(at<m.Until||at==m.Until&&rev<m.Revision)).Select(m=>m.Target.Value).SingleOrDefault()==AuthoredHouse(w,person);
    internal static AdoptionHouse[] KnownHouses(ContractWorld w,EntityId parent,EntityId child,long at,long rev)=>new[]{parent,child}.Select(p=>AuthoredHouse(w,p)).Where(o=>o!=null).Distinct().Select(o=>{
        var known=RepresentativeKnowledge.Known(w,w.PlayerControl,new(o!),at,Math.Min(rev,w.CommitSequence))??throw new InvalidDataException("家の代表者を確認できません");return new AdoptionHouse(new(o!),known.Person,known.ObservedAt,known.Source);
    }).OrderBy(h=>h.Owner.Value,StringComparer.Ordinal).ToArray();
    internal static bool IndividualAccepts(ContractWorld w,EntityId actor,EntityId other,long at,long rev){
        if(!w.Characters.Single(c=>c.Id==actor).WillObey)return false;var score=CharacterRelations.DiagnosticScores(w,actor,other,at,rev);
        return CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Duty)+CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Affection)+score.Trust-score.Hostility>=80;
    }

    internal static bool Related(ContractWorld w,EntityId a,EntityId b,long at,long rev,(EntityId Parent,EntityId Child)? extra=null){
        var rows=(w.Adoption?.Entries??[]).Where(e=>e.At<at||e.At==at&&e.Revision<rev).ToArray();
        var links=rows.Where(e=>e.Input.Action==AdoptionAction.Implement).Select(e=>rows.Single(p=>p.Input.Id==e.Input.Original).Terms!).Select(t=>(t.Parent,t.Child)).ToList();if(extra is {} edge)links.Add(edge);
        HashSet<EntityId> Ancestors(EntityId who){var seen=new HashSet<EntityId>();var pending=new Stack<EntityId>();pending.Push(who);while(pending.TryPop(out var current))if(seen.Add(current))foreach(var link in links.Where(l=>l.Child==current))pending.Push(link.Parent);return seen;}
        return Ancestors(a).Overlaps(Ancestors(b));
    }
    internal static bool MarriageConflict(ContractWorld w,EntityId parent,EntityId child,long at,long rev){
        var rows=(w.Marriage?.Entries??[]).Where(e=>e.At<at||e.At==at&&e.Revision<rev).ToArray();
        return rows.Where(p=>p.Terms!=null).Any(p=>{var events=rows.Where(e=>e.Input.Original==p.Input.Id).ToArray();bool active=events.Any(e=>e.Input.Action==MarriageAction.Implement)||at<=p.Terms!.Until&&!events.Any(e=>e.Input.Action is MarriageAction.PersonalNo or MarriageAction.Withdraw);return active&&Related(w,p.Terms!.First,p.Terms.Second,at,rev,(parent,child));});
    }
}
