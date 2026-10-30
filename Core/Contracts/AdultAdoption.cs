namespace HakutaiKagami.Core.Contracts;
public enum AdoptionAction { Propose, PersonalYes, PersonalNo, ApproveHouse, Implement, Withdraw }
public sealed record AdoptionInput(EntityId Id,AdoptionAction Action,EntityId? Other=null,bool AsParent=true,EntityId? Original=null,EntityId? House=null);
public sealed record AdoptionHouse(EntityId Owner,EntityId Representative,long ObservedAt,string Source);
public sealed record AdoptionTerms(EntityId Id,EntityId Parent,EntityId Child,EntityId Site,long Until,AdoptionHouse[] Houses);
public sealed record AdoptionEntry(AdoptionInput Input,EntityId Actor,long At,long Revision,AdoptionTerms? Terms);
public sealed record AdoptionState(int Schema,string Policy,AdoptionEntry[] Entries);
public sealed record AdoptionCard(EntityId Id,EntityId Parent,EntityId Child,EntityId Site,string Status,bool CanImplement,EntityId[] OwnHouseApprovals,bool CanWithdraw);
public static class AdultAdoption
{
    public const string Policy="adult-local-adoption-personal-house-separate-implementation-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.AdoptionPolicy==Policy;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Adoption=new(1,Policy,[]);}
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Adoption?.Entries.Any(e=>e.Input.Id==id)==true;
    static AdoptionEntry[] Entries(ContractWorld w)=>w.Adoption?.Entries??[];
    static AdoptionEntry[] Case(AdoptionEntry[] rows,EntityId root)=>rows.Where(e=>e.Input.Id==root||e.Input.Original==root).ToArray();
    static bool Closed(AdoptionEntry[] rows,AdoptionTerms t,long at)=>at>t.Until||Case(rows,t.Id).Any(e=>e.Input.Action is AdoptionAction.PersonalNo or AdoptionAction.Withdraw or AdoptionAction.Implement);
    static bool Present(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&PersonalCompanions.SiteAt(w,actor,at,rev)==site&&!NpcArmies.CommanderBusyAt(w,actor,at,rev);
    static string? AuthoredHouse(ContractWorld w,EntityId person)=>w.NewGame!.Start.People.Single(p=>p.Id==person.Value).Faction;
    static bool SameHouse(ContractWorld w,EntityId person,long at,long rev)=>w.Memberships.Where(m=>m.Subject==person&&m.From<=at&&(at<m.Until||at==m.Until&&rev<m.Revision)).Select(m=>m.Target.Value).SingleOrDefault()==AuthoredHouse(w,person);
    static AdoptionHouse[] KnownHouses(ContractWorld w,EntityId parent,EntityId child,long at,long rev)=>new[]{parent,child}.Select(p=>AuthoredHouse(w,p)).Where(o=>o!=null).Distinct().Select(o=>{
        var known=RepresentativeKnowledge.Known(w,w.PlayerControl,new(o!),at,Math.Min(rev,w.CommitSequence))??throw new InvalidDataException("家の代表者を確認できません");return new AdoptionHouse(new(o!),known.Person,known.ObservedAt,known.Source);
    }).OrderBy(h=>h.Owner.Value,StringComparer.Ordinal).ToArray();
    static bool FamilyAvailable(AdoptionEntry[] rows,EntityId parent,EntityId child){
        var actual=rows.Where(e=>e.Input.Action==AdoptionAction.Implement).Select(e=>rows.Single(q=>q.Input.Id==e.Input.Original).Terms!).ToArray();if(actual.Any(t=>t.Child==child))return false;
        var ancestors=new HashSet<EntityId>();var next=parent;while(ancestors.Add(next)){if(next==child)return false;var p=actual.SingleOrDefault(t=>t.Child==next);if(p==null)return true;next=p.Parent;}return false;
    }
    static bool IndividualAccepts(ContractWorld w,EntityId actor,EntityId other,long at,long rev){
        if(!w.Characters.Single(c=>c.Id==actor).WillObey)return false;var score=CharacterRelations.DiagnosticScores(w,actor,other,at,rev);
        return CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Duty)+CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Affection)+score.Trust-score.Hostility>=80;
    }
    static bool Ready(ContractWorld w,AdoptionTerms t,AdoptionEntry[] rows,long at,long rev){
        if(Closed(rows,t,at)||!Present(w,t.Parent,t.Site,at,rev)||!Present(w,t.Child,t.Site,at,rev)||!SameHouse(w,t.Parent,at,rev)||!SameHouse(w,t.Child,at,rev))return false;
        var own=Case(rows,t.Id);if(!own.Any(e=>e.Input.Action==AdoptionAction.PersonalYes))return false;
        return t.Houses.All(h=>own.Any(e=>e.Input.Action==AdoptionAction.ApproveHouse&&e.Input.House==h.Owner&&e.Actor==h.Representative)&&Present(w,h.Representative,t.Site,at,rev)&&FactionStances.ResolveAuthority(w,h.Representative,h.Owner,at,Math.Min(rev,w.CommitSequence))!=null);
    }
    static void Check(ContractWorld w,AdoptionEntry e,AdoptionEntry[] prior){
        var i=e.Input;Need(Enabled(w)&&Enum.IsDefined(i.Action)&&EntityId.IsValid(i.Id.Value)&&e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence,"Invalid adoption record");
        Need(!prior.Any(p=>p.Input.Id==i.Id)&&!FactionStances.OtherIdUsed(w,i.Id,includeAdoption:false)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使ったIDです");
        if(i.Action==AdoptionAction.Propose){
            Need(e.Actor==w.PlayerControl&&i.Other!=null&&i.Other!=e.Actor&&i.Original==null&&i.House==null,"本人の養子相談を指定してください");var t=e.Terms;Need(t!=null&&t.Id==i.Id&&t.Parent==(i.AsParent?e.Actor:i.Other.Value)&&t.Child==(i.AsParent?i.Other.Value:e.Actor)&&t.Until==e.At+7*86400,"養子の役割・期限が不正です");
            Need(Present(w,t.Parent,t.Site,e.At,e.Revision)&&Present(w,t.Child,t.Site,e.At,e.Revision)&&SameHouse(w,t.Parent,e.At,e.Revision)&&SameHouse(w,t.Child,e.At,e.Revision),"双方の成人・所在と既知の所属を確認してください");
            Need(t.Houses.SequenceEqual(KnownHouses(w,t.Parent,t.Child,e.At,e.Revision-1)),"家の既知代表者が一致しません");
            Need(FamilyAvailable(prior,t.Parent,t.Child)&&!prior.Where(q=>q.Terms!=null).Any(q=>q.Terms!.Child==t.Child&&!Closed(prior,q.Terms,e.At)),"既存の養子関係または相談と競合します");return;
        }
        Need(i.Other==null&&i.AsParent&&i.Original!=null&&e.Terms==null,"合意原本を指定してください");var proposal=prior.SingleOrDefault(p=>p.Input.Id==i.Original&&p.Terms!=null);Need(proposal!=null,"原本がありません");var terms=proposal.Terms!;var current=Case(prior,terms.Id);Need(!Closed(prior,terms,e.At),"相談は終了しています");
        if(i.Action is AdoptionAction.PersonalYes or AdoptionAction.PersonalNo){var actor=proposal.Actor==terms.Parent?terms.Child:terms.Parent;Need(e.Actor==actor&&actor!=w.PlayerControl&&i.House==null&&e.At>=proposal.At+1&&!current.Any(p=>p.Input.Action is AdoptionAction.PersonalYes or AdoptionAction.PersonalNo),"本人返答が重複または未着です");Need(Present(w,actor,terms.Site,e.At,e.Revision)&&Present(w,proposal.Actor,terms.Site,e.At,e.Revision),"本人への現地相談が必要です");bool yes=SameHouse(w,actor,e.At,e.Revision)&&IndividualAccepts(w,actor,proposal.Actor,e.At,e.Revision);Need((i.Action==AdoptionAction.PersonalYes)==yes,"本人の意思が一致しません");}
        else if(i.Action==AdoptionAction.ApproveHouse){var h=terms.Houses.SingleOrDefault(h=>h.Owner==i.House&&h.Representative==e.Actor);Need(h!=null&&current.Any(p=>p.Input.Action==AdoptionAction.PersonalYes)&&!current.Any(p=>p.Input.Action==AdoptionAction.ApproveHouse&&p.Input.House==h.Owner),"家の承諾者・順序が不正です");Need(Present(w,e.Actor,terms.Site,e.At,e.Revision)&&Present(w,proposal.Actor,terms.Site,e.At,e.Revision)&&FactionStances.ResolveAuthority(w,e.Actor,h.Owner,e.At,e.Revision)!=null,"家の現地承諾または権限がありません");Need(e.Actor==w.PlayerControl||w.Characters.Single(c=>c.Id==e.Actor).WillObey,"家の承諾はありません");}
        else if(i.Action==AdoptionAction.Implement){Need(e.Actor==w.PlayerControl&&i.House==null&&Ready(w,terms,prior,e.At,e.Revision)&&FamilyAvailable(prior,terms.Parent,terms.Child),"当人・家の承諾、所在、権限を再確認してください");}
        else if(i.Action==AdoptionAction.Withdraw)Need(e.Actor==proposal.Actor&&i.House==null,"打診した本人だけが取り下げられます");
        else throw new InvalidDataException("Unknown adoption action");
    }
    static void Add(ContractWorld w,AdoptionInput i,EntityId actor,AdoptionTerms? terms=null){var e=new AdoptionEntry(i,actor,w.ElapsedSeconds,w.CommitSequence,terms);Check(w,e,Entries(w));w.Adoption=w.Adoption! with{Entries=[..w.Adoption.Entries,e]};}
    internal static void Apply(ContractWorld w,AdoptionInput i){
        Need(i.Action is AdoptionAction.Propose or AdoptionAction.ApproveHouse or AdoptionAction.Implement or AdoptionAction.Withdraw,"NPCの意思を代行できません");AdoptionTerms? t=null;
        if(i.Action==AdoptionAction.Propose){Need(i.Other!=null&&w.Characters.Any(c=>c.Id==i.Other),"相手を指定してください");var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence)??throw new InvalidDataException("先に用件・移動を終えてください");var parent=i.AsParent?w.PlayerControl:i.Other.Value;var child=i.AsParent?i.Other.Value:w.PlayerControl;t=new(i.Id,parent,child,site,w.ElapsedSeconds+7*86400,KnownHouses(w,parent,child,w.ElapsedSeconds,w.CommitSequence-1));}
        Add(w,i,w.PlayerControl,t);
    }
    internal static void Update(ContractWorld w){
        if(!Enabled(w))return;
        foreach(var proposal in Entries(w).Where(e=>e.Terms!=null).ToArray()){
            var t=proposal.Terms!;if(Closed(Entries(w),t,w.ElapsedSeconds)||w.ElapsedSeconds<proposal.At+1)continue;var actor=proposal.Actor==t.Parent?t.Child:t.Parent;var current=Case(Entries(w),t.Id);
            if(!current.Any(e=>e.Input.Action is AdoptionAction.PersonalYes or AdoptionAction.PersonalNo)&&Present(w,actor,t.Site,w.ElapsedSeconds,w.CommitSequence)&&Present(w,proposal.Actor,t.Site,w.ElapsedSeconds,w.CommitSequence)){
                bool yes=SameHouse(w,actor,w.ElapsedSeconds,w.CommitSequence)&&IndividualAccepts(w,actor,proposal.Actor,w.ElapsedSeconds,w.CommitSequence);var id=new EntityId("request:adoption-person/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(t.Id.Value)));Add(w,new(id,yes?AdoptionAction.PersonalYes:AdoptionAction.PersonalNo,Original:t.Id),actor);
            }
            current=Case(Entries(w),t.Id);if(!current.Any(e=>e.Input.Action==AdoptionAction.PersonalYes))continue;
            foreach(var h in t.Houses.Where(h=>h.Representative!=w.PlayerControl&&!current.Any(e=>e.Input.Action==AdoptionAction.ApproveHouse&&e.Input.House==h.Owner))){
                if(!Present(w,h.Representative,t.Site,w.ElapsedSeconds,w.CommitSequence)||!Present(w,proposal.Actor,t.Site,w.ElapsedSeconds,w.CommitSequence)||FactionStances.ResolveAuthority(w,h.Representative,h.Owner,w.ElapsedSeconds,w.CommitSequence)==null||!w.Characters.Single(c=>c.Id==h.Representative).WillObey)continue;
                var id=new EntityId("request:adoption-house/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(t.Id.Value+"/"+h.Owner.Value)));Add(w,new(id,AdoptionAction.ApproveHouse,Original:t.Id,House:h.Owner),h.Representative);
            }
        }
    }
    internal static long? NextBoundary(ContractWorld w,long target){if(!Enabled(w))return null;return Entries(w).Where(e=>e.Terms!=null&&!Closed(Entries(w),e.Terms!,w.ElapsedSeconds)).SelectMany(e=>new[]{e.At+1,e.Terms!.Until+1}).Where(t=>t>w.ElapsedSeconds&&t<=target).Select(t=>(long?)t).Min();}
    public static AdoptionCard[] View(ContractWorld w){if(!Enabled(w))return [];return Entries(w).Where(e=>e.Terms!=null).Select(p=>{var t=p.Terms!;var rows=Case(Entries(w),t.Id);bool closed=Closed(Entries(w),t,w.ElapsedSeconds);string status=rows.Any(e=>e.Input.Action==AdoptionAction.Implement)?"養子縁組を現地で実施済み（家督・所属は別）":rows.Any(e=>e.Input.Action==AdoptionAction.Withdraw)?"打診を取下げ":rows.Any(e=>e.Input.Action==AdoptionAction.PersonalNo)?"本人が辞退":w.ElapsedSeconds>t.Until?"期限切れ":rows.Any(e=>e.Input.Action==AdoptionAction.PersonalYes)?"本人合意済み・家の承諾と実施を確認":"本人への現地相談待ち";return new AdoptionCard(t.Id,t.Parent,t.Child,t.Site,status,Ready(w,t,Entries(w),w.ElapsedSeconds,w.CommitSequence+1),closed||!rows.Any(e=>e.Input.Action==AdoptionAction.PersonalYes)?[]:t.Houses.Where(h=>h.Representative==w.PlayerControl&&!rows.Any(e=>e.Input.Action==AdoptionAction.ApproveHouse&&e.Input.House==h.Owner)).Select(h=>h.Owner).ToArray(),!closed);}).ToArray();}
    public static EntityId[] Candidates(ContractWorld w){if(!Enabled(w))return [];var site=PersonalCompanions.SiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);return site==null?[]:w.Characters.Where(c=>c.Id!=w.PlayerControl&&Present(w,c.Id,site.Value,w.ElapsedSeconds,w.CommitSequence+1)).Select(c=>c.Id).ToArray();}
    public static void Validate(ContractWorld w,ContractWorld? trustedPrior=null){if(!Enabled(w)){Need(w.Adoption==null,"Adoption without policy");return;}Need(w.Adoption is {Schema:1,Policy:Policy,Entries:not null},"Missing adoption state");var rows=Entries(w);if(trustedPrior?.Adoption is {} old)Need(ContractJson.Write(rows.Take(old.Entries.Length).ToArray()).SequenceEqual(ContractJson.Write(old.Entries)),"Prior adoption entries changed");Need(rows.All(e=>e!=null)&&rows.Select(e=>e.Input.Id).Distinct().Count()==rows.Length,"Duplicate adoption record");for(int n=0;n<rows.Length;n++){if(n>0)Need(rows[n].At>=rows[n-1].At&&rows[n].Revision>=rows[n-1].Revision,"Adoption order changed");Check(w,rows[n],rows.Take(n).ToArray());}}
}
public sealed partial class WorldSession
{
    public CommandResult DecideAdoption(AdoptionInput input,EntityId actor,long revision){lock(gate){
        if(actor!=state.PlayerControl||!AdultAdoption.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の養子相談ではありません");
        var old=state.Adoption!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input&&old.Actor==actor?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"記録済みです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>AdultAdoption.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人の判断を記録しました。家の承諾と実施は別です");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
