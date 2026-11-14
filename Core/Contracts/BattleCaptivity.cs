using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 合戦での捕縛と処遇（BD-44・C7、2026-11-13〜14）。本人の軍が打ち崩した敵の軍の大将を、固定シードの判定で捕らえる。
// 捕縛は交戦の手番（WorldClashStep.Captures）に記録し、手番の前の世界から同じ結果を再現できることを確かめる。
// 捕らえた人物は捕らえた家の拠点（出陣した城）に留め置かれ、拘束中は新しい行動を始められない（HostageCustody.HeldAt）。捕縛の秒の後の位置は城になる。
// 処遇は、捕らえた家の当主権限を持つ者が、その拠点で明示に選ぶ：解放、登用の誘い、処断。何もしなければ留め置く。自動の処刑・登用・解放はしない。
// 登用に応じるかは捕虜本人の判断（元の家の当主は応じない。それ以外は捕縛ごとに固定の判定）。断られたら同じ捕縛の間は誘い直せない。
// 登用に応じたときに移るのは本人の所属と主従だけ。所領・兵・私財・役職は移さない。
// 処断は自然死と同じ死亡記録（NaturalDeath.Execution）に乗り、訃報・後見・家督の流れを使う。
public sealed record BattleCapture(EntityId Person,EntityId FromOwner,EntityId CaptorOwner,EntityId Holder,EntityId Site,long At);
public enum CaptiveAction { Release,Recruit,Execute,Plead }
// Amount：捕らえられた本人が解放を願って差し出す銭（Plead のときだけ）。
// Exchange：解放を願うとき、引き換えに放つ捕虜（本人の家が捕らえた、相手の家の者）。
public sealed record CaptiveInput(EntityId Id,CaptiveAction Action,EntityId Person,long Amount=0,EntityId? Exchange=null);
// Accepted：登用の誘いに本人が応じたか（他の操作では false）。
public sealed record CaptiveEntry(CaptiveInput Input,EntityId Actor,EntityId Site,long At,long Revision,bool Accepted);
public sealed record CaptiveState(int Schema,string Policy,CaptiveEntry[] Entries,RansomPaper[] Papers,RansomOpening[] Openings);
public sealed record CaptiveCard(EntityId Person,EntityId FromOwner,EntityId Site,long CapturedAt,bool Held,bool CanAct,bool Declined);

