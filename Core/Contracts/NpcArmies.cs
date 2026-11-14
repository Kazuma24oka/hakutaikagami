namespace HakutaiKagami.Core.Contracts;

public sealed record NpcArmySight(EntityId Army,EntityId Commander,EntityId Owner,RoutePoint Position,RoutePoint Next,int Soldiers,bool Attacking);
public sealed record NpcArmyObjective(EntityId TargetOwner,EntityId TargetSite,FactionStanceClaim Claim,int HoldSeconds);
public sealed record NpcArmyMuster(EntityId Id,EntityId Commander,EntityId Owner,EntityId Site,long At,long Revision,FactionStanceAuthority Authority,NpcArmyObjective? Objective=null)
{
    // 家臣が大将のとき、出陣を決めた当主（権限と既知の敵対意思は当主のもの）。当主自身が率いるときは持たない。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Lord {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] public EntityId Issuer=>Lord??Commander;
}
public sealed record NpcArmyMarch(EntityId Muster,Intent Input,long At,long Revision);
public sealed record NpcArmyReturn(EntityId Muster,long At,long Revision,long Consumed);
public sealed record NpcArmyState(int Schema,string Contract,NpcArmyMuster[] Musters,NpcArmyMarch[] Marches,NpcArmyReturn[] Returns);

