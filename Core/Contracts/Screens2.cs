using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// S11（2026-11-17）：画面と文面の仕上げ。一覧（NationalSystems）に Screens2 のある新規の全国開始だけ。どの画面も本人の知っていることだけを使う。
// ・全種書状の帳面：本人に届いた書状と本人が出した書状を、種類の分け・差出・宛先・日時・開封・不達（本人が知らせを開いたものだけ）で並べる。開いていない書状の中身は出さない。
// ・自動の停止と減速：本人が開いた訃報（Death）と、本人が知った重い病状（本人の重さ3の傷病の始まり、本人に届いた病状報告の重さ3）で、時間の設定に従って止まるか遅くなる。
// ・人物帳：5能力と6分野32技能・経験・出自の手がかり・成長の理由・療養中にできること・教育。他人は推定と知っている病状だけ。
// ・判断支援の比較：知っている危機（敵の接近・本人の病・届いた誘い・史実の機会）ごとの対処案。詳しさは判断支援の設定で変わり、警告はいつも残す。
// ・ルールのつまみ：史実の事件の重み・脅威への敏感さ・捕縛の率・戦傷の率・出生（低い・標準・高い）。コースで組み、個別に変えられる。標準はつまみの無い世界と同じ値。
// ・史実の機会：本人が当主の家が史実の出陣の攻め手なら、家老から機会の書状が届く。応じれば翌月に出陣し、相手の家と約束があれば競合で不発、断れば見送り。
// ・家督の帳面・私的な約束・軍勢の帳面と軍団への出陣の命（当主だけ。優先度の順に一月に一つ）・呼び方（職名・宛名・結び）・会話の差し替え口。
public enum RuleLevel { Low, Standard, High }
public sealed record RuleKnobs(RuleLevel Events,RuleLevel Threat,RuleLevel Capture,RuleLevel Wounds,RuleLevel Births);
public sealed record HistoryOpportunity(string Event,EntityId House,EntityId Letter,EntityId Sender,long At,long Revision)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public bool? Accepted {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? AnsweredAt {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Result {get;init;}
}
public sealed record CorpsOrder(EntityId Id,EntityId House,string Province,EntityId Commander,int Priority,long At,long Revision)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? UsedAt {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? CancelledAt {get;init;}
}

public static class Screens2
{
    public const string Name=NationalSystems.Screens2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name);
    const long Day=86400;
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..16];
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??FamilyBirth.People(w).SingleOrDefault(x=>x.Id==p.Value)?.Name??p.Value;
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    public static string? HouseOf(ContractWorld w,EntityId person)=>FamilyBirth.People(w).SingleOrDefault(p=>p.Id==person.Value)?.Faction;
    static bool IsHead(ContractWorld w,EntityId person,string? house)=>house!=null&&HouseSuccession.Head(w,new(house))==person;

    // ---- ルールのつまみ ----
    public static readonly (string Name,RuleKnobs Knobs)[] Courses=[
        ("標準",new(RuleLevel.Standard,RuleLevel.Standard,RuleLevel.Standard,RuleLevel.Standard,RuleLevel.Standard)),
        ("史実のとおり",new(RuleLevel.High,RuleLevel.Standard,RuleLevel.Standard,RuleLevel.Standard,RuleLevel.Low)),
        ("波乱",new(RuleLevel.Low,RuleLevel.High,RuleLevel.High,RuleLevel.High,RuleLevel.High)),
        ("穏やか",new(RuleLevel.Standard,RuleLevel.Low,RuleLevel.Low,RuleLevel.Low,RuleLevel.Standard))];
    public static readonly string[] KnobNames=["史実の事件","脅威への敏感さ","捕縛","戦傷","出生"];
    public static string LevelName(RuleLevel l)=>l switch{RuleLevel.Low=>"低い",RuleLevel.High=>"高い",_=>"標準"};
    static RuleKnobs? Knobs(ContractWorld w)=>On(w)?w.NewGame?.Judgment?.Rules:null;
    // 史実の事件が起きる重み（千分率）。標準は今までの値（史実尊重900・準史実500）。
    internal static int EventChance(ContractWorld w,int standard)=>Knobs(w)?.Events switch{RuleLevel.Low=>standard*6/10,RuleLevel.High=>Math.Min(990,standard*13/10),_=>standard};
    internal static int ThreatChance(ContractWorld w,int standard)=>Knobs(w)?.Threat switch{RuleLevel.Low=>standard/2,RuleLevel.High=>Math.Min(1000,standard*3/2),_=>standard};
    internal static int CaptureChance(ContractWorld w)=>Knobs(w)?.Capture switch{RuleLevel.Low=>200,RuleLevel.High=>500,_=>350};
    internal static int WoundChance(ContractWorld w)=>Knobs(w)?.Wounds switch{RuleLevel.Low=>8,RuleLevel.High=>25,_=>15};
    internal static int BirthThreshold(ContractWorld w)=>Knobs(w)?.Births switch{RuleLevel.Low=>48,RuleLevel.High=>144,_=>96};
    internal static void ValidateKnobs(NewGameState data)
    {
        if(data.Judgment?.Rules is not {} r)return;
        Need(data.NationalSystems?.Contains(Name)==true&&new[]{r.Events,r.Threat,r.Capture,r.Wounds,r.Births}.All(Enum.IsDefined),"ルールのつまみが不正です");
    }

    // ---- 書状の帳面 ----
    public static readonly string[] Categories=["家","軍","外交","家族","報告","仕事","その他"];
    public static string Category(string kind)=>kind switch{
        _ when kind.StartsWith("family-",StringComparison.Ordinal)||kind is "heir-pledge-request" or "marriage-letter"=>"家族",
        _ when kind.StartsWith("strategy-",StringComparison.Ordinal)||kind.Contains("military",StringComparison.Ordinal)||kind.Contains("army",StringComparison.Ordinal)||kind is "captive-ransom-paper" or "castle-residence-notice" or HistoryKind=>"軍",
        _ when kind.Contains("treaty",StringComparison.Ordinal)||kind.Contains("truce",StringComparison.Ordinal)||kind.Contains("pact",StringComparison.Ordinal)||kind.Contains("peace",StringComparison.Ordinal)||kind.Contains("independence",StringComparison.Ordinal)||kind.Contains("shugo",StringComparison.Ordinal)||kind.Contains("shogun",StringComparison.Ordinal)||kind.Contains("central",StringComparison.Ordinal)||kind.Contains("imperial",StringComparison.Ordinal)||kind.Contains("coalition",StringComparison.Ordinal)||kind.Contains("faction",StringComparison.Ordinal)=>"外交",
        _ when kind.Contains("report",StringComparison.Ordinal)||kind.Contains("notice",StringComparison.Ordinal)||kind is "delivery-failure" or "region-query"=>"報告",
        _ when kind.StartsWith("contract-",StringComparison.Ordinal)||kind.Contains("work",StringComparison.Ordinal)||kind.Contains("service",StringComparison.Ordinal)||kind.Contains("assessment",StringComparison.Ordinal)||kind.Contains("mission",StringComparison.Ordinal)||kind.Contains("freight",StringComparison.Ordinal)||kind.Contains("steward",StringComparison.Ordinal)=>"仕事",
        _ when kind.Contains("house",StringComparison.Ordinal)||kind.Contains("reform",StringComparison.Ordinal)||kind.Contains("succession",StringComparison.Ordinal)||kind.Contains("salary",StringComparison.Ordinal)=>"家",
        _=>"その他"};
    public sealed record LetterRow(string Id,string Category,string Kind,string Sender,string Recipient,long At,bool Incoming,bool Opened,bool Failed,string? Body);
    public static LetterRow[] Letters(ContractWorld w,EntityId viewer,string? category=null)
    {
        if(!On(w))return [];
        string me=viewer.Value;var byId=w.Messaging.Messages.ToDictionary(m=>m.Id,StringComparer.Ordinal);
        // 本人が開いた不達の知らせが指す書状だけを不達と示す。
        var failed=w.Messaging.Messages.Where(m=>m.Kind=="delivery-failure"&&m.RecipientId==me&&MessageFlow.Knows(w.Messaging,m.Id,me))
            .Select(m=>w.Messaging.Attempts.FirstOrDefault(a=>a.Id==m.Body)?.MessageId).Where(x=>x!=null).ToHashSet(StringComparer.Ordinal);
        var rows=new List<LetterRow>();
        foreach(var g in w.Messaging.Attempts.Where(a=>a.Status==AttemptStatus.Delivered&&a.ReceiverId==me&&a.FinishedAt<=w.ElapsedSeconds).GroupBy(a=>a.MessageId))
        {
            if(!byId.TryGetValue(g.Key,out var m)||m.SenderId==me)continue;
            bool opened=MessageFlow.Knows(w.Messaging,m.Id,me);
            rows.Add(new(m.Id,Category(m.Kind),KindName(m.Kind),Address(w,viewer,new(m.SenderId)),Address(w,viewer,new(m.RecipientId)),g.Min(a=>a.FinishedAt)??m.CreatedAt,true,opened,false,opened?Readable(m):null));
        }
        foreach(var m in w.Messaging.Messages.Where(m=>m.SenderId==me&&m.CreatedAt<=w.ElapsedSeconds))
            rows.Add(new(m.Id,Category(m.Kind),KindName(m.Kind),Address(w,viewer,viewer),Address(w,viewer,new(m.RecipientId)),m.CreatedAt,false,true,failed.Contains(m.Id),Readable(m)));
        return rows.Where(r=>category==null||r.Category==category).OrderByDescending(r=>r.At).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    // 書状の本文：JSON の原本は種類の名前だけにする（画面に原本の書式を出さない）。
    static string Readable(MessageEnvelope m)=>m.Body.StartsWith('{')||m.Body.StartsWith('[')?KindName(m.Kind):m.Body;
    public static string KindName(string kind)=>kind switch{
        FamilyNotice.Kind=>"訃報","family-birth-notice"=>"出生の知らせ","family-coming-of-age"=>"元服の知らせ","delivery-failure"=>"不達の知らせ",
        NationalStrategy.LetterKind=>"合戦の知らせ",NationalStrategy.InviteKind=>"包囲網の誘い",NationalStrategy.InviteReplyKind=>"誘いへの返書",HistoryKind=>"出陣の上申",
        ClinicalHealth.ReportKind=>"病状の報告","contract-order"=>"仕事の依頼","contract-report"=>"仕事の報告","assessment-notice"=>"見立ての知らせ","captive-ransom-paper"=>"身代金の書状",
        "secret-pact"=>"密約","heir-pledge-request"=>"誓紙の求め","house-reform-proposal"=>"家中の改めの上申","public-independence-letter"=>"独立の書状","mediated-peace-proposal"=>"和睦の勧め",
        _=>Category(kind)+"の書状"};

    // ---- 呼び方 ----
    // 職名：朝廷の官途・幕府職・家中の役・当主の順で、史実の職名を使う。全場面で同じ関数を使う。
    public static string Title(ContractWorld w,EntityId person)
    {
        if(!On(w))return "";
        var house=HouseOf(w,person);long at=w.ElapsedSeconds;
        var shogunate=Domestic2.Posts(w,at).Where(x=>house!=null&&x.Value==house&&IsHead(w,person,house)).Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
        if(shogunate!=null)return shogunate;
        if(house!=null&&IsHead(w,person,house)&&(w.Strategy?.Shugo??[]).LastOrDefault(s=>s.House.Value==house&&s.At<=at) is {} shugo)return shugo.Province+"守護";
        if(CentralTitles.Held(w,person).FirstOrDefault()?.Name is {} court)return court.Split(new[]{',','、'},StringSplitOptions.RemoveEmptyEntries)[0];
        if(house!=null&&(w.Strategy?.Houses??[]).SingleOrDefault(h=>h.House.Value==house)?.Posts.FirstOrDefault(p=>p.Person==person)?.Post is {} post)return post;
        return house!=null&&IsHead(w,person,house)?"当主":"";
    }
    public enum Standing { Superior, Equal, Inferior, Self }
    public static Standing Relation(ContractWorld w,EntityId from,EntityId to)
    {
        if(from==to)return Standing.Self;
        var fh=HouseOf(w,from);var th=HouseOf(w,to);
        if(fh!=null&&fh==th){if(IsHead(w,to,th))return Standing.Superior;if(IsHead(w,from,fh))return Standing.Inferior;}
        return Standing.Equal;
    }
    // 宛名：目上は「様」、同格は「殿」、目下は「へ」。職名があれば前に付ける。
    public static string Address(ContractWorld w,EntityId viewer,EntityId person)
    {
        if(!w.Characters.Any(c=>c.Id==person)&&!FamilyBirth.People(w).Any(p=>p.Id==person.Value))return person.Value.StartsWith("site:",StringComparison.Ordinal)?NewGameWorld.SiteName(person.Value):person.Value;
        string name=PersonName(w,person);if(!On(w))return name;string title=Title(w,person);
        var r=Relation(w,viewer,person);
        return (title.Length>0?title+"　":"")+name+(r switch{Standing.Superior=>"様",Standing.Inferior=>"へ",Standing.Self=>"",_=>"殿"});
    }
    public static string Closing(ContractWorld w,EntityId from,EntityId to)=>Relation(w,from,to) switch{Standing.Superior=>"恐惶謹言",Standing.Inferior=>"謹言",_=>"恐々謹言"};
    // 書状の文面：宛名と結びを差出人と宛先の間柄で変える。
    public static string Compose(ContractWorld w,EntityId from,EntityId to,string body)=>On(w)?Address(w,from,to)+"\n"+body+"\n"+Closing(w,from,to)+"　"+PersonName(w,from):body;

    // ---- 会話の差し替え口 ----
    public sealed record Stage(string Portrait,string Background,string Fallback);
    public static Stage StageFor(EntityId speaker,string scene)=>new($"res://Assets/Portraits/{speaker.Value.Replace(':','_').Replace('/','_')}.png",$"res://Assets/Backgrounds/{scene}.png",speaker.Value);

    // ---- 人物帳 ----
    public static readonly (string Field,DevelopmentSkill[] Skills)[] Fields=[
        ("軍事",[DevelopmentSkill.Tactics,DevelopmentSkill.Logistics,DevelopmentSkill.Siege,DevelopmentSkill.Drill,DevelopmentSkill.Navy,DevelopmentSkill.Swordsmanship,DevelopmentSkill.Spear,DevelopmentSkill.Bow,DevelopmentSkill.Cavalry,DevelopmentSkill.Gun]),
        ("内政",[DevelopmentSkill.Agriculture,DevelopmentSkill.Construction,DevelopmentSkill.Finance,DevelopmentSkill.Adjudication,DevelopmentSkill.Irrigation]),
        ("外交・人事",[DevelopmentSkill.Negotiation,DevelopmentSkill.Etiquette,DevelopmentSkill.Mediation,DevelopmentSkill.TalentAssessment]),
        ("情報・謀略",[DevelopmentSkill.IntelligenceGathering,DevelopmentSkill.Subversion,DevelopmentSkill.Guard,DevelopmentSkill.Stealth]),
        ("商業・産業",[DevelopmentSkill.Commerce,DevelopmentSkill.Mining,DevelopmentSkill.Workshop,DevelopmentSkill.Appraisal]),
        ("教養・育成",[DevelopmentSkill.Tea,DevelopmentSkill.Renga,DevelopmentSkill.Teaching,DevelopmentSkill.Writing,DevelopmentSkill.Medicine])];
    public sealed record SkillField(string Field,DevelopmentValue[] Skills);
    public sealed record Growth(string Text,long At);
    public sealed record CareLine(string Action,bool Can,string Reason);
    public sealed record Education(string Plan,string? Teacher,string? Learning,string? Interrupted,string[] Reports,string? Reflection);
    public sealed record PersonBook(EntityId Person,string Name,string Title,bool Own,DevelopmentValue[] Abilities,SkillField[] Fields,string[] Origins,Growth[] Growth,HealthView? Health,CareLine[] Care,Education? Education);
    public static PersonBook? Book(ContractWorld w,EntityId viewer,EntityId person)
    {
        if(!On(w)||CharacterDevelopment.View(w,viewer,person) is not {} card)return null;
        bool own=viewer==person;var names=card.Skills.ToDictionary(s=>s.Name);
        var fields=Fields.Select(f=>new SkillField(f.Field,f.Skills.Select(s=>names.GetValueOrDefault(CharacterDevelopment.Name(s))).Where(v=>v!=null).Select(v=>v!).ToArray())).Where(f=>f.Skills.Length>0).ToArray();
        string[] origins=own?CharacterDevelopment.Hints(w,person).Select(s=>CharacterDevelopment.Name(s)+"の経歴").ToArray():[];
        var growth=new List<Growth>();
        if(own)
        {
            // 成長の理由は行為（任務）の区切りでまとめる。
            foreach(var g in (w.Development?.Awards??[]).Where(a=>a.Actor==person).GroupBy(a=>a.Task).Select(g=>(g.Key,At:g.Max(a=>a.At),Skills:g.GroupBy(a=>a.Skill).Select(s=>(s.Key,Xp:s.Sum(a=>a.Experience))).ToArray())).OrderByDescending(x=>x.At).Take(8))
            {
                var task=w.Tasks.SingleOrDefault(t=>t.Id==g.Key);string act=task?.Action switch{ActionKind.Train=>"修行",ActionKind.Inspect=>"点検",null=>"務め",_=>"務め"};
                growth.Add(new(act+"で"+string.Join("・",g.Skills.Select(s=>CharacterDevelopment.Name(s.Key)+"の経験"+s.Xp/3600+"時間分")),g.At));
            }
            foreach(var e in (w.Strategy?.Persons??[]).Where(e=>e.Person==person).TakeLast(4))growth.Add(new(e.Text,e.At));
        }
        var health=PersonalHealth.View(w,viewer,person);
        return new(person,PersonName(w,person),Title(w,person),own,card.Abilities,fields,origins,growth.OrderByDescending(g=>g.At).ToArray(),health,own?Care(w,person):[],EducationOf(w,viewer,person));
    }
    // 療養中にできること：面会・書状・政務と、移動・武芸・修行。できない理由を健康・権限・拘束に分ける。
    public static CareLine[] Care(ContractWorld w,EntityId person)
    {
        long at=w.ElapsedSeconds;bool held=BattleCaptivity.HeldAt(w,person,at,w.CommitSequence+1);bool judge=PersonalHealth.CanJudge(w,person,at);
        var house=HouseOf(w,person);bool office=IsHead(w,person,house)||(w.Strategy?.Houses??[]).Any(h=>h.House.Value==house&&h.Posts.Any(p=>p.Person==person));
        CareLine Line(string action,bool health,bool needsOffice,bool free)
            =>!free&&held?new(action,false,"拘束"):!health?new(action,false,"健康"):needsOffice&&!office?new(action,false,"権限"):new(action,true,"");
        return [Line("面会",judge,false,true),Line("書状",judge,false,false),Line("政務",judge,true,false),
            Line("移動",PersonalHealth.Can(w,person,HealthLimit.Travel,at),false,false),Line("武芸",PersonalHealth.Can(w,person,HealthLimit.Weapon,at),false,false),Line("修行",PersonalHealth.Can(w,person,HealthLimit.Practice,at),false,false)];
    }
    // 教育：本人か、本人が当主の家の子だけ。家の方針・師・学んでいる技能・中断の理由・教師の見立て・元服の振り返り。
    public static Education? EducationOf(ContractWorld w,EntityId viewer,EntityId person)
    {
        var p=FamilyBirth.People(w).SingleOrDefault(x=>x.Id==person.Value);if(p==null)return null;
        int age=p.BirthYear is int y?WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year-y:99;
        bool mine=viewer==person||p.Faction!=null&&IsHead(w,viewer,p.Faction)&&HeirDesignation.KnownChildren(w,new(p.Faction)).Any(c=>c.Person==person);
        if(!mine)return null;
        var events=Duties2.Events(w).Where(e=>e.Person==person).ToArray();var persons=(w.Strategy?.Persons??[]).Where(e=>e.Person==person).ToArray();
        if(age>=16&&!events.Any(e=>e.Kind is "growth" or "aspiration-formed")&&!persons.Any(e=>e.Kind=="genpuku"))return null;
        string plan=p.Faction!=null&&(w.Strategy?.Houses??[]).SingleOrDefault(h=>h.House.Value==p.Faction)?.Policy is {} policy?"家の方針："+policy:"家の方針：定めなし";
        var train=w.Tasks.Where(t=>t.Executor==person&&t.Action==ActionKind.Train).LastOrDefault();
        var intent=train==null?null:w.Requests.SingleOrDefault(r=>r.Id==train.RequestId)?.Intent;
        string? teacher=intent?.Teacher is {} t?PersonName(w,t):null;string? learning=intent?.TrainingSkill is {} sk?CharacterDevelopment.Name(sk):null;
        string? interrupted=BattleCaptivity.HeldAt(w,person,w.ElapsedSeconds,w.CommitSequence+1)?"拘束":!PersonalHealth.Can(w,person,HealthLimit.Practice,w.ElapsedSeconds)?"健康":null;
        var reports=events.Where(e=>e.Kind=="growth").Select(e=>e.Text).ToArray();
        string? reflection=events.Where(e=>e.Kind=="aspiration-formed").Select(e=>e.Text).LastOrDefault()??persons.Where(e=>e.Kind=="genpuku").Select(e=>e.Text).LastOrDefault();
        return new(plan,teacher,learning,interrupted,reports,reflection);
    }

    // ---- 判断支援の比較 ----
    public sealed record Option(string Name,string Preparation,string Time,string Cost,string Basis,string Uncertainty);
    public sealed record Crisis(string Kind,string What,string Warning,Option[] Options);
    public static Crisis[] Crises(ContractWorld w,EntityId viewer)
    {
        if(!On(w))return [];
        var support=w.NewGame?.Judgment?.Support??DecisionSupport.Brief;var list=new List<Crisis>();long at=w.ElapsedSeconds;
        var own=w.Memberships.Where(m=>m.Subject==viewer&&m.From<=at&&m.Until>at).Select(m=>m.Target).ToHashSet();
        var known=FactionStances.Known(w,viewer);
        if(NpcArmies.Enabled(w))
            foreach(var s in NpcArmies.Sights(w,viewer).Where(s=>known.Any(k=>k.EffectiveFrom<=at&&k.Until>at&&(own.Contains(k.FromOwner)&&k.ToOwner==s.Owner||own.Contains(k.ToOwner)&&k.FromOwner==s.Owner))).OrderBy(s=>s.Army.Value,StringComparer.Ordinal))
            {
                var me=RegionalCampaign.ActorPointAt(w,viewer,at);double km=Math.Sqrt(Math.Pow(s.Position.EastMm-me.EastMm,2)+Math.Pow(s.Position.NorthMm-me.NorthMm,2))/1_000_000d;long hours=(long)Math.Ceiling(km/4);
                string basis=$"見た日時 {WorldCalendar.Display(w.EpochDate,at)}、見た兵 約{s.Soldiers}";
                list.Add(new("敵の接近",$"{HouseName(w,s.Owner)}の軍 約{s.Soldiers}",s.Attacking?"攻撃中":"接近中",[
                    new("籠城","兵糧と門を固める","すぐ",$"兵糧の消費",basis,"見た兵の数は前後2割"),
                    new("迎え撃つ","兵を集める",$"約{hours}時間で接する",$"兵の損害",basis,"見た兵の数は前後2割、援軍は不明"),
                    new("退く","荷をまとめる","すぐ","城と村を明け渡す",basis,"追われるかは不明")]));
            }
        foreach(var c in PersonalHealth.View(w,viewer,viewer)?.Conditions??[])
            list.Add(new("本人の病",$"{c.Part}の{c.Kind}　{c.Severity}",Grave(c.Severity)?"峠":"",[
                new("療養する","務めを預ける",c.Outlook,"務めの遅れ","本人の容態","治る日は前後する"),
                new("医者を呼ぶ","銭と使い","使いの往復",$"医者への礼","本人の容態","医者の腕は不明"),
                new("構わず務める","なし","すぐ","悪化の恐れ","本人の容態","悪化するかは不明")]));
        foreach(var (c,i) in NationalStrategy.OpenInvites(w).Where(x=>x.Invite.Head==viewer))
            list.Add(new("包囲網の誘い",HouseName(w,c.Target)+"への包囲網",$"{c.Members.Length}家",[
                new("加わる","返書","返書の往復",$"{HouseName(w,c.Target)}との敵対","届いた誘い","加わる家の本気は不明"),
                new("断る","返書","返書の往復","誘った家との間柄","届いた誘い","断った後の扱いは不明")]));
        foreach(var o in OpenOpportunities(w).Where(o=>HouseSuccession.Head(w,o.House)==viewer))
            list.Add(new("出陣の機会",EventName(o.Event),"",[
                new("応じる","陣触れ","翌月の初めに出陣","兵と兵糧","家老の上申","相手の備えは不明"),
                new("見送る","なし","なし","機会を逃す","家老の上申","相手が先に動くかは不明")]));
        return list.Select(c=>support switch{
            DecisionSupport.Detailed=>c,
            DecisionSupport.Brief=>c with{Options=c.Options.Select(o=>new Option(o.Name,"",o.Time,o.Cost,"","")).ToArray()},
            _=>c with{Options=c.Options.Select(o=>new Option(o.Name,"","","","","")).ToArray()}}).ToArray();
    }

    // ---- 約束 ----
    public sealed record PromiseRow(string Kind,string With,string Terms,long Since,long? Until,string State);
    public static (PromiseRow[] House,PromiseRow[] Private) Promises(ContractWorld w,EntityId viewer)
    {
        if(!On(w))return ([],[]);
        long at=w.ElapsedSeconds;var house=HouseOf(w,viewer);
        var housePromises=new List<PromiseRow>();
        if(IsHead(w,viewer,house))
            foreach(var c in Diplomacy2.Commitments(w,new(house!),at))
            {
                var pact=(w.Strategy?.Pacts??[]).SingleOrDefault(p=>p.Id==c.Pact);
                housePromises.Add(new(c.Kind=="alliance"?"同盟":"和睦",HouseName(w,c.Other),c.Basis.StartsWith("threat:",StringComparison.Ordinal)?HouseName(w,new(c.Basis[7..]))+"への備え":c.Basis,pact?.From??at,c.Until,"続いている"));
            }
        var mine=new List<PromiseRow>();
        if(viewer==w.PlayerControl)
        {
            foreach(var p in w.Villages?.Promises??[])
                mine.Add(new("村への約束",NewGameWorld.SiteName(p.Site.Value),$"{p.Kind}　{p.Amount}",p.ConfirmedAt,p.DeliverBy,p.CancelledAt!=null?"取りやめ":p.Delivered>=p.Amount?"果たした":"果たす前"));
            foreach(var o in w.Standdown?.Orders??[])
                if(w.Standdown!.Quotes.SingleOrDefault(q=>q.Id==o.Input.Quote) is {} q)
                    mine.Add(new("出陣の見合わせ",PersonName(w,q.Person),$"{q.Hours}時間　{q.Fee}文",o.At,q.At+q.Hours*3600L,at<q.At+q.Hours*3600L?"続いている":"終わった"));
        }
        foreach(var o in OpenOpportunities(w).Where(o=>o.Accepted==true&&HouseSuccession.Head(w,o.House)==viewer))
            mine.Add(new("出陣の約束",HouseName(w,o.House),EventName(o.Event),o.AnsweredAt??o.At,null,"翌月に出陣"));
        return (housePromises.ToArray(),mine.ToArray());
    }

    // ---- 家督 ----
    public sealed record HeirRow(EntityId Person,string Name,int? Age,bool Adopted,bool Designated,bool Adult,int? ComingOfAgeYear);
    public sealed record SuccessionBook(EntityId House,string HouseName,EntityId Head,string HeadName,bool ViewerIsHead,bool CanRetire,string? Guardian,HeirRow[] Candidates);
    public static SuccessionBook? Succession(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||HouseOf(w,viewer) is not {} house)return null;
        var id=new EntityId(house);var head=HouseSuccession.Head(w,id);bool isHead=head==viewer;int year=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year;
        var designated=(w.HeirNominations??[]).Where(n=>n.House==id&&n.At<=w.ElapsedSeconds).Select(n=>(EntityId?)n.Heir).LastOrDefault();
        var rows=isHead?HeirDesignation.KnownChildren(w,id).Select(c=>new HeirRow(c.Person,PersonName(w,c.Person),c.BirthYear is int b?year-b:null,c.Adopted,c.Person==designated,w.Characters.SingleOrDefault(x=>x.Id==c.Person)?.Life==LifeState.Adult,c.BirthYear is int by&&year-by<FamilyKin.ComingOfAge?by+FamilyKin.ComingOfAge:null)).ToArray():[];
        var guardian=Duties2.Events(w).LastOrDefault(e=>e.Kind=="guardian"&&e.House==id&&!Duties2.Events(w).Any(x=>x.Kind=="guardian-end"&&x.Detail==e.Id))?.Person;
        return new(id,HouseName(w,id),head,PersonName(w,head),isHead,isHead&&ControlSuccession.RetireCandidates(w).Length>0,guardian is {} g?PersonName(w,g):null,rows);
    }

    // ---- 軍勢 ----
    public sealed record ArmyRow(string Id,string Leader,long Soldiers,string Objective,string Line,int? Priority,string State);
    public static ArmyRow[] Armies(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||HouseOf(w,viewer) is not {} house)return [];
        var rows=new List<ArmyRow>();var s=w.Strategy;var id=new EntityId(house);
        if(s!=null)
        {
            var owners=NationalStrategy.Owners(w,s);var power=NationalStrategy.Powers(w,owners);
            foreach(var c in s.Campaigns.Where(c=>c.Attacker==id&&!s.Outcomes.Any(o=>o.Campaign==c.Id)))
            {
                var order=(s.Orders??[]).FirstOrDefault(o=>c.Basis=="order:"+o.Id.Value);
                long soldiers=power.GetValueOrDefault(house,50)*(c.Share??100)/100;
                rows.Add(new(c.Id.Value,PersonName(w,c.Commander??HouseSuccession.Head(w,id)),soldiers,NewGameWorld.SiteName(c.Target.Value)+"（"+HouseName(w,c.Defender)+"）",c.Line==2?"二の線":c.Line==1?"主の線":"一手",order?.Priority,"出陣中"));
            }
            foreach(var o in (s.Orders??[]).Where(o=>o.House==id&&o.UsedAt==null&&o.CancelledAt==null).OrderBy(o=>o.Priority).ThenBy(o=>o.Id.Value,StringComparer.Ordinal))
                rows.Add(new(o.Id.Value,PersonName(w,o.Commander),0,o.Province,"軍団",o.Priority,"命じた"));
        }
        foreach(var m in NpcArmies.Musters(w).Where(m=>m.Owner==id&&!NpcArmies.ReturnedAt(w,m,w.ElapsedSeconds,w.CommitSequence)))
            rows.Add(new(m.Id.Value,PersonName(w,m.Commander),0,NewGameWorld.SiteName(m.Site.Value),"一手",null,"出陣中"));
        foreach(var a in NewGameMilitary.Armies(w).Where(a=>a.Organization==id))
            rows.Add(new(a.Id.Value,PersonName(w,a.Commander),0,"","本人の軍",null,"率いている"));
        return rows.ToArray();
    }
    // 軍団へ出陣を命じられる国（本人の家の城のある国）と率いる人（家の成人）。
    public static string[] Provinces(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||HouseOf(w,viewer) is not {} house||!IsHead(w,viewer,house))return [];
        var owners=NationalStrategy.Owners(w,w.Strategy);
        return owners.Where(x=>x.Value==house).Select(x=>CentralStanding.ProvinceOf(new(x.Key))).Where(p=>p!=null).Select(p=>p!).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    }
    public static EntityId[] Commanders(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||HouseOf(w,viewer) is not {} house||!IsHead(w,viewer,house))return [];
        return FamilyBirth.People(w).Where(p=>p.Faction==house&&p.Id!=viewer.Value&&w.Characters.SingleOrDefault(c=>c.Id.Value==p.Id)?.Life==LifeState.Adult).Select(p=>new EntityId(p.Id)).OrderBy(p=>p.Value,StringComparer.Ordinal).Take(40).ToArray();
    }
    internal static void Order(ContractWorld w,string province,EntityId commander,int priority)
    {
        var house=HouseOf(w,w.PlayerControl);
        Need(On(w)&&w.Strategy!=null&&house!=null&&IsHead(w,w.PlayerControl,house),"家の当主だけが軍団に出陣を命じられます");
        Need(Provinces(w,w.PlayerControl).Contains(province)&&Commanders(w,w.PlayerControl).Contains(commander)&&priority is >=1 and <=9,"命じられる国と率いる人を選んでください");
        var orders=w.Strategy.Orders??[];
        Need(!orders.Any(o=>o.House.Value==house&&o.UsedAt==null&&o.CancelledAt==null&&o.Province==province),"その国にはすでに命じています");
        var id=new EntityId("order:corps/"+Hash(house+"/"+province+"/"+w.ElapsedSeconds+"/"+w.CommitSequence));
        w.Strategy=w.Strategy with{Orders=[..orders,new(id,new(house),province,commander,priority,w.ElapsedSeconds,w.CommitSequence)]};
    }
    internal static void Cancel(ContractWorld w,EntityId order)
    {
        var o=(w.Strategy?.Orders??[]).SingleOrDefault(o=>o.Id==order);
        Need(o!=null&&o.UsedAt==null&&o.CancelledAt==null&&HouseSuccession.Head(w,o.House)==w.PlayerControl,"取り消せる命ではありません");
        w.Strategy=w.Strategy! with{Orders=w.Strategy.Orders!.Select(x=>x.Id==order?x with{CancelledAt=w.ElapsedSeconds}:x).ToArray()};
    }
    // 月の初めの家の戦略から：本人の家の命を優先度の順に見て、その国の境に攻められる城があれば一つだけ出陣する。
    internal static CorpsOrder[]? Muster(ContractWorld w,StrategyState s,Dictionary<string,string?> owners,List<StrategyCampaign> campaigns,List<StrategyPact> pacts,HashSet<string> playerHouse)
    {
        if(!On(w)||s.Orders is not {} orders)return s.Orders;
        long now=w.ElapsedSeconds;var date=WorldCalendar.At(w.EpochDate,now);var result=orders.ToArray();
        foreach(var house in orders.Where(o=>o.UsedAt==null&&o.CancelledAt==null).Select(o=>o.House.Value).Distinct(StringComparer.Ordinal).OrderBy(h=>h,StringComparer.Ordinal))
        {
            if(HouseSuccession.Head(w,new(house))!=w.PlayerControl||campaigns.Any(c=>c.Attacker.Value==house&&c.At==now))continue;
            foreach(var o in orders.Where(o=>o.House.Value==house&&o.UsedAt==null&&o.CancelledAt==null).OrderBy(o=>o.Priority).ThenBy(o=>o.Id.Value,StringComparer.Ordinal))
            {
                if(w.Characters.SingleOrDefault(c=>c.Id==o.Commander)?.Life!=LifeState.Adult)continue;
                var target=owners.Where(x=>x.Value==house&&CentralStanding.ProvinceOf(new(x.Key))==o.Province).SelectMany(x=>NationalStrategy.Neighbors(x.Key))
                    .Distinct(StringComparer.Ordinal).Select(t=>(Site:t,Owner:owners.GetValueOrDefault(t)))
                    .Where(t=>t.Owner!=null&&t.Owner!=house&&!pacts.Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==house&&p.B.Value==t.Owner||p.B.Value==house&&p.A.Value==t.Owner)))
                    .OrderBy(t=>t.Site,StringComparer.Ordinal).FirstOrDefault();
                if(target.Site==null)continue;
                campaigns.Add(new(new($"campaign:national/{date:yyyyMM}/{Hash(house+"/order/"+o.Id.Value)[..12]}"),new(house),new(target.Owner!),new(target.Site),now,w.CommitSequence,"order:"+o.Id.Value){Commander=o.Commander});
                result=result.Select(x=>x.Id==o.Id?x with{UsedAt=now}:x).ToArray();
                break;
            }
        }
        return result;
    }

    // ---- 史実の機会 ----
    public const string HistoryKind="history-opportunity";
    static string EventName(string id)=>NationalStrategy.HistoryEventName(id);
    public static HistoryOpportunity[] OpenOpportunities(ContractWorld w)=>(w.Strategy?.Opportunities??[]).Where(o=>o.Result==null&&MessageFlow.Knows(w.Messaging,o.Letter.Value,w.PlayerControl.Value)).ToArray();
    // 本人が当主の家が史実の出陣の攻め手で、相手と境を接していれば、家老から機会の書状が届く。
    internal static bool Offer(ContractWorld w,HistoricalCampaign e,string house,Dictionary<string,string?> owners,List<HistoryOpportunity> offers)
    {
        if(!On(w)||HouseSuccession.Head(w,new(house))!=w.PlayerControl)return false;
        var defender=NationalStart.FactionId(e.Defender);
        bool borders=owners.Where(x=>x.Value==house).Any(x=>NationalStrategy.Neighbors(x.Key).Any(n=>owners.GetValueOrDefault(n)==defender));
        // 上申するのは家中の成人。本人と同じ城にいる者を先に。
        var here=PersonalCompanions.PhysicalSiteAt(w,w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);
        var sender=FamilyBirth.People(w).Where(p=>p.Faction==house&&p.Id!=w.PlayerControl.Value&&w.Characters.SingleOrDefault(c=>c.Id.Value==p.Id)?.Life==LifeState.Adult).Select(p=>new EntityId(p.Id)).OrderBy(p=>PersonalCompanions.PhysicalSiteAt(w,p,w.ElapsedSeconds,w.CommitSequence+1) is {} s&&s==here?0:1).ThenBy(p=>p.Value,StringComparer.Ordinal).FirstOrDefault();
        if(!borders||sender.Value==null||w.Characters.Single(c=>c.Id==w.PlayerControl).Life!=LifeState.Adult)return false;
        var letter=new EntityId("message:history-opportunity/"+Hash(e.Id+"/"+house));
        string body=Compose(w,sender,w.PlayerControl,$"{HouseName(w,new(defender))}を攻める好機と存じます。{EventName(e.Id)}の儀、御下知を賜りたく。");
        MessageFlow.Send(w.Messaging,new MessageEnvelope(letter.Value,"history:"+Hash(e.Id),HistoryKind,sender.Value,w.PlayerControl.Value,body,FamilyBirth.People(w).Single(p=>p.Id==w.PlayerControl.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
        w.SealedMessages=[..w.SealedMessages,letter];
        offers.Add(new(e.Id,new(house),letter,sender,w.ElapsedSeconds,w.CommitSequence));
        RegionalCampaign.Bind(w);
        return true;
    }
    internal static void Answer(ContractWorld w,EntityId letter,bool accept)
    {
        var o=OpenOpportunities(w).SingleOrDefault(o=>o.Letter==letter&&o.Accepted==null);
        Need(o!=null&&HouseSuccession.Head(w,o.House)==w.PlayerControl,"答えられる上申ではありません");
        w.Strategy=w.Strategy! with{Opportunities=w.Strategy.Opportunities!.Select(x=>x.Letter==letter?x with{Accepted=accept,AnsweredAt=w.ElapsedSeconds}:x).ToArray()};
    }
    // 翌月の初め：応じた機会は、相手の家と約束がなく境に城があれば出陣する。約束があれば競合で不発。断った・答えなかったものは見送り。
    internal static HistoryOpportunity[]? Settle(ContractWorld w,StrategyState s,Dictionary<string,string?> owners,List<StrategyCampaign> campaigns,List<StrategyHistoryRecord> history,List<StrategyPact> pacts)
    {
        if(!On(w)||s.Opportunities is not {} rows)return s.Opportunities;
        long now=w.ElapsedSeconds;var date=WorldCalendar.At(w.EpochDate,now);var result=rows.ToArray();
        for(int n=0;n<result.Length;n++)
        {
            var o=result[n];if(o.Result!=null||o.At>=now)continue;
            string outcome;var house=o.House.Value;var e=NationalStrategy.HistoryFor(w).Single(x=>x.Id==o.Event);var defender=NationalStart.FactionId(e.Defender);
            if(o.Accepted!=true)outcome=o.Accepted==false?"player-declined":"lapsed";
            else if(pacts.Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==house&&p.B.Value==defender||p.B.Value==house&&p.A.Value==defender)))outcome="conflict";
            else if(owners.Where(x=>x.Value==house).SelectMany(x=>NationalStrategy.Neighbors(x.Key)).Distinct(StringComparer.Ordinal).Where(t=>owners.GetValueOrDefault(t)==defender).OrderBy(t=>t,StringComparer.Ordinal).FirstOrDefault() is not {} site)outcome="premise";
            else if(campaigns.Any(c=>c.Attacker.Value==house&&c.At==now))outcome="conflict";
            else{campaigns.Add(new(new($"campaign:national/{date:yyyyMM}/{Hash(house+"/history/"+o.Event)[..12]}"),o.House,new(defender),new(site),now,w.CommitSequence,"history:"+o.Event));outcome="fired";}
            result[n]=o with{Result=outcome};history.Add(new(o.Event,now,w.CommitSequence,outcome));
        }
        return result;
    }

    // ---- 自動の停止 ----
    // 本人が知った重い病状：本人の重さ3以上の傷病の始まり、本人が開いた病状報告に重さ3以上の見立て。
    static bool Grave(string severity)=>severity is "重傷" or "危篤";
    internal static IEnumerable<string> SevereIllness(ContractWorld w)
    {
        if(!On(w))yield break;long now=w.ElapsedSeconds;var me=w.PlayerControl;
        foreach(var c in PersonalHealth.Active(w,me,now).Where(c=>c.Severity>=3&&c.OnsetAt==now))yield return c.Id.Value;
        foreach(var k in w.Messaging.Knowledge.Where(k=>k.CharacterId==me.Value&&k.LearnedAt==now))
        {
            var m=w.Messaging.Messages.Single(m=>m.Id==k.MessageId);if(m.Kind!=ClinicalHealth.ReportKind)continue;
            if(ContractJson.Read<ClinicalReport>(Encoding.UTF8.GetBytes(m.Body)).Cards.Any(c=>Grave(c.Severity)))yield return m.Id;
        }
    }

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(!On(w)){Need(s.Orders==null&&s.Opportunities==null,"画面の仕上げの記録が一覧の外にあります");return;}
        foreach(var o in s.Orders??[])
            Need(o.Revision<=w.CommitSequence&&o.At<=w.ElapsedSeconds&&o.Priority is >=1 and <=9&&(o.UsedAt==null||o.UsedAt>=o.At&&o.UsedAt<=w.ElapsedSeconds)&&(o.CancelledAt==null||o.UsedAt==null),"軍団への命が不正です");
        foreach(var o in s.Opportunities??[])
            Need(NationalStrategy.HistoryFor(w).Any(e=>e.Id==o.Event)&&w.Messaging.Messages.Any(m=>m.Id==o.Letter.Value&&m.Kind==HistoryKind)&&o.At<=w.ElapsedSeconds&&(o.Accepted==null)==(o.AnsweredAt==null)&&o.Result is null or "fired" or "conflict" or "premise" or "player-declined" or "lapsed","史実の機会の記録が不正です");
        if(prior?.Opportunities is {} old)Need((s.Opportunities??[]).Take(old.Length).Select(o=>o with{Accepted=null,AnsweredAt=null,Result=null}).SequenceEqual(old.Select(o=>o with{Accepted=null,AnsweredAt=null,Result=null})),"過去の史実の機会が変更されています");
        if(prior?.Orders is {} oldOrders)Need((s.Orders??[]).Take(oldOrders.Length).Select(o=>o with{UsedAt=null,CancelledAt=null}).SequenceEqual(oldOrders.Select(o=>o with{UsedAt=null,CancelledAt=null})),"過去の軍団への命が変更されています");
        foreach(var c in s.Campaigns.Where(c=>c.Basis.StartsWith("order:",StringComparison.Ordinal)))
            Need((s.Orders??[]).Any(o=>"order:"+o.Id.Value==c.Basis&&o.House==c.Attacker&&o.UsedAt==c.At&&o.Commander==c.Commander),"軍団への命の出陣が命と合いません");
    }
}

