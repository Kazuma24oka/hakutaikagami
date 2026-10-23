using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record RoutRetreat(EntityId Order,long At,long Revision,int[] Units,string Reason);
public sealed record RoutRegroup(int Unit,long At,long Revision,int Morale);
public sealed record RoutArrival(int Unit,long At,long Revision);
public sealed record NewRoutState(string Contract,RoutRetreat[] Retreats,RoutArrival[] Arrivals,RoutRegroup[] Regroups);

public static partial class NewGameMilitary
{
    public const string RoutContract="local-morale-collapse-independent-unit-retreat-physical-regroup-v1";
    public const int RegroupSeconds=60,RegroupMorale=50;
    static bool RoutStarted(ContractWorld w)=>w.NewMilitary?.Requests.Any(q=>q.Input.Action==NewMilitaryAction.CombatStart&&q.Input.MotionContract==RoutContract)==true;
    public static bool RoutActive(ContractWorld w){if(w.NewMilitary?.Rout is not {} raw)return false;var s=RoutScope(w,raw);return s.Retreats.SelectMany(r=>r.Units).Any(i=>!s.Regroups.Any(g=>g.Unit==i));}
    public static string RoutPhase(ContractWorld w,int i)
    {
        var s=w.NewMilitary?.Rout;if(s!=null)s=RoutScope(w,s);
        if(s==null||!s.Retreats.Any(r=>r.Units.Contains(i)))return "standing";
        if(s.Regroups.Any(g=>g.Unit==i))return "regrouped";
        if(Position(w,i,w.ElapsedSeconds)==Home(w,i))return "regrouping";
        return w.NewMilitary!.Orders.Last().Routes[i].Length==0?"rout-blocked":"routed";
    }
    public static int RegroupedMorale(ContractWorld w,int i)=>RepeatedExercise(w)?OwnCombatReport(w,w.ElapsedSeconds)[i].Morale:w.NewMilitary?.Rout?.Regroups.FirstOrDefault(g=>g.Unit==i)?.Morale??OwnCombatReport(w,w.ElapsedSeconds)[i].Morale;
    static NewRoutState? RoutBefore(NewRoutState? s,long at,long rev)=>s==null?null:s with{Retreats=s.Retreats.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray(),Arrivals=s.Arrivals.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray(),Regroups=s.Regroups.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray()};
    static void StartRout(ContractWorld w,NewMilitaryInput i)
    {
        if(i.Action==NewMilitaryAction.CombatStart&&i.MotionContract==RoutContract)w.NewMilitary=w.NewMilitary! with{Rout=w.NewMilitary.Rout??new(RoutContract,[],[],[])};
    }
    static void UpdateRout(ContractWorld w)
    {
        if(w.NewMilitary?.Rout is not {} s||!Deployed(w))return;
        var own=OwnCombatReport(w,w.ElapsedSeconds);var scoped=RoutScope(w,s);
        var trigger=Enumerable.Range(0,3).Where(i=>own[i].Soldiers>0&&own[i].Morale<=25&&!scoped.Retreats.Any(r=>r.Units.Contains(i))).ToArray();
        if(trigger.Length>0){
            var origins=Enumerable.Range(0,3).Select(i=>Position(w,i,w.ElapsedSeconds)).ToArray();
            var old=w.NewMilitary.Orders.LastOrDefault();
            var routes=Enumerable.Range(0,3).Select(i=>old?.Routes[i].ToArray()??[]).ToArray();
            var planned=new HashSet<int>();bool blocked=false;
            foreach(int i in trigger){
                try{
                    var obstacles=origins.Where((p,k)=>k!=i&&RegionalGround.Distance(p,origins[i])>=2250).Concat(planned.Select(k=>Home(w,k))).Distinct().ToArray();
                    var path=RegionalGround.Plan(origins[i],Home(w,i),w.ElapsedSeconds,obstacles,atlas:true);bool found=false;
                    for(int wait=0;wait<=600;wait++){
                        var candidate=path.Select(l=>l with{StartAt=l.StartAt+wait,EndAt=l.EndAt+wait}).ToArray();
                        long end=candidate.LastOrDefault()?.EndAt??w.ElapsedSeconds;
                        if(Enumerable.Range(0,3).Where(k=>k!=i&&(!trigger.Contains(k)||planned.Contains(k))).Any(k=>RegionalGround.Conflict(candidate,origins[i],routes[k],origins[k],w.ElapsedSeconds,Math.Max(end,routes[k].LastOrDefault()?.EndAt??0)+1)))continue;
                        routes[i]=candidate;found=true;break;
                    }
                    if(!found)throw new InvalidDataException("rout reservation blocked");
                }catch(InvalidDataException){routes[i]=[];blocked=true;}
                planned.Add(i);
            }
            var id=new EntityId("order:rout/"+Hash((RepeatedExercise(w)?CombatStart(w)!.Input.Id.Value:w.NewMilitary.Terms.Case.Value)+"/"+w.ElapsedSeconds+"/"+string.Join('/',trigger)));
            w.NewMilitary=w.NewMilitary with{Orders=[..w.NewMilitary.Orders,new(id,NewMilitaryAction.AutoRout,w.ElapsedSeconds,w.CommitSequence,origins,routes)],Rout=s with{Retreats=[..s.Retreats,new(id,w.ElapsedSeconds,w.CommitSequence,trigger,blocked?"morale-collapse/blocked-hold":"morale-collapse")]}};
        }
        s=w.NewMilitary.Rout!;scoped=RoutScope(w,s);
        foreach(int i in scoped.Retreats.SelectMany(r=>r.Units).Distinct()){
            if(RoutScope(w,w.NewMilitary.Rout!).Regroups.Any(g=>g.Unit==i))continue;
            if(Position(w,i,w.ElapsedSeconds)!=Home(w,i))continue;
            if(!RoutScope(w,w.NewMilitary.Rout).Arrivals.Any(a=>a.Unit==i))w.NewMilitary=w.NewMilitary with{Rout=w.NewMilitary.Rout with{Arrivals=[..w.NewMilitary.Rout.Arrivals,new(i,w.ElapsedSeconds,w.CommitSequence)]}};
            long arrival=RoutScope(w,w.NewMilitary.Rout).Arrivals.Single(a=>a.Unit==i).At;
            if(w.ElapsedSeconds<arrival+RegroupSeconds||CombatActive(w))continue;
            w.NewMilitary=w.NewMilitary with{Rout=w.NewMilitary.Rout with{Regroups=[..w.NewMilitary.Rout.Regroups,new(i,w.ElapsedSeconds,w.CommitSequence,RegroupMorale)]}};
        }
    }
    static void ValidateRout(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var r=s.Rout;
        Need((r!=null)==RoutStarted(w),"敗走演習の開始と任意台帳が違います");if(r==null)return;
        Need(r.Contract==RoutContract&&r.Retreats.SelectMany(t=>t.Units.Select(i=>(ExerciseRecordKey(w,t.At,t.Revision),i))).Distinct().Count()==r.Retreats.Sum(t=>t.Units.Length)&&r.Regroups.Select(g=>(ExerciseRecordKey(w,g.At,g.Revision),g.Unit)).Distinct().Count()==r.Regroups.Length&&r.Arrivals.Select(a=>(ExerciseRecordKey(w,a.At,a.Revision),a.Unit)).Distinct().Count()==r.Arrivals.Length,"敗走/到達/再集結が重複しています");
        Need(r.Retreats.All(t=>t.Units.Length>0&&t.Units.All(i=>i is >=0 and <3))&&r.Regroups.All(g=>g.Unit is >=0 and <3)&&r.Arrivals.All(a=>a.Unit is >=0 and <3),"敗走の部隊番号が不正です");
        if(!replay)return;
        foreach(var group in r.Retreats.Select(t=>(t.At,t.Revision)).Concat(r.Arrivals.Select(t=>(t.At,t.Revision))).Concat(r.Regroups.Select(t=>(t.At,t.Revision))).Distinct()){
            Need(group.At>=0&&group.At<=w.ElapsedSeconds&&group.Revision>0&&group.Revision<=w.CommitSequence,"敗走の時点が不正です");
            var prior=BeforeState(w,group.At,group.Revision-1);prior.CommitSequence=group.Revision;BeforeUnitMessages(prior);MessageFlow.Advance(prior.Messaging,prior.ElapsedSeconds);UpdateRout(prior);UpdateCancellation(prior);UpdateWithdrawal(prior);UpdateCancellationHoldings(prior);UpdateCommandHolding(prior);UpdateHeldDelivery(prior);UpdateReplyRecovery(prior);
            Need(Same(prior.NewMilitary!.Rout,RoutBefore(r,group.At,group.Revision)),"士気と到達から敗走/再集結を再現できません");
            Need(Same(prior.NewMilitary.Orders,s.Orders.Where(o=>Before(o.At,o.Revision,group.At,group.Revision)).ToArray()),"敗走経路が原判断と違います");
        }
        var current=ContractJson.Copy(w);UpdateRout(current);
        Need(Same(current.NewMilitary!.Rout,r)&&Same(current.NewMilitary.Orders,s.Orders),"必要な敗走/再集結が欠落しています");
    }
}
