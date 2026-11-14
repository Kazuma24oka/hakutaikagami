namespace HakutaiKagami.Core.Contracts;

// 本人の軍の部隊を家臣に任せる（全国の開始の一覧の PlayerUnitLeaders、2026-11-15）。
// 出陣のときに、同じ城にいる自分の家の成人の家臣を2人まで選び、2番目・3番目の部隊を率いさせる。率いる間は軍と一緒に野にいて、ほかの用をしない。
// 入城すると本人と同じ城に戻る。部隊が NPC の軍の打撃で崩れると、率いる家臣は本人と同じ判定で捕らえられることがある。
public static partial class NewGameMilitary
{
    // 全国の開始と、捕縛の検査用の地方の版（出陣で家臣を選んだときだけ働く）。
    public static bool UnitLeaderRule(ContractWorld w)=>NationalSystems.On(w,"PlayerUnitLeaders")||w.NewGame?.CapturePolicy==BattleCaptivity.Policy;
    static bool BeforeQ(NewMilitaryRequest q,long at,long rev)=>q.At<at||q.At==at&&q.Revision<rev;
    // その時点の最後の出陣（入城していなければ）。
    static NewMilitaryRequest? ActiveDeploy(ContractWorld w,long at,long rev)
    {
        if(w.NewMilitary is not {} s)return null;NewMilitaryRequest? last=null;
        foreach(var q in s.Requests)if(q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter&&BeforeQ(q,at,rev))last=q;
        return last?.Input.Action==NewMilitaryAction.Deploy?last:null;
    }
    // 今、部隊を率いている家臣（捕らえられた・亡くなった者を除く）。
    public static EntityId[] UnitLeadersAt(ContractWorld w,long at,long rev)
    {
        if(!UnitLeaderRule(w)||ActiveDeploy(w,at,rev) is not {} d||d.Input.Leaders is not {Length:>0} ls)return [];
        return ls.Where(p=>!BattleCaptivity.HeldAt(w,p,at,rev)&&!Assassination.DeadAt(w,p,at,rev-1)&&!(BattleCaptivity.Enabled(w)&&BattleCaptivity.AllCaptures(w).Any(c=>c.Person==p&&c.At>=d.At&&(c.At<at||c.At==at)))).ToArray();
    }
    internal static bool LeadsUnitAt(ContractWorld w,EntityId person,long at,long rev)=>w.NewMilitary!=null&&UnitLeaderRule(w)&&person!=w.PlayerControl&&UnitLeadersAt(w,at,rev).Contains(person);
    // 部隊を率いた後、入城した城（その後に別の旅をしていなければ、ここにいる）。
    internal static (EntityId Site,long At)? LeaderReturn(ContractWorld w,EntityId person,long at,long rev)
    {
        if(w.NewMilitary is not {} s||!UnitLeaderRule(w))return null;NewMilitaryRequest? deploy=null;(EntityId,long)? back=null;
        foreach(var q in s.Requests)
        {
            if(!BeforeQ(q,at,rev))continue;
            if(q.Input.Action==NewMilitaryAction.Deploy)deploy=q.Input.Leaders?.Contains(person)==true?q:null;
            else if(q.Input.Action==NewMilitaryAction.Enter&&deploy!=null){back=(ArmyRoadMarch.AssemblySite(w,q.At)??s.Terms.Site,q.At);deploy=null;}
        }
        return back;
    }
    // 出陣に連れて行ける家臣：本人の家の成人で、本人と同じ城にいて、ほかの用をしていない者。
    public static EntityId[] UnitLeaderCandidates(ContractWorld w)
    {
        if(!UnitLeaderRule(w)||w.NewMilitary is not {} s)return [];long at=w.ElapsedSeconds,rev=w.CommitSequence;
        var here=PersonalCompanions.PhysicalSiteAt(w,w.PlayerControl,at,rev+1);if(here==null)return [];
        return w.Memberships.Where(m=>m.Target==s.Terms.Owner&&m.From<=at&&at<m.Until).Select(m=>m.Subject).Distinct().OrderBy(p=>p.Value,StringComparer.Ordinal)
            .Where(p=>p!=HouseSuccession.Head(w,s.Terms.Owner,at,rev)&&CanLeadUnit(w,p,here.Value,at,rev)).ToArray();
    }
    static bool CanLeadUnit(ContractWorld w,EntityId p,EntityId site,long at,long rev)=>p!=w.PlayerControl&&w.Characters.Any(c=>c.Id==p&&c.Life==LifeState.Adult)&&!Assassination.DeadAt(w,p,at,rev)&&
        !BattleCaptivity.HeldAt(w,p,at,rev+1)&&!HostageCustody.HeldAt(w,p,at,rev+1)&&PersonalCompanions.PhysicalSiteAt(w,p,at,rev+1)==site&&!PersonalCompanions.Occupied(w,p,at,rev)&&!NpcArmies.CommanderBusyAt(w,p,at,rev)&&
        !w.Tasks.Any(t=>t.Executor==p&&t.Execution is ExecutionPhase.Running or ExecutionPhase.Waiting);
    static void ValidateLeaders(ContractWorld w,NewMilitaryInput input)
    {
        if(input.Leaders==null)return;
        Need(input.Action==NewMilitaryAction.Deploy&&UnitLeaderRule(w)&&input.Leaders.Length is >=1 and <=2&&input.Leaders.Distinct().Count()==input.Leaders.Length,"部隊を任せる家臣は出陣のときに2人まで選びます");
        var ok=UnitLeaderCandidates(w);Need(input.Leaders.All(ok.Contains),"同じ城にいる自分の家の家臣を選んでください");
    }
    // 部隊の大将：2番目・3番目の部隊は任せた家臣、ほかは本人（または主君）。
    static EntityId UnitCommander(ContractWorld w,int unit,EntityId commander,bool custody)
    {
        if(!custody||unit==0||!UnitLeaderRule(w))return commander;var ls=UnitLeadersAt(w,w.ElapsedSeconds,w.CommitSequence+1);
        return unit<=ls.Length?ls[unit-1]:commander;
    }
}