// Actual garrison custody. No default wars, generated enemies, castle capture, or shared knowledge.
public static partial class NpcArmies
{
    public const string CompactCombatPolicy="head-led-owned-garrison-sparse-local-combat-v3";
    public const string CombatPolicy="head-led-owned-garrison-local-combat-v2";
    public static bool Supported(string? policy)=>policy is Policy or CombatPolicy or CompactCombatPolicy;
    public const string Policy="head-led-owned-garrison-common-land-travel-v1",LandRoute="npc-army-land-only-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool b,string why){if(!b)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>(NewGameWorld.Enabled(w)&&Supported(w.NewGame?.WorldArmyPolicy))||NationalSystems.On(w,"NpcArmies");
    public static NpcArmyMuster[] Musters(ContractWorld w)=>w.NpcArmies?.Musters??[];
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NpcArmyMuster,MilitaryLedgerIdentity> bindings=new();
    static MilitaryLedgerIdentity Binding(NpcArmyMuster m)=>bindings.GetValue(m,q=>NewGameMilitary.LedgerIdentity(q.Owner,q.Site));
    public static bool HoldsGarrison(ContractWorld w,EntityId owner,EntityId site)=>Musters(w).Any(m=>m.Owner==owner&&m.Site==site);
    public static ArmyEntry[] Armies(ContractWorld w)=>Musters(w).Select(m=>new ArmyEntry(Binding(m).Army,m.Owner,m.Commander)).ToArray();
    static void CheckMuster(ContractWorld w,NpcArmyMuster m,IEnumerable<NpcArmyMuster> prior)
    {
        Need(Enabled(w)&&m.Commander!=ControlSuccession.PlayerAtRevision(w,m.Revision)&&m.At>=0&&m.At<=w.ElapsedSeconds&&m.Revision>0&&m.Revision<=w.CommitSequence&&EntityId.IsValid(m.Id.Value),"NPC軍の版・本人・時点が不正です");
        Need(MilitaryEconomy.CanMuster(w,m.Site,m.At,m.Revision),"現地の支援人手または家の自由米が不足しています");
        Need(!PrivateStanddown.ShouldWait(w,m.Commander,m.At,m.Revision),"本人の新規軍引受は見合わせ期間中です");Need(!prior.Any(p=>p.Id==m.Id||p.Commander==m.Commander||p.Owner==m.Owner&&p.Site==m.Site),"同じ駐屯兵または指揮官を二重に割り当てられません");
        var authority=FactionStances.ResolveAuthority(w,m.Issuer,m.Owner,m.At,m.Revision);
        if(m.Lord is {} lord)Need(RetainerCommand(w)&&lord!=m.Commander&&CanLead(w,m.Commander,lord,m.Owner,m.Site,m.At,m.Revision)&&TransportWork.At(w,lord,m.Site,m.At,m.Revision,true),"当主が城で直属の家臣に大将を命じてください");
        Need(authority!=null&&authority==m.Authority&&authority.Home==m.Site,"当主・所属・拠点支配の根拠がありません");
        var site=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==m.Site).Position;
        Need(RegionalCampaign.RoadActorPointAt(w,m.Commander,m.At)==site&&!w.Requests.Any(r=>r.Intent.Executor==m.Commander&&r.CreatedAt<=m.At&&r.CommitSequence<m.Revision&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>m.At))),"現地で他の用件を終えてから駐屯兵を引き受けます");
        Need(!NewGameMilitary.LedgerStates(w).Any(s=>s.Terms.Owner==m.Owner&&s.Terms.Site==m.Site),"貸与履歴がある駐屯兵の再割当ては未対応です");
        if(m.Objective is {} o){
            var f=w.NewGame!.Start.Factions.Single(f=>f.Id==o.TargetOwner.Value);
            Need(o.HoldSeconds==30&&(o.TargetSite==new EntityId(f.Home)||NationalStrategy.RealTarget(w,o.Claim.Declaration)==o.TargetSite)&&o.Claim==FactionStances.ClaimAt(w,m.Owner,o.TargetOwner,m.At,m.Revision)&&FactionStances.KnownOwnDeclarations(w,m.Issuer).Any(k=>k.Declaration==o.Claim.Declaration),"出撃対象は当主自身の既知の敵対意思と拠点に限ります");
        }
        Need(!FactionStances.OtherIdUsed(w,m.Id,false)&&!FactionStances.OwnIdUsed(w,m.Id),"別の操作で使ったIDです");
    }
    // Domain mutation for the authoritative NPC controller, never a player command to another head.
    public static void Muster(ContractWorld w,EntityId id,EntityId commander,EntityId owner,NpcArmyObjective? objective=null,EntityId? lord=null)
    {
        var a=FactionStances.ResolveAuthority(w,lord??commander,owner,w.ElapsedSeconds,w.CommitSequence);
        Need(a!=null,"当主権限がありません");var m=new NpcArmyMuster(id,commander,owner,a.Home,w.ElapsedSeconds,w.CommitSequence,a,objective){Lord=lord};
        Need(w.Locations.Any(l=>l.Actor==commander&&l.Kind==LocationKind.AtSite&&l.Site==a.Home),"現地で引き受けてください");CheckMuster(w,m,Musters(w));w.NpcArmies=new(1,Policy,[..Musters(w),m],w.NpcArmies?.Marches??[],w.NpcArmies?.Returns??[]);MilitaryEconomy.FundNpc(w,m);if(MilitaryEconomy.Enabled(w)){CommunityEconomy.Tick(w);MaterialMarkets.Tick(w);}Sync(w);
    }
    internal static long CarryCapacity(ContractWorld w,NpcArmyMuster m)=>TreatyDefense.CarryCapacity(w,m);
    internal static bool ReturnedAt(ContractWorld w,NpcArmyMuster m,long at,long rev)=>w.NpcArmies?.Returns.Any(q=>q.Muster==m.Id&&(q.At<at||q.At==at&&q.Revision<=rev))==true;
    public static bool CommanderBusyAt(ContractWorld w,EntityId actor,long at,long revision)=>Musters(w).Any(m=>m.Commander==actor&&(m.At<at||m.At==at&&m.Revision<=revision)&&!ReturnedAt(w,m,at,revision));
    public static bool IsMarch(ContractWorld w,Intent i)=>i.RoutePolicy==LandRoute&&w.NpcArmies?.Marches.Any(m=>m.Input==i)==true;
    public static Intent Move(ContractWorld w,EntityId muster,string id,string destination)
    {
        var m=Musters(w).Single(m=>m.Id==muster);Need(!ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence),"帰還後の再出陣は未対応です");
        return TravelWorld.ActorMove(w,id,m.Commander,destination,true,LandRoute);
    }
    internal static void RecordMarch(ContractWorld w,Intent i)
    {
        if(i.RoutePolicy!=LandRoute)return;
        var m=Musters(w).Single(m=>m.Commander==i.Actor);
        w.NpcArmies=w.NpcArmies! with{Marches=[..w.NpcArmies.Marches,new(m.Id,i,w.ElapsedSeconds,w.CommitSequence)]};
    }
    internal static void ValidatePendingMarch(ContractWorld w,Intent i)
    {
        Need(!Musters(w).Any(m=>m.Commander==i.Actor&&ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)),"帰還済みです");
        Need(Enabled(w)&&i.Actor!=w.PlayerControl&&Musters(w).Any(m=>m.Commander==i.Actor),"NPC軍の引受原本がありません");
        Need(i==TravelWorld.ActorMove(w,i.RequestId.Value,i.Actor,RegionalCampaign.Destination(w,i.DestinationSite??throw new InvalidDataException("目的地がありません")),true,LandRoute),"NPC陸軍の移動条件が違います");
    }
    public static RoutePoint Position(ContractWorld w,EntityId army,long at)
    {
        Need(at>=0&&at<=w.ElapsedSeconds,"未来の軍位置は参照しません");
        var m=Musters(w).Single(m=>Binding(m).Army==army&&m.At<=at);return RegionalCampaign.RoadActorPointAt(w,m.Commander,at);
    }
    // Consume actual person-seconds from first departure until explicit physical return.
    // No new sortie or automatic replenishment is created by loading this history.
    public static long Consumed(ContractWorld w,NpcArmyMuster m)
    {
        if(MilitaryEconomy.Enabled(w))return ConsumedAt(w,m,w.ElapsedSeconds,w.CommitSequence+1);
        var starts=(w.NpcArmies?.Marches??[]).Where(q=>q.Muster==m.Id).Select(q=>w.Tasks.Single(t=>t.RequestId==q.Input.RequestId).StartedAt).Where(t=>t!=null).Select(t=>t!.Value).ToArray();
        var end=w.NpcArmies?.Returns.SingleOrDefault(q=>q.Muster==m.Id)?.At??w.ElapsedSeconds;return starts.Length==0?0:Math.Min(CarryCapacity(w,m),checked(100*(end-starts.Min())-WorldClashes.Losses(w,end).Where(l=>Binding(m).Units.Contains(l.Unit)&&l.At>=starts.Min()).Sum(l=>checked((end-l.At)*l.Soldiers))));
    }
    static ResourceLot[] Lots(ContractWorld w,NpcArmyMuster m)
    {
        var b=Binding(m);var returned=ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence);return [new(b.Depot,"rice",m.Owner,m.Owner,m.Site,FieldSupply.Unit,MilitaryEconomy.InitialStock(w)+MilitaryEconomy.Credit(w,b.Depot,w.ElapsedSeconds,w.CommitSequence+1)-(returned?Consumed(w,m):CarryCapacity(w,m)),MilitaryEconomy.InitialStock(w),0),new(b.Carry,"rice",m.Owner,m.Owner,m.Site,FieldSupply.Unit,returned?0:CarryCapacity(w,m)-Consumed(w,m),0,0,returned?null:m.Commander)];
    }
    public static long Net(ContractWorld w,EntityId lot)
    {
        foreach(var m in Musters(w)){var b=Binding(m);if(lot==b.Depot)return -(ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)?Consumed(w,m):CarryCapacity(w,m));if(lot==b.Carry)return ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)?0:CarryCapacity(w,m)-Consumed(w,m);}return 0;
    }
    public static bool IsLot(ContractWorld w,ResourceLot l)=>Musters(w).Any(m=>l.Id==Binding(m).Depot||l.Id==Binding(m).Carry);
    public static bool AllowsMobileLot(ContractWorld w,ResourceLot l)=>Musters(w).Any(m=>l.Id==Binding(m).Carry&&l.MobileHolder==m.Commander);
    internal static void Sync(ContractWorld w)
    {
        var ms=Musters(w);if(ms.Length==0)return;var ids=ms.SelectMany(m=>Binding(m).Units).ToHashSet();
        w.Units=[..w.Units.Where(u=>!ids.Contains(u.Id)),..ms.SelectMany(m=>Binding(m).Units.Select((id,n)=>new UnitState(id,Binding(m).Army,m.Commander,m.Owner,(n==0?40:30)-WorldClashes.SoldiersLost(w,id,w.ElapsedSeconds),0)))];
        var lots=ms.SelectMany(m=>new[]{Binding(m).Depot,Binding(m).Carry}).ToHashSet();
        w.ResourceLots=[..w.ResourceLots.Where(l=>!lots.Contains(l.Id)),..ms.SelectMany(m=>Lots(w,m))];
    }
    public static void Return(ContractWorld w,EntityId muster)
    {
        var m=Musters(w).Single(m=>m.Id==muster);Need(!ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence),"帰還済みです");
        var position=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==m.Site).Position;
        Need(w.ElapsedSeconds>=m.At&&w.CommitSequence>m.Revision&&w.Locations.Any(l=>l.Actor==m.Commander&&l.Kind==LocationKind.AtSite&&l.Site==m.Site)&&RegionalCampaign.RoadActorPointAt(w,m.Commander,w.ElapsedSeconds)==position&&!w.Tasks.Any(t=>t.Executor==m.Commander&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Failed or ExecutionPhase.Stopped)),"本人と軍が現地へ帰還し、移動任務を終えてから返納します");
        var used=Consumed(w,m);w.NpcArmies=w.NpcArmies! with{Returns=[..w.NpcArmies.Returns,new(muster,w.ElapsedSeconds,w.CommitSequence,used)]};MilitaryEconomy.RefundNpc(w,m);if(MilitaryEconomy.Enabled(w)){CommunityEconomy.Tick(w);MaterialMarkets.Tick(w);}Sync(w);
    }
    static string Key(EntityId id)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value));
    static EntityId MusterId(FactionStanceClaim claim)=>new("decision:npc-army/"+Key(claim.Declaration));
    static string MarchId(NpcArmyMuster m,bool home)=>"request:npc-army/"+Key(m.Id)+(home?"/home":"/out");
    static SetupFaction[] DecidingHeads(ContractWorld w){var ids=(w.FactionStances?.Decisions??[]).Where(d=>d.Input.Action==FactionStanceAction.DeclareHostile&&d.Authority.Issuer!=w.PlayerControl).Select(d=>d.Authority.Issuer.Value).ToHashSet(StringComparer.Ordinal);return ids.Count==0?[]:HouseSuccession.Factions(w).Where(f=>ids.Contains(f.Head)).ToArray();}
    internal static int BoundaryCost(ContractWorld w)=>!Enabled(w)?0:2*(DecidingHeads(w).Length+Musters(w).Count(m=>m.Objective!=null&&!ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)));
    // 家臣が軍を率いる規則（全国の開始の RetainerCommand と、捕縛の検査用の地方の版）。旧保存は当主自身が率いる。
    public static bool RetainerCommand(ContractWorld w)=>NationalSystems.On(w,"RetainerCommand")||w.NewGame?.CapturePolicy==BattleCaptivity.Policy;
    static bool Active(TimedRelation r,long at,long rev)=>r.From<=at&&(at<r.Until||at==r.Until&&rev<r.Revision);
    // 大将になれる家臣：同じ家の成人で、当主に直接仕え、城にいて、ほかの用件・軍・拘束・療養がない者。
    static bool CanLead(ContractWorld w,EntityId person,EntityId head,EntityId owner,EntityId site,long at,long rev)=>
        person!=w.PlayerControl&&person!=head&&w.Characters.Any(c=>c.Id==person&&c.Life==LifeState.Adult)&&
        w.Memberships.Any(r=>r.Subject==person&&r.Target==owner&&Active(r,at,rev))&&w.ServiceBonds.Any(r=>r.Subject==person&&r.Target==head&&Active(r,at,rev))&&
        !HouseSuccession.Factions(w,at,rev).Any(f=>f.Head==person.Value)&&TransportWork.At(w,person,site,at,rev,true)&&
        !HostageCustody.HeldAt(w,person,at,rev)&&!Assassination.DeadAt(w,person,at,rev)&&(!ClinicalHealth.Enabled(w)||PersonalHealth.Can(w,person,HealthLimit.Judgment,at))&&
        !PrivateStanddown.ShouldWait(w,person,at,rev)&&!Musters(w).Any(m=>m.Commander==person&&(m.At<at||m.At==at&&m.Revision<rev)&&!ReturnedAt(w,m,at,rev))&&
        !w.Requests.Any(r=>r.Intent.Executor==person&&r.CreatedAt<=at&&r.CommitSequence<rev&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at)));
    // 当主が城にいる直属の家臣から大将を選ぶ（人物IDの順で最初の者）。いなければ当主自身が率いる。
    internal static EntityId Leader(ContractWorld w,EntityId head,EntityId owner,EntityId site)
    {
        if(!RetainerCommand(w))return head;long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        return w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==site).Select(l=>l.Actor).OrderBy(p=>p.Value,StringComparer.Ordinal)
            .FirstOrDefault(p=>CanLead(w,p,head,owner,site,at,rev),head);
    }
    // 全国の開始（一覧の ArmyCadence）では、当主が出陣を考えるのは毎時の初めだけ（統合案 W1：判断の頻度）。道の見積もりを毎秒しない。
    public const long Cadence=3600;
    static bool Hourly(ContractWorld w)=>NationalSystems.On(w,"ArmyCadence");
    internal static NpcArmyObjective? Candidate(ContractWorld w,EntityId commander,EntityId owner)
    {
        if(Hourly(w)&&w.ElapsedSeconds%Cadence!=0)return null;
        if(PrivateStanddown.ShouldWait(w,commander,w.ElapsedSeconds,w.CommitSequence)||!Enabled(w)||commander==w.PlayerControl||Musters(w).Any(m=>m.Commander==commander||m.Lord==commander)||NewGameMilitary.LedgerStates(w).Any(s=>s.Terms.Owner==owner)||w.Tasks.Any(t=>t.Executor==commander&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)))return null;
        var a=FactionStances.ResolveAuthority(w,commander,owner,w.ElapsedSeconds,w.CommitSequence);if(a==null||!MilitaryEconomy.CanMuster(w,a.Home,w.ElapsedSeconds,w.CommitSequence+1)||!w.Locations.Any(l=>l.Actor==commander&&l.Kind==LocationKind.AtSite&&l.Site==a.Home))return null;
        foreach(var known in FactionStances.KnownOwnDeclarations(w,commander).OrderBy(k=>k.Declaration.Value,StringComparer.Ordinal)){
            var claim=FactionStances.ClaimAt(w,owner,known.ToOwner,w.ElapsedSeconds,w.CommitSequence);if(claim==null||claim.Declaration!=known.Declaration)continue;
            if(TruceNegotiations.WillNotInitiate(w,owner,claim.ToOwner,w.ElapsedSeconds,w.CommitSequence))continue;var target=NationalStrategy.RealOpen(w,claim.Declaration)?NationalStrategy.RealTarget(w,claim.Declaration)!.Value:new EntityId(w.NewGame!.Start.Factions.Single(f=>f.Id==claim.ToOwner.Value).Home); // 実際の軍勢の出陣は出陣の城へ
            try{var i=TravelWorld.ActorMove(w,"request:npc-army-preview",Leader(w,commander,owner,a.Home),NewGameWorld.Node(target.Value),true,LandRoute);if(2L*i.WorkSeconds+60<NewGameMilitary.Capacity/100&&(!WorldClashes.Enabled(w)||w.ElapsedSeconds+i.WorkSeconds+32<claim.Until))return new(claim.ToOwner,target,claim,30);}catch(InvalidDataException){}
        }return null;
    }
    // 出陣先に着いて留まり終える時刻（出陣先への行軍が終わっていなければ空）。
    internal static long? HoldEnd(ContractWorld w,NpcArmyMuster m)
    {
        if(m.Objective==null)return null;var q=w.NpcArmies!.Marches.LastOrDefault(q=>q.Muster==m.Id&&q.Input.RequestId.Value==MarchId(m,false));if(q==null)return null;
        var t=w.Tasks.Single(t=>t.RequestId==q.Input.RequestId);return t.FinishedAt is {} end&&t.Execution==ExecutionPhase.Completed?end+m.Objective.HoldSeconds:null;
    }
    // 軍が戦えない：大将が捕らえられた、兵がいない、出陣先への行軍が失敗した、出陣先で留まり終える前に帰った。
    internal static bool Broken(ContractWorld w,NpcArmyMuster m)
    {
        if(BattleCaptivity.HeldAt(w,m.Commander,w.ElapsedSeconds,w.CommitSequence+1))return true;
        var b=Binding(m);if(w.Units.Any(u=>u.Army==b.Army)&&w.Units.Where(u=>u.Army==b.Army).Sum(u=>u.Soldiers)<=0)return true;
        var q=w.NpcArmies!.Marches.LastOrDefault(q=>q.Muster==m.Id&&q.Input.RequestId.Value==MarchId(m,false));
        if(q!=null&&w.Tasks.Single(t=>t.RequestId==q.Input.RequestId) is {FinishedAt:not null} t&&t.Execution!=ExecutionPhase.Completed)return true;
        if(!ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence))return false;
        var back=w.NpcArmies.Returns.Single(r=>r.Muster==m.Id).At;return HoldEnd(w,m) is not {} hold||back<hold;
    }
    internal static bool NeedsTurn(ContractWorld w,NpcArmyMuster m)
    {
        if(CastleHandover.HoldsNpc(w,m.Id)||m.Objective==null||ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence))return false;
        // 大将が捕らえられた軍は、新しい行軍を始めない（大将を失った軍の扱いは残件）。
        if(BattleCaptivity.HeldAt(w,m.Commander,w.ElapsedSeconds,w.CommitSequence+1))return false;
        var qs=w.NpcArmies!.Marches.Where(q=>q.Muster==m.Id).ToArray();if(qs.Length==0)return true;
        var t=w.Tasks.Single(t=>t.RequestId==qs[^1].Input.RequestId);if(t.FinishedAt==null)return false;
        if(qs[^1].Input.RequestId.Value==MarchId(m,true))return w.Locations.Any(l=>l.Actor==m.Commander&&l.Kind==LocationKind.AtSite&&l.Site==m.Site);
        bool endedReactiveMission=w.FactionNotices?.Reactions.Any(r=>r.Decision.Id==m.Objective.Claim.Declaration&&r.Decision.Action==FactionStanceAction.DeclareHostile)==true&&!FactionStances.KnownOwnDeclarations(w,m.Issuer).Any(c=>c.Declaration==m.Objective.Claim.Declaration&&c.EffectiveFrom<=w.ElapsedSeconds&&w.ElapsedSeconds<c.Until);
        endedReactiveMission|=TruceNegotiations.WillNotInitiate(w,m.Owner,m.Objective.TargetOwner,w.ElapsedSeconds,w.CommitSequence);
        if(!endedReactiveMission&&t.Execution==ExecutionPhase.Completed&&w.ElapsedSeconds<t.FinishedAt+m.Objective.HoldSeconds)return false;
        if(w.Locations.Any(l=>l.Actor==m.Commander&&l.Kind==LocationKind.AtSite&&l.Site==m.Site))return true;
        try{Move(w,m.Id,MarchId(m,true),NewGameWorld.Node(m.Site.Value));return true;}catch(InvalidDataException){return false;}
    }
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var times=new List<long>();
        if(Hourly(w)){if(DecidingHeads(w).Length>0)times.Add((w.ElapsedSeconds/Cadence+1)*Cadence);}
        else foreach(var f in DecidingHeads(w))if(Candidate(w,new(f.Head),new(f.Id))!=null)times.Add(w.ElapsedSeconds+1);
        foreach(var m in Musters(w)){
            if(CastleHandover.HoldsNpc(w,m.Id))continue;
            if(NeedsTurn(w,m))times.Add(w.ElapsedSeconds+1);
            else if(m.Objective!=null&&!ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)){
                var q=w.NpcArmies!.Marches.LastOrDefault(q=>q.Muster==m.Id);var t=q==null?null:w.Tasks.Single(t=>t.RequestId==q.Input.RequestId);
                if(q?.Input.RequestId.Value==MarchId(m,false)&&t?.FinishedAt is {} end&&end+m.Objective.HoldSeconds>w.ElapsedSeconds)times.Add(end+m.Objective.HoldSeconds);
            }
        }return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.TurnSnapshot();if(!Enabled(w)||w.Paused)return;
        foreach(var f in DecidingHeads(w).OrderBy(f=>f.Id,StringComparer.Ordinal)){
            w=session.TurnSnapshot();if(Candidate(w,new(f.Head),new(f.Id)) is {} objective){var head=new EntityId(f.Head);var leader=Leader(w,head,new(f.Id),new(f.Home));session.MusterNpcArmy(MusterId(objective.Claim),leader,new(f.Id),objective,leader==head?null:head);}
        }
        foreach(var id in Musters(session.TurnSnapshot()).Select(m=>m.Id)){
            w=session.TurnSnapshot();var m=Musters(w).Single(m=>m.Id==id);if(!NeedsTurn(w,m))continue;
            var last=w.NpcArmies!.Marches.LastOrDefault(q=>q.Muster==id);
            if(last?.Input.RequestId.Value==MarchId(m,true)){if(w.Locations.Any(l=>l.Actor==m.Commander&&l.Kind==LocationKind.AtSite&&l.Site==m.Site))session.ReturnNpcArmy(id);continue;}
            bool home=last!=null;if(home&&w.Locations.Any(l=>l.Actor==m.Commander&&l.Kind==LocationKind.AtSite&&l.Site==m.Site)){session.ReturnNpcArmy(id);continue;}var input=Move(w,id,MarchId(m,home),NewGameWorld.Node((home?m.Site:m.Objective!.TargetSite).Value));session.Submit(input,session.Revision);
        }
    }
    public static NpcArmySight[] Sights(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))return [];var seen=RegionalCampaign.Nearby(w,observer).ToDictionary(v=>v.Actor);var result=new List<NpcArmySight>();
        foreach(var m in Musters(w)){
            if(!seen.TryGetValue(m.Commander,out var v)||ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence))continue;
            var b=Binding(m);int soldiers=checked((int)w.Units.Where(u=>u.Army==b.Army).Sum(u=>u.Soldiers));
            if(soldiers>0)result.Add(new(b.Army,m.Commander,m.Owner,v.Position,v.NextPosition,soldiers,b.Units.Any(id=>WorldClashes.AttacksNow(w,id))));
        }return result.OrderBy(v=>v.Army.Value,StringComparer.Ordinal).ToArray();
    }
    public static void Validate(ContractWorld w,ContractWorld? trusted=null)
    {
        Need(Enabled(w)==(w.NpcArmies!=null),"NPC軍の明示版と世界状態が一致しません");if(w.NpcArmies is not {} s)return;
        var t=trusted?.NpcArmies;int ms=ContractValidation.TrustedPrefix(t?.Musters,s.Musters),qs=ms==s.Musters.Length?ContractValidation.TrustedPrefix(t?.Marches,s.Marches):0,rs=ms==s.Musters.Length?ContractValidation.TrustedPrefix(t?.Returns,s.Returns):0;
        Need(s.Schema==1&&s.Contract==Policy,"未知のNPC軍状態です");
        for(int n=ms;n<s.Musters.Length;n++)CheckMuster(w,s.Musters[n],s.Musters.Take(n));
        Need(s.Marches.Select(q=>q.Input.RequestId).Distinct().Count()==s.Marches.Length,"NPC軍の移動原本が重複しています");
        foreach(var q in s.Marches.Skip(qs)){var m=s.Musters.Single(m=>m.Id==q.Muster);var r=w.Requests.Single(r=>r.Id==q.Input.RequestId);
            Need(r.Intent==q.Input&&r.CreatedAt==q.At&&r.CommitSequence==q.Revision&&q.At>=m.At&&q.Revision>m.Revision&&q.Input.Actor==m.Commander&&q.Input.Executor==m.Commander&&q.Input.Action==ActionKind.Move&&q.Input.RoutePolicy==LandRoute,"NPC軍の引受・移動原本と共通任務が一致しません");
            Need(w.Branches!.Journeys.Where(j=>j.Actor==m.Commander&&j.Task==w.Tasks.Single(t=>t.RequestId==r.Id).Id).All(j=>j.Legs.All(l=>!NationalRoutes.Sea(l.Edge))),"NPC陸軍は海路を通りません");
        }
        Need(s.Returns.Select(q=>q.Muster).Distinct().Count()==s.Returns.Length,"帰還の二重計上です");
        foreach(var q in s.Returns.Skip(rs)){var m=s.Musters.Single(m=>m.Id==q.Muster);var site=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==m.Site).Position;
            Need(q.At>=m.At&&q.At<=w.ElapsedSeconds&&q.Revision>m.Revision&&q.Revision<=w.CommitSequence&&q.Consumed==Consumed(w,m)&&RegionalCampaign.RoadActorPointAt(w,m.Commander,q.At)==site&&!w.Requests.Any(r=>r.Intent.Executor==m.Commander&&r.CommitSequence<q.Revision&&r.CreatedAt<=q.At&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>q.At))),"現地帰還と兵糧返納の根拠が一致しません");
            Need(!s.Marches.Any(march=>march.Muster==m.Id&&march.Revision>=q.Revision),"帰還済みの軍を再生成できません");
        }
        if(s.Musters.Length==0)return; // Syncもこの場合は何も変えないため、世界全体の複写と自己比較を省く。
        var copy=ContractJson.Copy(w);Sync(copy);Need(ContractJson.CanonicalHash(copy.Units)==ContractJson.CanonicalHash(w.Units)&&ContractJson.CanonicalHash(copy.ResourceLots.OrderBy(l=>l.Id.Value,StringComparer.Ordinal).ToArray())==ContractJson.CanonicalHash(w.ResourceLots.OrderBy(l=>l.Id.Value,StringComparer.Ordinal).ToArray()),"NPC軍の兵数・兵糧が引受と消費原本に一致しません");
    }
}
