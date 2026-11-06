using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public sealed record SuccessionOpening(EntityId House,EntityId Deceased,EntityId Home,long At,long Revision);
public sealed record HouseClaim(EntityId House,EntityId Deceased,EntityId Claimant,long At,long Revision);
public sealed record ClaimOpinion(long ClaimRevision,EntityId Speaker,bool Supports,long At,long Revision);
public sealed record KnownSuccession(SuccessionOpening Opening,bool OwnClaim,HeirNomination? KnownNomination);

public static class Guardianship
{
    internal static IEnumerable<SuccessionOpening> Openings(ContractWorld w)
    {
        if(w.NewGame==null)yield break;
        var deaths=(w.NaturalDeaths??[]).Select(d=>(d.Person,d.At,d.Revision))
            .Concat(InformationSchemes.Events(w).Where(e=>e.Input.Action==SchemeAction.FatalEncounter)
            .Select(e=>(e.Input.Subject,e.At,e.Revision)));
        foreach(var (deceased,at,revision) in deaths)
            foreach(var house in HouseSuccession.Factions(w,at,revision-1).Where(f=>f.Head==deceased.Value))
                yield return new(new(house.Id),deceased,new(house.Home),at,revision);
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        MinorGuardians.Validate(w,prior);
        var rows=w.HouseClaims??[];
        var existing=prior?.HouseClaims??[];
        if(!rows.Take(existing.Length).SequenceEqual(existing) ||
            rows.DistinctBy(c=>(c.House,c.Deceased,c.Claimant)).Count()!=rows.Length)
            throw new InvalidDataException("家督申立の原本が変更または重複しています");
        long revision=0;
        foreach(var c in rows)
        {
            var opening=Openings(w).SingleOrDefault(o=>o.House==c.House && o.Deceased==c.Deceased);
            if(opening==null || c.At<opening.At || c.At>w.ElapsedSeconds || c.Revision<=opening.Revision ||
                c.Revision<=revision || c.Revision>w.CommitSequence ||
                Assassination.DeadAt(w,c.Claimant,c.At,c.Revision) ||
                !FamilyConsentRules.ParentLinks(w,opening.At,opening.Revision).Contains((c.Deceased,c.Claimant)) ||
                !TransportWork.At(w,c.Claimant,opening.Home,c.At,c.Revision,true))
                throw new InvalidDataException("申立人・死亡原本・現地立会の時点が一致しません");
            revision=c.Revision;
        }
        var opinions=w.ClaimOpinions??[];
        var previous=prior?.ClaimOpinions??[];
        if(!opinions.Take(previous.Length).SequenceEqual(previous) ||
            opinions.DistinctBy(o=>(o.ClaimRevision,o.Speaker)).Count()!=opinions.Length)
            throw new InvalidDataException("家臣の意向原本が変更または重複しています");
        long last=0;
        foreach(var opinion in opinions)
        {
            var claim=rows.SingleOrDefault(c=>c.Revision==opinion.ClaimRevision);
            if(claim==null || opinion.Revision<=claim.Revision || opinion.Revision<=last || opinion.Revision>w.CommitSequence ||
                opinion.At<claim.At || opinion.At>w.ElapsedSeconds || !CanAnswer(w,claim,opinion.Speaker,opinion.At,opinion.Revision) ||
                opinion.Supports!=FamilyConsentRules.IndividualAccepts(w,opinion.Speaker,claim.Claimant,opinion.At,opinion.Revision-1))
                throw new InvalidDataException("申立・現地の家臣・本人の返答が一致しません");
            last=opinion.Revision;
        }
    }

