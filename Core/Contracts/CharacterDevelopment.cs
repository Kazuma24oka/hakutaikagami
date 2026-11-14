using System.Text;
namespace HakutaiKagami.Core.Contracts;

public enum DevelopmentAbility { Leadership, Valor, Intelligence, Politics, Negotiation, Charm }
public enum DevelopmentSkill { Agriculture, Commerce, Construction, Logistics, Tactics, IntelligenceGathering, Etiquette,
    // 32技能の版（2026-11-14、Docs/Decisions/2026-11-06_CHARACTER_ABILITIES_SKILLS_GROWTH.md）で加えた25技能。旧版の世界は上の7つだけ。
    Siege, Drill, Navy, Swordsmanship, Spear, Bow, Cavalry, Gun, Finance, Adjudication, Irrigation, Negotiation, Mediation, TalentAssessment,
    Subversion, Guard, Stealth, Mining, Workshop, Appraisal, Tea, Renga, Teaching, Writing, Medicine }
public sealed record DevelopmentAward(EntityId Task,EntityId Actor,DevelopmentSkill Skill,long At,long Seconds,int Efficiency,long Experience);
public sealed record DevelopmentState(int Schema,string Policy,DevelopmentAward[] Awards);
public sealed record DevelopmentValue(string Name,int? Value,long Experience,string Source,long? ObservedAt,string Confidence);
public sealed record DevelopmentCard(EntityId Person,bool Own,DevelopmentValue[] Abilities,DevelopmentValue[] Skills,string Note);

