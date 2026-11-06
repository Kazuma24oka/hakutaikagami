using HakutaiKagami.Core.Messaging;
using System.Text;
using System.Text.Json.Serialization;
namespace HakutaiKagami.Core.Contracts;
public enum CourtAction { RecruitOffer,RecruitAccept,RecruitDecline,ReturnSigning,ResignService,Nominate,AcceptOffice,DeclineOffice,RevokeOffice,ResignOffice,DeclareScale,Convene,AcceptInvitation,DeclineInvitation,HoldCouncil,SubmitCase,ApproveCase,DenyCase,ReviseCase,DeliverDocument,ConsultReform,DeferReform,OpenLocalMarket,GrantTempleSeat,ChallengeSeat,InsistSeat,SettleSeat }
public enum CourtRole { Elder,Steward,Castellan,Mediator,TeaMaster,Commander,WorksMagistrate,TownMagistrate }
public enum CourtScale { Local,Daimyo,Great,Hegemon }
public enum CourtCaseKind { RelieveVillage,BuyWood,BuyWeapons,SetBudget }
public sealed record CourtInput(EntityId Id,CourtAction Action,EntityId Person,EntityId Owner,EntityId Site,CourtRole Role=CourtRole.Steward,long Amount=0,EntityId? Reference=null,long When=0,CourtCaseKind Case=CourtCaseKind.RelieveVillage,CourtScale Scale=CourtScale.Local,EntityId? Carrier=null)
{[JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public ReformKind? Reform{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId[]? People{get;init;}}
public sealed record CourtEvent(CourtInput Input,EntityId Actor,EntityId Place,long At,long Revision,bool PersonalAlreadyBound);
public sealed record CourtState(int Schema,string Policy,CourtEvent[] Events);
public sealed record CourtDocument(EntityId Id,CourtEvent Source,EntityId Sender,EntityId Recipient,string Name);
public static class HouseholdCourt
{
 public const string Policy="physical-recruitment-standing-office-local-council-v1";
 internal static CourtEvent[] Events(ContractWorld w)=>w.Court?.Events??[];
 static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool c,string why){if(!c)throw new InvalidDataException(why);}
 internal static EntityId Id(string kind,string key)=>new("court:"+kind+"/"+ContractJson.Hash(Encoding.UTF8.GetBytes(key))[..24]);
 static bool Before(CourtEvent e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
 public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Events(w).Any(e=>e.Input.Id==id);
 static CourtEvent Source(ContractWorld w,EntityId? id,CourtAction action)=>Events(w).Single(e=>e.Input.Id==id&&e.Input.Action==action);
 static bool Local(ContractWorld w,EntityId a,EntityId b)=>SocialWorld.Site(w,a) is {} site&&SocialWorld.Site(w,b)==site;
 static EntityId Head(ContractWorld w,EntityId owner,long at,long rev)=>HouseSuccession.Head(w,owner,at,rev);
 static bool HeadAuthority(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev)=>Head(w,owner,at,rev)==actor&&FactionStances.ResolveAuthority(w,actor,owner,at,Math.Min(rev,w.CommitSequence))!=null&&w.LandRights.Any(r=>r.Subject==owner&&r.Target==site&&r.Kind=="control"&&r.From<=at&&at<r.Until);
 static bool Active(TimedRelation r,long at,long rev)=>r.From<=at&&(at<r.Until||at==r.Until&&rev<r.Revision);
 static bool Member(ContractWorld w,EntityId actor,EntityId owner,long at,long rev)=>w.Memberships.Any(m=>m.Subject==actor&&m.Target==owner&&Active(m,at,rev));
 static bool FreeToJoin(ContractWorld w,EntityId actor,EntityId principal)=>!HostageCustody.HeldAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1)&&!w.Memberships.Any(m=>m.Subject==actor&&Active(m,w.ElapsedSeconds,w.CommitSequence+1))&&!w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target!=principal&&Active(b,w.ElapsedSeconds,w.CommitSequence+1))&&!w.Offices.Any(o=>o.Subject==actor&&Active(o,w.ElapsedSeconds,w.CommitSequence+1));
 public static long Net(ContractWorld w,EntityId wallet,long at,long revision){long n=0;foreach(var e in Events(w).Where(e=>Before(e,at,revision))){if(e.Input.Action==CourtAction.RecruitOffer&&NewGameWork.Wallet(e.Actor)==wallet)n=checked(n-e.Input.Amount);if(e.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);var to=e.Input.Action==CourtAction.RecruitAccept?e.Actor:offer.Actor;if(NewGameWork.Wallet(to)==wallet)n=checked(n+offer.Input.Amount);}}return n;}
 public static long SigningEscrow(ContractWorld w)=>Events(w).Where(e=>e.Input.Action==CourtAction.RecruitOffer&&!Events(w).Any(a=>a.Input.Reference==e.Input.Id&&a.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning)).Sum(e=>e.Input.Amount);
 public static CourtDocument[] DocumentRows(ContractWorld w){var docs=new List<CourtDocument>();foreach(var e in Events(w)){
  var i=e.Input;if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.SubmitCase)docs.Add(new(i.Id,e,e.Actor,i.Person,i.Action==CourtAction.RecruitOffer?"登用の条件書（支度金を同封）":i.Action==CourtAction.Nominate?"常設職の任命状":i.Action==CourtAction.RevokeOffice?"役職の解任通知":"評定への上申書"));
  if(i.Action==CourtAction.Convene)foreach(var person in i.People??[])docs.Add(new(Id("invitation",i.Id.Value+"/"+person.Value),e,e.Actor,person,"評定の招待状"));
  if(i.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline or CourtAction.AcceptOffice or CourtAction.DeclineOffice){var parent=Events(w).Single(e=>e.Input.Id==i.Reference);docs.Add(new(i.Id,e,e.Actor,parent.Actor,"本人の返答書"));}
  if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);docs.Add(new(i.Id,e,e.Actor,proposal.Actor,"評定の回答書"));}
 }return docs.ToArray();}
 internal static PersonalObject[] Documents(ContractWorld w,long revision)=>DocumentRows(w).Where(d=>d.Source.Revision<=revision).Select(d=>new PersonalObject(d.Id,d.Name,"court-paper",1,d.Sender,d.Sender,d.Source.Place,null,false,["文書の到達と本人の確認・引受は別。所領・兵・私財は一括移転しない"])).ToArray();
 internal static bool ValidRecipient(ContractWorld w,EntityId document,EntityId recipient)=>DocumentRows(w).Any(d=>d.Id==document&&d.Recipient==recipient);
 static bool Received(ContractWorld w,EntityId document,EntityId recipient,long at=long.MaxValue,long rev=long.MaxValue)=>SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.ReceiveGift&&e.Actor==recipient&&e.At<=at&&e.Revision<rev&&SocialWorld.Ledger(w).Any(g=>g.Input.Id==e.Input.Reference&&g.Input.Asset==document));
 internal static PersonalObject ReturnObject(ContractWorld w,PersonalObject item,long revision){var returned=Events(w).SingleOrDefault(e=>e.Input.Action==CourtAction.ReturnSigning&&e.Input.Reference==item.Id&&e.Revision<revision);if(returned==null)return item;var original=Source(w,item.Id,CourtAction.RecruitOffer);return item with{Owner=original.Actor,Holder=original.Actor,Site=returned.Place,Consumed=true,History=[..item.History,"支度金と条件書を現地で返還"]};}
 internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,string kind,long at=long.MaxValue,long rev=long.MaxValue){var rows=original.ToList();foreach(var e in Events(w).Where(e=>e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev))){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);if(kind=="service"&&e.PersonalAlreadyBound)continue;var end=Events(w).FirstOrDefault(x=>x.Input.Action==CourtAction.ResignService&&x.Input.Reference==offer.Input.Id&&Before(x,at,rev));rows.Add(new(Id(kind,e.Input.Id.Value),e.Actor,kind=="membership"?offer.Input.Owner:offer.Actor,kind,NewGameWorld.Basis,e.At,end?.At??long.MaxValue,end?.Revision??e.Revision));}return rows.ToArray();}
 public static CourtScale Scale(ContractWorld w,EntityId owner,long at=long.MaxValue,long rev=long.MaxValue)=>Events(w).Where(e=>e.Input.Action==CourtAction.DeclareScale&&e.Input.Owner==owner&&Before(e,at,rev)).LastOrDefault()?.Input.Scale??CourtScale.Local;
 public static int SeatLimit(CourtScale scale,CourtRole role)=>role switch
 {
     CourtRole.Elder or CourtRole.Steward => 1+(int)scale,
     CourtRole.Castellan => scale>=CourtScale.Daimyo?1:0,
     CourtRole.Mediator or CourtRole.Commander => (int)scale,
     CourtRole.WorksMagistrate or CourtRole.TownMagistrate => scale>=CourtScale.Great?1:0,
     CourtRole.TeaMaster => scale>=CourtScale.Daimyo?1:0,
     _=>0
 };

 static bool Vacancy(ContractWorld w,EntityId owner,EntityId site,CourtRole role,long at,long rev)
 {
     var occupied=Events(w).Count(e=>e.Input.Action==CourtAction.AcceptOffice && Before(e,at,rev) &&
         at<EndOffice(w,e,at,rev) && Source(w,e.Input.Reference,CourtAction.Nominate).Input is {} offer &&
         offer.Owner==owner && offer.Site==site && offer.Role==role);
     return occupied<SeatLimit(Scale(w,owner,at,rev),role);
 }
 public static string RoleName(CourtRole role,CourtScale scale)=>role switch{CourtRole.Elder=>scale>=CourtScale.Great?"宿老":"家老",CourtRole.Steward=>scale>=CourtScale.Daimyo?"奉行":"代官",CourtRole.Castellan=>"城主（常設）",CourtRole.WorksMagistrate=>"普請奉行",CourtRole.TownMagistrate=>"町奉行",CourtRole.Mediator=>"取次",CourtRole.TeaMaster=>"茶頭",_=>"部将（指揮は別契約）"};
 public static string Seat(ContractWorld w,EntityId principal,EntityId person,long at=long.MaxValue,long revision=long.MaxValue){bool family=w.Adoption?.Entries.Any(e=>e.Input.Action==AdoptionAction.Implement&&(e.At<at||e.At==at&&e.Revision<revision)&&w.Adoption.Entries.Any(q=>q.Terms is {} t&&q.Input.Id==e.Input.Original&&t.Parent==principal&&t.Child==person))==true||w.Marriage?.Entries.Any(e=>e.Input.Action==MarriageAction.Implement&&(e.At<at||e.At==at&&e.Revision<revision)&&w.Marriage.Entries.Any(q=>q.Terms is {} t&&q.Input.Id==e.Input.Original&&(t.First==principal&&t.Second==person||t.Second==principal&&t.First==person)))==true;if(family)return "一門（本人が知る縁組）";var original=w.NewGame!.Start.People.Single(p=>p.Id==person.Value);return !original.Created&&original.Lord==principal.Value?"譜代（開始資料の根拠）":Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==person)?"新参（登用の合意）":"新参・席次の来歴は未採録";}
 public static bool CanConvene(ContractWorld w,EntityId actor,EntityId owner,EntityId site){if(HeadAuthority(w,actor,owner,site,w.ElapsedSeconds,w.CommitSequence+1))return true;var appointment=Appointment(w,actor,owner,site,w.ElapsedSeconds,w.CommitSequence+1);return appointment!=null&&Source(w,appointment.Input.Reference,CourtAction.Nominate).Input.Role==CourtRole.Elder;}
 static long EndOffice(ContractWorld w,CourtEvent accepted,long at,long rev){var offer=Source(w,accepted.Input.Reference,CourtAction.Nominate);var ends=Events(w).Where(e=>Before(e,at,rev)&&(e.Input.Action==CourtAction.ResignOffice&&e.Input.Reference==offer.Input.Id||e.Input.Action==CourtAction.RevokeOffice&&e.Input.Reference==offer.Input.Id&&Received(w,e.Input.Id,accepted.Actor,at,rev))).Select(e=>e.Input.Action==CourtAction.RevokeOffice?SocialWorld.Ledger(w).Where(r=>r.Input.Action==SocialWorldAction.ReceiveGift&&SocialWorld.Ledger(w).Any(g=>g.Input.Id==r.Input.Reference&&g.Input.Asset==e.Input.Id)).Min(r=>r.At):e.At);var succession=w.Succession?.Entries.Where(e=>e.Input.Action==HouseSuccessionAction.Resolve&&e.Outcome=="confirmed"&&e.Input.Owner==offer.Input.Owner&&e.At>=accepted.At&&(e.At<at||e.At==at&&e.Revision<rev)).Select(e=>e.At)??[];return ends.Concat(succession).DefaultIfEmpty(long.MaxValue).Min();}
 internal static TimedRelation[] Offices(ContractWorld w,TimedRelation[] original,long at=long.MaxValue,long rev=long.MaxValue)=>[..original,..Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&Before(e,at,rev)).Select(e=>{var offer=Source(w,e.Input.Reference,CourtAction.Nominate);return new TimedRelation(Id("office",offer.Input.Id.Value),e.Actor,offer.Input.Site,"house-"+offer.Input.Role.ToString().ToLowerInvariant(),NewGameWorld.Basis,e.At,EndOffice(w,e,at,rev),e.Revision);})];
 static CourtEvent? Appointment(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev)=>Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&e.Actor==actor&&Before(e,at,rev)&&e.At<=at&&at<EndOffice(w,e,at,rev)).LastOrDefault(e=>{var offer=Source(w,e.Input.Reference,CourtAction.Nominate);return offer.Input.Owner==owner&&offer.Input.Site==site;});
 internal static CourtRole? Specialist(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
 {
     if(RegionalDomains.Owner(w,site) is not {} owner || Appointment(w,actor,owner,site,at,rev) is not {} appointment)
         return null;
     var offer=Source(w,appointment.Input.Reference,CourtAction.Nominate);
     if(offer.Input.Role is not (CourtRole.WorksMagistrate or CourtRole.TownMagistrate))return null;
     return Member(w,actor,owner,at,rev) && HeadAuthority(w,offer.Actor,owner,site,at,rev) &&
         !HostageCustody.HeldAt(w,actor,at,rev) && !CastleAdministration.Blocked(w,site,at,rev)?offer.Input.Role:null;
 }
 internal static FactionStanceAuthority? Authority(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev,bool treasury=false){if(HostageCustody.HeldAt(w,actor,at,rev))return null;if(CastleAdministration.Blocked(w,site,at,rev))return null;var accepted=Appointment(w,actor,owner,site,at,rev);if(accepted==null||!Member(w,actor,owner,at,rev))return null;var offer=Source(w,accepted.Input.Reference,CourtAction.Nominate);if(offer.Input.Role is not(CourtRole.Steward or CourtRole.Castellan))return null;var source=FactionStances.ResolveAuthority(w,offer.Actor,owner,at,Math.Min(rev,w.CommitSequence));if(source==null||!HeadAuthority(w,offer.Actor,owner,site,at,rev))return null;var member=w.Memberships.Last(m=>m.Subject==actor&&m.Target==owner&&Active(m,at,rev));return source with{Issuer=actor,Office=Id("office",offer.Input.Id.Value),Membership=member.Id};}
 internal static long Budget(ContractWorld w,EntityId actor,EntityId site,EconomyAction action,long at,long rev){var epoch=DateOnly.Parse(w.EpochDate);var month=epoch.AddDays((int)(at/86400));long used=w.Communities?.Orders.Where(e=>e.Actor==actor&&e.Input.Site==site&&e.Input.Action==action&&(e.At<at||e.At==at&&e.Revision<rev)&&epoch.AddDays((int)(e.At/86400)).Year==month.Year&&epoch.AddDays((int)(e.At/86400)).Month==month.Month).Sum(e=>e.Input.Amount)??0;return Math.Max(0,(action==EconomyAction.CollectLevy?1000:200)-used);}
 internal static void Sync(ContractWorld w){if(w.Court==null)return;var original=NewGameWorld.InitialRelations(w.NewGame!);w.Memberships=NewGameService.Relations(w,original.Members,"membership");w.ServiceBonds=NewGameService.Relations(w,original.Bonds,"service");w.Offices=Offices(w,HouseSuccession.Offices(w,original.Offices));}
 static void Add(ContractWorld w,CourtInput i,EntityId actor,EntityId place,bool alreadyBound=false){w.Court??=new(1,Policy,[]);w.Court=w.Court with{Events=[..w.Court.Events,new(i,actor,place,w.ElapsedSeconds,w.CommitSequence,alreadyBound)]};}
 static CourtDocument Document(ContractWorld w,EntityId id)=>DocumentRows(w).Single(d=>d.Id==id);
 static void Deliver(ContractWorld w,CourtDocument doc,EntityId actor,EntityId? carrier){Need(SocialAssets.Inventory(w,actor).Any(o=>o.Id==doc.Id),"自分が持つ実物を引き渡してください");SocialWorld.Execute(w,new(Id("delivery",doc.Id.Value),SocialWorldAction.DispatchGift,doc.Recipient,carrier){Asset=doc.Id},actor);}
 static void CheckSeatDispute(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {
  var grant=HouseReforms.TempleMarketSeat(w,i.Site,at,revision);
  Need(grant!=null && i.Reference==grant.Input.Id && i.Person==grant.Input.Person &&
      place==i.Site && HeadAuthority(w,actor,i.Owner,i.Site,at,revision) &&
      HouseReforms.OpenMarketAt(w,i.Site,at,revision),"現地の代表者、安堵状、楽市、現在の当主権限を確認してください");
  var previous=HouseReforms.SeatDispute(w,i.Site,at,revision);
  Need(i.Action switch{
   CourtAction.ChallengeSeat=>previous==null,
   CourtAction.InsistSeat=>previous?.Input.Action==CourtAction.ChallengeSeat && previous.At<at,
   CourtAction.SettleSeat=>previous?.Input.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat,
   _=>false},"この安堵状について現在選べる処置ではありません");
 }
 static void CheckTempleSeat(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {
  Need(place==i.Site && HeadAuthority(w,actor,i.Owner,i.Site,at,revision) &&
      i.Person==SocialWorld.Temple(w,i.Site).Representative && MaterialMarkets.Enabled(w) &&
      MaterialMarkets.Sites(w).Contains(i.Site),"市場を支配する当主と現地の寺社代表を確認してください");
  Need(SocialWorld.Ledger(w).Any(e=>e.Actor==actor && e.Input.Person==i.Person && e.Site==i.Site &&
      (e.Input.Action is SocialWorldAction.Donate or SocialWorldAction.Protect or SocialWorldAction.IssueBan) && e.Revision<revision && e.At<=at),"先に現地で寺社代表と寄進・保護・禁制のいずれかを取り交わしてください");
  Need(HouseReforms.TempleMarketSeat(w,i.Site,at,revision)==null &&
      !HouseReforms.OpenMarketAt(w,i.Site,at,revision),"この市場では新しく座を安堵できません");
 }
 static void CheckReformDecision(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long revision)
 {
  var proposal=Source(w,input.Reference,CourtAction.ConsultReform);
  Need(proposal.Revision<revision && proposal.Actor==actor && proposal.Input.Owner==input.Owner &&
      proposal.Input.Site==input.Site && proposal.Input.Person==input.Person && place==input.Site &&
      HeadAuthority(w,actor,input.Owner,input.Site,at,revision),"相談原本と現地の当主権限を確認してください");
  if(input.Action==CourtAction.OpenLocalMarket){
   Need(proposal.Input.Reform==ReformKind.OpenMarket && MaterialMarkets.Sites(w).Contains(input.Site),
       "実在する現地市場の楽市案を選んでください");
   Need(!HouseReforms.OpenMarketAt(w,input.Site,at,revision),"この市場ではすでに楽市を実施しています");
  }
  var readAt=MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(proposal.Input,HouseReforms.Answer),actor.Value);
  Need(readAt.HasValue && readAt.Value<=at,"相手の返書を読んでから判断してください");
  Need(!Events(w).Any(e=>e.Revision<revision && (e.Input.Action is CourtAction.DeferReform or CourtAction.OpenLocalMarket) &&
      e.Input.Reference==proposal.Input.Id),"この相談案は決裁済みです");
 }
 public static void Execute(ContractWorld w,CourtInput i,EntityId actor)
 {
  Need(NewGameWork.Enabled(w)&&w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&Enum.IsDefined(i.Action)&&Enum.IsDefined(i.Role)&&Enum.IsDefined(i.Case)&&Enum.IsDefined(i.Scale),"家中の用件が不正です");var place=SocialWorld.Site(w,actor);Need(place!=null,"現地へ到着してください");Need(HouseSuccession.Factions(w).Any(f=>f.Id==i.Owner.Value)&&w.Definitions.Sites.Any(s=>s.Id==i.Site),"家と拠点を指定してください");
  Need(i.Action==CourtAction.ConsultReform || i.Reform==null,"改革の指定は家中改革の相談に限ります");
  bool leader=HeadAuthority(w,actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1),bound=false;
  if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.DeclareScale)Need(leader&&place==i.Site,"その拠点を支配する当主の現地判断が必要です");
  if(i.Action==CourtAction.RecruitOffer){Need(i.Person!=actor&&w.Characters.Any(c=>c.Id==i.Person&&c.Life==LifeState.Adult)&&i.Amount is >=20 and <=500&&i.Amount<=NewGameWork.Balance(w,actor),"成人への条件と実在の私財の支度金20〜500文を指定してください");Need(!Events(w).Any(e=>e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.Owner==i.Owner&&!Events(w).Any(r=>r.Input.Reference==e.Input.Id&&r.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning)),"先の条件書と支度金を先に精算してください");}
  else if(i.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline or CourtAction.ReturnSigning){var offer=Source(w,i.Reference,CourtAction.RecruitOffer);Need(offer.Input.Person==actor&&i.Owner==offer.Input.Owner&&i.Site==offer.Input.Site,"本人宛ての同じ条件書を選んでください");Need(SocialAssets.Inventory(w,actor).Any(o=>o.Id==offer.Input.Id),"実物の条件書と支度金がまだ届いていません");if(i.Action==CourtAction.ReturnSigning)Need(Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.RecruitDecline)&&Local(w,actor,offer.Actor)&&!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.ReturnSigning),"断った条件書と支度金を本人へ持ち帰ってください");else {Need(!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline),"本人は返答済みです");if(i.Action==CourtAction.RecruitAccept){Need(w.ElapsedSeconds<=offer.At+30*86400L&&HeadAuthority(w,offer.Actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1)&&FreeToJoin(w,actor,offer.Actor),"期限・署名者の権限・本人の所属と返納を確認してください");bound=w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==offer.Actor&&Active(b,w.ElapsedSeconds,w.CommitSequence+1));}}}
  else if(i.Action==CourtAction.ResignService){var accepted=Source(w,i.Reference,CourtAction.RecruitOffer);Need(Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==actor&&e.Input.Reference==accepted.Input.Id)&&Local(w,actor,accepted.Actor),"自分の登用原本を示し、元の主君と現地で確認してください");Need(Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Input.Reference==accepted.Input.Id&&e.At<w.ElapsedSeconds)&&!Events(w).Any(e=>e.Input.Reference==accepted.Input.Id&&e.Input.Action==i.Action)&&!w.Tasks.Any(t=>t.Executor==actor&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Failed or ExecutionPhase.Stopped))&&!w.Units.Any(u=>u.Commander==actor)&&!w.ResourceLots.Any(l=>l.Holder==actor&&l.Owner!=actor&&l.Quantity>0)&&!Offices(w,[]).Any(o=>o.Subject==actor&&Active(o,w.ElapsedSeconds,w.CommitSequence+1)),"実務と常設職を返してから所属を終えてください");}
  else if(i.Action==CourtAction.Nominate){Need(i.Person!=actor&&Member(w,i.Person,i.Owner,w.ElapsedSeconds,w.CommitSequence+1),"同じ家の人物へ条件を示してください");Need(Vacancy(w,i.Owner,i.Site,i.Role,w.ElapsedSeconds,w.CommitSequence+1),"現在の家の規模ではこの役割に空席がありません");if(i.Role==CourtRole.Elder)Need(Seat(w,actor,i.Person).StartsWith("一門")||Seat(w,actor,i.Person).StartsWith("譜代")||MeritRewards.Records(w,actor).Count(r=>r.Subject==i.Person)>=3,"新参の重臣登用には、自分が認めた複数の働きが必要です");}
  else if(i.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice){var offer=Source(w,i.Reference,CourtAction.Nominate);Need(offer.Input.Person==actor&&i.Owner==offer.Input.Owner&&i.Site==offer.Input.Site&&SocialAssets.Inventory(w,actor).Any(o=>o.Id==offer.Input.Id),"本人の任命状を実際に確認してください");Need(!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice),"この任命には返答済みです");if(i.Action==CourtAction.AcceptOffice)Need(Vacancy(w,i.Owner,i.Site,offer.Input.Role,w.ElapsedSeconds,w.CommitSequence+1)&&HeadAuthority(w,offer.Actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1)&&Member(w,actor,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&!Offices(w,[]).Any(o=>o.Subject==actor&&o.Target==i.Site&&Active(o,w.ElapsedSeconds,w.CommitSequence+1)),"現在の署名権・所属・兼務を確認してください");}
  else if(i.Action is CourtAction.RevokeOffice or CourtAction.ResignOffice){var offer=Source(w,i.Reference,CourtAction.Nominate);Need(Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.AcceptOffice&&e.At<w.ElapsedSeconds),"引受後、時刻を進めてから終了の手続きをしてください");Need(i.Action==CourtAction.RevokeOffice?actor==offer.Actor&&i.Person==offer.Input.Person:actor==offer.Input.Person&&Local(w,actor,Head(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)),"任命者の解任通知か、本人の現地辞任が必要です");}
  else if(i.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat or CourtAction.SettleSeat)CheckSeatDispute(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.GrantTempleSeat)CheckTempleSeat(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.DeferReform or CourtAction.OpenLocalMarket)CheckReformDecision(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ConsultReform){
   Need(leader&&place==i.Site&&i.Person!=actor&&Member(w,i.Person,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&
       i.Reform is {} kind && Enum.IsDefined(kind),"現地の当主・所属する相談相手・改革案を指定してください");
   Need(!Events(w).Any(e=>e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.Owner==i.Owner&&e.Input.Site==i.Site&&
       e.Input.Reform==i.Reform&&!HouseReforms.AnswerKnown(w,e,actor)),"同じ相手からの返書を待っています");
  }
  else if(i.Action==CourtAction.DeclareScale){var footprint=HouseTerritoryScale.Measure(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1);Need(i.Scale>Scale(w,i.Owner)&&footprint.Supports(i.Scale),"支配拠点の実態が宣言の条件に届いていません");}
  else if(i.Action==CourtAction.Convene){Need(CanConvene(w,actor,i.Owner,i.Site)&&place==i.Site&&i.When>=w.ElapsedSeconds+3600&&i.When<=w.ElapsedSeconds+30*86400L&&i.People is {Length:>0}&&i.People.Distinct().Count()==i.People.Length&&i.People.All(p=>p!=actor&&Member(w,p,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)),"当主が現地で家中の相手と開催時刻を指定してください");}
  else if(i.Action is CourtAction.AcceptInvitation or CourtAction.DeclineInvitation){Need(i.Reference!=null,"届いた招待状を選んでください");var doc=Document(w,i.Reference.Value);Need(doc.Source.Input.Action==CourtAction.Convene&&doc.Recipient==actor&&SocialAssets.Inventory(w,actor).Any(o=>o.Id==doc.Id)&&w.ElapsedSeconds<doc.Source.Input.When,"本人宛ての招待状と開催時刻を確認してください");Need(!Events(w).Any(e=>e.Input.Reference==doc.Id&&e.Input.Action is CourtAction.AcceptInvitation or CourtAction.DeclineInvitation),"本人は返答済みです");}
  else if(i.Action==CourtAction.HoldCouncil){var meeting=Source(w,i.Reference,CourtAction.Convene);i=i with{People=[actor,..Attendees(w,meeting).Where(p=>p!=actor)]};Need((meeting.Actor==actor||DocumentRows(w).Any(d=>d.Source.Input.Id==meeting.Input.Id&&d.Recipient==actor&&Events(w).Any(reply=>reply.Input.Action==CourtAction.AcceptInvitation&&reply.Actor==actor&&reply.Input.Reference==d.Id)))&&leader&&place==meeting.Input.Site&&w.ElapsedSeconds>=meeting.Input.When&&w.ElapsedSeconds<=meeting.Input.When+3600&&!Events(w).Any(e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.Reference==meeting.Input.Id),"予定の場所と時刻に当主本人が集まってください");}
  else if(i.Action==CourtAction.SubmitCase){Need(i.Person!=actor&&Member(w,actor,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&i.Amount is >=1 and <=200,"家中の本人が実行可能な範囲の上申を作ってください");Need(i.Person==Head(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)||w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==i.Person&&Active(b,w.ElapsedSeconds,w.CommitSequence+1)),"知っている主君に上申してください");}
  else if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);Need(leader&&proposal.Input.Owner==i.Owner&&proposal.Input.Site==i.Site&&Received(w,proposal.Input.Id,actor)&&place==i.Site,"届いて確認した自分の管轄の上申だけを審査してください");Need(!Events(w).Any(e=>e.Input.Reference==proposal.Input.Id&&e.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase),"この案件は審査済みです");Need(Events(w).Any(e=>e.Actor==actor&&e.Input.Action==CourtAction.HoldCouncil&&e.Input.Owner==i.Owner&&e.Place==place&&w.ElapsedSeconds<=e.At+3600),"現地で評定を開いてから案件を裁いてください");}
  else if(i.Action==CourtAction.DeliverDocument){Need(i.Reference!=null,"引き渡す文書を選んでください");Deliver(w,Document(w,i.Reference.Value),actor,i.Carrier);Add(w,i,actor,place.Value);return;}
  else throw new InvalidDataException("未対応の家中の用件です");
  Add(w,i,actor,place.Value,bound);if(i.Action==CourtAction.ConsultReform)HouseReforms.Send(w,i,actor);if(i.Action==CourtAction.DeclareScale)HouseScaleNotices.Dispatch(w,i,actor);Sync(w);NewGameWork.Sync(w);
  if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.SubmitCase)Deliver(w,Document(w,i.Id),actor,i.Carrier);
  if(i.Action==CourtAction.ApproveCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);var q=proposal.Input;if(q.Case==CourtCaseKind.RelieveVillage)CommunityEconomy.ApplyFor(w,new(Id("effect",i.Id.Value),q.Site,EconomyAction.RelieveVillage,q.Amount),actor);else RegionalDomains.Apply(w,new(Id("effect",i.Id.Value),q.Case==CourtCaseKind.SetBudget?TreasuryAction.SetBudget:TreasuryAction.Buy,q.Site,q.Case==CourtCaseKind.BuyWood?"wood":q.Case==CourtCaseKind.BuyWeapons?"weapons":null,q.Case==CourtCaseKind.SetBudget?0:q.Amount,q.Case==CourtCaseKind.SetBudget?q.Amount:0),actor);}
  if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase)Deliver(w,Document(w,i.Id),actor,i.Carrier);
 }
 static EntityId[] Attendees(ContractWorld w,CourtEvent meeting)=>DocumentRows(w).Where(d=>d.Source.Input.Id==meeting.Input.Id&&Events(w).Any(e=>e.Actor==d.Recipient&&e.Input.Reference==d.Id&&e.Input.Action==CourtAction.AcceptInvitation)&&SocialWorld.Site(w,d.Recipient)==meeting.Input.Site&&Member(w,d.Recipient,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1)).Select(d=>d.Recipient).Concat(SocialWorld.Site(w,meeting.Actor)==meeting.Input.Site?[meeting.Actor]:[]).Distinct().ToArray();
 public static bool KnowsReply(ContractWorld w,CourtEvent reply,EntityId observer){if(reply.Actor==observer||Received(w,reply.Input.Id,observer))return true;if(TransportWork.At(w,observer,reply.Place,reply.At,reply.Revision,stableCompletion:true))return true;var original=reply.Input.Reference;if(original==null)return false;var gift=SocialWorld.Ledger(w).SingleOrDefault(g=>g.Input.Id==Id("delivery",original.Value.Value));if(gift==null||gift.Actor!=observer)return false;var carrier=gift.Input.Carrier??gift.Actor;return TransportWork.At(w,carrier,reply.Place,reply.At,reply.Revision,stableCompletion:true)&&SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.AcknowledgeGiftReport&&e.Actor==observer&&e.Input.Reference==gift.Input.Id&&e.At>=reply.At);}
 public static EntityId[] Roster(ContractWorld w,EntityId observer,EntityId owner)=>w.Memberships.Where(m=>m.Target==owner&&Active(m,w.ElapsedSeconds,w.CommitSequence+1)).Select(m=>m.Subject).Distinct().Where(person=>w.NewGame!.Start.People.Any(p=>p.Id==person.Value&&p.Faction==owner.Value)||Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==person&&Source(w,e.Input.Reference,CourtAction.RecruitOffer).Input.Owner==owner&&KnowsReply(w,e,observer))).ToArray();
 public static CourtEvent[] KnownCases(ContractWorld w,EntityId actor)=>Events(w).Where(e=>e.Input.Action==CourtAction.SubmitCase&&(e.Actor==actor||Received(w,e.Input.Id,actor))).ToArray();
 public static CourtEvent? KnownDecision(ContractWorld w,EntityId actor,EntityId proposal)=>Events(w).LastOrDefault(e=>e.Input.Reference==proposal&&e.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase&&(e.Actor==actor||Received(w,e.Input.Id,actor)));
 internal static bool Bound(ContractWorld w,EntityId principal,long at,long rev)=>Events(w).Any(e=>e.Actor==w.PlayerControl&&e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev+1)&&Source(w,e.Input.Reference,CourtAction.RecruitOffer).Actor==principal&&!Events(w).Any(end=>end.Input.Action==CourtAction.ResignService&&end.Input.Reference==e.Input.Reference&&Before(end,at,rev+1)));
 internal static ServiceView Decorate(ContractWorld w,long at,long rev,ServiceView original){var joined=Events(w).LastOrDefault(e=>e.Actor==w.PlayerControl&&e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev+1)&&!Events(w).Any(end=>end.Input.Action==CourtAction.ResignService&&end.Input.Reference==e.Input.Reference&&Before(end,at,rev+1)));if(joined==null)return original;var offer=Source(w,joined.Input.Reference,CourtAction.RecruitOffer);var principal=HouseholdService.BondsAt(w,at,rev+1).LastOrDefault(b=>b.Subject==w.PlayerControl&&Active(b,at,rev+1))?.Target;return original with{Role="家臣（登用合意）",House=offer.Input.Owner,Principal=principal,CanApply=false};}
 public static CourtDocument[] PendingDocuments(ContractWorld w,EntityId actor)=>DocumentRows(w).Where(d=>d.Recipient==actor&&Received(w,d.Id,actor)).ToArray();
 public static RelationshipFact[] Facts(ContractWorld w,EntityId observer,EntityId subject){var rows=new List<RelationshipFact>();foreach(var e in Events(w)){
  if(e.Input.Action==CourtAction.RecruitAccept){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);if(observer==e.Actor&&subject==offer.Actor)rows.Add(new(observer,subject,"court:"+e.Input.Id.Value,"条件書と実物の支度金を確認し、家への所属と直属を引き受けた",e.At,e.At,3,2,0,0));}
  if(e.Input.Action==CourtAction.HoldCouncil&&e.Input.People is {} people&&observer!=subject&&people.Contains(observer)&&people.Contains(subject))rows.Add(new(observer,subject,"court:"+e.Input.Id.Value,"同じ場所の評定に実際に参集した",e.At,e.At,1,1,0,0));
 }return rows.ToArray();}
 internal static bool SignedBudgetDecision(ContractWorld w,EntityId id,EntityId actor)=>Events(w).Any(e=>e.Actor==actor&&e.Input.Action==CourtAction.ApproveCase&&Id("effect",e.Input.Id.Value)==id&&Source(w,e.Input.Reference,CourtAction.SubmitCase).Input.Case==CourtCaseKind.SetBudget);
 static bool Attempt(ContractWorld w,CourtInput input,EntityId actor)=>TransactionalWorld.TryApply(w,c=>Execute(c,input,actor));
 internal static void Tick(ContractWorld w,bool clockAdvanced=false)
 {
  if(w.Court==null)return;
  foreach(var doc in DocumentRows(w).Where(d=>d.Recipient!=w.PlayerControl&&Received(w,d.Id,d.Recipient)).ToArray()){
   var source=doc.Source;var p=doc.Recipient;if(SocialWorld.Site(w,p) is not {} site)continue;CourtAction? action=source.Input.Action switch{CourtAction.RecruitOffer=>FreeToJoin(w,p,source.Actor)&&HeadAuthority(w,source.Actor,source.Input.Owner,source.Input.Site,w.ElapsedSeconds,w.CommitSequence+1)?CourtAction.RecruitAccept:CourtAction.RecruitDecline,CourtAction.Nominate=>Vacancy(w,source.Input.Owner,source.Input.Site,source.Input.Role,w.ElapsedSeconds,w.CommitSequence+1)?CourtAction.AcceptOffice:CourtAction.DeclineOffice,CourtAction.Convene=>site==source.Place?CourtAction.AcceptInvitation:CourtAction.DeclineInvitation,_=>null};if(action==null)continue;
   var answers=source.Input.Action==CourtAction.RecruitOffer?new[]{CourtAction.RecruitAccept,CourtAction.RecruitDecline}:source.Input.Action==CourtAction.Nominate?new[]{CourtAction.AcceptOffice,CourtAction.DeclineOffice}:new[]{CourtAction.AcceptInvitation,CourtAction.DeclineInvitation};EntityId reference=source.Input.Action==CourtAction.Convene?doc.Id:source.Input.Id;if(Events(w).Any(e=>e.Actor==p&&e.Input.Reference==reference&&answers.Contains(e.Input.Action)))continue;
   int duty=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticTrait(w,p,SocialTrait.Duty):50;int caution=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticTrait(w,p,SocialTrait.Caution):50;if(SocialWorld.Axes(w,p,source.Actor).Hostility>30||duty+50<caution)action=source.Input.Action==CourtAction.RecruitOffer?CourtAction.RecruitDecline:source.Input.Action==CourtAction.Nominate?CourtAction.DeclineOffice:CourtAction.DeclineInvitation;
   Attempt(w,new(Id("reply",doc.Id.Value),action.Value,source.Actor,source.Input.Owner,source.Input.Site,Reference:reference),p);
  }
  foreach(var declined in Events(w).Where(e=>e.Input.Action==CourtAction.RecruitDecline&&e.Actor!=w.PlayerControl).ToArray()){var offer=Source(w,declined.Input.Reference,CourtAction.RecruitOffer);if(Local(w,declined.Actor,offer.Actor)&&!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.ReturnSigning))Attempt(w,new(Id("return-signing",offer.Input.Id.Value),CourtAction.ReturnSigning,offer.Actor,offer.Input.Owner,offer.Input.Site,Reference:offer.Input.Id),declined.Actor);}
  foreach(var meeting in Events(w).Where(e=>e.Input.Action==CourtAction.Convene&&Head(w,e.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1)!=w.PlayerControl&&e.Input.When<=w.ElapsedSeconds&&w.ElapsedSeconds<=e.Input.When+3600).ToArray()){
   if(!Events(w).Any(e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.Reference==meeting.Input.Id)&&SocialWorld.Site(w,Head(w,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1))==meeting.Input.Site){var chair=Head(w,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1);Attempt(w,new(Id("hold",meeting.Input.Id.Value),CourtAction.HoldCouncil,chair,meeting.Input.Owner,meeting.Input.Site,Reference:meeting.Input.Id),chair);}
  }
  foreach(var proposal in Events(w).Where(e=>e.Input.Action==CourtAction.SubmitCase).OrderBy(e=>e.Revision).ToArray()){
   var principal=proposal.Input.Person;if(principal==w.PlayerControl||!Received(w,proposal.Input.Id,principal)||Events(w).Any(e=>e.Input.Reference==proposal.Input.Id&&e.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase))continue;
   if(!Events(w).Any(e=>e.Actor==principal&&e.Input.Action==CourtAction.HoldCouncil&&e.Input.Owner==proposal.Input.Owner&&e.Place==proposal.Input.Site&&w.ElapsedSeconds<=e.At+3600))continue;
   if(Attempt(w,new(Id("decision",proposal.Input.Id.Value),CourtAction.ApproveCase,proposal.Actor,proposal.Input.Owner,proposal.Input.Site,Reference:proposal.Input.Id),principal))break;if(Attempt(w,new(Id("decision",proposal.Input.Id.Value),CourtAction.DenyCase,proposal.Actor,proposal.Input.Owner,proposal.Input.Site,Reference:proposal.Input.Id),principal))break;
  }
  if(clockAdvanced&&w.ElapsedSeconds%86400==0&&RegionalDomains.Enabled(w))foreach(var accepted in Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&e.Actor!=w.PlayerControl).ToArray()){
   var offer=Source(w,accepted.Input.Reference,CourtAction.Nominate);var actor=accepted.Actor;if(SocialWorld.Site(w,actor)!=offer.Input.Site||Authority(w,actor,offer.Input.Owner,offer.Input.Site,w.ElapsedSeconds,w.CommitSequence+1)==null)continue;
   if(Events(w).Any(e=>e.Actor==actor&&e.Input.Action==CourtAction.SubmitCase&&e.Input.Site==offer.Input.Site&&!Events(w).Any(r=>r.Input.Reference==e.Input.Id&&r.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase)))continue;
   var materials=w.Materials?.Houses.SingleOrDefault(m=>m.Site==offer.Input.Site);if(materials==null||materials.Wood>=30)continue;var id=Id("npc-case",actor.Value+"/"+w.ElapsedSeconds/(30*86400L));if(!OwnIdUsed(w,id))Attempt(w,new(id,CourtAction.SubmitCase,offer.Actor,offer.Input.Owner,offer.Input.Site,Amount:5,Case:CourtCaseKind.BuyWood),actor);
  }
  foreach(var owner in HouseSuccession.Factions(w,w.ElapsedSeconds,w.CommitSequence+1)){
   var head=new EntityId(owner.Head);var home=new EntityId(owner.Home);if(head==w.PlayerControl||SocialWorld.Site(w,head)!=home)continue;
   if(KnownCases(w,head).Any(c=>KnownDecision(w,head,c.Input.Id)==null)&&!Events(w).Any(e=>e.Actor==head&&e.Input.Action==CourtAction.Convene&&e.At/86400==w.ElapsedSeconds/86400)){
    var people=Roster(w,head,new(owner.Id)).Where(p=>p!=head).ToArray();if(people.Length>0){var meetingId=Id("npc-meeting",owner.Id+"/"+w.ElapsedSeconds/86400);if(Attempt(w,new(meetingId,CourtAction.Convene,head,new(owner.Id),home,When:w.ElapsedSeconds+3600){People=people},head))foreach(var doc in DocumentRows(w).Where(d=>d.Source.Input.Id==meetingId&&Local(w,head,d.Recipient)).ToArray())Attempt(w,new(Id("npc-invite",doc.Id.Value),CourtAction.DeliverDocument,doc.Recipient,new(owner.Id),home,Reference:doc.Id),head);}
   }
  }
 }
 internal static long? NextBoundary(ContractWorld w,long target){if(w.Court==null)return null;var dates=Events(w).Where(e=>e.Input.Action==CourtAction.Convene).Select(e=>e.Input.When).Append((w.ElapsedSeconds/86400+1)*86400);return dates.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
 internal static void Rewind(ContractWorld w,long at,long rev){CraftTrade.Rewind(w,at,rev);BrokerWork.Rewind(w,at,rev);if(w.Court!=null){var es=Events(w).Where(e=>e.At<at||e.At==at&&e.Revision<=rev).ToArray();w.Court=es.Length==0?null:w.Court with{Events=es};}}
 public static void Validate(ContractWorld w,ContractWorld? prior)
 {
  if(w.Court==null){Need(prior?.Court==null,"家中の記録が消えています");return;}Need(NewGameWork.Enabled(w)&&w.Court.Schema==1&&w.Court.Policy==Policy,"家中の版が不正です");var es=Events(w);Need(es.Select(e=>e.Input.Id).Distinct().Count()==es.Length&&es.All(e=>e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence&&e.Input.Amount>=0&&Enum.IsDefined(e.Input.Action)&&Enum.IsDefined(e.Input.Role)&&Enum.IsDefined(e.Input.Case)&&Enum.IsDefined(e.Input.Scale)&&w.Characters.Any(c=>c.Id==e.Actor)&&w.Definitions.Sites.Any(s=>s.Id==e.Place)),"家中の原本が不正です");if(prior?.Court!=null)Need(ContractJson.CanonicalHash(es.Take(prior.Court.Events.Length).ToArray())==ContractJson.CanonicalHash(prior.Court.Events),"過去の家中の記録が変更されています");
  foreach(var e in es){var i=e.Input;
   if(i.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat or CourtAction.SettleSeat)CheckSeatDispute(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.GrantTempleSeat)CheckTempleSeat(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.DeferReform or CourtAction.OpenLocalMarket)CheckReformDecision(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.ConsultReform){
    Need(i.Reform is {} kind && Enum.IsDefined(kind) && i.Site==e.Place &&
        HeadAuthority(w,e.Actor,i.Owner,i.Site,e.At,e.Revision) && i.Person!=e.Actor &&
        Member(w,i.Person,i.Owner,e.At,e.Revision),"相談時点の権限・当事者・案が一致しません");
    var id=HouseReforms.Letter(i,HouseReforms.Proposal);
    Need(w.SealedMessages.Contains(new EntityId(id)) && w.Messaging.Messages.Any(m=>m.Id==id &&
        m.CorrelationId==i.Id.Value && m.Kind==HouseReforms.Proposal && m.SenderId==e.Actor.Value &&
        m.RecipientId==i.Person.Value),"相談の原本と宛先が一致しません");
   }else Need(i.Reform==null,"別の用件に改革案が混在しています");
   if(i.Action==CourtAction.Nominate&&i.Role==CourtRole.Elder)Need(Seat(w,e.Actor,i.Person,e.At,e.Revision).StartsWith("一門")||Seat(w,e.Actor,i.Person,e.At,e.Revision).StartsWith("譜代")||MeritRewards.Records(w,e.Actor,e.At,e.Revision).Count(r=>r.Subject==i.Person)>=3,"任命時点の席次と働きの根拠がありません");
   if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.DeclareScale)Need(e.Place==i.Site&&HeadAuthority(w,e.Actor,i.Owner,i.Site,e.At,e.Revision),"発令当時の当主の管轄がありません");
   if(i.Action==CourtAction.RecruitAccept){var offer=Source(w,i.Reference,CourtAction.RecruitOffer);Need(e.At<=offer.At+30*86400L&&HeadAuthority(w,offer.Actor,offer.Input.Owner,offer.Input.Site,e.At,e.Revision)&&!w.Memberships.Any(m=>m.Subject==e.Actor&&m.Id!=Id("membership",i.Id.Value)&&Active(m,e.At,e.Revision)&&(m.From<e.At||m.From==e.At&&m.Revision<e.Revision)),"登用時の期限・署名権・既存の所属が矛盾しています");}

   if(i.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline or CourtAction.ReturnSigning){var offer=Source(w,i.Reference,CourtAction.RecruitOffer);Need(offer.Input.Person==e.Actor&&Received(w,offer.Input.Id,e.Actor,e.At,e.Revision+1),"条件書を受領していない人物が返答しています");Need(es.Count(q=>q.Input.Reference==offer.Input.Id&&q.Input.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline)<=1&&es.Count(q=>q.Input.Reference==offer.Input.Id&&q.Input.Action==CourtAction.ReturnSigning)<=1,"登用の二重返答・二重返金です");if(i.Action==CourtAction.ReturnSigning)Need(es.Any(q=>q.Input.Reference==offer.Input.Id&&q.Input.Action==CourtAction.RecruitDecline)&&!es.Any(q=>q.Input.Reference==offer.Input.Id&&q.Input.Action==CourtAction.RecruitAccept),"返金できない支度金です");}
   if(i.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice){var offer=Source(w,i.Reference,CourtAction.Nominate);Need(offer.Input.Person==e.Actor&&Received(w,offer.Input.Id,e.Actor,e.At,e.Revision+1)&&es.Count(q=>q.Input.Reference==offer.Input.Id&&q.Input.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice)==1,"未受領の任命状か二重返答です");}
   if(i.Action==CourtAction.HoldCouncil){var meeting=Source(w,i.Reference,CourtAction.Convene);Need(i.People!=null&&i.People.Contains(e.Actor)&&i.People.Distinct().Count()==i.People.Length&&e.At>=meeting.Input.When&&e.At<=meeting.Input.When+3600&&es.Count(x=>x.Input.Action==CourtAction.HoldCouncil&&x.Input.Reference==meeting.Input.Id)==1,"評定の参集記録が不正です");foreach(var person in i.People)Need((person==e.Actor||person==meeting.Actor||DocumentRows(w).Any(d=>d.Source.Input.Id==meeting.Input.Id&&d.Recipient==person&&es.Any(reply=>reply.Input.Action==CourtAction.AcceptInvitation&&reply.Actor==person&&reply.Input.Reference==d.Id&&reply.Revision<=e.Revision)))&&TransportWork.At(w,person,meeting.Input.Site,e.At,e.Revision,stableCompletion:true),"実際にはいない人物が評定へ参集しています");}
   if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);Need(Received(w,proposal.Input.Id,e.Actor,e.At,e.Revision+1)&&es.Count(x=>x.Input.Reference==proposal.Input.Id&&x.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase)==1,"未受領または二重審査の案件です");if(i.Action==CourtAction.ApproveCase)Need(proposal.Input.Case==CourtCaseKind.RelieveVillage?w.Communities?.Orders.Any(o=>o.Input.Id==Id("effect",i.Id.Value)&&o.Actor==e.Actor&&o.Input.Amount==proposal.Input.Amount)==true:w.Domains?.Entries.Any(o=>o.Input.Id==Id("effect",i.Id.Value)&&o.Actor==e.Actor)==true,"承認した案件の実際の処分がありません");}
  }
  Need(SigningEscrow(w)>=0,"実在しない支度金を払い出しています");
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageCourt(CourtInput input,long revision){lock(gate){var old=HouseholdCourt.Events(state).SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null){var expected=input.Action==CourtAction.HoldCouncil?input with{People=old.Input.People}:input;return new(ContractJson.CanonicalHash(old.Input)==ContractJson.CanonicalHash(expected)?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"同じ家中の用件は受付済みです");}if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");if(input.Action==CourtAction.HoldCouncil&&input.People!=null)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"参集者は実際の所在と承諾から確定します");try{Change(w=>HouseholdCourt.Execute(w,input,w.PlayerControl));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"条件と本人の確認、実効の範囲を分けて記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
