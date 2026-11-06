using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public enum CoalitionPurpose { CoordinateSorties, ForbidSeparatePeace, Both }
public sealed record CoalitionTerms(EntityId Target,EntityId[] Invitees,CoalitionPurpose Purpose,long AssemblyAt);

public static class CoalitionPacts
{
    public const string Invitation="coalition-invitation",Reply="coalition-reply";
    internal static bool Handles(CourtAction action)=>action is CourtAction.ProposeCoalition or CourtAction.JoinCoalition or CourtAction.RefuseCoalition or CourtAction.DeferCoalition;
    static bool Final(CourtAction action)=>action is CourtAction.JoinCoalition or CourtAction.RefuseCoalition;
    public static string TermsLabel(CoalitionTerms terms)=>terms.Purpose switch{
        CoalitionPurpose.CoordinateSorties=>"同時期の出兵",CoalitionPurpose.ForbidSeparatePeace=>"単独で和睦しない",_=>"同時期の出兵と単独和睦の禁止"};
    static RepresentativeObservation? Address(ContractWorld w,CourtEvent offer,EntityId owner)=>
        RepresentativeKnowledge.Known(w,offer.Actor,owner,offer.At,offer.Revision-1);
    static string InvitationId(CourtInput offer,EntityId owner)=>HouseholdCourt.Id("coalition-call",offer.Id.Value+"/"+owner.Value).Value;
    static IEnumerable<MessageEnvelope> Letters(ContractWorld w,CourtEvent e){
        var i=e.Input;
        if(i.Action==CourtAction.ProposeCoalition){
            foreach(var house in i.Coalition!.Invitees){
                var recipient=Address(w,e,house)??throw new InvalidDataException("呼びかけ時点の宛先が不明です");
                yield return new MessageEnvelope(InvitationId(i,house),i.Id.Value,Invitation,e.Actor.Value,recipient.Person.Value,
                    w.Definitions.Organizations.Single(o=>o.Id==i.Coalition.Target).Name+"を対象に"+TermsLabel(i.Coalition)+"を提案する。期日 "+i.Coalition.AssemblyAt,recipient.Home.Value,e.At){DeliverySite=recipient.Home.Value};
            }
        }else if(Final(i.Action)){
            var offer=HouseholdCourt.Events(w).Single(x=>x.Input.Id==i.Reference);
            yield return new MessageEnvelope(HouseholdCourt.Id("coalition-answer",i.Id.Value).Value,offer.Input.Id.Value,Reply,e.Actor.Value,offer.Actor.Value,
                i.Action==CourtAction.JoinCoalition?"この原本の盟約に加わる。":"この原本への加盟を断る。",offer.Place.Value,e.At){DeliverySite=offer.Place.Value};
        }
    }
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){
        var house=HouseSuccession.Factions(w,at,rev-1).SingleOrDefault(f=>f.Id==i.Owner.Value);
        if(!TruceNegotiations.Enabled(w) || house?.Head!=actor.Value || house.Home!=place.Value || place!=i.Site ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev))
            throw new InvalidDataException("本人が本拠で家を代表して包囲網の書状を扱ってください");
        if(i.Action==CourtAction.ProposeCoalition){
            var t=i.Coalition;
            if(t==null || !Enum.IsDefined(t.Purpose) || i.Reference!=null || i.Person!=actor || t.Target==i.Owner ||
                t.AssemblyAt<=at || t.Invitees is not {Length:>=2} || t.Invitees.Distinct().Count()!=t.Invitees.Length ||
                t.Invitees.Contains(i.Owner) || t.Invitees.Contains(t.Target) ||
                t.Invitees.Append(t.Target).Any(h=>RepresentativeKnowledge.Known(w,actor,h,at,rev-1)==null))
                throw new InvalidDataException("対象と異なる二家以上の既知の宛先、目的、将来の期日を指定してください");
            return;
        }
        var prior=HouseholdCourt.Events(w).Where(e=>e.Revision<rev && e.At<=at).ToArray();
        var offer=prior.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.ProposeCoalition);
        if(offer==null || i.Coalition!=null || i.Person!=offer.Actor || offer.Input.Coalition!.AssemblyAt<=at ||
            !offer.Input.Coalition.Invitees.Contains(i.Owner) || Address(w,offer,i.Owner)?.Person!=actor ||
            MessageFlow.LearnedAt(w.Messaging,InvitationId(offer.Input,i.Owner),actor.Value) is not {} opened || opened>at ||
            prior.Any(e=>e.Input.Reference==i.Reference && e.Input.Owner==i.Owner && (Final(e.Input.Action)||e.Input.Action==i.Action)))
            throw new InvalidDataException("本人が開封した期限内の同じ盟約に一度だけ回答できます");
    }
    public static IEnumerable<(string Letter,CourtInput? Answer)> Invitations(ContractWorld w,EntityId actor){
        var events=HouseholdCourt.Events(w);
        foreach(var offer in events.Where(e=>e.Input.Action==CourtAction.ProposeCoalition && w.ElapsedSeconds<e.Input.Coalition!.AssemblyAt)){
            foreach(var house in offer.Input.Coalition!.Invitees){
                var id=InvitationId(offer.Input,house);
                if(!w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value) ||
                    events.Any(e=>Final(e.Input.Action) && e.Input.Owner==house && e.Input.Reference==offer.Input.Id))continue;
                if(!MessageFlow.Knows(w.Messaging,id,actor.Value)){yield return(id,null);continue;}
                var home=Address(w,offer,house)!.Home;
                foreach(var action in new[]{CourtAction.JoinCoalition,CourtAction.DeferCoalition,CourtAction.RefuseCoalition}){
                    var input=new CourtInput(HouseholdCourt.Id(action.ToString(),id),action,offer.Actor,house,home,Reference:offer.Input.Id);
                    try{Check(w,input,actor,home,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
                    yield return(id,input);
                }
            }
        }
    }
    internal static IEnumerable<(CourtInput Answer,EntityId Actor)> NpcReplies(ContractWorld w){
        var proposals=HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.ProposeCoalition && w.ElapsedSeconds<e.Input.Coalition!.AssemblyAt);
        foreach(var offer in proposals){
            var terms=offer.Input.Coalition!;
            foreach(var owner in terms.Invitees){
                var recipient=Address(w,offer,owner)!;var actor=recipient.Person;
                if(actor==w.PlayerControl || HouseSuccession.Head(w,owner)!=actor || SocialWorld.Site(w,actor)!=recipient.Home)continue;
                var id=InvitationId(offer.Input,owner);
                if(!w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value) ||
                    HouseholdCourt.Events(w).Any(e=>e.Input.Owner==owner && e.Input.Reference==offer.Input.Id && Final(e.Input.Action)))continue;
                if(!MessageFlow.Knows(w.Messaging,id,actor.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id,actor.Value,w.ElapsedSeconds,id)];
                var threat=FactionStances.Known(w,actor).Any(k=>k.FromOwner==terms.Target && k.ToOwner==owner && k.EffectiveFrom<=w.ElapsedSeconds && w.ElapsedSeconds<k.Until);
                var trust=SocialWorld.ConsentBonus(w,actor,offer.Actor,w.ElapsedSeconds,w.CommitSequence);
                var action=threat || trust>=2?CourtAction.JoinCoalition:trust<0?CourtAction.RefuseCoalition:CourtAction.DeferCoalition;
                if(action==CourtAction.DeferCoalition && HouseholdCourt.Events(w).Any(e=>e.Input.Action==action && e.Input.Reference==offer.Input.Id && e.Input.Owner==owner))continue;
                yield return(new(HouseholdCourt.Id(action.ToString(),id),action,offer.Actor,owner,recipient.Home,Reference:offer.Input.Id),actor);
            }
        }
    }
    internal static void Send(ContractWorld w,CourtEvent e){
        foreach(var m in Letters(w,e)){MessageFlow.Send(w.Messaging,m,e.At);w.SealedMessages=[..w.SealedMessages,new(m.Id)];}
        RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,CourtEvent e){
        foreach(var m in Letters(w,e))if(!w.Messaging.Messages.Contains(m) || !w.SealedMessages.Any(x=>x.Value==m.Id))
            throw new InvalidDataException("包囲網の書状が当時の原本・宛先と一致しません");
    }
}
