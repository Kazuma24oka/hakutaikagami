using System.Text;
using System.Text.Json.Serialization;
using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum MeritAction { Enable, Grant, Petition, LinkSalary, ReviewCompanion }
public enum MeritRewardKind { Coins, Object, Commendation, Surname, NameCharacter }
public sealed record MeritInput(EntityId Id,MeritAction Action,EntityId Person,EntityId[] Merits,MeritRewardKind Kind=MeritRewardKind.Commendation,long Amount=0,EntityId? Object=null,EntityId? Carrier=null);
public sealed record MeritEvent(MeritInput Input,EntityId Actor,EntityId Site,long At,long Revision,string Name);
public sealed record MeritState(int Schema,string Policy,MeritEvent[] Events);
public sealed record MeritRecord(EntityId Task,EntityId Principal,EntityId Subject,EntityId Review,int Version,long KnownAt,int Weight,bool Issued,bool Received);
public sealed record MeritGrievance(EntityId Subject,EntityId Principal,EntityId[] Tasks,long Since,string Description);
public static class MeritRewards
{
 public const string Policy="known-review-physical-reward-received-not-currency-v1";
 internal static MeritEvent[] Events(ContractWorld w)=>w.Merits?.Events??[];
 static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool c,string reason){if(!c)throw new InvalidDataException(reason);}
 internal static EntityId GiftId(EntityId grant)=>new("request:merit-gift/"+ContractJson.Hash(Encoding.UTF8.GetBytes(grant.Value))[..24]);
 public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Events(w).Any(e=>e.Input.Id==id);
 static bool Before(MeritEvent e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
 public static MeritRecord[] Records(ContractWorld w,EntityId principal,long at=long.MaxValue,long revision=long.MaxValue)
 {
  if(!NewGameWork.Enabled(w))return [];
  var messages=w.Messaging.Messages.Where(m=>m.Kind==AssessmentNotice.Kind&&m.SenderId==principal.Value&&m.CreatedAt<=at).GroupBy(m=>AssessmentNotice.Read(m.Body).Task).Select(g=>g.OrderByDescending(m=>AssessmentNotice.Read(m.Body).Version).First());var result=new List<MeritRecord>();
  foreach(var m in messages){var p=AssessmentNotice.Read(m.Body);if(!WorkProgression.Completed(p.Review.Achievement))continue;var task=w.Tasks.Single(t=>t.Id==p.Task);if(task.Issuer!=principal)continue;
   var gift=Events(w).FirstOrDefault(e=>e.Actor==principal&&e.Input.Person==task.Executor&&e.Input.Action is MeritAction.Grant or MeritAction.LinkSalary&&e.Input.Merits.Contains(task.Id)&&Before(e,at,revision));bool received=false;
   if(gift?.Input.Action==MeritAction.Grant)received=SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.ReceiveGift&&e.Input.Reference==GiftId(gift.Input.Id)&&e.At<=at&&e.Revision<revision);
   if(gift?.Input.Action==MeritAction.LinkSalary){var term=w.Salary?.Terms.SingleOrDefault(t=>t.Id==gift.Input.Object);received=term!=null&&w.Salary!.Entries.Any(e=>e.Input.Action==SalaryAction.Receive&&e.Input.Contract==term.Id&&e.At<=at&&e.Revision<revision&&e.Revision>gift.Revision);}
   var intent=w.Requests.Single(r=>r.Id==task.RequestId).Intent;int danger=p.Review.Evidence.Count(id=>w.Evidence.Any(e=>e.Id==id&&e.Claim.StartsWith("route-block:",StringComparison.Ordinal))||w.Messaging.Messages.Any(x=>x.Id==id.Value&&x.Kind=="route-observation"&&x.Body.Contains("block",StringComparison.Ordinal)));int entrusted=(int)Math.Min(3,Math.Max(0,intent.Amount)/100);int weight=1+Math.Min(2,danger)+entrusted;
   result.Add(new(task.Id,principal,task.Executor,new(m.Id),p.Version,m.CreatedAt,weight,gift!=null,received));
  }
  foreach(var e in Events(w).Where(e=>e.Actor==principal&&e.Input.Action==MeritAction.ReviewCompanion&&Before(e,at,revision))){var task=e.Input.Merits.Single();var grant=Events(w).FirstOrDefault(g=>g.Actor==principal&&g.Input.Action is MeritAction.Grant or MeritAction.LinkSalary&&g.Input.Merits.Contains(task)&&Before(g,at,revision));bool received=grant!=null&&(grant.Input.Action==MeritAction.Grant?SocialWorld.Ledger(w).Any(g=>g.Input.Action==SocialWorldAction.ReceiveGift&&g.Input.Reference==GiftId(grant.Input.Id)&&g.At<=at&&g.Revision<revision):w.Salary?.Entries.Any(p=>p.Input.Action==SalaryAction.Receive&&p.Input.Contract==grant.Input.Object&&p.Revision>grant.Revision&&p.At<=at&&p.Revision<revision)==true);result.Add(new(task,principal,e.Input.Person,e.Input.Id,1,e.At,1,grant!=null,received));}
  return result.ToArray();
 }
 public static MeritRecord[] View(ContractWorld w,EntityId observer)=>w.Characters.Select(c=>c.Id).SelectMany(p=>Records(w,p)).Where(r=>r.Principal==observer||r.Subject==observer&&(MessageFlow.Knows(w.Messaging,r.Review.Value,observer.Value)||SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.ReceiveGift&&e.Actor==observer&&e.Input.Reference==GiftId(r.Review)))).Select(r=>{
  if(r.Subject==observer){var promise=Events(w).SingleOrDefault(e=>e.Input.Action is MeritAction.Grant or MeritAction.LinkSalary&&e.Actor==r.Principal&&e.Input.Merits.Contains(r.Task));bool heard=promise==null||r.Received||promise.Input.Action==MeritAction.Grant&&SocialWorld.Gifts(w,observer).Any(g=>g.Id==GiftId(promise.Input.Id));return r with{Issued=r.Issued&&heard,KnownAt=w.Messaging.Knowledge.Where(k=>k.MessageId==r.Review.Value&&k.CharacterId==observer.Value).Select(k=>k.LearnedAt).Concat(SocialWorld.Ledger(w).Where(e=>e.Input.Action==SocialWorldAction.ReceiveGift&&e.Actor==observer&&e.Input.Reference==GiftId(r.Review)).Select(e=>e.At)).Min()};}
  var grant=Events(w).SingleOrDefault(e=>e.Actor==r.Principal&&e.Input.Action==MeritAction.Grant&&e.Input.Merits.Contains(r.Task));if(grant!=null&&r.Received&&!SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.AcknowledgeGiftReport&&e.Actor==observer&&e.Input.Reference==GiftId(grant.Input.Id)))return r with{Received=false};return r;
 }).ToArray();
 static bool Sworn(ContractWorld w,EntityId principal,EntityId subject)=>HouseholdService.BondsAt(w,w.ElapsedSeconds,w.CommitSequence+1).Any(b=>b.Subject==subject&&b.Target==principal&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until);
 static string NameFor(ContractWorld w,EntityId principal,EntityId subject,MeritRewardKind kind,long? at=null,long? revision=null){string name=w.Definitions.Characters.Single(c=>c.Id==subject).Name;if(kind==MeritRewardKind.Surname){var house=HouseSuccession.Factions(w,at??w.ElapsedSeconds,revision??w.CommitSequence+1).SingleOrDefault(f=>f.Head==principal.Value);Need(house!=null,"与姓には家の代表としての権限が必要です");return house.Name.TrimEnd('家')+" "+name;}string donor=w.Definitions.Characters.Single(c=>c.Id==principal).Name.Trim();return name+"（拝領字「"+donor[^1]+"」）";}
 internal static void Add(ContractWorld w,MeritInput input,EntityId actor,EntityId site,string name=""){w.Merits??=new(1,Policy,[]);w.Merits=w.Merits with{Events=[..w.Merits.Events,new(input,actor,site,w.ElapsedSeconds,w.CommitSequence,name)]};}
 public static void Execute(ContractWorld w,MeritInput i,EntityId actor)
 {
  Need(NewGameWork.Enabled(w)&&Enum.IsDefined(i.Action)&&Enum.IsDefined(i.Kind)&&i.Merits!=null&&i.Merits.Distinct().Count()==i.Merits.Length,"論功の入力が不正です");var site=SocialWorld.Site(w,actor);Need(site!=null,"現地へ到着してください");
  if(i.Action==MeritAction.Enable){Need(i.Person==actor&&i.Merits.Length==0&&i.Amount==0&&i.Object==null&&i.Carrier==null,"本人の帳面を開いてください");Add(w,i,actor,site.Value);return;}
  if(i.Action==MeritAction.Petition){Need(SocialWorld.Site(w,i.Person)==site&&Grievances(w,actor).Any(g=>g.Principal==i.Person),"本人が知る未報奨の経緯を、主君に現地で上申してください");Need(!Events(w).Any(e=>e.Actor==actor&&e.Input.Person==i.Person&&e.Input.Action==MeritAction.Petition&&e.At/86400==w.ElapsedSeconds/86400),"今日は上申済みです");Add(w,i,actor,site.Value);return;}
  if(i.Action==MeritAction.ReviewCompanion){
   Need(actor==w.PlayerControl&&i.Merits.Length==1&&i.Object!=null&&i.Amount==0&&i.Carrier==null,"本人への点検返報を選んでください");var paper=w.Companions?.Reports.SingleOrDefault(r=>r.Message==i.Object.Value.Value);Need(paper!=null&&MessageFlow.Knows(w.Messaging,paper.Message,actor.Value),"まだ開封していない返報です");var order=w.Companions!.Orders.Single(q=>q.Input.Id==paper.Order);Need(order.Input.Person==i.Person&&order.Task?.Action==ActionKind.Inspect&&w.Tasks.Single(t=>t.RequestId==order.Task.RequestId).Id==i.Merits[0]&&w.Messaging.Messages.Single(m=>m.Id==paper.Message).Body=="頼まれた現地の点検を終えました。","完了の点検返報ではありません");Need(!Events(w).Any(e=>e.Input.Action==MeritAction.ReviewCompanion&&e.Input.Merits.Contains(i.Merits[0])),"この返報は評価済みです");Add(w,i,actor,site.Value);SocialWorld.Execute(w,new(GiftId(i.Id),SocialWorldAction.DispatchGift,i.Person){Asset=i.Id},actor);return;
  }
  var records=Records(w,actor);Need(i.Person!=actor&&w.Characters.Any(c=>c.Id==i.Person&&c.Life==LifeState.Adult)&&i.Merits.Length>0&&i.Merits.All(id=>records.Any(r=>r.Task==id&&r.Subject==i.Person&&!r.Issued)),"自分の勲功帳にある未沙汰の働きだけを選んでください");
  if(i.Action==MeritAction.LinkSalary){Need(i.Object is {} id&&w.Salary?.Terms.Any(t=>t.Id==id&&t.Employer==actor&&t.Employee==i.Person)==true&&w.Salary!.Quotes.Any(q=>q.Id==i.Object&&q.At>=records.Where(r=>i.Merits.Contains(r.Task)).Max(r=>r.KnownAt)),"働きの評価後に別途成立した本人との給与契約が必要です");Add(w,i,actor,site.Value);return;}
  Need(i.Action==MeritAction.Grant,"未対応の論功です");string name="";
  if(i.Kind==MeritRewardKind.Coins)Need(i.Amount>0&&i.Object==null,"実在する私財の銭を指定してください");
  else if(i.Kind==MeritRewardKind.Object)Need(i.Amount==0&&i.Object!=null,"手元の私有物を選んでください");
  else {Need(i.Amount==0&&i.Object==null,"文書に架空の銭や物を指定できません");if(i.Kind is MeritRewardKind.Surname or MeritRewardKind.NameCharacter){Need(Sworn(w,actor,i.Person),"本人への主従の根拠が必要です");name=NameFor(w,actor,i.Person,i.Kind);}}
  Add(w,i,actor,site.Value,name);
  var gift=new SocialWorldInput(GiftId(i.Id),SocialWorldAction.DispatchGift,i.Person,i.Carrier,i.Kind==MeritRewardKind.Coins?i.Amount:0){Asset=i.Kind==MeritRewardKind.Coins?null:i.Kind==MeritRewardKind.Object?i.Object:i.Id};SocialWorld.Execute(w,gift,actor);
 }
 internal static PersonalObject[] Documents(ContractWorld w,long revision)=>Events(w).Where(e=>e.Revision<=revision&&(e.Input.Action==MeritAction.ReviewCompanion||e.Input.Action==MeritAction.Grant&&e.Input.Kind is MeritRewardKind.Commendation or MeritRewardKind.Surname or MeritRewardKind.NameCharacter)).Select(e=>new PersonalObject(e.Input.Id,e.Input.Action==MeritAction.ReviewCompanion?"点検の評価通知（封書）":e.Input.Kind==MeritRewardKind.Commendation?"感状（封書）":"名乗りの申し渡し（封書）","commendation",1,e.Actor,e.Actor,e.Site,null,false,["認識した評価原本に基づく恩賞。実際の受領まで未確定"])).ToArray();
 public static MeritGrievance[] Grievances(ContractWorld w,EntityId subject){if(w.Merits==null)return [];int months=CharacterRelations.Enabled(w)?1+CharacterRelations.DiagnosticTrait(w,subject,SocialTrait.Duty)/20:3;return View(w,subject).Where(r=>r.Subject==subject&&!r.Received&&w.ElapsedSeconds-r.KnownAt>=months*30*86400L).GroupBy(r=>r.Principal).Select(g=>new MeritGrievance(subject,g.Key,g.Select(r=>r.Task).ToArray(),g.Min(r=>r.KnownAt)+months*30*86400L,"働きの評価は届いたが、恩賞をまだ実際に受け取っていない")).ToArray();}
 public static RelationshipFact[] Facts(ContractWorld w,EntityId observer,EntityId subject){var rows=new List<RelationshipFact>();foreach(var g in Grievances(w,observer).Where(g=>g.Principal==subject))rows.Add(new(observer,subject,"merit-grievance:"+subject.Value,g.Description,g.Since,g.Since,-2,-1,0,2));foreach(var e in Events(w).Where(e=>e.Input.Action==MeritAction.Petition&&e.Actor==subject&&e.Input.Person==observer))rows.Add(new(observer,subject,"merit:"+e.Input.Id.Value,"本人から未報奨への不満を現地で上申された",e.At,e.At,0,0,0,0));return rows.ToArray();}
 public static string Face(ContractWorld w,EntityId observer,EntityId principal){int n=View(w,observer).Count(r=>r.Principal==principal&&r.Subject==observer&&r.Received);return n>=3?"幾度も働きが認められ、顔を覚えられている":n>0?"働きが認められた経緯がある":"まだ取次ぎを頼めるほどの経緯はない";}
 public static string DisplayName(ContractWorld w,EntityId observer,EntityId subject){var award=Events(w).Where(e=>e.Input.Action==MeritAction.Grant&&e.Input.Person==subject&&e.Name.Length>0).LastOrDefault(e=>SocialWorld.Ledger(w).Any(r=>r.Input.Action==SocialWorldAction.ReceiveGift&&r.Input.Reference==GiftId(e.Input.Id))&&(observer==subject||observer==e.Actor&&SocialWorld.Ledger(w).Any(r=>r.Input.Action==SocialWorldAction.AcknowledgeGiftReport&&r.Input.Reference==GiftId(e.Input.Id))));return award?.Name??w.Definitions.Characters.Single(c=>c.Id==subject).Name;}
 public static RenownFact[] Renown(ContractWorld w,EntityId observer)=>Events(w).Where(e=>e.Input.Action==MeritAction.Grant&&e.Input.Kind!=MeritRewardKind.Coins&&e.Input.Kind!=MeritRewardKind.Object).SelectMany(e=>SocialWorld.Ledger(w).Where(r=>r.Input.Reference==GiftId(e.Input.Id)&&r.Input.Action==SocialWorldAction.ReceiveGift&&r.Actor==observer).Select(r=>new RenownFact(e.Input.Id.Value,observer,observer,RenownKind.Benevolence,r.At,r.At,r.Site,"感状・名乗りを現地で実際に受領した。戦功とは区別",false))).ToArray();
 public static void Tick(ContractWorld w,bool clockAdvanced)
 {
  if(w.Merits==null||!clockAdvanced||w.ElapsedSeconds%86400!=0)return;
  foreach(var principal in w.Characters.Where(c=>c.Id!=w.PlayerControl&&c.Life==LifeState.Adult).Select(c=>c.Id).OrderBy(x=>x.Value,StringComparer.Ordinal)){
   var site=SocialWorld.Site(w,principal);if(site==null)continue;
   foreach(var complaint in Grievances(w,principal).Where(g=>SocialWorld.Site(w,g.Principal)==site)){
    if(Events(w).Any(e=>e.Actor==principal&&e.Input.Action==MeritAction.Petition&&e.Input.Person==complaint.Principal&&e.At/86400==w.ElapsedSeconds/86400))continue;
    var id=new EntityId("request:npc-petition/"+ContractJson.Hash(Encoding.UTF8.GetBytes(principal.Value+"/"+complaint.Principal.Value+"/"+w.ElapsedSeconds/86400))[..24]);Execute(w,new(id,MeritAction.Petition,complaint.Principal,complaint.Tasks),principal);
   }
   foreach(var group in Records(w,principal).Where(r=>!r.Issued&&SocialWorld.Site(w,r.Subject)==site&&w.ElapsedSeconds-r.KnownAt>=86400).GroupBy(r=>r.Subject).ToArray()){
    long amount=10*group.Sum(r=>r.Weight);if(NewGameWork.Balance(w,principal)<amount)continue;
    var zero=new NpcPersonality(0,0,0,0,0,0);var personality=CharacterRelations.Enabled(w)?new NpcPersonality(CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.Ambition),CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.Duty),CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.Caution),CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.Courage),CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.Affection),CharacterRelations.DiagnosticTrait(w,principal,SocialTrait.GainSeeking)):new(50,50,50,50,50,50);
    int utility=35+Math.Min(30,group.Sum(r=>r.Weight)*5);var choice=NpcChoiceRanking.Choose(new(principal,w.Seed,w.ElapsedSeconds,personality,NpcAspiration.PreserveHouse,HistoryMode.Fictional,NpcAttention.Equal,[new("reward",true,true,utility,new(0,3,-1,0,2,-1),true,false,false),new("wait",true,true,20,zero,false,false,false)]));if(choice!="reward")continue;
    var id=new EntityId("request:npc-merit/"+ContractJson.Hash(Encoding.UTF8.GetBytes(principal.Value+"/"+group.First().Task.Value))[..24]);TransactionalWorld.TryApply(w,c=>Execute(c,new(id,MeritAction.Grant,group.Key,group.Select(r=>r.Task).ToArray(),MeritRewardKind.Coins,amount),principal));
   }
  }
 }
 internal static long? NextBoundary(ContractWorld w,long target)=>w.Merits!=null&&(w.ElapsedSeconds/86400+1)*86400<=target?(w.ElapsedSeconds/86400+1)*86400:null;
 internal static void Rewind(ContractWorld w,long at,long revision){if(w.Merits!=null){var es=Events(w).Where(e=>e.At<at||e.At==at&&e.Revision<=revision).ToArray();w.Merits=es.Length==0?null:w.Merits with{Events=es};}}
 public static void Validate(ContractWorld w,ContractWorld? prior)
 {
  if(w.Merits==null){Need(prior?.Merits==null,"勲功帳の記録が消えています");return;}Need(NewGameWork.Enabled(w)&&w.Merits.Schema==1&&w.Merits.Policy==Policy,"勲功帳の版が違います");var es=Events(w);Need(es.Select(e=>e.Input.Id).Distinct().Count()==es.Length&&es.All(e=>e.At<=w.ElapsedSeconds&&e.At>=0&&e.Revision>0&&e.Revision<=w.CommitSequence&&Enum.IsDefined(e.Input.Action)&&Enum.IsDefined(e.Input.Kind)&&e.Input.Merits!=null),"論功の記録が不正です");
  if(prior?.Merits!=null)Need(ContractJson.CanonicalHash(es.Take(prior.Merits.Events.Length).ToArray())==ContractJson.CanonicalHash(prior.Merits.Events),"過去の論功が書き換わりました");
  foreach(var e in es.Where(e=>e.Input.Action==MeritAction.ReviewCompanion)){
   var report=w.Companions?.Reports.SingleOrDefault(r=>r.Message==e.Input.Object?.Value);Need(report!=null&&e.Actor==w.PlayerControl&&e.Input.Merits.Length==1&&AssessmentNotice.KnowsAt(w,report.Message,e.Actor,e.At),"未知の返報から評価が作られています");var order=w.Companions!.Orders.Single(q=>q.Input.Id==report.Order);Need(order.Input.Person==e.Input.Person&&order.Task?.Action==ActionKind.Inspect&&w.Tasks.Single(t=>t.RequestId==order.Task.RequestId).Id==e.Input.Merits[0]&&w.Messaging.Messages.Single(m=>m.Id==report.Message).Body=="頼まれた現地の点検を終えました。","点検の評価原本が違います");Need(!es.Any(old=>old.Revision<e.Revision&&old.Input.Action==MeritAction.ReviewCompanion&&old.Input.Merits.Contains(e.Input.Merits[0])),"同じ働きの二重評価です");
  }
  foreach(var e in es.Where(e=>e.Input.Action is MeritAction.Grant or MeritAction.LinkSalary)){Need(e.Input.Merits.Length>0&&e.Input.Merits.Distinct().Count()==e.Input.Merits.Length&&e.Input.Merits.All(id=>Records(w,e.Actor,e.At,e.Revision).Any(r=>r.Task==id&&r.Subject==e.Input.Person&&!r.Issued)),"未知の働き・二重恩賞です");if(e.Input.Action==MeritAction.Grant){Need(SocialWorld.Ledger(w).Any(g=>g.Input.Id==GiftId(e.Input.Id)&&g.Actor==e.Actor&&g.Input.Person==e.Input.Person&&g.Revision==e.Revision&&g.Input.Amount==(e.Input.Kind==MeritRewardKind.Coins?e.Input.Amount:0)&&g.Input.Asset==(e.Input.Kind==MeritRewardKind.Coins?null:e.Input.Kind==MeritRewardKind.Object?e.Input.Object:e.Input.Id)),"恩賞の実物引渡しがありません");Need(e.Name==(e.Input.Kind is MeritRewardKind.Surname or MeritRewardKind.NameCharacter?NameFor(w,e.Actor,e.Input.Person,e.Input.Kind,e.At,e.Revision):""),"名乗りの文面が違います");}}
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageMerit(MeritInput input,long revision){lock(gate){var old=MeritRewards.Events(state).SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(ContractJson.CanonicalHash(old.Input)==ContractJson.CanonicalHash(input)?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"論功は受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");try{Change(w=>MeritRewards.Execute(w,input,w.PlayerControl));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"恩賞の沙汰と実際の受領を分けて記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
