using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum CareerAction { Apply, Accept, Decline, RequestRelease, Inquire }
public enum CareerEventKind { Offered, Joined, Declined, Refused, Expired, ReleaseWaiting, Released, Dismissed, StatusSent }
public enum CareerStatement { Offered, Agreed, Declined, Refused, Expired, Ended, AwaitingReports, NoAgreement }
public sealed record CareerTerms(EntityId Principal,EntityId House,EntityId Site,int OfferSeconds,bool Recruits,bool DismissAfterReport);
public sealed record CareerInput(EntityId Id,CareerAction Action,EntityId? Case=null);
public sealed record CareerRequest(CareerInput Input,EntityId Case,long At,long Revision,string Message);
public sealed record CareerEvent(EntityId Id,EntityId Case,CareerEventKind Kind,long At,long Revision,string Message,string Related);
public sealed record CareerNotice(EntityId Case,CareerStatement Statement,long? EffectiveAt,long? OfferUntil,string BasisMessage);
public sealed record CareerOpening(string Message,long At,long Revision);
public sealed record CareerState(int Schema,CareerTerms Terms,CareerRequest[] Requests,CareerEvent[] Events,CareerOpening[] Openings);
public sealed record CareerCard(EntityId Case,string Status,EntityId? Unopened,bool CanAccept,bool CanDecline,bool CanRelease);
public sealed record CareerView(string Role,bool CanApply,CareerCard[] Cases);

