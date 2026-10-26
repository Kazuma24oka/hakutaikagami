using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record WorkPayment(EntityId Id,EntityId Task,EntityId Notice,long Amount,long At,long Revision);
public sealed record WorkCycleState(int Schema,string Contract,CareerOpening[] Openings,WorkPayment[] Payments)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorkDutyAgreement[]? Duties {get;init;}
}
public static class NewGameWork
{
    public const string Policy="local-work-physical-reply-fee20-wallet1000-mon-v1";
    public const long InitialCoins=1000,Fee=20;
    public static readonly EntityId Basis=new("basis:local-work-contract");
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&Supported(w.NewGame?.WorkPolicy);
    public static bool Supported(string? policy)=>policy is Policy or WorkProgression.Policy or WorkFollowup.Policy;
    public static bool ScopeSupported(ContractWorld w,EntityId site)=>!Enabled(w)||BranchRoutes.GraphEdges(w).Any(e=>(e.From==NewGameWorld.Node(site.Value)||e.To==NewGameWorld.Node(site.Value))&&!NationalRoutes.Sea(e.Id));
    public static EntityId Wallet(EntityId actor)=>new("lot:personal-mon/"+actor.Value.Replace(':','/'));
    public static bool IsWallet(ContractWorld w,ResourceLot l)=>Enabled(w)&&l.Id==Wallet(l.Owner);
    static EntityId DebtId(TaskState t)=>WorldSession.Child("debt",t.Id,"work-fee");
    static EntityId EffectId(EntityId task)=>WorldSession.Child("effect",task,"work-fee");
    static EntityId TransferId(EntityId task)=>WorldSession.Child("transfer",task,"work-fee");
    static string Key(EntityId task)=>task.Value+"/work-fee/1";
    static bool Eligible(TaskState t)=>t.Action==ActionKind.Inspect&&t.Issuer!=t.Executor;
    public static long Reward(TaskState t)=>t.Kind==TaskKind.VoluntaryRequest?Fee:0;
    public static long Reward(ContractWorld w,TaskState t)=>t.Kind==TaskKind.VoluntaryRequest?Fee:WorkDuty.Reward(w,t);
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>CommunityEconomy.OwnIdUsed(w,id)||PrivateStanddown.OwnIdUsed(w,id)||PrivateInformation.OwnIdUsed(w,id)||TruceNegotiations.OwnIdUsed(w,id)||PersonalCompanions.OwnIdUsed(w,id)||RegionalEconomy.OwnIdUsed(w,id)||FactionNotices.OwnIdUsed(w,id)||NpcArmies.Musters(w).Any(m=>m.Id==id)||FactionStances.OwnIdUsed(w,id)||w.WorkCycle?.Duties?.Any(a=>a.Id==id)==true||w.WorkCycle?.Payments.Any(p=>p.Id==id)==true||TransportWork.Jobs(w).Any(j=>j.Handover.Terms.Id==id||j.Handover.Entries.Any(e=>e.Input.Id==id));
    public static long Balance(ContractWorld w,EntityId actor)=>w.ResourceLots.SingleOrDefault(l=>l.Id==Wallet(actor))?.Quantity??0;
    internal static void Initialize(ContractWorld w){if(!Enabled(w))return;w.WorkCycle=new(1,Policy,[],[]);w.Definitions=w.Definitions with{AuthorityBases=[..w.Definitions.AuthorityBases,new(Basis,"現地任意点検の受領確認後に銭20文。私財初期1000文は試作用開始設定。軍・徴収権とは独立") ]};Sync(w);}
    static ResourceLot[] WalletRows(ContractWorld w)=>w.NewGame!.Start.People.Where(p=>w.Characters.Single(c=>c.Id.Value==p.Id).Life==LifeState.Adult).OrderBy(p=>p.Id,StringComparer.Ordinal).Select(p=>{var id=Wallet(new(p.Id));long delta=w.Transfers.Where(t=>t.ToLot==id).Sum(t=>t.Amount)-w.Transfers.Where(t=>t.FromLot==id).Sum(t=>t.Amount);return new ResourceLot(id,"money",new(p.Id),new(p.Id),new(p.Site),"mon",checked(InitialCoins+delta+PrivateStanddown.Net(w,id)+PrivateInformation.Net(w,id)+PersonalCompanions.Net(w,id)+TransportWork.Net(w,id)+VoyageExpense.Net(w,id)),InitialCoins,0,new(p.Id));}).ToArray();
    internal static void Sync(ContractWorld w){if(Enabled(w))w.ResourceLots=[..w.ResourceLots.Where(l=>!IsWallet(w,l)),..WalletRows(w)];}
    static EntityId[] Papers(ContractWorld w,TaskState t)=>w.Outbox.Where(o=>o.Task==t.Id&&o.Sender==t.Executor&&(o.Kind=="route-observation"||o.Kind=="contract-report")).Select(o=>o.Id).OrderBy(id=>id.Value,StringComparer.Ordinal).ToArray();
    public static MissionReview.Review Review(ContractWorld w,TaskState t,EntityId assessor,long at,MissionReview.Review original)
    {
        if(!Enabled(w)||!Eligible(t))return original;
        var papers=Papers(w,t);bool completeReport=w.Messaging.Messages.Any(m=>m.Id==WorldSession.Child("message",t.Id,"report").Value&&m.Body=="任務完了");
        var known=papers.Where(id=>AssessmentNotice.KnowsAt(w,id.Value,assessor,at)).ToArray();
        if(known.Length!=papers.Length||papers.Length==0||completeReport&&!papers.Any(id=>w.Messaging.Messages.Any(m=>m.Id==id.Value&&m.Kind=="route-observation")))return original with{Achievement="結果未確認",Evidence=known};
        return original with{Achievement=WorkProgression.Enabled(w)&&original.Achievement=="点検完了の申告を受領"?WorkProgression.Achievement:original.Achievement,Evidence=known,Liability=WorkProgression.Enabled(w)?"履行の評価。責任・処分・昇進は別審査":"受領確認。責任・功績・処分は別審査"};
    }
    public static bool SupportsRetry(ContractWorld w,MessageEnvelope m,EntityId actor,string destination)=>Enabled(w)&&m.SenderId==actor.Value&&(m.Kind is "route-observation" or "contract-report" or "assessment-notice" or "assessment-response")&&w.Tasks.Any(t=>Eligible(t)&&t.RequestId.Value==m.CorrelationId&&t.Scope.Value==m.Scope&&NewGameWorld.Node(t.Scope.Value)==destination);
    static MessageEnvelope[] FailedReplies(ContractWorld w)=>!Enabled(w)?[]:w.Messaging.Messages.Where(m=>m.Kind==AssessmentNotice.Kind&&m.SenderId!=w.PlayerControl.Value).Where(m=>{var a=MessageFlow.Latest(w.Messaging,m.Id);return a.Status is AttemptStatus.Lost or AttemptStatus.Rejected&&a.Number<MessageFlow.MaxAttempts&&!w.Messaging.Receipts.Any(r=>r.MessageId==m.Id);}).ToArray();
    internal static int BoundaryCost(ContractWorld w)=>!Enabled(w)?0:w.Tasks.Where(t=>Eligible(t)&&AssessmentNotice.Authored(w,t.Id,t.Issuer).Length==0).Sum(t=>Papers(w,t).Count(id=>!MessageFlow.Knows(w.Messaging,id.Value,t.Issuer.Value))+1)+FailedReplies(w).Length;
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w))return;
        foreach(var m in FailedReplies(w)){var t=w.Tasks.Single(t=>t.RequestId.Value==m.CorrelationId);var a=MessageFlow.Latest(w.Messaging,m.Id);if(MessageFlow.FailureKnown(w.Messaging,a.Id,t.Issuer.Value)&&Local(w,t,w.ElapsedSeconds,w.CommitSequence))RegionalCampaign.Retry(w,t.Issuer,m.Id,NewGameWorld.Node(t.Scope.Value),w.ElapsedSeconds+1);}
        foreach(var t in w.Tasks.Where(Eligible).OrderBy(t=>t.Id.Value,StringComparer.Ordinal)){
            foreach(var id in Papers(w,t))if(!MessageFlow.Knows(w.Messaging,id.Value,t.Issuer.Value)&&w.Messaging.Attempts.Any(a=>a.MessageId==id.Value&&a.ReceiverId==t.Issuer.Value&&a.Status==AttemptStatus.Delivered))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id.Value,t.Issuer.Value,w.ElapsedSeconds,id.Value)];
            var review=AssessmentNotice.ReviewAt(w,t,t.Issuer,w.ElapsedSeconds);if(review.Achievement=="結果未確認"||AssessmentNotice.Authored(w,t.Id,t.Issuer).Length>0)continue;
            var idNotice=new EntityId("message:assessment/"+t.Id.Value.Replace(':','/')+"/1");var payload=new AssessmentNotice.Payload(t.Id,t.Issuer,t.Executor,1,null,review);
            MessageFlow.Send(w.Messaging,new(idNotice.Value,t.RequestId.Value,AssessmentNotice.Kind,t.Issuer.Value,t.Executor.Value,AssessmentNotice.Body(payload),t.Scope.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,idNotice];
            EnsureDebt(w,t,review);
        }
    }
    internal static void EnsureDebt(ContractWorld w,TaskState t,MissionReview.Review review){if(Enabled(w)&&WorkProgression.Completed(review.Achievement)&&Reward(w,t)>0&&!w.Debts.Any(d=>d.Id==DebtId(t)))w.Debts=[..w.Debts,new(DebtId(t),t.Issuer,t.Executor,"mon",Reward(w,t),Basis)];}
    internal static void RecordOpening(ContractWorld w,EntityId actor,string id){if(!Enabled(w)||actor!=w.PlayerControl||!w.Messaging.Messages.Any(m=>m.Id==id&&m.Kind==AssessmentNotice.Kind)||w.WorkCycle!.Openings.Any(o=>o.Message==id))return;w.WorkCycle=w.WorkCycle with{Openings=[..w.WorkCycle.Openings,new(id,w.ElapsedSeconds,w.CommitSequence)]};}
    public static bool Paid(ContractWorld w,EntityId task)=>w.WorkCycle?.Payments.Any(p=>p.Task==task)==true;
    public static bool CanRequest(WorldSession session,EntityId task)
    {
        var w=session.DiagnosticSnapshot();if(!Enabled(w)||Paid(w,task))return false;var t=w.Tasks.SingleOrDefault(t=>t.Id==task);if(t==null||t.Executor!=session.PlayerControl||Reward(w,t)==0)return false;
        bool known=AssessmentNotice.LatestKnown(session,session.PlayerControl).Any(p=>p.Task==task&&WorkProgression.Completed(p.Review.Achievement));
        return known&&w.Locations.Any(l=>l.Actor==session.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==t.Scope)&&RegionalCampaign.Nearby(w,session.PlayerControl).Any(p=>p.Actor==t.Issuer);
    }
    static bool Local(ContractWorld w,TaskState t,long at,long revision)
    {
        var last=NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).OrderBy(q=>q.Revision).Where(q=>q.At<at||q.At==at&&q.Revision<revision).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter);if(last?.Input.Action==NewMilitaryAction.Deploy)return false;
        var point=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==t.Scope).Position;
        return RegionalCampaign.RoadActorPointAt(w,t.Executor,at)==point&&RegionalCampaign.RoadActorPointAt(w,t.Issuer,at)==point&&!w.Branches!.Journeys.Any(j=>(j.Actor==t.Executor||j.Actor==t.Issuer)&&w.Requests.Any(r=>r.Id==w.Tasks.Single(t=>t.Id==j.Task).RequestId&&(r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<revision))&&j.Legs.Any(l=>l.StartAt<=at&&at<l.EndAt));
    }
    internal static void Pay(ContractWorld w,EntityId id,TaskState t,EntityId notice)
    {
        if(!Local(w,t,w.ElapsedSeconds,w.CommitSequence)||Balance(w,t.Issuer)<Reward(w,t))throw new InvalidDataException("現地の受渡しを確定できません。未払い分は残ります");
        var proof=new WorkPayment(id,t.Id,notice,Reward(w,t),w.ElapsedSeconds,w.CommitSequence);var effect=EffectId(t.Id);
        w.WorkCycle=w.WorkCycle! with{Payments=[..w.WorkCycle.Payments,proof]};w.EffectLedger=[..w.EffectLedger,new(effect,Key(t.Id),ContractJson.CanonicalHash(proof),t.Id,w.ElapsedSeconds,"work-payment")];w.Transfers=[..w.Transfers,new(TransferId(t.Id),effect,Wallet(t.Issuer),Wallet(t.Executor),Reward(w,t),w.ElapsedSeconds)];w.Debts=w.Debts.Select(d=>d.Id==DebtId(t)?d with{Outstanding=0}:d).ToArray();Sync(w);
    }
    internal static void Rewind(ContractWorld w,long at,long rev)
    {
        if(!Enabled(w))return;var future=w.WorkCycle!.Payments.Where(p=>p.At>at||p.At==at&&p.Revision>rev).Select(p=>p.Task).ToHashSet();w.WorkCycle=w.WorkCycle with{Payments=w.WorkCycle.Payments.Where(p=>!future.Contains(p.Task)).ToArray(),Openings=w.WorkCycle.Openings.Where(o=>o.At<at||o.At==at&&o.Revision<=rev).ToArray()};w.Transfers=w.Transfers.Where(t=>!future.Any(task=>TransferId(task)==t.Id)).ToArray();w.EffectLedger=w.EffectLedger.Where(e=>!future.Any(task=>EffectId(task)==e.Id)).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        static void Need(bool b,string m){if(!b)throw new InvalidDataException(m);}
        if(!Enabled(w)){Need(w.WorkCycle==null&&!w.ResourceLots.Any(l=>l.Id.Value.StartsWith("lot:personal-mon/",StringComparison.Ordinal)),"Unexpected work economy");return;}
        var state=w.WorkCycle;Need(state!=null&&state.Schema==1&&state.Contract==Policy,"Missing work cycle");Need(w.Definitions.AuthorityBases.Any(b=>b.Id==Basis),"Missing work contract basis");
        Need(state!.Payments.Select(p=>p.Id).Distinct().Count()==state.Payments.Length&&state.Payments.Select(p=>p.Task).Distinct().Count()==state.Payments.Length,"Repeated work payment");
        Need(state.Openings.Select(o=>o.Message).Distinct().Count()==state.Openings.Length,"Repeated work notice opening");
        foreach(var o in state.Openings)Need(w.Messaging.Messages.Any(m=>m.Id==o.Message&&m.Kind==AssessmentNotice.Kind&&m.RecipientId==w.PlayerControl.Value)&&MessageFlow.LearnedAt(w.Messaging,o.Message,w.PlayerControl.Value)==o.At&&o.At<=w.ElapsedSeconds&&o.Revision>0&&o.Revision<=w.CommitSequence,"Invalid work notice opening");
        foreach(var p in state.Payments){var t=w.Tasks.Single(t=>t.Id==p.Task);var note=w.Messaging.Messages.Single(m=>m.Id==p.Notice.Value&&m.Kind==AssessmentNotice.Kind);var payload=AssessmentNotice.Read(note.Body);Need(Eligible(t)&&t.Executor==w.PlayerControl&&p.Amount==Reward(w,t)&&p.Amount>0&&payload.Task==t.Id&&WorkProgression.Completed(payload.Review.Achievement)&&state.Openings.Any(o=>o.Message==note.Id&&o.At<=p.At&&o.Revision<p.Revision)&&p.At<=w.ElapsedSeconds&&p.Revision<=w.CommitSequence&&Local(w,t,p.At,p.Revision),"Payment lacks prior personal receipt or local handover");Need(w.EffectLedger.Contains(new(EffectId(t.Id),Key(t.Id),ContractJson.CanonicalHash(p),t.Id,p.At,"work-payment"))&&w.Transfers.Contains(new(TransferId(t.Id),EffectId(t.Id),Wallet(t.Issuer),Wallet(t.Executor),p.Amount,p.At)),"Missing actual coin transfer");}
        Need(w.EffectLedger.Count(e=>e.Result=="work-payment")==state.Payments.Length&&w.Transfers.Count(x=>w.ResourceLots.Any(l=>IsWallet(w,l)&&(l.Id==x.FromLot||l.Id==x.ToLot)))==state.Payments.Length,"Orphan money movement");
        var debts=w.Tasks.Where(Eligible).Where(t=>Reward(w,t)>0&&AssessmentNotice.Authored(w,t.Id,t.Issuer).Any(m=>WorkProgression.Completed(AssessmentNotice.Read(m.Body).Review.Achievement))).Select(t=>new DebtState(DebtId(t),t.Issuer,t.Executor,"mon",Paid(w,t.Id)?0:Reward(w,t),Basis)).OrderBy(d=>d.Id.Value).ToArray();Need(ContractJson.CanonicalHash(debts)==ContractJson.CanonicalHash(w.Debts.Where(d=>d.Basis==Basis).OrderBy(d=>d.Id.Value).ToArray()),"Work debt was lost or changed");
        Need(ContractJson.CanonicalHash(w.ResourceLots.Where(l=>IsWallet(w,l)).ToArray())==ContractJson.CanonicalHash(WalletRows(w)),"Personal coin balance differs from actual transfers");Need(w.Reservations.All(r=>!w.ResourceLots.Any(l=>IsWallet(w,l)&&l.Id==r.Lot)),"Money wallet is reserved by unsupported operation");
    }
}
