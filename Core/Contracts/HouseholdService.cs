namespace HakutaiKagami.Core.Contracts;
public enum HouseholdServiceAction { Release,Pledge }
public sealed record HouseholdServiceInput(EntityId Id,HouseholdServiceAction Action,EntityId Owner,EntityId Other,EntityId? Source=null);
public sealed record HouseholdServiceEntry(HouseholdServiceInput Input,EntityId Subject,EntityId Site,long At,long Revision);
public sealed record HouseholdServiceState(int Schema,string Policy,HouseholdServiceEntry[] Entries);
public sealed record HouseholdServiceRelease(EntityId Source,EntityId Subject,EntityId Principal,EntityId Other,bool CanRelease);
public sealed record HouseholdServiceView(EntityId? Owner,EntityId? Site,EntityId? Principal,EntityId? Source,bool CanRelease,bool CanPledge,string Status,HouseholdServiceRelease[] Releases);
// Local personal allegiance is deliberately independent of membership, office and delegation.
public static class HouseholdService
{
    public const string Policy="local-post-succession-personal-service-independent-house-membership-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static HouseholdServiceEntry[] Entries(ContractWorld w,long at,long rev)=>w.HouseService?.Entries.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray()??[];
    static bool Active(TimedRelation b,long at,long rev)=>b.From<=at&&(at<b.Until||at==b.Until&&rev<b.Revision);
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.HouseService?.Entries.Any(e=>e.Input.Id==id)==true;
    static EntityId BondId(EntityId id)=>new("service:household/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value)));
    internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,long at=long.MaxValue,long rev=long.MaxValue)
    {
        var rows=original.ToList();foreach(var e in Entries(w,at,rev)){
            if(e.Input.Action==HouseholdServiceAction.Release){int n=rows.FindIndex(b=>b.Id==e.Input.Source&&b.Subject==e.Subject);Need(n>=0&&rows[n].From<e.At&&e.At<=rows[n].Until,"個人奉公の元の期間がありません");rows[n]=rows[n] with{Until=e.At,Revision=e.Revision};}
            else rows.Add(new(BondId(e.Input.Id),e.Subject,e.Input.Other,"service",NewGameWorld.Basis,e.At,long.MaxValue,e.Revision));
        }return rows.ToArray();
    }
    internal static TimedRelation[] BondsAt(ContractWorld w,long at,long rev)=>w.HouseService==null?w.ServiceBonds:Relations(w,Base(w),at,Math.Min(rev,w.CommitSequence));
    static TimedRelation[] Base(ContractWorld w)=>NewGameService.Relations(w,NewGameWorld.InitialRelations(w.NewGame!).Bonds,"service",household:false);
    static TimedRelation? Bond(ContractWorld w,long at,long rev)=>Relations(w,Base(w),at,rev).SingleOrDefault(b=>b.Subject==w.PlayerControl&&b.Kind=="service"&&Active(b,at,rev));
    static EntityId? Owner(ContractWorld w,long at,long rev)=>w.Memberships.SingleOrDefault(b=>b.Subject==w.PlayerControl&&b.Kind=="membership"&&Active(b,at,rev))?.Target;
    static bool ChangedHouse(ContractWorld w,EntityId owner,long at,long rev)=>HouseSuccession.Enabled(w)&&HouseSuccession.Jobs(w,at,rev).Any(j=>j.Terms.Owner==owner&&j.Applied!=null);
    static bool Local(ContractWorld w,EntityId who,EntityId site,long at,long rev)=>CommunityStewardship.Local(w,who,site,at,rev+1);
    static bool Clear(ContractWorld w,EntityId subject,long at,long rev)=>!PersonalLife.Resting(w,at,rev+1)&&!NpcArmies.CommanderBusyAt(w,subject,at,rev)&&!HouseLogistics.FreightJobs(w,at,rev+1).Any(j=>j.Terms.Carrier==subject&&j.Phase is not(HouseFreightPhase.Completed or HouseFreightPhase.Returned))&&(subject!=w.PlayerControl||!NewGameMilitary.Outstanding(w,at,rev)&&!NewGameMilitary.LedgerStates(w).Any(s=>{var c=w.ForkMilitaryLedger();c.NewMilitary=s;return NewGameMilitary.CustodyAt(c,at,rev);}))&&!w.Tasks.Any(t=>t.Executor==subject&&(t.FinishedAt==null||t.FinishedAt>at)&&w.Requests.Any(q=>q.Id==t.RequestId&&Before(q.CreatedAt,q.CommitSequence,at,rev)))&&!(w.Service?.Requests.Any(q=>q.Input.Action==CareerAction.RequestRelease&&Before(q.At,q.Revision,at,rev)&&!w.Service.Events.Any(e=>e.Case==q.Case&&e.Kind==CareerEventKind.Released&&Before(e.At,e.Revision,at,rev)))??false);
    static bool ReportsReady(ContractWorld w,EntityId subject,EntityId principal,long at,long rev)=>w.Requests.Where(q=>q.Intent.Actor==principal&&q.Intent.Executor==subject&&Before(q.CreatedAt,q.CommitSequence,at,rev)).All(q=>w.Tasks.Any(t=>t.RequestId==q.Id&&t.FinishedAt<=at&&HakutaiKagami.Core.Messaging.MessageFlow.LearnedAt(w.Messaging,WorldSession.Child("message",t.Id,"report").Value,principal.Value) is {} known&&known<=at));
    static HouseholdServiceEntry Make(ContractWorld w,HouseholdServiceInput i,long at,long rev)
    {
        Need(HouseSuccession.Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action)&&ChangedHouse(w,i.Owner,at,rev-1)&&Owner(w,at,rev-1)==i.Owner&&i.Other!=w.PlayerControl,"当主交代後の同じ家で本人の奉公を個別確認してください");
        var f=HouseSuccession.Factions(w,at,rev-1).Single(f=>f.Id==i.Owner.Value);var site=new EntityId(f.Home);
        Need(Local(w,w.PlayerControl,site,at,rev)&&Local(w,i.Other,site,at,rev)&&!CastleAdministration.Blocked(w,site,at,rev)&&w.Characters.Single(c=>c.Id==i.Other).WillObey,"現地の同意・所在と返納済みの実務を確認してください");
        var bonds=Relations(w,Base(w),at,rev-1);var source=i.Action==HouseholdServiceAction.Release?bonds.SingleOrDefault(b=>b.Id==i.Source&&Active(b,at,rev-1)):Bond(w,at,rev-1);var subject=i.Action==HouseholdServiceAction.Release?source?.Subject??w.PlayerControl:w.PlayerControl;Need(Clear(w,subject,at,rev-1),"当事者の返納と未完了実務を確認してください");
        if(i.Action==HouseholdServiceAction.Release){Need(source!=null&&source.Id==i.Source&&((source.Subject==w.PlayerControl&&source.Target==i.Other)||(source.Subject==i.Other&&source.Target==w.PlayerControl))&&source.From<at&&w.Memberships.Any(m=>m.Subject==source.Subject&&m.Target==i.Owner&&Active(m,at,rev-1))&&ReportsReady(w,source.Subject,source.Target,at,rev-1),"元の奉公と未着の返報・期間を確認してください");}
        else Need(i.Source==null&&source==null&&!Cycle(bonds,w.PlayerControl,i.Other,at,rev-1)&&i.Other==new EntityId(f.Head)&&FactionStances.ResolveAuthority(w,i.Other,i.Owner,at,Math.Min(rev-1,w.CommitSequence))!=null,"現当主との新しい個人奉公だけを合意できます");
        return new(i,subject,site,at,rev);
    }
    internal static void Apply(ContractWorld w,HouseholdServiceInput i){var e=Make(w,i,w.ElapsedSeconds,w.CommitSequence);w.HouseService=new(1,Policy,[..(w.HouseService?.Entries??[]),e]);NewGameService.Sync(w);}
    static bool Cycle(TimedRelation[] bonds,EntityId subject,EntityId target,long at,long rev){var seen=new HashSet<EntityId>{subject};var who=target;while(seen.Add(who)){var next=bonds.SingleOrDefault(b=>b.Subject==who&&b.Kind=="service"&&Active(b,at,rev));if(next==null)return false;who=next.Target;}return true;}
    internal static bool? BoundOverride(ContractWorld w,EntityId principal,long at,long rev)=>!Entries(w,at,rev).Any(e=>e.Subject==w.PlayerControl)?null:Bond(w,at,rev)?.Target==principal;
    internal static ServiceView Decorate(ContractWorld w,long at,long rev,ServiceView view)
    {
        if(!Entries(w,at,rev).Any(e=>e.Subject==w.PlayerControl))return view;var owner=Owner(w,at,rev);var principal=Bond(w,at,rev)?.Target;string role=owner==null?"浪人":principal==null?"家中・個人の直属奉公なし":"家中・個別に合意した奉公";
        return view with{House=owner,Principal=principal,Role=role,CanApply=false,Cases=view.Cases.Select(c=>c with{CanRelease=false,Status=c.Status+"（個人奉公は別合意）"}).ToArray()};
    }
    public static HouseholdServiceView? View(ContractWorld w)
    {
        if(!HouseSuccession.Enabled(w))return null;long at=w.ElapsedSeconds,rev=w.CommitSequence;var owner=Owner(w,at,rev);if(owner==null||!ChangedHouse(w,owner.Value,at,rev))return null;var home=new EntityId(HouseSuccession.Factions(w).Single(f=>f.Id==owner.Value.Value).Home);var bond=Bond(w,at,rev);bool local=Local(w,w.PlayerControl,home,at,rev);bool release=false,pledge=false;
        if(local&&bond!=null){try{Make(w,new(new("request:house-service-preview"),HouseholdServiceAction.Release,owner.Value,bond.Target,bond.Id),at,rev+1);release=true;}catch(InvalidDataException){}}
        if(local&&bond==null){var head=HouseSuccession.Head(w,owner.Value);try{Make(w,new(new("request:house-service-preview"),HouseholdServiceAction.Pledge,owner.Value,head),at,rev+1);pledge=true;}catch(InvalidDataException){}}
        var releases=new List<HouseholdServiceRelease>();foreach(var b in Relations(w,Base(w),at,rev).Where(b=>b.Kind=="service"&&Active(b,at,rev)&&(b.Subject==w.PlayerControl||b.Target==w.PlayerControl))){var other=b.Subject==w.PlayerControl?b.Target:b.Subject;bool can=false;try{Make(w,new(new("request:house-service-preview"),HouseholdServiceAction.Release,owner.Value,other,b.Id),at,rev+1);can=true;}catch(InvalidDataException){}releases.Add(new(b.Id,b.Subject,b.Target,other,can));}
        return new(owner,local?home:null,bond?.Target,bond?.Id,release,pledge,bond==null?"所属は家に保持・個人的な直属奉公はなし":"個人奉公の相手を保持。家の行政・軍務は別の原本",releases.ToArray());
    }
    internal static void Validate(ContractWorld w)
    {
        if(w.HouseService==null)return;Need(HouseSuccession.Enabled(w)&&w.HouseService is {Schema:1,Policy:Policy,Entries:not null},"個人奉公の版が不正です");long at=0,rev=0;var ids=new HashSet<EntityId>();foreach(var e in w.HouseService.Entries){Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&ids.Add(e.Input.Id)&&!FactionStances.OtherIdUsed(w,e.Input.Id,includeHouseService:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id),"個人奉公の原本順序・IDが不正です");Need(e==Make(w,e.Input,e.At,e.Revision),"個人奉公の同意・所属・引継ぎが原本と一致しません");at=e.At;rev=e.Revision;}}
}
public sealed partial class WorldSession
{
    public HouseholdServiceView? HouseholdServiceView(){lock(gate)return HouseholdService.View(state);}
    public CommandResult ManageHouseholdService(HouseholdServiceInput i,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!HouseSuccession.Enabled(state))return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,"本人の個人奉公ではありません");var old=state.HouseService?.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,i.Id)||FactionStances.OwnIdUsed(state,i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"別の原本です");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>HouseholdService.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"所属と別に個人奉公の合意を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
