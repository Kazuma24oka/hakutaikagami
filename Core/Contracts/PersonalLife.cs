namespace HakutaiKagami.Core.Contracts;

public enum LifeAction { EatPrivateRice, RestOutside, RestInn, CancelRest, SetGoal }
public enum LifeGoal { WorkPay, ReceiveStipend, PayStipend }
public sealed record LifeInput(EntityId Id,LifeAction Action,EntityId Site,EntityId? Original=null,LifeGoal Goal=LifeGoal.WorkPay,long Target=0);
public sealed record LifeEntry(LifeInput Input,long At,long Revision,long Paid);
public sealed record PersonalLifeState(int Schema,string Policy,LifeEntry[] Entries);
public sealed record LifeGoalCard(EntityId Id,LifeGoal Goal,long Target,long Progress,bool Achieved,long? AchievedAt);
public sealed record PersonalLifeView(string Role,EntityId? House,EntityId? Principal,EntityId? Site,long PrivateMoney,long PrivateRice,long MealRiceConsumed,long RestSeconds,long? RestUntil,EntityId? ActiveRest,bool CanRest,long StipendReceivable,long StipendPayable,bool HoldsArmy,string Burden,LifeGoalCard[] Goals);

// Opt-in v18. Meals and rest are explicit choices, never an automatic poverty/death rule.
public static class PersonalLife
{
    public const string Policy="personal-life-private-food-local-rest-receipt-goals-v18";
    public const long InnCost=10,InnSeconds=7200,OutsideSeconds=14400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or CastleHandover.Policy or CastleAdministration.Policy;
    static bool Before(LifeEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
    static LifeEntry[] Entries(ContractWorld w,long at=long.MaxValue,long rev=long.MaxValue)=>w.Life?.Entries.Where(e=>Before(e,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Life?.Entries.Any(e=>e.Input.Id==id)==true;
    static bool Rest(LifeEntry e)=>e.Input.Action is LifeAction.RestOutside or LifeAction.RestInn;
    static long End(LifeEntry e)=>checked(e.At+(e.Input.Action==LifeAction.RestInn?InnSeconds:OutsideSeconds));
    static long End(LifeEntry e,IEnumerable<LifeEntry> es)=>Math.Min(End(e),es.Where(x=>x.Input.Action==LifeAction.CancelRest&&x.Input.Original==e.Input.Id).Select(x=>x.At).DefaultIfEmpty(long.MaxValue).Min());
    static LifeEntry? Active(IEnumerable<LifeEntry> es,long at)=>es.LastOrDefault(e=>Rest(e)&&e.At<=at&&at<End(e,es));
    public static bool Resting(ContractWorld w,long at,long rev)=>Active(Entries(w,at,rev),at)!=null;
    public static long Net(ContractWorld w,EntityId wallet,long at=long.MaxValue,long rev=long.MaxValue)=>wallet==NewGameWork.Wallet(w.PlayerControl)?-Entries(w,at,rev).Sum(e=>e.Paid):0;
    public static long RiceConsumed(ContractWorld w,EntityId actor,long at=long.MaxValue,long rev=long.MaxValue)=>actor==w.PlayerControl?Entries(w,at,rev).LongCount(e=>e.Input.Action==LifeAction.EatPrivateRice):0;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Life=new(1,Policy,[]);}
    static bool Free(ContractWorld w,long at,long rev)=>!CastleHandover.Jobs(w,at,rev).Any(j=>j.Pending!=null)&&!HouseLogistics.Active(w,at,rev)&&!NewGameMilitary.LedgerStates(w).Any(s=>{var past=w.ForkMilitaryObservation();past.NewMilitary=s;return NewGameMilitary.CustodyAt(past,at,rev);})&&!w.Tasks.Any(t=>t.Executor==w.PlayerControl&&w.Requests.Any(r=>r.Id==t.RequestId&&(r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<rev))&&(t.FinishedAt==null||t.FinishedAt>at)&&t.Execution is not(ExecutionPhase.Stopped or ExecutionPhase.Completed));
    static bool Local(ContractWorld w,EntityId site,long at,long rev)=>TransportWork.At(w,w.PlayerControl,site,at,rev,stableCompletion:true);
    internal static void Apply(ContractWorld w,LifeInput i)
    {
        w.Life=w.Life! with{Entries=[..w.Life.Entries,new(i,w.ElapsedSeconds,w.CommitSequence,i.Action==LifeAction.RestInn?InnCost:0)]};NewGameWork.Sync(w);
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Life==null,"旧版へ生活規則を追加できません");return;}
        Need(w.Life is {Schema:1,Policy:Policy,Entries:not null},"生活原本の版が違います");
        var es=w.Life.Entries;var seen=new List<LifeEntry>();long at=0,rev=0;var ids=new HashSet<EntityId>();
        foreach(var e in es){var i=e.Input;Need(EntityId.IsValid(i.Id.Value)&&ids.Add(i.Id)&&Enum.IsDefined(i.Action)&&Enum.IsDefined(i.Goal)&&e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence,"生活原本の順序が不正です");Need(!FactionStances.OtherIdUsed(w,i.Id,includeLife:false)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使った原本です");Need(Local(w,i.Site,e.At,e.Revision),"本人が拠点にいません");
            Need(e.Paid==(i.Action==LifeAction.RestInn?InnCost:0),"宿代の実支払が違います");
            Need(i.Action==LifeAction.SetGoal||i.Goal==LifeGoal.WorkPay&&i.Target==0,"食事/休息に目標値を混在できません");
            if(i.Action==LifeAction.CancelRest)Need(i.Original is {} original&&Active(seen,e.At)?.Input.Id==original,"休息の原本と残り時間を確認してください");
            else {Need(i.Original==null,"不要な原本参照です");Need(Active(seen,e.At)==null,"休息を終えるか中止してください");
                if(i.Action==LifeAction.EatPrivateRice)Need(HouseSalary.PrivateRice(w,w.PlayerControl,e.At,e.Revision)>=1,"食事に使える私有米がありません");
                else if(i.Action==LifeAction.SetGoal)Need(i.Target is >=1 and <=5000,"目標は1〜5000です");
                else {Need(Free(w,e.At,e.Revision),"任務や預かった部隊を先に引き継いでください");if(i.Action==LifeAction.RestInn)Need(TransportWork.WalletBefore(w,w.PlayerControl,e.At,e.Revision)>=InnCost,"私財の自由残高が宿代に足りません。無料休息は選べます");}
            }
            seen.Add(e);at=e.At;rev=e.Revision;
        }
        // Rest is an exclusive personal activity; receiving/reading letters remains possible.
        foreach(var r in es.Where(Rest)){
            long end=End(r,es);Need(!w.Requests.Any(q=>q.Intent.Executor==w.PlayerControl&&q.CommitSequence>r.Revision&&q.CreatedAt<end&&q.CreatedAt>=r.At),"休息中に実務や移動を開始できません");
            Need(!NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Any(q=>q.Revision>r.Revision&&q.At<end&&q.At>=r.At&&q.Input.Action is NewMilitaryAction.Pickup or NewMilitaryAction.Deploy),"休息中に部隊を引き受け/展開できません");
        }
        Need(HouseSalary.PrivateRice(w,w.PlayerControl)>=0,"私有米の二重消費です");
    }
    internal static long? NextBoundary(ContractWorld w,long target)=>Entries(w).Where(Rest).Select(e=>End(e,Entries(w))).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    static LifeGoalCard Goal(ContractWorld w,LifeEntry e,LifeEntry[] es)
    {
        long endRev=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.Revision).DefaultIfEmpty(long.MaxValue).Min();
        IEnumerable<(long At,long Rev,long Amount)> rows=e.Input.Goal==LifeGoal.WorkPay?(w.WorkCycle?.Payments??[]).Where(p=>w.Tasks.Single(t=>t.Id==p.Task).Executor==w.PlayerControl).Select(p=>(p.At,p.Revision,p.Amount)):(w.Salary?.Entries??[]).Where(p=>p.Input.Action==SalaryAction.Receive).Where(p=>{var t=w.Salary!.Terms.Single(t=>t.Id==p.Input.Contract);return e.Input.Goal==LifeGoal.ReceiveStipend?t.Employee==w.PlayerControl:t.Employer==w.PlayerControl;}).Select(p=>(p.At,p.Revision,w.Salary!.Entries.Single(x=>x.Input.Id==p.Input.Preparation).Input.Amount));
        long total=0;long? met=null;foreach(var p in rows.Where(p=>p.Rev>e.Revision&&p.Rev<endRev).OrderBy(p=>p.Rev)){total=checked(total+p.Amount);if(total>=e.Input.Target)met??=p.At;}
        return new(e.Input.Id,e.Input.Goal,e.Input.Target,total,met!=null,met);
    }
    internal static PersonalLifeView? View(ContractWorld w)
    {
        if(!Enabled(w))return null;var es=Entries(w);var active=Active(es,w.ElapsedSeconds);var service=NewGameService.View(w);var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;
        bool stable=site is {} s&&Local(w,s,w.ElapsedSeconds,w.CommitSequence+1);long due(SalaryTerm t)=>t.Monthly*t.DueAt.LongCount(d=>d<=w.ElapsedSeconds&&(t.EndedAt==null||d<=t.EndedAt))-t.Paid;
        var terms=w.Salary!.Terms;bool held=NewGameMilitary.CustodyAt(w,w.ElapsedSeconds,w.CommitSequence+1);
        string burden=held?"預かった部隊と兵糧は家の所有。本人の生活費とは別で、返報・指揮返還まで責任が残ります。":"本人の私財と家の蔵は別。所属や肩書だけでは他人の米・兵を処分できません。";
        if(HouseLogistics.Active(w,w.ElapsedSeconds,w.CommitSequence+1))burden+=" 家から預かった運搬物・前渡金は帰還精算が必要です。";
        long meals=RiceConsumed(w,w.PlayerControl)+(w.Trade?.Deals.Where(d=>d.Phase==TradePhase.Consumed).Sum(d=>d.Amount)??0);
        long rest=es.Where(Rest).Sum(e=>Math.Max(0,Math.Min(w.ElapsedSeconds,End(e,es))-e.At));
        var start=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value);string role=service.Role;if(service.House?.Value==start.Faction&&service.Principal?.Value==start.Lord&&service.House!=null&&start.InitialRole is StartTemplate.Subretainer or StartTemplate.Castellan or StartTemplate.LocalLord)role=NewGameWorld.RoleName(start.InitialRole.Value);if(CastleAdministration.Enabled(w)&&start.InitialRole==StartTemplate.Castellan&&w.Castellan!.Entries.Any(e=>e.Input.Site.Value==start.Site&&e.Input.Action==CastellanAction.Appoint))role=service.Role;role=CastleAdministration.Role(w,w.PlayerControl)??role;
        return new(role,service.House,service.Principal,stable?site:null,NewGameWork.Balance(w,w.PlayerControl),HouseSalary.PrivateRice(w,w.PlayerControl),meals,rest,active==null?null:End(active,es),active?.Input.Id,stable&&active==null&&Free(w,w.ElapsedSeconds,w.CommitSequence+1),terms.Where(t=>t.Employee==w.PlayerControl).Sum(due),terms.Where(t=>t.Employer==w.PlayerControl).Sum(due),held,burden,es.Where(e=>e.Input.Action==LifeAction.SetGoal).Select(e=>Goal(w,e,es)).ToArray());
    }
}
public sealed partial class WorldSession
{
    public PersonalLifeView? PersonalLifeView(){lock(gate)return PersonalLife.View(state);}
    public CommandResult ManageLife(LifeInput input,EntityId actor,long revision){lock(gate){
        if(actor!=state.PlayerControl||!PersonalLife.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の生活操作ではありません");
        var old=state.Life!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"処理済みの生活操作です");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>PersonalLife.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"生活の原本を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
