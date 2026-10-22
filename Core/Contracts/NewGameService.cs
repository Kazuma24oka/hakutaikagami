using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record ServiceInput(EntityId Id,CareerAction Action,EntityId? Case=null,EntityId? Principal=null);
public sealed record ServiceCase(EntityId Id,EntityId Principal,EntityId House,EntityId Site,bool Initial);
public sealed record ServiceRequest(ServiceInput Input,EntityId Case,long At,long Revision,string Message);
public sealed record ServiceState(int Schema,string Contract,ServiceCase[] Cases,ServiceRequest[] Requests,CareerEvent[] Events,CareerOpening[] Openings);
public sealed record ServiceCard(EntityId Case,EntityId Principal,string Status,bool CanAccept,bool CanDecline,bool CanRelease);
public sealed record ServiceView(string Role,EntityId? House,EntityId? Principal,bool CanApply,ServiceCard[] Cases);

// Parameterized personal agreements. Transport, openings, work effects and the clock remain shared.
public static partial class NewGameService
{
    public const string Contract="new-game-local-mutual-service-physical-effective-known-notice-independent-personal-retinue-v1";
    const int OfferSeconds=120;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value,string reason){if(!value)throw new InvalidDataException(reason);}
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static bool Before(CareerEvent e,long at,long rev)=>Before(e.At,e.Revision,at,rev);
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string Text<T>(T value)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(value));
    static string RequestId(EntityId id)=>"message:service/request/"+Hash(id.Value);
    static string NoticeId(EntityId id,CareerEventKind kind,string related)=>"message:service/notice/"+Hash(id.Value+"/"+kind+"/"+related);
    static ServiceState State(ContractWorld w)=>w.Service??new(1,Contract,[],[],[],[]);
    static SetupPerson Player(ContractWorld w)=>w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value);
    static bool FreeOfOffice(ContractWorld w)=>!w.Offices.Any(r=>r.Subject==w.PlayerControl)&&!w.LandRights.Any(r=>r.Subject==w.PlayerControl);
    static ServiceCase? Initial(ContractWorld w)
    {
        var p=Player(w);return p.Lord==null||p.Faction==null?null:new(new("service-case:initial/"+Hash(p.Id)),new(p.Lord),new(p.Faction),new(p.Site),true);
    }
    static ServiceCase[] Cases(ContractWorld w){var initial=Initial(w);var rows=State(w).Cases;return initial!=null&&!rows.Any(c=>c.Id==initial.Id)?[initial,..rows]:rows;}
    static CareerEvent? Event(ContractWorld w,EntityId id,CareerEventKind kind)=>State(w).Events.SingleOrDefault(e=>e.Case==id&&e.Kind==kind);
    static CareerEvent? End(ContractWorld w,EntityId id)=>Event(w,id,CareerEventKind.Released);
    static CareerEvent? Joined(ContractWorld w,ServiceCase c)=>c.Initial?new(new("service-event:initial"),c.Id,CareerEventKind.Joined,0,0,"",""):Event(w,c.Id,CareerEventKind.Joined);
    static bool Active(ContractWorld w,ServiceCase c,long at,long rev)=>Joined(w,c) is {} j&&Before(j,at,rev)&&(End(w,c.Id) is not {} end||!Before(end,at,rev));
    public static bool KnownBoundAt(ContractWorld w,EntityId principal,long at,long revision)=>View(w,at,revision).Principal==principal;
    public static bool BoundAt(ContractWorld w,EntityId principal,long at,long revision)=>NewGameWorld.Enabled(w)&&Cases(w).Any(c=>c.Principal==principal&&Active(w,c,at,revision));
    static bool ReadAt(ContractWorld w,CareerEvent e,long at,long rev)=>State(w).Openings.Any(o=>o.Message==e.Message&&Before(o.At,o.Revision,at,rev));
    static string Name(ContractWorld w,EntityId id)=>w.Definitions.Characters.Single(p=>p.Id==id).Name;
    static string HouseName(ContractWorld w,EntityId id)=>w.Definitions.Organizations.Single(p=>p.Id==id).Name;
    public static ServiceView View(ContractWorld w,long? at=null,long? revision=null)
    {
        if(!NewGameWorld.Enabled(w))return new("未確認",null,null,false,[]);
        long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;var state=State(w);var initial=Initial(w);var p=Player(w);EntityId? house=p.Faction==null?null:new(p.Faction),lord=p.Lord==null?null:new(p.Lord);
        var learned=state.Events.Where(e=>Before(e,t,r)&&ReadAt(w,e,t,r)).OrderBy(e=>e.At).ThenBy(e=>e.Revision).ToArray();
        CareerNotice Notice(CareerEvent e)=>ContractJson.Read<CareerNotice>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==e.Message).Body));
        var recognized=learned.Select(e=>(Event:e,Notice:Notice(e))).Where(n=>n.Notice.Statement is CareerStatement.Agreed or CareerStatement.Ended).OrderBy(n=>n.Notice.EffectiveAt).ThenBy(n=>n.Notice.Statement==CareerStatement.Agreed?Joined(w,Cases(w).Single(c=>c.Id==n.Event.Case))!.Revision:End(w,n.Event.Case)!.Revision);
        foreach(var n in recognized){var c=Cases(w).Single(c=>c.Id==n.Event.Case);house=n.Notice.Statement==CareerStatement.Agreed?c.House:null;lord=n.Notice.Statement==CareerStatement.Agreed?c.Principal:null;}
        var cards=new List<ServiceCard>();bool pending=false;
        foreach(var c in Cases(w)){
            var own=state.Requests.Where(q=>q.Case==c.Id&&Before(q.At,q.Revision,t,r)).ToArray();if(!c.Initial&&!own.Any(q=>q.Input.Action==CareerAction.Apply))continue;
            var known=learned.Where(e=>e.Case==c.Id).ToArray();var offer=known.SingleOrDefault(e=>e.Kind==CareerEventKind.Offered);bool answered=own.Any(q=>q.Input.Action is CareerAction.Accept or CareerAction.Decline);
            bool closed=known.Any(e=>Notice(e).Statement is CareerStatement.Declined or CareerStatement.Expired or CareerStatement.Refused or CareerStatement.Ended);bool employed=(c.Initial||known.Any(e=>Notice(e).Statement==CareerStatement.Agreed))&&!known.Any(e=>Notice(e).Statement==CareerStatement.Ended);
            bool valid=offer!=null&&t<offer.At+OfferSeconds&&!answered&&!closed;
            string status=employed?"所属・直属を認識":(known.LastOrDefault() is {} last?Notice(last).Statement:CareerStatement.NoAgreement) switch{CareerStatement.Offered=>answered?"回答発送・成立通知待ち":valid?"仕官条件を確認・回答可能":"回答期限を経過",CareerStatement.Declined=>"辞退を確認",CareerStatement.Expired=>"期限切れを確認",CareerStatement.Refused=>"不承諾を確認",CareerStatement.Ended=>"辞去成立を確認",_=>"申し出発送・返答未確認"};
            if(own.Any(q=>q.Input.Action==CareerAction.RequestRelease)&&!known.Any(e=>e.Kind==CareerEventKind.Released))status=known.Any(e=>e.Kind==CareerEventKind.ReleaseWaiting)?"引継ぎの返報待ち":"辞去願い発送・返答未確認";
            cards.Add(new(c.Id,c.Principal,Name(w,c.Principal)+"："+status,valid,valid,employed&&FreeOfOffice(w)&&!own.Any(q=>q.Input.Action==CareerAction.RequestRelease)));
            if(!c.Initial&&!closed&&!employed)pending=true;
        }
        string role=house==null?"浪人":lord==null?NewGameWorld.RoleName(p.InitialRole??StartTemplate.Daimyo):"家臣・随行";
        return new(role,house,lord,house==null&&lord==null&&!pending&&FreeOfOffice(w),cards.ToArray());
    }
    public static EntityId[] LocalPrincipals(ContractWorld w)
    {
        if(!NewGameWorld.Enabled(w))return [];
        var own=w.Locations.Single(l=>l.Actor==w.PlayerControl);if(own.Kind!=LocationKind.AtSite)return [];
        return w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Head)).Where(id=>id!=w.PlayerControl&&w.Characters.Single(p=>p.Id==id).Life==LifeState.Adult&&w.Locations.Any(l=>l.Actor==id&&l.Kind==LocationKind.AtSite&&l.Site==own.Site)).OrderBy(id=>id.Value,StringComparer.Ordinal).ToArray();
    }
    static bool LocalAt(ContractWorld w,ServiceCase c,long at)=>RegionalCampaign.ActorPointAt(w,w.PlayerControl,at)==BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==c.Site).Position&&RegionalCampaign.ActorPointAt(w,c.Principal,at)==RegionalCampaign.ActorPointAt(w,w.PlayerControl,at);
    static ServiceCase NewCase(ContractWorld w,ServiceInput input)
    {
        Need(input.Principal!=null&&LocalPrincipals(w).Contains(input.Principal.Value),"到着した拠点で会える勢力代表を選んでください");var f=w.NewGame!.Start.Factions.Single(f=>f.Head==input.Principal.Value.Value);var loc=w.Locations.Single(l=>l.Actor==w.PlayerControl);
        return new(input.Id,input.Principal.Value,new(f.Id),loc.Site!.Value,false);
    }
    internal static void Dispatch(ContractWorld w,ServiceInput input)
    {
        var s=State(w);var v=View(w);ServiceCase c;
        if(input.Action==CareerAction.Apply){Need(input.Case==null&&v.CanApply,"未解決の申し出や所属があります");c=NewCase(w,input);s=s with{Cases=[..s.Cases,c]};}
        else {Need(input.Principal==null&&input.Case!=null,"本人の案件を選んでください");c=Cases(w).Single(c=>c.Id==input.Case);if(!s.Cases.Any(q=>q.Id==c.Id))s=s with{Cases=[..s.Cases,c]};var card=v.Cases.Single(q=>q.Case==c.Id);Need(input.Action switch{CareerAction.Accept=>card.CanAccept,CareerAction.Decline=>card.CanDecline,CareerAction.RequestRelease=>card.CanRelease,CareerAction.Inquire=>true,_=>false},"開封した条件・本人の所属を確認してください");}
        var message=RequestId(input.Id);var q=new ServiceRequest(input,c.Id,w.ElapsedSeconds,w.CommitSequence,message);w.Service=s with{Requests=[..s.Requests,q]};
        long? due=input.Action==CareerAction.Accept?Event(w,c.Id,CareerEventKind.Offered)!.At+OfferSeconds-1:null;
        MessageFlow.Send(w.Messaging,new(message,c.Id.Value,"service-request",w.PlayerControl.Value,c.Principal.Value,Text(input),c.Site.Value,w.ElapsedSeconds,due),w.ElapsedSeconds,1);
    }
    static void Announce(ContractWorld w,ServiceCase c,CareerEventKind kind,CareerStatement statement,string related,long? effective=null,long? until=null)
    {
        string id=NoticeId(c.Id,kind,related);var notice=new CareerNotice(c.Id,statement,effective,until,related);MessageFlow.Send(w.Messaging,new(id,c.Id.Value,"service-notice",c.Principal.Value,w.PlayerControl.Value,Text(notice),c.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];w.Service=w.Service! with{Events=[..w.Service.Events,new(WorldSession.Allocate(w,"service-event"),c.Id,kind,w.ElapsedSeconds,w.CommitSequence,id,related)]};
    }
    static bool Received(ContractWorld w,string message,ServiceCase c,long at)=>MessageFlow.LearnedAt(w.Messaging,message,c.Principal.Value) is {} t&&t<=at;
    static bool ReportsKnown(ContractWorld w,ServiceCase c,long at,long rev)
    {
        var join=Joined(w,c);if(join==null)return false;
        return w.Requests.Where(q=>q.Intent.Actor==c.Principal&&q.Intent.Executor==w.PlayerControl&&q.CommitSequence>=join.Revision&&q.CommitSequence<=rev&&q.CreatedAt<=at).All(q=>{var task=w.Tasks.Single(t=>t.RequestId==q.Id);var msg=WorldSession.Child("message",task.Id,"report").Value;return MessageFlow.LearnedAt(w.Messaging,msg,c.Principal.Value) is {} known&&known<=at;});
    }
    static (CareerStatement State,long? Effective) Status(ContractWorld w,ServiceCase c,long at,long rev)
    {
        if(End(w,c.Id) is {} end&&Before(end,at,rev))return(CareerStatement.Ended,end.At);
        if(Active(w,c,at,rev))return(CareerStatement.Agreed,Joined(w,c)!.At);
        var last=State(w).Events.LastOrDefault(e=>e.Case==c.Id&&Before(e,at,rev)&&e.Kind is CareerEventKind.Declined or CareerEventKind.Expired or CareerEventKind.Refused);
        return(last?.Kind switch{CareerEventKind.Declined=>CareerStatement.Declined,CareerEventKind.Expired=>CareerStatement.Expired,CareerEventKind.Refused=>CareerStatement.Refused,_=>CareerStatement.NoAgreement},null);
    }
    internal static void Update(ContractWorld w)
    {
        if(!NewGameWorld.Enabled(w)||w.Service==null)return;
        foreach(var c in w.Service.Cases){
            var requests=w.Service.Requests.Where(q=>q.Case==c.Id).ToArray();var app=requests.SingleOrDefault(q=>q.Input.Action==CareerAction.Apply);
            if(app!=null&&Received(w,app.Message,c,w.ElapsedSeconds)&&Event(w,c.Id,CareerEventKind.Offered)==null&&Event(w,c.Id,CareerEventKind.Refused)==null){if(Cases(w).Any(other=>Active(w,other,w.ElapsedSeconds,w.CommitSequence)))Announce(w,c,CareerEventKind.Refused,CareerStatement.Refused,app.Message);else Announce(w,c,CareerEventKind.Offered,CareerStatement.Offered,app.Message,until:w.ElapsedSeconds+OfferSeconds);}
            var offer=Event(w,c.Id,CareerEventKind.Offered);bool resolved=c.Initial||w.Service.Events.Any(e=>e.Case==c.Id&&e.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Refused or CareerEventKind.Expired);
            var answer=requests.SingleOrDefault(q=>q.Input.Action is CareerAction.Accept or CareerAction.Decline&&Received(w,q.Message,c,w.ElapsedSeconds));
            if(!resolved&&offer!=null){if(answer?.Input.Action==CareerAction.Decline)Announce(w,c,CareerEventKind.Declined,CareerStatement.Declined,answer.Message);else if(answer!=null&&w.ElapsedSeconds<offer.At+OfferSeconds)Announce(w,c,CareerEventKind.Joined,CareerStatement.Agreed,answer.Message,w.ElapsedSeconds);else if(w.ElapsedSeconds>=offer.At+OfferSeconds)Announce(w,c,CareerEventKind.Expired,CareerStatement.Expired,answer?.Message??offer.Message);}
            var release=requests.SingleOrDefault(q=>q.Input.Action==CareerAction.RequestRelease&&Received(w,q.Message,c,w.ElapsedSeconds));
            if(release!=null&&Active(w,c,w.ElapsedSeconds,w.CommitSequence)){if(ReportsKnown(w,c,w.ElapsedSeconds,w.CommitSequence))Announce(w,c,CareerEventKind.Released,CareerStatement.Ended,release.Message,w.ElapsedSeconds);else if(Event(w,c.Id,CareerEventKind.ReleaseWaiting)==null)Announce(w,c,CareerEventKind.ReleaseWaiting,CareerStatement.AwaitingReports,release.Message);}
            foreach(var query in requests.Where(q=>q.Input.Action==CareerAction.Inquire&&Received(w,q.Message,c,w.ElapsedSeconds)&&!w.Service.Events.Any(e=>e.Kind==CareerEventKind.StatusSent&&e.Related==q.Message))){var status=Status(w,c,w.ElapsedSeconds,w.CommitSequence);Announce(w,c,CareerEventKind.StatusSent,status.State,query.Message,status.Effective);}
        }
    }
    internal static long? NextBoundary(ContractWorld w,long target)=>w.Service?.Events.Where(e=>e.Kind==CareerEventKind.Offered&&!w.Service.Events.Any(x=>x.Case==e.Case&&x.Kind is CareerEventKind.Joined or CareerEventKind.Declined or CareerEventKind.Expired or CareerEventKind.Refused)).Select(e=>e.At+OfferSeconds).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    internal static void RecordOpening(ContractWorld w,EntityId actor,string id){if(actor!=w.PlayerControl||w.Service==null||!w.Service.Events.Any(e=>e.Message==id)||w.Service.Openings.Any(o=>o.Message==id))return;w.Service=w.Service with{Openings=[..w.Service.Openings,new(id,w.ElapsedSeconds,w.CommitSequence)]};}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>w.Service?.Cases.FirstOrDefault(c=>c.Id.Value==m.CorrelationId&&m.Kind is "service-request" or "service-notice") is {} c?NewGameWorld.Node(c.Site.Value):null;
    internal static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Service?.Requests.Any(q=>q.Input.Id==id)==true;
    internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,string kind)
    {
        var rows=original.ToList();var initial=Initial(w);if(initial!=null&&End(w,initial.Id) is {} e)for(int n=0;n<rows.Count;n++)if(rows[n].Subject==w.PlayerControl)rows[n]=rows[n] with{Until=e.At,Revision=e.Revision};
        foreach(var c in State(w).Cases.Where(c=>!c.Initial))if(Event(w,c.Id,CareerEventKind.Joined) is {} join){var end=End(w,c.Id);rows.Add(new(new(kind+":service/"+Hash(c.Id.Value)),w.PlayerControl,kind=="membership"?c.House:c.Principal,kind,NewGameWorld.Basis,join.At,end?.At??long.MaxValue,end?.Revision??join.Revision));}
        return rows.ToArray();
    }
    internal static void Sync(ContractWorld w){if(!NewGameWorld.Enabled(w)||w.Service==null)return;var rel=NewGameWorld.InitialRelations(w.NewGame!);w.Memberships=Relations(w,rel.Members,"membership");w.ServiceBonds=Relations(w,rel.Bonds,"service");}
}
