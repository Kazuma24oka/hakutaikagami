using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum MilitaryAction { Request, Accept, Pickup, Return }
public enum MilitaryEffect { Offered, Permission, PickedUp, Waiting, Returned, Refused, Expired, StatusSent }
public sealed record MilitaryInput(EntityId Id,MilitaryAction Action);
public sealed record MilitaryRequest(MilitaryInput Input,long At,long Revision,string? Message);
public sealed record MilitaryEvent(MilitaryEffect Effect,long At,long Revision,string Source,string Message);
public sealed record MilitaryNotice(MilitaryEffect Effect,string Source,long EffectiveAt,long? Deadline);
public sealed record MilitaryState(int Schema,MilitaryRequest[] Requests,MilitaryEvent[] Events,CareerOpening[] Openings);
public sealed record MilitaryView(string Status,EntityId? Unopened,bool CanRequest,bool CanAccept,bool CanPickup,bool CanReturn,bool Custody);
// A bounded same-life placement loan. Prepared stock and troop definitions are not permissions.
public static partial class CareerMilitary
{
    public const string Ruleset="created-career-military-v1",Contract="same-life-local-command-custody-physical-permission-handover-no-engagement-v1";
    public const long OfferSeconds=120;
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    static EntityId Lord=>RetainerStart.Lord;
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    internal static string RequestMessage(EntityId id)=>"message:military/request/"+Hash(id.Value);
    static string NoticeMessage(MilitaryEffect effect)=>"message:military/notice/"+Hash(Contract+"/"+effect);
    static string Text<T>(T t)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(t));
    static bool Prior(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static MilitaryEvent? Event(ContractWorld w,MilitaryEffect e,long at,long rev)=>w.Military!.Events.SingleOrDefault(x=>x.Effect==e&&Prior(x.At,x.Revision,at,rev));
    internal static bool CustodyAt(ContractWorld w,long at,long? revision=null)=>Enabled(w)&&Event(w,MilitaryEffect.PickedUp,at,revision??w.CommitSequence)!=null&&Event(w,MilitaryEffect.Returned,at,revision??w.CommitSequence)==null;
    public static bool Custody(ContractWorld w)=>CustodyAt(w,w.ElapsedSeconds);
    internal static bool OutstandingAt(ContractWorld w,long at,long rev)=>Enabled(w)&&Event(w,MilitaryEffect.Permission,at,rev)!=null&&Event(w,MilitaryEffect.Returned,at,rev)==null;
    internal static bool DialogueAt(ContractWorld w,long at,long rev)=>Enabled(w)&&(OutstandingAt(w,at,rev)||Event(w,MilitaryEffect.Offered,at,rev)!=null&&Event(w,MilitaryEffect.Permission,at,rev)==null&&Event(w,MilitaryEffect.Refused,at,rev)==null&&Event(w,MilitaryEffect.Expired,at,rev)==null);
    static bool Local(ContractWorld w)=>w.Locations.Any(l=>l.Actor==w.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==ContractFixture.Castle)&&NpcWork.Inputs(w,Lord,w.ElapsedSeconds).LocalWitness;
    static bool Home(ContractWorld w)=>FieldWorld.UnitIds.All(id=>w.Region!.Units.Any(u=>u.Id==id&&u.Position==u.Home&&!u.Route.Any(l=>l.EndAt>w.ElapsedSeconds)));
    static void Commander(ContractWorld w,EntityId actor){var ids=FieldWorld.UnitIds.ToHashSet();w.Units=w.Units.Select(u=>ids.Contains(u.Id)?u with{Commander=actor}:u).ToArray();w.Region=w.Region! with{Units=w.Region.Units.Select(u=>ids.Contains(u.Id)?u with{Commander=actor}:u).ToArray()};}
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020)
    {
        var w=FieldPresence.Create(name,seed);var peace=Career.Create(name,seed);
        w.WorldId=new("world:"+Ruleset);w.Career=peace.Career;w.Military=new(1,[],[],[]);
        w.Memberships=w.Memberships.Where(x=>x.Subject!=w.PlayerControl).ToArray();w.ServiceBonds=w.ServiceBonds.Where(x=>x.Subject!=w.PlayerControl&&x.Target!=w.PlayerControl).ToArray();
        var removed=w.Evidence.Where(e=>e.Id.Value.StartsWith("evidence:created-retainer/")||e.Id.Value.StartsWith("evidence:created-command/")).Select(e=>e.Id).ToHashSet();w.Evidence=w.Evidence.Where(e=>!removed.Contains(e.Id)).ToArray();w.Knowledge=w.Knowledge.Where(k=>!removed.Contains(k.Evidence)).ToArray();
        w.CommandMandates=[];Commander(w,Lord);
        w.Region=w.Region! with{Hostile=false,Sights=[],Units=w.Region.Units.Select(u=>{int i=Array.IndexOf(FieldWorld.UnitIds,u.Id);if(i<0)return u;var p=ContinuousWorld.Point("castle",i*10000,0);return u with{Home=p,Position=p,Goal=p,RouteOrigin=p,Route=[],Phase="standing"};}).ToArray()};
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,peace.Definitions.AuthorityBases.Single(b=>b.Id==Career.Basis)]};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};RegionalBattle.Sync(w);ContractValidation.Validate(w);return w;
    }
    public static MilitaryView View(WorldSession s,EntityId observer)=>View(s.DiagnosticSnapshot(),observer);
    internal static MilitaryView View(ContractWorld w,EntityId observer,long? at=null,long? revision=null)
    {
        if(!Enabled(w)||observer!=w.PlayerControl)return new("未確認",null,false,false,false,false,false);
        long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;var s=w.Military!;
        bool Before(MilitaryRequest q)=>Prior(q.At,q.Revision,t,r);
        bool Known(MilitaryEvent e)=>s.Openings.Any(o=>o.Message==e.Message&&Prior(o.At,o.Revision,t,r));
        bool Quoted(MilitaryEvent e)=>s.Events.Where(q=>q.Effect==MilitaryEffect.StatusSent&&Prior(q.At,q.Revision,t,r)&&Known(q)).Any(q=>ReadStatus(w,q).Original==e.Message);
        var known=s.Events.Where(e=>e.Effect!=MilitaryEffect.StatusSent&&Prior(e.At,e.Revision,t,r)&&(Known(e)||Quoted(e))).ToArray();var last=known.LastOrDefault();
        var unopened=s.Events.FirstOrDefault(e=>Prior(e.At,e.Revision,t,r)&&!Known(e)&&w.Messaging.Attempts.Any(a=>a.MessageId==e.Message&&a.Status==AttemptStatus.Delivered&&a.ReceiverId==observer.Value&&a.FinishedAt<=t));
        bool answered=s.Requests.Any(q=>q.Input.Action==MilitaryAction.Accept&&Before(q)),picked=s.Requests.Any(q=>q.Input.Action==MilitaryAction.Pickup&&Before(q)),returned=known.Any(e=>e.Effect==MilitaryEffect.Returned);
        bool permission=known.Any(e=>e.Effect==MilitaryEffect.Permission)&&!returned;
        var offer=known.SingleOrDefault(e=>e.Effect==MilitaryEffect.Offered);
        bool returning=s.Requests.Any(q=>q.Input.Action==MilitaryAction.Return&&Before(q));
        string status=returned?"指揮返還の通知を確認":returning?"指揮返還願い発送・帰還/兵糧/返報の確認待ち":picked?"主家兵100を現地引受・配置/保持/帰還のみ":last?.Effect switch{MilitaryEffect.Offered=>answered?"軍務回答発送・許可未確認":"条件：主家兵100 / 配置・保持・帰還 / 回答到達は"+(last.At+OfferSeconds)+"秒未満",MilitaryEffect.Permission=>"軍務許可を確認・指揮は未引受",MilitaryEffect.Waiting=>"帰還・物資返還・現況返報の確認待ち",MilitaryEffect.Refused=>"軍務不承諾を確認",MilitaryEffect.Expired=>"軍務条件の期限切れを確認",_=>s.Requests.Any(Before)?"軍務申し出発送・結果未確認":"軍務は別途申し出が必要"};
        if(last!=null&&!Known(last)&&Quoted(last))status+="（照会引用）";
        if(known.Length==0&&s.Events.Any(q=>q.Effect==MilitaryEffect.StatusSent&&Prior(q.At,q.Revision,t,r)&&Known(q)&&ReadStatus(w,q).Original==null))status="照会返書：先方に軍務条件の記録なし（返書作成時点）";
        return new(status,unopened==null?null:new(unopened.Message),!s.Requests.Any(Before)&&Career.View(w,observer,t,r).Cases.Any(c=>c.CanRelease),offer!=null&&t<offer.At+OfferSeconds&&!answered,permission&&!picked,picked&&!returned&&!s.Requests.Any(q=>q.Input.Action==MilitaryAction.Return&&Before(q)),picked&&!returned);
    }
    static MilitaryEvent Announce(ContractWorld w,MilitaryEffect effect,string source)
    {
        var id=NoticeMessage(effect);var body=new MilitaryNotice(effect,source,w.ElapsedSeconds,effect==MilitaryEffect.Offered?w.ElapsedSeconds+OfferSeconds:null);
        MessageFlow.Send(w.Messaging,new(id,id,"military-notice",Lord.Value,w.PlayerControl.Value,Text(body),ContractFixture.Castle.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];
        var e=new MilitaryEvent(effect,w.ElapsedSeconds,w.CommitSequence,source,id);w.Military=w.Military! with{Events=[..w.Military.Events,e]};return e;
    }
    internal static void RecordOpening(ContractWorld w,EntityId actor,string id){if(!Enabled(w)||actor!=w.PlayerControl||!w.Military!.Events.Any(e=>e.Message==id)||w.Military.Openings.Any(o=>o.Message==id))return;w.Military=w.Military with{Openings=[..w.Military.Openings,new(id,w.ElapsedSeconds,w.CommitSequence)]};}
    internal static void Dispatch(ContractWorld w,MilitaryInput input)
    {
        var view=View(w,w.PlayerControl);if(!Enum.IsDefined(input.Action)||!EntityId.IsValid(input.Id.Value))throw new InvalidDataException("Unknown military input");
        if(input.Action==MilitaryAction.Request&&(!view.CanRequest||!Local(w))||input.Action==MilitaryAction.Accept&&!view.CanAccept||input.Action==MilitaryAction.Pickup&&(!view.CanPickup||!Local(w)||!Home(w)||!Career.ActiveAt(w,Lord,w.ElapsedSeconds))||input.Action==MilitaryAction.Return&&!view.CanReturn)throw new InvalidDataException("Known military conditions/local custody missing");
        string? message=input.Action==MilitaryAction.Pickup?null:RequestMessage(input.Id);var q=new MilitaryRequest(input,w.ElapsedSeconds,w.CommitSequence,message);w.Military=w.Military! with{Requests=[..w.Military.Requests,q]};
        if(input.Action==MilitaryAction.Pickup){Announce(w,MilitaryEffect.PickedUp,input.Id.Value);Commander(w,w.PlayerControl);w.CommandMandates=[new(CreatedCampaign.Mandate,Lord,w.PlayerControl,CreatedCampaign.Army,FieldWorld.UnitIds,[RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat],CreatedCampaign.Basis,w.ElapsedSeconds,long.MaxValue)];return;}
        long? deadline=input.Action==MilitaryAction.Accept?Event(w,MilitaryEffect.Offered,w.ElapsedSeconds,w.CommitSequence)!.At+OfferSeconds-1:null;
        MessageFlow.Send(w.Messaging,new(message!,input.Id.Value,"military-request",w.PlayerControl.Value,Lord.Value,Text(input),ContractFixture.Castle.Value,w.ElapsedSeconds,deadline),w.ElapsedSeconds,1);
    }
    static bool ReturnReady(ContractWorld w)=>Local(w)&&Home(w)&&!FieldPresence.PlayerDeployed(w)&&FieldSupply.Carried(w).Quantity==0&&w.Engagement!.Reports.Any(r=>r.At>=Math.Max(w.Military!.Events.Single(e=>e.Effect==MilitaryEffect.PickedUp).At,w.Supply!.Sorties.LastOrDefault()?.Until??0)&&MessageFlow.Knows(w.Messaging,r.Message,Lord.Value));
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w))return;bool Has(MilitaryEffect e)=>w.Military!.Events.Any(x=>x.Effect==e);
        foreach(var q in w.Military!.Requests.Where(q=>q.Message!=null&&MessageFlow.Knows(w.Messaging,q.Message,Lord.Value)).ToArray()){
            if(q.Input.Action==MilitaryAction.Request&&!Has(MilitaryEffect.Offered)&&!Has(MilitaryEffect.Refused)){if(Career.ActiveAt(w,Lord,w.ElapsedSeconds)&&Career.WorkReportsKnown(w,out _))Announce(w,MilitaryEffect.Offered,q.Message!);else Announce(w,MilitaryEffect.Refused,q.Message!);}
            if(q.Input.Action==MilitaryAction.Accept&&!Has(MilitaryEffect.Permission)&&!Has(MilitaryEffect.Expired)&&!Has(MilitaryEffect.Refused)){var offer=Event(w,MilitaryEffect.Offered,w.ElapsedSeconds,w.CommitSequence);if(offer!=null&&w.ElapsedSeconds<offer.At+OfferSeconds&&Career.ActiveAt(w,Lord,w.ElapsedSeconds))Announce(w,MilitaryEffect.Permission,q.Message!);else Announce(w,MilitaryEffect.Expired,q.Message!);}
            if(q.Input.Action==MilitaryAction.Return&&!Has(MilitaryEffect.Returned)){if(ReturnReady(w)){var e=Announce(w,MilitaryEffect.Returned,q.Message!);w.CommandMandates=w.CommandMandates!.Select(m=>m with{Until=e.At}).ToArray();Commander(w,Lord);}else if(!Has(MilitaryEffect.Waiting))Announce(w,MilitaryEffect.Waiting,q.Message!);}
        }
        var pending=Event(w,MilitaryEffect.Offered,w.ElapsedSeconds,w.CommitSequence);if(pending!=null&&!Has(MilitaryEffect.Permission)&&!Has(MilitaryEffect.Expired)&&w.ElapsedSeconds>=pending.At+OfferSeconds)Announce(w,MilitaryEffect.Expired,pending.Message);
        UpdateInquiries(w);
    }
    internal static long? NextBoundary(ContractWorld w,long target){if(!Enabled(w))return null;var e=Event(w,MilitaryEffect.Offered,w.ElapsedSeconds,w.CommitSequence);long due=(e?.At??0)+OfferSeconds;return e!=null&&!w.Military!.Events.Any(x=>x.Effect is MilitaryEffect.Permission or MilitaryEffect.Expired)&&due>w.ElapsedSeconds&&due<=target?due:null;}
}