// One limited peace-life profile. Recruitment and the optional end-of-training decision are declared fixtures.
public static partial class Career
{
    public const string Ruleset="created-career-peace-v1",Contract="same-person-physical-mutual-assent-independent-relations-notice-and-known-role-v1";
    public static readonly EntityId Basis=new("basis:career-agreement");
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||CareerMilitary.Enabled(w);
    static CareerTerms Terms(bool recruits,bool dismiss)=>new(RetainerStart.Lord,RetainerStart.House,ContractFixture.Castle,120,recruits,dismiss);
    public static string Fingerprint(ContractWorld w)=>ContractJson.CanonicalHash(new[]{Contract,PersonalStart.Fingerprint(w),ContractJson.CanonicalHash(w.Career!.Terms)+(CareerMilitary.Enabled(w)?CareerMilitary.Contract:"")});
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020,bool recruits=true,bool dismissAfterReport=false)
    {
        var w=PersonalStart.Create(name,seed);w.WorldId=new("world:"+Ruleset);w.Career=new(1,Terms(recruits,dismissAfterReport),[],[],[]);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,new(Basis,"作成人物の試用仕官/辞去合意。所属と直属のみ。軍・所領・役職・財産は移転しない。懲罰的な人事AIではない")]};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);return w;
    }
    static string Hash(EntityId id)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value));
    internal static string RequestMessage(EntityId id)=>"message:career/request/"+Hash(id);
    static string CaseMessage(EntityId id,string suffix)=>"message:career/notice/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value+"/"+suffix));
    static string Text<T>(T v)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(v));
    internal static T Read<T>(MessageEnvelope m)=>ContractJson.Read<T>(System.Text.Encoding.UTF8.GetBytes(m.Body));
    static CareerEvent? Event(ContractWorld w,EntityId id,CareerEventKind kind)=>w.Career!.Events.SingleOrDefault(e=>e.Case==id&&e.Kind==kind);
    static CareerEvent? End(ContractWorld w,EntityId id)=>w.Career!.Events.SingleOrDefault(e=>e.Case==id&&e.Kind is CareerEventKind.Released or CareerEventKind.Dismissed);
    static bool Before(CareerEvent e,long at,long revision)=>e.At<at||e.At==at&&e.Revision<=revision;
    public static bool ActiveAt(ContractWorld w,EntityId principal,long at,long? revision=null)
    {
        if(!Enabled(w)||principal!=w.Career!.Terms.Principal)return false;long rev=revision??w.CommitSequence;
        return w.Career.Events.Any(e=>e.Kind==CareerEventKind.Joined&&Before(e,at,rev)&&(End(w,e.Case) is not {} end||!Before(end,at,rev)));
    }
    public static bool DialoguePendingAt(ContractWorld w,EntityId principal,long at,long? revision=null)
    {
        if(!Enabled(w)||principal!=w.Career!.Terms.Principal)return false;long rev=revision??w.CommitSequence;
        return w.Career.Events.Any(e=>e.Kind==CareerEventKind.Offered&&Before(e,at,rev)&&!w.Career.Events.Any(r=>r.Case==e.Case&&r.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired&&Before(r,at,rev)));
    }
    static void Send(ContractWorld w,MessageEnvelope message,bool sealedLetter)
    {
        MessageFlow.Send(w.Messaging,message,w.ElapsedSeconds,1);
        if(sealedLetter)w.SealedMessages=[..w.SealedMessages,new EntityId(message.Id)];
    }
    static CareerEvent Announce(ContractWorld w,EntityId id,CareerEventKind kind,CareerStatement statement,string related,string suffix,long? effective=null,long? until=null)
    {
        var message=CaseMessage(id,suffix);var body=new CareerNotice(id,statement,effective,until,related);
        Send(w,new(message,id.Value,"career-notice",w.Career!.Terms.Principal.Value,w.PlayerControl.Value,Text(body),w.Career.Terms.Site.Value,w.ElapsedSeconds),true);
        var entry=new CareerEvent(WorldSession.Allocate(w,"career-event"),id,kind,w.ElapsedSeconds,w.CommitSequence,message,related);
        w.Career=w.Career with{Events=[..w.Career.Events,entry]};return entry;
    }
    static EntityId Relation(EntityId id,string kind)=>new(kind+":career/"+Hash(id));
    static void Join(ContractWorld w,CareerRequest request)
    {
        var e=Announce(w,request.Case,CareerEventKind.Joined,CareerStatement.Agreed,request.Message,"agreement",w.ElapsedSeconds);
        w.Memberships=[..w.Memberships,new(Relation(e.Case,"membership"),w.PlayerControl,w.Career!.Terms.House,"membership",Basis,e.At,long.MaxValue,e.Revision)];
        w.ServiceBonds=[..w.ServiceBonds,new(Relation(e.Case,"service"),w.PlayerControl,w.Career.Terms.Principal,"service",Basis,e.At,long.MaxValue,e.Revision)];
    }
    static void Finish(ContractWorld w,EntityId id,CareerEventKind kind,string related)
    {
        var e=Announce(w,id,kind,CareerStatement.Ended,related,"ended",w.ElapsedSeconds);
        w.Memberships=w.Memberships.Select(r=>r.Id==Relation(id,"membership")?r with{Until=e.At,Revision=e.Revision}:r).ToArray();
        w.ServiceBonds=w.ServiceBonds.Select(r=>r.Id==Relation(id,"service")?r with{Until=e.At,Revision=e.Revision}:r).ToArray();
    }
    static bool Known(ContractWorld w,string message,EntityId actor)=>MessageFlow.Knows(w.Messaging,message,actor.Value);
    static bool ReportsKnown(ContractWorld w,EntityId id,out string? latest,long? at=null,long? revision=null)
    {
        var joined=Event(w,id,CareerEventKind.Joined)!;var principal=w.Career!.Terms.Principal;long time=at??w.ElapsedSeconds,rev=revision??w.CommitSequence;
        var tasks=w.Tasks.Where(t=>t.Issuer==principal&&t.Executor==w.PlayerControl&&w.Requests.Single(r=>r.Id==t.RequestId).CommitSequence>=joined.Revision&&w.Requests.Single(r=>r.Id==t.RequestId).CommitSequence<=rev&&w.Requests.Single(r=>r.Id==t.RequestId).CreatedAt<=time).ToArray();latest=null;
        foreach(var t in tasks){var report=WorldSession.Child("message",t.Id,"report").Value;if(MessageFlow.LearnedAt(w.Messaging,report,principal.Value) is not {} known||known>time)return false;latest=report;}
        return !CareerMilitary.OutstandingAt(w,time,CareerMilitary.Enabled(w)&&w.Military!.Events.Any(e=>e.Effect==MilitaryEffect.Returned&&e.Revision==rev)?rev-1:rev);
    }
    internal static bool WorkReportsKnown(ContractWorld w,out string? latest,long? at=null,long? revision=null){long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;var join=w.Career!.Events.LastOrDefault(e=>e.Kind==CareerEventKind.Joined&&Before(e,t,r));latest=null;return join!=null&&ReportsKnown(w,join.Case,out latest,t,r);}
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {
        if(!Enabled(w)||actor!=w.PlayerControl||!w.Messaging.Messages.Any(m=>m.Id==message&&m.Kind=="career-notice")||w.Career!.Openings.Any(o=>o.Message==message))return;
        w.Career=w.Career with{Openings=[..w.Career.Openings,new(message,w.ElapsedSeconds,w.CommitSequence)]};
    }
    internal static void Dispatch(ContractWorld w,CareerInput input)
    {
        var state=w.Career!;var view=View(w,w.PlayerControl);EntityId id=input.Action==CareerAction.Apply?input.Id:input.Case??throw new InvalidDataException("Career case required");
        if(!Enum.IsDefined(input.Action)||!EntityId.IsValid(input.Id.Value)||w.Requests.Any(r=>r.Id==input.Id)||w.Region!.Commands.Any(c=>c.Request.Id==input.Id)||w.Field!.Orders.Any(o=>o.Request.Id==input.Id)||w.TravelDecisions!.Any(d=>d.Request.Id==input.Id))throw new InvalidDataException("Career request ID invalid or used");
        if(input.Action==CareerAction.Apply){if(input.Case!=null||!view.CanApply||!w.Locations.Any(l=>l.Actor==w.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==state.Terms.Site)||!NpcWork.Inputs(w,state.Terms.Principal,w.ElapsedSeconds).LocalWitness)throw new InvalidDataException("An application requires a local audience and no known pending agreement");}
        else{
            if(!state.Requests.Any(r=>r.Case==id&&r.Input.Action==CareerAction.Apply))throw new InvalidDataException("Unknown personally issued application");
            var card=view.Cases.Single(c=>c.Case==id);
            if(input.Action==CareerAction.Accept&&!card.CanAccept||input.Action==CareerAction.Decline&&!card.CanDecline||input.Action==CareerAction.RequestRelease&&!card.CanRelease)throw new InvalidDataException("The required known offer or service is absent");
        }
        var request=new CareerRequest(input,id,w.ElapsedSeconds,w.CommitSequence,RequestMessage(input.Id));w.Career=state with{Requests=[..state.Requests,request]};
        long? deadline=input.Action==CareerAction.Accept?checked(Event(w,id,CareerEventKind.Offered)!.At+state.Terms.OfferSeconds-1):null;
        Send(w,new(request.Message,id.Value,"career-request",w.PlayerControl.Value,state.Terms.Principal.Value,Text(input),state.Terms.Site.Value,w.ElapsedSeconds,deadline),false);
    }
    public static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;
        return w.Career!.Events.Where(e=>e.Kind==CareerEventKind.Offered&&!w.Career.Events.Any(r=>r.Case==e.Case&&r.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired)).Select(e=>checked(e.At+w.Career.Terms.OfferSeconds)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    static (CareerStatement Statement,long? Effective) StatusAt(ContractWorld w,EntityId id,long at,long revision)
    {
        var ended=End(w,id);if(ended!=null&&Before(ended,at,revision))return(CareerStatement.Ended,ended.At);
        var joined=Event(w,id,CareerEventKind.Joined);if(joined!=null&&Before(joined,at,revision))return(CareerStatement.Agreed,joined.At);
        var closed=w.Career!.Events.LastOrDefault(e=>e.Case==id&&e.Kind is CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired&&Before(e,at,revision));
        return(closed?.Kind switch{CareerEventKind.Declined=>CareerStatement.Declined,CareerEventKind.Refused=>CareerStatement.Refused,CareerEventKind.Expired=>CareerStatement.Expired,_=>CareerStatement.NoAgreement},null);
    }
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w))return;var state=w.Career!;var principal=state.Terms.Principal;
        foreach(var app in state.Requests.Where(r=>r.Input.Action==CareerAction.Apply&&Known(w,r.Message,principal)).ToArray())
        {
            var id=app.Case;var offered=Event(w,id,CareerEventKind.Offered);
            if(offered==null&&!w.Career!.Events.Any(e=>e.Case==id&&e.Kind==CareerEventKind.Refused)){
                if(!state.Terms.Recruits||ActiveAt(w,principal,w.ElapsedSeconds))Announce(w,id,CareerEventKind.Refused,CareerStatement.Refused,app.Message,"refused");
                else offered=Announce(w,id,CareerEventKind.Offered,CareerStatement.Offered,app.Message,"offer",until:checked(w.ElapsedSeconds+state.Terms.OfferSeconds));
            }
            if(offered==null)continue;
            bool resolved=w.Career!.Events.Any(e=>e.Case==id&&e.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired);
            var answer=w.Career.Requests.SingleOrDefault(r=>r.Case==id&&r.Input.Action is CareerAction.Accept or CareerAction.Decline&&Known(w,r.Message,principal));
            if(!resolved&&answer!=null){if(answer.Input.Action==CareerAction.Decline)Announce(w,id,CareerEventKind.Declined,CareerStatement.Declined,answer.Message,"declined");else if(w.ElapsedSeconds<offered.At+state.Terms.OfferSeconds&&!ActiveAt(w,principal,w.ElapsedSeconds))Join(w,answer);else Announce(w,id,CareerEventKind.Expired,CareerStatement.Expired,answer.Message,"expired");}
            else if(!resolved&&w.ElapsedSeconds>=offered.At+state.Terms.OfferSeconds)Announce(w,id,CareerEventKind.Expired,CareerStatement.Expired,offered.Message,"expired");
            var joined=Event(w,id,CareerEventKind.Joined);if(joined==null||End(w,id)!=null)continue;
            var release=w.Career.Requests.SingleOrDefault(r=>r.Case==id&&r.Input.Action==CareerAction.RequestRelease&&Known(w,r.Message,principal));
            if(ReportsKnown(w,id,out var report)){
                if(release!=null)Finish(w,id,CareerEventKind.Released,release.Message);
                else if(state.Terms.DismissAfterReport&&report!=null)Finish(w,id,CareerEventKind.Dismissed,report);
            }else if(release!=null&&Event(w,id,CareerEventKind.ReleaseWaiting)==null)Announce(w,id,CareerEventKind.ReleaseWaiting,CareerStatement.AwaitingReports,release.Message,"handover-waiting");
        }
        foreach(var q in w.Career!.Requests.Where(r=>r.Input.Action==CareerAction.Inquire&&Known(w,r.Message,principal)).ToArray())
        {
            if(w.Career.Events.Any(e=>e.Kind==CareerEventKind.StatusSent&&e.Related==q.Message))continue;
            var status=StatusAt(w,q.Case,w.ElapsedSeconds,w.CommitSequence);
            Announce(w,q.Case,CareerEventKind.StatusSent,status.Statement,q.Message,"status/"+Hash(q.Input.Id),status.Effective);
        }
    }
}
