namespace HakutaiKagami.Core.Contracts;
public enum CastellanAction { Appoint,Release,ReceiveStore }
public sealed record CastellanInput(EntityId Id,CastellanAction Action,EntityId Site,EntityId? Person=null,EntityId? Appointment=null,int Days=30);
public sealed record CastellanReceipt(EntityId Owner,long ObservedAt,long Rice,long Wood,long Arms,long Cash);
public sealed record CastellanEntry(CastellanInput Input,EntityId Principal,EntityId Agent,EntityId Owner,long At,long Revision,long Until,CastellanReceipt? Receipt);
public sealed record CastellanState(int Schema,string Policy,CastellanEntry[] Entries);
public sealed record CastellanCard(EntityId Id,EntityId Site,EntityId Person,long At,long Until,string Status,CastellanReceipt? Receipt,bool Local,bool CanRelease,bool CanReceive);
public sealed record CastellanView(EntityId? Site,EntityId[] Candidates,CastellanCard[] Appointments);
// Administrative office and custody are not ownership or political head authority.
public static class CastleAdministration
{
    public const string Policy="local-consented-castellan-and-separate-store-custody-v20";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy==Policy;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    static bool Before(CastellanEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<rev;
    static CastellanEntry[] Prefix(ContractWorld w,long at,long rev)=>w.Castellan?.Entries.Where(e=>Before(e,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Castellan?.Entries.Any(e=>e.Input.Id==id)==true;
    static EntityId Owner(ContractWorld w,EntityId site)=>new(w.NewGame!.Start.Castles.Single(c=>c.Id==site.Value).Faction??throw new InvalidDataException("所有家がありません"));
    static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>CommunityStewardship.Local(w,actor,site,at,rev);
    static bool Bound(ContractWorld w,EntityId actor,EntityId principal,EntityId owner,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&w.ServiceBonds.Any(b=>b.Subject==actor&&b.Target==principal&&b.From<=at&&(at<b.Until||at==b.Until&&rev<b.Revision))&&w.Memberships.Any(m=>m.Subject==actor&&m.Target==owner&&m.From<=at&&(at<m.Until||at==m.Until&&rev<m.Revision));
    // A dated foreign gate transfer blocks new disposals. Never undo earlier orders.
    public static bool Blocked(ContractWorld w,EntityId site,long at,long rev)=>Enabled(w)&&CastleHandover.Jobs(w,at,rev).Any(j=>j.Terms.Site==site&&j.Gate!=null&&j.Terms.Occupier!=Owner(w,site)&&CastleHandover.Forces(w,j.Terms.Occupier,site,at,rev)>0);
    static CastellanEntry? Current(ContractWorld w,EntityId site,long at,long rev){var es=Prefix(w,at,rev);var e=es.LastOrDefault(e=>e.Input.Site==site&&e.Input.Action is CastellanAction.Appoint or CastellanAction.Release);return e?.Input.Action==CastellanAction.Appoint&&at<e.Until&&Bound(w,e.Agent,e.Principal,e.Owner,at,rev)?e:null;}
    internal static StewardGrant? Grant(ContractWorld w,EntityId site,long at,long rev){if(!Enabled(w)||Blocked(w,site,at,rev))return null;var e=Current(w,site,at,rev);return e==null?null:new(e.Input.Id,e.Principal,e.Agent,e.Owner,site,site,e.At,e.Until,e.Revision);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Castellan=new(1,Policy,[]);}
    internal static CastellanReceipt Store(ContractWorld w,EntityId site,long at,long rev){var m=MaterialMarkets.HouseAt(w,site,at,rev);return new(Owner(w,site),at,CommunityEconomy.AccountAt(w,site,at,rev).HouseRice,m.Wood,m.Weapons,RegionalDomains.Cash(w,site,at,rev));}
    static CastellanEntry Prepare(ContractWorld w,CastellanInput i,long at,long rev)
    {
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&CommunityEconomy.SitesFor(w).Contains(i.Site),"城主操作の対象が不正です");
        var owner=Owner(w,i.Site);var principal=new EntityId(w.NewGame!.Start.Factions.Single(f=>f.Id==owner.Value).Head);
        Need(Local(w,w.PlayerControl,i.Site,at,rev)&&!PersonalLife.Resting(w,at,rev)&&!Blocked(w,i.Site,at,rev),"現地で実務できる支配状況ではありません");
        if(i.Action==CastellanAction.Appoint){Need(i.Person is {}&&i.Person!=principal&&i.Appointment==null&&i.Days>=1&&i.Days<=30,"同意する担当者と1〜30日の期間を指定してください");var agent=i.Person!.Value;
            Need((w.PlayerControl==principal||w.PlayerControl==agent)&&Local(w,principal,i.Site,at,rev)&&Local(w,agent,i.Site,at,rev)&&Bound(w,agent,principal,owner,at,rev)&&FactionStances.ResolveAuthority(w,principal,owner,at,Math.Min(rev,w.CommitSequence))!=null,"現地当主と成人直属家臣の権限が必要です");
            var other=w.PlayerControl==principal?agent:principal;Need(w.Characters.Single(c=>c.Id==other).WillObey,"相手が任命条件に同意しません");
            Need(!w.Stewardship!.Grants.Any(g=>g.Site==i.Site&&CommunityStewardship.Active(w,g,at,rev)),"先の代理行政委任を終了してください");
            return new(i,principal,agent,owner,at,rev,checked(at+i.Days*86400L),null);
        }
        var old=Current(w,i.Site,at,rev);Need(old!=null&&old.Input.Id==i.Appointment&&i.Person==null&&i.Days==30,"有効な任命原本を指定してください");
        if(i.Action==CastellanAction.Release)Need(w.PlayerControl==principal&&Local(w,old.Agent,i.Site,at,rev),"当主と担当者の現地引継ぎが必要です");
        else Need(w.PlayerControl==old.Agent&&CommunityStewardship.Authority(w,old.Agent,owner,i.Site,at,rev)!=null,"任命された担当者が現地で保管確認してください");
        return new(i,old.Principal,old.Agent,old.Owner,at,rev,old.Until,i.Action==CastellanAction.ReceiveStore?Store(w,i.Site,at,rev):null);
    }
    internal static void Apply(ContractWorld w,CastellanInput i){var e=Prepare(w,i,w.ElapsedSeconds,w.CommitSequence);w.Castellan=w.Castellan! with{Entries=[..w.Castellan.Entries,e]};}
    internal static void Validate(ContractWorld w){if(!Enabled(w)){Need(w.Castellan==null,"旧版へ城主任命を追加できません");return;}Need(w.Castellan is {Schema:1,Policy:Policy,Entries:not null},"城主任命の原本がありません");long rev=0,at=0;var ids=new HashSet<EntityId>();foreach(var e in w.Castellan.Entries){Need(e.Revision>rev&&e.Revision<=w.CommitSequence&&e.At>=at&&e.At<=w.ElapsedSeconds&&ids.Add(e.Input.Id),"城主原本の順序が不正です");Need(!FactionStances.OtherIdUsed(w,e.Input.Id,includeCastellan:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id)&&e==Prepare(w,e.Input,e.At,e.Revision),"任命・保管の根拠が一致しません");rev=e.Revision;at=e.At;}}
    public static CastellanView? ViewForProjection(ContractWorld w)=>View(w);
    internal static string? Role(ContractWorld w,EntityId actor)=>Enabled(w)&&w.Castellan!.Entries.Where(e=>e.Input.Action==CastellanAction.Appoint&&e.Agent==actor).Any(e=>Current(w,e.Input.Site,w.ElapsedSeconds,w.CommitSequence+1)?.Input.Id==e.Input.Id)?"城主（期限付き行政・保管）":null;
    internal static CastellanView? View(ContractWorld w){if(!Enabled(w))return null;var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;bool local=site is {} s&&Local(w,w.PlayerControl,s,w.ElapsedSeconds,w.CommitSequence+1);long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        var candidates=local&&!Blocked(w,site!.Value,at,rev)?w.NewGame!.Start.People.Select(p=>new EntityId(p.Id)).Where(p=>p!=w.PlayerControl&&Local(w,p,site.Value,at,rev)&&w.ServiceBonds.Any(b=>(b.Subject==p&&b.Target==w.PlayerControl||b.Subject==w.PlayerControl&&b.Target==p)&&b.From<=at&&at<b.Until)).ToArray():[];
        var cards=w.Castellan!.Entries.Where(e=>e.Input.Action==CastellanAction.Appoint).Select(e=>{bool here=local&&site==e.Input.Site;bool active=Current(w,e.Input.Site,at,rev)?.Input.Id==e.Input.Id;bool blocked=here&&Blocked(w,e.Input.Site,at,rev);var receipt=w.Castellan.Entries.LastOrDefault(q=>q.Input.Action==CastellanAction.ReceiveStore&&q.Input.Appointment==e.Input.Id);return new CastellanCard(e.Input.Id,e.Input.Site,e.Agent,e.At,e.Until,here?(blocked?"軍事占有との競合・新規処分を保留":active?"期限付き城主・行政と受領のみ":"交代・期限・直属関係により終了"):"合意日時の任命記録・現況未確認",receipt?.Receipt,here,here&&active&&!blocked&&e.Principal==w.PlayerControl,here&&active&&!blocked&&e.Agent==w.PlayerControl);}).ToArray();return new(local?site:null,candidates,cards);}
}
public sealed partial class WorldSession
{
    public CastellanView? CastellanView(){lock(gate)return CastleAdministration.View(state);}
    public CommandResult ManageCastellan(CastellanInput i,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!CastleAdministration.Enabled(state))return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,"本人の城主操作ではありません");var old=state.Castellan!.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,i.Id)||FactionStances.OwnIdUsed(state,i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"別のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>CastleAdministration.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"城主・保管の原本を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
