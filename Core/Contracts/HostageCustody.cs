using static HakutaiKagami.Core.Contracts.FamilyConsentRules;
namespace HakutaiKagami.Core.Contracts;
public enum CustodyAction { Propose, PersonalAccept, PersonalRefuse, ReceiverAccept, ReceiverRefuse, Admit, Withdraw, Release }
public sealed record CustodyInput(EntityId Id,CustodyAction Action,EntityId? Person=null,EntityId? ReceiverHouse=null,EntityId? Original=null,int Days=0,string? Guarantee=null);
public sealed record CustodyTerms(EntityId Id,EntityId Person,EntityId Guardian,EntityId GuardianHouse,EntityId Custodian,EntityId CustodianHouse,EntityId Site,long Until,string Guarantee,string Treatment,AdoptionHouse KnownReceiver);
public sealed record CustodyEntry(CustodyInput Input,EntityId Actor,long At,long Revision,CustodyTerms? Terms);
public sealed record CustodyState(int Schema,string Policy,CustodyEntry[] Entries);
public sealed record CustodyCard(EntityId Id,EntityId Person,EntityId Site,string Status,long LastObservedAt,EntityId Custodian,long Until,string Guarantee,string Treatment,bool CanAdmit,bool CanWithdraw,bool CanRelease);
public static class HostageCustody
{
    public const string Policy="local-adult-consenting-hostage-distinct-custody-release-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.CustodyPolicy==Policy;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Custody=new(1,Policy,[]);}
    static CustodyEntry[] Entries(ContractWorld w)=>w.Custody?.Entries??[];
    static CustodyEntry[] Before(ContractWorld w,long at,long rev)=>Entries(w).Where(e=>e.At<at||e.At==at&&e.Revision<rev).ToArray();
    static CustodyEntry[] Case(CustodyEntry[] rows,EntityId id)=>rows.Where(e=>e.Input.Id==id||e.Input.Original==id).ToArray();
    static bool Held(CustodyEntry[] rows,EntityId id)=>Case(rows,id).Any(e=>e.Input.Action==CustodyAction.Admit)&&!Case(rows,id).Any(e=>e.Input.Action==CustodyAction.Release);
    static bool Closed(CustodyEntry[] rows,CustodyTerms t,long at)=>Case(rows,t.Id).Any(e=>e.Input.Action is CustodyAction.PersonalRefuse or CustodyAction.ReceiverRefuse or CustodyAction.Withdraw or CustodyAction.Release)||at>t.Until&&!Held(rows,t.Id);
    public static bool HeldAt(ContractWorld w,EntityId person,long at,long rev)=>Before(w,at,rev).Where(e=>e.Terms?.Person==person).Any(p=>Held(Before(w,at,rev),p.Input.Id));
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Entries(w).Any(e=>e.Input.Id==id);
    static bool Active(TimedRelation r,long at,long rev)=>r.From<=at&&(at<r.Until||at==r.Until&&rev<r.Revision);
    static bool PersonalRetainer(ContractWorld w,EntityId person,EntityId guardian,long at,long rev)=>w.NewGame!.Registered.Any(p=>p.Id==person.Value)&&HouseholdService.BondsAt(w,at,rev).Any(b=>b.Subject==person&&b.Target==guardian&&Active(b,at,rev));
    static bool FamilyPending(ContractWorld w,EntityId person,long at,long rev){
        var a=(w.Adoption?.Entries??[]).Where(e=>e.At<at||e.At==at&&e.Revision<rev).ToArray();
        if(a.Where(p=>p.Terms!=null&&(p.Terms.Parent==person||p.Terms.Child==person)&&at<=p.Terms.Until).Any(p=>!a.Any(e=>e.Input.Original==p.Input.Id&&e.Input.Action is AdoptionAction.Implement or AdoptionAction.Withdraw or AdoptionAction.PersonalNo)))return true;
        var m=(w.Marriage?.Entries??[]).Where(e=>e.At<at||e.At==at&&e.Revision<rev).ToArray();
        return m.Where(p=>p.Terms!=null&&(p.Terms.First==person||p.Terms.Second==person)&&at<=p.Terms.Until).Any(p=>!m.Any(e=>e.Input.Original==p.Input.Id&&e.Input.Action is MarriageAction.Implement or MarriageAction.Withdraw or MarriageAction.PersonalNo));
    }
    static bool Free(ContractWorld w,EntityId person,EntityId site,long at,long rev){
        if(person==w.PlayerControl||!Present(w,person,site,at,rev)||w.Offices.Any(o=>o.Subject==person&&Active(o,at,rev))||FamilyPending(w,person,at,rev))return false;
        return (w.Companions?.Orders??[]).Where(q=>q.Input.Person==person&&(q.At<at||q.At==at&&q.Revision<rev)&&q.Input.Action is CompanionAction.Agree or CompanionAction.EndAgreement).LastOrDefault()?.Input.Action!=CompanionAction.Agree;
    }
    static bool LocalAuthorities(ContractWorld w,CustodyTerms t,long at,long rev)=>Present(w,t.Guardian,t.Site,at,rev)&&Present(w,t.Custodian,t.Site,at,rev)&&FactionStances.ResolveAuthority(w,t.Guardian,t.GuardianHouse,at,Math.Min(rev,w.CommitSequence))!=null&&FactionStances.ResolveAuthority(w,t.Custodian,t.CustodianHouse,at,Math.Min(rev,w.CommitSequence))!=null;
    static AdoptionHouse Known(ContractWorld w,EntityId owner,long at,long rev){var k=RepresentativeKnowledge.Known(w,w.PlayerControl,owner,at,Math.Min(rev,w.CommitSequence))??throw new InvalidDataException("相手の家の代表者を確認してください");return new(owner,k.Person,k.ObservedAt,k.Source);}
    static bool Ready(ContractWorld w,CustodyTerms t,CustodyEntry[] rows,long at,long rev)=>!Closed(rows,t,at)&&!Held(rows,t.Id)&&at<=t.Until&&LocalAuthorities(w,t,at,rev)&&Free(w,t.Person,t.Site,at,rev)&&PersonalRetainer(w,t.Person,t.Guardian,at,rev)&&Case(rows,t.Id).Any(e=>e.Input.Action==CustodyAction.PersonalAccept)&&Case(rows,t.Id).Any(e=>e.Input.Action==CustodyAction.ReceiverAccept);
    static void Check(ContractWorld w,CustodyEntry e,CustodyEntry[] prior){
        var i=e.Input;Need(Enabled(w)&&Enum.IsDefined(i.Action)&&EntityId.IsValid(i.Id.Value)&&e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence,"Invalid custody input");
        Need(!prior.Any(x=>x.Input.Id==i.Id)&&!FactionStances.OtherIdUsed(w,i.Id,includeCustody:false)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使ったIDです");
        if(i.Action==CustodyAction.Propose){
            var t=e.Terms;Need(e.Actor==w.PlayerControl&&t!=null&&i.Person==t.Person&&i.ReceiverHouse==t.CustodianHouse&&i.Original==null&&i.Days is >=1 and <=7&&i.Guarantee!=null&&i.Guarantee.Length is >0 and <=200&&!string.IsNullOrWhiteSpace(i.Guarantee)&&!i.Guarantee.Any(char.IsControl),"預かりの当事者と保証内容を指定してください");
            Need(t.Id==i.Id&&t.Guardian==e.Actor&&t.GuardianHouse!=t.CustodianHouse&&t.Custodian!=e.Actor&&t.Until==e.At+i.Days*86400L&&t.Guarantee==i.Guarantee&&t.Treatment=="通常の宿泊・食事の提供義務"&&t.KnownReceiver==Known(w,t.CustodianHouse,e.At,e.Revision-1)&&t.Custodian==t.KnownReceiver.Representative,"保証原本の当事者・期限・待遇が不正です");
            Need(LocalAuthorities(w,t,e.At,e.Revision)&&Free(w,t.Person,t.Site,e.At,e.Revision)&&PersonalRetainer(w,t.Person,t.Guardian,e.At,e.Revision),"本人・両家の権限者が現地にいて、他の用件を終える必要があります");
            Need(!prior.Where(p=>p.Terms?.Person==t.Person).Any(p=>!Closed(prior,p.Terms!,e.At)),"既存の身柄契約と競合します");return;
        }
        Need(i.Person==null&&i.ReceiverHouse==null&&i.Original!=null&&i.Days==0&&i.Guarantee==null&&e.Terms==null,"同じ保証原本を指定してください");var p=prior.SingleOrDefault(p=>p.Input.Id==i.Original&&p.Terms!=null);Need(p!=null,"保証原本がありません");var terms=p.Terms!;var current=Case(prior,terms.Id);
        if(i.Action==CustodyAction.Release){Need(e.Actor==terms.Guardian&&Held(prior,terms.Id)&&LocalAuthorities(w,terms,e.At,e.Revision)&&PersonalCompanions.PhysicalSiteAt(w,terms.Person,e.At,e.Revision)==terms.Site,"現地の現在の権限者と同じ本人を確認してください");return;}
        Need(!Closed(prior,terms,e.At)&&!Held(prior,terms.Id),"受入前の保証案件ではありません");
        if(i.Action is CustodyAction.PersonalAccept or CustodyAction.PersonalRefuse){Need(e.Actor==terms.Person&&e.At>=p.At+1&&!current.Any(x=>x.Input.Action is CustodyAction.PersonalAccept or CustodyAction.PersonalRefuse)&&Present(w,terms.Guardian,terms.Site,e.At,e.Revision)&&Free(w,terms.Person,terms.Site,e.At,e.Revision),"本人への現地確認が必要です");Need((i.Action==CustodyAction.PersonalAccept)==IndividualAccepts(w,terms.Person,terms.Guardian,e.At,e.Revision),"本人の意思と異なります");}
        else if(i.Action is CustodyAction.ReceiverAccept or CustodyAction.ReceiverRefuse){Need(e.Actor==terms.Custodian&&current.Any(x=>x.Input.Action==CustodyAction.PersonalAccept)&&!current.Any(x=>x.Input.Action is CustodyAction.ReceiverAccept or CustodyAction.ReceiverRefuse)&&LocalAuthorities(w,terms,e.At,e.Revision),"受入先の現地承諾が必要です");Need((i.Action==CustodyAction.ReceiverAccept)==w.Characters.Single(c=>c.Id==terms.Custodian).WillObey,"受入先の意思と異なります");}
        else if(i.Action==CustodyAction.Admit)Need(e.Actor==terms.Guardian&&Ready(w,terms,prior,e.At,e.Revision),"本人・受入先の承諾と現地の空きを確認してください");
        else if(i.Action==CustodyAction.Withdraw)Need(e.Actor==terms.Guardian,"打診者本人だけが取り下げられます");
        else throw new InvalidDataException("Unsupported custody action");
    }
    static void Add(ContractWorld w,CustodyInput i,EntityId actor,CustodyTerms? terms=null){var e=new CustodyEntry(i,actor,w.ElapsedSeconds,w.CommitSequence,terms);Check(w,e,Entries(w));w.Custody=w.Custody! with{Entries=[..w.Custody.Entries,e]};}
    internal static void Apply(ContractWorld w,CustodyInput i){
        Need(i.Action is CustodyAction.Propose or CustodyAction.Admit or CustodyAction.Withdraw or CustodyAction.Release,"本人・受入先の返答を代行できません");CustodyTerms? t=null;
        if(i.Action==CustodyAction.Propose){Need(i.Person!=null&&i.ReceiverHouse!=null,"当事者を指定してください");var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence)??throw new InvalidDataException("現地で用件を終えてください");var owner=w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Id)).SingleOrDefault(o=>FactionStances.ResolveAuthority(w,w.PlayerControl,o,w.ElapsedSeconds,w.CommitSequence)!=null);Need(EntityId.IsValid(owner.Value),"家の現地権限がありません");var known=Known(w,i.ReceiverHouse.Value,w.ElapsedSeconds,w.CommitSequence-1);t=new(i.Id,i.Person.Value,w.PlayerControl,owner,known.Representative,i.ReceiverHouse.Value,site,checked(w.ElapsedSeconds+i.Days*86400L),i.Guarantee!,"通常の宿泊・食事の提供義務",known);}
        Add(w,i,w.PlayerControl,t);
    }
    internal static void Update(ContractWorld w){if(!Enabled(w))return;foreach(var p in Entries(w).Where(e=>e.Terms!=null).ToArray()){
        var t=p.Terms!;if(Closed(Entries(w),t,w.ElapsedSeconds)||Held(Entries(w),t.Id)||w.ElapsedSeconds<p.At+1)continue;var rows=Case(Entries(w),t.Id);
        if(!rows.Any(e=>e.Input.Action is CustodyAction.PersonalAccept or CustodyAction.PersonalRefuse)&&Present(w,t.Guardian,t.Site,w.ElapsedSeconds,w.CommitSequence)&&Free(w,t.Person,t.Site,w.ElapsedSeconds,w.CommitSequence)){bool yes=IndividualAccepts(w,t.Person,t.Guardian,w.ElapsedSeconds,w.CommitSequence);Add(w,new(new("request:custody-person/"+ContractJson.CanonicalHash(t.Id)),yes?CustodyAction.PersonalAccept:CustodyAction.PersonalRefuse,Original:t.Id),t.Person);}
        rows=Case(Entries(w),t.Id);if(rows.Any(e=>e.Input.Action==CustodyAction.PersonalAccept)&&!rows.Any(e=>e.Input.Action is CustodyAction.ReceiverAccept or CustodyAction.ReceiverRefuse)&&LocalAuthorities(w,t,w.ElapsedSeconds,w.CommitSequence))Add(w,new(new("request:custody-receiver/"+ContractJson.CanonicalHash(t.Id)),w.Characters.Single(c=>c.Id==t.Custodian).WillObey?CustodyAction.ReceiverAccept:CustodyAction.ReceiverRefuse,Original:t.Id),t.Custodian);
    }}
    internal static long? NextBoundary(ContractWorld w,long target)=>!Enabled(w)?null:Entries(w).Where(e=>e.Terms!=null&&!Closed(Entries(w),e.Terms!,w.ElapsedSeconds)).SelectMany(e=>new[]{e.At+1,e.Terms!.Until+1}).Where(t=>t>w.ElapsedSeconds&&t<=target).Select(t=>(long?)t).Min();
    public static EntityId[] Candidates(ContractWorld w){if(!Enabled(w))return [];var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);return site==null?[]:w.Characters.Where(c=>PersonalRetainer(w,c.Id,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1)&&Free(w,c.Id,site.Value,w.ElapsedSeconds,w.CommitSequence+1)).Select(c=>c.Id).ToArray();}
    public static CustodyCard[] View(ContractWorld w)=>!Enabled(w)?[]:Entries(w).Where(e=>e.Terms!=null).Select(p=>{var t=p.Terms!;var rows=Case(Entries(w),t.Id);bool held=Held(Entries(w),t.Id);string status=rows.Any(e=>e.Input.Action==CustodyAction.Release)?"現地で解放・返還済み":held?w.ElapsedSeconds>t.Until?"預かり中・返還期限超過（所在の最終確認は下記）":"預かり中（所在の最終確認は下記）":Closed(Entries(w),t,w.ElapsedSeconds)?"辞退・取下げ・期限切れで終了":rows.Any(e=>e.Input.Action==CustodyAction.ReceiverAccept)?"本人・受入先が合意、現地受入待ち":"本人と受入先への相談待ち";return new CustodyCard(t.Id,t.Person,t.Site,status,rows.Max(e=>e.At),t.Custodian,t.Until,t.Guarantee,t.Treatment,Ready(w,t,Entries(w),w.ElapsedSeconds,w.CommitSequence+1),!held&&!Closed(Entries(w),t,w.ElapsedSeconds),held&&LocalAuthorities(w,t,w.ElapsedSeconds,w.CommitSequence+1)&&PersonalCompanions.PhysicalSiteAt(w,t.Person,w.ElapsedSeconds,w.CommitSequence+1)==t.Site);}).ToArray();
    public static void Validate(ContractWorld w,ContractWorld? prior=null){if(!Enabled(w)){Need(w.Custody==null,"Custody without policy");return;}Need(w.Custody is {Schema:1,Policy:Policy,Entries:not null},"Missing custody state");var rows=Entries(w);if(prior?.Custody is {} old)Need(ContractJson.Write(rows.Take(old.Entries.Length).ToArray()).SequenceEqual(ContractJson.Write(old.Entries)),"Prior custody changed");for(int n=0;n<rows.Length;n++){if(n>0)Need(rows[n].At>=rows[n-1].At&&rows[n].Revision>=rows[n-1].Revision,"Custody order changed");Check(w,rows[n],rows.Take(n).ToArray());}foreach(var p in rows.Where(p=>p.Terms!=null&&Held(rows,p.Input.Id)))Need(PersonalCompanions.PhysicalSiteAt(w,p.Terms!.Person,w.ElapsedSeconds,w.CommitSequence+1)==p.Terms.Site,"Held person moved without escort");}
}
public sealed partial class WorldSession
{
    public CommandResult ManageCustody(CustodyInput input,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!HostageCustody.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の身柄相談ではありません");var old=state.Custody!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input&&old.Actor==actor?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"記録済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>HostageCustody.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人・受入先・所在の確認を記録しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
