using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record PersonalServiceCard(EntityId Person,bool Active,long Since);
public static partial class PersonalCompanions
{
    public const string PersonalServicePolicy="local-companions-demonstrated-work-personal-service-v3";
    public static bool SingleInspectionEnabled(ContractWorld w)=>w.NewGame?.CompanionPolicy is PrivateInspectionPolicy or PersonalServicePolicy;
    static CompanionOrder? PledgeAt(ContractWorld w,EntityId person,long at,long rev)=>Orders(w).Where(q=>q.Input.Person==person&&Before(q,at,rev)&&q.Input.Action is CompanionAction.PledgeService or CompanionAction.ReleaseService).LastOrDefault() is {Input.Action:CompanionAction.PledgeService} q?q:null;
    static bool CanPledge(ContractWorld w,EntityId person,long at,long rev)
    {
        if(w.NewGame?.CompanionPolicy!=PersonalServicePolicy||PledgeAt(w,person,at,rev)!=null||!Active(Orders(w).Where(q=>Before(q,at,rev)).ToArray(),person)||Owed(w,person,at,rev)!=0)return false;
        var p=w.NewGame.Start.People.Single(p=>p.Id==person.Value);if(p.Lord!=null||p.Faction!=null||w.Offices.Any(o=>o.Subject==person&&o.From<=at&&at<o.Until)||w.Characters.Single(c=>c.Id==person) is not {Life:LifeState.Adult,WillObey:true})return false;
        if(!Orders(w).Any(q=>q.Input.Person==person&&Before(q,at,rev)&&q.Task!=null&&w.Tasks.Any(t=>t.RequestId==q.Task.RequestId&&t.Execution==ExecutionPhase.Completed&&t.FinishedAt<=at)&&w.Companions!.Reports.Any(r=>r.Order==q.Input.Id&&(r.At<at||r.At==at&&r.Revision<rev)&&MessageFlow.Knows(w.Messaging,r.Message,w.PlayerControl.Value)&&w.Messaging.Knowledge.Any(k=>k.MessageId==r.Message&&k.CharacterId==w.PlayerControl.Value&&k.LearnedAt<=at))))return false;
        var seen=new HashSet<EntityId>();var pending=new Queue<EntityId>();pending.Enqueue(w.PlayerControl);while(pending.TryDequeue(out var current)){if(current==person)return false;if(!seen.Add(current))continue;foreach(var b in w.ServiceBonds.Where(b=>b.Subject==current&&b.From<=at&&at<b.Until&&b.Revision<rev))pending.Enqueue(b.Target);}return true;
    }
    static void CheckService(ContractWorld w,CompanionOrder q)
    {
        Need(w.NewGame?.CompanionPolicy==PersonalServicePolicy&&q.Input.Amount==0,"この保存で扱える個人の直属合意ではありません");
        if(q.Input.Action==CompanionAction.PledgeService)Need(CanPledge(w,q.Input.Person,q.At,q.Revision),"所属のない本人と仕事の実績・開封した返報・手当を確認してください");
        else Need(PledgeAt(w,q.Input.Person,q.At,q.Revision) is {} p&&q.At>p.At,"有効な直属合意を確認してください。合意直後は少し時間をおいてください");
    }
    internal static TimedRelation[] PersonalServiceRelations(ContractWorld w)
    {
        if(w.NewGame?.CompanionPolicy!=PersonalServicePolicy)return [];
        return Orders(w).Where(q=>q.Input.Action==CompanionAction.PledgeService).Select(q=>{var end=Orders(w).FirstOrDefault(o=>o.Input.Person==q.Input.Person&&o.Input.Action==CompanionAction.ReleaseService&&o.Revision>q.Revision);return new TimedRelation(new("service:personal-pledge/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(q.Input.Id.Value))),q.Input.Person,w.PlayerControl,"service",NewGameWorld.Basis,q.At,end?.At??long.MaxValue,end?.Revision??q.Revision);}).ToArray();
    }
    public static PersonalServiceCard[] PersonalServiceView(ContractWorld w)=>w.NewGame?.CompanionPolicy!=PersonalServicePolicy?[]:Orders(w).Where(q=>q.Input.Action==CompanionAction.PledgeService).Select(q=>q.Input.Person).Distinct().Select(id=>{var q=PledgeAt(w,id,w.ElapsedSeconds,w.CommitSequence+1);return new PersonalServiceCard(id,q!=null,q?.At??Orders(w).Last(q=>q.Input.Person==id&&q.Input.Action==CompanionAction.PledgeService).At);}).ToArray();
    internal static PrivateInspectionReply ConsultService(ContractWorld w,EntityId person,EntityId actor,long rev)
    {
        PrivateInspectionReply No(string t)=>new(w.CommitSequence,false,0,t);
        if(actor!=w.PlayerControl||rev!=w.CommitSequence||w.NewGame?.CompanionPolicy!=PersonalServicePolicy||person==actor||!w.Characters.Any(c=>c.Id==person))return No("この相談は今はできません。");
        var site=SiteAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1);if(site==null||SiteAt(w,person,w.ElapsedSeconds,w.CommitSequence+1)!=site)return No("本人に現地で会って相談してください。");
        if(!CanPledge(w,person,w.ElapsedSeconds,w.CommitSequence+1))return No("今の立場や、これまでの用件と手当を確認してから改めて話しましょう。");
        return new(w.CommitSequence,true,0,"これまでの仕事を踏まえ、あなた個人の直属として加わりましょう。所属や兵、土地はこの合意で移しません。手当の契約は今までどおりです。");
    }
}
public sealed partial class WorldSession
{
    public PersonalServiceCard[] PersonalServiceView(){lock(gate)return PersonalCompanions.PersonalServiceView(state);}
    public PrivateInspectionReply ConsultPersonalService(EntityId person,EntityId actor,long revision){lock(gate)return PersonalCompanions.ConsultService(state,person,actor,revision);}
}
