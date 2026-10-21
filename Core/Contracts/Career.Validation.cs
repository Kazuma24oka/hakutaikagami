using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class Career
{
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){if(w.Career!=null)throw new InvalidDataException("Unexpected career module");return;}
        var s=w.Career??throw new InvalidDataException("Missing career module");
        if(s.Schema!=1||ContractJson.CanonicalHash(s.Terms)!=ContractJson.CanonicalHash(Terms(s.Terms.Recruits,s.Terms.DismissAfterReport))||w.Definitions.Version!=RegionalBattle.Version(w)||!w.Definitions.AuthorityBases.Any(b=>b.Id==Basis)||w.WorldId!=new EntityId("world:"+Ruleset))throw new InvalidDataException("Career definition or policy differs");
        PersonalStart.ValidatePeaceIdentity(w,true);
        bool LearnedBy(string message,EntityId actor,long at)=>MessageFlow.LearnedAt(w.Messaging,message,actor.Value) is {} known&&known<=at;
        if(s.Requests.Select(r=>r.Input.Id).Distinct().Count()!=s.Requests.Length||s.Events.Select(e=>e.Message).Distinct().Count()!=s.Events.Length||s.Openings.Select(o=>o.Message).Distinct().Count()!=s.Openings.Length)throw new InvalidDataException("Duplicate career business or opening");
        if(w.Messaging.Messages.Count(m=>m.Kind=="career-request")!=s.Requests.Length||w.Messaging.Messages.Count(m=>m.Kind=="career-notice")!=s.Events.Length||s.Events.Length!=w.IdCounters.GetValueOrDefault("career-event"))throw new InvalidDataException("Career letter/event ledger is incomplete");
        foreach(var o in s.Openings){var m=w.Messaging.Messages.Single(m=>m.Id==o.Message);if(m.Kind!="career-notice"||m.RecipientId!=w.PlayerControl.Value||o.At<0||o.At>w.ElapsedSeconds||o.Revision<1||o.Revision>w.CommitSequence||MessageFlow.LearnedAt(w.Messaging,m.Id,w.PlayerControl.Value)!=o.At||!w.Messaging.Attempts.Any(a=>a.MessageId==m.Id&&a.Status==AttemptStatus.Delivered&&a.ReceiverId==w.PlayerControl.Value&&a.FinishedAt<=o.At)||!s.Events.Any(e=>e.Message==m.Id&&e.Revision<=o.Revision))throw new InvalidDataException("Career recognition lacks a physical opening");}
        foreach(var r in s.Requests){
            var input=r.Input;var m=w.Messaging.Messages.Single(m=>m.Id==r.Message);var before=View(w,w.PlayerControl,r.At,r.Revision-1);
            if(w.Requests.Any(x=>x.Id==input.Id)||w.Region!.Commands.Any(x=>x.Request.Id==input.Id)||w.Field!.Orders.Any(x=>x.Request.Id==input.Id)||w.TravelDecisions!.Any(x=>x.Request.Id==input.Id)||!EntityId.IsValid(input.Id.Value)||!Enum.IsDefined(input.Action)||r.At<0||r.At>w.ElapsedSeconds||r.Revision<1||r.Revision>w.CommitSequence||r.Message!=RequestMessage(input.Id)||m.Kind!="career-request"||m.CorrelationId!=r.Case.Value||m.Body!=Text(input)||m.SenderId!=w.PlayerControl.Value||m.RecipientId!=s.Terms.Principal.Value||m.Scope!=s.Terms.Site.Value||m.CreatedAt!=r.At||w.SealedMessages.Any(id=>id.Value==r.Message))throw new InvalidDataException("Career request lacks its immutable sender/body/time");
            if(input.Action==CareerAction.Apply){if(input.Case!=null||r.Case!=input.Id||!before.CanApply||!NpcWork.Inputs(w,s.Terms.Principal,r.At,r.Revision-1).LocalWitness)throw new InvalidDataException("Application lacks known eligibility/local audience");}
            else{
                if(input.Case!=r.Case||!s.Requests.Any(a=>a.Case==r.Case&&a.Input.Action==CareerAction.Apply&&a.Revision<r.Revision))throw new InvalidDataException("Career decision lacks an earlier own case");
                var card=before.Cases.Single(c=>c.Case==r.Case);if(input.Action==CareerAction.Accept&&!card.CanAccept||input.Action==CareerAction.Decline&&!card.CanDecline||input.Action==CareerAction.RequestRelease&&!card.CanRelease)throw new InvalidDataException("Career decision used an unknown offer/service");
            }
            long? deadline=input.Action==CareerAction.Accept?Event(w,r.Case,CareerEventKind.Offered)!.At+s.Terms.OfferSeconds-1:null;if(m.DeliverBy!=deadline)throw new InvalidDataException("Acceptance receipt window differs");
        }
        for(int n=0;n<s.Events.Length;n++){
            var e=s.Events[n];if(e.Id!=new EntityId("career-event:"+(n+1))||e.At<0||e.At>w.ElapsedSeconds||e.Revision<1||e.Revision>w.CommitSequence||n>0&&(e.At<s.Events[n-1].At||e.Revision<s.Events[n-1].Revision)||!Enum.IsDefined(e.Kind))throw new InvalidDataException("Career event ordering differs");
            var app=s.Requests.Single(r=>r.Case==e.Case&&r.Input.Action==CareerAction.Apply);var offered=Event(w,e.Case,CareerEventKind.Offered);var joined=Event(w,e.Case,CareerEventKind.Joined);var end=End(w,e.Case);
            var source=w.Messaging.Messages.Single(m=>m.Id==e.Related);var m=w.Messaging.Messages.Single(m=>m.Id==e.Message);CareerStatement statement;long? effective=null,until=null;string suffix;
            bool RelatedRequest(CareerAction action)=>s.Requests.Any(r=>r.Case==e.Case&&r.Input.Action==action&&r.Message==e.Related&&r.Revision<e.Revision)&&LearnedBy(e.Related,s.Terms.Principal,e.At);
            bool ResolvedBefore()=>s.Events.Any(x=>x.Case==e.Case&&x.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired&&x.Revision<e.Revision);
            switch(e.Kind){
                case CareerEventKind.Offered:
                    if(e.Related!=app.Message||!s.Terms.Recruits||!LearnedBy(app.Message,s.Terms.Principal,e.At)||ActiveAt(w,s.Terms.Principal,e.At,e.Revision-1))throw new InvalidDataException("Offer lacks received application and principal policy");statement=CareerStatement.Offered;until=e.At+s.Terms.OfferSeconds;suffix="offer";break;
                case CareerEventKind.Joined:
                    if(offered==null||!RelatedRequest(CareerAction.Accept)||e.At>=offered.At+s.Terms.OfferSeconds||ResolvedBefore()||ActiveAt(w,s.Terms.Principal,e.At,e.Revision-1))throw new InvalidDataException("Service effective without timely mutual assent");statement=CareerStatement.Agreed;effective=e.At;suffix="agreement";break;
                case CareerEventKind.Declined:
                    if(offered==null||!RelatedRequest(CareerAction.Decline)||ResolvedBefore())throw new InvalidDataException("Decline lacks the recipient's recognition");statement=CareerStatement.Declined;suffix="declined";break;
                case CareerEventKind.Refused:
                    if(e.Related!=app.Message||!LearnedBy(app.Message,s.Terms.Principal,e.At)||s.Terms.Recruits&&!ActiveAt(w,s.Terms.Principal,e.At,e.Revision-1))throw new InvalidDataException("Refusal lacks principal policy or an existing service");statement=CareerStatement.Refused;suffix="refused";break;
                case CareerEventKind.Expired:
                    if(offered==null||e.At<offered.At+s.Terms.OfferSeconds||ResolvedBefore()||e.Related!=offered.Message&&!RelatedRequest(CareerAction.Accept))throw new InvalidDataException("Premature career offer expiry");statement=CareerStatement.Expired;suffix="expired";break;
                case CareerEventKind.ReleaseWaiting:
                    if(joined==null||!Before(joined,e.At,e.Revision-1)||!RelatedRequest(CareerAction.RequestRelease)||ReportsKnown(w,e.Case,out _,e.At,e.Revision-1))throw new InvalidDataException("Handover wait lacks unreceived own reports");statement=CareerStatement.AwaitingReports;suffix="handover-waiting";break;
                case CareerEventKind.Released:
                case CareerEventKind.Dismissed:
                    if(joined==null||e.At<=joined.At||!ReportsKnown(w,e.Case,out var report,e.At,e.Revision-1))throw new InvalidDataException("End of service lacks its earlier agreement/handover");
                    if(e.Kind==CareerEventKind.Released&&!RelatedRequest(CareerAction.RequestRelease)||e.Kind==CareerEventKind.Dismissed&&(!s.Terms.DismissAfterReport||report==null||e.Related!=report||!LearnedBy(report,s.Terms.Principal,e.At)))throw new InvalidDataException("Termination lacks a declared policy and known basis");statement=CareerStatement.Ended;effective=e.At;suffix="ended";break;
                case CareerEventKind.StatusSent:
                    if(!RelatedRequest(CareerAction.Inquire))throw new InvalidDataException("Status reply lacks received own inquiry");var status=StatusAt(w,e.Case,e.At,e.Revision-1);statement=status.Statement;effective=status.Effective;suffix="status/"+Hash(s.Requests.Single(r=>r.Message==e.Related).Input.Id);break;
                default:throw new InvalidDataException("Unknown career event");
            }
            var expected=new CareerNotice(e.Case,statement,effective,until,e.Related);
            if(e.Message!=CaseMessage(e.Case,suffix)||m.Kind!="career-notice"||m.CorrelationId!=e.Case.Value||m.SenderId!=s.Terms.Principal.Value||m.RecipientId!=w.PlayerControl.Value||m.Scope!=s.Terms.Site.Value||m.CreatedAt!=e.At||m.DeliverBy!=null||m.Body!=Text(expected)||!w.SealedMessages.Any(id=>id.Value==m.Id)||app.Revision>=e.Revision)throw new InvalidDataException("Career notice lacks its immutable event/basis");
        }
        var joins=s.Events.Where(e=>e.Kind==CareerEventKind.Joined).ToArray();
        TimedRelation[] Expected(string kind,EntityId target)=>joins.Select(e=>{var end=End(w,e.Case);return new TimedRelation(Relation(e.Case,kind),w.PlayerControl,target,kind,Basis,e.At,end?.At??long.MaxValue,end?.Revision??e.Revision);}).ToArray();
        if(ContractJson.CanonicalHash(w.Memberships.Where(r=>r.Subject==w.PlayerControl).ToArray())!=ContractJson.CanonicalHash(Expected("membership",s.Terms.House))||ContractJson.CanonicalHash(w.ServiceBonds.Where(r=>r.Subject==w.PlayerControl||r.Target==w.PlayerControl).ToArray())!=ContractJson.CanonicalHash(Expected("service",s.Terms.Principal)))throw new InvalidDataException("Career relationships differ from their independent recorded periods");
        foreach(var id in s.Requests.Where(r=>r.Input.Action==CareerAction.Apply).Select(r=>r.Case))foreach(var kind in Enum.GetValues<CareerEventKind>().Where(k=>k!=CareerEventKind.StatusSent))if(s.Events.Count(e=>e.Case==id&&e.Kind==kind)>1)throw new InvalidDataException("Career effect repeated");
    }
}
