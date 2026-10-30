using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum CompanionAction { Agree, EndAgreement, Pay, TravelTogether, Inspect, PrivateInspect, PledgeService, ReleaseService, RequestLordRelease, LordReleaseApproved, LordReleaseRefused, LordReleaseReceived, LordRefusalReceived, InquireLordRelease }
public sealed record CompanionInput(EntityId Id,EntityId Person,CompanionAction Action,long Amount=0,string? Destination=null);
public sealed record CompanionOrder(CompanionInput Input,long At,long Revision,EntityId Site,Intent? Task,Intent? LeaderTask);
public sealed record CompanionReport(EntityId Order,string Message,long At,long Revision,EntityId Site);
public sealed record CompanionState(int Schema,string Contract,CompanionOrder[] Orders,CompanionReport[] Reports);
public sealed record CompanionCard(EntityId Person,string Name,string Kind,bool Active,long Owed,string Status,bool Nearby,bool Busy);
public static partial class PersonalCompanions
{
    public const string Policy="local-consenting-companions-private20-per-day-land-tasks-v1",LandRoute="companion-own-land-trip-v1",ReportKind="companion-local-report";
    public const long Day=86400,DailyPay=20;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&Supported(w.NewGame?.CompanionPolicy);
    static CompanionOrder[] Orders(ContractWorld w)=>w.Companions?.Orders??[];
    static bool Before(CompanionOrder q,long at,long rev)=>q.At<at||q.At==at&&q.Revision<rev;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Orders(w).Any(q=>q.Input.Id==id);
    static bool Active(CompanionOrder[] prior,EntityId person)=>prior.LastOrDefault(q=>q.Input.Person==person&&q.Input.Action is CompanionAction.Agree or CompanionAction.EndAgreement)?.Input.Action==CompanionAction.Agree;
    public static long Owed(ContractWorld w,EntityId person,long at,long revision)
    {
        long due=0;CompanionOrder? join=null;foreach(var q in Orders(w).Where(q=>q.Input.Person==person&&Before(q,at,revision))){if(q.Input.Action==CompanionAction.Agree)join=q;else if(q.Input.Action==CompanionAction.EndAgreement&&join!=null){due=checked(due+DailyPay*Math.Max(1,(q.At-join.At+Day-1)/Day));join=null;}if(q.Input.Action is CompanionAction.Agree or CompanionAction.Pay)due=checked(due-q.Input.Amount);}if(join!=null)due=checked(due+DailyPay*(1+(at-join.At)/Day));return due;
    }
    public static long Net(ContractWorld w,EntityId lot,long at=long.MaxValue,long revision=long.MaxValue)
    {long n=0;foreach(var q in Orders(w).Where(q=>Before(q,at,revision)&&q.Input.Action is CompanionAction.Agree or CompanionAction.Pay or CompanionAction.PrivateInspect)){if(lot==NewGameWork.Wallet(w.PlayerControl))n=checked(n-q.Input.Amount);if(lot==NewGameWork.Wallet(q.Input.Person))n=checked(n+q.Input.Amount);}return n;}
    internal static EntityId? SiteAt(ContractWorld w,EntityId actor,long at,long rev)
    {
        if(w.Requests.Any(r=>r.Intent.Executor==actor&&r.CommitSequence<rev&&r.CreatedAt<=at&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at))))return null;
        if(actor==w.PlayerControl&&NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Where(q=>q.Revision<rev&&q.At<=at).OrderBy(q=>q.Revision).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter)?.Input.Action==NewMilitaryAction.Deploy)return null;
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor&&w.Tasks.Any(t=>t.Id==j.Task&&t.FinishedAt<=at&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CommitSequence<rev)));
        return j==null?new(w.NewGame!.Start.People.Single(p=>p.Id==actor.Value).Site):RegionalCampaign.Site(j.Legs[^1].To);
    }
    static bool Eligible(ContractWorld w,EntityId person,long at,long rev)
    {
        if(person==w.PlayerControl||w.Characters.SingleOrDefault(c=>c.Id==person)?.Life!=LifeState.Adult||w.Offices.Any(r=>r.Subject==person&&r.From<=at&&at<r.Until)||NpcArmies.CommanderBusyAt(w,person,at,rev))return false;
        var original=w.NewGame!.Start.People.Single(p=>p.Id==person.Value);if(ReleaseEnabled(w)&&original.Lord!=null&&original.Lord!=w.PlayerControl.Value&&ReleasedFromOldLord(w,person,at,rev)&&!ReleaseKnown(w,person,at,rev))return false;
        var bonds=HouseholdService.BondsAt(w,at,rev).Where(r=>r.Subject==person&&r.From<=at&&at<r.Until).ToArray();if(bonds.Any(b=>b.Target!=w.PlayerControl))return false;
        return bonds.Any(b=>b.Target==w.PlayerControl)||!w.Memberships.Any(r=>r.Subject==person&&r.From<=at&&at<r.Until);
    }
    static bool Busy(ContractWorld w,EntityId person,long at,long rev)=>Orders(w).Where(q=>q.Input.Person==person&&q.Task!=null&&Before(q,at,rev)).Any(q=>!w.Requests.Any(r=>r.Id==q.Task!.RequestId)||w.Tasks.Any(t=>t.RequestId==q.Task!.RequestId&&(t.FinishedAt==null||t.FinishedAt>at)));
    public static bool Occupied(ContractWorld w,EntityId person,long at,long rev)=>Enabled(w)&&(Busy(w,person,at,rev+1)||ReleasePending(w,person,at,rev+1));
    public static CompanionCard[] View(ContractWorld w)
    {
        if(!Enabled(w))return [];var now=Orders(w).Where(q=>Before(q,w.ElapsedSeconds,w.CommitSequence+1)).ToArray();var site=SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);
        return w.NewGame!.Start.People.Where(p=>p.Id!=w.PlayerControl.Value).Select(p=>{var id=new EntityId(p.Id);bool active=Active(now,id),near=site!=null&&SiteAt(w,id,w.ElapsedSeconds,w.CommitSequence+1)==site;bool known=now.Any(q=>q.Input.Person==id);if(!known&&!(near&&(Eligible(w,id,w.ElapsedSeconds,w.CommitSequence+1)||CommissionContact(w,id,w.ElapsedSeconds,w.CommitSequence+1))))return null;long owed=Owed(w,id,w.ElapsedSeconds,w.CommitSequence+1);bool busy=Busy(w,id,w.ElapsedSeconds,w.CommitSequence+1)||!near&&now.Any(q=>q.Input.Person==id&&q.Task!=null&&!w.Companions!.Reports.Any(r=>r.Order==q.Input.Id&&MessageFlow.Knows(w.Messaging,r.Message,w.PlayerControl.Value)));return new CompanionCard(id,p.Name,!Eligible(w,id,w.ElapsedSeconds,w.CommitSequence+1)?"一件だけの私的点検協力":p.Lord==w.PlayerControl.Value?"直属家臣との協力契約":"個人的な随行協力",active,owed,!active?busy?"頼んだ用件が進行中":now.Any(q=>q.Input.Person==id&&q.Input.Action==CompanionAction.PrivateInspect)?"単発の協力・継続契約なし":known?"協力終了・未払いは保持":"現地で協力を相談可能":busy?"頼んだ用件が進行中":owed>0?"手当の支払い待ち":"協力中",near,busy);}).Where(c=>c!=null).Select(c=>c!).OrderBy(c=>c.Person.Value,StringComparer.Ordinal).ToArray();
    }
    internal static Intent TaskInput(ContractWorld w,CompanionInput input)
    {string id="request:companion-task/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(input.Id.Value));return input.Action==CompanionAction.TravelTogether?TravelWorld.ActorMove(w,id,input.Person,input.Destination??throw new InvalidDataException("行先がありません"),true,LandRoute):NewGameWorld.InspectFor(w,input.Person,id,w.ElapsedSeconds);}
    static void Check(ContractWorld w,CompanionOrder q,CompanionOrder[] prior)
    {
        if(ReleaseEvent(q.Input.Action)){CheckReleaseEvent(w,q);return;}
        var i=q.Input;Need(EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&i.Amount>=0&&q.At>=0&&q.At<=w.ElapsedSeconds&&q.Revision>0&&q.Revision<=w.CommitSequence&&w.Characters.Any(c=>c.Id==i.Person),"協力契約の入力が不正です");Need(SiteAt(w,w.PlayerControl,q.At,q.Revision)==q.Site&&SiteAt(w,i.Person,q.At,q.Revision)==q.Site,"面会・現物受渡しは現地で行います");Need(!Busy(w,i.Person,q.At,q.Revision),"先に相手の用件を終えてください");
        Need(!FactionStances.OtherIdUsed(w,i.Id,includeCompanions:false)&&!FactionStances.OwnIdUsed(w,i.Id)&&w.Military?.Requests.Any(r=>r.Input.Id==i.Id)!=true&&w.Career?.Requests.Any(r=>r.Input.Id==i.Id)!=true,"協力契約のIDが別の操作に使われています");bool active=Active(prior,i.Person);long owed=Owed(w,i.Person,q.At,q.Revision);
        if(i.Action==CompanionAction.InquireLordRelease)CheckReleaseInquiry(w,q);
        else if(i.Action==CompanionAction.RequestLordRelease)Need(i.Amount==0&&CanAskRelease(w,i.Person,q.At,q.Revision),"先に本人との情報交換・仕事・返報と立場を確認してください");
        else if(i.Action is CompanionAction.PledgeService or CompanionAction.ReleaseService)CheckService(w,q);
        else if(i.Action==CompanionAction.PrivateInspect){Need(SingleInspectionEnabled(w)&&i.Amount==DailyPay&&owed==0&&!active&&CanCommission(w,i.Person,q.At,q.Revision),"面識と精算のある、仕事の空いた本人へ一件20文で点検を相談してください");}
        else if(i.Action==CompanionAction.Agree){Need(CharacterRelations.AcceptsCompanion(w,i.Person,i.Amount,q.At,q.Revision),"本人は今の条件で継続協力を引き受けないと答えました。条件や実績を変えずに聞き直しても返答は同じです");Need(!active&&owed==0&&Eligible(w,i.Person,q.At,q.Revision)&&w.Characters.Single(c=>c.Id==i.Person).WillObey&&i.Amount==DailyPay,"他家の家臣を無断で引き抜かず、協力可能な成人に一日20文を前払いします");}
        else if(i.Action==CompanionAction.Pay)Need(i.Amount>0&&i.Amount<=owed,"支払う未払い手当がありません");
        else{Need(active&&i.Amount==0,"有効な協力合意がありません");if(i.Action!=CompanionAction.EndAgreement)Need(owed==0&&Eligible(w,i.Person,q.At,q.Revision)&&w.Characters.Single(c=>c.Id==i.Person).WillObey,"支払い・担当・本人の協力意思を確認してください");}
        if(i.Action is CompanionAction.Agree or CompanionAction.Pay or CompanionAction.PrivateInspect)Need(TransportWork.WalletBefore(w,w.PlayerControl,q.At,q.Revision)>=i.Amount,"私財が足りません。手当の未払いは消えません");
        Need((i.Action==CompanionAction.TravelTogether)==(i.Destination!=null)&&(i.Destination==null||BranchRoutes.GraphNodes(w).Any(n=>n.Id==i.Destination)),"行先の指定が不正です");Need((i.Action is CompanionAction.TravelTogether or CompanionAction.Inspect or CompanionAction.PrivateInspect)==(q.Task!=null),"共通任務の原本が一致しません");
        Need((i.Action==CompanionAction.TravelTogether)==(q.LeaderTask!=null)&& (q.LeaderTask==null||q.LeaderTask==LeaderInput(BeforeTask(w,q),i)),"本人の同行経路・共通任務が一致しません");if(q.Task is {} task){Need(task==TaskInput(BeforeTask(w,q),i),"任務の経路・所要時間・期限が引受時の条件と一致しません");Need(task.Actor==i.Person&&task.Executor==i.Person&&task.EffectiveAt==q.At&&task.DeliverySeconds==1&&task.ReportSeconds==1&&task.Kind==TaskKind.BindingOrder&&!task.Important,"本人が引き受けた共通任務ではありません");Need(task.Action==(i.Action is CompanionAction.Inspect or CompanionAction.PrivateInspect?ActionKind.Inspect:ActionKind.Move)&&task.Scope==(i.Action is CompanionAction.Inspect or CompanionAction.PrivateInspect?q.Site:ContractFixture.Castle)&&task.RoutePolicy==(i.Action is CompanionAction.Inspect or CompanionAction.PrivateInspect?null:LandRoute),"協力任務の分類が不正です");}
    }
    internal static void Apply(ContractWorld w,CompanionInput input)
    {Need(Enabled(w),"この開始では随行協力が有効ではありません");var site=SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence)??throw new InvalidDataException("到着してから相談してください");Intent? task=input.Action is CompanionAction.TravelTogether or CompanionAction.Inspect or CompanionAction.PrivateInspect?TaskInput(w,input):null;var leader=input.Action==CompanionAction.TravelTogether?LeaderInput(w,input):null;var q=new CompanionOrder(input,w.ElapsedSeconds,w.CommitSequence,site,task,leader);Check(w,q,Orders(w));w.Companions=w.Companions! with{Orders=[..Orders(w),q]};if(input.Action==CompanionAction.InquireLordRelease)SendReleaseInquiry(w,q);if(input.Action==CompanionAction.RequestLordRelease)SendRelease(w,q,"request",input.Person,OldLord(w,input.Person),LordHome(w,input.Person),"役目を引き継いだうえで辞去を願います。");}
    public static bool IsTask(ContractWorld w,Intent i)=>Orders(w).Any(q=>q.Task==i||q.LeaderTask==i);
    static Intent LeaderInput(ContractWorld w,CompanionInput i)=>TravelWorld.ActorMove(w,"request:companion-leader/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(i.Id.Value)),w.PlayerControl,i.Destination!,true,LandRoute);
    internal static void ValidatePending(ContractWorld w,Intent i){var q=Orders(w).SingleOrDefault(q=>q.Task==i||q.LeaderTask==i)??throw new InvalidDataException("引受原本がありません");Need(i==(q.Task==i?TaskInput(w,q.Input):LeaderInput(w,q.Input)),"引受時の移動・点検条件が変わりました");}
    static ContractWorld BeforeTask(ContractWorld w,CompanionOrder q)
    {
        var c=w.ForkTransportLocation();c.ElapsedSeconds=q.At;c.CommitSequence=q.Revision-1;c.Requests=w.Requests.Where(r=>r.CommitSequence<q.Revision).ToArray();c.Tasks=w.Tasks.Where(t=>c.Requests.Any(r=>r.Id==t.RequestId)).ToArray();c.Branches=w.Branches! with{Journeys=w.Branches!.Journeys.Where(j=>c.Tasks.Any(t=>t.Id==j.Task)).ToArray()};c.Locations=w.Locations.Select(l=>SiteAt(w,l.Actor,q.At,q.Revision) is {} site?new LocationState(l.Actor,LocationKind.AtSite,site,null,0,0,null,null,null,null,0):l).ToArray();c.Evidence=w.Evidence.Where(e=>e.RecordedAt<=q.At).ToArray();c.Knowledge=w.Knowledge.Where(k=>k.LearnedAt<=q.At&&c.Evidence.Any(e=>e.Id==k.Evidence)).ToArray();return c;
    }
    internal static void Rewind(ContractWorld w,long at,long revision){if(w.Companions is {} s)w.Companions=s with{Orders=s.Orders.Where(q=>Before(q,at,revision+1)).ToArray(),Reports=s.Reports.Where(r=>r.At<at||r.At==at&&r.Revision<=revision).ToArray()};}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind==ReportKind&&w.Companions?.Reports.SingleOrDefault(r=>r.Message==m.Id) is {} r?NewGameWorld.Node(r.Site.Value):null;
    static string ReportBody(CompanionOrder q,TaskState t)=>q.Input.Action is CompanionAction.Inspect or CompanionAction.PrivateInspect?(t.Execution==ExecutionPhase.Completed?"頼まれた現地の点検を終えました。":"現地点検を完了できませんでした。"):t.Execution==ExecutionPhase.Completed?"指定された拠点へ到着しました。":"指定された拠点まで進めませんでした。";
    internal static void Update(ContractWorld w)
    {if(!Enabled(w))return;foreach(var q in Orders(w).Where(q=>q.Task!=null)){if(w.Companions!.Reports.Any(r=>r.Order==q.Input.Id))continue;var t=w.Tasks.SingleOrDefault(t=>t.RequestId==q.Task!.RequestId);if(t?.FinishedAt==null)continue;var site=SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence);if(site==null||SiteAt(w,q.Input.Person,w.ElapsedSeconds,w.CommitSequence)!=site)continue;string id="message:companion-report/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(q.Input.Id.Value));var body=ReportBody(q,t);MessageFlow.Send(w.Messaging,new(id,q.Input.Id.Value,ReportKind,q.Input.Person.Value,w.PlayerControl.Value,body,site.Value.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];w.Companions=w.Companions with{Reports=[..w.Companions.Reports,new(q.Input.Id,id,w.ElapsedSeconds,w.CommitSequence,site.Value)]};}}
    internal static void Validate(ContractWorld w,ContractWorld? trustedPrior=null)
    {if(!Enabled(w)){Need(w.Companions==null,"旧世界に随行契約を追加できません");return;}Need(w.Companions is {Schema:1}&&w.Companions.Contract==w.NewGame!.CompanionPolicy&&w.Companions.Orders!=null&&w.Companions.Reports!=null,"協力契約の原本がありません");var orders=Orders(w);Need(orders.Select(q=>q.Input.Id).Distinct().Count()==orders.Length&&orders.Zip(orders.Skip(1)).All(p=>p.First.Revision<p.Second.Revision&&p.First.At<=p.Second.At),"協力契約の順序・IDが不正です");for(int n=0;n<orders.Length;n++){Check(w,orders[n],orders.Take(n).ToArray());var q=orders[n];if(q.Task!=null){foreach(var input in new[]{q.Task,q.LeaderTask}.Where(i=>i!=null)){var r=w.Requests.SingleOrDefault(r=>r.Id==input!.RequestId);Need(r!=null?r.Intent==input&&r.CommitSequence>q.Revision:trustedPrior!=null,"協力原本と共通任務が異なります");}}}
        ValidateReleaseLetters(w);
        Need(w.Companions!.Reports.Select(r=>r.Order).Distinct().Count()==w.Companions.Reports.Length,"返報が重複しています");foreach(var r in w.Companions.Reports){var q=orders.Single(q=>q.Input.Id==r.Order&&q.Task!=null);var t=w.Tasks.Single(t=>t.RequestId==q.Task!.RequestId);Need(t.FinishedAt<=r.At&&r.At<=w.ElapsedSeconds&&r.Revision>q.Revision&&r.Revision<=w.CommitSequence&&SiteAt(w,w.PlayerControl,r.At,r.Revision)==r.Site&&SiteAt(w,q.Input.Person,r.At,r.Revision)==r.Site&&w.Messaging.Messages.Any(m=>m.Id==r.Message&&m.Kind==ReportKind&&m.CorrelationId==q.Input.Id.Value&&m.SenderId==q.Input.Person.Value&&m.RecipientId==w.PlayerControl.Value&&m.CreatedAt==r.At&&m.Scope==r.Site.Value&&m.Body==ReportBody(q,t)&&w.SealedMessages.Contains(new(r.Message))),"現地の返報原本が一致しません");}}
}
