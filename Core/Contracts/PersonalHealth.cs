using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public enum HealthKind { Injury, Illness }
public enum HealthPart { Leg, Arm, Body, Fever }
public enum HealthLimit { Travel, Weapon, Practice, Judgment }
// 状態は既存の原本（賊との接触）と固定シードの病から導出し、保存するのは手当の原本だけ。
public sealed record HealthCondition(EntityId Id,EntityId Person,HealthKind Kind,HealthPart Part,int Severity,long OnsetAt,long BaseSeconds);
public sealed record HealthCareInput(EntityId Id,EntityId Patient,EntityId Condition,EntityId Carer);
public sealed record HealthCareEntry(HealthCareInput Input,EntityId Site,long At,long Revision);
public sealed record HealthState(int Schema,string Policy,HealthCareEntry[] Care)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public ClinicalLedger? Clinic {get;init;}
}
public sealed record HealthCard(EntityId Id,string Kind,string Part,string Severity,long OnsetAt,bool Treated,string Outlook);
public sealed record HealthView(EntityId Person,bool Own,bool Observed,HealthCard[] Conditions,string[] Limits);

// 健康・療養 v1（2026-11-06 採用の設計 DG-04）。新規開始の版付き設定だけで有効。
public static class PersonalHealth
{
    public const string Policy="personal-health-injury-illness-care-v1";
    public const int Edition=1;
    const long Day=86400,Window=30*Day;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.HealthPolicy==ClinicalHealth.Policy||w.NewGame?.Judgment?.HealthEdition==Edition&&NpcJudgment.Enabled(w);
    internal static void Initialize(ContractWorld w){if(Enabled(w)){w.Health=new(1,Policy,[]);ClinicalHealth.Initialize(w);}}
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Health?.Care.Any(c=>c.Input.Id==id)==true;

    static byte[] Digest(string key)=>SHA256.HashData(Encoding.UTF8.GetBytes(key));
    static EntityId ConditionId(string source)=>new("condition:"+ContractJson.Hash(Encoding.UTF8.GetBytes(source))[..24]);

    // 開始時の人物の生年は開始の定義ごとに一度だけ引けるようにする（全国では数千人）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,Dictionary<string,int?>> births=new();
    static int Age(ContractWorld w,EntityId person,long at)
    {
        var start=births.GetValue(w.NewGame!,d=>d.Start.People.GroupBy(p=>p.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().BirthYear,StringComparer.Ordinal));
        int? born=start.TryGetValue(person.Value,out var y)?y:FamilyBirth.People(w).FirstOrDefault(p=>p.Id==person.Value)?.BirthYear;
        if(born is not int year)return 30;
        return WorldCalendar.At(w.EpochDate,at).Year-year;
    }
    // 加齢は回復の経過だけを遅くする。能力・技能の記録は変えない。
    static long Scaled(ContractWorld w,EntityId person,long at,long seconds)
    {
        int age=Age(w,person,at);
        return age<=45?seconds:checked(seconds*(100+(age-45)*3)/100);
    }
    static long Base(HealthKind kind,int severity)=>(kind,severity) switch
    {
        (HealthKind.Injury,1)=>3*Day,(HealthKind.Injury,2)=>14*Day,(HealthKind.Injury,3)=>45*Day,
        (HealthKind.Illness,1)=>4*Day,(HealthKind.Illness,2)=>10*Day,(HealthKind.Illness,3)=>30*Day,
        _=>throw new InvalidDataException("傷病の重さが不正です")
    };

