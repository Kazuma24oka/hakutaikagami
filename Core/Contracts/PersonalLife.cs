namespace HakutaiKagami.Core.Contracts;

public enum LifeAction { EatPrivateRice, RestOutside, RestInn, CancelRest, SetGoal }
public enum LifeGoal { WorkPay, ReceiveStipend, PayStipend, GainTitle, KeepHouse, KeepHoldings, SupportLord, Revenge }
public sealed record LifeInput(EntityId Id,LifeAction Action,EntityId Site,EntityId? Original=null,LifeGoal Goal=LifeGoal.WorkPay,long Target=0)
{
    // 報復の相手（報復の目標だけ）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Subject {get;init;}
}
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
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or CastleHandover.Policy or CastleAdministration.Policy or HouseSuccession.Policy;
    static bool Before(LifeEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
    static LifeEntry[] Entries(ContractWorld w,long at=long.MaxValue,long rev=long.MaxValue)=>w.Life?.Entries.Where(e=>Before(e,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Life?.Entries.Any(e=>e.Input.Id==id)==true;
    static bool Rest(LifeEntry e)=>e.Input.Action is LifeAction.RestOutside or LifeAction.RestInn;
    static long End(LifeEntry e)=>checked(e.At+(e.Input.Action==LifeAction.RestInn?InnSeconds:OutsideSeconds));
    static long End(LifeEntry e,IEnumerable<LifeEntry> es)=>Math.Min(End(e),es.Where(x=>x.Input.Action==LifeAction.CancelRest&&x.Input.Original==e.Input.Id).Select(x=>x.At).DefaultIfEmpty(long.MaxValue).Min());
    static LifeEntry? Active(IEnumerable<LifeEntry> es,long at)=>es.LastOrDefault(e=>Rest(e)&&e.At<=at&&at<End(e,es));
    public static bool Resting(ContractWorld w,long at,long rev)=>Active(Entries(w,at,rev),at)!=null;
    public static long Net(ContractWorld w,EntityId wallet,long at=long.MaxValue,long rev=long.MaxValue)=>-Entries(w,at,rev).Where(e=>wallet==NewGameWork.Wallet(ControlSuccession.PlayerAtRevision(w,e.Revision))).Sum(e=>e.Paid);
    public static long RiceConsumed(ContractWorld w,EntityId actor,long at=long.MaxValue,long rev=long.MaxValue)=>actor==w.PlayerControl?Entries(w,at,rev).LongCount(e=>e.Input.Action==LifeAction.EatPrivateRice):0;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Life=new(1,Policy,[]);}
    static bool Free(ContractWorld w,long at,long rev)=>!TreatySupply.Carrying(w,at,rev)&&!CastleHandover.Jobs(w,at,rev).Any(j=>j.Pending!=null)&&!HouseLogistics.Active(w,at,rev)&&!NewGameMilitary.LedgerStates(w).Any(s=>{var past=w.ForkMilitaryObservation();past.NewMilitary=s;return NewGameMilitary.CustodyAt(past,at,rev);})&&!w.Tasks.Any(t=>t.Executor==w.PlayerControl&&w.Requests.Any(r=>r.Id==t.RequestId&&(r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<rev))&&(t.FinishedAt==null||t.FinishedAt>at)&&t.Execution is not(ExecutionPhase.Stopped or ExecutionPhase.Completed));
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
            Need(i.Action==LifeAction.SetGoal||i.Goal==LifeGoal.WorkPay&&i.Target==0&&i.Subject==null,"食事/休息に目標値を混在できません");
            if(i.Action==LifeAction.CancelRest)Need(i.Original is {} original&&Active(seen,e.At)?.Input.Id==original,"休息の原本と残り時間を確認してください");
            else {Need(i.Original==null,"不要な原本参照です");Need(Active(seen,e.At)==null,"休息を終えるか中止してください");
                if(i.Action==LifeAction.EatPrivateRice)Need(HouseSalary.PrivateRice(w,ControlSuccession.PlayerAtRevision(w,e.Revision),e.At,e.Revision)>=1,"食事に使える私有米がありません");
                else if(i.Action==LifeAction.SetGoal){Need(i.Target is >=1 and <=5000,"目標は1〜5000です");
                    var me=ControlSuccession.PlayerAtRevision(w,e.Revision);
                    Need(i.Goal==LifeGoal.Revenge?i.Subject is {} foe&&foe!=me&&i.Target==1&&w.Characters.Any(c=>c.Id==foe)&&!Assassination.DeadAt(w,foe,e.At,e.Revision):i.Subject==null,"報復の相手を一人選んでください");}
                else {Need(Free(w,e.At,e.Revision),"任務や預かった部隊を先に引き継いでください");if(i.Action==LifeAction.RestInn)Need(TransportWork.WalletBefore(w,ControlSuccession.PlayerAtRevision(w,e.Revision),e.At,e.Revision)>=InnCost,"私財の自由残高が宿代に足りません。無料休息は選べます");}
            }
            seen.Add(e);at=e.At;rev=e.Revision;
        }
        // Rest is an exclusive personal activity; receiving/reading letters remains possible.
        foreach(var r in es.Where(Rest)){
            long end=End(r,es);Need(!w.Requests.Any(q=>q.Intent.Executor==ControlSuccession.PlayerAtRevision(w,r.Revision)&&q.CommitSequence>r.Revision&&q.CreatedAt<end&&q.CreatedAt>=r.At),"休息中に実務や移動を開始できません");
            Need(!NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Any(q=>q.Revision>r.Revision&&q.At<end&&q.At>=r.At&&q.Input.Action is NewMilitaryAction.Pickup or NewMilitaryAction.Deploy),"休息中に部隊を引き受け/展開できません");
        }
        Need(HouseSalary.PrivateRice(w,w.PlayerControl)>=0,"私有米の二重消費です");
    }
    internal static long? NextBoundary(ContractWorld w,long target)=>Entries(w).Where(Rest).Select(e=>End(e,Entries(w))).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    static LifeGoalCard Goal(ContractWorld w,LifeEntry e,LifeEntry[] es)
    {
        long endRev=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.Revision).DefaultIfEmpty(long.MaxValue).Min();
        if(e.Input.Goal==LifeGoal.KeepHouse)
        {
            // 家の存続：目標を決めてから一年ごとの日に、自分の家（決めたときの所属）が城を一つでも持っていれば一年と数える。
            var me=ControlSuccession.PlayerAtRevision(w,e.Revision);var house=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==me.Value)?.Faction;
            long endAt=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.At).DefaultIfEmpty(long.MaxValue).Min();
            int years=0;long? kept=null;
            for(long t=e.At+365*86400L;t<=Math.Min(w.ElapsedSeconds,endAt)&&house!=null;t+=365*86400L)
            {
                if(!w.NewGame!.Start.Castles.Any(c=>NationalStrategy.Owner(w,new(c.Id),t,long.MaxValue)?.Value==house))break;
                years++;if(years>=e.Input.Target)kept??=t;
            }
            return new(e.Input.Id,e.Input.Goal,e.Input.Target,years,kept!=null,kept);
        }
        if(e.Input.Goal is LifeGoal.KeepHoldings or LifeGoal.SupportLord)
        {
            // 所領維持：決めたときに自分の家が持っていた城を、一年ごとの日にすべて持っていれば一年。
            // 主家支援：主家（自分の家の上の家、なければ自分の家）が、決めたときの城の数以上を持っていれば一年。
            var me=ControlSuccession.PlayerAtRevision(w,e.Revision);var house=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==me.Value)?.Faction;
            if(e.Input.Goal==LifeGoal.SupportLord&&house!=null&&w.FactionRelations?.Relations.FirstOrDefault(r=>r.SubjectFaction==house)?.OverlordFaction is {} lord)house=lord;
            long endAt=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.At).DefaultIfEmpty(long.MaxValue).Min();
            string[] Held(long t)=>house==null?[]:w.NewGame!.Start.Castles.Where(c=>NationalStrategy.Owner(w,new(c.Id),t,long.MaxValue)?.Value==house).Select(c=>c.Id).ToArray();
            var start=Held(e.At);int years=0;long? kept=null;
            for(long t=e.At+365*86400L;t<=Math.Min(w.ElapsedSeconds,endAt)&&start.Length>0;t+=365*86400L)
            {
                var now=Held(t);
                if(e.Input.Goal==LifeGoal.KeepHoldings?start.Except(now).Any():now.Length<start.Length)break;
                years++;if(years>=e.Input.Target)kept??=t;
            }
            return new(e.Input.Id,e.Input.Goal,e.Input.Target,years,kept!=null,kept);
        }
        if(e.Input.Goal==LifeGoal.Revenge)
        {
            // 報復：相手の死を本人が知ったとき（自分で討った、または死を見届けた・伝え聞いた証拠を持つ）。
            var me=ControlSuccession.PlayerAtRevision(w,e.Revision);var foe=e.Input.Subject!.Value;
            long endAt=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.At).DefaultIfEmpty(long.MaxValue).Min();
            long? known=RevengeKnown(w,me,foe,e.At,Math.Min(w.ElapsedSeconds,endAt));
            return new(e.Input.Id,e.Input.Goal,1,known!=null?1:0,known!=null,known);
        }
        if(e.Input.Goal==LifeGoal.GainTitle)
        {
            // 出世：目標を決めた後に、返書を開封して知った叙任の数。
            long endAt=es.Where(n=>n.Input.Action==LifeAction.SetGoal&&n.Revision>e.Revision).Select(n=>n.At).DefaultIfEmpty(long.MaxValue).Min();
            var learned=CentralRank.LearnedBy(w,ControlSuccession.PlayerAtRevision(w,e.Revision)).Where(t=>t>e.At&&t<endAt).OrderBy(t=>t).ToArray();
            return new(e.Input.Id,e.Input.Goal,e.Input.Target,learned.Length,learned.Length>=e.Input.Target,learned.Length>=e.Input.Target?learned[e.Input.Target-1]:null);
        }
        IEnumerable<(long At,long Rev,long Amount)> rows=e.Input.Goal==LifeGoal.WorkPay?(w.WorkCycle?.Payments??[]).Where(p=>w.Tasks.Single(t=>t.Id==p.Task).Executor==w.PlayerControl).Select(p=>(p.At,p.Revision,p.Amount)):(w.Salary?.Entries??[]).Where(p=>p.Input.Action==SalaryAction.Receive).Where(p=>{var t=w.Salary!.Terms.Single(t=>t.Id==p.Input.Contract);return e.Input.Goal==LifeGoal.ReceiveStipend?t.Employee==w.PlayerControl:t.Employer==w.PlayerControl;}).Select(p=>(p.At,p.Revision,w.Salary!.Entries.Single(x=>x.Input.Id==p.Input.Preparation).Input.Amount));
        long total=0;long? met=null;foreach(var p in rows.Where(p=>p.Rev>e.Revision&&p.Rev<endRev).OrderBy(p=>p.Rev)){total=checked(total+p.Amount);if(total>=e.Input.Target)met??=p.At;}
        return new(e.Input.Id,e.Input.Goal,e.Input.Target,total,met!=null,met);
    }
    // 報復の相手に選べる人：本人が姿や消息を記録した人、会いに行った人、いま同じ拠点にいる人（生きている人、自分を除く）。
    public static EntityId[] RevengeChoices(ContractWorld w,EntityId me,long at,long rev)
    {
        var known=w.Evidence.Where(x=>x.Observer==me&&x.RecordedAt<=at).Select(x=>x.Subject)
            .Concat(SocialWorld.Ledger(w).Where(e=>e.Actor==me&&e.Input.Action==SocialWorldAction.Visit&&e.At<=at).Select(e=>e.Input.Person));
        if(at==w.ElapsedSeconds&&SocialWorld.Site(w,me) is {} here)
            known=known.Concat(w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==here).Select(l=>l.Actor));
        var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        return known.Where(x=>x!=me&&alive.Contains(x)&&!Assassination.DeadAt(w,x,at,rev)).Distinct().OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
    }
    // 相手の死を本人が知った時刻：自分で討った・現地で見届けた襲撃、または本人に届いた訃報・目撃した自然死。
    static long? RevengeKnown(ContractWorld w,EntityId me,EntityId foe,long from,long until)
    {
        var slain=InformationSchemes.Events(w).Where(x=>x.Input.Action==SchemeAction.FatalEncounter&&x.Input.Subject==foe&&x.At>=from&&x.At<=until)
            .Where(x=>x.Actor==me||x.Input.Person==me||Assassination.Witness(w,x,me)!=null).Select(x=>(long?)x.At);
        var natural=(w.NaturalDeaths??[]).Where(d=>d.Person==foe&&d.At>=from&&FamilyNotice.KnowsAt(w,d,me,until,w.CommitSequence))
            .Select(d=>(long?)Math.Max(d.At,HakutaiKagami.Core.Messaging.MessageFlow.LearnedAt(w.Messaging,FamilyNotice.LetterId(d,me.Value),me.Value)??d.At));
        return slain.Concat(natural).Min();
    }
    internal static LifeGoalCard[] Goals(ContractWorld w){var es=Entries(w);return es.Where(e=>e.Input.Action==LifeAction.SetGoal).Select(e=>Goal(w,e,es)).ToArray();}
    internal static PersonalLifeView? View(ContractWorld w)
    {
        if(!Enabled(w))return null;var es=Entries(w);var active=Active(es,w.ElapsedSeconds);var service=NewGameService.View(w);var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;
        bool stable=site is {} s&&Local(w,s,w.ElapsedSeconds,w.CommitSequence+1);long due(SalaryTerm t)=>t.Monthly*t.DueAt.LongCount(d=>d<=w.ElapsedSeconds&&(t.EndedAt==null||d<=t.EndedAt))-t.Paid;
        var terms=w.Salary!.Terms;bool held=NewGameMilitary.CustodyAt(w,w.ElapsedSeconds,w.CommitSequence+1);
        string burden=held?"預かった部隊と兵糧は家の所有。本人の生活費とは別で、返報・指揮返還まで責任が残ります。":"本人の私財と家の蔵は別。所属や肩書だけでは他人の米・兵を処分できません。";
        if(HouseLogistics.Active(w,w.ElapsedSeconds,w.CommitSequence+1))burden+=" 家から預かった運搬物・前渡金は帰還精算が必要です。";
        long meals=RiceConsumed(w,w.PlayerControl)+(w.Trade?.Deals.Where(d=>d.Phase==TradePhase.Consumed).Sum(d=>d.Amount)??0);
        long rest=es.Where(Rest).Sum(e=>Math.Max(0,Math.Min(w.ElapsedSeconds,End(e,es))-e.At));
        var start=FamilyBirth.People(w).Single(p=>p.Id==w.PlayerControl.Value);string role=service.Role;if(service.House?.Value==start.Faction&&service.Principal?.Value==start.Lord&&service.House!=null&&start.InitialRole is StartTemplate.Subretainer or StartTemplate.Castellan or StartTemplate.LocalLord)role=NewGameWorld.RoleName(start.InitialRole.Value);if(CastleAdministration.Enabled(w)&&start.InitialRole==StartTemplate.Castellan&&w.Castellan!.Entries.Any(e=>e.Input.Site.Value==start.Site&&CastleAdministration.IsAppointment(e.Input.Action)))role=service.Role;role=CastleAdministration.Role(w,w.PlayerControl)??role;role=HouseSuccession.Role(w)??role;
        return new(role,service.House,service.Principal,stable?site:null,NewGameWork.Balance(w,w.PlayerControl),HouseSalary.PrivateRice(w,w.PlayerControl),meals,rest,active==null?null:End(active,es),active?.Input.Id,stable&&active==null&&Free(w,w.ElapsedSeconds,w.CommitSequence+1),terms.Where(t=>t.Employee==w.PlayerControl).Sum(due),terms.Where(t=>HouseSuccession.Enabled(w)?HouseSuccession.Head(w,t.Owner)==w.PlayerControl:t.Employer==w.PlayerControl).Sum(due),held,burden,es.Where(e=>e.Input.Action==LifeAction.SetGoal).Select(e=>Goal(w,e,es)).ToArray());
    }
}
public sealed partial class WorldSession
{
    public PersonalLifeView? PersonalLifeView(){lock(gate)return PersonalLife.View(state);}
    public CommandResult ManageLife(LifeInput input,EntityId actor,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(actor!=state.PlayerControl||!PersonalLife.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の生活操作ではありません");
        var old=state.Life!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"処理済みの生活操作です");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>PersonalLife.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"生活の原本を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
