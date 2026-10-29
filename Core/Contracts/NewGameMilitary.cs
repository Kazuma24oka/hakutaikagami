using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum NewMilitaryAction { Request, Accept, Pickup, Load, Unload, Deploy, Form, Hold, Recall, Enter, Report, Return, Inquire, CombatPermit, CombatStart, CombatEnd, Handoff, Reclaim, Withdraw, Rejoin, Attach, Observe, RemoteRecall, PersonalHome, PersonalMove, AutoRout, RetryRecallKnown, RetryRecallHome, InquireUnitKnown, InquireUnitHome, TakeHeldReply, ReturnHeldReportHome, DeliverHeldRecallHome, CancelRecallKnown, CancelRecallHome, RetryCancelKnown, RetryCancelHome, DeliverHeldCancelHome, ObserveField, SendUnitDirective, RetryUnitDirective, DeliverHeldUnitDirective, CancelUnitDirective, RetryUnitDirectiveCancellation, DeliverHeldUnitDirectiveCancellation, RequestEngagement, CancelEngagement }
public sealed record NewMilitaryInput(EntityId Id,NewMilitaryAction Action,long Amount=0,FieldPoint? A=null,FieldPoint? B=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EngagementTaskRequest? Engagement {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EntityId? EngagementOriginal {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? MotionContract {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LedgerPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EntityId? FromLedger {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CycleContract {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ExercisePolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ObservationPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public KnownMilitaryTarget? KnownTarget {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitInquiryTarget? InquiryTarget {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveInput? Directive {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveRetryInput? DirectiveRetry {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveHandoffInput? DirectiveHandoff {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveCancellationInput? DirectiveCancellation {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveCancelRetryInput? DirectiveCancelRetry {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveCancelHandoffInput? DirectiveCancelHandoff {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthorityPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PostPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? DeliverBy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EntityId? TargetOriginal {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? RequestedEffectiveAt {get;init;}
}
public sealed record NewMilitaryRequest(NewMilitaryInput Input,long At,long Revision,string? Message);
public sealed record NewMilitaryTerms(EntityId Principal,EntityId Owner,EntityId Site,EntityId Army,EntityId Case);
public sealed record NewMilitaryOrder(EntityId Id,NewMilitaryAction Action,long At,long Revision,FieldPoint[] Origins,FieldLeg[][] Routes)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int[]? AffectedUnits {get;init;}
}
public sealed record NewMilitaryReport(string Message,long At,long Revision,long ReturnRevision,long Carry,int Soldiers);
public sealed record NewMilitaryState(int Schema,string Contract,NewMilitaryTerms Terms,NewMilitaryRequest[] Requests,MilitaryEvent[] Events,CareerOpening[] Openings,NewMilitaryOrder[] Orders,SupplySortie[] Sorties,SupplyMove[] Moves,SupplyUse[] Uses,NewMilitaryReport[] Reports)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PersonalWithdrawalState? Withdrawal {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MilitaryLedgerIdentity? Binding {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NewRoutState? Rout {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitRetryState? UnitRetry {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitInquiryState? UnitInquiry {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitDirectiveState? UnitDirective {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitReplyRecoveryState? ReplyRecovery {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitHeldDeliveryState? HeldDelivery {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitCommandHoldingState? CommandHolding {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UnitCancellationState? Cancellation {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MilitaryObservationState? Observation {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NewMilitaryAuthorityState? Authority {get;init;}
}
public sealed record NewMilitaryView(string Status,bool CanRequest,bool CanAccept,bool CanPickup,bool Custody,bool Deployed,bool Returning,long Carry,bool CanEnter);

// Personal loan adapter: common clock, couriers, sealed openings, ground planner and resource lots.
public static partial class NewGameMilitary
{
    public const string Contract="new-game-personal-loan-physical-permission-local-custody-shared-ground-person-seconds-return-report-v1";
    public const long OfferSeconds=120,Capacity=100*86400L*2,InitialStock=100*86400L*10;
    public static readonly EntityId Depot=new("lot:new-military/depot"),Carry=new("lot:new-military/carry");
    public static readonly EntityId[] UnitIds=[new("unit:new-military/0"),new("unit:new-military/1"),new("unit:new-military/2")];
    static readonly int[] Strength=[40,30,30];
    static string Hash(string s)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(s));
    static string Text<T>(T x)=>System.Text.Encoding.UTF8.GetString(ContractJson.Write(x));
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool x,string why){if(!x)throw new InvalidDataException(why);}
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static MilitaryEvent? Event(ContractWorld w,MilitaryEffect effect,long? at=null,long? rev=null)=>CaseEvents(w,at,rev).SingleOrDefault(e=>e.Effect==effect);
    static bool Has(ContractWorld w,MilitaryEffect e)=>Event(w,e)!=null;
    static string RequestId(EntityId id)=>"message:new-military/request/"+Hash(id.Value);
    static string NoticeId(ContractWorld w,MilitaryEffect e,string source,EntityId? caseId=null)=>"message:new-military/notice/"+Hash((caseId??CaseStart(w)!.Input.Id).Value+"/"+e+"/"+source);
    public static ArmyEntry[] Armies(ContractWorld w)=>LedgerStates(w).Select(s=>new ArmyEntry(s.Terms.Army,s.Terms.Owner,s.Terms.Principal)).ToArray();
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>NewGameWork.OwnIdUsed(w,id)||LedgerStates(w).Any(s=>s.Requests.Any(q=>q.Input.Id==id));
    static bool LedgerOutstanding(ContractWorld w,long at,long rev)=>NewGameWorld.Enabled(w)&&Event(w,MilitaryEffect.Permission,at,rev)!=null&&Event(w,MilitaryEffect.Returned,at,rev)==null;
    public static bool DeployedAt(ContractWorld w,long at)=>CaseRequests(w,at).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter)?.Input.Action==NewMilitaryAction.Deploy;
    public static bool Deployed(ContractWorld w)=>DeployedAt(w,w.ElapsedSeconds);
    public static bool CustodyAt(ContractWorld w,long at,long rev)=>Event(w,MilitaryEffect.PickedUp,at,rev)!=null&&Event(w,MilitaryEffect.Returned,at,rev)==null;
    internal static FieldPoint OriginAnchor(ContractWorld w)=>FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==w.NewMilitary!.Terms.Site).Position);
    public static FieldPoint Anchor(ContractWorld w)=>ArmyRoadMarch.AssemblyAnchor(w,w.ElapsedSeconds)??OriginAnchor(w);
    public static FieldPoint Home(ContractWorld w,int i){var a=OriginAnchor(w);return new(a.X,a.Y+(i==0?0:i==1?8000:-8000));}
    public static FieldPoint Position(ContractWorld w,int i,long at){if(ArmyRoadMarch.FormationPoint(w,i,at) is {} road)return road;var o=EffectiveOrder(w,i,at);return o==null?Home(w,i):RegionalGround.At(o.Routes[i],o.Origins[i],at);}
    public static bool Moving(ContractWorld w)=>ArmyRoadMarch.Active(w)||w.NewMilitary?.Orders.LastOrDefault()?.Routes.Any(r=>(r.LastOrDefault()?.EndAt??0)>w.ElapsedSeconds)==true;
    public static bool AtHome(ContractWorld w)=>w.NewMilitary!=null&&!Moving(w)&&Enumerable.Range(0,3).All(i=>Position(w,i,w.ElapsedSeconds)==Home(w,i));
    static bool Local(ContractWorld w)=>w.NewMilitary is {} s&&w.Locations.Any(l=>l.Actor==w.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==s.Terms.Site)&&w.Locations.Any(l=>l.Actor==s.Terms.Principal&&l.Kind==LocationKind.AtSite&&l.Site==s.Terms.Site);
    static bool ReportsKnown(ContractWorld w,EntityId lord,long at,long rev)=>w.Requests.Where(q=>q.Intent.Actor==lord&&q.Intent.Executor==w.PlayerControl&&q.CreatedAt<=at&&q.CommitSequence<=rev).All(q=>MessageFlow.LearnedAt(w.Messaging,WorldSession.Child("message",w.Tasks.Single(t=>t.RequestId==q.Id).Id,"report").Value,lord.Value) is {} known&&known<=at);
    static bool Returning(ContractWorld w,long at,long rev)=>CaseRequests(w,at,rev).Any(q=>q.Input.Action==NewMilitaryAction.Return);
    static bool Known(ContractWorld w,MilitaryEvent e,long t,long r)=>w.NewMilitary!.Openings.Any(o=>o.Message==e.Message&&Before(o.At,o.Revision,t,r));
    static MilitaryNotice Read(ContractWorld w,MilitaryEvent e)=>ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==e.Message).Body));
    public static NewMilitaryView View(ContractWorld w,long? at=null,long? revision=null)
    {
        long t=at??w.ElapsedSeconds,r=revision??(ArmyRoadMarch.Enabled(w)?w.CommitSequence+1:w.CommitSequence);var service=NewGameService.View(w,t,r);var s=w.NewMilitary;
        if(s==null)return new("軍務は別に申し出て許可を得ます",service.Principal!=null&&NewGameService.LocalPrincipals(w).Contains(service.Principal.Value)&&w.Locations.Any(l=>l.Actor==service.Principal&&l.Kind==LocationKind.AtSite&&w.Locations.Any(a=>a.Actor==w.PlayerControl&&a.Kind==LocationKind.AtSite&&a.Site==l.Site)),false,false,false,false,false,0,false);
        var known=CaseEvents(w,t,r).Where(e=>Known(w,e,t,r)).Select(e=>(Event:e,Notice:Read(w,e))).OrderBy(e=>e.Notice.EffectiveAt).ThenBy(e=>e.Notice.Effect==MilitaryEffect.Returned?1:0).ToArray();
        bool permission=known.Any(e=>e.Notice.Effect==MilitaryEffect.Permission),returned=known.Any(e=>e.Notice.Effect==MilitaryEffect.Returned),answered=CaseRequests(w,t,r).Any(q=>q.Input.Action==NewMilitaryAction.Accept),picked=CaseRequests(w,t,r).Any(q=>q.Input.Action==NewMilitaryAction.Pickup),returning=Returning(w,t,r);
        var offer=known.FirstOrDefault(e=>e.Notice.Effect==MilitaryEffect.Offered);bool closed=known.Any(e=>e.Notice.Effect is MilitaryEffect.Expired or MilitaryEffect.Refused);
        string status=returned?"指揮返還の通知を確認":returning?"指揮返還願い発送・帰還/兵糧/返報の確認待ち":picked?Detached(w)?"本人は部隊と別行動・部隊の現況は未確認":RecognitionEnabled(w)&&!AllOwnCurrentlySeen(w)?"主家兵の指揮中・全隊の現況は未確認":"主家兵"+TotalStrength(w,t)+"を現地引受":permission?"軍務許可を確認・兵は未引受":closed?"軍務の不承諾・期限切れを確認":offer.Event!=null?answered?"軍務回答発送・許可未確認":"軍務条件を開封・回答可能":"軍務申し出発送・結果未確認";
        return new((Cases(w).Length>1?"軍務"+Cases(w).Length+"：":"")+status,CanRenewKnown(w,t,r),offer.Event!=null&&t<offer.Notice.Deadline&&!answered&&!closed,permission&&!picked&&!returned&&MilitaryPrincipalKnown(w,t,r),picked&&!returned,DeployedAt(w,t),returning,CarryAt(w,t,r),picked&&!returned&&DeployedAt(w,t)&&!RoutActive(w)&&(ArmyRoadMarch.Enabled(w)?ArmyRoadMarch.AtAssembly(w):AtHome(w)&&PersonPosition(w,t)==Home(w,0)));
    }
    public static long CarryAt(ContractWorld w,long at,long rev)=>w.NewMilitary is {} s?s.Moves.Where(m=>s.Requests.Any(q=>q.Input.Id==m.Id&&Before(q.At,q.Revision,at,rev))).Sum(m=>m.Loading?m.Amount:-m.Amount)-s.Uses.Where(u=>u.From<at).Sum(u=>Math.Min(u.Consumed,checked(u.Soldiers*(Math.Min(at,u.Until)-u.From))))+FieldConvoy.Credit(w,CarryFor(w),at,rev)+ArmyRoadMarch.CarryAdjustment(w,CarryFor(w),at,rev):0;
    public static BranchView BranchView(ContractWorld w,ActorView knowledge){var p=PersonPosition(w,w.ElapsedSeconds);var next=PersonPosition(w,w.ElapsedSeconds+1);var a=FieldWorld.ToWorld(p);var b=FieldWorld.ToWorld(next);long distance=RegionalGround.Distance(p,next);return new(knowledge,a,new(b.EastMm-a.EastMm,b.NorthMm-a.NorthMm),0,p!=next,null,[],BranchRoutes.KnownBlocked(w,knowledge.Observer),b,distance);}
    static NewMilitaryTerms Terms(ContractWorld w,EntityId request,string? authorityPolicy=null,string? ledgerPolicy=null)
    {
        if(authorityPolicy==AuthorityContract&&CanRequestOwnAuthority(w)){var f=HouseSuccession.Factions(w).Single(f=>f.Head==w.PlayerControl.Value);var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site!.Value;return new(w.PlayerControl,new(f.Id),site,ledgerPolicy==LedgerIdentityContract?LedgerIdentity(new(f.Id),site).Army:new("army:new-military/"+Hash(f.Id)),request);}
        var v=NewGameService.View(w);Need(v.Principal is {}&&v.House is {}&&NewGameService.LocalPrincipals(w).Contains(v.Principal.Value),"認識した主君と現地で会ってください");var loc=w.Locations.Single(l=>l.Actor==w.PlayerControl);
        return new(v.Principal.Value,v.House.Value,loc.Site!.Value,ledgerPolicy==LedgerIdentityContract?LedgerIdentity(v.House.Value,loc.Site!.Value).Army:new("army:new-military/"+Hash(v.House.Value.Value)),request);
    }
    static void Announce(ContractWorld w,MilitaryEffect effect,string source,long? effective=null,MilitaryEffect? quote=null,EntityId? caseId=null)
    {
        var s=w.NewMilitary!;var id=NoticeId(w,effect,source,caseId);var notice=new MilitaryNotice(quote??effect,source,effective??w.ElapsedSeconds,effect==MilitaryEffect.Offered?w.ElapsedSeconds+OfferSeconds:null);
        if(notice.Effect==MilitaryEffect.EngagementPermission)notice=notice with{Engagement=effect==MilitaryEffect.StatusSent?Read(w,w.NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.EngagementPermission&&e.At==notice.EffectiveAt&&EventCase(w,e)==(caseId??CaseStart(w)!.Input.Id))).Engagement:EngagementTermsFor(w,source)};
        if(notice.Effect==MilitaryEffect.CombatPermission)notice=notice with{Deadline=notice.EffectiveAt+CombatPermitDuration,Combat=CombatTerms(w,notice.EffectiveAt)};
        MessageFlow.Send(w.Messaging,new(id,(caseId??CaseStart(w)!.Input.Id).Value,"new-military-notice",s.Terms.Principal.Value,w.PlayerControl.Value,Text(notice),s.Terms.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];w.NewMilitary=s with{Events=[..s.Events,new(effect,w.ElapsedSeconds,w.CommitSequence,source,id)]};RecordAuthorityProof(w,effect);SyncAuthority(w);
    }
    public static void Dispatch(ContractWorld w,NewMilitaryInput input)
    {
        Need(!WorldClashes.Enabled(w)||input.Action is not(NewMilitaryAction.CombatPermit or NewMilitaryAction.CombatStart),"実在軍の世界では旧架空演習を混在させません");
        Need(input.LedgerPolicy==null||input.Action==NewMilitaryAction.Request&&input.LedgerPolicy==LedgerIdentityContract,"未知の主家別台帳方針です");
        Need(input.FromLedger==null||input.Action==NewMilitaryAction.Request&&input.LedgerPolicy==LedgerIdentityContract,"元台帳の指定は主家別軍務の申出だけです");
        if(MilitaryEconomy.Enabled(w)&&input.Action==NewMilitaryAction.Request)Need(input.LedgerPolicy==LedgerIdentityContract,"新規軍務では主家別の軍用倉を指定してください");
        SelectRequestedLedger(w,input);
        Need(input.AuthorityPolicy==null||input.Action==NewMilitaryAction.Request&&input.AuthorityPolicy==AuthorityContract,"実在発令根拠は版付きの新規軍務申出だけに指定します");
        ValidateEngagementInput(w,input);ValidateKnownTarget(w,input);ValidateUnitDirectiveInput(w,input);ValidateDirectiveRetryInput(w,input);ValidateDirectiveHandoffInput(w,input);ValidateDirectiveCancellationInput(w,input);ValidateDirectiveCancelRetryInput(w,input);ValidateDirectiveCancelHandoffInput(w,input);ValidateInquiryTarget(w,input);
        Need(input.ObservationPolicy==null||(input.Action is NewMilitaryAction.Request or NewMilitaryAction.ObserveField)&&input.ObservationPolicy==ObservationContract,"観測方針は版付きの軍務申出で指定します");
        Need(input.ExercisePolicy==null||(input.Action is NewMilitaryAction.CombatPermit or NewMilitaryAction.CombatStart)&&input.ExercisePolicy==RepeatedExerciseContract,"未知の反復演習方針です");Need(input.ExercisePolicy==null||input.Action!=NewMilitaryAction.CombatStart||input.MotionContract==RoutContract,"新しい反復演習では敗走経路を明示します");Need(input.CycleContract==null||input.Action==NewMilitaryAction.Request&&input.CycleContract==RenewalContract,"再任方針は軍務の申出だけに指定します");Need(CancellationAction(input.Action)?input.TargetOriginal is {} target&&EntityId.IsValid(target.Value):input.TargetOriginal==null,"取消対象は本人が発した原撤退命令です");Need(input.RequestedEffectiveAt==null||CancellationAction(input.Action)&&input.RequestedEffectiveAt>=w.ElapsedSeconds,"取消効力の要求時刻が不正です");Need(input.TransportPolicy==null||SenderEvidenceAction(input.Action)&&input.TransportPolicy==UnitSenderEvidenceContract,"未知の送信者受領証拠方針です");Need(input.DeliverBy==null||(input.Action==NewMilitaryAction.RemoteRecall||CancellationAction(input.Action)||DirectiveAction(input.Action)||DirectiveCancellationAction(input.Action))&&input.DeliverBy>=checked(w.ElapsedSeconds+1),"撤退命令の期限は本人が発送した後の正式受領時刻で指定します");Need(input.OriginalPolicy==null||(input.Action==NewMilitaryAction.RemoteRecall&&input.OriginalPolicy==UnitCommandHoldingContract||DirectiveAction(input.Action)&&input.OriginalPolicy==UnitDirectiveHoldingContract||DirectiveCancellationAction(input.Action)&&input.OriginalPolicy==UnitDirectiveCancelHoldingContract),"未知の原命令保持方針です");Need(input.PostPolicy==null||ReplyPolicyAction(input.Action)&&input.PostPolicy==UnitReplyRecoveryContract,"未知の返書配送方針です");Need(input.MotionContract==null||input.Action==NewMilitaryAction.CombatStart&&input.MotionContract==RoutContract,"未知の交戦移動契約です");Need(Enum.IsDefined(input.Action)&&EntityId.IsValid(input.Id.Value)&&input.Amount>=0,"軍務入力が不正です");var v=View(w);
        if(input.Action==NewMilitaryAction.Request){var planned=Terms(w,input.Id,input.AuthorityPolicy,input.LedgerPolicy);Need(!NpcArmies.HoldsGarrison(w,planned.Owner,planned.Site),"この駐屯兵はNPC軍が引受中です。二重貸与できません");if(w.NewMilitary==null){Need((v.CanRequest||input.AuthorityPolicy==AuthorityContract&&CanRequestOwnAuthority(w))&&input.CycleContract==null,"現地で主君に最初の軍務を申し出てください");w.NewMilitary=new(1,Contract,Terms(w,input.Id,input.AuthorityPolicy,input.LedgerPolicy),[],[],[],[],[],[],[],[]){Binding=input.LedgerPolicy==LedgerIdentityContract?LedgerIdentity(Terms(w,input.Id,input.AuthorityPolicy,input.LedgerPolicy).Owner,Terms(w,input.Id,input.AuthorityPolicy,input.LedgerPolicy).Site):null};}else Need(input.CycleContract==RenewalContract&&CanRenew(w),"前案件の終了を確認し、指揮と兵糧を返して同じ主君に会ってください");}
        var s=w.NewMilitary??throw new InvalidDataException("軍務の申し出がありません");bool custody=CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&CurrentPlacementAuthority(w),deployed=Deployed(w),returning=Returning(w,w.ElapsedSeconds,w.CommitSequence);
        Need(input.Action switch{
            NewMilitaryAction.RequestEngagement=>CanRequestEngagement(w),NewMilitaryAction.CancelEngagement=>CanCancelEngagement(w,input.EngagementOriginal!.Value),
            NewMilitaryAction.ObserveField=>input.ObservationPolicy==ObservationContract&&!RecognitionEnabled(w),NewMilitaryAction.Request=>true,NewMilitaryAction.Accept=>v.CanAccept,
            NewMilitaryAction.Pickup=>v.CanPickup&&!returning&&Has(w,MilitaryEffect.Permission)&&!Has(w,MilitaryEffect.Returned)&&Local(w)&&AtHome(w)&&MilitaryBoundAt(w,w.ElapsedSeconds,w.CommitSequence)&&!TravelWorld.Active(w),
            NewMilitaryAction.Load or NewMilitaryAction.Unload=>custody&&!deployed&&Local(w)&&AtHome(w)&&(input.Action==NewMilitaryAction.Unload||!returning),
            NewMilitaryAction.Deploy=>custody&&!deployed&&(ArmyRoadMarch.Enabled(w)?ArmyRoadMarch.AtAssembly(w):Local(w)&&AtHome(w))&&v.Carry>0&&!returning&&!TravelWorld.Active(w)&&!w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Execution is ExecutionPhase.Running or ExecutionPhase.Waiting),
            NewMilitaryAction.Form or NewMilitaryAction.Hold=>(!RecognitionEnabled(w)||AllOwnCurrentlySeen(w))&&custody&&deployed&&!Detached(w)&&!returning&&v.Carry>0&&!CombatActive(w)&&!RoutActive(w),
            NewMilitaryAction.Recall=>custody&&deployed&&!Detached(w),NewMilitaryAction.Enter=>custody&&deployed&&!RoutActive(w)&&(ArmyRoadMarch.Enabled(w)?ArmyRoadMarch.AtAssembly(w):AtHome(w)&&PersonPosition(w,w.ElapsedSeconds)==Home(w,0)),
            NewMilitaryAction.Report=>custody&&!Detached(w)&&(!RecognitionEnabled(w)||AllOwnCurrentlySeen(w)),
            NewMilitaryAction.Return=>!returning&&v.CanPickup||!returning&&v.Custody,
            NewMilitaryAction.CombatPermit=>custody&&!returning&&(!CombatStarted(w)||input.ExercisePolicy==RepeatedExerciseContract)&&!CaseRequests(w).Any(q=>q.Input.Action==NewMilitaryAction.CombatPermit),NewMilitaryAction.CombatStart=>CanStartCombat(w,input.ExercisePolicy),NewMilitaryAction.CombatEnd=>CombatActive(w),NewMilitaryAction.Inquire=>true,_=>DirectiveCancelHandoffAction(input.Action)&&DirectiveCancellationHandoffAllowed(w,input.DirectiveCancelHandoff)||DirectiveCancelRetryAction(input.Action)&&UnitDirectiveCancelRetryAllowed(w,input.DirectiveCancelRetry!.Cancellation)||DirectiveCancellationAction(input.Action)&&UnitDirectiveCancellationAllowed(w,input.DirectiveCancellation!.Original)||DirectiveHandoffAction(input.Action)&&DirectiveHandoffAllowed(w,input.DirectiveHandoff)||DirectiveRetryAction(input.Action)&&UnitDirectiveRetryAllowed(w,input.DirectiveRetry!.Original)||DirectiveAction(input.Action)&&UnitDirectiveAllowed(w)||WithdrawalAllowed(w,input)||UnitRetryAllowed(w,input.Action,input.TransportPolicy)||UnitInquiryAllowed(w,input.Action)||HeldAllowed(w,input.Action)||CancellationAllowed(w,input.Action,input.TargetOriginal)},"軍務の許可・現地引受・兵糧・帰還条件が不足しています");
        Need(input.Action is NewMilitaryAction.Load or NewMilitaryAction.Unload?input.Amount>0:input.Amount==0,"兵糧数量は受渡しだけに指定します");Need(input.Action==NewMilitaryAction.Form?input.A!=null&&input.B!=null:input.Action==NewMilitaryAction.PersonalMove?input.A!=null&&input.B==null:input.A==null&&input.B==null,"配置線は配置だけに指定します");
        string? message=input.Action is NewMilitaryAction.Request or NewMilitaryAction.Accept or NewMilitaryAction.Return or NewMilitaryAction.Report or NewMilitaryAction.Inquire or NewMilitaryAction.CombatPermit or NewMilitaryAction.RequestEngagement or NewMilitaryAction.CancelEngagement?RequestId(input.Id):null;
        w.NewMilitary=s with{Requests=[..s.Requests,new(input,w.ElapsedSeconds,w.CommitSequence,message)]};EnableAuthority(w,input);
        if(message!=null){long? due=input.Action==NewMilitaryAction.Accept?Event(w,MilitaryEffect.Offered)!.At+OfferSeconds-1:null;string body=input.Action==NewMilitaryAction.Report?Text(new NewMilitaryReport(message,w.ElapsedSeconds,w.CommitSequence,LastEntry(w),v.Carry,TotalStrength(w,w.ElapsedSeconds))):Text(input);MessageFlow.Send(w.Messaging,new(message,CaseStart(w)!.Input.Id.Value,input.Action==NewMilitaryAction.Report?"new-military-report":"new-military-request",w.PlayerControl.Value,s.Terms.Principal.Value,body,s.Terms.Site.Value,w.ElapsedSeconds,due),w.ElapsedSeconds,1);if(input.Action==NewMilitaryAction.Report)w.NewMilitary=w.NewMilitary! with{Reports=[..w.NewMilitary.Reports,new(message,w.ElapsedSeconds,w.CommitSequence,LastEntry(w),v.Carry,TotalStrength(w,w.ElapsedSeconds))]};}
        ApplyWithdrawal(w,input);StartRout(w,input);ApplyUnitRetry(w,input);ApplyUnitInquiry(w,input);ApplyUnitDirective(w,input);EnableDirectiveHolding(w,input);ApplyDirectiveRetry(w,input);EnableReplyRecovery(w,input);EnableCommandHolding(w,input);ApplyCancellation(w,input);ApplyDirectiveCancellation(w,input);ApplyDirectiveCancelRetry(w,input);ApplyHeld(w,input);
        switch(input.Action){
            case NewMilitaryAction.Pickup:MilitaryEconomy.CheckPickup(w,s.Terms.Site,w.ElapsedSeconds,w.CommitSequence);Announce(w,MilitaryEffect.PickedUp,input.Id.Value);break;
            case NewMilitaryAction.Load:case NewMilitaryAction.Unload:Transfer(w,input);break;
            case NewMilitaryAction.Deploy:w.NewMilitary=w.NewMilitary! with{Sorties=[..w.NewMilitary.Sorties,new(w.ElapsedSeconds,null)]};break;
            case NewMilitaryAction.Form:case NewMilitaryAction.Hold:case NewMilitaryAction.Recall:Order(w,input);break;
            case NewMilitaryAction.Enter:var last=w.NewMilitary!.Sorties.Last();w.NewMilitary=w.NewMilitary with{Sorties=[..w.NewMilitary.Sorties[..^1],last with{Until=w.ElapsedSeconds}]};break;
        }
    }
    static long LastEntry(ContractWorld w)=>CaseRequests(w).LastOrDefault(q=>q.Input.Action==NewMilitaryAction.Enter)?.Revision??0;
    static void Transfer(ContractWorld w,NewMilitaryInput i){var s=w.NewMilitary!;long carry=s.Moves.Sum(m=>m.Loading?m.Amount:-m.Amount)-s.Uses.Sum(u=>u.Consumed)+FieldConvoy.Credit(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1)+ArmyRoadMarch.CarryAdjustment(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1),depot=MilitaryEconomy.InitialStock(w)+MilitaryEconomy.Credit(w,DepotFor(w),w.ElapsedSeconds,w.CommitSequence+1)-s.Moves.Sum(m=>m.Loading?m.Amount:-m.Amount);Need(i.Action==NewMilitaryAction.Load?i.Amount<=depot&&i.Amount<=Capacity-carry:i.Amount<=carry,"受渡し可能な兵糧を超えています");var loc=w.Locations.Single(l=>l.Actor==w.PlayerControl);w.NewMilitary=s with{Moves=[..s.Moves,new(i.Id,i.Action==NewMilitaryAction.Load,i.Amount,w.ElapsedSeconds,new(loc.Actor,loc.Kind,loc.Site,loc.Route,loc.SegmentIndex,loc.ProgressMm,loc.LatitudeE7,loc.LongitudeE7,loc.HeightMm,loc.TerrainRevision))]};}
    static void Order(ContractWorld w,NewMilitaryInput i)
    {
        var origins=Enumerable.Range(0,3).Select(n=>Position(w,n,w.ElapsedSeconds)).ToArray();var routes=new FieldLeg[3][];var goals=new FieldPoint[3];
        for(int n=0;n<3;n++){
            var goal=i.Action==NewMilitaryAction.Recall?(ArmyRoadMarch.Enabled(w)?ArmyRoadMarch.AssemblyPoint(w,n,w.ElapsedSeconds):Home(w,n)):i.Action==NewMilitaryAction.Hold?origins[n]:new FieldPoint(i.A!.X+(i.B!.X-i.A.X)*n/2,i.A.Y+(i.B.Y-i.A.Y)*n/2);goals[n]=goal;
            Need(InExerciseRange(w,goal),"この軍務の演習範囲は拠点周囲120mです");if(i.Action==NewMilitaryAction.Form)Need(RegionalGround.Distance(i.A!,i.B!)>=10000,"配置線を10m以上離してください");
            var obstacles=origins.Where((p,k)=>k!=n&&RegionalGround.Distance(p,origins[n])>=2250).Concat(goals.Take(n)).Distinct().ToArray();var path=RegionalGround.Plan(origins[n],goal,w.ElapsedSeconds,obstacles,atlas:true);bool found=false;
            for(int wait=0;wait<=600;wait++){var candidate=path.Select(l=>l with{StartAt=l.StartAt+wait,EndAt=l.EndAt+wait}).ToArray();long until=candidate.LastOrDefault()?.EndAt??w.ElapsedSeconds;if(Enumerable.Range(0,n).Any(k=>RegionalGround.Conflict(candidate,origins[n],routes[k],origins[k],w.ElapsedSeconds,Math.Max(until,routes[k].LastOrDefault()?.EndAt??0)+1)))continue;routes[n]=candidate;found=true;break;}Need(found,"部隊の経路が競合しています");
        }
        w.NewMilitary=w.NewMilitary! with{Orders=[..w.NewMilitary.Orders,new(i.Id,i.Action,w.ElapsedSeconds,w.CommitSequence,origins,routes)]};
    }
    internal static void Tick(ContractWorld w,long from){if(!Deployed(w)||w.ElapsedSeconds<=from)return;var s=w.NewMilitary!;int men=TotalStrength(w,from);long carry=s.Moves.Sum(m=>m.Loading?m.Amount:-m.Amount)-s.Uses.Sum(u=>u.Consumed)+FieldConvoy.Credit(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1)+ArmyRoadMarch.CarryAdjustment(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1),amount=Math.Min(carry,checked(men*(w.ElapsedSeconds-from)));var uses=s.Uses;var next=new SupplyUse(from,w.ElapsedSeconds,men,amount);if(uses.LastOrDefault() is {} last&&last.Until==from&&!FieldConvoy.HasCreditAt(w,CarryFor(w),from)&&!ArmyRoadMarch.HasCreditAt(w,CarryFor(w),from)&&last.From>=s.Sorties.Last().From&&last.Soldiers==men&&(WorldClashes.Sparse(w)||last.Consumed==men*(last.Until-last.From)&&amount==men*(w.ElapsedSeconds-from)))uses=[..uses[..^1],last with{Until=w.ElapsedSeconds,Consumed=last.Consumed+amount}];else uses=[..uses,next];w.NewMilitary=s with{Uses=uses};}
    static bool Ready(ContractWorld w){var s=w.NewMilitary!;if(!Has(w,MilitaryEffect.PickedUp))return Local(w);return Local(w)&&!Deployed(w)&&AtHome(w)&&View(w).Carry==0&&s.Reports.Any(r=>r.Revision>=CaseStart(w)!.Revision&&r.ReturnRevision==LastEntry(w)&&r.Carry==0&&MessageFlow.LearnedAt(w.Messaging,r.Message,s.Terms.Principal.Value) is {} known&&known<=w.ElapsedSeconds);}
    static void UpdateLedger(ContractWorld w)
    {
        var s=w.NewMilitary;if(s==null)return;
        UpdateRout(w);UpdateCancellation(w);UpdateWithdrawal(w);UpdateCancellationHoldings(w);UpdateCommandHolding(w);UpdateHeldDelivery(w);UpdateReplyRecovery(w);s=w.NewMilitary!;
        foreach(var q in CaseRequests(w).Where(q=>q.Message!=null&&MessageFlow.Knows(w.Messaging,q.Message,s.Terms.Principal.Value))){
            if(q.Input.Action==NewMilitaryAction.CombatPermit&&!Has(w,MilitaryEffect.CombatPermission)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&!Returning(w,w.ElapsedSeconds,w.CommitSequence)&&MilitaryBoundAt(w,w.ElapsedSeconds,w.CommitSequence))Announce(w,MilitaryEffect.CombatPermission,q.Message!);
            if(q.Input.Action==NewMilitaryAction.Request&&!Has(w,MilitaryEffect.Offered)&&!Has(w,MilitaryEffect.Refused)){if(MilitaryBoundAt(w,w.ElapsedSeconds,w.CommitSequence)&&IssuerAuthorityAvailable(w)&&ReportsKnown(w,s.Terms.Principal,w.ElapsedSeconds,w.CommitSequence))Announce(w,MilitaryEffect.Offered,q.Message!);else Announce(w,MilitaryEffect.Refused,q.Message!);}
            if(q.Input.Action==NewMilitaryAction.Accept&&!Has(w,MilitaryEffect.Permission)&&!Has(w,MilitaryEffect.Expired)&&!Has(w,MilitaryEffect.Refused)){if(Event(w,MilitaryEffect.Offered) is {} offer&&w.ElapsedSeconds<offer.At+OfferSeconds&&MilitaryBoundAt(w,w.ElapsedSeconds,w.CommitSequence)&&IssuerAuthorityAvailable(w))Announce(w,MilitaryEffect.Permission,q.Message!);else Announce(w,CurrentAuthorityCase(w)&&!IssuerAuthorityAvailable(w)?MilitaryEffect.Refused:MilitaryEffect.Expired,q.Message!);}
            if(q.Input.Action==NewMilitaryAction.Return&&!Has(w,MilitaryEffect.Returned)){if(Ready(w))Announce(w,MilitaryEffect.Returned,q.Message!);else if(!Has(w,MilitaryEffect.Waiting))Announce(w,MilitaryEffect.Waiting,q.Message!);}
            if(q.Input.Action==NewMilitaryAction.Inquire&&!w.NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==q.Message)){var e=CaseEvents(w).LastOrDefault(e=>e.Effect is MilitaryEffect.Returned or MilitaryEffect.Permission or MilitaryEffect.Expired or MilitaryEffect.Refused or MilitaryEffect.CombatPermission or MilitaryEffect.EngagementPermission or MilitaryEffect.EngagementRejected or MilitaryEffect.EngagementCancelled);Announce(w,MilitaryEffect.StatusSent,q.Message!,e?.At??w.ElapsedSeconds,e?.Effect??MilitaryEffect.StatusSent);}
        }
        UpdateEngagementTasks(w);UpdateHistoricalInquiries(w);
        if(Event(w,MilitaryEffect.Offered) is {} pending&&!Has(w,MilitaryEffect.Permission)&&!Has(w,MilitaryEffect.Expired)&&!Has(w,MilitaryEffect.Refused)&&w.ElapsedSeconds>=pending.At+OfferSeconds)Announce(w,MilitaryEffect.Expired,pending.Message);
    }
    static long? LedgerNextBoundary(ContractWorld w,long target){var times=new List<long>();if(Event(w,MilitaryEffect.Offered) is {} e&&!Has(w,MilitaryEffect.Permission)&&!Has(w,MilitaryEffect.Expired))times.Add(e.At+OfferSeconds);if(DirectiveCancelBoundary(w,target) is {} directiveBoundary)times.Add(directiveBoundary);if(CancellationBoundary(w,target) is {} cancellationBoundary)times.Add(cancellationBoundary);if(Deployed(w)){times.Add(w.ElapsedSeconds+1);}return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
    static void RecordLedgerOpening(ContractWorld w,EntityId actor,string id){if(actor==w.PlayerControl&&w.NewMilitary is {} s&&s.Events.Any(e=>e.Message==id)&&!s.Openings.Any(o=>o.Message==id))w.NewMilitary=s with{Openings=[..s.Openings,new(id,w.ElapsedSeconds,w.CommitSequence)]};}
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind.StartsWith("new-military-",StringComparison.Ordinal)&&LedgerStates(w).SingleOrDefault(s=>s.Requests.Any(q=>q.Input.Id.Value==m.CorrelationId)) is {} ledger?NewGameWorld.Node(ledger.Terms.Site.Value):null;
    static long LedgerNet(ContractWorld w,EntityId lot){if(w.NewMilitary==null)return 0;long moved=w.NewMilitary.Moves.Sum(m=>m.Loading?m.Amount:-m.Amount);return lot==DepotFor(w)?-moved:lot==CarryFor(w)?moved-w.NewMilitary.Uses.Sum(u=>u.Consumed)+FieldConvoy.Credit(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1)+ArmyRoadMarch.CarryAdjustment(w,CarryFor(w),w.ElapsedSeconds,w.CommitSequence+1):0;}
    static void SyncLedger(ContractWorld w,bool placePerson=true)
    {
        SyncAuthority(w);if(w.NewMilitary is not {} s){TransportWork.Sync(w);NewGameWork.Sync(w);return;}bool custody=CustodyAt(w,w.ElapsedSeconds,w.CommitSequence);var commander=custody?w.PlayerControl:s.Terms.Principal;
        w.Units=Enumerable.Range(0,3).Select(i=>new UnitState(UnitIdsFor(w)[i],s.Terms.Army,commander,s.Terms.Owner,StrengthAt(w,i,w.ElapsedSeconds),0)).ToArray();
        w.ResourceLots=[new(DepotFor(w),"rice",s.Terms.Owner,s.Terms.Owner,s.Terms.Site,FieldSupply.Unit,MilitaryEconomy.InitialStock(w)+Net(w,DepotFor(w))+MilitaryEconomy.Credit(w,DepotFor(w),w.ElapsedSeconds,w.CommitSequence+1),MilitaryEconomy.InitialStock(w),0),new(CarryFor(w),"rice",s.Terms.Owner,UnitCarries(w)?s.Terms.Owner:w.PlayerControl,s.Terms.Site,FieldSupply.Unit,Net(w,CarryFor(w)),0,0,UnitCarries(w)?UnitIdsFor(w)[0]:w.PlayerControl)];
        TransportWork.Sync(w);NewGameWork.Sync(w);var loc=w.Locations.Single(l=>l.Actor==w.PlayerControl);
        if(!placePerson)return;
        if(Deployed(w)){var p=PersonPosition(w,w.ElapsedSeconds);var point=FieldWorld.ToWorld(p);var geo=HakutaiKagami.Core.World.JapanGeography.Inverse(new(point.EastMm,point.NorthMm));w.Locations=w.Locations.Select(l=>l.Actor==w.PlayerControl?new LocationState(l.Actor,LocationKind.AtPosition,null,null,0,0,geo.LatitudeE7,geo.LongitudeE7,ContinuousWorld.Height(p),Contract,0):l).ToArray();}
        else if(loc.Kind==LocationKind.AtPosition)w.Locations=w.Locations.Select(l=>l.Actor==w.PlayerControl?new LocationState(l.Actor,LocationKind.AtSite,ArmyRoadMarch.AssemblySite(w,w.ElapsedSeconds)??s.Terms.Site,null,0,0,null,null,null,null,0):l).ToArray();
    }
}