public sealed partial class WorldSession
{
    public Screens2.LetterRow[] Letters(string? category=null){lock(gate)return Screens2.Letters(state,state.PlayerControl,category);}
    public Screens2.PersonBook? PersonBook(EntityId person){lock(gate)return Screens2.Book(state,state.PlayerControl,person);}
    public Screens2.Crisis[] Crises(){lock(gate)return Screens2.Crises(state,state.PlayerControl);}
    public (Screens2.PromiseRow[] House,Screens2.PromiseRow[] Private) Promises(){lock(gate)return Screens2.Promises(state,state.PlayerControl);}
    public Screens2.SuccessionBook? SuccessionBook(){lock(gate)return Screens2.Succession(state,state.PlayerControl);}
    public Screens2.ArmyRow[] HouseArmies(){lock(gate)return Screens2.Armies(state,state.PlayerControl);}
    public string[] CorpsProvinces(){lock(gate)return Screens2.Provinces(state,state.PlayerControl);}
    public EntityId[] CorpsCommanders(){lock(gate)return Screens2.Commanders(state,state.PlayerControl);}
    public HistoryOpportunity[] HistoryOpportunities(){lock(gate)return Screens2.OpenOpportunities(state).Where(o=>o.Accepted==null).ToArray();}
    CommandResult Screens2Command(string kind,long revision,Action<ContractWorld> apply,string done)
    {using var _ccRev=ControlContext.At(revision);
        lock(gate)
        {
            var id=new EntityId("request:screens2/"+kind+"/"+revision);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{apply(trial);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(apply);
            return new(ResultCode.Accepted,id,state.CommitSequence,done);
        }
    }
    public CommandResult OrderCorps(string province,EntityId commander,int priority,long revision)=>Screens2Command("order",revision,w=>Screens2.Order(w,province,commander,priority),"軍団に出陣を命じた");
    public CommandResult CancelCorpsOrder(EntityId order,long revision)=>Screens2Command("cancel",revision,w=>Screens2.Cancel(w,order),"命を取り消した");
    public CommandResult AnswerHistoryOpportunity(EntityId letter,bool accept,long revision)=>Screens2Command("history",revision,w=>Screens2.Answer(w,letter,accept),accept?"出陣を許した":"見送った");
}
