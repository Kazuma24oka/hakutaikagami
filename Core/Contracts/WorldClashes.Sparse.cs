namespace HakutaiKagami.Core.Contracts;

public static partial class WorldClashes
{
    public const string SparsePolicy="actual-owned-armies-after-due-events-sparse-contact-replay-v2";
    public static bool Sparse(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.WorldArmyPolicy==NpcArmies.CompactCombatPolicy;
    sealed record Window(EntityId Army,long From,long Until,long Barrier);
    static IEnumerable<(long At,long Revision)> ExternalInputs(ContractWorld w)
    {
        return TreatyNegotiations.ExternalInputs(w).Concat(TruceNegotiations.ExternalInputs(w)).Concat(NewGameMilitary.ActiveInputs(w)).Concat(NewGameMilitary.RemoteInputs(w)).Concat(w.Requests.Select(q=>(q.CreatedAt,q.CommitSequence)))
            .Concat(NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests.Where(q=>q.Input.Action!=NewMilitaryAction.AutoRout).Select(q=>(q.At,q.Revision))))
            .Concat(NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Openings.Select(q=>(q.At,q.Revision))))
            .Concat((w.FactionStances?.Decisions??[]).Select(q=>(q.At,q.Revision)))
            .Concat((w.FactionNotices?.Requests??[]).Select(q=>(q.At,q.Revision)))
            .Concat(NpcArmies.Musters(w).Select(q=>(q.At,q.Revision)))
            .Concat((w.NpcArmies?.Marches??[]).Select(q=>(q.At,q.Revision)))
            .Concat((w.NpcArmies?.Returns??[]).Select(q=>(q.At,q.Revision)));
    }
    static long ReplayRevision(ContractWorld w,long at)=>ExternalInputs(w).Where(q=>q.At>=at).Select(q=>q.Revision-1).DefaultIfEmpty(w.CommitSequence).Min();
    static void CheckRevision(ContractWorld w,WorldClashStep step)
    {
        long before=ExternalInputs(w).Where(q=>q.At<step.At).Select(q=>q.Revision).DefaultIfEmpty(0).Max();
        long automatic=NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Orders.Where(o=>o.At==step.At&&!s.Requests.Any(q=>q.Input.Id==o.Id&&q.At==o.At&&q.Revision==o.Revision)).Select(o=>o.Revision)).DefaultIfEmpty(0).Max();
        Need(step.Revision>before&&step.Revision>=automatic&&step.Revision<=ReplayRevision(w,step.At),"接触境界と同秒の受領・本人の判断の順序が違います");
    }
    static bool PotentialHostility(ContractWorld w,EntityId a,EntityId b,long from,long until)
    {
        var ds=w.FactionStances?.Decisions??[];return ds.Any(d=>d.Input.Action==FactionStanceAction.DeclareHostile&&
            (d.Input.Owner==a&&d.Input.Target==b||d.Input.Owner==b&&d.Input.Target==a)&&d.Input.EffectiveAt<=until&&
            Math.Min(d.Input.Until,ds.Where(q=>q.Input.Action==FactionStanceAction.WithdrawOwnDeclaration&&q.Input.Original==d.Input.Id).Select(q=>q.Input.EffectiveAt).DefaultIfEmpty(long.MaxValue).Min())>from);
    }
    static IEnumerable<long> Changes(ContractWorld w,NewMilitaryState ledger,long from,long until)
    {
        var times=new List<long>{from,until};
        times.AddRange(TruceNegotiations.Changes(w));times.AddRange(TreatyNegotiations.Changes(w));times.AddRange(ledger.Requests.Select(q=>q.At));times.AddRange(ledger.Events.Select(q=>q.At));times.AddRange(ledger.Openings.Select(q=>q.At));times.AddRange(ledger.Orders.Select(q=>q.At));
        times.AddRange(ledger.Orders.SelectMany(o=>o.Routes.SelectMany(a=>a).SelectMany(l=>new[]{l.StartAt,l.EndAt})));
        var letters=ledger.Events.Select(e=>e.Message).ToHashSet(StringComparer.Ordinal);
        foreach(var m in w.Messaging.Messages.Where(m=>letters.Contains(m.Id))){var n=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(m.Body));if(n.Engagement is {} e){times.Add(e.EffectiveFrom);times.Add(e.Until);}}
        times.AddRange((w.FactionStances?.Decisions??[]).SelectMany(d=>new[]{d.At,d.Input.EffectiveAt,d.Input.Until}));
        times.AddRange(NpcArmies.Musters(w).Select(m=>m.At));times.AddRange((w.NpcArmies?.Returns??[]).Select(q=>q.At));
        times.AddRange(w.Branches!.Journeys.SelectMany(j=>j.Legs.SelectMany(l=>new[]{l.StartAt,l.EndAt})));
        times.AddRange(w.Tasks.SelectMany(t=>new[]{t.StartedAt??-1,t.FinishedAt??-1}));
        times.AddRange(w.Memberships.Concat(w.ServiceBonds).Concat(w.Offices).Concat(w.LandRights).SelectMany(t=>new[]{t.From,t.Until}));
        times.AddRange(w.Grants.SelectMany(t=>new[]{t.From,t.Until}));
        return times.Where(t=>t>=from&&t<=until).Distinct().Order();
    }
    static (long From,long Until)? NearWindow(FieldPoint a,FieldPoint b,FieldPoint c,FieldPoint d,long from,long until)
    {
        if(until-from<2)return null;long lo=from+1,hi=until-1;
        // Conservative fallback outside normal historical coordinate/time scales.
        if(from>1000000000000L||new[]{a.X,a.Y,b.X,b.Y,c.X,c.Y,d.X,d.Y}.Any(v=>System.Numerics.BigInteger.Abs(v)>100000000000000L))return(lo,hi);
        double ax=(double)a.X-c.X,ay=(double)a.Y-c.Y,dx=((double)b.X-d.X)-ax,dy=((double)b.Y-d.Y)-ay;
        double radius=ContactCombat.RadiusMm+4,aa=dx*dx+dy*dy,cc=ax*ax+ay*ay-radius*radius;
        if(aa==0)return cc<=0?(lo,hi):null;
        // Closest-point form avoids catastrophic cancellation of b²-4ac on long roads.
        double closest=-(ax*dx+ay*dy)/aa,perpendicular=(ax*dy-ay*dx)/Math.Sqrt(aa),remaining=radius*radius-perpendicular*perpendicular;if(remaining<0)return null;
        double half=Math.Sqrt(Math.Max(0,remaining/aa)),enter=closest-half,exit=closest+half;if(exit<0||enter>1)return null;
        long duration=until-from;long start=from+(long)Math.Clamp(Math.Floor(Math.Clamp(enter,0,1)*duration)-2,0,duration),end=from+(long)Math.Clamp(Math.Ceiling(Math.Clamp(exit,0,1)*duration)+2,0,duration);
        return Math.Max(lo,start)<=Math.Min(hi,end)?(Math.Max(lo,start),Math.Min(hi,end)):null;
    }
    static IEnumerable<Window> Windows(ContractWorld w,NewMilitaryState ledger,SupplySortie sortie)
    {
        long from=sortie.From,until=sortie.Until??w.ElapsedSeconds;if(until<=from)yield break;
        var opponents=NpcArmies.Musters(w).Where(m=>m.At<=until&&m.Owner!=ledger.Terms.Owner&&PotentialHostility(w,ledger.Terms.Owner,m.Owner,from,until)).ToArray();if(opponents.Length==0)yield break;
        var context=NewGameMilitary.WorldEngagementContext(w,ledger.Terms.Army);var times=Changes(w,ledger,from,until).ToArray();
        for(int n=1;n<times.Length;n++){
            long start=times[n-1],end=times[n];var ranges=new List<(long From,long Until)>();
            foreach(var m in opponents.Where(m=>m.At<=end)){
                var c=FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,m.Commander,start));var d=FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,m.Commander,end));
                for(int i=0;i<3;i++)if(NearWindow(NewGameMilitary.Position(context,i,start),NewGameMilitary.Position(context,i,end),c,d,start,end) is {} window)ranges.Add(window);
            }
            long? first=null;long last=0;foreach(var range in ranges.OrderBy(v=>v.From).ThenBy(v=>v.Until)){
                if(first==null){first=range.From;last=range.Until;continue;}if(range.From<=last+1){last=Math.Max(last,range.Until);continue;}
                yield return new(ledger.Terms.Army,first.Value,last,end);first=range.From;last=range.Until;
            }
            if(first!=null)yield return new(ledger.Terms.Army,first.Value,last,end);
            // Endpoint inputs may differ from the open interval; always inspect them separately.
            yield return new(ledger.Terms.Army,end,end,end);
        }
    }
    static bool NoPrimaryAttack(WorldEngagementSnapshot snapshot)=>FieldEngagement.Resolve(snapshot.At,snapshot.Units.Select(u=>u with{Position=new(0,0),Height=0}).ToArray(),snapshot.Mandates,snapshot.Hostilities).Strikes.Length==0;
    static void ValidateSparse(ContractWorld w,ContractWorld? trustedPrior)
    {
        var s=w.WorldClashes!;var sorties=NewGameMilitary.LedgerStates(w).SelectMany(l=>l.Sorties.Select(q=>(Ledger:l,Sortie:q))).OrderBy(q=>q.Sortie.From).ToArray();
        for(int n=0;n<s.Steps.Length;n++){var step=s.Steps[n];Need(step.At==step.From+1&&step.At<=w.ElapsedSeconds&&step.Revision>0&&step.Revision<=w.CommitSequence&&step.Result.At==step.At&&step.Result.Strikes.Length>0&&step.Result.Losses.Length>0&&(n==0||step.At>s.Steps[n-1].At&&step.Revision>s.Steps[n-1].Revision)&&sorties.Any(q=>q.Ledger.Terms.Army==step.Army&&q.Sortie.From<=step.From&&step.At<=(q.Sortie.Until??w.ElapsedSeconds)),"疎な接触履歴の軍・時刻・順序が不正です");CheckRevision(w,step);}
        if(trustedPrior?.WorldClashes is {Contract:SparsePolicy} old){
            Need(s.Steps.Length>=old.Steps.Length,"実損害履歴を削除できません");for(int n=0;n<old.Steps.Length;n++)Need(ContractJson.CanonicalHash(old.Steps[n])==ContractJson.CanonicalHash(s.Steps[n]),"過去の実損害を書き換えられません");
            if(w.ElapsedSeconds==trustedPrior.ElapsedSeconds||!NewGameMilitary.Deployed(trustedPrior)){Need(s.Steps.Length==old.Steps.Length,"時計を進めず損害を適用できません");return;}
            Need(w.ElapsedSeconds==trustedPrior.ElapsedSeconds+1,"接触は共通軍務の毎秒境界を使います");
            var candidate=new WorldClashStep(trustedPrior.NewMilitary!.Terms.Army,trustedPrior.ElapsedSeconds,w.ElapsedSeconds,w.CommitSequence,new(w.ElapsedSeconds,[],[]));
            var context=Before(w,candidate,old.Steps.Length);var result=WorldEngagement.Resolve(context);bool hit=result.Strikes.Length>0;Need(s.Steps.Length==old.Steps.Length+(hit?1:0),"現在秒の実損害が欠落・重複しています");
            if(hit)Need(s.Steps[^1].Army==candidate.Army&&s.Steps[^1].At==candidate.At&&s.Steps[^1].Revision==w.CommitSequence&&ContractJson.CanonicalHash(s.Steps[^1].Result)==ContractJson.CanonicalHash(result),"現在秒の損害を再現できません");return;
        }
        int index=0;
        foreach(var entry in sorties)foreach(var window in Windows(w,entry.Ledger,entry.Sortie)){
            long at=window.From;while(true){Need(index>=s.Steps.Length||s.Steps[index].At>=at,"接触不能な時刻へ損害を追加しています");
                bool recorded=index<s.Steps.Length&&s.Steps[index].At==at;long revision=recorded?s.Steps[index].Revision:ReplayRevision(w,at);
                var candidate=new WorldClashStep(window.Army,at-1,at,revision,new(at,[],[]));var context=Before(w,candidate,index);var snapshot=WorldEngagement.Snapshot(context);var result=FieldEngagement.Resolve(at,snapshot.Units,snapshot.Mandates,snapshot.Hostilities);
                Need(recorded==(result.Strikes.Length>0),"移動・受領原本から必要な実損害が欠落・追加されています");
                if(recorded){Need(s.Steps[index].Army==window.Army&&ContractJson.CanonicalHash(s.Steps[index].Result)==ContractJson.CanonicalHash(result),"元の時点の実接触を再現できません");index++;}
                if(at==window.Until||!recorded&&NoPrimaryAttack(snapshot))break;at++;
            }
        }
        Need(index==s.Steps.Length,"移動と接触条件で裏付けられない損害があります");
    }
}
