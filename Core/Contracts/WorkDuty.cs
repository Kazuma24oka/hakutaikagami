namespace HakutaiKagami.Core.Contracts;
// A local witnessed agreement for one duty, not a political office or command grant.
public sealed record WorkDutyAgreement(EntityId Id,EntityId Principal,EntityId Bond,EntityId Site,long Fee,long At,long Revision);
public static class WorkDuty
{
    public const long Fee=20;
    public const string Title="陸路点検の担当";
    static WorkDutyAgreement[] Rows(ContractWorld w)=>w.WorkCycle?.Duties??[];
    static bool Prior(long time,long revision,long at,long rev)=>time<at||time==at&&revision<=rev;
    static bool BondActive(ContractWorld w,TimedRelation b,long at,long rev)
    {
        if(at<b.From)return false;
        // End of service may be committed later at the same world second.
        bool ended=b.Until<at||b.Until==at&&b.Revision<=rev;
        bool joined=b.From<at||b.From==at&&(b.Until!=long.MaxValue||b.Revision<=rev);
        return joined&&!ended&&NewGameService.BoundAt(w,b.Target,at,rev);
    }
    public static WorkDutyAgreement? Active(ContractWorld w,long? at=null,long? revision=null)
    {
        long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;
        return Rows(w).LastOrDefault(a=>Prior(a.At,a.Revision,t,r)&&w.ServiceBonds.Any(b=>b.Id==a.Bond&&BondActive(w,b,t,r)));
    }
    public static EntityId? Candidate(ContractWorld w,long? at=null,long? revision=null)
    {
        if(!WorkProgression.Enabled(w))return null;long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;
        var p=NewGameService.View(w,t,r).Principal;if(p==null||WorkProgression.Recognized(w,p.Value,t,r+1)==0)return null;
        var b=w.ServiceBonds.SingleOrDefault(b=>b.Subject==w.PlayerControl&&b.Target==p&&BondActive(w,b,t,r));
        if(b==null||Rows(w).Any(a=>a.Bond==b.Id&&Prior(a.At,a.Revision,t,r)))return null;
        var own=RegionalCampaign.ActorPointAt(w,w.PlayerControl,t);var lord=RegionalCampaign.ActorPointAt(w,p.Value,t);
        var location=w.Locations.Single(l=>l.Actor==w.PlayerControl);
        return own==lord&&BranchRoutes.GraphNodes(w).Any(n=>n.Position==own)&&location.Kind==LocationKind.AtSite?p:null;
    }
    internal static void Accept(ContractWorld w,EntityId id,EntityId principal)
    {
        if(Candidate(w,w.ElapsedSeconds,w.CommitSequence-1)!=principal)throw new InvalidDataException("主君の評価・同じ拠点での面会を確認してください");
        var b=w.ServiceBonds.Single(b=>b.Subject==w.PlayerControl&&b.Target==principal&&BondActive(w,b,w.ElapsedSeconds,w.CommitSequence-1));
        var a=new WorkDutyAgreement(id,principal,b.Id,w.Locations.Single(l=>l.Actor==w.PlayerControl).Site!.Value,Fee,w.ElapsedSeconds,w.CommitSequence);
        w.WorkCycle=w.WorkCycle! with{Duties=[..Rows(w),a]};
    }
    public static long Reward(ContractWorld w,TaskState task)
    {
        if(task.Kind!=TaskKind.BindingOrder||task.Action!=ActionKind.Inspect||task.Executor!=w.PlayerControl)return 0;
        var q=w.Requests.Single(r=>r.Id==task.RequestId);var a=Active(w,q.CreatedAt,q.CommitSequence-1);
        return a!=null&&a.Principal==task.Issuer&&a.Site==task.Scope?a.Fee:0;
    }
    public static void Validate(ContractWorld w)
    {
        var rows=Rows(w);if(rows.Length==0){if(w.WorkCycle?.Duties!=null)throw new InvalidDataException("Empty duty ledger");return;}
        void Need(bool ok){if(!ok)throw new InvalidDataException("担当合意の根拠が不正です");}
        Need(WorkProgression.Enabled(w)&&rows.Select(a=>a.Id).Distinct().Count()==rows.Length&&rows.Select(a=>a.Bond).Distinct().Count()==rows.Length);
        long last=0;
        foreach(var a in rows){
            Need(EntityId.IsValid(a.Id.Value)&&a.Revision>last&&a.Revision<=w.CommitSequence&&a.At>=0&&a.At<=w.ElapsedSeconds&&a.Fee==Fee);last=a.Revision;
            var b=w.ServiceBonds.Single(b=>b.Id==a.Bond);Need(b.Subject==w.PlayerControl&&b.Target==a.Principal&&BondActive(w,b,a.At,a.Revision-1));
            Need(NewGameService.View(w,a.At,a.Revision-1).Principal==a.Principal&&WorkProgression.Recognized(w,a.Principal,a.At,a.Revision)>0);
            var point=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==a.Site).Position;
            Need(RegionalCampaign.ActorPointAt(w,w.PlayerControl,a.At)==point&&RegionalCampaign.ActorPointAt(w,a.Principal,a.At)==point);
            Need(!w.Requests.Any(q=>q.Id==a.Id)&&w.TravelDecisions?.Any(q=>q.Request.Id==a.Id)!=true&&w.Service?.Requests.Any(q=>q.Input.Id==a.Id)!=true&&w.NewMilitary?.Requests.Any(q=>q.Input.Id==a.Id)!=true&&!w.WorkCycle!.Payments.Any(p=>p.Id==a.Id)&&!TransportWork.Jobs(w).Any(j=>j.Handover.Terms.Id==a.Id||j.Handover.Entries.Any(e=>e.Input.Id==a.Id)));
        }
    }
}
public sealed partial class WorldSession
{
    public CommandResult AcceptWorkDuty(EntityId id,EntityId principal,EntityId actor,long revision)
    {
        lock(gate){
            if(actor!=state.PlayerControl||!EntityId.IsValid(id.Value))return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の担当引受ではありません");
            var old=state.WorkCycle?.Duties?.SingleOrDefault(a=>a.Id==id);
            if(old!=null)return new(old.Principal==principal?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"担当は引受済みです");
            if(NewGameMilitary.OwnIdUsed(state,id)||state.Service?.Requests.Any(q=>q.Input.Id==id)==true||state.Requests.Any(q=>q.Id==id)||state.TravelDecisions?.Any(q=>q.Request.Id==id)==true)return new(ResultCode.IdPayloadConflict,id,state.CommitSequence,"別の操作で使ったIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>WorkDuty.Accept(w,id,principal));return new(ResultCode.Accepted,id,state.CommitSequence,"陸路点検の担当を引き受けました。今後の現地点検に一件20文の手当が付きます。");}
            catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"主君の評価と現地の面会が必要です");}
        }
    }
}