public static partial class BattleCaptivity
{
    public const string Policy="battle-capture-routed-commander-explicit-treatment-v2";
    const int Chance=35,Broken=30,Willing=40;
    public static bool Enabled(ContractWorld w)=>w.NewGame?.CapturePolicy==Policy||NationalSystems.On(w,"BattleCaptivity");
    // 本人が捕らえられる規則（全国の一覧の PlayerCapture と、捕縛の検査用の地方の版）。
    static bool PlayerCapture(ContractWorld w)=>w.NewGame?.CapturePolicy==Policy||NationalSystems.On(w,"PlayerCapture");
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Captives=new(1,Policy,[],[],[]);}
    static CaptiveEntry[] Entries(ContractWorld w)=>w.Captives?.Entries??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>Entries(w).Any(e=>e.Input.Id==id);

    // 手番の前の世界で、本人の軍の打撃で全部隊が崩れた（士気30以下になった）敵の軍の大将を判定する。
    internal static BattleCapture[]? Resolve(ContractWorld w,EngagementResult result)
    {
        if(!Enabled(w)||w.NewMilitary is not {} own)return null;
        var mine=w.Units.Where(u=>u.Army==own.Terms.Army).Select(u=>u.Id).ToHashSet();
        var rows=new List<BattleCapture>();
        foreach(var m in NpcArmies.Musters(w).Where(m=>m.At<=result.At&&!w.NpcArmies!.Returns.Any(r=>r.Muster==m.Id)).OrderBy(m=>m.Id.Value,StringComparer.Ordinal))
        {
            var units=NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Units;
            if(!result.Strikes.Any(s=>units.Contains(s.Target)&&mine.Contains(s.Attacker)))continue;
            int Morale(EntityId u,bool after)=>100-WorldClashes.MoraleLost(w,u,result.At)-(after?result.Losses.Where(l=>l.Unit==u).Sum(l=>l.Morale):0);
            if(units.All(u=>Morale(u,false)<=Broken)||!units.All(u=>Morale(u,true)<=Broken))continue;
            if(Assassination.DeadAt(w,m.Commander,result.At,w.CommitSequence)||HostageCustody.HeldAt(w,m.Commander,result.At,w.CommitSequence+1))continue;
            if(Roll("battle-capture-v1",w.Seed,m.Commander,result.At/86400)>=Chance)continue;
            rows.Add(new(m.Commander,m.Owner,own.Terms.Owner,w.PlayerControl,own.Terms.Site,result.At));
        }
        // 本人が率いる本隊（本人が指揮する部隊、なければ最初の部隊）が NPC の軍の打撃で崩れたとき、本人を同じ判定で捕らえる（当てた軍の家が、その軍の城に留め置く）。
        if(PlayerCapture(w)&&mine.Count>0&&!HostageCustody.HeldAt(w,w.PlayerControl,result.At,w.CommitSequence+1)&&!Assassination.DeadAt(w,w.PlayerControl,result.At,w.CommitSequence))
        {
            int Mine(EntityId u,bool after)=>100-WorldClashes.MoraleLost(w,u,result.At)-(after?result.Losses.Where(l=>l.Unit==u).Sum(l=>l.Morale):0);
            var main=w.Units.Where(u=>mine.Contains(u.Id)).OrderBy(u=>u.Commander==w.PlayerControl?0:1).ThenBy(u=>u.Id.Value,StringComparer.Ordinal).First().Id;
            var by=NpcArmies.Musters(w).Where(m=>m.At<=result.At&&!w.NpcArmies!.Returns.Any(r=>r.Muster==m.Id)).OrderBy(m=>m.Id.Value,StringComparer.Ordinal)
                .FirstOrDefault(m=>{var units=NewGameMilitary.LedgerIdentity(m.Owner,m.Site).Units;return result.Strikes.Any(s=>s.Target==main&&units.Contains(s.Attacker));});
            if(by!=null&&Mine(main,false)>Broken&&Mine(main,true)<=Broken&&Roll("battle-capture-v1",w.Seed,w.PlayerControl,result.At/86400)<Chance)
                rows.Add(new(w.PlayerControl,own.Terms.Owner,by.Owner,by.Issuer,by.Site,result.At));
        }
        return rows.Count==0?null:rows.ToArray();
    }
    // 捕らえた人の行動（軍の行軍など）は捕縛の秒で終わる。本人あての報告は出さない（道の途中から出す相手も手段もない）。
    const string CaptureStop="捕縛により行動終了";
    internal static void StopCaptured(ContractWorld w,BattleCapture[]? captures)
    {
        foreach(var c in captures??[])
            foreach(var t in w.Tasks.Where(t=>t.Executor==c.Person&&t.Execution==ExecutionPhase.Running).ToArray())
                WorldSession.Stop(w,t,CaptureStop,report:t.Issuer!=c.Person);
    }
    internal static bool SilentCaptureStop(ContractWorld w,TaskState t)=>Enabled(w)&&t.InternalReason==CaptureStop&&t.Issuer==t.Executor&&
        t.Report==ReportingPhase.NotCreated&&t.FinishedAt is {} at&&Captures(w).Any(x=>x.Capture.Person==t.Executor&&x.Capture.At==at)&&w.Outbox.All(o=>o.Task!=t.Id);
    static int Roll(string kind,ulong seed,EntityId person,long salt)=>
        SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{kind}/{seed}/{person.Value}/{salt}")))[0]%100;
    internal static void Validate(ContractWorld context,WorldClashStep step,EngagementResult result)
    {
        if(ContractJson.CanonicalHash(step.Captures)!=ContractJson.CanonicalHash(Resolve(context,result)))throw new InvalidDataException("交戦時の捕縛を再現できません");
    }

    // 交戦の記録は手番ごとに作り直されるので、記録ごとに捕縛の一覧を覚えておく（所在の判定で頻繁に引く）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldClashState,(BattleCapture,long)[]> cache=new();
    static (BattleCapture Capture,long Revision)[] Captures(ContractWorld w)
    {
        var clash=w.WorldClashes is not {} s?[]:cache.GetValue(s,x=>x.Steps.SelectMany(step=>(step.Captures??[]).Select(c=>(c,step.Revision))).ToArray());
        // 実際の軍勢の出陣の決着での捕縛（NationalStrategy.CaptureLeader）。
        var strategy=(w.Strategy?.Outcomes.Where(o=>o.Capture!=null).Select(o=>(o.Capture!,o.Revision)).ToArray()??[]).Concat(NpcArmies.SkirmishCaptureRows(w)).ToArray();
        return strategy.Length==0?clash:clash.Concat(strategy).OrderBy(x=>x.Item2).ThenBy(x=>x.Item1.At).ToArray();
    }
    internal static BattleCapture[] AllCaptures(ContractWorld w)=>Captures(w).Select(x=>x.Capture).ToArray();
    static bool Before(long a,long ar,long at,long rev)=>a<at||a==at&&ar<rev;
    static (BattleCapture Capture,long Revision)? Last(ContractWorld w,EntityId person,long at,long rev)=>
        Captures(w).Where(x=>x.Capture.Person==person&&Before(x.Capture.At,x.Revision,at,rev)).Select(x=>((BattleCapture,long)?)x).LastOrDefault();
    // 拘束を終える操作：解放、応じた登用、処断。
    static bool Ends(CaptiveEntry e)=>e.Input.Action is not(CaptiveAction.Recruit or CaptiveAction.Plead)||e.Accepted;
    static CaptiveEntry[] Since(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>
        Entries(w).Where(e=>(e.Input.Person==person||e.Input.Exchange==person&&e.Accepted)&&e.Revision>capturedRevision&&Before(e.At,e.Revision,at,rev)).ToArray();
    static CaptiveEntry? EndOf(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>Since(w,person,capturedRevision,at,rev).FirstOrDefault(Ends);
    public static BattleCapture? CaptivityAt(ContractWorld w,EntityId person,long at,long rev)
    {
        if(!Enabled(w)||Last(w,person,at,rev) is not {} last)return null;
        return EndOf(w,person,last.Revision,at,rev)==null?last.Capture:null;
    }
    public static bool HeldAt(ContractWorld w,EntityId person,long at,long rev)=>CaptivityAt(w,person,at,rev)!=null;
    // 拘束中は捕らえた家の拠点。拘束が終わった後、本人が新しい行動を始めるまではその拠点（捕らえる前の行軍は続きとして扱わない）。
    // 道の上の位置（RegionalCampaign）は rev=0 で引き、捕縛の秒より後から城にそろえる（同じ秒に出た伝令の出発地を変えない）。
    internal static EntityId? SiteAt(ContractWorld w,EntityId person,long at,long rev)
    {
        if(!Enabled(w)||Last(w,person,at,rev) is not {} last)return null;
        var end=EndOf(w,person,last.Revision,at,rev);
        if(end==null)return last.Capture.Site;
        bool travelled=w.Tasks.Any(t=>t.Executor==person&&t.StartedAt>=end.At&&t.StartedAt<=at);
        return travelled?null:end.Input.Person==person?end.Site:last.Capture.Site;
    }
    static bool CanAct(ContractWorld w,BattleCapture c,EntityId actor,long at,long rev)=>
        FactionStances.ResolveAuthority(w,actor,c.CaptorOwner,at,Math.Min(rev,w.CommitSequence))!=null&&TransportWork.At(w,actor,c.Site,at,rev,true)&&!Assassination.DeadAt(w,actor,at,rev);
    static bool Declined(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>
        Since(w,person,capturedRevision,at,rev).Any(e=>e.Input.Action==CaptiveAction.Recruit&&!e.Accepted);
    // 本人の判断：元の家の当主は応じない。それ以外は捕縛ごとに固定の判定で、応じる者は4割ほど。
    static bool Accepts(ContractWorld w,BattleCapture c,long at,long rev)=>
        HouseSuccession.Head(w,c.FromOwner,at,rev)!=c.Person&&Roll("captive-recruit-v1",w.Seed,c.Person,c.At)<Willing;
    static BattleCapture Check(ContractWorld w,CaptiveInput i,EntityId actor,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action),"捕縛の入力が不正です");
        var last=Last(w,i.Person,at,rev);
        Need(last!=null&&EndOf(w,i.Person,last.Value.Revision,at,rev)==null&&!Assassination.DeadAt(w,i.Person,at,rev-1),"その人物は捕らえていません");
        var c=last.Value.Capture;
        if(i.Action==CaptiveAction.Plead)
        {
            // 捕らえられた本人が、捕らえた家の当主へ銭を差し出して解放を願う。当主がその城にいて権限を持つときだけ答える。
            Need(actor==i.Person&&(i.Exchange==null?i.Amount is >=PleaMin and <=RansomMax:i.Amount==0&&Exchangeable(w,c,i.Exchange.Value,actor,at,rev)),i.Exchange==null?"差し出す銭は500〜3000文です":"引き換えにできる捕虜ではありません");
            var head=HouseSuccession.Head(w,c.CaptorOwner,at,rev);
            Need(FactionStances.ResolveAuthority(w,head,c.CaptorOwner,at,Math.Min(rev,w.CommitSequence))!=null&&TransportWork.At(w,head,c.Site,at,rev,true),"捕らえた家の当主が城にいません");
            Need(!Since(w,i.Person,last.Value.Revision,at,rev).Any(e=>e.Input.Action==CaptiveAction.Plead&&e.At>at-86400),"一日に一度だけ願えます");
            return c;
        }
        Need(i.Amount==0&&i.Exchange==null,"捕縛の入力が不正です");
        Need(CanAct(w,c,actor,at,rev),"捕らえた家の当主権限を持つ者が、留め置いている拠点で行ってください");
        if(i.Action==CaptiveAction.Recruit)Need(!Declined(w,i.Person,last.Value.Revision,at,rev),"すでに登用を断られています");
        return c;
    }
    internal static void Apply(ContractWorld w,CaptiveInput i,EntityId actor)
    {
        Need(!OwnIdUsed(w,i.Id),"別の操作に使ったIDです");
        long at=w.ElapsedSeconds,rev=w.CommitSequence;
        var c=Check(w,i,actor,at,rev);
        bool accepted=Answer(w,i,c,at,rev);
        w.Captives=w.Captives! with{Entries=[..Entries(w),new(i,actor,c.Site,at,rev,accepted)]};
        if(i.Action==CaptiveAction.Execute)FamilyMortality.Die(w,i.Person,null,i.Id);
        if(accepted&&i.Action==CaptiveAction.Recruit)NewGameService.Sync(w);
    }
    public const long PleaMin=500;
    // 引き換えにできる捕虜：本人が当主の家が捕らえていて、本人を捕らえた家の者。当主は自分の家の者が戻るなら応じる。
    static bool Exchangeable(ContractWorld w,BattleCapture mine,EntityId other,EntityId actor,long at,long rev)=>
        CaptivityAt(w,other,at,rev) is {} x&&x.FromOwner==mine.CaptorOwner&&x.CaptorOwner==mine.FromOwner&&HouseSuccession.Head(w,x.CaptorOwner,at,rev)==actor&&!Assassination.DeadAt(w,other,at,rev);
    // 本人の判断：登用の誘いは捕虜本人が、解放の願いは捕らえた家の当主が答える。当主は差し出された銭が500文以上で、本人の私財で払えれば応じる。
    static bool Answer(ContractWorld w,CaptiveInput i,BattleCapture c,long at,long rev)=>i.Action switch
    {
        CaptiveAction.Recruit=>Accepts(w,c,at,rev),
        CaptiveAction.Plead=>i.Exchange!=null||NewGameWork.Enabled(w)&&TransportWork.WalletBefore(w,i.Person,at,rev)>=i.Amount,
        _=>false,
    };
    // 解放の願いで応じたときの銭：本人の私財から捕らえた家の当主へ。
    internal static long PleaNet(ContractWorld w,EntityId wallet,long at,long rev)
    {
        long n=0;
        foreach(var e in Entries(w).Where(e=>e.Input.Action==CaptiveAction.Plead&&e.Accepted&&Before(e.At,e.Revision,at,rev)))
        {
            var c=Last(w,e.Input.Person,e.At,e.Revision)!.Value.Capture;var head=HouseSuccession.Head(w,c.CaptorOwner,e.At,e.Revision);
            if(NewGameWork.Wallet(e.Input.Person)==wallet)n=checked(n-e.Input.Amount);
            if(NewGameWork.Wallet(head)==wallet)n=checked(n+e.Input.Amount);
        }
        return n;
    }
    // 処断による死亡記録が、同じ秒・同じ版の処断の操作と一致するか。
    internal static bool Executed(ContractWorld w,NaturalDeath d,EntityId order)=>
        Entries(w).Any(e=>e.Input.Id==order&&e.Input.Action==CaptiveAction.Execute&&e.Input.Person==d.Person&&e.At==d.At&&e.Revision==d.Revision);
    // 登用に応じた人の所属と主従：元の所属と主従をその時点で閉じ、捕らえた家と誘った当主へ付け替える。
    internal static TimedRelation[] Relations(ContractWorld w,TimedRelation[] original,string kind)
    {
        var joins=Entries(w).Where(Joined).ToArray();
        if(joins.Length==0)return original;
        var rows=original.ToList();
        foreach(var e in joins)
        {
            var c=CaptivityAt(w,e.Input.Person,e.At,e.Revision)!;
            for(int n=0;n<rows.Count;n++)
                if(rows[n].Subject==e.Input.Person&&rows[n].From<=e.At&&e.At<rows[n].Until)rows[n]=rows[n] with{Until=e.At,Revision=e.Revision};
            rows.Add(new(InformationSchemes.Id("captive-"+kind,e.Input.Id.Value),e.Input.Person,kind=="membership"?c.CaptorOwner:e.Actor,kind,NewGameWorld.Basis,e.At,long.MaxValue,e.Revision));
        }
        return rows.ToArray();
    }
    static bool Joined(CaptiveEntry e)=>e.Accepted&&e.Input.Action==CaptiveAction.Recruit;
    internal static bool HasJoins(ContractWorld w)=>Entries(w).Any(Joined);
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Captives==null&&!Captures(w).Any(),"この保存では捕縛を使えません");return;}
        Need(w.Captives is {Schema:1,Policy:Policy},"捕縛の記録が不正です");
        var rows=Entries(w);
        Need(rows.Select(e=>e.Input.Id).Distinct().Count()==rows.Length,"捕縛の操作のIDが重複しています");
        Need(rows.Zip(rows.Skip(1)).All(p=>p.First.At<=p.Second.At&&p.First.Revision<p.Second.Revision),"捕縛の操作の順序が不正です");
        foreach(var e in rows)
        {
            Need(e.At>=0&&e.At<=w.ElapsedSeconds&&e.Revision>0&&e.Revision<=w.CommitSequence,"捕縛の操作の時点が不正です");
            var c=Check(w,e.Input,e.Actor,e.At,e.Revision);
            Need(c.Site==e.Site,"処遇の場所が一致しません");
            Need(e.Accepted==Answer(w,e.Input,c,e.At,e.Revision),"返答が一致しません");
            if(e.Input.Action==CaptiveAction.Execute)
                Need((w.NaturalDeaths??[]).Any(d=>d.Execution==e.Input.Id&&d.Person==e.Input.Person),"処断の死亡記録がありません");
        }
        ValidateRansom(w);
    }
    // 本人が知る捕虜：本人の家が捕らえた人物だけ（捕縛は本人の軍の現地で起きる）。
    public static CaptiveCard[] View(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))return [];
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        return Captures(w).Where(x=>x.Capture.Holder==observer).GroupBy(x=>x.Capture.Person).Select(g=>g.Last()).Select(x=>
        {
            var c=x.Capture;
            bool held=HeldAt(w,c.Person,at,rev)&&!Assassination.DeadAt(w,c.Person,at,rev);
            return new CaptiveCard(c.Person,c.FromOwner,c.Site,c.At,held,held&&CanAct(w,c,observer,at,rev),Declined(w,c.Person,x.Revision,at,rev));
        }).ToArray();
    }
}

public sealed partial class WorldSession
{
    public CaptiveCard[] Captives(){lock(gate)return BattleCaptivity.View(state,state.PlayerControl);}
    // 本人が捕らえられているとき、その捕縛（捕らえた家・城）。
    public BattleCapture? MyCaptivity(){lock(gate)return BattleCaptivity.CaptivityAt(state,state.PlayerControl,state.ElapsedSeconds,state.CommitSequence+1);}
    public CommandResult ManageCaptive(CaptiveInput input,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(!BattleCaptivity.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"この保存では捕縛を使えません");
        var old=state.Captives!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"処理済みの操作です");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>BattleCaptivity.Apply(w,input,w.PlayerControl));}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
        bool yes=state.Captives!.Entries.Last().Accepted;
        string done=input.Action switch{CaptiveAction.Release=>"解放しました",CaptiveAction.Execute=>"処断しました",CaptiveAction.Plead=>yes?"解放されました":"聞き入れられませんでした",_=>yes?"登用に応じました":"登用を断られました"};
        return new(ResultCode.Accepted,input.Id,state.CommitSequence,done);
    }}
}
