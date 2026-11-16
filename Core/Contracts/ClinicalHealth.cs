namespace HakutaiKagami.Core.Contracts;

public sealed record ClinicalPlan(HealthCareInput Input,EntityId Actor,EntityId Site,long At,long DueAt,long Revision);
public sealed record ClinicalLedger(int Schema,string Policy,ClinicalPlan[] Plans,ClinicalReport[] Reports);
public sealed record ClinicalView(int Fatigue,int MedicineExperience,long? TreatmentUntil,int Aftereffect);

// 旧保存には追加しない。医術は治療の完了でだけ経験になり、恒久能力は書き換えない。
public static partial class ClinicalHealth
{
    public const string Policy="clinical-treatment-fatigue-v2";
    public const long TreatmentSeconds=7200;
    public static bool Enabled(ContractWorld w)=>w.NewGame?.HealthPolicy==Policy&&PersonalHealth.Enabled(w);
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Health=w.Health! with{Clinic=new(1,Policy,[],[])};}
    static ClinicalPlan[] Plans(ContractWorld w)=>w.Health?.Clinic?.Plans??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Plans(w).Any(p=>p.Input.Id==id)||(w.Health?.Clinic?.Reports.Any(r=>r.Input.Id==id)??false);
    internal static EntityId Receipt(ClinicalPlan p)=>new("care:"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Input.Id.Value))[..24]);
    static bool Completed(ContractWorld w,ClinicalPlan p)=>w.Health!.Care.Any(c=>c.Input.Id==Receipt(p));
    public static bool Busy(ContractWorld w,EntityId person,long at)=>Enabled(w)&&Plans(w).Any(p=>(p.Input.Carer==person||p.Input.Patient==person)&&p.At<=at&&p.Revision<=(ControlContext.Revision??long.MaxValue)&&at<p.DueAt);
    internal static bool IsCompletion(ContractWorld w,HealthCareEntry e)=>Enabled(w)&&Plans(w).Any(p=>Receipt(p)==e.Input.Id&&p.DueAt==e.At&&p.Input with{Id=e.Input.Id}==e.Input);
    internal static long? Next(ContractWorld w,long target)=>!Enabled(w)?null:Plans(w).Where(p=>!Completed(w,p)&&p.DueAt>w.ElapsedSeconds&&p.DueAt<=target).Select(p=>(long?)p.DueAt).Min();
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    static bool Consent(ContractWorld w,ClinicalPlan p)
    {
        if(p.Input.Carer==p.Actor)return true;
        var a=w.Companions?.Orders.LastOrDefault(q=>q.Input.Person==p.Input.Carer&&q.At<=p.At&&q.Revision<p.Revision&&q.Input.Action is CompanionAction.Agree or CompanionAction.EndAgreement);
        return a?.Input.Action==CompanionAction.Agree;
    }
    static void CheckStart(ContractWorld w,ClinicalPlan p)
    {
        Need(p.Input.Patient==p.Actor,"本人の診療ではありません");
        Need(Consent(w,p),"手当を頼める同行者ではありません");
        Need(PersonalCompanions.SiteAt(w,p.Input.Patient,p.At,p.Revision+1)==p.Site&&PersonalCompanions.SiteAt(w,p.Input.Carer,p.At,p.Revision+1)==p.Site,"患者と手当する人が同じ拠点にいません");
        Need(PersonalHealth.Active(w,p.Input.Patient,p.At).Any(c=>c.Id==p.Input.Condition),"手当する傷病がありません");
        Need(!w.Health!.Care.Any(c=>c.Input.Condition==p.Input.Condition&&c.At<=p.At),"この傷病は手当済みです");
        // 開始前の傷病を検査する。自分自身の予約による拘束は除く。
        var before=ContractJson.Copy(w);before.Health=before.Health! with{Clinic=before.Health.Clinic! with{Plans=Plans(w).Where(q=>q.Revision<p.Revision).ToArray()}};
        Need(PersonalHealth.Can(before,p.Input.Carer,HealthLimit.Judgment,p.At),"手当する人の病状が重すぎます");
        Need(p.Input.Carer!=p.Input.Patient||PersonalHealth.Can(before,p.Input.Carer,HealthLimit.Travel,p.At),"重い傷病で自分では手当できません");
    }
    internal static void Start(ContractWorld w,HealthCareInput input,EntityId actor)
    {
        Need(Enabled(w),"診療の規則がありません");
        Need(!Busy(w,input.Patient,w.ElapsedSeconds)&&!Busy(w,input.Carer,w.ElapsedSeconds),"診療中です");
        Need(!w.Tasks.Any(t=>(t.Executor==input.Carer||t.Executor==input.Patient)&&t.Execution==ExecutionPhase.Running),"用件が進行中です");
        Need(!PersonalCompanions.Occupied(w,input.Carer,w.ElapsedSeconds,w.CommitSequence),"手当する人に用件があります");
        Need(!Plans(w).Any(p=>p.Input.Condition==input.Condition),"この傷病には診療があります");
        var site=SocialWorld.Site(w,actor);Need(site!=null,"拠点にいません");
        var plan=new ClinicalPlan(input,actor,site.Value,w.ElapsedSeconds,checked(w.ElapsedSeconds+TreatmentSeconds),w.CommitSequence);
        CheckStart(w,plan);
        w.Health=w.Health! with{Clinic=w.Health.Clinic! with{Plans=[..Plans(w),plan]}};
    }
    internal static void Complete(ContractWorld w)
    {
        if(!Enabled(w))return;
        foreach(var p in Plans(w).Where(p=>p.DueAt==w.ElapsedSeconds&&!Completed(w,p)).OrderBy(p=>p.Input.Id.Value,StringComparer.Ordinal))
        {
            Need(PersonalCompanions.SiteAt(w,p.Input.Patient,p.DueAt,w.CommitSequence+1)==p.Site&&PersonalCompanions.SiteAt(w,p.Input.Carer,p.DueAt,w.CommitSequence+1)==p.Site,"診療中に拠点が変わっています");
            w.Health=w.Health! with{Care=[..w.Health.Care,new(p.Input with{Id=Receipt(p)},p.Site,p.DueAt,w.CommitSequence)]};
        }
    }
    // 直近一日の実際の徒歩区間を重複なく合計する。休息・待機した時間は疲労を減らす。
    public static int Fatigue(ContractWorld w,EntityId person,long at)
    {
        if(!Enabled(w))return 0;
        long start=Math.Max(0,at-86400),end=start,walking=0;
        foreach(var l in (w.Branches?.Journeys??[]).Where(j=>j.Actor==person).SelectMany(j=>j.Legs).Where(l=>l.StartAt<at&&l.EndAt>start).OrderBy(l=>l.StartAt))
        {long a=Math.Max(end,Math.Max(start,l.StartAt)),b=Math.Min(at,l.EndAt);if(b>a){walking+=b-a;end=b;}}
        long idle=at-start-walking;
        return (int)Math.Clamp((walking*2-idle)/1800+Aftereffect(w,person,at),0,100);
    }
    public static int Aftereffect(ContractWorld w,EntityId person,long at)=>!Enabled(w)?0:PersonalHealth.Conditions(w,person,at).Any(c=>c.Kind==HealthKind.Injury&&c.Severity>=3&&PersonalHealth.RecoverAt(w,c)<=at&&at<PersonalHealth.RecoverAt(w,c)+7*86400)?15:0;
    public static ClinicalView? View(ContractWorld w,EntityId observer,EntityId person)=>!Enabled(w)||observer!=person?null:new(Fatigue(w,person,w.ElapsedSeconds),Plans(w).Count(p=>p.Input.Carer==person&&Completed(w,p))*2,Plans(w).Where(p=>(p.Input.Carer==person||p.Input.Patient==person)&&!Completed(w,p)).Select(p=>(long?)p.DueAt).SingleOrDefault(),Aftereffect(w,person,w.ElapsedSeconds));
    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        if(!Enabled(w)){Need(w.Health?.Clinic==null,"旧保存へ診療を追加できません");return;}
        Need(w.Health?.Clinic is {Schema:1,Policy:Policy,Plans:not null},"診療原本の版が違います");
        ValidateReports(w,prior);
        var plans=Plans(w);if(prior?.Health?.Clinic is {} old)Need(plans.Take(old.Plans.Length).SequenceEqual(old.Plans),"過去の診療を変更できません");
        var ids=new HashSet<EntityId>();long revision=-1;
        foreach(var p in plans)
        {
            Need(EntityId.IsValid(p.Input.Id.Value)&&ids.Add(p.Input.Id)&&p.At>=0&&p.At<=w.ElapsedSeconds&&p.Revision>revision&&p.Revision<=w.CommitSequence&&p.DueAt==p.At+TreatmentSeconds,"診療原本が不正です");
            Need(!FactionStances.OtherIdUsed(w,p.Input.Id,includeClinical:false)&&!FactionStances.OwnIdUsed(w,p.Input.Id),"別の操作の原本です");
            Need(!plans.Any(q=>q.Revision<p.Revision&&q.At<=p.At&&p.At<q.DueAt&&(q.Input.Patient==p.Input.Patient||q.Input.Carer==p.Input.Carer||q.Input.Carer==p.Input.Patient||q.Input.Patient==p.Input.Carer)),"診療が重複しています");
            if(prior==null||p.Revision>prior.CommitSequence)CheckStart(w,p);
            var care=w.Health!.Care.Where(c=>c.Input.Id==Receipt(p)).ToArray();
            Need(care.Length==(p.DueAt<=w.ElapsedSeconds?1:0),"診療の完了時刻が違います");
            if(care.Length==1)Need(IsCompletion(w,care[0])&&care[0].Site==p.Site,"診療と手当原本が違います");
            revision=p.Revision;
        }
    }
}
public sealed partial class WorldSession
{
    public ClinicalView? ClinicalView(){lock(gate)return ClinicalHealth.View(state,state.PlayerControl,state.PlayerControl);}
    CommandResult ManageClinicalHealth(HealthCareInput input,EntityId actor,long revision)
    {
        var old=state.Health!.Clinic!.Plans.SingleOrDefault(p=>p.Input.Id==input.Id);
        if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"診療");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id)||PersonalHealth.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"原本");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"時点");
        try{Change(w=>ClinicalHealth.Start(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"診療");}
        catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }
}