    internal static bool CanAnswer(ContractWorld w,HouseClaim claim,EntityId speaker,long at,long revision)
    {
        if(speaker==claim.Claimant || speaker==claim.Deceased || !CharacterRelations.Enabled(w) || w.NewGame==null)return false;
        var member=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==speaker.Value);
        var home=new EntityId(w.NewGame.Start.Factions.Single(h=>h.Id==claim.House.Value).Home);
        return member!=null && FamilyKin.LifeAt(w.NewGame,member,at)==LifeState.Adult &&
            !Assassination.DeadAt(w,speaker,at,revision) && !Assassination.DeadAt(w,claim.Claimant,at,revision) &&
            w.Memberships.Any(m=>m.Subject==speaker && m.Target==claim.House && m.From<=at && at<m.Until) &&
            TransportWork.At(w,speaker,home,at,revision,true) && TransportWork.At(w,claim.Claimant,home,at,revision,true);
    }

    public static IEnumerable<ClaimOpinion> HeardOpinions(ContractWorld w,HouseClaim claim,EntityId observer)=>
        (w.ClaimOpinions??[]).Where(o=>o.ClaimRevision==claim.Revision && (o.Speaker==observer ||
            !Assassination.DeadAt(w,observer,o.At,o.Revision) && TransportWork.At(w,observer,
                new(w.NewGame!.Start.Factions.Single(h=>h.Id==claim.House.Value).Home),o.At,o.Revision,true)));

    public static IEnumerable<HouseClaim> WitnessedClaims(ContractWorld w,SuccessionOpening opening,EntityId observer)=>
        (w.HouseClaims??[]).Where(c=>c.House==opening.House && c.Deceased==opening.Deceased &&
            (c.Claimant==observer || !Assassination.DeadAt(w,observer,c.At,c.Revision) &&
            TransportWork.At(w,observer,opening.Home,c.At,c.Revision,true)));

    public static IEnumerable<KnownSuccession> Known(ContractWorld w,EntityId observer)
    {
        foreach(var opening in Openings(w))
        {
            var dead=opening.Deceased;
            bool knows=(w.NaturalDeaths??[]).Any(d=>d.Person==dead && FamilyNotice.Knows(w,d,observer)) ||
                InformationSchemes.Events(w).Any(e=>e.Input.Action==SchemeAction.FatalEncounter &&
                    e.Input.Subject==dead && e.Actor==observer);
            if(!knows || observer==dead)continue;
            var ownClaim=FamilyConsentRules.ParentLinks(w,opening.At,opening.Revision).Contains((dead,observer));
            var nomination=(w.HeirNominations??[]).LastOrDefault(p=>p.House==opening.House && p.Head==dead &&
                p.Revision<=opening.Revision && w.Messaging.Messages.Any(m=>m.Kind==HeirPledges.RequestKind &&
                    m.CorrelationId==HeirPledges.Version(p) && MessageFlow.Knows(w.Messaging,m.Id,observer.Value)));
            yield return new(opening,ownClaim,nomination);
        }
    }
}

public sealed partial class WorldSession
{
    public CommandResult ConsultHouseMember(long claimRevision,EntityId speaker,long shown)
    {
        lock(gate)
        {
            var claim=state.HouseClaims?.SingleOrDefault(c=>c.Revision==claimRevision && c.Claimant==state.PlayerControl);
            if(claim==null)return new(ResultCode.NotAvailable,speaker,state.CommitSequence,"自分の申立を指定してください");
            if((state.ClaimOpinions??[]).Any(o=>o.ClaimRevision==claimRevision && o.Speaker==speaker))
                return new(ResultCode.AlreadyAccepted,speaker,state.CommitSequence,"この申立への返答は記録済みです");
            if(shown!=state.CommitSequence)return new(ResultCode.StateChanged,speaker,state.CommitSequence,"表示を更新してください");
            if(!Guardianship.CanAnswer(state,claim,speaker,state.ElapsedSeconds,shown))
                return new(ResultCode.NotAvailable,speaker,shown,"本拠にいる家臣本人に会ってください");
            var support=FamilyConsentRules.IndividualAccepts(state,speaker,claim.Claimant,state.ElapsedSeconds,shown);
            Change(w=>w.ClaimOpinions=[..w.ClaimOpinions??[],new(claimRevision,speaker,support,w.ElapsedSeconds,w.CommitSequence)]);
            return new(ResultCode.Accepted,speaker,state.CommitSequence,"家臣の返答を記録しました");
        }
    }

    public CommandResult ClaimSuccession(EntityId house,EntityId deceased,long revision)
    {
        lock(gate)
        {
            var actor=state.PlayerControl;
            if((state.HouseClaims??[]).Any(c=>c.House==house && c.Deceased==deceased && c.Claimant==actor))
                return new(ResultCode.AlreadyAccepted,house,state.CommitSequence,"現地で申立済みです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,house,state.CommitSequence,"状況を再確認してください");
            var dispute=Guardianship.Known(state,actor).SingleOrDefault(c=>c.Opening.House==house && c.Opening.Deceased==deceased);
            if(dispute?.OwnClaim!=true || SocialWorld.Site(state,actor)!=dispute.Opening.Home ||
                state.Characters.Single(c=>c.Id==actor).Life!=LifeState.Adult)
                return new(ResultCode.NotAvailable,house,state.CommitSequence,"故当主の死去を確認した成人の候補が本拠で申し立てます");
            Change(w=>w.HouseClaims=[..w.HouseClaims??[],new(house,deceased,actor,w.ElapsedSeconds,w.CommitSequence)]);
            return new(ResultCode.Accepted,house,state.CommitSequence,"家督を求める旨を現地で申し立てました");
        }
    }
}
