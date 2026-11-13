using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 合戦での捕縛（BD-44、2026-11-13）。本人の軍が打ち崩した敵の軍の大将を、固定シードの判定で捕らえる。
// 捕縛は交戦の手番（WorldClashStep.Captures）に記録し、手番の前の世界から同じ結果を再現できることを確かめる。
// 捕らえた人物は捕らえた家の拠点（出陣した城）に留め置かれ、拘束中は新しい行動を始められない（HostageCustody.HeldAt）。捕縛の秒の後の位置は城になる。
// 解放は、捕らえた家の当主権限を持つ者が、その拠点で明示に行う。自動の処刑・登用・解放はしない。
public sealed record BattleCapture(EntityId Person,EntityId FromOwner,EntityId CaptorOwner,EntityId Holder,EntityId Site,long At);
public enum CaptiveAction { Release }
public sealed record CaptiveInput(EntityId Id,CaptiveAction Action,EntityId Person);
public sealed record CaptiveEntry(CaptiveInput Input,EntityId Actor,EntityId Site,long At,long Revision);
public sealed record CaptiveState(int Schema,string Policy,CaptiveEntry[] Entries);
public sealed record CaptiveCard(EntityId Person,EntityId FromOwner,EntityId Site,long CapturedAt,bool Held,bool CanRelease);

public static class BattleCaptivity
{
    public const string Policy="battle-capture-routed-commander-explicit-release-v1";
    const int Chance=35,Broken=30;
    public static bool Enabled(ContractWorld w)=>w.NewGame?.CapturePolicy==Policy||NationalSystems.On(w,"BattleCaptivity");
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Captives=new(1,Policy,[]);}
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
            var d=SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"battle-capture-v1/{w.Seed}/{m.Commander.Value}/{result.At/86400}")));
            if(d[0]%100>=Chance)continue;
            rows.Add(new(m.Commander,m.Owner,own.Terms.Owner,w.PlayerControl,own.Terms.Site,result.At));
        }
        return rows.Count==0?null:rows.ToArray();
    }
    internal static void Validate(ContractWorld context,WorldClashStep step,EngagementResult result)
    {
        if(ContractJson.CanonicalHash(step.Captures)!=ContractJson.CanonicalHash(Resolve(context,result)))throw new InvalidDataException("交戦時の捕縛を再現できません");
    }

    // 交戦の記録は手番ごとに作り直されるので、記録ごとに捕縛の一覧を覚えておく（所在の判定で頻繁に引く）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldClashState,(BattleCapture,long)[]> cache=new();
    static (BattleCapture Capture,long Revision)[] Captures(ContractWorld w)=>w.WorldClashes is not {} s?[]:
        cache.GetValue(s,x=>x.Steps.SelectMany(step=>(step.Captures??[]).Select(c=>(c,step.Revision))).ToArray());
    static (BattleCapture Capture,long Revision)? Last(ContractWorld w,EntityId person,long at,long rev)=>
        Captures(w).Where(x=>x.Capture.Person==person&&(x.Capture.At<at||x.Capture.At==at&&x.Revision<rev)).Select(x=>((BattleCapture,long)?)x).LastOrDefault();
    static CaptiveEntry? ReleaseOf(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>
        Entries(w).FirstOrDefault(e=>e.Input.Person==person&&e.Input.Action==CaptiveAction.Release&&e.Revision>capturedRevision&&(e.At<at||e.At==at&&e.Revision<rev));
    public static BattleCapture? CaptivityAt(ContractWorld w,EntityId person,long at,long rev)
    {
        if(!Enabled(w)||Last(w,person,at,rev) is not {} last)return null;
        return ReleaseOf(w,person,last.Revision,at,rev)==null?last.Capture:null;
    }
    public static bool HeldAt(ContractWorld w,EntityId person,long at,long rev)=>CaptivityAt(w,person,at,rev)!=null;
    // 拘束中は捕らえた家の拠点。解放の後、本人が新しい行動を始めるまでは解放された拠点（捕らえる前の行軍は続きとして扱わない）。
    // 道の上の位置（RegionalCampaign）は rev=0 で引き、捕縛の秒より後から城にそろえる（同じ秒に出た伝令の出発地を変えない）。
    internal static EntityId? SiteAt(ContractWorld w,EntityId person,long at,long rev)
    {
        if(!Enabled(w)||Last(w,person,at,rev) is not {} last)return null;
        var release=ReleaseOf(w,person,last.Revision,at,rev);
        if(release==null)return last.Capture.Site;
        bool travelled=w.Tasks.Any(t=>t.Executor==person&&t.StartedAt>=release.At&&t.StartedAt<=at);
        return travelled?null:release.Site;
    }
    static bool CanRelease(ContractWorld w,BattleCapture c,EntityId actor,long at,long rev)=>
        FactionStances.ResolveAuthority(w,actor,c.CaptorOwner,at,Math.Min(rev,w.CommitSequence))!=null&&TransportWork.At(w,actor,c.Site,at,rev,true)&&!Assassination.DeadAt(w,actor,at,rev);
    static void Check(ContractWorld w,CaptiveInput i,EntityId actor,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action),"捕縛の入力が不正です");
        var c=CaptivityAt(w,i.Person,at,rev);
        Need(c!=null,"その人物は捕らえていません");
        Need(CanRelease(w,c,actor,at,rev),"捕らえた家の当主権限を持つ者が、留め置いている拠点で解放してください");
    }
    internal static void Apply(ContractWorld w,CaptiveInput i,EntityId actor)
    {
        Need(!OwnIdUsed(w,i.Id),"別の操作に使ったIDです");
        Check(w,i,actor,w.ElapsedSeconds,w.CommitSequence);
        var site=CaptivityAt(w,i.Person,w.ElapsedSeconds,w.CommitSequence)!.Site;
        w.Captives=w.Captives! with{Entries=[..Entries(w),new(i,actor,site,w.ElapsedSeconds,w.CommitSequence)]};
    }
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
            Check(w,e.Input,e.Actor,e.At,e.Revision);
            Need(CaptivityAt(w,e.Input.Person,e.At,e.Revision)!.Site==e.Site,"解放の場所が一致しません");
        }
    }
    // 本人が知る捕虜：本人の家が捕らえた人物だけ（捕縛は本人の軍の現地で起きる）。
    public static CaptiveCard[] View(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))return [];
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        return Captures(w).Where(x=>x.Capture.Holder==observer).GroupBy(x=>x.Capture.Person).Select(g=>g.Last().Capture).Select(c=>
        {
            bool held=HeldAt(w,c.Person,at,rev);
            return new CaptiveCard(c.Person,c.FromOwner,c.Site,c.At,held,held&&CanRelease(w,c,observer,at,rev));
        }).ToArray();
    }
}

public sealed partial class WorldSession
{
    public CaptiveCard[] Captives(){lock(gate)return BattleCaptivity.View(state,state.PlayerControl);}
    public CommandResult ManageCaptive(CaptiveInput input,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(!BattleCaptivity.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"この保存では捕縛を使えません");
        var old=state.Captives!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"処理済みの操作です");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>BattleCaptivity.Apply(w,input,w.PlayerControl));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"解放しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
