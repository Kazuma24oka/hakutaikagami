namespace HakutaiKagami.Core.Contracts;

public enum FactionStanceAction { DeclareHostile, WithdrawOwnDeclaration }
public sealed record FactionStanceInput(EntityId Id,FactionStanceAction Action,EntityId Owner,EntityId Target,
    long EffectiveAt,long Until,EntityId? Original=null);
public sealed record FactionStanceAuthority(EntityId Issuer,EntityId Owner,EntityId Home,
    EntityId Membership,EntityId Office,EntityId Control,long At,long Revision);
public sealed record FactionStanceDecision(FactionStanceInput Input,long At,long Revision,FactionStanceAuthority Authority);
public sealed record FactionStanceState(int Schema,string Contract,FactionStanceDecision[] Decisions);
public sealed record FactionStanceClaim(EntityId Declaration,EntityId FromOwner,EntityId ToOwner,
    long EffectiveFrom,long Until,long ObservedAt);

// An owner's declared hostile intent is world truth, not a bilateral treaty, received order,
// global knowledge, or permission to spawn armies. Ending one's own declaration does not end
// the opponent's declaration. There are no implicit starting enemies.
public static class FactionStances
{
    public const string Contract="head-authority-directed-hostility-world-truth-own-withdrawal-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text)
    {if(!ok)throw new InvalidDataException(text);}
    static bool Before(FactionStanceDecision d,long at,long rev)=>d.At<at||d.At==at&&d.Revision<=rev;
    static bool Active(TimedRelation r,long at,long rev)=>r.From<=at&&(at<r.Until||at==r.Until&&rev<r.Revision);
    static FactionStanceDecision[] At(ContractWorld w,long at,long rev)=>w.FactionStances?.Decisions.Where(d=>Before(d,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.FactionStances?.Decisions.Any(d=>d.Input.Id==id)==true;
    internal static bool OtherIdUsed(ContractWorld w,EntityId id,bool includeNpc=true,bool includeNotices=true,bool includeEconomy=true,bool includeCompanions=true,bool includeTruces=true,bool includeInformation=true,bool includeStanddown=true)=>includeStanddown&&PrivateStanddown.OwnIdUsed(w,id)||includeInformation&&PrivateInformation.OwnIdUsed(w,id)||includeTruces&&TruceNegotiations.OwnIdUsed(w,id)||includeCompanions&&PersonalCompanions.OwnIdUsed(w,id)||includeEconomy&&RegionalEconomy.OwnIdUsed(w,id)||includeNotices&&FactionNotices.OwnIdUsed(w,id)||includeNpc&&NpcArmies.Musters(w).Any(m=>m.Id==id)||w.Requests.Any(r=>r.Id==id)||
        NewGameService.OwnIdUsed(w,id)||w.TravelDecisions?.Any(d=>d.Request.Id==id)==true||
        NewGameMilitary.LedgerStates(w).Any(s=>s.Requests.Any(q=>q.Input.Id==id))||
        w.WorkCycle?.Duties?.Any(a=>a.Id==id)==true||w.WorkCycle?.Payments.Any(p=>p.Id==id)==true||
        TransportWork.Jobs(w).Any(j=>j.Handover.Terms.Id==id||j.Handover.Entries.Any(e=>e.Input.Id==id));
    // Political authority is independent of local physical troop handover. A head may decide
    // while travelling. An ordinary retainer's placement grant is not political authority.
    public static FactionStanceAuthority? ResolveAuthority(ContractWorld w,EntityId issuer,EntityId owner,long at,long revision)
    {
        if(!NewGameWorld.Enabled(w)||at<0||at>w.ElapsedSeconds||revision<0||revision>w.CommitSequence||
            w.Characters.SingleOrDefault(c=>c.Id==issuer)?.Life!=LifeState.Adult)return null;
        var faction=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Id==owner.Value&&f.Head==issuer.Value);if(faction==null)return null;
        var home=new EntityId(faction.Home);
        var member=w.Memberships.SingleOrDefault(r=>r.Subject==issuer&&r.Target==owner&&r.Kind=="membership"&&Active(r,at,revision));
        var office=w.Offices.SingleOrDefault(r=>r.Subject==issuer&&r.Target==home&&r.Kind=="house-head"&&Active(r,at,revision));
        var control=w.LandRights.SingleOrDefault(r=>r.Subject==owner&&r.Target==home&&r.Kind=="control"&&Active(r,at,revision));
        return member==null||office==null||control==null?null:new(issuer,owner,home,member.Id,office.Id,control.Id,at,revision);
    }
    static long End(FactionStanceDecision original,IEnumerable<FactionStanceDecision> all)=>
        Math.Min(original.Input.Until,all.Where(d=>d.Input.Action==FactionStanceAction.WithdrawOwnDeclaration&&d.Input.Original==original.Input.Id).Select(d=>d.Input.EffectiveAt).DefaultIfEmpty(long.MaxValue).Min());
    static void CheckInput(ContractWorld w,FactionStanceInput input,EntityId issuer,long at,long revision,FactionStanceDecision[] prior)
    {
        Need(NewGameWorld.Enabled(w)&&EntityId.IsValid(input.Id.Value)&&Enum.IsDefined(input.Action)&&
            EntityId.IsValid(input.Owner.Value)&&EntityId.IsValid(input.Target.Value)&&input.Owner!=input.Target&&
            w.NewGame!.Start.Factions.Any(f=>f.Id==input.Target.Value)&&input.EffectiveAt>=at,
            "敵対意思の操作・対象・効力時刻が不正です");
        Need(!OtherIdUsed(w,input.Id)&&!prior.Any(d=>d.Input.Id==input.Id),"別の操作で使ったIDです");
        Need(ResolveAuthority(w,issuer,input.Owner,at,revision)!=null,"その家の政治判断を行う当主権限がありません");
        if(input.Action==FactionStanceAction.DeclareHostile){
            Need(input.Original==null&&input.Until>input.EffectiveAt,"敵対意思の期間が不正です");
            Need(!prior.Where(d=>d.Input.Action==FactionStanceAction.DeclareHostile&&d.Input.Owner==input.Owner&&d.Input.Target==input.Target)
                .Any(d=>Math.Max(d.Input.EffectiveAt,input.EffectiveAt)<Math.Min(End(d,prior),input.Until)),"同じ期間の敵対意思はすでに記録されています");
        }else{
            Need(input.Until==0&&input.Original is {} id&&EntityId.IsValid(id.Value),"撤回する自家の原判断が必要です");
            var original=prior.SingleOrDefault(d=>d.Input.Id==input.Original&&d.Input.Action==FactionStanceAction.DeclareHostile);
            Need(original!=null&&original.Input.Owner==input.Owner&&original.Input.Target==input.Target&&
                !prior.Any(d=>d.Input.Original==original.Input.Id)&&input.EffectiveAt<original.Input.Until,
                "他家の意思・終了済みの意思・別の対象を撤回できません");
        }
    }
    // Authoritative NPC controllers may call this domain mutation; the user-facing session
    // below separately requires the controlled actor. This method still proves actual head rights.
    public static void Apply(ContractWorld w,FactionStanceInput input,EntityId issuer)
    {
        var prior=w.FactionStances?.Decisions??[];CheckInput(w,input,issuer,w.ElapsedSeconds,w.CommitSequence,prior);
        var source=ResolveAuthority(w,issuer,input.Owner,w.ElapsedSeconds,w.CommitSequence)!;
        w.FactionStances=new(1,Contract,[..prior,new(input,w.ElapsedSeconds,w.CommitSequence,source)]);
    }
    public static HostilityInterval[] Truth(ContractWorld w,long at)
    {
        Need(at>=0&&at<=w.ElapsedSeconds,"未確定の未来の敵対状態は参照しません");var all=At(w,at,w.CommitSequence);
        return all.Where(d=>d.Input.Action==FactionStanceAction.DeclareHostile&&d.Input.EffectiveAt<=at&&at<End(d,all))
            .OrderBy(d=>d.Input.Owner.Value,StringComparer.Ordinal).ThenBy(d=>d.Input.Target.Value,StringComparer.Ordinal)
            .Select(d=>new HostilityInterval(d.Input.Owner,d.Input.Target,d.Input.EffectiveAt,End(d,all))).ToArray();
    }
    public static FactionStanceClaim? ClaimAt(ContractWorld w,EntityId owner,EntityId target,long at,long revision)
    {
        Need(at>=0&&at<=w.ElapsedSeconds&&revision>=0&&revision<=w.CommitSequence,"未確定の敵対情報は参照しません");var all=At(w,at,revision);var d=all.SingleOrDefault(d=>d.Input.Action==FactionStanceAction.DeclareHostile&&
            d.Input.Owner==owner&&d.Input.Target==target&&d.Input.EffectiveAt<=at&&at<End(d,all));
        return d==null?null:new(d.Input.Id,owner,target,d.Input.EffectiveAt,End(d,all),at);
    }
    // Membership alone never reveals the head's decision. Physical reports/permission quotations
    // are added separately; this first path knows only one's own political decisions.
    public static FactionStanceClaim[] KnownOwnDeclarations(ContractWorld w,EntityId observer)
    {
        return OwnAt(w,observer,w.ElapsedSeconds,w.CommitSequence);
    }
    public static FactionStanceClaim[] OwnAt(ContractWorld w,EntityId observer,long at,long revision)
    {
        Need(at>=0&&at<=w.ElapsedSeconds&&revision>=0&&revision<=w.CommitSequence,"未確定の政治情報は参照しません");var own=At(w,at,revision).Where(d=>d.Authority.Issuer==observer).ToArray();
        return own.Where(d=>d.Input.Action==FactionStanceAction.DeclareHostile).Select(d=>new FactionStanceClaim(d.Input.Id,d.Input.Owner,d.Input.Target,d.Input.EffectiveAt,End(d,own),at)).ToArray();
    }
    internal static long? NextBoundary(ContractWorld w,long target)=>At(w,w.ElapsedSeconds,w.CommitSequence)
        .SelectMany(d=>new[]{d.Input.EffectiveAt,d.Input.Until}).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    public static FactionStanceClaim[] Known(ContractWorld w,EntityId observer)
    {
        var result=KnownOwnDeclarations(w,observer).Concat(FactionNotices.Known(w,observer)).ToList();
        if(observer==w.PlayerControl)foreach(var ledger in NewGameMilitary.LedgerStates(w))foreach(var opened in ledger.Openings){
            if(opened.At>w.ElapsedSeconds)continue;
            var m=w.Messaging.Messages.Single(m=>m.Id==opened.Message);
            var n=ContractJson.Read<MilitaryNotice>(System.Text.Encoding.UTF8.GetBytes(m.Body));
            if(n.Engagement is {Hostility:{} claim} terms&&terms.Agent==observer)result.Add(claim);
        }
        return result.GroupBy(c=>c.Declaration).Select(g=>g.OrderByDescending(c=>c.ObservedAt).ThenBy(c=>c.Until).First())
            .OrderBy(c=>c.FromOwner.Value,StringComparer.Ordinal).ThenBy(c=>c.ToOwner.Value,StringComparer.Ordinal).ThenBy(c=>c.Declaration.Value,StringComparer.Ordinal).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        var s=w.FactionStances;if(s==null)return;
        Need(NewGameWorld.Enabled(w)&&s.Schema==1&&s.Contract==Contract&&s.Decisions!=null&&s.Decisions.Length>0,"敵対意思の保存形式が不正です");
        var prior=new List<FactionStanceDecision>();
        foreach(var d in s.Decisions){
            Need(d!=null&&d.Input!=null&&d.Authority!=null&&d.At>=0&&d.At<=w.ElapsedSeconds&&d.Revision>0&&d.Revision<=w.CommitSequence&&
                (prior.Count==0||d.At>=prior[^1].At&&d.Revision>prior[^1].Revision),"政治判断の順序・時点が不正です");
            CheckInput(w,d.Input,d.Authority.Issuer,d.At,d.Revision,prior.ToArray());
            Need(d.Authority==ResolveAuthority(w,d.Authority.Issuer,d.Input.Owner,d.At,d.Revision),"記録時の当主・所属・役職・支配の根拠が一致しません");prior.Add(d);
        }
    }
}