    // 賊との接触が終わった時点の負傷。部位は接触と人物から固定する。
    static IEnumerable<HealthCondition> Injuries(ContractWorld w)
    {
        foreach(var e in w.Bandits?.Encounters??[])
        {
            if(e.Phase is "選択待ち" or "交戦中")continue;
            foreach(var p in e.People.Where(p=>p.Side==0))
            {
                var first=e.Initial.SingleOrDefault(x=>x.Id==p.Id);if(first==null)continue;
                int loss=first.Health-p.Health;if(loss<=0)continue;
                int severity=loss<=5?1:loss<=12?2:3;
                string source=$"injury/{e.Id.Value}/{p.Id.Value}";
                var part=(Digest(source)[0]%3) switch{0=>HealthPart.Leg,1=>HealthPart.Arm,_=>HealthPart.Body};
                yield return new(ConditionId(source),p.Id,HealthKind.Injury,part,severity,e.LastAt,Scaled(w,p.Id,e.LastAt,Base(HealthKind.Injury,severity)));
            }
        }
    }

    // 30日ごとの固定シードの病。直前90日に候補があれば重ねない（再帰しない判定）。
    static bool Candidate(ContractWorld w,EntityId person,long window,out byte[] digest)
    {
        digest=Digest(FormattableString.Invariant($"health-illness-v1/{w.NewGame!.Seed}/{person.Value}/{window}"));
        int age=Age(w,person,window*Window);
        int risk=Math.Clamp(15+Math.Max(0,age-40)*2,0,200);
        return BinaryPrimitives.ReadUInt32LittleEndian(digest)%1000<risk;
    }
    // 固定シードと生年だけで決まる部分は開始の定義ごとに覚えておく。死亡の判定は毎回行う。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<(string,long),HealthCondition?>> illnesses=new();
    internal static HealthCondition? Illness(ContractWorld w,EntityId person,long window)
    {
        if(window<0)return null;
        var c=illnesses.GetOrCreateValue(w.NewGame!).GetOrAdd((person.Value,window),_=>RawIllness(w,person,window));
        return c!=null&&Assassination.DeadAt(w,person,c.OnsetAt,long.MaxValue)?null:c;
    }
    static HealthCondition? RawIllness(ContractWorld w,EntityId person,long window)
    {
        if(!Candidate(w,person,window,out var d))return null;
        for(long k=window-3;k<window;k++)if(k>=0&&Candidate(w,person,k,out _))return null;
        long at=window*Window+BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(4))%(uint)(Window/3600)*3600;
        if(Age(w,person,at)<15)return null;
        int roll=d[8]%100;int severity=roll<65?1:roll<92?2:3;
        return new(ConditionId($"illness/{person.Value}/{window}"),person,HealthKind.Illness,HealthPart.Fever,severity,at,Scaled(w,person,at,Base(HealthKind.Illness,severity)));
    }
    internal static IEnumerable<HealthCondition> EventConditions(ContractWorld w)=>Injuries(w).Concat(OfficerBattleHealth.Conditions(w));
    internal static IEnumerable<EntityId> Living(ContractWorld w)=>FamilyBirth.People(w).Select(p=>new EntityId(p.Id));

    public static HealthCondition[] Conditions(ContractWorld w,EntityId person,long until)
    {
        if(!Enabled(w))return [];
        var rows=Injuries(w).Concat(OfficerBattleHealth.Conditions(w)).Where(c=>c.Person==person&&c.OnsetAt<=until).ToList();
        for(long k=0;k<=until/Window;k++)if(Illness(w,person,k) is {} c&&c.OnsetAt<=until)rows.Add(c);
        return rows.OrderBy(c=>c.OnsetAt).ThenBy(c=>c.Id.Value,StringComparer.Ordinal).ToArray();
    }
    internal static HealthCareEntry? Treatment(ContractWorld w,HealthCondition c)=>w.Health?.Care.FirstOrDefault(e=>e.Input.Condition==c.Id)??NpcCare(w,c);
    // NPC が自分から手当を受ける（全国の開始の一覧の NpcSelfCare を持つ世界だけ）。発症の1日後に城や町に留まっていれば、そこで手当を受けたものとする。
    // 記録は持たず、その時点の所在から決まる（保存から戻しても同じ）。操作する人物は自分で診療を選ぶ。
    public const long NpcCareDelay=86400;
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<string,HealthCareEntry?>> npcCare=new();
    static HealthCareEntry? NpcCare(ContractWorld w,HealthCondition c)
    {
        if(c.Person==w.PlayerControl||w.NewGame is not {} data||!NationalSystems.On(w,"NpcSelfCare"))return null;
        long at=checked(c.OnsetAt+NpcCareDelay);if(at>w.ElapsedSeconds)return null;
        return npcCare.GetValue(data,_=>new(StringComparer.Ordinal)).GetOrAdd(c.Id.Value,_=>
            PersonalCompanions.PhysicalSiteAt(w,c.Person,at,0) is {} site?new HealthCareEntry(new(new("care:npc/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(c.Id.Value))),c.Person,c.Id,c.Person),site,at,0):null);
    }
    // 手当は残りの経過を短くするが、即時の全快はない。
    public static long RecoverAt(ContractWorld w,HealthCondition c)
    {
        long untreated=checked(c.OnsetAt+c.BaseSeconds);
        if(Treatment(w,c) is not {} care||care.At>=untreated)return untreated;
        return care.At+(untreated-care.At)*3/5;
    }
    public static HealthCondition[] Active(ContractWorld w,EntityId person,long at)=>
        Conditions(w,person,at).Where(c=>c.OnsetAt<=at&&at<RecoverAt(w,c)).ToArray();

    static IEnumerable<HealthLimit> LimitsOf(HealthCondition c)
    {
        if(c.Kind==HealthKind.Injury)
        {
            if(c.Severity>=3){yield return HealthLimit.Travel;yield return HealthLimit.Weapon;yield return HealthLimit.Practice;yield break;}
            if(c.Severity>=2&&c.Part==HealthPart.Leg)yield return HealthLimit.Travel;
            if(c.Severity>=2&&c.Part is HealthPart.Arm or HealthPart.Body)yield return HealthLimit.Weapon;
            yield break;
        }
        if(c.Severity>=2){yield return HealthLimit.Practice;yield return HealthLimit.Judgment;}
        if(c.Severity>=3){yield return HealthLimit.Travel;yield return HealthLimit.Weapon;}
    }
    public static HealthLimit[] Limits(ContractWorld w,EntityId person,long at)=>
        Active(w,person,at).SelectMany(c=>LimitsOf(c).Concat(ClinicalHealth.Enabled(w)&&c.Severity>=3?[HealthLimit.Judgment]:Array.Empty<HealthLimit>())).Concat(ClinicalHealth.Fatigue(w,person,at)>=80?[HealthLimit.Practice]:Array.Empty<HealthLimit>()).Concat(ClinicalHealth.Fatigue(w,person,at)>=95?[HealthLimit.Weapon]:Array.Empty<HealthLimit>()).Concat(ClinicalHealth.Busy(w,person,at)?[HealthLimit.Travel,HealthLimit.Weapon,HealthLimit.Practice,HealthLimit.Judgment]:Array.Empty<HealthLimit>()).Distinct().Order().ToArray();
    public static bool Can(ContractWorld w,EntityId person,HealthLimit limit,long at)=>!Enabled(w)||!Limits(w,person,at).Contains(limit);
    static HealthLimit? LimitFor(ActionKind a)=>a switch{ActionKind.Move or ActionKind.Inspect=>HealthLimit.Travel,ActionKind.Train=>HealthLimit.Practice,_=>null};
    public static string? Blocked(ContractWorld w,Intent i)=>(i.Executor==w.PlayerControl||ClinicalHealth.Enabled(w))&&LimitFor(i.Action) is {} l&&!Can(w,i.Executor,l,w.ElapsedSeconds)?Reason(l):null;
    public static string Reason(HealthLimit l)=>l switch
    {
        HealthLimit.Travel=>"傷病のため遠出・出陣はできません",
        HealthLimit.Weapon=>"傷病のため武器を取って戦えません",
        HealthLimit.Practice=>"傷病のため修行に集中できません",
        HealthLimit.Judgment=>"病が重く、判断を要する政務を行えません",
        _=>"傷病のため行えません"
    };

    // 発症時点で、続けられない修行を中断する。役職・預り物・責任は動かさない。
    internal static long? Next(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;long? best=ClinicalHealth.Next(w,target);
        for(long k=w.ElapsedSeconds/Window;k<=target/Window;k++)
            foreach(var p in Living(w))
                if(Illness(w,p,k) is {} c&&c.OnsetAt>w.ElapsedSeconds&&c.OnsetAt<=target)best=Math.Min(best??c.OnsetAt,c.OnsetAt);
        return best;
    }
    internal static void Apply(ContractWorld w)
    {
        if(!Enabled(w))return;ClinicalHealth.Complete(w);long k=w.ElapsedSeconds/Window;
        foreach(var p in Living(w))
        {
            if(Illness(w,p,k) is not {} c||c.OnsetAt!=w.ElapsedSeconds)continue;
            if(Can(w,p,HealthLimit.Practice,w.ElapsedSeconds))continue;
            foreach(var task in w.Tasks.Where(t=>t.Executor==p&&t.Action==ActionKind.Train&&t.Execution==ExecutionPhase.Running).ToArray())
                WorldSession.Stop(w,task,"病のため修行を中断");
        }
    }

    static bool Busy(ContractWorld w,EntityId carer)=>carer!=w.PlayerControl&&PersonalCompanions.View(w).Any(c=>c.Person==carer&&c.Busy);
    internal static void Apply(ContractWorld w,HealthCareInput i,EntityId actor)
    {
        Need(Enabled(w),"健康の規則がありません");
        Need(actor==w.PlayerControl,"本人の操作ではありません");
        Need(i.Carer==actor||PersonalCompanions.View(w).Any(c=>c.Person==i.Carer&&c.Active&&c.Nearby&&!c.Busy),"手当を頼める同行者ではありません");
        var site=SocialWorld.Site(w,i.Carer);Need(site!=null,"手当する人が拠点にいません");
        w.Health=w.Health! with{Care=[..w.Health.Care,new(i,site.Value,w.ElapsedSeconds,w.CommitSequence)]};
    }
    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        if(!Enabled(w)){Need(w.Health==null,"旧版へ健康の規則を追加できません");return;}
        Need(w.Health is {Schema:1,Policy:Policy,Care:not null},"健康原本の版が違います");
        if(prior?.Health is {} old)Need(w.Health.Care.Take(old.Care.Length).SequenceEqual(old.Care),"過去の手当原本が変更されています");
        long at=0,rev=0;var ids=new HashSet<EntityId>();var treated=new HashSet<EntityId>();
        foreach(var e in w.Health.Care)
        {
            var i=e.Input;
            Need(EntityId.IsValid(i.Id.Value)&&ids.Add(i.Id)&&e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence,"手当原本の順序が不正です");
            Need(!FactionStances.OtherIdUsed(w,i.Id)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使った原本です");
            var c=Active(w,i.Patient,e.At).SingleOrDefault(c=>c.Id==i.Condition);
            Need(c!=null,"手当する傷病が今はありません");
            Need(treated.Add(i.Condition),"同じ傷病への手当は一度です");
            Need(ClinicalHealth.IsCompletion(w,e)?PersonalCompanions.SiteAt(w,i.Patient,e.At,e.Revision+1)==e.Site:SocialWorld.Site(w,i.Patient) is {} ps&&ps==e.Site,"患者が手当する人と同じ拠点にいません");
            Need(Can(w,i.Carer,HealthLimit.Travel,e.At)||i.Carer!=i.Patient,"重い傷病で自分では手当できません");
            at=e.At;rev=e.Revision;
        }
        // 本人の新しい行動だけを、行動を始めた時点の状態で照合する。
        ClinicalHealth.Validate(w,prior);
        var player=w.PlayerControl;
        foreach(var r in w.Requests.Where(r=>r.Intent.Executor==player&&(prior==null||r.CommitSequence>prior.CommitSequence)))
        {
            if(LimitFor(r.Intent.Action) is {} l)Need(Can(w,player,l,r.CreatedAt),Reason(l));
        }
        foreach(var d in (w.Bandits?.Decisions??[]).Where(d=>d.Actor==player&&d.Input.Action==BanditAction.Fight&&(prior==null||d.Revision>prior.CommitSequence)))
            Need(Can(w,player,HealthLimit.Weapon,d.At),Reason(HealthLimit.Weapon));
        foreach(var q in NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Where(q=>(prior==null||q.Revision>prior.CommitSequence)&&q.Input.Action is NewMilitaryAction.Pickup or NewMilitaryAction.Deploy or NewMilitaryAction.CombatStart))
            Need(Can(w,player,q.Input.Action==NewMilitaryAction.CombatStart?HealthLimit.Weapon:HealthLimit.Travel,q.At),Reason(q.Input.Action==NewMilitaryAction.CombatStart?HealthLimit.Weapon:HealthLimit.Travel));
    }

    internal static HealthCard[] ObservedCards(ContractWorld w,EntityId person,long at)=>Active(w,person,at).Select(c=>new HealthCard(c.Id,c.Kind==HealthKind.Injury?"負傷":"病",Part(c.Part),Severity(c),c.OnsetAt,w.Health?.Care.Any(e=>e.Input.Condition==c.Id&&e.At<=at)==true,"")).ToArray();
    static string Severity(HealthCondition c)=>(c.Kind,c.Severity) switch
    {
        (HealthKind.Injury,1)=>"軽傷",(HealthKind.Injury,2)=>"深手",(HealthKind.Injury,_)=>"重傷",
        (HealthKind.Illness,1)=>"微熱",(HealthKind.Illness,2)=>"高熱",_=>"危篤"
    };
    static string Part(HealthPart p)=>p switch{HealthPart.Leg=>"脚",HealthPart.Arm=>"腕",HealthPart.Body=>"胴",_=>"熱"};
    // 他人の病状は同じ拠点で見た範囲だけ。回復の見込みは本人にだけ幅で示す。
    public static HealthView? View(ContractWorld w,EntityId observer,EntityId person)
    {
        if(!Enabled(w))return null;long now=w.ElapsedSeconds;bool own=observer==person;
        bool observed=own||SocialWorld.Site(w,observer) is {} a&&SocialWorld.Site(w,person)==a;
        if(!observed)return ClinicalHealth.ReportView(w,observer,person)??new(person,false,false,[],[]);
        var cards=Active(w,person,now).Select(c=>
        {
            string outlook="";
            if(own){long left=Math.Max(1,(RecoverAt(w,c)-now+Day-1)/Day);long lo=Math.Max(1,left*3/4),hi=Math.Max(lo+1,left*5/4+1);outlook=$"見立てではおよそ{lo}〜{hi}日";}
            return new HealthCard(c.Id,c.Kind==HealthKind.Injury?"負傷":"病",Part(c.Part),Severity(c),c.OnsetAt,Treatment(w,c)!=null,outlook);
        }).ToArray();
        string[] limits=own?Limits(w,person,now).Select(Reason).ToArray():[];
        return new(person,own,true,cards,limits);
    }
}

public sealed partial class WorldSession
{
    public HealthView? HealthView(EntityId person){lock(gate)return PersonalHealth.View(state,state.PlayerControl,person);}
    public CommandResult ManageHealth(HealthCareInput input,EntityId actor,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(actor!=state.PlayerControl||!PersonalHealth.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の手当の操作ではありません");
        if(ClinicalHealth.Enabled(state))return ManageClinicalHealth(input,actor,revision);
        var old=state.Health!.Care.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"処理済みの手当です");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>PersonalHealth.Apply(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"手当を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
