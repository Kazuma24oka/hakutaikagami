namespace HakutaiKagami.Core.Contracts;
public sealed record TruceSuccessionInput(EntityId Id,EntityId Owner,EntityId Acceptance);
public sealed record TruceSuccessionEntry(TruceSuccessionInput Input,EntityId Previous,EntityId Successor,EntityId Site,long At,long Revision);
public sealed record TruceSuccessionState(int Schema,string Policy,TruceSuccessionEntry[] Entries);
public sealed record TruceSuccessionCard(EntityId Acceptance,EntityId Owner,EntityId? Other,EntityId Previous,EntityId Successor,long? Until,bool CanRead);
// A local showing of a known organization-party agreement; never rewrites its old papers.
public static class TruceSuccession
{
    public const string Policy="local-known-formed-organization-truce-successor-reading-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    static bool Before(TruceSuccessionEntry e,long at,long rev)=>e.At<at||e.At==at&&e.Revision<=rev;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.TruceInheritance?.Entries.Any(e=>e.Input.Id==id)==true;
    internal static bool Learned(ContractWorld w,EntityId actor,EntityId acceptance,long at,long rev)=>w.TruceInheritance?.Entries.Any(e=>e.Successor==actor&&e.Input.Acceptance==acceptance&&Before(e,at,Math.Min(rev,w.CommitSequence)))==true;
    static TruceSuccessionEntry Make(ContractWorld w,TruceSuccessionInput i,long at,long rev)
    {
        Need(HouseSuccession.Enabled(w)&&TruceNegotiations.Enabled(w)&&EntityId.IsValid(i.Id.Value),"当主交代後の不戦合意の確認ではありません");
        var formation=w.Truces!.Formations.SingleOrDefault(f=>f.Acceptance==i.Acceptance&& (f.At<at||f.At==at&&f.Revision<rev));
        Need(formation!=null&&(formation.Terms.A==i.Owner||formation.Terms.B==i.Owner)&&at<formation.Terms.Until,"期限内の成立済み組織合意を指定してください");
        var p=w.Truces.Papers.Single(p=>p.Input.Id==i.Acceptance);var previous=p.Input.Owner==i.Owner?p.Author:p.Recipient;var head=HouseSuccession.Head(w,i.Owner,at,rev-1);var home=new EntityId(HouseSuccession.Factions(w,at,rev-1).Single(f=>f.Id==i.Owner.Value).Home);
        Need(previous!=head&&HouseSuccession.Jobs(w,at,rev-1).Any(j=>j.Applied!=null&&j.Terms.Owner==i.Owner&&j.Terms.Incumbent==previous&&j.Terms.Heir==head),"元の代表者から現当主への個別確認が必要です");
        Need((w.PlayerControl==previous||w.PlayerControl==head)&&CommunityStewardship.Local(w,w.PlayerControl,home,at,rev)&&CommunityStewardship.Local(w,previous,home,at,rev)&&CommunityStewardship.Local(w,head,home,at,rev)&&!PersonalLife.Resting(w,at,rev)&&!CastleAdministration.Blocked(w,home,at,rev),"旧代表者と新当主の現地立会いが必要です");
        Need((previous==w.PlayerControl||w.Characters.Single(c=>c.Id==previous).WillObey)&&(head==w.PlayerControl||w.Characters.Single(c=>c.Id==head).WillObey)&&FactionStances.ResolveAuthority(w,head,i.Owner,at,Math.Min(rev-1,w.CommitSequence))!=null,"両者自身の同意と現在の締結権限が必要です");
        Need(TruceNegotiations.Known(w,previous,at,rev-1).Any(f=>f.Acceptance==i.Acceptance)&&!TruceNegotiations.Known(w,head,at,rev-1).Any(f=>f.Acceptance==i.Acceptance),"旧代表者が実際に知る成立原本を、新当主が初めて確認してください");
        return new(i,previous,head,home,at,rev);
    }
    internal static void Apply(ContractWorld w,TruceSuccessionInput i){var e=Make(w,i,w.ElapsedSeconds,w.CommitSequence);w.TruceInheritance=new(1,Policy,[..(w.TruceInheritance?.Entries??[]),e]);}
    public static TruceSuccessionCard[] View(ContractWorld w)
    {
        if(!HouseSuccession.Enabled(w)||!TruceNegotiations.Enabled(w))return [];var list=new List<TruceSuccessionCard>();long at=w.ElapsedSeconds,rev=w.CommitSequence;
        foreach(var f in w.Truces!.Formations)foreach(var owner in new[]{f.Terms.A,f.Terms.B}){var p=w.Truces.Papers.Single(p=>p.Input.Id==f.Acceptance);var previous=p.Input.Owner==owner?p.Author:p.Recipient;var head=HouseSuccession.Head(w,owner);if(previous==head||w.PlayerControl!=previous&&w.PlayerControl!=head||!TruceNegotiations.Known(w,w.PlayerControl,at,rev).Any(k=>k.Acceptance==f.Acceptance)&&!CommunityStewardship.Local(w,previous,new(HouseSuccession.Factions(w).Single(q=>q.Id==owner.Value).Home),at,rev+1))continue;
            // A candidate is exposed only after both participants can actually show/read it locally.
            bool can=false;try{Make(w,new(new("request:truce-inheritance-preview"),owner,f.Acceptance),at,rev+1);can=true;}catch(InvalidDataException){}
            bool known=TruceNegotiations.Known(w,w.PlayerControl,at,rev).Any(k=>k.Acceptance==f.Acceptance);if(can||w.TruceInheritance?.Entries.Any(e=>e.Input.Acceptance==f.Acceptance&&e.Previous==previous&&e.Successor==head&&Before(e,at,rev))==true)list.Add(new(f.Acceptance,owner,known?(owner==f.Terms.A?f.Terms.B:f.Terms.A):null,previous,head,known?f.Terms.Until:null,can));
        }return list.ToArray();
    }
    internal static void Validate(ContractWorld w){if(w.TruceInheritance==null)return;Need(HouseSuccession.Enabled(w)&&TruceNegotiations.Enabled(w)&&w.TruceInheritance is {Schema:1,Policy:Policy,Entries:not null},"不戦引継ぎの版が不正です");long at=0,rev=0;var ids=new HashSet<EntityId>();foreach(var e in w.TruceInheritance.Entries){Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&ids.Add(e.Input.Id)&&!FactionStances.OtherIdUsed(w,e.Input.Id,includeTruceInheritance:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id)&&e==Make(w,e.Input,e.At,e.Revision),"不戦引継ぎの当事者・知識・原本が一致しません");at=e.At;rev=e.Revision;}}
    internal static IEnumerable<(long At,long Revision)> Inputs(ContractWorld w)=>w.TruceInheritance?.Entries.Select(e=>(e.At,e.Revision))??[];
    internal static void Rewind(ContractWorld w,long at,long rev){if(w.TruceInheritance is {} s)w.TruceInheritance=s with{Entries=s.Entries.Where(e=>Before(e,at,rev)).ToArray()};}
}
public sealed partial class WorldSession
{
    public TruceSuccessionCard[] TruceSuccessionView(){lock(gate)return TruceSuccession.View(state);}
    public CommandResult ReadInheritedTruce(TruceSuccessionInput i,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl)return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,"本人の引継ぎ確認ではありません");var old=state.TruceInheritance?.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);if(old!=null)return new(old.Input==i?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"確認済みです");if(FactionStances.OtherIdUsed(state,i.Id)||FactionStances.OwnIdUsed(state,i.Id))return new(ResultCode.IdPayloadConflict,i.Id,state.CommitSequence,"別の原本です");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,i.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>TruceSuccession.Apply(w,i));return new(ResultCode.Accepted,i.Id,state.CommitSequence,"元の条件と期限を新当主が現地で確認しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,i.Id,state.CommitSequence,e.Message);}}}
}
