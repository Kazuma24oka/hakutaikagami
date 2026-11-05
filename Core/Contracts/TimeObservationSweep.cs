namespace HakutaiKagami.Core.Contracts;

public static class TimeObservationSweep
{
    // 区間の両端は既存の道路の折れ点・出航待ち境界。外へ出て戻る折れ線を直線扱いしない。
    public static long? FirstCandidate(RoutePoint own0,RoutePoint own1,RoutePoint other0,RoutePoint other1,long seconds)
    {
        if(seconds<=0)return null;
        double x0=other0.EastMm-own0.EastMm,y0=other0.NorthMm-own0.NorthMm;
        if(Math.Abs(x0)<=80000&&Math.Abs(y0)<=80000)return null;
        double lo=0,hi=1;
        bool Axis(double p,double q){double d=q-p;if(d==0)return Math.Abs(p)<=80002;double a=(-80002-p)/d,b=(80002-p)/d;if(a>b)(a,b)=(b,a);lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;}
        if(!Axis(x0,other1.EastMm-own1.EastMm)||!Axis(y0,other1.NorthMm-own1.NorthMm)||lo>1||hi<0)return null;
        // 整数位置の丸め誤差を考慮して候補の一秒手前で観測する。UIには候補を出さない。
        return Math.Clamp((long)Math.Floor(Math.Max(0,lo)*seconds),1,seconds);
    }
    internal static long Boundary(ContractWorld w,long target)
    {
        if(!WorldTimeControl.Enabled(w)||!NpcArmies.Enabled(w)||target<=w.ElapsedSeconds+1)return target;
        var start=RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds);
        var end=RegionalCampaign.ActorPointAt(w,w.PlayerControl,target);
        long best=target;
        foreach(var army in NpcArmies.Musters(w).Where(m=>NpcArmies.CommanderBusyAt(w,m.Commander,w.ElapsedSeconds,w.CommitSequence))){
            var a=RegionalCampaign.RoadActorPointAt(w,army.Commander,w.ElapsedSeconds);
            var b=RegionalCampaign.RoadActorPointAt(w,army.Commander,target);
            if(FirstCandidate(start,end,a,b,target-w.ElapsedSeconds) is {} delta)best=Math.Min(best,w.ElapsedSeconds+delta);
        }
        return best;
    }
}
