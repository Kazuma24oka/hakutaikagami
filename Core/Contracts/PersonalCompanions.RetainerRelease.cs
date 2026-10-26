using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record RetainerReleaseCard(EntityId Request,EntityId Person,string Status,bool KnownReleased);
public static partial class PersonalCompanions
{
    public const string RetainerReleasePolicy="local-companions-consented-old-lord-release-physical-notice-v4",ReleaseNoticeKind="retainer-release-notice";
    public static bool PersonalServiceEnabled(ContractWorld w)=>w.NewGame?.CompanionPolicy is PersonalServicePolicy or RetainerReleasePolicy;
    static bool ReleaseEnabled(ContractWorld w)=>w.NewGame?.CompanionPolicy==RetainerReleasePolicy;
    static bool ReleaseEvent(CompanionAction a)=>a is CompanionAction.LordReleaseApproved or CompanionAction.LordReleaseRefused or CompanionAction.LordReleaseReceived or CompanionAction.LordRefusalReceived;
    static string ReleaseKey(CompanionOrder q)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(q.Input.Id.Value));
    static string ReleaseMessage(CompanionOrder q,string stage)=>"message:rr/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(q.Input.Id.Value+"/"+stage));
    static EntityId ReleaseDecision(CompanionOrder q,CompanionAction action)=>new("decision:retainer-release/"+ReleaseKey(q)+"/"+action);
    static CompanionOrder[] ReleaseRequests(ContractWorld w)=>Orders(w).Where(q=>q.Input.Action==CompanionAction.RequestLordRelease).ToArray();
    static CompanionOrder? ReleaseChild(ContractWorld w,CompanionOrder q,params CompanionAction[] actions)=>Orders(w).SingleOrDefault(o=>o.Input.Destination==q.Input.Id.Value&&actions.Contains(o.Input.Action));
    static EntityId OldLord(ContractWorld w,EntityId person)=>new(w.NewGame!.Start.People.Single(p=>p.Id==person.Value).Lord??throw new InvalidDataException("開始時の直属主君がありません"));
    static EntityId LordHome(ContractWorld w,EntityId person)=>new(w.NewGame!.Start.Factions.Single(f=>f.Head==OldLord(w,person).Value).Home);
    static bool AtReleaseSite(ContractWorld w,EntityId actor,EntityId site,long at)=>RegionalCampaign.RoadActorPointAt(w,actor,at)==BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position;
    static bool Arrived(ContractWorld w,string message,EntityId actor,long at)=>w.Messaging.Attempts.Any(a=>a.MessageId==message&&a.ReceiverId==actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=at);
    static bool ReleaseBefore(CompanionOrder o,long at,long rev)=>Before(o,at,rev);
    internal static bool ReleasedFromOldLord(ContractWorld w,EntityId person,long at,long rev)=>ReleaseEnabled(w)&&Orders(w).Any(o=>o.Input.Person==person&&o.Input.Action==CompanionAction.LordReleaseReceived&&ReleaseBefore(o,at,rev));
    static CompanionOrder InquiryParent(ContractWorld w,CompanionOrder inquiry)=>ReleaseRequests(w).Last(q=>q.Input.Person==inquiry.Input.Person&&Before(q,inquiry.At,inquiry.Revision));
    static string InquiryMessage(CompanionOrder q,CompanionOrder inquiry)=>ReleaseMessage(q,"status/"+ReleaseKey(inquiry));
    static bool NoticeKnown(ContractWorld w,CompanionOrder q,long at,long rev)=>MessageFlow.LearnedAt(w.Messaging,ReleaseMessage(q,"notice"),w.PlayerControl.Value) is {} learned&&learned<=at||Orders(w).Any(o=>o.Input.Action==CompanionAction.InquireLordRelease&&Before(o,at,rev)&&InquiryParent(w,o).Input.Id==q.Input.Id&&MessageFlow.LearnedAt(w.Messaging,InquiryMessage(q,o),w.PlayerControl.Value) is {} time&&time<=at);
    static bool ReleaseKnown(ContractWorld w,EntityId person,long at,long rev)=>ReleaseRequests(w).Where(q=>q.Input.Person==person&&ReleaseBefore(q,at,rev)).Any(q=>ReleasedFromOldLord(w,person,at,rev)&&NoticeKnown(w,q,at,rev)&&ReleaseChild(w,q,CompanionAction.LordReleaseReceived)!=null);
    static void CheckReleaseInquiry(ContractWorld w,CompanionOrder o)
    {Need(ReleaseEnabled(w)&&o.Input.Amount==0,"この保存では辞去結果を問い合わせられません");var q=InquiryParent(w,o);var r=ReleaseChild(w,q,CompanionAction.LordReleaseReceived,CompanionAction.LordRefusalReceived);Need(r!=null&&Before(r,o.At,o.Revision),"本人もまだ旧主の返答を待っています");}
    static void SendReleaseInquiry(ContractWorld w,CompanionOrder o)
    {var q=InquiryParent(w,o);var r=ReleaseChild(w,q,CompanionAction.LordReleaseReceived,CompanionAction.LordRefusalReceived)!;var id=InquiryMessage(q,o);var original=w.Messaging.Messages.Single(m=>m.Id==ReleaseMessage(q,"notice"));MessageFlow.Send(w.Messaging,new(id,q.Input.Id.Value,ReleaseNoticeKind,q.Input.Person.Value,w.PlayerControl.Value,original.Body,o.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];}

    static bool ReleasePending(ContractWorld w,EntityId person,long at,long rev)=>ReleaseEnabled(w)&&ReleaseRequests(w).Where(q=>q.Input.Person==person&&ReleaseBefore(q,at,rev)).Any(q=>!Orders(w).Any(o=>o.Input.Destination==q.Input.Id.Value&&o.Input.Action is CompanionAction.LordReleaseReceived or CompanionAction.LordRefusalReceived&&ReleaseBefore(o,at,rev)));
    static bool ReleaseReportsReady(ContractWorld w,EntityId person,long at,long rev)
    {
        var lord=OldLord(w,person);return w.Requests.Where(r=>r.Intent.Actor==lord&&r.Intent.Executor==person&&(r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<rev)).All(r=>w.Tasks.Any(t=>t.RequestId==r.Id&&t.FinishedAt<=at&&MessageFlow.LearnedAt(w.Messaging,WorldSession.Child("message",t.Id,"report").Value,lord.Value) is {} known&&known<=at));
    }
    static bool HasReleaseWork(ContractWorld w,EntityId person,long at,long rev)=>Orders(w).Any(q=>q.Input.Person==person&&q.Input.Action==CompanionAction.PrivateInspect&&Before(q,at,rev)&&q.Task!=null&&w.Tasks.Any(t=>t.RequestId==q.Task.RequestId&&t.Execution==ExecutionPhase.Completed&&t.FinishedAt<=at)&&w.Companions!.Reports.Any(r=>r.Order==q.Input.Id&&(r.At<at||r.At==at&&r.Revision<rev)&&MessageFlow.LearnedAt(w.Messaging,r.Message,w.PlayerControl.Value) is {} learned&&learned<=at));
    static bool CanAskRelease(ContractWorld w,EntityId person,long at,long rev)
    {
        if(!ReleaseEnabled(w)||ReleasePending(w,person,at,rev)||ReleasedFromOldLord(w,person,at,rev)||!HasReleaseWork(w,person,at,rev)||!CommissionContact(w,person,at,rev))return false;
        var p=w.NewGame!.Start.People.Single(p=>p.Id==person.Value);return p.Lord!=null&&p.Lord!=w.PlayerControl.Value&&p.Faction!=null&&w.NewGame.Start.Factions.Any(f=>f.Head==p.Lord)&&w.Characters.Single(c=>c.Id==person) is {Life:LifeState.Adult,WillObey:true}&&!w.Offices.Any(o=>o.Subject==person&&o.From<=at&&at<o.Until)&&!NpcArmies.CommanderBusyAt(w,person,at,rev)&&SiteAt(w,person,at,rev)!=null;
    }
    static void SendRelease(ContractWorld w,CompanionOrder q,string stage,EntityId sender,EntityId receiver,EntityId site,string body)
    {string id=ReleaseMessage(q,stage);MessageFlow.Send(w.Messaging,new(id,q.Input.Id.Value,"retainer-release-"+stage,sender.Value,receiver.Value,body,site.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];}
    internal static string? ReleaseAddress(ContractWorld w,MessageEnvelope m)
    {var inquiry=Orders(w).FirstOrDefault(o=>o.Input.Action==CompanionAction.InquireLordRelease&&m.Id==InquiryMessage(InquiryParent(w,o),o));if(inquiry!=null)return NewGameWorld.Node(inquiry.Site.Value);var q=ReleaseRequests(w).SingleOrDefault(q=>m.Id==ReleaseMessage(q,"request")||m.Id==ReleaseMessage(q,"reply")||m.Id==ReleaseMessage(q,"notice"));return q==null?null:NewGameWorld.Node((m.Id==ReleaseMessage(q,"request")?LordHome(w,q.Input.Person):q.Site).Value);}
    static (CompanionOrder Request,CompanionAction Action)? NextRelease(ContractWorld w)
    {
        if(!ReleaseEnabled(w))return null;
        foreach(var q in ReleaseRequests(w)){
            var reply=ReleaseChild(w,q,CompanionAction.LordReleaseApproved,CompanionAction.LordReleaseRefused);var person=q.Input.Person;var lord=OldLord(w,person);
            if(reply==null){if(Arrived(w,ReleaseMessage(q,"request"),lord,w.ElapsedSeconds)&&AtReleaseSite(w,lord,LordHome(w,person),w.ElapsedSeconds)&&SiteAt(w,lord,w.ElapsedSeconds,w.CommitSequence+1)!=null&&ReleaseReportsReady(w,person,w.ElapsedSeconds,w.CommitSequence+1))return(q,w.Characters.Single(c=>c.Id==lord).WillObey?CompanionAction.LordReleaseApproved:CompanionAction.LordReleaseRefused);}
            else if(ReleaseChild(w,q,CompanionAction.LordReleaseReceived,CompanionAction.LordRefusalReceived)==null&&Arrived(w,ReleaseMessage(q,"reply"),person,w.ElapsedSeconds)&&AtReleaseSite(w,person,q.Site,w.ElapsedSeconds)&&(reply.Input.Action==CompanionAction.LordReleaseRefused||ReleaseReportsReady(w,person,w.ElapsedSeconds,w.CommitSequence+1)))return(q,reply.Input.Action==CompanionAction.LordReleaseApproved?CompanionAction.LordReleaseReceived:CompanionAction.LordRefusalReceived);
        }return null;
    }
    internal static long? NextReleaseBoundary(ContractWorld w,long target)=>NextRelease(w)!=null&&w.ElapsedSeconds<target?w.ElapsedSeconds+1:null;
    internal static void UpdateRelease(ContractWorld w)
    {
        if(w.Paused||Orders(w).LastOrDefault()?.Revision==w.CommitSequence||NextRelease(w) is not {} next)return;
        var q=next.Request;var action=next.Action;var receiver=action is CompanionAction.LordReleaseApproved or CompanionAction.LordReleaseRefused?OldLord(w,q.Input.Person):q.Input.Person;string incoming=ReleaseMessage(q,receiver==q.Input.Person?"reply":"request");
        if(!MessageFlow.Knows(w.Messaging,incoming,receiver.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(incoming,receiver.Value,w.ElapsedSeconds,incoming)];
        var o=new CompanionOrder(new(ReleaseDecision(q,action),q.Input.Person,action,0,q.Input.Id.Value),w.ElapsedSeconds,w.CommitSequence,q.Site,null,null);w.Companions=w.Companions! with{Orders=[..Orders(w),o]};
        if(receiver!=q.Input.Person)SendRelease(w,q,"reply",receiver,q.Input.Person,q.Site,action==CompanionAction.LordReleaseApproved?"辞去を承諾する。引継ぎを終えて離れてよい。":"今回は辞去を承諾しない。");
        else SendRelease(w,q,"notice",q.Input.Person,w.PlayerControl,q.Site,action==CompanionAction.LordReleaseReceived?"旧主の承諾を受け、辞去が整いました。新しい主従や所属の話は、別に相談しましょう。":"旧主の承諾が得られず、今は元の所属と直属のままです。");
    }
    static void CheckReleaseEvent(ContractWorld w,CompanionOrder o)
    {
        Need(ReleaseEnabled(w)&&o.Input.Amount==0&&o.Task==null&&o.LeaderTask==null&&o.At>=0&&o.At<=w.ElapsedSeconds&&o.Revision>0&&o.Revision<=w.CommitSequence,"辞去の出来事の版・時点が不正です");var q=ReleaseRequests(w).Single(q=>q.Input.Id.Value==o.Input.Destination);var a=o.Input.Action;Need(o.Input.Person==q.Input.Person&&o.Input.Id==ReleaseDecision(q,a)&&o.Site==q.Site&&o.Revision>q.Revision&&o.At>q.At,"辞去の原本が違います");
        bool decision=a is CompanionAction.LordReleaseApproved or CompanionAction.LordReleaseRefused;var actor=decision?OldLord(w,q.Input.Person):q.Input.Person;string incoming=ReleaseMessage(q,decision?"request":"reply");Need(Arrived(w,incoming,actor,o.At)&&AtReleaseSite(w,actor,decision?LordHome(w,q.Input.Person):q.Site,o.At)&&MessageFlow.LearnedAt(w.Messaging,incoming,actor.Value) is {} learned&&learned<=o.At,"本人が原本を現地受領していません");
        if(decision)Need(SiteAt(w,actor,o.At,o.Revision)!=null&&(a==CompanionAction.LordReleaseApproved)==w.Characters.Single(c=>c.Id==actor).WillObey&&ReleaseReportsReady(w,q.Input.Person,o.At,o.Revision),"旧主の判断・引継ぎ条件が違います");else{var r=ReleaseChild(w,q,CompanionAction.LordReleaseApproved,CompanionAction.LordReleaseRefused);Need(r!=null&&r.Revision<o.Revision&&r.At<o.At&&(a==CompanionAction.LordReleaseReceived)==(r.Input.Action==CompanionAction.LordReleaseApproved)&&(a!=CompanionAction.LordReleaseReceived||ReleaseReportsReady(w,q.Input.Person,o.At,o.Revision)),"旧主の承諾がまだ本人へ届いていません");}
        Need(!FactionStances.OtherIdUsed(w,o.Input.Id,includeCompanions:false)&&!FactionStances.OwnIdUsed(w,o.Input.Id),"辞去IDが競合しています");
    }
    static void ValidateReleaseLetters(ContractWorld w)
    {
        int count=0;foreach(var q in ReleaseRequests(w)){
            void Paper(string stage,EntityId sender,EntityId receiver,EntityId site,long at,string body){string id=ReleaseMessage(q,stage);var expected=new MessageEnvelope(id,q.Input.Id.Value,"retainer-release-"+stage,sender.Value,receiver.Value,body,site.Value,at);Need(w.Messaging.Messages.Any(m=>m==expected)&&w.SealedMessages.Contains(new(id)),"辞去書状の本文・当事者・宛先・発送時点が違います");count++;}
            Paper("request",q.Input.Person,OldLord(w,q.Input.Person),LordHome(w,q.Input.Person),q.At,"役目を引き継いだうえで辞去を願います。");
            var reply=ReleaseChild(w,q,CompanionAction.LordReleaseApproved,CompanionAction.LordReleaseRefused);if(reply!=null)Paper("reply",OldLord(w,q.Input.Person),q.Input.Person,q.Site,reply.At,reply.Input.Action==CompanionAction.LordReleaseApproved?"辞去を承諾する。引継ぎを終えて離れてよい。":"今回は辞去を承諾しない。");
            var received=ReleaseChild(w,q,CompanionAction.LordReleaseReceived,CompanionAction.LordRefusalReceived);if(received!=null)Paper("notice",q.Input.Person,w.PlayerControl,q.Site,received.At,received.Input.Action==CompanionAction.LordReleaseReceived?"旧主の承諾を受け、辞去が整いました。新しい主従や所属の話は、別に相談しましょう。":"旧主の承諾が得られず、今は元の所属と直属のままです。");
        }foreach(var inquiry in Orders(w).Where(o=>o.Input.Action==CompanionAction.InquireLordRelease)){var q=InquiryParent(w,inquiry);var original=w.Messaging.Messages.Single(m=>m.Id==ReleaseMessage(q,"notice"));var expected=new MessageEnvelope(InquiryMessage(q,inquiry),q.Input.Id.Value,ReleaseNoticeKind,q.Input.Person.Value,w.PlayerControl.Value,original.Body,inquiry.Site.Value,inquiry.At);Need(w.Messaging.Messages.Any(m=>m==expected)&&w.SealedMessages.Contains(new(expected.Id)),"辞去結果の再受渡し原本が違います");count++;}Need(w.Messaging.Messages.Count(m=>m.Kind.StartsWith("retainer-release-",StringComparison.Ordinal))==count,"辞去書状に原本の裏付けがありません");
    }
    internal static TimedRelation[] CloseOldService(ContractWorld w,IEnumerable<TimedRelation> rows)
    {return rows.Select(r=>{var e=Orders(w).SingleOrDefault(o=>o.Input.Person==r.Subject&&o.Input.Action==CompanionAction.LordReleaseReceived);return e==null?r:r with{Until=e.At,Revision=e.Revision};}).ToArray();}
    public static RetainerReleaseCard[] ReleaseView(ContractWorld w)=>!ReleaseEnabled(w)?[]:ReleaseRequests(w).Select(q=>{string message=ReleaseMessage(q,"notice");bool read=NoticeKnown(w,q,w.ElapsedSeconds,w.CommitSequence+1);bool freed=read&&ReleaseChild(w,q,CompanionAction.LordReleaseReceived)!=null;return new RetainerReleaseCard(q.Input.Id,q.Input.Person,read?freed?"本人から辞去成立の通知を確認":"本人から辞去不承諾の通知を確認":"本人が旧主へ申し出・結果の通知待ち",freed);}).ToArray();
}
public sealed partial class WorldSession
{
    public RetainerReleaseCard[] RetainerReleaseView(){lock(gate)return PersonalCompanions.ReleaseView(state);}
}
