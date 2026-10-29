namespace HakutaiKagami.Core.Contracts;
public enum CastleAction { Offer,Confirm,Gate,RequestKeep,ResolveKeep,Cancel,Observe }
public sealed record CastleInput(EntityId Id,CastleAction Action,EntityId Site,EntityId? Original=null,EntityId? RecipientOwner=null);
public sealed record CastleTerms(EntityId Id,EntityId Site,EntityId Owner,EntityId Occupier,EntityId Incumbent,EntityId Recipient,EntityId RecipientMuster,long Until,bool Agreed);
public sealed record CastleEntry(CastleInput Input,long At,long Revision,CastleTerms? Terms,long? Due,string? Outcome,bool Witness);
public sealed record CastleHandoverState(int Schema,string Policy,CastleEntry[] Entries);
public sealed record CastleJob(CastleTerms Terms,CastleEntry? Confirmed,CastleEntry? Gate,CastleEntry? Pending,CastleEntry? Resolution,CastleEntry? Occupied,CastleEntry? Cancelled,CastleEntry? Observed);
public sealed record CastleCard(EntityId Id,EntityId Site,EntityId Owner,EntityId Occupier,long Until,string Status,long WitnessAt,bool Local,int? RecipientSoldiers,int? IncumbentSoldiers,bool CanConfirm,bool CanGate,bool CanRequestKeep,bool CanCancel);
public sealed record CastleView(EntityId? Site,EntityId[] LocalRecipients,CastleCard[] Castles);
public static class CastleHandover
{
    public const string Policy="local-incumbent-consensual-gate-keep-occupation-separate-property-v19";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or CastleAdministration.Policy or HouseSuccession.Policy;
    static bool Before(CastleEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
    static CastleEntry[] Entries(ContractWorld w,long at=long.MaxValue,long rev=long.MaxValue)=>w.Castles?.Entries.Where(e=>Before(e,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Castles?.Entries.Any(e=>e.Input.Id==id)==true;
    static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>TransportWork.At(w,actor,site,at,rev,stableCompletion:true);
    static EntityId Owner(ContractWorld w,EntityId site)=>new(w.NewGame!.Start.Castles.Single(c=>c.Id==site.Value).Faction??throw new InvalidDataException("領有記録が未設定です"));
    static EntityId Head(ContractWorld w,EntityId owner,long? at=null,long? rev=null)=>HouseSuccession.Head(w,owner,at,rev);
    static ContractWorld PastLosses(ContractWorld w,long at,long rev){var c=w.ForkMilitaryObservation();if(w.WorldClashes is {} s)c.WorldClashes=s with{Steps=s.Steps.Where(s=>s.At<at||s.At==at&&s.Revision<rev).ToArray()};return c;}
    // Only explicitly registered armies are counted. Missing fortress data is not a historical zero.
    public static int Forces(ContractWorld w,EntityId owner,EntityId site,long at,long rev)
    {
        var c=PastLosses(w,at,rev);var anchor=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position);int n=0;
        foreach(var m in NpcArmies.Musters(w).Where(m=>m.Owner==owner&&(m.At<at||m.At==at&&m.Revision<rev)&&!w.NpcArmies!.Returns.Any(r=>r.Muster==m.Id&&(r.At<at||r.At==at&&r.Revision<rev)))){
            var b=NewGameMilitary.LedgerIdentity(m.Owner,m.Site);if(NpcArmies.Position(w,b.Army,at)!=FieldWorld.ToWorld(anchor))continue;n+=b.Units.Select((id,i)=>Math.Max(0,(i==0?40:30)-WorldClashes.SoldiersLost(c,id,at))).Sum();}
        foreach(var s in NewGameMilitary.LedgerStates(w).Where(s=>s.Terms.Owner==owner)){var p=c.ForkMilitaryObservation();p.NewMilitary=s;if(!NewGameMilitary.CustodyAt(p,at,rev))continue;for(int i=0;i<3;i++)if(RegionalGround.Distance(NewGameMilitary.Position(p,i,at),anchor)<=20000)n+=Math.Max(0,NewGameMilitary.StrengthAt(p,i,at));}
        return n;
    }
    public static CastleJob[] Jobs(ContractWorld w,long at=long.MaxValue,long rev=long.MaxValue)
    {
        var es=Entries(w,at,rev);return es.Where(e=>e.Input.Action==CastleAction.Offer).Select(e=>{
            var rows=es.Where(x=>x.Input.Original==e.Input.Id).ToArray();var resolution=rows.LastOrDefault(x=>x.Input.Action==CastleAction.ResolveKeep);var request=rows.LastOrDefault(x=>x.Input.Action==CastleAction.RequestKeep);return new CastleJob(e.Terms!,rows.LastOrDefault(x=>x.Input.Action==CastleAction.Confirm),rows.LastOrDefault(x=>x.Input.Action==CastleAction.Gate),request!=null&&(resolution==null||request.Revision>resolution.Revision)?request:null,resolution,rows.LastOrDefault(x=>x.Input.Action==CastleAction.ResolveKeep&&x.Outcome=="occupied"),rows.LastOrDefault(x=>x.Input.Action==CastleAction.Cancel),rows.LastOrDefault(x=>x.Witness));}).ToArray();
    }
    static string Outcome(ContractWorld w,CastleJob j,long at,long rev)=>at>j.Terms.Until?"expired":!Local(w,j.Terms.Incumbent,j.Terms.Site,at,rev)||!Local(w,j.Terms.Recipient,j.Terms.Site,at,rev)||Forces(w,j.Terms.Occupier,j.Terms.Site,at,rev)==0?"absent":Forces(w,j.Terms.Owner,j.Terms.Site,at,rev)>0?"relief-hold":"occupied";
    static EntityId ResolutionId(CastleEntry e)=>new("decision:castle-keep/"+e.Input.Id.Value.Replace(':','/'));
    static CastleEntry Prepare(ContractWorld w,CastleInput i,long at,long rev,bool automatic=false)
    {
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action),"引渡しの入力が不正です");var es=Entries(w,at,rev);var jobs=Jobs(w,at,rev);bool local=Local(w,w.PlayerControl,i.Site,at,rev);
        Need(i.Action==CastleAction.ResolveKeep||local&&!PersonalLife.Resting(w,at,rev),"当事者本人が現地にいません");
        if(i.Action==CastleAction.Offer){Need(!automatic&&i.Original==null&&i.RecipientOwner is {} recipient&&recipient!=Owner(w,i.Site),"相手と対象城を選んでください");var owner=Owner(w,i.Site);Need(FactionStances.ResolveAuthority(w,w.PlayerControl,owner,at,rev)!=null&&w.LandRights.Any(r=>r.Subject==owner&&r.Target==i.Site&&r.Kind=="control"&&r.From<=at&&at<r.Until),"本人に現地引渡しの締結権がありません");Need(!jobs.Any(j=>j.Terms.Site==i.Site&&j.Cancelled==null&&(j.Gate!=null||at<=j.Terms.Until)),"同じ城の引渡しが残っています");var head=Head(w,i.RecipientOwner!.Value,at,rev);Need(head!=w.PlayerControl&&Local(w,head,i.Site,at,rev),"相手の指揮官が現地にいません");Need(FactionStances.ResolveAuthority(w,head,i.RecipientOwner.Value,at,rev)!=null,"Recipient lacks actual head authority");var muster=NpcArmies.Musters(w).SingleOrDefault(m=>m.Commander==head&&m.Owner==i.RecipientOwner&&m.At<=at);Need(muster!=null&&Forces(w,i.RecipientOwner.Value,i.Site,at,rev)>0,"相手の実在部隊が現地にいません");bool willing=w.Characters.Single(c=>c.Id==head).WillObey;var t=new CastleTerms(i.Id,i.Site,owner,i.RecipientOwner.Value,w.PlayerControl,head,muster.Id,checked(at+86400),willing);return new(i,at,rev,t,null,null,true);}
        var job=jobs.SingleOrDefault(j=>j.Terms.Id==i.Original);Need(job!=null&&job.Terms.Site==i.Site&&i.RecipientOwner==null,"同じ引渡し原本が必要です");var j=job;Need(j.Cancelled==null,"取りやめ済みです");
        if(i.Action==CastleAction.ResolveKeep){Need(automatic&&j.Pending!=null&&i.Id==ResolutionId(j.Pending)&&at==j.Pending.Due,"予定された本丸引渡しの境界ではありません");return new(i,at,rev,null,null,Outcome(w,j,at,rev),local);}
        Need(!automatic,"自動操作の種別が違います");
        if(i.Action==CastleAction.Observe)return new(i,at,rev,null,null,j.Occupied!=null?(Forces(w,j.Terms.Occupier,i.Site,at,rev)==0?"occupation-record-no-force":Forces(w,j.Terms.Owner,i.Site,at,rev)>0?"contested":"occupied"):j.Resolution?.Outcome??(j.Gate!=null?"gate-only":"agreement-only"),true);
        Need(at<=j.Terms.Until&&j.Occupied==null,"期限切れまたは本丸引渡し済みです");
        if(i.Action==CastleAction.Cancel){Need(j.Gate==null,"渡した城門は巻き戻せません");return new(i,at,rev,null,null,null,true);}
        Need(j.Terms.Agreed&&Local(w,j.Terms.Recipient,i.Site,at,rev)&&Forces(w,j.Terms.Occupier,i.Site,at,rev)>0,"同意と相手軍の現地立会いが必要です");
        if(i.Action==CastleAction.Confirm)Need(j.Confirmed==null,"本人確認済みです");
        else if(i.Action==CastleAction.Gate)Need(j.Confirmed!=null&&j.Gate==null,"本人確認後に城門を渡してください");
        else Need(i.Action==CastleAction.RequestKeep&&j.Gate!=null&&j.Pending==null,"城門引渡し後に本丸の引渡しを予定してください");
        return new(i,at,rev,null,i.Action==CastleAction.RequestKeep?checked(at+1):null,null,true);
    }
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Castles=new(1,Policy,[]);}
    internal static void Apply(ContractWorld w,CastleInput i){var e=Prepare(w,i,w.ElapsedSeconds,w.CommitSequence);w.Castles=w.Castles! with{Entries=[..w.Castles.Entries,e]};}
    internal static void Tick(ContractWorld w){if(!Enabled(w))return;foreach(var j in Jobs(w).Where(j=>j.Pending?.Due==w.ElapsedSeconds)){var i=new CastleInput(ResolutionId(j.Pending!),CastleAction.ResolveKeep,j.Terms.Site,j.Terms.Id);var e=Prepare(w,i,w.ElapsedSeconds,w.CommitSequence,true);w.Castles=w.Castles! with{Entries=[..w.Castles.Entries,e]};}}
    internal static long? NextBoundary(ContractWorld w,long target)=>Jobs(w).SelectMany(j=>new[]{j.Pending?.Due,j.Terms.Until+1}).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().FirstOrDefault();
    public static bool HoldsNpc(ContractWorld w,EntityId muster)=>Enabled(w)&&Jobs(w).Any(j=>j.Terms.RecipientMuster==muster&&j.Cancelled==null&&j.Confirmed!=null&&(j.Occupied!=null||w.ElapsedSeconds<=j.Terms.Until));
    internal static void Validate(ContractWorld w){if(!Enabled(w)){Need(w.Castles==null,"旧版へ城引渡しを追加できません");return;}Need(w.Castles is {Schema:1,Policy:Policy,Entries:not null},"城引渡しの版が違います");var es=w.Castles.Entries;var ids=new HashSet<EntityId>();long prior=0,priorAt=0;foreach(var e in es){Need(e.Revision>prior&&e.Revision<=w.CommitSequence&&e.At>=priorAt&&e.At<=w.ElapsedSeconds&&ids.Add(e.Input.Id),"城原本の順序/IDが不正です");Need(!FactionStances.OtherIdUsed(w,e.Input.Id,includeCastle:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id),"別操作の原本です");Need(e==Prepare(w,e.Input,e.At,e.Revision,e.Input.Action==CastleAction.ResolveKeep),"引渡し原本/立会い/占有と履行が違います");prior=e.Revision;priorAt=e.At;}foreach(var j in Jobs(w))Need(j.Pending==null||j.Pending.Due>w.ElapsedSeconds,"本丸の予定境界が未処理です");}
    static string Label(string? s)=>s switch{"contested"=>"本丸引渡し原本を保持・現地に旧主側部隊があり係争",
"occupied"=>"本丸引渡し・軍事占有を立会確認","relief-hold"=>"旧主側部隊/救援が残るため本丸引渡し保留","absent"=>"当事者/受取部隊が不在で本丸引渡し保留","expired"=>"期限切れ・渡した門の記録は保持","occupation-record-no-force"=>"占有原本あり・現地の受取部隊は不在","gate-only"=>"城門のみ引渡し","agreement-only"=>"合意原本のみ・軍事占有前",_=>"本丸引渡し結果は未確認"};
    public static CastleView? ViewForProjection(ContractWorld w)=>View(w);
    internal static CastleView? View(ContractWorld w){if(!Enabled(w))return null;var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;bool stable=site is {} x&&Local(w,w.PlayerControl,x,w.ElapsedSeconds,w.CommitSequence+1);var candidates=!stable||!w.NewGame!.Start.Castles.Any(c=>c.Id==site!.Value.Value&&c.Faction!=null)||FactionStances.ResolveAuthority(w,w.PlayerControl,Owner(w,site!.Value),w.ElapsedSeconds,w.CommitSequence)==null?[]:HouseSuccession.Factions(w).Where(f=>new EntityId(f.Id)!=Owner(w,site!.Value)&&Local(w,new(f.Head),site.Value,w.ElapsedSeconds,w.CommitSequence+1)&&Forces(w,new(f.Id),site.Value,w.ElapsedSeconds,w.CommitSequence+1)>0).Select(f=>new EntityId(f.Id)).ToArray();var cards=Jobs(w).Select(j=>{bool local=stable&&site==j.Terms.Site;var witness=Entries(w).Where(e=>(e.Input.Id==j.Terms.Id||e.Input.Original==j.Terms.Id)&&e.Witness).Last();string status=j.Cancelled!=null?"引渡し前に取りやめ":j.Pending!=null?"本丸引渡しを次の秒境界に予定":Label(j.Observed?.Outcome??(j.Gate!=null?"gate-only":"agreement-only"));if(local&&j.Occupied!=null&&Forces(w,j.Terms.Occupier,j.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)==0)status=Label("occupation-record-no-force");if(local&&j.Occupied!=null&&Forces(w,j.Terms.Owner,j.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1)>0)status=Label("contested");if(!local)status+="（立会日時までの記録・現況未確認）";bool ready=local&&j.Cancelled==null&&j.Occupied==null&&w.ElapsedSeconds<=j.Terms.Until&&j.Terms.Agreed;return new CastleCard(j.Terms.Id,j.Terms.Site,j.Terms.Owner,j.Terms.Occupier,j.Terms.Until,status,local?w.ElapsedSeconds:witness.At,local,local?Forces(w,j.Terms.Occupier,j.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1):null,local?Forces(w,j.Terms.Owner,j.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1):null,ready&&j.Confirmed==null,ready&&j.Confirmed!=null&&j.Gate==null,ready&&j.Gate!=null&&j.Pending==null,local&&j.Cancelled==null&&j.Gate==null&&w.ElapsedSeconds<=j.Terms.Until);}).ToArray();return new(stable?site:null,candidates,cards);}
}
public sealed partial class WorldSession
{
    public CastleView? CastleView(){lock(gate)return CastleHandover.View(state);}
    public CommandResult ManageCastle(CastleInput i,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!CastleHandover.Enabled(state)||i.Action==CastleAction.ResolveKeep)return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,"本人の城引渡し操作ではありません");var old=state.Castles!.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,i.Id)||FactionStances.OwnIdUsed(state,i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"別操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>CastleHandover.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"引渡しの原本を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