public static class CharacterDevelopment
{
    public const string Policy="five-base-seven-skills-completed-time-evidence-v1";
    // 32技能の版：技能レベルは累積経験 3600×L×(L+1) で決まり（0〜100）、独習（修行）は実働の50%、実務は100%を経験にする。1日8時間まで。
    // 基礎能力は使った主能力へ 7,200,000 経験で1上がる。開始時の熟練は人物ごとに持つ（史料のない人は試作の初期値で、未採録の印を付ける）。新規の全国開始から。
    public const string Policy32="five-base-32-skills-experience-curve-v2";
    public static bool IsPolicy(string? policy)=>policy is Policy or Policy32;
    public static bool Skills32(ContractWorld w)=>w.NewGame?.DevelopmentPolicy==Policy32;
    public static DevelopmentSkill[] Skills(ContractWorld w)=>Skills32(w)?Enum.GetValues<DevelopmentSkill>():Enum.GetValues<DevelopmentSkill>().Take(7).ToArray();
    public const int PracticeSeconds=1800;
    static readonly DevelopmentAbility[] BaseAbilities=[DevelopmentAbility.Leadership,DevelopmentAbility.Valor,DevelopmentAbility.Intelligence,DevelopmentAbility.Politics,DevelopmentAbility.Charm];
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&IsPolicy(w.NewGame?.DevelopmentPolicy);
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Development=new(1,w.NewGame!.DevelopmentPolicy!,[]);}
    public static DevelopmentAbility Ability(DevelopmentSkill skill)=>skill switch{
        DevelopmentSkill.Agriculture or DevelopmentSkill.Construction=>DevelopmentAbility.Politics,
        DevelopmentSkill.Commerce or DevelopmentSkill.Etiquette=>DevelopmentAbility.Negotiation,
        DevelopmentSkill.Logistics=>DevelopmentAbility.Leadership,
        DevelopmentSkill.Tactics=>DevelopmentAbility.Valor,
        _=>DevelopmentAbility.Intelligence};
    public static string Name(DevelopmentAbility a)=>a switch{DevelopmentAbility.Leadership=>"統率",DevelopmentAbility.Valor=>"武勇",DevelopmentAbility.Intelligence=>"知略",DevelopmentAbility.Politics=>"政務",DevelopmentAbility.Negotiation=>"交渉",DevelopmentAbility.Charm=>"魅力",_=>throw new ArgumentOutOfRangeException(nameof(a))};
    public static string Name(DevelopmentSkill s)=>s switch{DevelopmentSkill.Agriculture=>"農政",DevelopmentSkill.Commerce=>"商取引",DevelopmentSkill.Construction=>"普請",DevelopmentSkill.Logistics=>"兵站",DevelopmentSkill.Tactics=>"戦術",DevelopmentSkill.IntelligenceGathering=>"情報",DevelopmentSkill.Etiquette=>"礼法",
        DevelopmentSkill.Siege=>"攻守城",DevelopmentSkill.Drill=>"調練",DevelopmentSkill.Navy=>"水軍",DevelopmentSkill.Swordsmanship=>"剣術",DevelopmentSkill.Spear=>"槍",DevelopmentSkill.Bow=>"弓",DevelopmentSkill.Cavalry=>"騎馬",DevelopmentSkill.Gun=>"鉄砲",
        DevelopmentSkill.Finance=>"財務",DevelopmentSkill.Adjudication=>"裁許",DevelopmentSkill.Irrigation=>"治水",DevelopmentSkill.Negotiation=>"交渉",DevelopmentSkill.Mediation=>"調停",DevelopmentSkill.TalentAssessment=>"人材評価",
        DevelopmentSkill.Subversion=>"調略",DevelopmentSkill.Guard=>"警固",DevelopmentSkill.Stealth=>"隠密",DevelopmentSkill.Mining=>"鉱山",DevelopmentSkill.Workshop=>"工房管理",DevelopmentSkill.Appraisal=>"鑑定",
        DevelopmentSkill.Tea=>"茶道",DevelopmentSkill.Renga=>"連歌",DevelopmentSkill.Teaching=>"教育",DevelopmentSkill.Writing=>"文筆",DevelopmentSkill.Medicine=>"医術",_=>throw new ArgumentOutOfRangeException(nameof(s))};
    // 32技能の版の主能力（決定の表の最初の主能力）。
    public static DevelopmentAbility Main(DevelopmentSkill s)=>s switch{
        DevelopmentSkill.Drill or DevelopmentSkill.Navy or DevelopmentSkill.Guard=>DevelopmentAbility.Leadership,
        DevelopmentSkill.Swordsmanship or DevelopmentSkill.Spear or DevelopmentSkill.Bow or DevelopmentSkill.Cavalry or DevelopmentSkill.Gun=>DevelopmentAbility.Valor,
        DevelopmentSkill.Logistics or DevelopmentSkill.Agriculture or DevelopmentSkill.Construction or DevelopmentSkill.Finance or DevelopmentSkill.Adjudication or DevelopmentSkill.Irrigation or DevelopmentSkill.Etiquette or DevelopmentSkill.Commerce or DevelopmentSkill.Mining or DevelopmentSkill.Workshop or DevelopmentSkill.Writing=>DevelopmentAbility.Politics,
        DevelopmentSkill.Tea or DevelopmentSkill.Teaching=>DevelopmentAbility.Charm,
        _=>DevelopmentAbility.Intelligence};
    static DevelopmentAbility AbilityFor(ContractWorld w,DevelopmentSkill s)=>Skills32(w)?Main(s):Ability(s);
    // 32技能の版：レベルLの累積経験の下限 3600×L×(L+1)。
    public static long LevelXp(int level)=>3600L*level*(level+1);
    public static int LevelOf(long xp){int l=0;while(l<100&&LevelXp(l+1)<=xp)l++;return l;}
    public const long AbilityXp=7_200_000;
    // 開始時の熟練（32技能の版）。史料の採録がまだないので、試作の初期値：その人の得意な技能を4つ（武器1つと、主能力の高い順に3つ）だけ持ち、ほかは0（未採録）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<string,Dictionary<DevelopmentSkill,int>>> starts=new();
    internal static int StartLevel(ContractWorld w,EntityId person,DevelopmentSkill skill)=>!Skills32(w)?0:
        w.NewGame!.Registered.SingleOrDefault(r=>r.Id==person.Value)?.StartSkills is {} chosen?chosen.SingleOrDefault(x=>x.Skill==skill)?.Level??0:Starts(w,person).GetValueOrDefault(skill);
    // 作成武将の技能経験の予算（決定の第5節）：標準実務3000時間相当、1技能の開始上限50。
    public const long CreatedBudget=3000L*3600;public const int CreatedCap=50;
    public static long Spent(IEnumerable<OfficerSkill> skills)=>skills.Sum(x=>LevelXp(x.Level));
    public static void CheckCreated(OfficerSkill[]? skills)
    {
        if(skills==null)return;
        Need(skills.All(x=>Enum.IsDefined(x.Skill)&&x.Level is >=1 and <=CreatedCap)&&skills.Select(x=>x.Skill).Distinct().Count()==skills.Length&&Spent(skills)<=CreatedBudget,"開始時の技能は1技能50まで、合計は予算の内にしてください");
    }
    // 経歴の推奨配分（予算と上限の内）。
    public static readonly (string Name,OfficerSkill[] Skills)[] Backgrounds=[
        ("武辺",[new(DevelopmentSkill.Swordsmanship,30),new(DevelopmentSkill.Spear,25),new(DevelopmentSkill.Cavalry,20),new(DevelopmentSkill.Tactics,20),new(DevelopmentSkill.Drill,15)]),
        ("兵法",[new(DevelopmentSkill.Tactics,30),new(DevelopmentSkill.Siege,25),new(DevelopmentSkill.Logistics,20),new(DevelopmentSkill.Drill,20),new(DevelopmentSkill.Bow,15)]),
        ("内政",[new(DevelopmentSkill.Agriculture,30),new(DevelopmentSkill.Construction,25),new(DevelopmentSkill.Finance,20),new(DevelopmentSkill.Irrigation,20),new(DevelopmentSkill.Writing,15)]),
        ("商い",[new(DevelopmentSkill.Commerce,30),new(DevelopmentSkill.Negotiation,25),new(DevelopmentSkill.Appraisal,20),new(DevelopmentSkill.Finance,20),new(DevelopmentSkill.Navy,15)]),
        ("文雅",[new(DevelopmentSkill.Etiquette,30),new(DevelopmentSkill.Tea,25),new(DevelopmentSkill.Renga,20),new(DevelopmentSkill.Writing,20),new(DevelopmentSkill.Teaching,15)]),
        ("医術",[new(DevelopmentSkill.Medicine,35),new(DevelopmentSkill.Teaching,20),new(DevelopmentSkill.Writing,20),new(DevelopmentSkill.Etiquette,15)]),
        ("忍び",[new(DevelopmentSkill.Stealth,30),new(DevelopmentSkill.IntelligenceGathering,25),new(DevelopmentSkill.Subversion,20),new(DevelopmentSkill.Guard,20),new(DevelopmentSkill.Swordsmanship,15)]),
    ];
    static Dictionary<DevelopmentSkill,int> Starts(ContractWorld w,EntityId person)=>starts.GetValue(w.NewGame!,_=>new(StringComparer.Ordinal)).GetOrAdd(person.Value,_=>{
        int H(string k)=>(int)(Convert.ToUInt32(ContractJson.Hash(Encoding.UTF8.GetBytes(Policy32+"/start/"+w.NewGame!.Seed+"/"+person.Value+"/"+k))[..8],16)%1000);
        var weapons=new[]{DevelopmentSkill.Swordsmanship,DevelopmentSkill.Spear,DevelopmentSkill.Bow,DevelopmentSkill.Cavalry};
        var weapon=weapons[H("weapon")%weapons.Length];
        var best=Enum.GetValues<DevelopmentSkill>().Where(s=>!weapons.Contains(s)&&s!=DevelopmentSkill.Gun).OrderByDescending(s=>Base(w,person,Main(s))+H(s.ToString())%25).ThenBy(s=>s).Take(3);
        var result=new Dictionary<DevelopmentSkill,int>();
        foreach(var s in best.Prepend(weapon))result[s]=Math.Clamp((Base(w,person,Main(s))-20)/2+H("level/"+s)%11,5,40);
        return result;});
    // 開始時の基礎能力は人物の定義（能力の記録かシード）だけで決まるので、人と能力ごとに一度だけ求めて覚える。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<(string,DevelopmentAbility),int>> bases=new();
    static int Base(ContractWorld w,EntityId actor,DevelopmentAbility ability)=>bases.GetValue(w.NewGame!,_=>new()).GetOrAdd((actor.Value,ability),_=>BaseCore(w,actor,ability));
    static int BaseCore(ContractWorld w,EntityId actor,DevelopmentAbility ability){
        var p=FamilyBirth.People(w).Single(x=>x.Id==actor.Value);var a=p.Attributes;
        if(a!=null)return ability switch{DevelopmentAbility.Leadership=>a.Leadership,DevelopmentAbility.Valor=>a.Valor,DevelopmentAbility.Intelligence=>a.Intelligence,DevelopmentAbility.Politics=>a.Politics,DevelopmentAbility.Negotiation=>a.Negotiation??50,DevelopmentAbility.Charm=>a.Charm??50,_=>throw new ArgumentOutOfRangeException(nameof(ability))};
        // Stable provisional balance values, explicitly not historical assessments.
        string hash=ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/"+w.NewGame!.Seed+"/"+actor.Value+"/"+ability));
        return 35+(int)(Convert.ToUInt32(hash[..8],16)%41);
    }
    static long Xp(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>rows.Where(a=>a.Actor==actor&&a.Skill==skill).Sum(a=>a.Experience);
    static int Skill(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>(int)Math.Min(100,Xp(rows,actor,skill)/7200);
    // 技能レベル（32技能の版は開始時の熟練の経験を足して曲線で求める）。
    static int SkillIn(ContractWorld w,IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>Skills32(w)?LevelOf(LevelXp(StartLevel(w,actor,skill))+Xp(rows,actor,skill)):Skill(rows,actor,skill);
    static int Value(ContractWorld w,IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentAbility ability)=>Skills32(w)
        ?Math.Min(100,Base(w,actor,ability)+(int)(rows.Where(a=>a.Actor==actor&&Main(a.Skill)==ability).Sum(a=>a.Experience)/AbilityXp))
        :Math.Min(100,Base(w,actor,ability)+(int)(rows.Where(a=>a.Actor==actor&&Ability(a.Skill)==ability).Sum(a=>a.Experience)/360000));
    static DevelopmentAward[] Derive(ContractWorld w){
        var rows=new List<DevelopmentAward>();
        var done=w.Tasks.Where(t=>t.Execution==ExecutionPhase.Completed&&t.InternalReason=="completed"&&t.Action is ActionKind.Train or ActionKind.Inspect).OrderBy(t=>t.FinishedAt).ThenBy(t=>t.Revision).ThenBy(t=>t.Id.Value,StringComparer.Ordinal).ToArray();
        if(done.Length==0)return [];
        // 依頼と行動は一度だけ引けるようにする（全国の人数で、終わった仕事ごとに一覧を頭から探さない）。
        var wanted=done.Select(t=>t.RequestId).ToHashSet();var tasks=done.Select(t=>t.Id).ToHashSet();
        var requests=w.Requests.Where(r=>wanted.Contains(r.Id)).GroupBy(r=>r.Id).ToDictionary(g=>g.Key,g=>g.Single());
        var actions=w.Actions.Where(a=>tasks.Contains(a.Task)).GroupBy(a=>a.Task).ToDictionary(g=>g.Key,g=>g.Single());
        foreach(var task in done){
            var i=requests[task.RequestId].Intent;
            var action=actions[task.Id];
            var skill=i.TrainingSkill??DevelopmentSkill.IntelligenceGathering;
            long end=task.FinishedAt!.Value,seconds=end-action.StartedAt;
            Need(seconds==i.WorkSeconds&&seconds>0,"Development requires actual completed common action time");
            // Skills are sampled at action start; never retroactively improve an earlier job.
            var prior=rows.Where(a=>a.At<=action.StartedAt).ToArray();
            // 32技能の版：独習（修行）は50%、実務（点検）は100%。旧版は能力と技能で決まる効率。
            int efficiency=Skills32(w)?(task.Action==ActionKind.Train?50:100):50+Value(w,prior,task.Executor,Ability(skill))/2+Skill(prior,task.Executor,skill)/2;
            long used=rows.Where(a=>a.Actor==task.Executor&&a.At/86400==end/86400).Sum(a=>a.Seconds);
            long credited=Math.Min(seconds,Math.Max(0,28800-used));
            // 師事：教える人が始めと終わりに同じ拠点にいて、その技能が学ぶ人より高ければ、100%＋教える人の教育×0.5%。教える人は教えた時間を教育の実務として得る。
            if(Skills32(w)&&i.Teacher is {} teacher&&Taught(w,prior,teacher,task.Executor,skill,task.Scope,action.StartedAt,end))
            {
                int teaching=SkillIn(w,prior,teacher,DevelopmentSkill.Teaching);efficiency=100+teaching/2;
                long tUsed=rows.Where(a=>a.Actor==teacher&&a.At/86400==end/86400).Sum(a=>a.Seconds);long tCredited=Math.Min(seconds,Math.Max(0,28800-tUsed));
                rows.Add(new(task.Id,task.Executor,skill,end,credited,efficiency,credited*efficiency/100));
                if(tCredited>0)rows.Add(new(task.Id,teacher,DevelopmentSkill.Teaching,end,tCredited,100,tCredited));
                continue;
            }
            rows.Add(new(task.Id,task.Executor,skill,end,credited,efficiency,credited*efficiency/100));
        }
        return rows.ToArray();
    }
    // 師事が成り立つか：教える人が学ぶ人と別人の成人で、始めと終わりに同じ拠点にいて、始めの時点でその技能が学ぶ人より高い。
    static bool Taught(ContractWorld w,DevelopmentAward[] prior,EntityId teacher,EntityId learner,DevelopmentSkill skill,EntityId site,long start,long end)=>
        teacher!=learner&&w.Characters.Any(c=>c.Id==teacher&&c.Life==LifeState.Adult)&&PersonalCompanions.PhysicalSiteAt(w,teacher,start,w.CommitSequence+1)==site&&PersonalCompanions.PhysicalSiteAt(w,teacher,end,w.CommitSequence+1)==site&&
        SkillIn(w,prior,teacher,skill)>SkillIn(w,prior,learner,skill);
    internal static int ForecastPractice(ContractWorld w, EntityId actor, DevelopmentSkill skill, long at, bool includeDelivery = true)
    {
        var prior = (w.Development?.Awards ?? []).Where(a => a.Actor == actor && a.At < at).ToArray();
        long xp = Xp(prior, actor, skill)+(Skills32(w)?LevelXp(StartLevel(w,actor,skill)):0);
        int current = SkillIn(w, prior, actor, skill);
        int efficiency = Skills32(w)?50:50 + Value(w, prior, actor, Ability(skill)) / 2 + current / 2;
        long end = checked(at + PracticeSeconds + (includeDelivery ? 1 : 0));
        long used = prior.Where(a => a.At / 86400 == end / 86400).Sum(a => a.Seconds);
        long credit = Math.Clamp(28800 - used, 0, PracticeSeconds) * efficiency / 100;
        return (int)((Skills32(w)?LevelOf(xp+credit):Math.Min(100, (xp + credit) / 7200)) - current) * 8;
    }
    // 熟練による作業時間（32技能の版）：基準時間×(1−0.25×技能/100)。技能は作業を始めた時点の値（後で上がっても遡らない）。旧版はそのまま。
    public static long WorkSeconds(ContractWorld w,EntityId actor,DevelopmentSkill skill,long baseSeconds,long at)=>!Skills32(w)?baseSeconds:checked(baseSeconds*(400-LevelAt(w,actor,skill,at))/400);
    // その時点の技能レベル（その時点より前に確定した経験まで）。
    internal static int LevelAt(ContractWorld w,EntityId actor,DevelopmentSkill skill,long at)=>!Enabled(w)||!Skills(w).Contains(skill)?0:SkillIn(w,(w.Development?.Awards??[]).Where(a=>a.At<at),actor,skill);
    internal static int EffectiveAt(ContractWorld w,EntityId actor,DevelopmentAbility ability,long at)=>Math.Clamp(Value(w,(w.Development?.Awards??[]).Where(a=>a.At<at),actor,ability),1,100);
    internal static void Sync(ContractWorld w){if(Enabled(w))w.Development=w.Development! with{Awards=Derive(w)};}
    // 好んで修行する技能は開始の定義（能力・シード）だけで決まるので、人ごとに一度だけ求めて覚える。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,System.Collections.Concurrent.ConcurrentDictionary<string,DevelopmentSkill>> preferred=new();
    internal static DevelopmentSkill PreferredPractice(ContractWorld w,EntityId actor)=>preferred.GetValue(w.NewGame!,_=>new(StringComparer.Ordinal)).GetOrAdd(actor.Value,_=>Skills(w).OrderByDescending(s=>Base(w,actor,AbilityFor(w,s))).ThenBy(s=>ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/practice/"+w.Seed+"/"+actor.Value+"/"+s)),StringComparer.Ordinal).First());
    internal static Intent PracticeForNpc(ContractWorld w,EntityId actor,string id,DevelopmentSkill skill){
        Need(Enabled(w)&&NewGameNpcRoutine.PurposeEnabled(w)&&NewGameNpcRoutine.IsActor(w,actor)&&Skills(w).Contains(skill),"NPC practice policy is required");
        var site=PersonalCompanions.PhysicalSiteAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1);Need(site!=null,"NPC must be at a site");long at=w.ElapsedSeconds;
        return new(new(id),actor,actor,site!.Value,ActionKind.Train,null,null,0,null,new(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),at,1,PracticeSeconds,1,false,TaskKind.BindingOrder){TrainingSkill=skill,Teacher=NpcTeacher(w,actor,site!.Value,skill,at)};
    }
    // 師事して修行する（32技能の版）。教える人がその拠点にいて、その技能が本人より高いこと。
    public static Intent Learn(ContractWorld w,string id,DevelopmentSkill skill,EntityId teacher){
        Need(Skills32(w),"師事は32技能の版だけです");var i=Practice(w,id,skill);long at=w.ElapsedSeconds;var prior=(w.Development?.Awards??[]).Where(a=>a.At<at).ToArray();
        Need(teacher!=w.PlayerControl&&w.Characters.Any(c=>c.Id==teacher&&c.Life==LifeState.Adult)&&PersonalCompanions.PhysicalSiteAt(w,teacher,at,w.CommitSequence+1)==i.Scope&&SkillIn(w,prior,teacher,skill)>SkillIn(w,prior,w.PlayerControl,skill),"同じ拠点にいて、その技能が高い人に師事してください");
        return i with{Teacher=teacher};
    }
    // NPC の師事（全国の開始の一覧の NpcTeachers と32技能の版）：修行のとき、同じ拠点にその技能が自分より高い成人がいれば、最も高い人に教わる。
    static EntityId? NpcTeacher(ContractWorld w,EntityId actor,EntityId site,DevelopmentSkill skill,long at)
    {
        if(!Skills32(w)||!NationalSystems.On(w,"NpcTeachers"))return null;var prior=(w.Development?.Awards??[]).Where(a=>a.At<at).ToArray();int mine=SkillIn(w,prior,actor,skill);
        return w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==site&&l.Actor!=actor).Select(l=>l.Actor).Where(p=>w.Characters.Any(c=>c.Id==p&&c.Life==LifeState.Adult)).Select(p=>(P:p,L:SkillIn(w,prior,p,skill))).Where(x=>x.L>mine)
            .OrderByDescending(x=>x.L).ThenBy(x=>x.P.Value,StringComparer.Ordinal).Select(x=>(EntityId?)x.P).FirstOrDefault();
    }
    // 本人の拠点にいて、その技能が本人より高い人（師事の相手の候補）。
    public static EntityId[] Teachers(ContractWorld w,DevelopmentSkill skill){
        if(!Skills32(w))return [];var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;if(site==null)return [];long at=w.ElapsedSeconds;var prior=(w.Development?.Awards??[]).Where(a=>a.At<at).ToArray();int mine=SkillIn(w,prior,w.PlayerControl,skill);
        return w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==site&&l.Actor!=w.PlayerControl).Select(l=>l.Actor).Where(p=>w.Characters.Any(c=>c.Id==p&&c.Life==LifeState.Adult)&&SkillIn(w,prior,p,skill)>mine).OrderByDescending(p=>SkillIn(w,prior,p,skill)).ThenBy(p=>p.Value,StringComparer.Ordinal).Take(5).ToArray();
    }
    public static Intent Practice(ContractWorld w,string id,DevelopmentSkill skill){
        Need(Enabled(w)&&Skills(w).Contains(skill),"修行方式が有効ではありません");
        var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);Need(site.Kind==LocationKind.AtSite&&site.Site!=null,"拠点に到着してから修行してください");
        long at=w.ElapsedSeconds;
        return new(new(id),w.PlayerControl,w.PlayerControl,site.Site!.Value,ActionKind.Train,null,null,0,null,new(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),at,1,PracticeSeconds,1,false,TaskKind.BindingOrder){TrainingSkill=skill};
    }
    internal static void ValidatePractice(ContractWorld w,Intent i,long at,bool pending){using var _cc=pending?ControlContext.Keep():ControlContext.At(w.Requests.Single(r=>r.Id==i.RequestId).CommitSequence);
        Need(Enabled(w)&&(i.Actor==w.PlayerControl||NewGameNpcRoutine.PurposeEnabled(w)&&(pending?NewGameNpcRoutine.IsActor(w,i.Actor):NewGameNpcRoutine.WasActor(w,i.Actor,at)))&&i.Executor==i.Actor&&i.Action==ActionKind.Train&&i.TrainingSkill!=null&&Skills(w).Contains(i.TrainingSkill.Value),"Invalid personal training");
        Need(i.WorkSeconds==PracticeSeconds&&i.EffectiveAt==at&&i.DeliverySeconds==1&&i.ReportSeconds==1&&!i.Important&&i.Kind==TaskKind.BindingOrder&&i.Deadline==new DeadlinePolicy(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),"Training duration or deadline changed");
        if(pending){Need(i==(i.Actor==w.PlayerControl?(i.Teacher is {} t?Learn(w,i.RequestId.Value,i.TrainingSkill!.Value,t):Practice(w,i.RequestId.Value,i.TrainingSkill!.Value)):PracticeForNpc(w,i.Actor,i.RequestId.Value,i.TrainingSkill!.Value)),"Training location changed");Need(!w.Tasks.Any(t=>t.Executor==i.Actor&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)),"先に現在の用件を終えるか中断してください");}
    }
    // 他人の基礎能力の推定（BD-16、全国の開始の一覧の AbilityReputation を持つ世界だけ）。真の値は渡さず、段階と推定幅・出典・時点だけを出す。
    // 面識（本人が相手との出来事を知っている）があれば幅が狭く、その最後の認識の時点。なければ、家の当主と同じ家の者は評判（幅が広く、開始時の時点）。どちらもなければ不明。
    // 推定の中心は真の値から、観る人・相手・能力・出典ごとの固定のずれ（評判は±10、面識は±4）を足したもの。
    static DevelopmentValue? Estimate(ContractWorld w,DevelopmentAward[] rows,EntityId observer,EntityId person,DevelopmentAbility a)
    {
        if(!NationalSystems.On(w,"AbilityReputation"))return null;
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        var met=w.Social?.Reactions.Where(r=>r.Observer==observer&&r.Subject==person&&r.LearnedAt<=at).Select(r=>(long?)r.LearnedAt).Max();
        bool Active(TimedRelation r)=>r.From<=at&&(at<r.Until||at==r.Until&&rev<r.Revision);
        bool famous=HouseSuccession.Factions(w).Any(f=>f.Head==person.Value)||w.Memberships.Any(m=>m.Subject==person&&Active(m)&&w.Memberships.Any(o=>o.Subject==observer&&o.Target==m.Target&&Active(o)));
        if(met==null&&!famous)return null;
        string source=met!=null?"面識":"評判";int spread=met!=null?4:10,width=met!=null?6:15;
        var d=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"ability-estimate-v1/{w.Seed}/{observer.Value}/{person.Value}/{a}/{source}"));
        int center=Math.Clamp(Value(w,rows,person,a)+d[0]%(2*spread+1)-spread,1,100),lo=Math.Max(1,center-width),hi=Math.Min(100,center+width);
        string tier=center<30?"低い":center<50?"並":center<70?"高い":"非常に高い";
        return new DevelopmentValue(Name(a),null,0,source,met??0,$"{tier}（{lo}〜{hi}）");
    }
    public static DevelopmentCard? View(ContractWorld w,EntityId observer,EntityId person){
        if(!Enabled(w)||!w.Characters.Any(c=>c.Id==observer)||!w.Characters.Any(c=>c.Id==person))return null;
        bool own=observer==person;var rows=w.Development!.Awards;
        if(own){
            var abilities=BaseAbilities.Select(a=>new DevelopmentValue(Name(a),Value(w,rows,person,a),rows.Where(x=>x.Actor==person&&AbilityFor(w,x.Skill)==a).Sum(x=>x.Experience),FamilyBirth.People(w).Single(p=>p.Id==person.Value).Attributes==null?"試作の固定初期値＋実行記録（史実評価ではありません）":"登録能力＋実行記録",w.ElapsedSeconds,"本人の正確値")).ToArray();
            var skills=Skills(w).Select(s=>new DevelopmentValue(Name(s),SkillIn(w,rows,person,s),Xp(rows,person,s)+(Skills32(w)?LevelXp(StartLevel(w,person,s)):0),Skills32(w)&&StartLevel(w,person,s)>0?"試作の初期値（未採録）＋完了した修行・点検の実時間":"完了した修行・点検の実時間",w.ElapsedSeconds,"本人の正確値")).ToArray();
            return new(person,true,abilities,skills,"無料修行は30分。完了時のみ経験を確定し、1日最大8時間分。未完了分は加算しません。経験7200で技能1、対応経験360000で基礎能力1（上限100）。勲功・役職とは別です。健康・負傷・加齢の補正は未導入です。");
        }
        // Only received factual reports enter this view. Never read the subject's private profile or XP.
        var facts=w.Outbox.Where(o=>o.Sender==person&&o.Recipient==observer&&o.Kind=="contract-report"&&o.Body=="任務完了"&&w.Messaging.Knowledge.Any(k=>k.CharacterId==observer.Value&&k.MessageId==o.Id.Value)).OrderByDescending(o=>o.At).ToArray();
        var seen=facts.FirstOrDefault(o=>w.Tasks.Any(t=>t.Id==o.Task&&t.Action==ActionKind.Inspect));
        var unknown=BaseAbilities.Select(a=>Estimate(w,rows,observer,person,a)??new DevelopmentValue(Name(a),null,0,"未確認",null,"不明")).ToArray();
        var estimates=Skills(w).Select(s=>s==DevelopmentSkill.IntelligenceGathering&&seen!=null?new DevelopmentValue(Name(s),null,0,"本人から届いた点検完了報告："+seen.Id,seen.At,w.ElapsedSeconds-seen.At>30*86400?"古い実績・熟練値は未確認":"点検完了の実績あり・熟練値は未確認"):new DevelopmentValue(Name(s),null,0,"未確認",null,"不明")).ToArray();
        return new(person,false,unknown,estimates,"所属・登用だけで能力や内心は公開されません。報告のない実行や、相手の真の数値は表示しません。");
    }
    public static void Validate(ContractWorld w){
        if(!Enabled(w)){Need(w.Development==null&&!w.Tasks.Any(t=>t.Action==ActionKind.Train),"Development data without opt-in policy");return;}
        Need(w.Development is {Schema:1,Awards:not null}&&w.Development.Policy==w.NewGame!.DevelopmentPolicy,"Missing development state");
        Need(w.Development!.Awards.SequenceEqual(Derive(w)),"Development awards detached from actual execution");
    }
}

public sealed partial class WorldSession
{
    public DevelopmentCard? CharacterCard(EntityId person){lock(gate)return CharacterDevelopment.View(state,state.PlayerControl,person);}
    public CommandResult StopPractice(EntityId task,long expectedRevision){lock(gate){
        var t=state.Tasks.SingleOrDefault(t=>t.Id==task&&t.Executor==state.PlayerControl&&t.Action==ActionKind.Train);
        if(t==null||!CharacterDevelopment.Enabled(state))return new(ResultCode.NotAvailable,task,state.CommitSequence,"本人の修行ではありません");
        if(t.Execution==ExecutionPhase.Stopped)return new(ResultCode.AlreadyAccepted,task,state.CommitSequence,"中断済みです");
        if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,task,state.CommitSequence,"表示を更新してください");
        if(t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)return new(ResultCode.NotAvailable,task,state.CommitSequence,"中断できません");
        Change(s=>{Stop(s,t,"cancelled");FlushOutbox(s);});return new(ResultCode.Accepted,task,state.CommitSequence,"修行を中断しました。未完了分は加算しません");
    }}
}
