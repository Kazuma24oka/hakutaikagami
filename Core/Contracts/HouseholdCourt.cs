using HakutaiKagami.Core.Messaging;
using System.Text;
using System.Text.Json.Serialization;
namespace HakutaiKagami.Core.Contracts;
public enum CourtAction { RecruitOffer,RecruitAccept,RecruitDecline,ReturnSigning,ResignService,Nominate,AcceptOffice,DeclineOffice,RevokeOffice,ResignOffice,DeclareScale,Convene,AcceptInvitation,DeclineInvitation,HoldCouncil,SubmitCase,ApproveCase,DenyCase,ReviseCase,DeliverDocument,ConsultReform,DeferReform,OpenLocalMarket,GrantTempleSeat,ChallengeSeat,InsistSeat,SettleSeat,EnactLetterLaw,LetterBreach,EstablishBarrier,AbolishBarrier,AssentReform,CounterReform,RejectReform,IssueTransfer,ReleaseCastleOffice,EnterCastleOffice,PresentTransfer,YieldCastleOffice,GrantLevyShare,ReceiveLevyShare,ExchangeLevyShare,EnactMarriageLaw,EnactSuccessionLaw,ReduceCastle,SettleCastleResidence,ReportResidence,SurrenderLevyShare,PresentServiceRelease,ConsentServiceRelease,RefuseServiceRelease,ReceiveProvinceShare,DeliverProvinceShare,EnactEqualPenalty,ChallengeFight,JoinFight,DeclineFight,CensureBoth,SeekRestorationSupport,SupportRestoration,ReserveRestoration,PledgeRestorationRice,HandOverRestorationRice,OfferRestorationEscort,AcceptRestorationEscort,DeclineRestorationEscort,ReleaseRestorationEscort,RecordRecoveredSeat,PresentRestorationArrears,RecognizeHouseContinuity,RaiseVillageSoldiers,HireMarketSoldiers,SendMusterNotice,AttendMuster,OfferDetachment,AcceptDetachment,DeclineDetachment,EndDetachment,BringOwnCompany,KeepPrivateCompany,PrivateCompanyPayment,AskCourtIntroduction,AcceptCourtIntroduction,DeclineCourtIntroduction,FileRankPetition,IssueCourtRank,RejectCourtRank,AskShugo,RecommendShugo,DeclineShugo,AppointShugo,RejectShugo,AskRankLeave,GrantRankLeave,RefuseRankLeave,AskPeaceMediation,SendPeaceProposal,DeclinePeaceMediation,AcceptPeaceProposal,RejectPeaceProposal,CertifyPeaceAgreement,ReportPeaceRefusal,ResignShogun,OfferShogunSupport,AcceptShogunSupport,DeclineShogunSupport,ExpelShogun,SendExileClaim,AskPedigree,AttestPedigree,ReportMissingPedigree,RecognizePedigree,FilePedigree,RequestRegentKin,AcceptRegentKin,DeclineRegentKin,PetitionChancellor,PetitionRegent,PetitionShogun,GrantHighOffice,RefuseHighOffice,InviteHegemon,DeferInvitation,DeclineImperialInvitation,GrantCompanionHonor,PermitLacquerPalanquin,ProposeCoalition,JoinCoalition,DeferCoalition,RefuseCoalition,ConfirmCoalition,WarnCoalitionTarget,LeaveCoalition,ReportCoalitionSortie,ReportSeparatePeace,RequestIndependentGate,AllowIndependentGate,RefuseIndependentGate,ReceiveIndependentGate,ReceiveIndependentKeep,AnnounceIndependence,SeekDeclaredSupport,JoinDeclaredHouse,RemainWithHouse,StayNeutral,ReportIndependentKeep }
public enum CourtRole { Elder,Steward,Castellan,Mediator,TeaMaster,Commander,WorksMagistrate,TownMagistrate }
public enum CourtScale { Local,Daimyo,Great,Hegemon }
public enum CourtCaseKind { RelieveVillage,BuyWood,BuyWeapons,SetBudget }
public sealed record CourtInput(EntityId Id,CourtAction Action,EntityId Person,EntityId Owner,EntityId Site,CourtRole Role=CourtRole.Steward,long Amount=0,EntityId? Reference=null,long When=0,CourtCaseKind Case=CourtCaseKind.RelieveVillage,CourtScale Scale=CourtScale.Local,EntityId? Carrier=null)
{[JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public ReformKind? Reform{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public string? BarrierEdge{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId? NewEstate{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId? FormerOffice{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId? LevyGrant{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId[]? People{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId? OtherHouse{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public CoalitionTerms? Coalition{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId[]? CoalitionSignatures{get;init;}
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public EntityId? ActivityEvidence{get;init;}
 // 京への奏請で望む位階・官職（CentralTitles の ID）。null は従来の従五位下。
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public string? Title{get;init;}}
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
 public static long Net(ContractWorld w,EntityId wallet,long at,long revision){using var _ccRev=ControlContext.At(revision);long n=0;foreach(var e in Events(w).Where(e=>Before(e,at,revision))){if(e.Input.Action==CourtAction.HireMarketSoldiers&&NewGameWork.Wallet(e.Actor)==wallet)n=checked(n-PrivateRecruitment.Wages(e));if(e.Input.Action==CourtAction.RecruitOffer&&NewGameWork.Wallet(e.Actor)==wallet)n=checked(n-e.Input.Amount);if(e.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);var to=e.Input.Action==CourtAction.RecruitAccept?e.Actor:offer.Actor;if(NewGameWork.Wallet(to)==wallet)n=checked(n+offer.Input.Amount);}}n-=PrivateRecruitment.RenewalWages(w,wallet,at,revision)+CentralRank.Offerings(w,wallet,at,revision);return n;}
 public static long SigningEscrow(ContractWorld w)=>Events(w).Where(e=>e.Input.Action==CourtAction.RecruitOffer&&!Events(w).Any(a=>a.Input.Reference==e.Input.Id&&a.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning)).Sum(e=>e.Input.Amount);
 public static CourtDocument[] DocumentRows(ContractWorld w){var docs=new List<CourtDocument>();foreach(var e in Events(w)){
  var i=e.Input;if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.SubmitCase)docs.Add(new(i.Id,e,e.Actor,i.Person,i.Action==CourtAction.RecruitOffer?"登用の条件書（支度金を同封）":i.Action==CourtAction.Nominate?"常設職の任命状":i.Action==CourtAction.RevokeOffice?"役職の解任通知":"評定への上申書"));
  if(i.Action==CourtAction.SendMusterNotice)docs.Add(new(i.Id,e,e.Actor,i.Person,"陣触れ：集合地・期限・軍役"));
  if(i.Action==CourtAction.Convene)foreach(var person in i.People??[])docs.Add(new(Id("invitation",i.Id.Value+"/"+person.Value),e,e.Actor,person,"評定の招待状"));
  if(i.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline or CourtAction.AcceptOffice or CourtAction.DeclineOffice){var parent=Events(w).Single(e=>e.Input.Id==i.Reference);docs.Add(new(i.Id,e,e.Actor,parent.Actor,"本人の返答書"));}
  if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);docs.Add(new(i.Id,e,e.Actor,proposal.Actor,"評定の回答書"));}
 }return docs.ToArray();}
 internal static PersonalObject[] Documents(ContractWorld w,long revision)=>DocumentRows(w).Where(d=>d.Source.Revision<=revision).Select(d=>new PersonalObject(d.Id,d.Name,"court-paper",1,d.Sender,d.Sender,d.Source.Place,null,false,["文書の到達と本人の確認・引受は別。所領・兵・私財は一括移転しない"])).ToArray();
 internal static bool ValidRecipient(ContractWorld w,EntityId document,EntityId recipient)=>DocumentRows(w).Any(d=>d.Id==document&&d.Recipient==recipient);
 internal static bool Received(ContractWorld w,EntityId document,EntityId recipient,long at=long.MaxValue,long rev=long.MaxValue)=>SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.ReceiveGift&&e.Actor==recipient&&e.At<=at&&e.Revision<rev&&SocialWorld.Ledger(w).Any(g=>g.Input.Id==e.Input.Reference&&g.Input.Asset==document));
 internal static PersonalObject ReturnObject(ContractWorld w,PersonalObject item,long revision){using var _ccRev=ControlContext.At(revision);var returned=Events(w).SingleOrDefault(e=>e.Input.Action==CourtAction.ReturnSigning&&e.Input.Reference==item.Id&&e.Revision<revision);if(returned==null)return item;var original=Source(w,item.Id,CourtAction.RecruitOffer);return item with{Owner=original.Actor,Holder=original.Actor,Site=returned.Place,Consumed=true,History=[..item.History,"支度金と条件書を現地で返還"]};}
 internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,string kind,long at=long.MaxValue,long rev=long.MaxValue){using var _ccRev=ControlContext.At(rev);var rows=original.ToList();foreach(var e in Events(w).Where(e=>e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev))){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);if(kind=="service"&&e.PersonalAlreadyBound)continue;var end=Events(w).FirstOrDefault(x=>x.Input.Action==CourtAction.ResignService&&x.Input.Reference==offer.Input.Id&&Before(x,at,rev));rows.Add(new(Id(kind,e.Input.Id.Value),e.Actor,kind=="membership"?offer.Input.Owner:offer.Actor,kind,NewGameWorld.Basis,e.At,end?.At??long.MaxValue,end?.Revision??e.Revision));}return rows.ToArray();}
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
 {using var _ccRev=ControlContext.At(rev);
     var occupied=Events(w).Count(e=>e.Input.Action==CourtAction.AcceptOffice && Before(e,at,rev) &&
         at<EndOffice(w,e,at,rev) && Source(w,e.Input.Reference,CourtAction.Nominate).Input is {} offer &&
         offer.Owner==owner && offer.Site==site && offer.Role==role);
     return occupied<SeatLimit(Scale(w,owner,at,rev),role);
 }
 public static string RoleName(CourtRole role,CourtScale scale)=>role switch{CourtRole.Elder=>scale>=CourtScale.Great?"宿老":"家老",CourtRole.Steward=>scale>=CourtScale.Daimyo?"奉行":"代官",CourtRole.Castellan=>"城主（常設）",CourtRole.WorksMagistrate=>"普請奉行",CourtRole.TownMagistrate=>"町奉行",CourtRole.Mediator=>"取次",CourtRole.TeaMaster=>"茶頭",_=>"部将（指揮は別契約）"};
 public static string Seat(ContractWorld w,EntityId principal,EntityId person,long at=long.MaxValue,long revision=long.MaxValue){using var _ccRev=ControlContext.At(revision);bool family=w.Adoption?.Entries.Any(e=>e.Input.Action==AdoptionAction.Implement&&(e.At<at||e.At==at&&e.Revision<revision)&&w.Adoption.Entries.Any(q=>q.Terms is {} t&&q.Input.Id==e.Input.Original&&t.Parent==principal&&t.Child==person))==true||w.Marriage?.Entries.Any(e=>e.Input.Action==MarriageAction.Implement&&(e.At<at||e.At==at&&e.Revision<revision)&&w.Marriage.Entries.Any(q=>q.Terms is {} t&&q.Input.Id==e.Input.Original&&(t.First==principal&&t.Second==person||t.Second==principal&&t.First==person)))==true;if(family)return "一門（本人が知る縁組）";var original=w.NewGame!.Start.People.Single(p=>p.Id==person.Value);return !original.Created&&original.Lord==principal.Value?"譜代（開始資料の根拠）":Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==person)?"新参（登用の合意）":"新参・席次の来歴は未採録";}
 public static bool CanConvene(ContractWorld w,EntityId actor,EntityId owner,EntityId site){if(HeadAuthority(w,actor,owner,site,w.ElapsedSeconds,w.CommitSequence+1))return true;var appointment=Appointment(w,actor,owner,site,w.ElapsedSeconds,w.CommitSequence+1);return appointment!=null&&Source(w,appointment.Input.Reference,CourtAction.Nominate).Input.Role==CourtRole.Elder;}
 static long EndOffice(ContractWorld w,CourtEvent accepted,long at,long rev){using var _ccRev=ControlContext.At(rev);var offer=Source(w,accepted.Input.Reference,CourtAction.Nominate);var ends=Events(w).Where(e=>Before(e,at,rev)&&(e.Input.Action==CourtAction.ResignOffice&&e.Input.Reference==offer.Input.Id||e.Input.Action==CourtAction.RevokeOffice&&e.Input.Reference==offer.Input.Id&&Received(w,e.Input.Id,accepted.Actor,at,rev))).Select(e=>e.Input.Action==CourtAction.RevokeOffice?SocialWorld.Ledger(w).Where(r=>r.Input.Action==SocialWorldAction.ReceiveGift&&SocialWorld.Ledger(w).Any(g=>g.Input.Id==r.Input.Reference&&g.Input.Asset==e.Input.Id)).Min(r=>r.At):e.At);var succession=w.Succession?.Entries.Where(e=>e.Input.Action==HouseSuccessionAction.Resolve&&e.Outcome=="confirmed"&&e.Input.Owner==offer.Input.Owner&&e.At>=accepted.At&&(e.At<at||e.At==at&&e.Revision<rev)).Select(e=>e.At)??[];return ends.Concat(succession).DefaultIfEmpty(long.MaxValue).Min();}
 internal static TimedRelation[] Offices(ContractWorld w,TimedRelation[] original,long at=long.MaxValue,long rev=long.MaxValue){using var _ccRev=ControlContext.At(rev);var rows=new List<TimedRelation>([..original,..Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&Before(e,at,rev)).Select(e=>{var offer=Source(w,e.Input.Reference,CourtAction.Nominate);return new TimedRelation(Id("office",offer.Input.Id.Value),e.Actor,offer.Input.Site,"house-"+offer.Input.Role.ToString().ToLowerInvariant(),NewGameWorld.Basis,e.At,EndOffice(w,e,at,rev),e.Revision);})]);
  foreach(var move in Events(w).Where(e=>Before(e,at,rev) && e.Input.Action is CourtAction.ReleaseCastleOffice or CourtAction.EnterCastleOffice or CourtAction.YieldCastleOffice)){
   if(move.Input.Action is CourtAction.ReleaseCastleOffice or CourtAction.YieldCastleOffice){
    int index=rows.FindIndex(r=>r.Id==move.Input.FormerOffice);
    if(index>=0)rows[index]=rows[index] with{Until=move.At,Revision=move.Revision};
   }else rows.Add(new(Id("transferred-office",move.Input.Id.Value),move.Actor,move.Place,"castellan",NewGameWorld.Basis,move.At,long.MaxValue,move.Revision));
  }
  return rows.ToArray();
 }
 static CourtEvent? Appointment(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev)=>Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&e.Actor==actor&&Before(e,at,rev)&&e.At<=at&&at<EndOffice(w,e,at,rev)).LastOrDefault(e=>{var offer=Source(w,e.Input.Reference,CourtAction.Nominate);return offer.Input.Owner==owner&&offer.Input.Site==site;});
 internal static CourtRole? Specialist(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
 {using var _ccRev=ControlContext.At(rev);
     if(RegionalDomains.Owner(w,site) is not {} owner || Appointment(w,actor,owner,site,at,rev) is not {} appointment)
         return null;
     var offer=Source(w,appointment.Input.Reference,CourtAction.Nominate);
     if(offer.Input.Role is not (CourtRole.WorksMagistrate or CourtRole.TownMagistrate))return null;
     return Member(w,actor,owner,at,rev) && HeadAuthority(w,offer.Actor,owner,site,at,rev) &&
         !HostageCustody.HeldAt(w,actor,at,rev) && !CastleAdministration.Blocked(w,site,at,rev)?offer.Input.Role:null;
 }
 internal static FactionStanceAuthority? Authority(ContractWorld w,EntityId actor,EntityId owner,EntityId site,long at,long rev,bool treasury=false){using var _ccRev=ControlContext.At(rev);if(HostageCustody.HeldAt(w,actor,at,rev))return null;if(CastleAdministration.Blocked(w,site,at,rev))return null;var accepted=Appointment(w,actor,owner,site,at,rev);if(accepted==null||!Member(w,actor,owner,at,rev))return null;var offer=Source(w,accepted.Input.Reference,CourtAction.Nominate);if(offer.Input.Role is not(CourtRole.Steward or CourtRole.Castellan))return null;var source=FactionStances.ResolveAuthority(w,offer.Actor,owner,at,Math.Min(rev,w.CommitSequence));if(source==null||!HeadAuthority(w,offer.Actor,owner,site,at,rev))return null;var member=w.Memberships.Last(m=>m.Subject==actor&&m.Target==owner&&Active(m,at,rev));return source with{Issuer=actor,Office=Id("office",offer.Input.Id.Value),Membership=member.Id};}
 internal static long Budget(ContractWorld w,EntityId actor,EntityId site,EconomyAction action,long at,long rev){using var _ccRev=ControlContext.At(rev);var epoch=DateOnly.Parse(w.EpochDate);var month=epoch.AddDays((int)(at/86400));long used=w.Communities?.Orders.Where(e=>e.Actor==actor&&e.Input.Site==site&&e.Input.Action==action&&(e.At<at||e.At==at&&e.Revision<rev)&&epoch.AddDays((int)(e.At/86400)).Year==month.Year&&epoch.AddDays((int)(e.At/86400)).Month==month.Month).Sum(e=>e.Input.Amount)??0;return Math.Max(0,(action==EconomyAction.CollectLevy?1000:200)-used);}
 internal static void Sync(ContractWorld w){if(w.Court==null)return;var original=NewGameWorld.InitialRelations(w.NewGame!);w.Memberships=NewGameService.Relations(w,original.Members,"membership");w.ServiceBonds=NewGameService.Relations(w,original.Bonds,"service");w.Offices=Offices(w,HouseSuccession.Offices(w,original.Offices));}
 static void Add(ContractWorld w,CourtInput i,EntityId actor,EntityId place,bool alreadyBound=false){w.Court??=new(1,Policy,[]);w.Court=w.Court with{Events=[..w.Court.Events,new(i,actor,place,w.ElapsedSeconds,w.CommitSequence,alreadyBound)]};}
 static CourtDocument Document(ContractWorld w,EntityId id)=>DocumentRows(w).Single(d=>d.Id==id);
 static void Deliver(ContractWorld w,CourtDocument doc,EntityId actor,EntityId? carrier){Need(SocialAssets.Inventory(w,actor).Any(o=>o.Id==doc.Id),"自分が持つ実物を引き渡してください");SocialWorld.Execute(w,new(Id("delivery",doc.Id.Value),SocialWorldAction.DispatchGift,doc.Recipient,carrier){Asset=doc.Id},actor);}
 static void CheckSeatDispute(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
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
 internal static void RecordForeignLetter(ContractWorld w,MessageEnvelope letter)
 {
  EntityId actor=new(letter.SenderId),other=new(letter.RecipientId);
  var site=SocialWorld.Site(w,actor);
  var owner=DefectionPacts.HouseAt(w,actor,w.ElapsedSeconds,w.CommitSequence);
  var foreign=DefectionPacts.HouseAt(w,other,w.ElapsedSeconds,w.CommitSequence);
  if(site==null || owner==null || foreign==null || foreign==owner ||
      !HouseReforms.LetterLawAt(w,owner.Value,site.Value,w.ElapsedSeconds,w.CommitSequence))return;
  Add(w,new(Id("foreign-letter-breach",letter.Id),CourtAction.LetterBreach,other,owner.Value,site.Value,
      Reference:new(letter.Id)),actor,site.Value);
 }
 static void CheckBarrier(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  Need(i.BarrierEdge is {} edge && HouseReforms.LocalRoads(w,i.Site).Any(r=>r.Id==edge) && i.Site==place &&
      HeadAuthority(w,actor,i.Owner,i.Site,at,revision),"現地で支配する陸路を指定してください");
  var existing=HouseReforms.BarrierAt(w,i.BarrierEdge!,at,revision);
  if(i.Action==CourtAction.EstablishBarrier)Need(existing==null && actor==i.Person,"同じ道に関所を重ねて設けることはできません");
  else Need(existing!=null && existing.Input.Owner==i.Owner && existing.Input.Site==i.Site,
      "この拠点が所管する関所を指定してください");
 }
 static bool IsReformReply(CourtAction action)=>action is CourtAction.AssentReform or CourtAction.CounterReform or CourtAction.RejectReform;
 static CourtEvent CheckReformReply(ContractWorld w,CourtInput input,EntityId actor,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  var proposal=Source(w,input.Reference,CourtAction.ConsultReform);
  var readAt=MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(proposal.Input,HouseReforms.Proposal),actor.Value);
  Need(proposal.Revision<revision && proposal.Input.Person==actor && input.Person==proposal.Actor &&
      input.Owner==proposal.Input.Owner && input.Site==proposal.Input.Site && readAt.HasValue && readAt.Value<=at,
      "本人宛ての改革案を開き、同じ原本の差出人へ返答してください");
  Need(!Events(w).Any(e=>e.Revision<revision && IsReformReply(e.Input.Action) && e.Input.Reference==input.Reference),
      "この原本にはすでに返答しました");
  return proposal;
 }
 static void CheckCastleTransfer(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long rev)
 {using var _ccRev=ControlContext.At(rev);
  var order=Source(w,input.Reference,CourtAction.IssueTransfer);
  var proposal=Source(w,order.Input.Reference,CourtAction.ConsultReform);
  var terms=proposal.Input;
  var releases=Events(w).Where(e=>e.Revision<rev && e.Input.Reference==order.Input.Id &&
      e.Input.Action==CourtAction.ReleaseCastleOffice).ToArray();
  var successor=terms.Person;
  var meeting=input.Action is CourtAction.PresentTransfer or CourtAction.YieldCastleOffice;
  Need(terms.Reform is not (ReformKind.CastleResidence or ReformKind.AllocateProvince) && terms.LevyGrant==null && order.Revision<rev && (input.Action==CourtAction.YieldCastleOffice?input.Person==successor:actor==successor) && (meeting || input.Person==order.Actor) && input.Owner==terms.Owner &&
      input.Site==place && Member(w,actor,input.Owner,at,rev) &&
      MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(order.Input,HouseReforms.TransferOrder),successor.Value) is {} opened &&
      opened<=at && TransportWork.At(w,actor,place,at,rev,true),"本人が命令書を開き、担当する城で手続きしてください");
  var original=NewGameWorld.InitialRelations(w.NewGame!);
  var offices=Offices(w,HouseSuccession.Offices(w,original.Offices),at,rev);
  bool InOffice(TimedRelation r)=>r.From<=at && at<r.Until && r.Kind is "castellan" or "house-castellan";
  Need(HeadAuthority(w,order.Actor,input.Owner,place,at,rev),"発令者の管轄が変更されています");
  if(meeting){
   var incumbent=input.Action==CourtAction.PresentTransfer?input.Person:actor;
   Need(successor!=incumbent && place==terms.NewEstate && releases.Length==1 && releases[0].At<at &&
       TransportWork.At(w,successor,place,at,rev,true) && TransportWork.At(w,incumbent,place,at,rev,true) &&
       Member(w,incumbent,input.Owner,at,rev) && offices.Any(r=>r.Id==input.FormerOffice &&
       r.Subject==incumbent && r.Target==place && r.From<at && InOffice(r)),"前任者と後任者が新城で同じ城主職を確認してください");
   var shown=Events(w).Any(e=>e.Revision<rev && e.Input.Reference==order.Input.Id && e.Input.FormerOffice==input.FormerOffice &&
       e.Input.Action==CourtAction.PresentTransfer && e.Input.Person==incumbent);
   Need(shown==(input.Action==CourtAction.YieldCastleOffice),"命令書の提示後、前任者本人が職を引き渡してください");
  }else if(input.Action==CourtAction.ReleaseCastleOffice){
   Need(place==terms.Site && releases.Length==0 && offices.Any(r=>r.Subject==actor && r.Target==place &&
       r.Id==input.FormerOffice && r.From<at && InOffice(r)),"旧城で現在の城主職の原本を指定してください");
  }else Need(place==terms.NewEstate && releases.Length==1 && releases[0].At<at && input.FormerOffice==null &&
       !offices.Any(r=>r.Target==place && InOffice(r)) &&
       (CastleAdministration.Grant(w,place,at,rev) is not {} administration || administration.Agent==actor) &&
       !Events(w).Any(e=>e.Revision<rev && e.Input.Reference==order.Input.Id && e.Input.Action==CourtAction.EnterCastleOffice),
       "旧職の引渡し後、空席の新城へ実際に到着してください");
 }
 static CourtEvent CheckTransferOrder(ContractWorld w,CourtInput order,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  var proposal=Source(w,order.Reference,CourtAction.ConsultReform);
  var terms=proposal.Input;
  Need((terms.Reform==ReformKind.Confiscate && terms.LevyGrant!=null || terms.NewEstate!=null && terms.Reform is ReformKind.ChangeFief or ReformKind.Relocate or ReformKind.CastleResidence or ReformKind.AllocateProvince) &&
      proposal.Actor==actor && place==terms.Site && order.Site==terms.Site && order.Person==terms.Person &&
      order.Owner==terms.Owner && proposal.Revision<revision,"本人が相談した同じ旧領・新領の原本を指定してください");
  CheckEstateTerms(w,terms,actor,at,revision);
  var answer=HouseReforms.Letter(terms,HouseReforms.Answer);
  Need(MessageFlow.LearnedAt(w.Messaging,answer,actor.Value) is {} opened && opened<=at &&
      w.Messaging.Messages.Any(m=>m.Id==answer && m.SenderId==terms.Person.Value &&
      m.Body==HouseReforms.Name(terms.Reform!.Value)+"への返答："+HouseReforms.ReplyText(CourtAction.AssentReform)),
      "この交換条件に対する本人の承諾を開封してください");
  Need(!Events(w).Any(e=>e.Revision<revision && e.Input.Reference==terms.Id &&
       (e.Input.Action is CourtAction.IssueTransfer or CourtAction.DeferReform)),"この原本は決裁済みです");
  return proposal;
 }
 static void CheckResidenceReport(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  var move=Source(w,i.Reference,CourtAction.SettleCastleResidence);
  Need(move.Revision<rev && move.Actor==actor && move.Place==place && i.Site==place && i.Owner==move.Input.Owner &&
      i.Person==move.Input.Person && HouseReforms.ResidenceAt(w,actor,at,rev)==place && TransportWork.At(w,actor,place,at,rev,true) &&
      !Events(w).Any(e=>Before(e,at,rev) && e.Input.Reference==move.Input.Id && e.Input.Action==CourtAction.ReportResidence),
      "転居した本人が新しい居所で当主宛の転居届を作成してください");
 }
 static void CheckResidence(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  var order=Source(w,i.Reference,CourtAction.IssueTransfer);
  var terms=Source(w,order.Input.Reference,CourtAction.ConsultReform).Input;
  Need(terms.Reform==ReformKind.CastleResidence && order.Revision<rev && terms.NewEstate==place && i.Site==place &&
      i.Owner==terms.Owner && i.Person==order.Actor && actor==terms.Person && Member(w,actor,i.Owner,at,rev) &&
      HeadAuthority(w,order.Actor,i.Owner,place,at,rev) && TransportWork.At(w,actor,place,at,rev,true) &&
      HouseReforms.ResidenceAt(w,actor,at,rev)==terms.Site &&
      MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(order.Input,HouseReforms.TransferOrder),actor.Value) is {} opened && opened<=at &&
      !Events(w).Any(e=>Before(e,at,rev) && e.Input.Action==CourtAction.SettleCastleResidence && e.Input.Reference==order.Input.Id),
      "本人が命令書を確認し、新しい城下に到着して居所を引き取ってください");
 }
 static void CheckServiceHandover(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  bool present=i.Action==CourtAction.PresentServiceRelease;
  var shown=present?null:Source(w,i.Reference,CourtAction.PresentServiceRelease);
  var proposal=Source(w,present?i.Reference:shown!.Input.Reference,CourtAction.ConsultReform);
  var subject=proposal.Input.Person;
  var oldLord=present?i.Person:actor;
  Need(proposal.Input.Reform==ReformKind.DirectService && proposal.Revision<rev && i.Owner==proposal.Input.Owner &&
      (present?actor==subject:shown!.Revision<rev && shown.Actor==subject && shown.Input.Person==actor && i.Person==subject && shown.Place==place) &&
      oldLord!=proposal.Actor && place==i.Site && Head(w,i.Owner,at,rev)==proposal.Actor && Member(w,subject,i.Owner,at,rev) &&
      Member(w,oldLord,i.Owner,at,rev) && TransportWork.At(w,subject,place,at,rev,true) && TransportWork.At(w,oldLord,place,at,rev,true) &&
      w.ServiceBonds.Any(b=>b.Id==i.FormerOffice && b.Subject==subject && b.Target==oldLord && Active(b,at,rev)) &&
      (present || shown!.Input.FormerOffice==i.FormerOffice) &&
      Events(w).Any(e=>Before(e,at,rev) && e.Actor==subject && e.Input.Action==CourtAction.AssentReform && e.Input.Reference==proposal.Input.Id) &&
      !Events(w).Any(e=>Before(e,at,rev) && e.Input.Reference==i.Reference && (present?e.Input.Action==i.Action:
          e.Input.Action is CourtAction.ConsentServiceRelease or CourtAction.RefuseServiceRelease)),
      "承諾した本人と現在の直属主が現地で同じ奉公原本を確認し、旧主が本人に返答してください");
 }
 static void CheckProvinceReceipt(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  var order=Source(w,i.Reference,CourtAction.IssueTransfer);
  var terms=Source(w,order.Input.Reference,CourtAction.ConsultReform).Input;
  bool handing=i.Action==CourtAction.DeliverProvinceShare;
  Need(terms.Reform==ReformKind.AllocateProvince && order.Revision<rev &&
       (handing?actor==order.Actor && i.Person==terms.Person:actor==terms.Person && i.Person==order.Actor) &&
       TransportWork.At(w,order.Actor,place,at,rev,true) &&
       i.Owner==terms.Owner && i.Site==place && (place==terms.Site || place==terms.NewEstate) && i.Amount==terms.Amount &&
       MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(order.Input,HouseReforms.TransferOrder),terms.Person.Value) is {} opened && opened<=at &&
       !Events(w).Any(e=>Before(e,at,rev) && (e.Input.Action is CourtAction.ReceiveProvinceShare or CourtAction.DeliverProvinceShare) && e.Input.Reference==order.Input.Id && e.Place==place),
       "開封した国割り命令に記された土地で、未受領の給付を引き取ってください");
  CheckLevyShare(w,i with{Person=terms.Person},order.Actor,place,at,rev);
 }
 static void CheckFight(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  Need(i.Site==place && actor!=i.Person && TransportWork.At(w,actor,place,at,rev,true) &&
      TransportWork.At(w,i.Person,place,at,rev,true) && Member(w,i.Person,i.Owner,at,rev),"当事者が現地にそろっていません");
  if(i.Action==CourtAction.CensureBoth){
   var fight=Source(w,i.Reference,CourtAction.JoinFight);
   Need(Before(fight,at,rev) && fight.Input.Owner==i.Owner && fight.Place==place && fight.Actor==i.Person &&
       actor!=fight.Input.Person && HeadAuthority(w,actor,i.Owner,place,at,rev) &&
       HouseReforms.WitnessedFight(w,actor,fight) && TransportWork.At(w,fight.Input.Person,place,at,rev,true) &&
       HouseReforms.EqualPenaltyAt(w,i.Owner,place,fight.At,fight.Revision) &&
       !Events(w).Any(e=>Before(e,at,rev) && e.Input.Action==i.Action && e.Input.Reference==i.Reference),"制定後の私闘を目撃した当主が双方に申し渡してください");
   return;
  }
  Need(Member(w,actor,i.Owner,at,rev),"同じ家の当事者を指定してください");
  if(i.Action==CourtAction.ChallengeFight){Need(i.Reference==null,"新たな私闘の申込みに原本は指定できません");return;}
  var challenge=Source(w,i.Reference,CourtAction.ChallengeFight);
  Need(Before(challenge,at,rev) && challenge.Input.Person==actor && challenge.Actor==i.Person && challenge.Input.Owner==i.Owner &&
      challenge.Place==place && !Events(w).Any(e=>Before(e,at,rev) && e.Input.Reference==i.Reference &&
      e.Input.Action is CourtAction.JoinFight or CourtAction.DeclineFight),"現地で自分に向けられた未回答の私闘に返答してください");
 }
 static void CheckSurrender(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){using var _ccRev=ControlContext.At(rev);
  var order=Source(w,i.Reference,CourtAction.IssueTransfer);
  var terms=Source(w,order.Input.Reference,CourtAction.ConsultReform).Input;
  Need(terms.Reform==ReformKind.Confiscate && terms.LevyGrant==i.LevyGrant && i.Amount==terms.Amount &&
       order.Revision<rev && actor==terms.Person && i.Person==order.Actor && i.Site==place && place==terms.Site &&
       i.Owner==terms.Owner && TransportWork.At(w,actor,place,at,rev,true) && TransportWork.At(w,order.Actor,place,at,rev,true) &&
       MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(order.Input,HouseReforms.TransferOrder),actor.Value) is {} opened && opened<=at,
       "返上命令を開封した本人と発令者が給付原本の土地で引き渡してください");
  CheckEstateTerms(w,terms,order.Actor,at,rev);
 }
 static void CheckLevyExchange(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
 {using var _ccRev=ControlContext.At(rev);
  var order=Source(w,i.Reference,CourtAction.IssueTransfer);
  var terms=Source(w,order.Input.Reference,CourtAction.ConsultReform).Input;
  Need(terms.LevyGrant!=null && i.LevyGrant==terms.LevyGrant && i.Amount==terms.Amount &&
      actor==terms.Person && i.Person==order.Actor && i.Owner==terms.Owner && i.Site==place && place==terms.NewEstate &&
      order.Revision<rev && Member(w,actor,i.Owner,at,rev) && TransportWork.At(w,actor,place,at,rev,true) &&
      MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(order.Input,HouseReforms.TransferOrder),actor.Value) is {} read && read<=at,
      "同じ原本の命令書を受領し、新領へ実際に赴いてください");
  CheckEstateTerms(w,terms,order.Actor,at,rev);
 }
 public static long AvailableLevyRice(ContractWorld w,CourtEvent grant,EntityId recipient,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  var place=grant.Input.Site;
  if(grant.Revision>=revision || grant.Input.Person!=recipient ||
     !TransportWork.At(w,recipient,place,at,revision,true) || !TransportWork.At(w,grant.Actor,place,at,revision,true) ||
     !HeadAuthority(w,grant.Actor,grant.Input.Owner,place,at,revision))return 0;
  var due=HouseReforms.AccruedLevyRice(w,grant,at,revision)-HouseReforms.ReceivedLevyRice(w,grant.Input.Id,at,revision);
  var free=CommunityEconomy.AccountAt(w,place,at,revision).HouseRice-HouseSalary.Reserved(w,place,at,revision);
  return Math.Max(0,Math.Min(due,free));
 }
 static void CheckLevyReceipt(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  var grant=HouseReforms.LevyGrants(w).Single(g=>g.Input.Id==i.Reference);
  Need(grant.Revision<revision && grant.Input.Person==actor && grant.Input.Site==place && i.Site==place &&
      i.Person==grant.Actor && i.Owner==grant.Input.Owner && i.Amount>0 &&
      TransportWork.At(w,actor,place,at,revision,true) && TransportWork.At(w,grant.Actor,place,at,revision,true) &&
      HeadAuthority(w,grant.Actor,i.Owner,place,at,revision),"給付を決めた当主と受給者が原本の土地で受け渡してください");
  Need(i.Amount<=AvailableLevyRice(w,grant,actor,at,revision),
      "発生済みの未受領額と蔵の自由米を超えて受領できません");
 }
 static void CheckLevyShare(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  Need(input.Amount is >=1 and <=100 && input.Site==place && actor!=input.Person &&
      HeadAuthority(w,actor,input.Owner,place,at,revision) && Member(w,input.Person,input.Owner,at,revision) &&
      TransportWork.At(w,input.Person,place,at,revision,true) && CommunityEconomy.SitesFor(w).Contains(place),
      "当主と家臣が現地で、実際の徴収米のうち受け取る割合（1〜100%）を定めてください");
  var grants=HouseReforms.LevyGrants(w).Where(e=>Before(e,at,revision) && HouseReforms.LevyEnd(w,e.Input.Id)>=revision &&
      e.Input.Owner==input.Owner && e.Input.Site==place).ToArray();
  Need(!grants.Any(e=>e.Input.Person==input.Person) && grants.Sum(e=>e.Input.Amount)+input.Amount<=100,
      "この土地では同じ人への重複給付、または徴収額を超える割当になります");
 }
 static void CheckEstateTerms(ContractWorld w,CourtInput i,EntityId actor,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  if(i.Reform==ReformKind.Confiscate){
   Need(i.NewEstate==null && HeadAuthority(w,actor,i.Owner,i.Site,at,revision) &&
       HouseReforms.LevyGrants(w).Any(g=>g.Input.Id==i.LevyGrant && g.Input.Person==i.Person && g.Place==i.Site &&
         g.Input.Owner==i.Owner && g.Input.Amount==i.Amount && Before(g,at,revision) && HouseReforms.LevyEnd(w,g.Input.Id)>=revision),
       "改易で返上する本人の有効な徴収米給付原本を指定してください");
   return;
  }
  Need(i.NewEstate is {} destination && destination!=i.Site &&
      HeadAuthority(w,actor,i.Owner,i.Site,at,revision) && HeadAuthority(w,actor,i.Owner,destination,at,revision),
      "旧領とは異なる、自家が処分できる新領を指定してください");
  if(i.Reform==ReformKind.AllocateProvince){
   Need(i.LevyGrant==null && i.Amount is >=1 and <=100 && CommunityEconomy.SitesFor(w).Contains(i.Site) &&
      CommunityEconomy.SitesFor(w).Contains(i.NewEstate!.Value),"徴収米を割り当てる二拠点と割合を指定してください");
   return;
  }
  if(i.Reform==ReformKind.CastleResidence){
   Need(i.LevyGrant==null && i.Amount==0 && HouseReforms.ResidenceAt(w,i.Person,at,revision)==i.Site,
       "現在の居所を旧地に指定してください");
   return;
  }
  if(i.LevyGrant is {} id){
   Need(i.Reform==ReformKind.ChangeFief && HouseReforms.LevyGrants(w).Any(g=>g.Input.Id==id && Before(g,at,revision) &&
       g.Input.Owner==i.Owner && g.Input.Site==i.Site && g.Input.Person==i.Person && g.Input.Amount==i.Amount &&
       HouseReforms.LevyEnd(w,id)>=revision),"旧領で有効な本人の収益原本と同じ割合を指定してください");
   var active=HouseReforms.LevyGrants(w).Where(g=>g.Input.Owner==i.Owner && g.Input.Site==i.NewEstate &&
       Before(g,at,revision) && HouseReforms.LevyEnd(w,g.Input.Id)>=revision).ToArray();
   Need(!active.Any(g=>g.Input.Person==i.Person) && active.Sum(g=>g.Input.Amount)+i.Amount<=100,
       "新領の既存給付を保つだけの未割当分がありません");
   return;
  }
  Need(w.LandRights.Any(r=>r.Subject==i.Person && r.Target==i.Site && r.Kind=="control" && Active(r,at,revision)) ||
      w.Offices.Any(o=>o.Subject==i.Person && o.Target==i.Site && (o.Kind is "castellan" or "house-castellan") && Active(o,at,revision)),
      "旧領の支配権または城主職を持つ本人を選んでください");
 }
 static void CheckTempleSeat(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  Need(place==i.Site && HeadAuthority(w,actor,i.Owner,i.Site,at,revision) &&
      i.Person==SocialWorld.Temple(w,i.Site).Representative && MaterialMarkets.Enabled(w) &&
      MaterialMarkets.Sites(w).Contains(i.Site),"市場を支配する当主と現地の寺社代表を確認してください");
  Need(SocialWorld.Ledger(w).Any(e=>e.Actor==actor && e.Input.Person==i.Person && e.Site==i.Site &&
      (e.Input.Action is SocialWorldAction.Donate or SocialWorldAction.Protect or SocialWorldAction.IssueBan) && e.Revision<revision && e.At<=at),"先に現地で寺社代表と寄進・保護・禁制のいずれかを取り交わしてください");
  Need(HouseReforms.TempleMarketSeat(w,i.Site,at,revision)==null &&
      !HouseReforms.OpenMarketAt(w,i.Site,at,revision),"この市場では新しく座を安堵できません");
 }
 static void CheckReformDecision(ContractWorld w,CourtInput input,EntityId actor,EntityId place,long at,long revision)
 {using var _ccRev=ControlContext.At(revision);
  var proposal=Source(w,input.Reference,CourtAction.ConsultReform);
  Need(proposal.Revision<revision && proposal.Actor==actor && proposal.Input.Owner==input.Owner &&
      proposal.Input.Site==input.Site && proposal.Input.Person==input.Person && place==input.Site &&
      HeadAuthority(w,actor,input.Owner,input.Site,at,revision),"相談原本と現地の当主権限を確認してください");
  if(input.Action==CourtAction.OpenLocalMarket){
   Need(proposal.Input.Reform==ReformKind.OpenMarket && MaterialMarkets.Sites(w).Contains(input.Site),
       "実在する現地市場の楽市案を選んでください");
   Need(!HouseReforms.OpenMarketAt(w,input.Site,at,revision),"この市場ではすでに楽市を実施しています");
  }
  if(input.Action==CourtAction.AbolishBarrier){
   Need(proposal.Input.Reform==ReformKind.RemoveBarrier && input.BarrierEdge==proposal.Input.BarrierEdge &&
       HouseReforms.BarrierAt(w,input.BarrierEdge!,at,revision)?.Revision<proposal.Revision,"相談原本に記した同じ関所を指定してください");
   CheckBarrier(w,input,actor,place,at,revision);
  }
  if(input.Action==CourtAction.ReduceCastle)
   Need(proposal.Input.Reform==ReformKind.ReduceCastles && CastleWorks.Enabled(w) && MaterialMarkets.Sites(w).Contains(place) &&
       HouseReforms.CastleReduction(w,place,at,revision)==null,"未実施の城で、現地の城割り相談を指定してください");
  if(input.Action==CourtAction.EnactEqualPenalty)
   Need(proposal.Input.Reform==ReformKind.EqualPenalty && !HouseReforms.EqualPenaltyAt(w,input.Owner,input.Site,at,revision),"現地の喧嘩両成敗の相談原本を指定してください");
  if(input.Action==CourtAction.EnactSuccessionLaw)
   Need(proposal.Input.Reform==ReformKind.SuccessionOrder && !HouseReforms.SuccessionLawAt(w,input.Owner,input.Site,at,revision),
       "現地で未制定の後継順位を定める相談原本を指定してください");
  if(input.Action==CourtAction.EnactMarriageLaw)
   Need(proposal.Input.Reform==ReformKind.MarriagePermission &&
       !HouseReforms.MarriageLawAt(w,input.Owner,input.Site,at,revision),"この管轄の婚姻許可制の相談原本を指定してください");
  if(input.Action==CourtAction.EnactLetterLaw)
   Need(proposal.Input.Reform==ReformKind.ForeignLetters &&
       !HouseReforms.LetterLawAt(w,input.Owner,input.Site,at,revision),"この管轄の他国私信禁止の新規条文を選んでください");
  var readAt=MessageFlow.LearnedAt(w.Messaging,HouseReforms.Letter(proposal.Input,HouseReforms.Answer),actor.Value);
  Need(readAt.HasValue && readAt.Value<=at,"相手の返書を読んでから判断してください");
  Need(!Events(w).Any(e=>e.Revision<revision && (e.Input.Action is CourtAction.IssueTransfer or CourtAction.DeferReform or CourtAction.OpenLocalMarket or CourtAction.EnactLetterLaw or CourtAction.EnactMarriageLaw or CourtAction.EnactSuccessionLaw or CourtAction.EnactEqualPenalty or CourtAction.ReduceCastle or CourtAction.AbolishBarrier) &&
      e.Input.Reference==proposal.Input.Id),"この相談案は決裁済みです");
 }
 public static void Execute(ContractWorld w,CourtInput i,EntityId actor)
 {
  Need(NewGameWork.Enabled(w)&&w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&Enum.IsDefined(i.Action)&&Enum.IsDefined(i.Role)&&Enum.IsDefined(i.Case)&&Enum.IsDefined(i.Scale),"家中の用件が不正です");var place=IndependenceGate.Handles(i.Action)?IndependenceGate.MeetingSite(w,actor):SocialWorld.Site(w,actor);Need(place!=null,"現地へ到着してください");bool activeOwner=HouseSuccession.Factions(w).Any(f=>f.Id==i.Owner.Value);Need((activeOwner||i.Action==CourtAction.ResignShogun&&i.Owner==SupremeOffice.Bakufu||HouseRestoration.NamesFormerHouse(w,i))&&w.Definitions.Sites.Any(s=>s.Id==i.Site),"家と拠点を指定してください");
  Need(i.Action is not (CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase or CourtAction.SubmitCase) || PersonalHealth.Can(w,actor,HealthLimit.Judgment,w.ElapsedSeconds),PersonalHealth.Reason(HealthLimit.Judgment));
  Need(i.Action==CourtAction.ProposeCoalition || i.Coalition==null,"包囲網の条件は呼びかけの原本に記します");
  Need(CoalitionPacts.IsActivity(i.Action) || i.ActivityEvidence==null,"行動報告にだけ出兵・和睦の原本を指定できます");
  Need(i.Action==CourtAction.ConfirmCoalition || i.CoalitionSignatures==null,"加盟返書の一覧は盟約の成立原本に記します");
  if(i.CoalitionSignatures is {} signatures)i=i with{CoalitionSignatures=[..signatures]};
  if(i.Coalition is {Invitees:not null} coalitionTerms)i=i with{Coalition=coalitionTerms with{Invitees=[..coalitionTerms.Invitees]}};
  Need(i.Action==CourtAction.ConsultReform || i.Reform==null,"改革の指定は家中改革の相談に限ります");
  Need(i.Action!=CourtAction.LetterBreach,"違反の事実は実際の発送から記録します");
  bool leader=activeOwner&&HeadAuthority(w,actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1),bound=false;
  if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.DeclareScale)Need(leader&&place==i.Site,"その拠点を支配する当主の現地判断が必要です");
  if(i.Action==CourtAction.RecruitOffer){Need(i.Person!=actor&&w.Characters.Any(c=>c.Id==i.Person&&c.Life==LifeState.Adult)&&i.Amount is >=20 and <=500&&i.Amount<=NewGameWork.Balance(w,actor),"成人への条件と実在の私財の支度金20〜500文を指定してください");Need(!Events(w).Any(e=>e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.Owner==i.Owner&&!Events(w).Any(r=>r.Input.Reference==e.Input.Id&&r.Input.Action is CourtAction.RecruitAccept or CourtAction.ReturnSigning)),"先の条件書と支度金を先に精算してください");}
  else if(i.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline or CourtAction.ReturnSigning){var offer=Source(w,i.Reference,CourtAction.RecruitOffer);Need(offer.Input.Person==actor&&i.Owner==offer.Input.Owner&&i.Site==offer.Input.Site,"本人宛ての同じ条件書を選んでください");Need(SocialAssets.Inventory(w,actor).Any(o=>o.Id==offer.Input.Id),"実物の条件書と支度金がまだ届いていません");if(i.Action==CourtAction.ReturnSigning)Need(Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.RecruitDecline)&&Local(w,actor,offer.Actor)&&!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.ReturnSigning),"断った条件書と支度金を本人へ持ち帰ってください");else {Need(!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.RecruitAccept or CourtAction.RecruitDecline),"本人は返答済みです");if(i.Action==CourtAction.RecruitAccept){Need(w.ElapsedSeconds<=offer.At+30*86400L&&HeadAuthority(w,offer.Actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1)&&FreeToJoin(w,actor,offer.Actor),"期限・署名者の権限・本人の所属と返納を確認してください");bound=w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==offer.Actor&&Active(b,w.ElapsedSeconds,w.CommitSequence+1));}}}
  else if(i.Action==CourtAction.ResignService){var accepted=Source(w,i.Reference,CourtAction.RecruitOffer);Need(Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==actor&&e.Input.Reference==accepted.Input.Id)&&Local(w,actor,accepted.Actor),"自分の登用原本を示し、元の主君と現地で確認してください");Need(Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Input.Reference==accepted.Input.Id&&e.At<w.ElapsedSeconds)&&!Events(w).Any(e=>e.Input.Reference==accepted.Input.Id&&e.Input.Action==i.Action)&&!w.Tasks.Any(t=>t.Executor==actor&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Failed or ExecutionPhase.Stopped))&&!w.Units.Any(u=>u.Commander==actor)&&!w.ResourceLots.Any(l=>l.Holder==actor&&l.Owner!=actor&&l.Quantity>0)&&!Offices(w,[]).Any(o=>o.Subject==actor&&Active(o,w.ElapsedSeconds,w.CommitSequence+1)),"実務と常設職を返してから所属を終えてください");}
  else if(i.Action==CourtAction.Nominate){Need(i.Person!=actor&&Member(w,i.Person,i.Owner,w.ElapsedSeconds,w.CommitSequence+1),"同じ家の人物へ条件を示してください");Need(Vacancy(w,i.Owner,i.Site,i.Role,w.ElapsedSeconds,w.CommitSequence+1),"現在の家の規模ではこの役割に空席がありません");if(i.Role==CourtRole.Elder)Need(Seat(w,actor,i.Person).StartsWith("一門")||Seat(w,actor,i.Person).StartsWith("譜代")||MeritRewards.Records(w,actor).Count(r=>r.Subject==i.Person)>=3,"新参の重臣登用には、自分が認めた複数の働きが必要です");}
  else if(i.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice){var offer=Source(w,i.Reference,CourtAction.Nominate);Need(offer.Input.Person==actor&&i.Owner==offer.Input.Owner&&i.Site==offer.Input.Site&&SocialAssets.Inventory(w,actor).Any(o=>o.Id==offer.Input.Id),"本人の任命状を実際に確認してください");Need(!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action is CourtAction.AcceptOffice or CourtAction.DeclineOffice),"この任命には返答済みです");if(i.Action==CourtAction.AcceptOffice)Need(Vacancy(w,i.Owner,i.Site,offer.Input.Role,w.ElapsedSeconds,w.CommitSequence+1)&&HeadAuthority(w,offer.Actor,i.Owner,i.Site,w.ElapsedSeconds,w.CommitSequence+1)&&Member(w,actor,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&!Offices(w,[]).Any(o=>o.Subject==actor&&o.Target==i.Site&&Active(o,w.ElapsedSeconds,w.CommitSequence+1)),"現在の署名権・所属・兼務を確認してください");}
  else if(i.Action is CourtAction.RevokeOffice or CourtAction.ResignOffice){var offer=Source(w,i.Reference,CourtAction.Nominate);Need(i.Owner==offer.Input.Owner&&i.Site==offer.Input.Site,"任命原本と同じ家・任地を指定してください");Need(Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.AcceptOffice&&e.At<w.ElapsedSeconds),"引受後、時刻を進めてから終了の手続きをしてください");Need(i.Action==CourtAction.RevokeOffice?actor==offer.Actor&&i.Person==offer.Input.Person:actor==offer.Input.Person&&Local(w,actor,Head(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)),"任命者の解任通知か、本人の現地辞任が必要です");}
  else if(i.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat or CourtAction.SettleSeat)CheckSeatDispute(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.ReleaseCastleOffice or CourtAction.EnterCastleOffice or CourtAction.PresentTransfer or CourtAction.YieldCastleOffice)CheckCastleTransfer(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.IssueTransfer){var terms=CheckTransferOrder(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);HouseReforms.SendTransfer(w,terms,i,actor);}
  else if(IsReformReply(i.Action)){
   var proposal=CheckReformReply(w,i,actor,w.ElapsedSeconds,w.CommitSequence);
   Need(!w.Messaging.Messages.Any(m=>m.Id==HouseReforms.Letter(proposal.Input,HouseReforms.Answer)),"返書は発送済みです");
   HouseReforms.Reply(w,proposal,HouseReforms.ReplyText(i.Action));
  }
  else if(i.Action==CourtAction.ReportResidence){CheckResidenceReport(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);HouseReforms.SendResidenceNotice(w,i,actor);}
  else if(i.Action==CourtAction.SettleCastleResidence)CheckResidence(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.PresentServiceRelease or CourtAction.ConsentServiceRelease or CourtAction.RefuseServiceRelease)CheckServiceHandover(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.ReceiveProvinceShare or CourtAction.DeliverProvinceShare)CheckProvinceReceipt(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.PrivateCompanyPayment)throw new InvalidDataException("糧食の精算は契約満了時に自動で行います");
  else if(i.Action==CourtAction.KeepPrivateCompany)PrivateRecruitment.CheckStanding(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(IndependenceLetters.Handles(i.Action))IndependenceLetters.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(IndependenceGate.Handles(i.Action))IndependenceGate.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(CoalitionPacts.Handles(i.Action))CoalitionPacts.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(ShogunateHonors.Handles(i.Action))ShogunateHonors.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(CourtInvitation.Handles(i.Action))CourtInvitation.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(SupremeInvestiture.Handles(i.Action))SupremeInvestiture.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(RegentKin.Handles(i.Action))RegentKin.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(CourtPedigree.Handles(i.Action))CourtPedigree.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.SendExileClaim)ExileLetters.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ExpelShogun)ShogunExile.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(SupremeOffice.IsSupport(i.Action))SupremeOffice.CheckSupport(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ResignShogun)SupremeOffice.CheckResignation(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(PeaceMediation.Handles(i.Action))PeaceMediation.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(RankLeave.Handles(i.Action))RankLeave.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(ShogunateAppointments.Handles(i.Action))ShogunateAppointments.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(CentralRank.Handles(i.Action))throw new InvalidDataException("朝廷の発給原本は手動で作成できません");
  else if(CentralMediation.Handles(i.Action))CentralMediation.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(HouseholdDetachment.Handles(i.Action))HouseholdDetachment.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(MusterNotices.Handles(i.Action))MusterNotices.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(PrivateRecruitment.Contract(i.Action))PrivateRecruitment.Check(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.PresentRestorationArrears)HouseRestoration.CheckArrears(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.RecognizeHouseContinuity)HouseRestoration.CheckContinuity(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.RecordRecoveredSeat)HouseRestoration.CheckRecoveredSeat(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(HouseRestoration.IsEscort(i.Action))HouseRestoration.CheckEscort(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(HouseRestoration.IsRice(i.Action))HouseRestoration.CheckRice(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(HouseRestoration.IsSupport(i.Action))HouseRestoration.CheckSupport(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.ChallengeFight or CourtAction.JoinFight or CourtAction.DeclineFight or CourtAction.CensureBoth)CheckFight(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.SurrenderLevyShare)CheckSurrender(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ExchangeLevyShare)CheckLevyExchange(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ReceiveLevyShare)CheckLevyReceipt(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.GrantLevyShare)CheckLevyShare(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.EstablishBarrier)CheckBarrier(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.GrantTempleSeat)CheckTempleSeat(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action is CourtAction.DeferReform or CourtAction.OpenLocalMarket or CourtAction.EnactLetterLaw or CourtAction.EnactMarriageLaw or CourtAction.EnactSuccessionLaw or CourtAction.EnactEqualPenalty or CourtAction.ReduceCastle or CourtAction.AbolishBarrier)CheckReformDecision(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
  else if(i.Action==CourtAction.ConsultReform){
   if(i.Reform==ReformKind.Confiscate && i.LevyGrant!=null)CheckEstateTerms(w,i,actor,w.ElapsedSeconds,w.CommitSequence);
   if(i.Reform is ReformKind.Relocate or ReformKind.ChangeFief || i.Reform is ReformKind.CastleResidence or ReformKind.AllocateProvince && i.NewEstate!=null)CheckEstateTerms(w,i,actor,w.ElapsedSeconds,w.CommitSequence);
   if(i.Reform==ReformKind.RemoveBarrier)CheckBarrier(w,i,actor,place!.Value,w.ElapsedSeconds,w.CommitSequence);
   Need(leader&&place==i.Site&&i.Person!=actor&&Member(w,i.Person,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&
       i.Reform is {} kind && Enum.IsDefined(kind),"現地の当主・所属する相談相手・改革案を指定してください");
   Need(!Events(w).Any(e=>e.Input.Action==i.Action&&e.Input.Person==i.Person&&e.Input.Owner==i.Owner&&e.Input.Site==i.Site&&
       e.Input.Reform==i.Reform&&e.Input.BarrierEdge==i.BarrierEdge&&e.Input.NewEstate==i.NewEstate&&!HouseReforms.AnswerKnown(w,e,actor)),"同じ相手からの返書を待っています");
  }
  else if(i.Action==CourtAction.DeclareScale){var footprint=HouseTerritoryScale.Measure(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1);Need(i.Scale>Scale(w,i.Owner)&&footprint.Supports(i.Scale),"支配拠点の実態が宣言の条件に届いていません");}
  else if(i.Action==CourtAction.Convene){Need(CanConvene(w,actor,i.Owner,i.Site)&&place==i.Site&&i.When>=w.ElapsedSeconds+3600&&i.When<=w.ElapsedSeconds+30*86400L&&i.People is {Length:>0}&&i.People.Distinct().Count()==i.People.Length&&i.People.All(p=>p!=actor&&Member(w,p,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)),"当主が現地で家中の相手と開催時刻を指定してください");}
  else if(i.Action is CourtAction.AcceptInvitation or CourtAction.DeclineInvitation){Need(i.Reference!=null,"届いた招待状を選んでください");var doc=Document(w,i.Reference.Value);Need(doc.Source.Input.Action==CourtAction.Convene&&doc.Recipient==actor&&SocialAssets.Inventory(w,actor).Any(o=>o.Id==doc.Id)&&w.ElapsedSeconds<doc.Source.Input.When,"本人宛ての招待状と開催時刻を確認してください");Need(!Events(w).Any(e=>e.Input.Reference==doc.Id&&e.Input.Action is CourtAction.AcceptInvitation or CourtAction.DeclineInvitation),"本人は返答済みです");}
  else if(i.Action==CourtAction.HoldCouncil){var meeting=Source(w,i.Reference,CourtAction.Convene);i=i with{People=[actor,..Attendees(w,meeting).Where(p=>p!=actor)]};Need((meeting.Actor==actor||DocumentRows(w).Any(d=>d.Source.Input.Id==meeting.Input.Id&&d.Recipient==actor&&Events(w).Any(reply=>reply.Input.Action==CourtAction.AcceptInvitation&&reply.Actor==actor&&reply.Input.Reference==d.Id)))&&leader&&place==meeting.Input.Site&&w.ElapsedSeconds>=meeting.Input.When&&w.ElapsedSeconds<=meeting.Input.When+3600&&!Events(w).Any(e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.Reference==meeting.Input.Id),"予定の場所と時刻に当主本人が集まってください");}
  else if(i.Action==CourtAction.SubmitCase){Need(i.Person!=actor&&Member(w,actor,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&i.Amount is >=1 and <=200,"家中の本人が実行可能な範囲の上申を作ってください");Need(i.Person==Head(w,i.Owner,w.ElapsedSeconds,w.CommitSequence+1)||w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==i.Person&&Active(b,w.ElapsedSeconds,w.CommitSequence+1)),"知っている主君に上申してください");}
  else if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);Need(leader&&proposal.Input.Owner==i.Owner&&proposal.Input.Site==i.Site&&Received(w,proposal.Input.Id,actor)&&place==i.Site,"届いて確認した自分の管轄の上申だけを審査してください");Need(!Events(w).Any(e=>e.Input.Reference==proposal.Input.Id&&e.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase),"この案件は審査済みです");Need(Events(w).Any(e=>e.Actor==actor&&e.Input.Action==CourtAction.HoldCouncil&&e.Input.Owner==i.Owner&&e.Place==place&&w.ElapsedSeconds<=e.At+3600),"現地で評定を開いてから案件を裁いてください");}
  else if(i.Action==CourtAction.DeliverDocument){Need(i.Reference!=null,"引き渡す文書を選んでください");Deliver(w,Document(w,i.Reference.Value),actor,i.Carrier);Add(w,i,actor,place.Value);return;}
  else throw new InvalidDataException("未対応の家中の用件です");
  Add(w,i,actor,place.Value,bound);if(i.Action==CourtAction.ReceiveIndependentKeep)NewGameService.Sync(w);if(IndependenceLetters.Handles(i.Action))IndependenceLetters.Send(w,Events(w).Single(e=>e.Input.Id==i.Id));if(CoalitionPacts.Handles(i.Action))CoalitionPacts.Send(w,Events(w).Single(e=>e.Input.Id==i.Id));CourtInvitation.Send(w,i);SupremeInvestiture.Send(w,i);ShogunateAppointments.Dispatch(w,i,actor);PeaceMediation.Send(w,i,actor);if(i.Action==CourtAction.SendExileClaim)ExileLetters.Send(w,i,actor);if(i.Action is CourtAction.ReceiveLevyShare or CourtAction.RaiseVillageSoldiers)CommunityEconomy.Tick(w);if(i.Action==CourtAction.ConsultReform)HouseReforms.Send(w,i,actor);if(i.Action==CourtAction.DeclareScale)HouseScaleNotices.Dispatch(w,i,actor);Sync(w);NewGameWork.Sync(w);
  if(i.Action is CourtAction.RecruitOffer or CourtAction.Nominate or CourtAction.RevokeOffice or CourtAction.SubmitCase or CourtAction.SendMusterNotice)Deliver(w,Document(w,i.Id),actor,i.Carrier);
  if(i.Action==CourtAction.ApproveCase){var proposal=Source(w,i.Reference,CourtAction.SubmitCase);var q=proposal.Input;if(q.Case==CourtCaseKind.RelieveVillage)CommunityEconomy.ApplyFor(w,new(Id("effect",i.Id.Value),q.Site,EconomyAction.RelieveVillage,q.Amount),actor);else RegionalDomains.Apply(w,new(Id("effect",i.Id.Value),q.Case==CourtCaseKind.SetBudget?TreasuryAction.SetBudget:TreasuryAction.Buy,q.Site,q.Case==CourtCaseKind.BuyWood?"wood":q.Case==CourtCaseKind.BuyWeapons?"weapons":null,q.Case==CourtCaseKind.SetBudget?0:q.Amount,q.Case==CourtCaseKind.SetBudget?q.Amount:0),actor);}
  if(i.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase)Deliver(w,Document(w,i.Id),actor,i.Carrier);
 }
 static EntityId[] Attendees(ContractWorld w,CourtEvent meeting)=>DocumentRows(w).Where(d=>d.Source.Input.Id==meeting.Input.Id&&Events(w).Any(e=>e.Actor==d.Recipient&&e.Input.Reference==d.Id&&e.Input.Action==CourtAction.AcceptInvitation)&&SocialWorld.Site(w,d.Recipient)==meeting.Input.Site&&Member(w,d.Recipient,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1)).Select(d=>d.Recipient).Concat(SocialWorld.Site(w,meeting.Actor)==meeting.Input.Site?[meeting.Actor]:[]).Distinct().ToArray();
 public static bool KnowsReply(ContractWorld w,CourtEvent reply,EntityId observer){if(reply.Actor==observer||Received(w,reply.Input.Id,observer))return true;if(TransportWork.At(w,observer,reply.Place,reply.At,reply.Revision,stableCompletion:true))return true;var original=reply.Input.Reference;if(original==null)return false;var gift=SocialWorld.Ledger(w).SingleOrDefault(g=>g.Input.Id==Id("delivery",original.Value.Value));if(gift==null||gift.Actor!=observer)return false;var carrier=gift.Input.Carrier??gift.Actor;return TransportWork.At(w,carrier,reply.Place,reply.At,reply.Revision,stableCompletion:true)&&SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.AcknowledgeGiftReport&&e.Actor==observer&&e.Input.Reference==gift.Input.Id&&e.At>=reply.At);}
 public static EntityId[] Roster(ContractWorld w,EntityId observer,EntityId owner)=>w.Memberships.Where(m=>m.Target==owner&&Active(m,w.ElapsedSeconds,w.CommitSequence+1)).Select(m=>m.Subject).Distinct().Where(person=>w.NewGame!.Start.People.Any(p=>p.Id==person.Value&&p.Faction==owner.Value)||Events(w).Any(e=>e.Input.Action==CourtAction.RecruitAccept&&e.Actor==person&&Source(w,e.Input.Reference,CourtAction.RecruitOffer).Input.Owner==owner&&KnowsReply(w,e,observer))).ToArray();
 internal static (CourtCaseKind Kind,long Amount)? LocalPetition(CommunityAccounts? account,long reservedRice,MaterialHouse? materials)
 {
  if (account is { Hunger: > 0 })
  {
   long rice = Math.Min(200,Math.Min(account.Hunger,account.HouseRice-reservedRice));
   if (rice > 0) return (CourtCaseKind.RelieveVillage,rice);
  }
  if (materials is { Wood: < 30 }) return (CourtCaseKind.BuyWood,Math.Min(5,30-materials.Wood));
  return null;
 }
 internal static CourtEvent[] CouncilAgenda(ContractWorld w)
 {
  // 当主間の処理順を保存し、同じ当主・同じ現地の既読案件の枠内でのみ順序を調整する。
  var received = Events(w).Where(e => e.Input.Action == CourtAction.SubmitCase &&
      e.Input.Person != w.PlayerControl && Received(w, e.Input.Id, e.Input.Person) &&
      !Events(w).Any(d => d.Input.Reference == e.Input.Id && d.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase))
      .OrderBy(e => e.Revision).ToArray();
  foreach (var group in received.Select((proposal,index) => (proposal,index))
      .GroupBy(x => (x.proposal.Input.Person,x.proposal.Input.Owner,x.proposal.Input.Site)))
  {
   var aspiration = w.NewGame?.Registered.SingleOrDefault(p => p.Id == group.Key.Person.Value)?.Aspiration;
   CourtCaseKind? preferred = aspiration switch
   {
    NpcAspiration.PreserveHouse => CourtCaseKind.BuyWeapons,
    NpcAspiration.UpholdOrder => CourtCaseKind.SetBudget,
    NpcAspiration.ProsperDomain => CourtCaseKind.RelieveVillage,
    NpcAspiration.EncourageTrade => CourtCaseKind.BuyWood,
    _ => null
   };
   if (preferred == null) continue;
   var sorted = group.OrderByDescending(x => x.proposal.Input.Case == preferred).ToArray();
   int n = 0;
   foreach (var slot in group) received[slot.index] = sorted[n++].proposal;
  }
  return received;
 }
 public static CourtEvent[] KnownCases(ContractWorld w,EntityId actor)=>Events(w).Where(e=>e.Input.Action==CourtAction.SubmitCase&&(e.Actor==actor||Received(w,e.Input.Id,actor))).ToArray();
 public static CourtEvent? KnownDecision(ContractWorld w,EntityId actor,EntityId proposal)=>Events(w).LastOrDefault(e=>e.Input.Reference==proposal&&e.Input.Action is CourtAction.ApproveCase or CourtAction.DenyCase or CourtAction.ReviseCase&&(e.Actor==actor||Received(w,e.Input.Id,actor)));
 internal static bool Bound(ContractWorld w,EntityId principal,long at,long rev)=>Events(w).Any(e=>e.Actor==w.PlayerControl&&e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev+1)&&Source(w,e.Input.Reference,CourtAction.RecruitOffer).Actor==principal&&!Events(w).Any(end=>end.Input.Action==CourtAction.ResignService&&end.Input.Reference==e.Input.Reference&&Before(end,at,rev+1)));
 internal static ServiceView Decorate(ContractWorld w,long at,long rev,ServiceView original){using var _ccRev=ControlContext.At(rev);var joined=Events(w).LastOrDefault(e=>e.Actor==w.PlayerControl&&e.Input.Action==CourtAction.RecruitAccept&&Before(e,at,rev+1)&&!Events(w).Any(end=>end.Input.Action==CourtAction.ResignService&&end.Input.Reference==e.Input.Reference&&Before(end,at,rev+1)));if(joined==null)return original;var offer=Source(w,joined.Input.Reference,CourtAction.RecruitOffer);var principal=HouseholdService.BondsAt(w,at,rev+1).LastOrDefault(b=>b.Subject==w.PlayerControl&&Active(b,at,rev+1))?.Target;return original with{Role="家臣（登用合意）",House=offer.Input.Owner,Principal=principal,CanApply=false};}
 public static CourtDocument[] PendingDocuments(ContractWorld w,EntityId actor)=>DocumentRows(w).Where(d=>d.Recipient==actor&&Received(w,d.Id,actor)).ToArray();
 public static RelationshipFact[] Facts(ContractWorld w,EntityId observer,EntityId subject){var rows=new List<RelationshipFact>();foreach(var e in Events(w)){
  if(e.Input.Action==CourtAction.CensureBoth){
   var fight=Source(w,e.Input.Reference,CourtAction.JoinFight);
   if((subject==fight.Actor || subject==fight.Input.Person) && observer!=subject && (observer==e.Actor || observer==fight.Actor || observer==fight.Input.Person))
    rows.Add(new(observer,subject,"court:"+e.Input.Id.Value+"/"+subject.Value,"私闘に加わり、現地で両成敗の叱責を受けた",e.At,e.At,0,-2,0,1));
  }
  if(e.Input.Action==CourtAction.RecruitAccept){var offer=Source(w,e.Input.Reference,CourtAction.RecruitOffer);if(observer==e.Actor&&subject==offer.Actor)rows.Add(new(observer,subject,"court:"+e.Input.Id.Value,"条件書と実物の支度金を確認し、家への所属と直属を引き受けた",e.At,e.At,3,2,0,0));}
  if(e.Input.Action==CourtAction.HoldCouncil&&e.Input.People is {} people&&observer!=subject&&people.Contains(observer)&&people.Contains(subject))rows.Add(new(observer,subject,"court:"+e.Input.Id.Value,"同じ場所の評定に実際に参集した",e.At,e.At,1,1,0,0));
 }return rows.ToArray();}
 internal static bool SignedBudgetDecision(ContractWorld w,EntityId id,EntityId actor)=>Events(w).Any(e=>e.Actor==actor&&e.Input.Action==CourtAction.ApproveCase&&Id("effect",e.Input.Id.Value)==id&&Source(w,e.Input.Reference,CourtAction.SubmitCase).Input.Case==CourtCaseKind.SetBudget);
 static bool Attempt(ContractWorld w,CourtInput input,EntityId actor)=>TransactionalWorld.TryApply(w,c=>Execute(c,input,actor));
 internal static void Tick(ContractWorld w,bool clockAdvanced=false)
 {
  if(w.Court==null)return;
  PrivateRecruitment.SettleDue(w);
  if(w.PlayerControl!=CentralCourtCatalog.Konoe)foreach(var answer in RegentKin.Answers(w))Attempt(w,answer,CentralCourtCatalog.Konoe);
  foreach(var (paper,scribe) in CourtPedigree.Reports(w,false))Attempt(w,paper,scribe);
  foreach(var (letter,author) in ExileLetters.Pending(w,false))Attempt(w,letter,author);
  foreach(var (reply,candidate) in SupremeOffice.SupportAnswers(w,false))Attempt(w,reply,candidate);
  foreach(var (reply,lord) in RankLeave.Pending(w))Attempt(w,reply,lord);
  PeaceMediation.ReadAndRespond(w);
  foreach(var reply in PeaceMediation.Answers(w,false))Attempt(w,reply,Events(w).Single(e=>e.Input.Id==reply.Reference).Input.Person);
  foreach(var (decision,recipient) in IndependenceLetters.NpcResponses(w))Attempt(w,decision,recipient);
  foreach(var (answer,guardLord) in IndependenceGate.NpcAnswers(w))Attempt(w,answer,guardLord);
  foreach(var actor in Events(w).Where(e=>e.Input.Action==CourtAction.ReceiveIndependentKeep && e.Actor!=w.PlayerControl).Select(e=>e.Actor).Distinct().ToArray())
   foreach(var report in IndependenceLetters.KeepReports(w,actor).ToArray())Attempt(w,report,actor);
  CoalitionPacts.ReadMemberCopies(w);
  foreach(var (activity,signer) in CoalitionPacts.NpcActivity(w))Attempt(w,activity,signer);
  foreach(var (confirmation,issuer) in CoalitionPacts.NpcConfirmations(w))Attempt(w,confirmation,issuer);
  foreach(var (answer,head) in CoalitionPacts.NpcReplies(w))Attempt(w,answer,head);
  SupremeInvestiture.ReadIncoming(w);
  if(w.PlayerControl!=CentralCourtCatalog.CourtBrokerIn(w))foreach(var answer in SupremeInvestiture.Responses(w))Attempt(w,answer,CentralCourtCatalog.CourtBrokerIn(w));
  foreach(var invitation in CourtInvitation.Offers(w))Attempt(w,invitation,CentralCourtCatalog.CourtBrokerIn(w));
  ShogunateAppointments.Process(w);
  CentralRank.Process(w);
  MusterNotices.Assemble(w);
  foreach(var (reply,broker) in CentralMediation.Pending(w))Attempt(w,reply,broker);
  foreach(var reply in HouseholdDetachment.Replies(w))Attempt(w,reply.Input,reply.Actor);
  foreach(var doc in DocumentRows(w).Where(d=>d.Recipient!=w.PlayerControl&&Received(w,d.Id,d.Recipient)).ToArray()){
   var source=doc.Source;var p=doc.Recipient;if(SocialWorld.Site(w,p) is not {} site)continue;CourtAction? action=source.Input.Action switch{CourtAction.RecruitOffer=>FreeToJoin(w,p,source.Actor)&&HeadAuthority(w,source.Actor,source.Input.Owner,source.Input.Site,w.ElapsedSeconds,w.CommitSequence+1)?CourtAction.RecruitAccept:CourtAction.RecruitDecline,CourtAction.Nominate=>Vacancy(w,source.Input.Owner,source.Input.Site,source.Input.Role,w.ElapsedSeconds,w.CommitSequence+1)?CourtAction.AcceptOffice:CourtAction.DeclineOffice,CourtAction.Convene=>site==source.Place?CourtAction.AcceptInvitation:CourtAction.DeclineInvitation,_=>null};if(action==null)continue;
   var answers=source.Input.Action==CourtAction.RecruitOffer?new[]{CourtAction.RecruitAccept,CourtAction.RecruitDecline}:source.Input.Action==CourtAction.Nominate?new[]{CourtAction.AcceptOffice,CourtAction.DeclineOffice}:new[]{CourtAction.AcceptInvitation,CourtAction.DeclineInvitation};EntityId reference=source.Input.Action==CourtAction.Convene?doc.Id:source.Input.Id;if(Events(w).Any(e=>e.Actor==p&&e.Input.Reference==reference&&answers.Contains(e.Input.Action)))continue;
   int duty=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticTrait(w,p,SocialTrait.Duty):50;int caution=CharacterRelations.Enabled(w)?CharacterRelations.DiagnosticTrait(w,p,SocialTrait.Caution):50;if(SocialWorld.Axes(w,p,source.Actor).Hostility>30||duty+50<caution)action=source.Input.Action==CourtAction.RecruitOffer?CourtAction.RecruitDecline:source.Input.Action==CourtAction.Nominate?CourtAction.DeclineOffice:CourtAction.DeclineInvitation;
   Attempt(w,new(Id("reply",doc.Id.Value),action.Value,source.Actor,source.Input.Owner,source.Input.Site,Reference:reference),p);
  }
  foreach(var shown in Events(w).Where(e=>e.Input.Action==CourtAction.PresentTransfer && e.Input.Person!=w.PlayerControl && e.Revision<w.CommitSequence)){
   var incumbent=shown.Input.Person;
   if(Events(w).Any(e=>e.Input.Action==CourtAction.YieldCastleOffice && e.Input.Reference==shown.Input.Reference && e.Input.FormerOffice==shown.Input.FormerOffice))continue;
   if(CharacterRelations.Enabled(w) && CharacterRelations.DiagnosticScores(w,incumbent,shown.Actor).Hostility>25)continue;
   Attempt(w,shown.Input with{Id=Id("yield-office",shown.Input.Id.Value),Action=CourtAction.YieldCastleOffice,Person=shown.Actor},incumbent);
  }
  foreach(var declined in Events(w).Where(e=>e.Input.Action==CourtAction.RecruitDecline&&e.Actor!=w.PlayerControl).ToArray()){var offer=Source(w,declined.Input.Reference,CourtAction.RecruitOffer);if(Local(w,declined.Actor,offer.Actor)&&!Events(w).Any(e=>e.Input.Reference==offer.Input.Id&&e.Input.Action==CourtAction.ReturnSigning))Attempt(w,new(Id("return-signing",offer.Input.Id.Value),CourtAction.ReturnSigning,offer.Actor,offer.Input.Owner,offer.Input.Site,Reference:offer.Input.Id),declined.Actor);}
  foreach(var meeting in Events(w).Where(e=>e.Input.Action==CourtAction.Convene&&Head(w,e.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1)!=w.PlayerControl&&e.Input.When<=w.ElapsedSeconds&&w.ElapsedSeconds<=e.Input.When+3600).ToArray()){
   if(!Events(w).Any(e=>e.Input.Action==CourtAction.HoldCouncil&&e.Input.Reference==meeting.Input.Id)&&SocialWorld.Site(w,Head(w,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1))==meeting.Input.Site){var chair=Head(w,meeting.Input.Owner,w.ElapsedSeconds,w.CommitSequence+1);Attempt(w,new(Id("hold",meeting.Input.Id.Value),CourtAction.HoldCouncil,chair,meeting.Input.Owner,meeting.Input.Site,Reference:meeting.Input.Id),chair);}
  }
  foreach(var proposal in CouncilAgenda(w)){
   var principal=proposal.Input.Person;
   if(!Events(w).Any(e=>e.Actor==principal&&e.Input.Action==CourtAction.HoldCouncil&&e.Input.Owner==proposal.Input.Owner&&e.Place==proposal.Input.Site&&w.ElapsedSeconds<=e.At+3600))continue;
   if(Attempt(w,new(Id("decision",proposal.Input.Id.Value),CourtAction.ApproveCase,proposal.Actor,proposal.Input.Owner,proposal.Input.Site,Reference:proposal.Input.Id),principal))break;if(Attempt(w,new(Id("decision",proposal.Input.Id.Value),CourtAction.DenyCase,proposal.Actor,proposal.Input.Owner,proposal.Input.Site,Reference:proposal.Input.Id),principal))break;
  }
  if(clockAdvanced&&w.ElapsedSeconds%86400==0&&RegionalDomains.Enabled(w))foreach(var accepted in Events(w).Where(e=>e.Input.Action==CourtAction.AcceptOffice&&e.Actor!=w.PlayerControl).ToArray()){
   var offer = Source(w,accepted.Input.Reference,CourtAction.Nominate);
   var actor = accepted.Actor;
   var site = offer.Input.Site;
   var owner = offer.Input.Owner;
   long at = w.ElapsedSeconds, rev = w.CommitSequence + 1;
   if (SocialWorld.Site(w,actor)!=site || Appointment(w,actor,owner,site,at,rev)?.Input.Id != accepted.Input.Id ||
       !CommunityEconomy.Local(w,actor,site,at,rev) || Authority(w,actor,owner,site,at,rev)==null) continue;
   var id = Id("npc-case",actor.Value+"/"+at/(30*86400L));
   if (OwnIdUsed(w,id) || KnownCases(w,actor).Any(e => e.Actor==actor && e.Input.Owner==owner &&
       e.Input.Site==site && KnownDecision(w,actor,e.Input.Id)==null)) continue;
   var account = w.Communities?.Accounts.SingleOrDefault(a => a.Site==site);
   var materials = w.Materials?.Houses.SingleOrDefault(m => m.Site==site);
   var need = LocalPetition(account,account==null?0:HouseSalary.Reserved(w,site,at,rev),materials);
   if (need is {} request)
       Attempt(w,new(id,CourtAction.SubmitCase,offer.Actor,owner,site,Amount:request.Amount,Case:request.Kind),actor);

  }
  foreach(var owner in HouseSuccession.Factions(w,w.ElapsedSeconds,w.CommitSequence+1)){
   var head=new EntityId(owner.Head);var home=new EntityId(owner.Home);if(head==w.PlayerControl||SocialWorld.Site(w,head)!=home)continue;
   if(KnownCases(w,head).Any(c=>KnownDecision(w,head,c.Input.Id)==null)&&!Events(w).Any(e=>e.Actor==head&&e.Input.Action==CourtAction.Convene&&e.At/86400==w.ElapsedSeconds/86400)){
    var people=Roster(w,head,new(owner.Id)).Where(p=>p!=head).ToArray();if(people.Length>0){var meetingId=Id("npc-meeting",owner.Id+"/"+w.ElapsedSeconds/86400);if(Attempt(w,new(meetingId,CourtAction.Convene,head,new(owner.Id),home,When:w.ElapsedSeconds+3600){People=people},head))foreach(var doc in DocumentRows(w).Where(d=>d.Source.Input.Id==meetingId&&Local(w,head,d.Recipient)).ToArray())Attempt(w,new(Id("npc-invite",doc.Id.Value),CourtAction.DeliverDocument,doc.Recipient,new(owner.Id),home,Reference:doc.Id),head);}
   }
  }
 }
 internal static long? NextBoundary(ContractWorld w,long target){if(w.Court==null)return null;var dates=Events(w).Where(e=>e.Input.Action is CourtAction.Convene or CourtAction.SendMusterNotice).Select(e=>e.Input.When).Append((w.ElapsedSeconds/86400+1)*86400);return dates.Concat(CentralRank.FilingDates(w)).Concat(CentralRank.Due(w)).Concat(PrivateRecruitment.DueDates(w)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
 internal static void Rewind(ContractWorld w,long at,long rev){using var _ccRev=ControlContext.At(rev);CraftTrade.Rewind(w,at,rev);BrokerWork.Rewind(w,at,rev);if(w.Court!=null){var es=Events(w).Where(e=>e.At<at||e.At==at&&e.Revision<=rev).ToArray();w.Court=es.Length==0?null:w.Court with{Events=es};}}
 public static void Validate(ContractWorld w,ContractWorld? prior)
 {
  if(w.Court==null){Need(prior?.Court==null,"家中の記録が消えています");return;}Need(NewGameWork.Enabled(w)&&w.Court.Schema==1&&w.Court.Policy==Policy,"家中の版が不正です");var es=Events(w);Need(es.Select(e=>e.Input.Id).Distinct().Count()==es.Length&&es.All(e=>e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence&&e.Input.Amount>=0&&Enum.IsDefined(e.Input.Action)&&Enum.IsDefined(e.Input.Role)&&Enum.IsDefined(e.Input.Case)&&Enum.IsDefined(e.Input.Scale)&&w.Characters.Any(c=>c.Id==e.Actor)&&w.Definitions.Sites.Any(s=>s.Id==e.Place)),"家中の原本が不正です");if(prior?.Court!=null)Need(ContractJson.CanonicalHash(es.Take(prior.Court.Events.Length).ToArray())==ContractJson.CanonicalHash(prior.Court.Events),"過去の家中の記録が変更されています");
  foreach(var e in es){var i=e.Input;
   if(i.Action is CourtAction.ChallengeSeat or CourtAction.InsistSeat or CourtAction.SettleSeat)CheckSeatDispute(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.LetterBreach){
    Need(w.Messaging.Messages.Any(m=>m.Id==i.Reference?.Value && (m.Kind is PactSignals.Call or PactSignals.Reply) &&
       m.SenderId==e.Actor.Value && m.RecipientId==i.Person.Value && m.CreatedAt==e.At &&
       i.Id==Id("foreign-letter-breach",m.Id)) && i.Site==e.Place &&
       HouseReforms.LetterLawAt(w,i.Owner,i.Site,e.At,e.Revision),"条文に対応する私信の発送原本がありません");
   }
   if(i.Action==CourtAction.ReportResidence){
    CheckResidenceReport(w,i,e.Actor,e.Place,e.At,e.Revision);
    Need(w.Messaging.Messages.Any(m=>m.Id==HouseReforms.Letter(i,HouseReforms.ResidenceNotice) && m.Kind==HouseReforms.ResidenceNotice &&
        m.CorrelationId==i.Id.Value && m.CreatedAt==e.At && m.SenderId==e.Actor.Value && m.RecipientId==i.Person.Value),"転居届の発送原本がありません");
   }
   if(i.Action==CourtAction.SettleCastleResidence)CheckResidence(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.PresentServiceRelease or CourtAction.ConsentServiceRelease or CourtAction.RefuseServiceRelease)CheckServiceHandover(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.ReceiveProvinceShare or CourtAction.DeliverProvinceShare)CheckProvinceReceipt(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(HouseholdDetachment.Handles(i.Action))HouseholdDetachment.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(MusterNotices.Handles(i.Action))MusterNotices.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   Need(CoalitionPacts.IsActivity(i.Action) || i.ActivityEvidence==null,"行動報告にだけ出兵・和睦の原本を指定できます");
  Need(i.Action==CourtAction.ConfirmCoalition || i.CoalitionSignatures==null,"加盟返書の一覧が他の原本に混入しています");
   Need(i.Action==CourtAction.ProposeCoalition || i.Coalition==null,"包囲網の条件が他の原本に混入しています");
   if(IndependenceLetters.Handles(i.Action)){IndependenceLetters.Check(w,i,e.Actor,e.Place,e.At,e.Revision);IndependenceLetters.Validate(w,e);}
   if(IndependenceGate.Handles(i.Action))IndependenceGate.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(CoalitionPacts.Handles(i.Action)){CoalitionPacts.Check(w,i,e.Actor,e.Place,e.At,e.Revision);CoalitionPacts.Validate(w,e);}
   if(ShogunateHonors.Handles(i.Action))ShogunateHonors.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(CourtInvitation.Handles(i.Action)){CourtInvitation.Check(w,i,e.Actor,e.Place,e.At,e.Revision);CourtInvitation.Validate(w,e);}
   if(SupremeInvestiture.Handles(i.Action)){SupremeInvestiture.Check(w,i,e.Actor,e.Place,e.At,e.Revision);SupremeInvestiture.ValidateLetter(w,e);}
   if(RegentKin.Handles(i.Action))RegentKin.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(CourtPedigree.Handles(i.Action))CourtPedigree.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.SendExileClaim){ExileLetters.Check(w,i,e.Actor,e.Place,e.At,e.Revision);ExileLetters.Validate(w,e);}
   if(i.Action==CourtAction.ExpelShogun)ShogunExile.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(SupremeOffice.IsSupport(i.Action))SupremeOffice.CheckSupport(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.ResignShogun)SupremeOffice.CheckResignation(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(PeaceMediation.Handles(i.Action)){PeaceMediation.Check(w,i,e.Actor,e.Place,e.At,e.Revision);PeaceMediation.Validate(w,e);}
   if(RankLeave.Handles(i.Action))RankLeave.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(ShogunateAppointments.Handles(i.Action)){ShogunateAppointments.Check(w,i,e.Actor,e.Place,e.At,e.Revision);ShogunateAppointments.ValidateLetter(w,e);}
   if(CentralMediation.Handles(i.Action))CentralMediation.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.KeepPrivateCompany)PrivateRecruitment.CheckStanding(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.PrivateCompanyPayment)PrivateRecruitment.ValidatePayment(w,e);
   if(PrivateRecruitment.Contract(i.Action))PrivateRecruitment.Check(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.PresentRestorationArrears)HouseRestoration.CheckArrears(w,i,e.Actor,e.Place,e.At,e.Revision);
     if(i.Action==CourtAction.RecognizeHouseContinuity)HouseRestoration.CheckContinuity(w,i,e.Actor,e.Place,e.At,e.Revision);
     if(i.Action==CourtAction.RecordRecoveredSeat)HouseRestoration.CheckRecoveredSeat(w,i,e.Actor,e.Place,e.At,e.Revision);
     if(HouseRestoration.IsEscort(i.Action))HouseRestoration.CheckEscort(w,i,e.Actor,e.Place,e.At,e.Revision);
     if(HouseRestoration.IsRice(i.Action))HouseRestoration.CheckRice(w,i,e.Actor,e.Place,e.At,e.Revision);
     if(HouseRestoration.IsSupport(i.Action))HouseRestoration.CheckSupport(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.ChallengeFight or CourtAction.JoinFight or CourtAction.DeclineFight or CourtAction.CensureBoth)CheckFight(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.SurrenderLevyShare)CheckSurrender(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.ExchangeLevyShare)CheckLevyExchange(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.ReceiveLevyShare)CheckLevyReceipt(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.GrantLevyShare)CheckLevyShare(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.EstablishBarrier || i.Action==CourtAction.ConsultReform && i.Reform==ReformKind.RemoveBarrier)CheckBarrier(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.GrantTempleSeat)CheckTempleSeat(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.DeferReform or CourtAction.OpenLocalMarket or CourtAction.EnactLetterLaw or CourtAction.EnactMarriageLaw or CourtAction.EnactSuccessionLaw or CourtAction.EnactEqualPenalty or CourtAction.ReduceCastle or CourtAction.AbolishBarrier)CheckReformDecision(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action is CourtAction.ReleaseCastleOffice or CourtAction.EnterCastleOffice or CourtAction.PresentTransfer or CourtAction.YieldCastleOffice)CheckCastleTransfer(w,i,e.Actor,e.Place,e.At,e.Revision);
   if(i.Action==CourtAction.IssueTransfer){
    CheckTransferOrder(w,i,e.Actor,e.Place,e.At,e.Revision);
    Need(w.Messaging.Messages.Any(m=>m.Id==HouseReforms.Letter(i,HouseReforms.TransferOrder) && m.SenderId==e.Actor.Value && m.RecipientId==i.Person.Value && m.CreatedAt==e.At),"移封命令の発送原本がありません");
   }
   if(IsReformReply(i.Action)){
    var proposal=CheckReformReply(w,i,e.Actor,e.At,e.Revision);
    Need(w.Messaging.Messages.Any(m=>m.Id==HouseReforms.Letter(proposal.Input,HouseReforms.Answer) &&
        m.CreatedAt==e.At && m.SenderId==e.Actor.Value && m.RecipientId==proposal.Actor.Value &&
        m.Body==HouseReforms.Name(proposal.Input.Reform!.Value)+"への返答："+HouseReforms.ReplyText(i.Action)),
        "本人の判断と発送された返書が一致しません");
   }
   if(i.Action==CourtAction.ConsultReform && i.Reform==ReformKind.Confiscate && i.LevyGrant!=null)CheckEstateTerms(w,i,e.Actor,e.At,e.Revision);
   if(i.NewEstate!=null){
    Need(CoalitionPacts.IsActivity(i.Action) || i.ActivityEvidence==null,"行動報告にだけ出兵・和睦の原本を指定できます");
  Need(i.Action==CourtAction.ConfirmCoalition || i.CoalitionSignatures==null,"加盟返書の一覧が他の原本に混入しています");
   Need(i.Action==CourtAction.ProposeCoalition || i.Coalition==null,"包囲網の条件は呼びかけの原本に記します");
  Need(CoalitionPacts.IsActivity(i.Action) || i.ActivityEvidence==null,"行動報告にだけ出兵・和睦の原本を指定できます");
  Need(i.Action==CourtAction.ConfirmCoalition || i.CoalitionSignatures==null,"加盟返書の一覧は盟約の成立原本に記します");
  if(i.CoalitionSignatures is {} signatures)i=i with{CoalitionSignatures=[..signatures]};
  if(i.Coalition is {Invitees:not null} coalitionTerms)i=i with{Coalition=coalitionTerms with{Invitees=[..coalitionTerms.Invitees]}};
  Need(i.Action==CourtAction.ConsultReform && (i.Reform is ReformKind.Relocate or ReformKind.ChangeFief or ReformKind.CastleResidence or ReformKind.AllocateProvince),"所領の交換条件と相談原本が一致しません");
    CheckEstateTerms(w,i,e.Actor,e.At,e.Revision);
   }
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
  Need(!PrivateRecruitment.DueDates(w).Any(t=>t<=w.ElapsedSeconds),"私兵の糧食精算が欠落しています");
  CentralRank.Validate(w);
  Need(SigningEscrow(w)>=0,"実在しない支度金を払い出しています");
 }
}
public sealed partial class WorldSession
{
 public CommandResult ManageCourt(CourtInput input,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){var old=HouseholdCourt.Events(state).SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null){var expected=input.Action==CourtAction.HoldCouncil?input with{People=old.Input.People}:input;return new(ContractJson.CanonicalHash(old.Input)==ContractJson.CanonicalHash(expected)?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"同じ家中の用件は受付済みです");}if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");if(NewGameWork.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");if(input.Action==CourtAction.HoldCouncil&&input.People!=null)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"参集者は実際の所在と承諾から確定します");try{Change(w=>HouseholdCourt.Execute(w,input,w.PlayerControl));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"条件と本人の確認、実効の範囲を分けて記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
