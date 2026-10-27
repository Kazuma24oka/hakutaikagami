namespace HakutaiKagami.Core.Contracts;
public sealed record MilitaryRiceInput(EntityId Id,EntityId Depot,long RiceKg,bool Returning=false,EntityId? Muster=null);
public sealed record MilitaryRiceEntry(MilitaryRiceInput Input,EntityId Actor,EntityId Owner,EntityId Site,long At,long Revision);
public sealed record MilitaryEconomyState(int Schema,string Policy,MilitaryRiceEntry[] Entries);
public sealed record MilitaryDepotRef(EntityId Depot,EntityId Owner,EntityId Site,EntityId Principal,NewMilitaryState? Player,NpcArmyMuster? Npc);
public sealed record MilitaryLaborWindow(EntityId Site,long From,long FromRevision,long? Until,long? UntilRevision);
public sealed record MilitaryEconomyView(EntityId Site,EntityId Depot,long HouseFreeKg,long DepotPersonSeconds,int SupportWorkers,bool CanFund,bool CanRefund);
public static class MilitaryEconomy
{
    public const string Policy="regional-support-labor-physical-house-rice-military-depots-v12";
    public const int SupportWorkers=20;
    public const long PersonSecondsPerKg=144000,NpcRiceKg=NewGameMilitary.Capacity/PersonSecondsPerKg;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy;
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<rev;
    internal static MilitaryRiceEntry[] Transfers(ContractWorld w,long at,long rev)=>w.MilitaryEconomy?.Entries.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.MilitaryEconomy?.Entries.Any(e=>e.Input.Id==id)==true;
    internal static long InitialStock(ContractWorld w)=>Enabled(w)?0:NewGameMilitary.InitialStock;
    internal static long Credit(ContractWorld w,EntityId depot,long at=long.MaxValue,long rev=long.MaxValue)=>checked(Transfers(w,at,rev).Where(e=>e.Input.Depot==depot).Sum(e=>e.Input.Returning?-e.Input.RiceKg:e.Input.RiceKg)*PersonSecondsPerKg);
    public static long Net(ContractWorld w,EntityId lot)=>Enabled(w)?Credit(w,lot):0;
    internal static (long At,long Revision,long Amount)[] HouseTransfers(ContractWorld w,EntityId site,long at,long rev)=>Transfers(w,at,rev).Where(e=>e.Site==site).Select(e=>(e.At,e.Revision,e.Input.Returning?e.Input.RiceKg:-e.Input.RiceKg)).ToArray();
    static bool VisibleWindow(MilitaryLaborWindow q,long rev)=>q.FromRevision<rev;
    static long? End(MilitaryLaborWindow q,long rev)=>q.UntilRevision<rev?q.Until:null;
    internal static bool PastUnchanged(ContractWorld w,ContractWorld? prior)=>!Enabled(w)||prior!=null&&Enabled(prior)&&ContractJson.CanonicalHash(w.MilitaryEconomy)==ContractJson.CanonicalHash(prior.MilitaryEconomy)&&ContractJson.CanonicalHash(Windows(w))==ContractJson.CanonicalHash(Windows(prior));
    static MilitaryLaborWindow[] Windows(ContractWorld w)=>NewGameMilitary.SupportWindows(w).Concat(NpcArmies.SupportWindows(w)).ToArray();
    public static int LaborAt(ContractWorld w,EntityId site,long at,long rev)=>!Enabled(w)?0:checked(Windows(w).Count(q=>q.Site==site&&VisibleWindow(q,rev)&&q.From<=at&&(End(q,rev)==null||at<End(q,rev)))*SupportWorkers);
    internal static long LaborSeconds(ContractWorld w,EntityId site,long from,long until,long rev)=>!Enabled(w)?0:checked(Windows(w).Where(q=>q.Site==site&&VisibleWindow(q,rev)).Sum(q=>Math.Max(0,Math.Min(until,End(q,rev)??until)-Math.Max(from,q.From)))*SupportWorkers);
    internal static void CheckPickup(ContractWorld w,EntityId site,long at,long rev){if(!Enabled(w))return;var a=CommunityEconomy.AccountAt(w,site,at,rev);Need(a.Workforce-a.Reserved>=SupportWorkers,"地域の補給支援20人を確保できません");}
    internal static bool CanMuster(ContractWorld w,EntityId site,long at,long rev)
    {if(!Enabled(w))return true;var a=CommunityEconomy.AccountAt(w,site,at,rev);return a.Workforce-a.Reserved>=SupportWorkers&&a.HouseRice-HouseSalary.Reserved(w,site,at,rev)>=NpcRiceKg;}
    static MilitaryDepotRef Binding(ContractWorld w,EntityId depot)=>NewGameMilitary.SupplyBinding(w,depot)??NpcArmies.SupplyBinding(w,depot)??throw new InvalidDataException("元の軍用倉がありません");
    internal static long DepotAt(ContractWorld w,EntityId depot,long at,long rev,bool correlatedReturn=false)
    {var b=Binding(w,depot);return checked(InitialStock(w)+Credit(w,depot,at,rev)+(b.Player!=null?NewGameMilitary.DepotNetAt(w,b.Player,at,rev):NpcArmies.DepotNetAt(w,b.Npc!,at,rev,correlatedReturn)));}
    static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&TransportWork.At(w,actor,site,at,rev,stableCompletion:true);
    static EntityId AutoId(ContractWorld w,EntityId muster,bool returning){string root="request:military-rice/"+(returning?"return/":"fund/")+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(muster.Value));var id=new EntityId(root);int n=0;while(FactionStances.OtherIdUsed(w,id,includeMilitaryEconomy:false)||FactionStances.OwnIdUsed(w,id))id=new(root+"/n"+(++n));return id;}
    static MilitaryRiceEntry Make(ContractWorld w,MilitaryRiceInput i,EntityId actor,long at,long rev)
    {
        Need(Enabled(w)&&i.RiceKg>0&&i.RiceKg<=1000,"米は1〜1000kgの整数で指定してください");var b=Binding(w,i.Depot);Need(RegionalDomains.Owner(w,b.Site)==b.Owner&&RegionalDomains.Head(w,b.Site)==b.Principal&&FactionStances.ResolveAuthority(w,b.Principal,b.Owner,at,Math.Min(rev,w.CommitSequence))!=null,"元の家と軍用倉の処分権がありません");
        bool automatic=i.Muster!=null;
        if(automatic){Need(b.Npc!=null&&b.Npc.Id==i.Muster&&actor==b.Principal&&actor!=w.PlayerControl&&i.Id==AutoId(w,i.Muster!.Value,i.Returning),"NPC軍の根拠原本がありません");var m=b.Npc!;if(i.Returning){Need(w.NpcArmies!.Returns.Any(r=>r.Muster==m.Id&&r.At==at&&r.Revision==rev),"部隊の実帰還前には返納できません");Need(i.RiceKg==DepotAt(w,i.Depot,at,rev,true)/PersonSecondsPerKg,"NPCの未使用兵糧の整数kgと一致しません");}else Need(m.At==at&&m.Revision==rev&&i.RiceKg==NpcRiceKg,"引受と同時に既定2日分だけ用意します");}
        else Need(actor==w.PlayerControl&&b.Player!=null&&NewGameMilitary.SupplyAllowed(w,b.Player,actor,at,rev,!i.Returning),"現地の軍務許可と元の当主との受渡しを確認してください");
        Need(Local(w,actor,b.Site,at,rev)&&Local(w,b.Principal,b.Site,at,rev),"元の拠点で当事者の現地受渡しが必要です");
        long stock=DepotAt(w,i.Depot,at,rev,automatic&&i.Returning);
        if(i.Returning)Need(checked(i.RiceKg*PersonSecondsPerKg)<=stock,"軍用倉の未使用整数kgを超えています");
        else{var account=CommunityEconomy.AccountAt(w,b.Site,at,rev);Need(i.RiceKg<=account.HouseRice-HouseSalary.Reserved(w,b.Site,at,rev)&&stock+checked(i.RiceKg*PersonSecondsPerKg)<=NewGameMilitary.InitialStock,"家の自由米または軍用倉の容量が不足しています");}
        return new(i,actor,b.Owner,b.Site,at,rev);
    }
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.MilitaryEconomy=new(1,Policy,[]);}
    internal static void Apply(ContractWorld w,MilitaryRiceInput i,EntityId actor){var e=Make(w,i,actor,w.ElapsedSeconds,w.CommitSequence);w.MilitaryEconomy=w.MilitaryEconomy! with{Entries=[..w.MilitaryEconomy!.Entries,e]};}
    internal static void FundNpc(ContractWorld w,NpcArmyMuster m){if(Enabled(w))Apply(w,new(AutoId(w,m.Id,false),NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Depot,NpcRiceKg,false,m.Id),m.Commander);}
    internal static void RefundNpc(ContractWorld w,NpcArmyMuster m){if(!Enabled(w))return;var depot=NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Depot;long kg=DepotAt(w,depot,w.ElapsedSeconds,w.CommitSequence,true)/PersonSecondsPerKg;if(kg>0)Apply(w,new(AutoId(w,m.Id,true),depot,kg,true,m.Id),m.Commander);}
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.MilitaryEconomy==null,"旧世界へ軍用米割当を追加できません");return;}Need(w.MilitaryEconomy is{Schema:1,Policy:Policy,Entries:not null},"軍用米の原本がありません");long at=0,rev=0;var ids=new HashSet<EntityId>();foreach(var e in w.MilitaryEconomy.Entries){Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&EntityId.IsValid(e.Input.Id.Value)&&ids.Add(e.Input.Id)&&!FactionStances.OtherIdUsed(w,e.Input.Id,includeMilitaryEconomy:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id),"軍用米のID・順序・時点が不正です");Need(e==Make(w,e.Input,e.Actor,e.At,e.Revision),"軍用米の原資・返納が当時の入力と一致しません");at=e.At;rev=e.Revision;}
        foreach(var m in NpcArmies.Musters(w)){Need(w.MilitaryEconomy.Entries.Count(e=>e.Input.Id==AutoId(w,m.Id,false))==1,"NPC携行兵糧の原資がありません");if(w.NpcArmies!.Returns.Any(q=>q.Muster==m.Id)){var r=w.NpcArmies.Returns.Single(q=>q.Muster==m.Id);long free=DepotAt(w,NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Depot,r.At,r.Revision,true);Need(free<PersonSecondsPerKg||w.MilitaryEconomy.Entries.Any(e=>e.Input.Id==AutoId(w,m.Id,true)),"NPCの返納可能な整数kgが未返納です");}}
        foreach(var site in CommunityEconomy.SitesFor(w))Need(LaborAt(w,site,w.ElapsedSeconds,w.CommitSequence+1)<=CommunityEconomy.Workers,"支援労働を過剰動員しています");
    }
    internal static MilitaryEconomyView? View(ContractWorld w)
    {if(!Enabled(w)||w.NewMilitary==null)return null;var s=w.NewMilitary;long at=w.ElapsedSeconds,rev=w.CommitSequence+1;if(!NewGameMilitary.SupplyAllowed(w,s,w.PlayerControl,at,rev,false))return null;var depot=s.Binding?.Depot??NewGameMilitary.Depot;long stock=DepotAt(w,depot,at,rev);bool fund=NewGameMilitary.SupplyAllowed(w,s,w.PlayerControl,at,rev,true);long rice=fund?CommunityEconomy.AccountAt(w,s.Terms.Site,at,rev).HouseRice-HouseSalary.Reserved(w,s.Terms.Site,at,rev):0;return new(s.Terms.Site,depot,rice,stock,LaborAt(w,s.Terms.Site,at,rev),fund,stock>=PersonSecondsPerKg);}
}
public static partial class NewGameMilitary
{
    internal static MilitaryLaborWindow[] SupportWindows(ContractWorld w)
    {var rows=new List<MilitaryLaborWindow>();foreach(var s in w.MilitaryLedgerHistory??LedgerStates(w)){var c=w.ForkMilitaryLedger();c.NewMilitary=s;c.OtherMilitaryLedgers=null;foreach(var e in s.Events.Where(e=>e.Effect==MilitaryEffect.PickedUp)){var root=EventCase(c,e);var end=s.Events.SingleOrDefault(r=>r.Effect==MilitaryEffect.Returned&&EventCase(c,r)==root);rows.Add(new(s.Terms.Site,e.At,e.Revision,end?.At,end?.Revision));}}return rows.ToArray();}
    internal static MilitaryDepotRef? SupplyBinding(ContractWorld w,EntityId depot){var s=(w.MilitaryLedgerHistory??LedgerStates(w)).SingleOrDefault(s=>(s.Binding?.Depot??Depot)==depot);return s==null?null:new(depot,s.Terms.Owner,s.Terms.Site,s.Terms.Principal,s,null);}
    internal static long DepotNetAt(ContractWorld w,NewMilitaryState s,long at,long rev)=>-s.Moves.Where(m=>s.Requests.Any(q=>q.Input.Id==m.Id&&(q.At<at||q.At==at&&q.Revision<rev))).Sum(m=>m.Loading?m.Amount:-m.Amount);
    internal static bool SupplyAllowed(ContractWorld w,NewMilitaryState s,EntityId actor,long at,long rev,bool funding)
    {var c=w.ForkMilitaryLedger();c.NewMilitary=s;c.OtherMilitaryLedgers=null;c.ElapsedSeconds=at;c.CommitSequence=Math.Min(w.CommitSequence,Math.Max(0,rev-1));bool local=actor==w.PlayerControl&&TransportWork.At(c,actor,s.Terms.Site,at,rev,stableCompletion:true)&&TransportWork.At(c,s.Terms.Principal,s.Terms.Site,at,rev,stableCompletion:true)&&!DeployedAt(c,at)&&Enumerable.Range(0,3).All(i=>Position(c,i,at)==Home(c,i));return local&&(!funding||CustodyAt(c,at,rev-1)&&MilitaryBoundAt(c,at,rev-1)&&CurrentPlacementAuthority(c));}
}
public static partial class NpcArmies
{
    internal static MilitaryLaborWindow[] SupportWindows(ContractWorld w)=>Musters(w).Select(m=>{var r=w.NpcArmies!.Returns.SingleOrDefault(r=>r.Muster==m.Id);return new MilitaryLaborWindow(m.Site,m.At,m.Revision,r?.At,r?.Revision);}).ToArray();
    internal static MilitaryDepotRef? SupplyBinding(ContractWorld w,EntityId depot){var m=Musters(w).SingleOrDefault(m=>Binding(m).Depot==depot);return m==null?null:new(depot,m.Owner,m.Site,m.Commander,null,m);}
    internal static long ConsumedAt(ContractWorld w,NpcArmyMuster m,long at,long rev)
    {var starts=w.NpcArmies!.Marches.Where(q=>q.Muster==m.Id&&(q.At<at||q.At==at&&q.Revision<rev)).Select(q=>w.Tasks.Single(t=>t.RequestId==q.Input.RequestId).StartedAt).Where(t=>t!=null&&t<=at).Select(t=>t!.Value).ToArray();if(starts.Length==0)return 0;long start=starts.Min();var returned=w.NpcArmies.Returns.SingleOrDefault(q=>q.Muster==m.Id&&(q.At<at||q.At==at&&q.Revision<rev));long end=Math.Min(at,returned?.At??at);return Math.Clamp(checked(100*(end-start)-WorldClashes.Losses(w,end).Where(l=>Binding(m).Units.Contains(l.Unit)&&l.At>=start).Sum(l=>checked((end-l.At)*l.Soldiers))),0,NewGameMilitary.Capacity);}
    internal static long DepotNetAt(ContractWorld w,NpcArmyMuster m,long at,long rev,bool includeReturn){if(m.At>at||m.At==at&&m.Revision>=rev)return 0;bool returned=w.NpcArmies!.Returns.Any(q=>q.Muster==m.Id&&(q.At<at||q.At==at&&(q.Revision<rev||includeReturn&&q.Revision==rev)));return returned?-ConsumedAt(w,m,at,includeReturn?rev+1:rev):-NewGameMilitary.Capacity;}
}
public sealed partial class WorldSession
{
    public MilitaryEconomyView? MilitaryEconomyView(){lock(gate)return MilitaryEconomy.View(state);}
    public CommandResult ManageMilitaryRice(MilitaryRiceInput input,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||input.Muster!=null||!MilitaryEconomy.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の軍用米受渡しではありません");var old=state.MilitaryEconomy!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"現地の米を読み直してください");try{Change(w=>MilitaryEconomy.Apply(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"家と軍用倉の米を実受渡ししました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
