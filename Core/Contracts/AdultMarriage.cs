using static HakutaiKagami.Core.Contracts.FamilyConsentRules;
namespace HakutaiKagami.Core.Contracts;
public enum MarriageAction { Propose, PersonalYes, PersonalNo, ApproveHouse, Implement, Withdraw }
public sealed record MarriageInput(EntityId Id,MarriageAction Action,EntityId? Other=null,EntityId? Original=null,EntityId? House=null);
public sealed record MarriageTerms(EntityId Id,EntityId First,EntityId Second,EntityId Site,long Until,AdoptionHouse[] Houses);
public sealed record MarriageEntry(MarriageInput Input,EntityId Actor,long At,long Revision,MarriageTerms? Terms);
public sealed record MarriageState(int Schema,string Policy,MarriageEntry[] Entries);
public sealed record MarriageCard(EntityId Id,EntityId First,EntityId Second,EntityId Site,string Status,bool CanImplement,EntityId[] OwnHouseApprovals,bool CanWithdraw);
public static class AdultMarriage
{
    public const string Policy="adult-local-marriage-personal-house-separate-implementation-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.MarriagePolicy==Policy;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Marriage=new(1,Policy,[]);}
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Marriage?.Entries.Any(e=>e.Input.Id==id)==true;
    static MarriageEntry[] Entries(ContractWorld w)=>w.Marriage?.Entries??[];
    static MarriageEntry[] Case(MarriageEntry[] rows,EntityId root)=>rows.Where(e=>e.Input.Id==root||e.Input.Original==root).ToArray();
    static bool Closed(MarriageEntry[] rows,MarriageTerms t,long at)=>at>t.Until||Case(rows,t.Id).Any(e=>e.Input.Action is MarriageAction.PersonalNo or MarriageAction.Withdraw or MarriageAction.Implement);
    static bool FamilyAvailable(ContractWorld w,MarriageEntry[] rows,EntityId first,EntityId second,long at,long rev){
        if(first==second||Related(w,first,second,at,rev))return false;
        if(new[]{first,second}.Any(id=>w.NewGame!.Registered.SingleOrDefault(p=>p.Id==id.Value)?.InitialMaritalStatus!=InitialMaritalStatus.Unmarried))return false;
        return !rows.Where(p=>p.Terms!=null).Any(p=>{var events=Case(rows,p.Input.Id);bool active=events.Any(e=>e.Input.Action==MarriageAction.Implement)||!Closed(rows,p.Terms!,at);return active&&(p.Terms!.First==first||p.Terms.Second==first||p.Terms.First==second||p.Terms.Second==second);});
    }
    static bool Ready(ContractWorld w,MarriageTerms t,MarriageEntry[] rows,long at,long rev){
        if(Closed(rows,t,at)||!Present(w,t.First,t.Site,at,rev)||!Present(w,t.Second,t.Site,at,rev)||!SameHouse(w,t.First,at,rev)||!SameHouse(w,t.Second,at,rev))return false;
        var own=Case(rows,t.Id);if(!own.Any(e=>e.Input.Action==MarriageAction.PersonalYes))return false;
        return t.Houses.All(h=>own.Any(e=>e.Input.Action==MarriageAction.ApproveHouse&&e.Input.House==h.Owner&&e.Actor==h.Representative)&&Present(w,h.Representative,t.Site,at,rev)&&FactionStances.ResolveAuthority(w,h.Representative,h.Owner,at,Math.Min(rev,w.CommitSequence))!=null);
    }
    static void Check(ContractWorld w,MarriageEntry e,MarriageEntry[] prior){
        var i=e.Input;Need(Enabled(w)&&Enum.IsDefined(i.Action)&&EntityId.IsValid(i.Id.Value)&&e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence,"Invalid marriage record");
        Need(!prior.Any(p=>p.Input.Id==i.Id)&&!FactionStances.OtherIdUsed(w,i.Id,includeMarriage:false)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使ったIDです");
        if(i.Action==MarriageAction.Propose){
            Need(e.Actor==w.PlayerControl&&i.Other!=null&&i.Other!=e.Actor&&i.Original==null&&i.House==null,"本人の婚姻相談を指定してください");var t=e.Terms;Need(t!=null&&t.Id==i.Id&&t.First==e.Actor&&t.Second==i.Other.Value&&t.Until==e.At+7*86400,"婚姻の役割・期限が不正です");
            Need(Present(w,t.First,t.Site,e.At,e.Revision)&&Present(w,t.Second,t.Site,e.At,e.Revision)&&SameHouse(w,t.First,e.At,e.Revision)&&SameHouse(w,t.Second,e.At,e.Revision),"双方の成人・所在と既知の所属を確認してください");
            Need(t.Houses.SequenceEqual(KnownHouses(w,t.First,t.Second,e.At,e.Revision-1)),"家の既知代表者が一致しません");
            Need(FamilyAvailable(w,prior,t.First,t.Second,e.At,e.Revision)&&!prior.Where(q=>q.Terms!=null).Any(q=>q.Terms!.Second==t.Second&&!Closed(prior,q.Terms,e.At)),"既知の未婚・親族・他の婚姻相談を確認してください");return;
        }
        Need(i.Other==null&&i.Original!=null&&e.Terms==null,"合意原本を指定してください");var proposal=prior.SingleOrDefault(p=>p.Input.Id==i.Original&&p.Terms!=null);Need(proposal!=null,"原本がありません");var terms=proposal.Terms!;var current=Case(prior,terms.Id);Need(!Closed(prior,terms,e.At),"相談は終了しています");
        if(i.Action is MarriageAction.PersonalYes or MarriageAction.PersonalNo){var actor=proposal.Actor==terms.First?terms.Second:terms.First;Need(e.Actor==actor&&actor!=w.PlayerControl&&i.House==null&&e.At>=proposal.At+1&&!current.Any(p=>p.Input.Action is MarriageAction.PersonalYes or MarriageAction.PersonalNo),"本人返答が重複または未着です");Need(Present(w,actor,terms.Site,e.At,e.Revision)&&Present(w,proposal.Actor,terms.Site,e.At,e.Revision),"本人への現地相談が必要です");bool yes=SameHouse(w,actor,e.At,e.Revision)&&IndividualAccepts(w,actor,proposal.Actor,e.At,e.Revision);Need((i.Action==MarriageAction.PersonalYes)==yes,"本人の意思が一致しません");}
        else if(i.Action==MarriageAction.ApproveHouse){var h=terms.Houses.SingleOrDefault(h=>h.Owner==i.House&&h.Representative==e.Actor);Need(h!=null&&current.Any(p=>p.Input.Action==MarriageAction.PersonalYes)&&!current.Any(p=>p.Input.Action==MarriageAction.ApproveHouse&&p.Input.House==h.Owner),"家の承諾者・順序が不正です");Need(Present(w,e.Actor,terms.Site,e.At,e.Revision)&&Present(w,proposal.Actor,terms.Site,e.At,e.Revision)&&FactionStances.ResolveAuthority(w,e.Actor,h.Owner,e.At,e.Revision)!=null,"家の現地承諾または権限がありません");Need(e.Actor==w.PlayerControl||w.Characters.Single(c=>c.Id==e.Actor).WillObey,"家の承諾はありません");}
        else if(i.Action==MarriageAction.Implement){Need(e.Actor==w.PlayerControl&&i.House==null&&Ready(w,terms,prior,e.At,e.Revision)&&FamilyAvailable(w,prior.Where(x=>x.Input.Id!=terms.Id&&x.Input.Original!=terms.Id).ToArray(),terms.First,terms.Second,e.At,e.Revision),"当人・家の承諾、所在、権限を再確認してください");}
        else if(i.Action==MarriageAction.Withdraw)Need(e.Actor==proposal.Actor&&i.House==null,"打診した本人だけが取り下げられます");
        else throw new InvalidDataException("Unknown marriage action");
    }
    static void Add(ContractWorld w,MarriageInput i,EntityId actor,MarriageTerms? terms=null){var e=new MarriageEntry(i,actor,w.ElapsedSeconds,w.CommitSequence,terms);Check(w,e,Entries(w));w.Marriage=w.Marriage! with{Entries=[..w.Marriage.Entries,e]};}
    internal static void Apply(ContractWorld w,MarriageInput i){
        Need(i.Action is MarriageAction.Propose or MarriageAction.ApproveHouse or MarriageAction.Implement or MarriageAction.Withdraw,"NPCの意思を代行できません");MarriageTerms? t=null;
        if(i.Action==MarriageAction.Propose){Need(i.Other!=null&&w.Characters.Any(c=>c.Id==i.Other),"相手を指定してください");var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence)??throw new InvalidDataException("先に用件・移動を終えてください");var first=w.PlayerControl;var second=i.Other.Value;t=new(i.Id,first,second,site,w.ElapsedSeconds+7*86400,KnownHouses(w,first,second,w.ElapsedSeconds,w.CommitSequence-1));}
        Add(w,i,w.PlayerControl,t);
    }
    internal static void Update(ContractWorld w){
        if(!Enabled(w))return;
        foreach(var proposal in Entries(w).Where(e=>e.Terms!=null).ToArray()){
            var t=proposal.Terms!;if(Closed(Entries(w),t,w.ElapsedSeconds)||w.ElapsedSeconds<proposal.At+1)continue;var actor=proposal.Actor==t.First?t.Second:t.First;var current=Case(Entries(w),t.Id);
            if(!current.Any(e=>e.Input.Action is MarriageAction.PersonalYes or MarriageAction.PersonalNo)&&Present(w,actor,t.Site,w.ElapsedSeconds,w.CommitSequence)&&Present(w,proposal.Actor,t.Site,w.ElapsedSeconds,w.CommitSequence)){
                bool yes=SameHouse(w,actor,w.ElapsedSeconds,w.CommitSequence)&&IndividualAccepts(w,actor,proposal.Actor,w.ElapsedSeconds,w.CommitSequence);var id=new EntityId("request:marriage-person/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(t.Id.Value)));Add(w,new(id,yes?MarriageAction.PersonalYes:MarriageAction.PersonalNo,Original:t.Id),actor);
            }
            current=Case(Entries(w),t.Id);if(!current.Any(e=>e.Input.Action==MarriageAction.PersonalYes))continue;
            foreach(var h in t.Houses.Where(h=>h.Representative!=w.PlayerControl&&!current.Any(e=>e.Input.Action==MarriageAction.ApproveHouse&&e.Input.House==h.Owner))){
                if(!Present(w,h.Representative,t.Site,w.ElapsedSeconds,w.CommitSequence)||!Present(w,proposal.Actor,t.Site,w.ElapsedSeconds,w.CommitSequence)||FactionStances.ResolveAuthority(w,h.Representative,h.Owner,w.ElapsedSeconds,w.CommitSequence)==null||!w.Characters.Single(c=>c.Id==h.Representative).WillObey)continue;
                var id=new EntityId("request:marriage-house/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(t.Id.Value+"/"+h.Owner.Value)));Add(w,new(id,MarriageAction.ApproveHouse,Original:t.Id,House:h.Owner),h.Representative);
            }
        }
    }
    internal static long? NextBoundary(ContractWorld w,long target){if(!Enabled(w))return null;return Entries(w).Where(e=>e.Terms!=null&&!Closed(Entries(w),e.Terms!,w.ElapsedSeconds)).SelectMany(e=>new[]{e.At+1,e.Terms!.Until+1}).Where(t=>t>w.ElapsedSeconds&&t<=target).Select(t=>(long?)t).Min();}
    public static MarriageCard[] View(ContractWorld w){if(!Enabled(w))return [];return Entries(w).Where(e=>e.Terms!=null).Select(p=>{var t=p.Terms!;var rows=Case(Entries(w),t.Id);bool closed=Closed(Entries(w),t,w.ElapsedSeconds);string status=rows.Any(e=>e.Input.Action==MarriageAction.Implement)?"婚姻を現地で実施済み（家督・所属は別）":rows.Any(e=>e.Input.Action==MarriageAction.Withdraw)?"打診を取下げ":rows.Any(e=>e.Input.Action==MarriageAction.PersonalNo)?"本人が辞退":w.ElapsedSeconds>t.Until?"期限切れ":rows.Any(e=>e.Input.Action==MarriageAction.PersonalYes)?"本人合意済み・家の承諾と実施を確認":"本人への現地相談待ち";return new MarriageCard(t.Id,t.First,t.Second,t.Site,status,Ready(w,t,Entries(w),w.ElapsedSeconds,w.CommitSequence+1)&&FamilyAvailable(w,Entries(w).Where(x=>x.Input.Id!=t.Id&&x.Input.Original!=t.Id).ToArray(),t.First,t.Second,w.ElapsedSeconds,w.CommitSequence+1),closed||!rows.Any(e=>e.Input.Action==MarriageAction.PersonalYes)?[]:t.Houses.Where(h=>h.Representative==w.PlayerControl&&!rows.Any(e=>e.Input.Action==MarriageAction.ApproveHouse&&e.Input.House==h.Owner)).Select(h=>h.Owner).ToArray(),!closed);}).ToArray();}
    public static EntityId[] Candidates(ContractWorld w){if(!Enabled(w))return [];var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);return site==null?[]:w.Characters.Where(c=>c.Id!=w.PlayerControl&&Present(w,c.Id,site.Value,w.ElapsedSeconds,w.CommitSequence+1)&&FamilyAvailable(w,Entries(w),w.PlayerControl,c.Id,w.ElapsedSeconds,w.CommitSequence+1)).Select(c=>c.Id).ToArray();}
    public static void Validate(ContractWorld w,ContractWorld? trustedPrior=null){if(!Enabled(w)){Need(w.Marriage==null,"Marriage without policy");return;}Need(w.Marriage is {Schema:1,Policy:Policy,Entries:not null},"Missing marriage state");var rows=Entries(w);if(trustedPrior?.Marriage is {} old)Need(ContractJson.Write(rows.Take(old.Entries.Length).ToArray()).SequenceEqual(ContractJson.Write(old.Entries)),"Prior marriage entries changed");Need(rows.All(e=>e!=null)&&rows.Select(e=>e.Input.Id).Distinct().Count()==rows.Length,"Duplicate marriage record");for(int n=0;n<rows.Length;n++){if(n>0)Need(rows[n].At>=rows[n-1].At&&rows[n].Revision>=rows[n-1].Revision,"Marriage order changed");Check(w,rows[n],rows.Take(n).ToArray());}}
}
public sealed partial class WorldSession
{
    public CommandResult DecideMarriage(MarriageInput input,EntityId actor,long revision){lock(gate){
        if(actor!=state.PlayerControl||!AdultMarriage.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の婚姻相談ではありません");
        var old=state.Marriage!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input&&old.Actor==actor?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"記録済みです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>AdultMarriage.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人の判断を記録しました。家の承諾と実施は別です");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
