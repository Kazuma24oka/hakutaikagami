using System.Text;
using System.Text.Json.Serialization;
namespace HakutaiKagami.Core.Contracts;

public enum SocialAssetAction { Buy, DraftIntroduction, PresentIntroduction, PlanTea, AcceptTea, DeclineTea, HoldTea }
public sealed record SocialAssetInput(EntityId Id, SocialAssetAction Action, EntityId Person, EntityId? Reference=null, long When=0) { [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public EntityId[]? People {get;init;} }
public sealed record SocialAssetEvent(SocialAssetInput Input, EntityId Actor, EntityId Site, long At, long Revision, long Cost);
public sealed record SocialAssetState(int Schema, string Policy, SocialAssetEvent[] Events);
public sealed record PersonalObject(EntityId Id,string Name,string Kind,long Quantity,EntityId Owner,EntityId Holder,EntityId? Site,EntityId? Gift,bool Consumed,string[] History);
public sealed record ArtifactDefinition(EntityId Id,EntityId Market,string Name,string Kind,long Price,string Provenance);

// 所有者と運搬者を別に保持する。品物は購入・既存物資の引渡し・文書の作成からしか生まれない。
public static class SocialAssets
{
 public const string Policy="physical-private-objects-introductions-tea-v1";
 internal static SocialAssetEvent[] Events(ContractWorld w)=>w.SocialAssets?.Events??[];
 static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
 static EntityId Id(string kind,string source)=>new("social-asset:"+kind+"/"+ContractJson.Hash(Encoding.UTF8.GetBytes(source))[..24]);
 public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Events(w).Any(e=>e.Input.Id==id);
 public static ArtifactDefinition[] Catalog(ContractWorld w,EntityId site)=>new[]{("tea","茶入",180L),("sword","刀",240L),("book","兵法書",150L)}.Select(k=>new ArtifactDefinition(Id(k.Item1,site.Value),site,"試作名物・"+w.Definitions.Sites.Single(s=>s.Id==site).Name+"の"+k.Item2,k.Item1,k.Item3,"架空の試作品。史実名物や史実の由緒を称しない。市の商人が保管")).ToArray();
 public static long Net(ContractWorld w,EntityId wallet,long at,long revision)=>-Events(w).Where(e=>NewGameWork.Wallet(e.Actor)==wallet&&(e.At<at||e.At==at&&e.Revision<revision)).Sum(e=>e.Cost);
 public static long MerchantReceipts(ContractWorld w)=>Events(w).Where(e=>e.Input.Action==SocialAssetAction.Buy).Sum(e=>e.Cost);
 public static long HospitalityExpense(ContractWorld w)=>Events(w).Where(e=>e.Input.Action==SocialAssetAction.HoldTea).Sum(e=>e.Cost);
 public static PersonalObject[] Objects(ContractWorld w,long beforeRevision=long.MaxValue)
 {
  var rows=MeritRewards.Documents(w,beforeRevision).Concat(InformationSchemes.Documents(w,beforeRevision)).Concat(HouseholdCourt.Documents(w,beforeRevision)).ToDictionary(o=>o.Id);
  void Make(EntityId id,string name,string kind,long quantity,EntityId owner,EntityId site,string origin){Need(!rows.ContainsKey(id),"同じ物の原本が重複しています");rows.Add(id,new(id,name,kind,quantity,owner,owner,site,null,false,[origin]));}
  foreach(var e in w.Trade?.Entries.Where(e=>e.Input.Action==TradeAction.GiftOut&&e.Revision<=beforeRevision)??[]){var deal=w.Trade!.Deals.Single(d=>d.Id==e.Input.Contract);Make(deal.Id,MaterialMarkets.Name(deal.Commodity),deal.Commodity??"rice",deal.Amount,e.Actor,e.Input.Site,"私有の買付原本 "+deal.Id.Value);}
  foreach(var e in Events(w).Where(e=>e.Revision<=beforeRevision)){
   if(e.Input.Action==SocialAssetAction.Buy){var a=Catalog(w,e.Site).Single(a=>a.Id==e.Input.Reference);Make(a.Id,a.Name,a.Kind,1,e.Actor,e.Site,a.Provenance+" → 現地で購入");}
   if(e.Input.Action==SocialAssetAction.DraftIntroduction)Make(e.Input.Id,"紹介状（封書）","introduction",1,e.Actor,e.Site,"本人が面識を持つ相手への紹介。宛先だけを記載");
   if(e.Input.Action==SocialAssetAction.PlanTea)Make(e.Input.Id,"茶会の招待状（封書）","invitation",1,e.Actor,e.Site,"現地で作成。受領と出席の承諾は別");
  }
  var changes=SocialWorld.Ledger(w).Where(e=>e.Revision<beforeRevision&&e.Input.Action is SocialWorldAction.DispatchGift or SocialWorldAction.ReceiveGift or SocialWorldAction.ReturnGift).OrderBy(e=>e.Revision).ToArray();
  foreach(var e in changes){var origin=e.Input.Action==SocialWorldAction.DispatchGift?e:SocialWorld.Ledger(w).Single(g=>g.Input.Id==e.Input.Reference);if(origin.Input.Asset is not {} id)continue;Need(rows.TryGetValue(id,out var obj),"品物の取得原本がありません");var o=obj!;
   if(e.Input.Action==SocialWorldAction.DispatchGift){Need(o.Owner==e.Actor&&o.Gift==null&&!o.Consumed,"既に贈った物・運搬中の物は使えません");rows[id]=o with{Holder=e.Input.Carrier??e.Actor,Site=null,Gift=e.Input.Id,History=[..o.History,"実物を運搬へ引渡し"]};}
   else if(e.Input.Action==SocialWorldAction.ReceiveGift){Need(o.Gift==origin.Input.Id,"違う荷の受領です");rows[id]=o with{Owner=e.Actor,Holder=e.Actor,Site=e.Site,Gift=null,History=[..o.History,"受領："+e.Actor.Value]};}
   else {Need(o.Gift==origin.Input.Id,"返却済みまたは受領済みです");rows[id]=o with{Owner=origin.Actor,Holder=origin.Actor,Site=e.Site,Gift=null,History=[..o.History,"贈り主へ実物返却"]};}
  }
  foreach(var e in Events(w).Where(e=>e.Revision<beforeRevision&&e.Input.Action==SocialAssetAction.PresentIntroduction)){var id=e.Input.Reference!.Value;Need(rows.TryGetValue(id,out var o)&&o.Owner==e.Actor&&!o.Consumed&&o.Gift==null,"使用済みの紹介状です");rows[id]=o with{Consumed=true,History=[..o.History,"宛先本人に提示・使用済み"]};}
  return rows.Values.Select(o=>HouseholdCourt.ReturnObject(w,o,beforeRevision)).ToArray();
 }
 public static long CarryingMass(ContractWorld w,EntityId actor,long beforeRevision=long.MaxValue)=>Objects(w,beforeRevision).Where(o=>o.Holder==actor&&!o.Consumed).Sum(o=>o.Quantity*(o.Kind is "introduction" or "invitation" or "commendation" or "scheme-letter" or "court-paper"?0:o.Kind=="weapons"?5:1));
 static long TradeMass(ContractWorld w,EntityId actor)=>actor!=w.PlayerControl?0:w.Trade?.Deals.Where(d=>d.Phase is TradePhase.Carrying or TradePhase.SaleQuoted or TradePhase.ReturnRequired).Sum(d=>d.Amount*MaterialMarkets.MassKg(d.Commodity))??0;
 internal static bool CanReceive(ContractWorld w,EntityId recipient,EntityId gift){var entry=SocialWorld.Ledger(w).Single(e=>e.Input.Id==gift);if(entry.Input.Asset is not {} id)return true;var o=Objects(w).Single(o=>o.Id==id);long added=o.Holder==recipient?0:o.Quantity*(o.Kind is "introduction" or "invitation" or "commendation" or "scheme-letter" or "court-paper"?0:o.Kind=="weapons"?5:1);return CarryingMass(w,recipient)+TradeMass(w,recipient)+added<=RegionalTrade.Capacity;}
 public static PersonalObject[] Inventory(ContractWorld w,EntityId owner)=>Objects(w).Where(o=>o.Owner==owner&&o.Holder==owner&&o.Gift==null&&!o.Consumed).ToArray();
 internal static void Attach(ContractWorld w,SocialWorldInput input,EntityId actor,EntityId site)
 {
  if(input.Asset is not {} id)return;w.SocialAssets??=new(1,Policy,[]);
  Need(input.Amount==0,"物と銭を同じ包みに二重指定できません");
  if(w.Trade?.Deals.SingleOrDefault(d=>d.Id==id&&d.Phase==TradePhase.Carrying) is {} deal){Need(actor==w.PlayerControl,"家の荷や他人の売買原本を使えません");RegionalTrade.Apply(w,new(Id("export",input.Id.Value),TradeAction.GiftOut,site,id));}
  var item=Objects(w).SingleOrDefault(o=>o.Id==id);Need(item!=null&&item.Owner==actor&&item.Holder==actor&&item.Gift==null&&!item.Consumed,"手元の私有物を選んでください");
  if(item!.Kind=="court-paper")Need(HouseholdCourt.ValidRecipient(w,id,input.Person),"条件書の宛先は変更できません");
  if(item.Kind is "introduction" or "invitation"){
   var source=Events(w).Single(e=>e.Input.Id==id);var target=item.Kind=="invitation"?source.Input.Person:source.Input.Reference!.Value;
   Need(input.Person==target,"封書の宛先を変更できません");
  }
 }
 static bool Local(ContractWorld w,EntityId a,EntityId b)=>SocialWorld.Site(w,a) is {} site&&SocialWorld.Site(w,b)==site;
 static bool Adult(ContractWorld w,EntityId actor)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult);
 static SocialAssetEvent Source(ContractWorld w,EntityId id,SocialAssetAction action)=>Events(w).Single(e=>e.Input.Id==id&&e.Input.Action==action);
 internal static void Add(ContractWorld w,SocialAssetInput input,EntityId actor,EntityId site,long cost=0){w.SocialAssets??=new(1,Policy,[]);w.SocialAssets=w.SocialAssets with{Events=[..w.SocialAssets.Events,new(input,actor,site,w.ElapsedSeconds,w.CommitSequence,cost)]};}
 public static void Execute(ContractWorld w,SocialAssetInput i,EntityId actor)
 {
  Need(NewGameWork.Enabled(w)&&Adult(w,actor)&&Enum.IsDefined(i.Action),"現在は扱えません");var site=SocialWorld.Site(w,actor);Need(site!=null,"現地へ到着してください");long cost=0;
  if(i.Action!=SocialAssetAction.PlanTea)Need(i.When==0,"この用件に開催時刻はありません");
  switch(i.Action){
   case SocialAssetAction.Buy:
    Need(i.Person==actor&&i.Reference!=null,"現地の名物を選んでください");var a=Catalog(w,site!.Value).SingleOrDefault(a=>a.Id==i.Reference);Need(a!=null&&!Objects(w).Any(o=>o.Id==a.Id),"一点物は既に売れています");cost=a!.Price;break;
   case SocialAssetAction.DraftIntroduction:
    Need(i.Reference is {} target&&target!=actor&&target!=i.Person&&Adult(w,target)&&Local(w,actor,i.Person),"紹介者と現地で会い、宛先を指定してください");
    Need(SocialWorld.Facts(w,i.Person,actor).Length>0&&SocialWorld.Facts(w,i.Person,i.Reference!.Value).Length>0,"紹介者が実際に面識を持つ相手だけ紹介できます");var axes=SocialWorld.Axes(w,i.Person,actor);Need(axes.Affection+axes.Respect-axes.Hostility>=2,"紹介者はまだ引き受けません");
    Need(!Events(w).Any(e=>e.Actor==actor&&e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.Reference==i.Reference&&e.At/86400==w.ElapsedSeconds/86400),"今日は既に同じ紹介状を受け取りました");break;
   case SocialAssetAction.PresentIntroduction:
    Need(i.Reference!=null&&Inventory(w,actor).Any(o=>o.Id==i.Reference&&o.Kind=="introduction"),"未使用の紹介状を本人が持参してください");var intro=Source(w,i.Reference!.Value,SocialAssetAction.DraftIntroduction);Need(intro.Input.Reference==i.Person&&Local(w,actor,i.Person),"宛先の本人に現地で提示してください");break;
   case SocialAssetAction.PlanTea:
    Need(i.Person!=actor&&Adult(w,i.Person)&&SocialWorld.Facts(w,actor,i.Person).Length>0&&i.When>=w.ElapsedSeconds+3600&&i.When<=w.ElapsedSeconds+30*86400L,"面識のある相手と、1時間後から30日以内の開催を指定してください");Need(!Events(w).Any(e=>e.Actor==actor&&e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.When==i.When),"同じ茶会に重ねて招待できません");Need(i.Reference==null||Inventory(w,actor).Any(o=>o.Id==i.Reference&&o.Kind=="tea"),"披露する茶器は手元の一点物を選んでください");break;
   case SocialAssetAction.AcceptTea:case SocialAssetAction.DeclineTea:
    var tea=Source(w,i.Reference!.Value,SocialAssetAction.PlanTea);Need(tea.Input.Person==actor&&tea.Actor==i.Person&&w.ElapsedSeconds<tea.Input.When,"本人宛ての招待と期限を確認してください");Need(Inventory(w,actor).Any(o=>o.Id==tea.Input.Id&&o.Kind=="invitation"),"招待状の実物がまだ届いていません");Need(!Events(w).Any(e=>e.Input.Reference==tea.Input.Id&&e.Input.Action is SocialAssetAction.AcceptTea or SocialAssetAction.DeclineTea),"本人は既に返答しました");break;
   case SocialAssetAction.HoldTea:
    var plan=Source(w,i.Reference!.Value,SocialAssetAction.PlanTea);Need(plan.Actor==actor&&plan.Site==site&&w.ElapsedSeconds>=plan.Input.When&&w.ElapsedSeconds<=plan.Input.When+3600,"予定の場所と時刻に集まってください");Need(!Events(w).Any(e=>e.Input.Action==SocialAssetAction.HoldTea&&SameTea(Source(w,e.Input.Reference!.Value,SocialAssetAction.PlanTea),plan)),"この茶会は開催済みです");Need(Attendees(w,plan).Length>0,"承諾した客がまだ現地にいません");Need(plan.Input.Reference==null||Inventory(w,actor).Any(o=>o.Id==plan.Input.Reference&&o.Kind=="tea"),"披露予定の茶器が手元にありません");cost=5*(1+Attendees(w,plan).Length);break;
   default:throw new InvalidDataException("未対応の用件です");
  }
  Need(cost<=NewGameWork.Balance(w,actor),"手持ちの銭が足りません");Add(w,i,actor,site!.Value,cost);NewGameWork.Sync(w);
 }
 static bool SameTea(SocialAssetEvent a,SocialAssetEvent b)=>a.Actor==b.Actor&&a.Site==b.Site&&a.Input.When==b.Input.When&&a.Input.Reference==b.Input.Reference;
 static EntityId[] Attendees(ContractWorld w,SocialAssetEvent plan){var letters=Events(w).Where(e=>e.Input.Action==SocialAssetAction.PlanTea&&SameTea(e,plan)).Select(e=>e.Input.Id).ToHashSet();return Events(w).Where(e=>e.Input.Action==SocialAssetAction.AcceptTea&&e.Input.Reference is {} letter&&letters.Contains(letter)&&SocialWorld.Site(w,e.Actor)==plan.Site).Select(e=>e.Actor).Distinct().ToArray();}
 public static RelationshipFact[] Facts(ContractWorld w,EntityId observer,EntityId subject)
 {
  var rows=new List<RelationshipFact>();foreach(var e in Events(w)){
   if(e.Input.Action==SocialAssetAction.PresentIntroduction&&(observer==e.Actor&&subject==e.Input.Person||subject==e.Actor&&observer==e.Input.Person))rows.Add(new(observer,subject,e.Input.Id.Value,"紹介状を本人に届けて会った",e.At,e.At,3,1,0,0));
   if(e.Input.Action==SocialAssetAction.HoldTea){var plan=Source(w,e.Input.Reference!.Value,SocialAssetAction.PlanTea);var participants=ParticipantsAt(w,e);if(observer!=subject&&participants.Contains(observer)&&participants.Contains(subject))rows.Add(new(observer,subject,e.Input.Id.Value,"同じ茶会に実際に参集した",e.At,e.At,plan.Input.Reference==null?2:4,1,0,0));}
  }return rows.ToArray();
 }
 // 開催時点の同席者を記録から取得する。現在の所在から過去の事実を作り替えない。
 static EntityId[] ParticipantsAt(ContractWorld w,SocialAssetEvent held)=>heldParticipants(w,held);
 static EntityId[] heldParticipants(ContractWorld w,SocialAssetEvent held)=>held.Input.People??[held.Actor];
 public static RenownFact[] Renown(ContractWorld w,EntityId observer)=>Events(w).Where(e=>e.Input.Action==SocialAssetAction.HoldTea&&ParticipantsAt(w,e).Contains(observer)).Select(e=>new RenownFact(e.Input.Id.Value,observer,e.Actor,RenownKind.Culture,e.At,e.At,e.Site,"実際に参集した茶会の記録",false)).ToArray();
 public static void Tick(ContractWorld w,bool clockAdvanced=false)
 {
  if(w.SocialAssets==null)return;
  if(clockAdvanced&&w.ElapsedSeconds%86400==0){
   foreach(var actor in w.Characters.Where(c=>c.Id!=w.PlayerControl&&c.Life==LifeState.Adult).Select(c=>c.Id).OrderBy(c=>c.Value,StringComparer.Ordinal)){
    var site=SocialWorld.Site(w,actor);if(site==null||NewGameWork.Balance(w,actor)<100)continue;
    long month=w.ElapsedSeconds/(30*86400L);var id=Id("npc-tea",actor.Value+"/"+month);if(OwnIdUsed(w,id))continue;
    var guest=w.Characters.Where(c=>c.Id!=actor&&c.Life==LifeState.Adult&&SocialWorld.Site(w,c.Id)==site&&SocialWorld.Axes(w,actor,c.Id).Affection>=2).Select(c=>c.Id).OrderBy(c=>c.Value,StringComparer.Ordinal).FirstOrDefault();if(guest.Value==null)continue;
    Add(w,new(id,SocialAssetAction.PlanTea,guest,When:w.ElapsedSeconds+3600),actor,site.Value);
    SocialWorld.Execute(w,new(Id("npc-invite",id.Value),SocialWorldAction.DispatchGift,guest){Asset=id},actor);
   }
  }
  if(clockAdvanced&&w.ElapsedSeconds%86400==0)SocialWorld.Tick(w);
  foreach(var plan in Events(w).Where(e=>e.Input.Action==SocialAssetAction.PlanTea&&e.Input.Person!=w.PlayerControl&&e.Input.When>w.ElapsedSeconds).ToArray()){
   var guest=plan.Input.Person;if(!Inventory(w,guest).Any(o=>o.Id==plan.Input.Id)||Events(w).Any(e=>e.Input.Reference==plan.Input.Id&&e.Input.Action is SocialAssetAction.AcceptTea or SocialAssetAction.DeclineTea))continue;
   var axes=SocialWorld.Axes(w,guest,plan.Actor);var action=axes.Hostility>axes.Affection+20||SocialWorld.Site(w,guest)!=plan.Site?SocialAssetAction.DeclineTea:SocialAssetAction.AcceptTea;Add(w,new(Id("tea-reply",plan.Input.Id.Value),action,plan.Actor,plan.Input.Id),guest,SocialWorld.Site(w,guest)??plan.Site);
  }
  foreach(var plan in Events(w).Where(e=>e.Input.Action==SocialAssetAction.PlanTea&&e.Actor!=w.PlayerControl&&e.Input.When<=w.ElapsedSeconds&&w.ElapsedSeconds<=e.Input.When+3600).ToArray()){
   if(Events(w).Any(e=>e.Input.Action==SocialAssetAction.HoldTea&&SameTea(Source(w,e.Input.Reference!.Value,SocialAssetAction.PlanTea),plan))||SocialWorld.Site(w,plan.Actor)!=plan.Site||Attendees(w,plan).Length==0)continue;
   var input=new SocialAssetInput(Id("npc-held",plan.Input.Id.Value),SocialAssetAction.HoldTea,plan.Actor,plan.Input.Id);PrepareHold(w,ref input);try{Execute(w,input,plan.Actor);}catch(InvalidDataException){}
  }
 }
 internal static long? NextBoundary(ContractWorld w,long target)=>Events(w).Where(e=>e.Input.Action==SocialAssetAction.PlanTea&&e.Input.When>w.ElapsedSeconds&&e.Input.When<=target).Select(e=>(long?)e.Input.When).Order().FirstOrDefault();
 internal static void PrepareHold(ContractWorld w,ref SocialAssetInput i){if(i.Action==SocialAssetAction.HoldTea){var p=Source(w,i.Reference!.Value,SocialAssetAction.PlanTea);i=i with{People=[p.Actor,..Attendees(w,p)]};}}
 internal static void Rewind(ContractWorld w,long at,long revision){if(w.SocialAssets!=null){var es=Events(w).Where(e=>e.At<at||e.At==at&&e.Revision<=revision).ToArray();w.SocialAssets=es.Length==0?null:w.SocialAssets with{Events=es};}}
 public static void Validate(ContractWorld w,ContractWorld? prior)
 {
  if(w.SocialAssets==null){Need(prior?.SocialAssets==null,"私有物の記録が消えています");return;}Need(NewGameWork.Enabled(w)&&w.SocialAssets.Schema==1&&w.SocialAssets.Policy==Policy,"私有物の版が不正です");var es=Events(w);Need(es.Select(e=>e.Input.Id).Distinct().Count()==es.Length&&es.All(e=>e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence&&e.Cost>=0&&Enum.IsDefined(e.Input.Action)&&w.Characters.Any(c=>c.Id==e.Actor)),"私有物の原本が不正です");
  if(prior?.SocialAssets!=null)Need(ContractJson.CanonicalHash(es.Take(prior.SocialAssets.Events.Length).ToArray())==ContractJson.CanonicalHash(prior.SocialAssets.Events),"過去の私有物の記録が変更されています");
  Need(es.Where(e=>e.Input.Action==SocialAssetAction.Buy).GroupBy(e=>e.Input.Reference).All(g=>g.Count()==1),"一点物が二重購入されています");
  foreach(var e in es){if(e.Input.Action==SocialAssetAction.Buy)Need(Catalog(w,e.Site).Any(a=>a.Id==e.Input.Reference&&a.Price==e.Cost),"名物の取得価格が違います");if(e.Input.Action==SocialAssetAction.HoldTea){var plan=Source(w,e.Input.Reference!.Value,SocialAssetAction.PlanTea);Need(e.Input.People is {Length:>=2} people&&people.Distinct().Count()==people.Length&&people.Contains(e.Actor)&&e.Cost==people.Length*5,"茶会の同席記録が不正です");Need(plan.Actor==e.Actor&&plan.Site==e.Site&&plan.Input.When<=e.At&&e.At<=plan.Input.When+3600,"開催原本と違います");foreach(var guest in e.Input.People!.Where(p=>p!=e.Actor))Need(Events(w).Any(reply=>reply.Input.Action==SocialAssetAction.AcceptTea&&reply.Actor==guest&&reply.Revision<=e.Revision&&SameTea(Source(w,reply.Input.Reference!.Value,SocialAssetAction.PlanTea),plan))&&TransportWork.At(w,guest,e.Site,e.At,e.Revision,stableCompletion:true),"未承諾・未到着の客が同席しています");}}
  _=Objects(w);foreach(var actor in w.Characters.Select(c=>c.Id))Need(CarryingMass(w,actor)+TradeMass(w,actor)<=RegionalTrade.Capacity,"私有物と贈答の携行上限100kgを超えています");
  foreach(var gift in SocialWorld.Ledger(w).Where(e=>e.Input.Action==SocialWorldAction.DispatchGift&&e.Input.Asset!=null)){
   var item=Objects(w,gift.Revision).SingleOrDefault(o=>o.Id==gift.Input.Asset);Need(item!=null&&item.Owner==gift.Actor&&item.Holder==gift.Actor&&item.Gift==null&&!item.Consumed,"引渡し時点で使える私有物ではありません");
  }
  foreach(var q in w.Trade?.Entries.Where(e=>e.Input.Action==TradeAction.GiftOut)??[])Need(SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.DispatchGift&&e.Input.Asset==q.Input.Contract&&e.Revision==q.Revision),"贈答への物資引渡し原本がありません");
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageSocialAsset(SocialAssetInput input,long revision)
 {
  lock(gate){var old=state.SocialAssets?.Events.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input with{People=null}==input with{People=null}?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"同じ用件は受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");if(input.People!=null)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"参集者は実際の所在から確定します");if(NewGameWork.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");try{Change(w=>{var actual=input;SocialAssets.PrepareHold(w,ref actual);SocialAssets.Execute(w,actual,w.PlayerControl);});return new(ResultCode.Accepted,input.Id,state.CommitSequence,"現地の品物・文書・茶会を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}
 }
}
