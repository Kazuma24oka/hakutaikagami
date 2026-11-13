using System.Text;
namespace HakutaiKagami.Core.Contracts;

public enum DevelopmentAbility { Leadership, Valor, Intelligence, Politics, Negotiation, Charm }
public enum DevelopmentSkill { Agriculture, Commerce, Construction, Logistics, Tactics, IntelligenceGathering, Etiquette }
public sealed record DevelopmentAward(EntityId Task,EntityId Actor,DevelopmentSkill Skill,long At,long Seconds,int Efficiency,long Experience);
public sealed record DevelopmentState(int Schema,string Policy,DevelopmentAward[] Awards);
public sealed record DevelopmentValue(string Name,int? Value,long Experience,string Source,long? ObservedAt,string Confidence);
public sealed record DevelopmentCard(EntityId Person,bool Own,DevelopmentValue[] Abilities,DevelopmentValue[] Skills,string Note);

public static class CharacterDevelopment
{
    public const string Policy="five-base-seven-skills-completed-time-evidence-v1";
    public const int PracticeSeconds=1800;
    static readonly DevelopmentAbility[] BaseAbilities=[DevelopmentAbility.Leadership,DevelopmentAbility.Valor,DevelopmentAbility.Intelligence,DevelopmentAbility.Politics,DevelopmentAbility.Charm];
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.DevelopmentPolicy==Policy;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Development=new(1,Policy,[]);}
    public static DevelopmentAbility Ability(DevelopmentSkill skill)=>skill switch{
        DevelopmentSkill.Agriculture or DevelopmentSkill.Construction=>DevelopmentAbility.Politics,
        DevelopmentSkill.Commerce or DevelopmentSkill.Etiquette=>DevelopmentAbility.Negotiation,
        DevelopmentSkill.Logistics=>DevelopmentAbility.Leadership,
        DevelopmentSkill.Tactics=>DevelopmentAbility.Valor,
        _=>DevelopmentAbility.Intelligence};
    public static string Name(DevelopmentAbility a)=>a switch{DevelopmentAbility.Leadership=>"統率",DevelopmentAbility.Valor=>"武勇",DevelopmentAbility.Intelligence=>"知略",DevelopmentAbility.Politics=>"政務",DevelopmentAbility.Negotiation=>"交渉",DevelopmentAbility.Charm=>"魅力",_=>throw new ArgumentOutOfRangeException(nameof(a))};
    public static string Name(DevelopmentSkill s)=>s switch{DevelopmentSkill.Agriculture=>"農政",DevelopmentSkill.Commerce=>"商取引",DevelopmentSkill.Construction=>"普請",DevelopmentSkill.Logistics=>"兵站",DevelopmentSkill.Tactics=>"戦術",DevelopmentSkill.IntelligenceGathering=>"情報",_=>"礼法"};
    static int Base(ContractWorld w,EntityId actor,DevelopmentAbility ability){
        var p=FamilyBirth.People(w).Single(x=>x.Id==actor.Value);var a=p.Attributes;
        if(a!=null)return ability switch{DevelopmentAbility.Leadership=>a.Leadership,DevelopmentAbility.Valor=>a.Valor,DevelopmentAbility.Intelligence=>a.Intelligence,DevelopmentAbility.Politics=>a.Politics,DevelopmentAbility.Negotiation=>a.Negotiation??50,DevelopmentAbility.Charm=>a.Charm??50,_=>throw new ArgumentOutOfRangeException(nameof(ability))};
        // Stable provisional balance values, explicitly not historical assessments.
        string hash=ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/"+w.NewGame!.Seed+"/"+actor.Value+"/"+ability));
        return 35+(int)(Convert.ToUInt32(hash[..8],16)%41);
    }
    static long Xp(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>rows.Where(a=>a.Actor==actor&&a.Skill==skill).Sum(a=>a.Experience);
    static int Skill(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>(int)Math.Min(100,Xp(rows,actor,skill)/7200);
    static int Value(ContractWorld w,IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentAbility ability)=>Math.Min(100,Base(w,actor,ability)+(int)(rows.Where(a=>a.Actor==actor&&Ability(a.Skill)==ability).Sum(a=>a.Experience)/360000));
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
            int efficiency=50+Value(w,prior,task.Executor,Ability(skill))/2+Skill(prior,task.Executor,skill)/2;
            long used=rows.Where(a=>a.Actor==task.Executor&&a.At/86400==end/86400).Sum(a=>a.Seconds);
            long credited=Math.Min(seconds,Math.Max(0,28800-used));
            rows.Add(new(task.Id,task.Executor,skill,end,credited,efficiency,credited*efficiency/100));
        }
        return rows.ToArray();
    }
    internal static int ForecastPractice(ContractWorld w, EntityId actor, DevelopmentSkill skill, long at, bool includeDelivery = true)
    {
        var prior = (w.Development?.Awards ?? []).Where(a => a.Actor == actor && a.At < at).ToArray();
        long xp = Xp(prior, actor, skill);
        int current = Skill(prior, actor, skill);
        int efficiency = 50 + Value(w, prior, actor, Ability(skill)) / 2 + current / 2;
        long end = checked(at + PracticeSeconds + (includeDelivery ? 1 : 0));
        long used = prior.Where(a => a.At / 86400 == end / 86400).Sum(a => a.Seconds);
        long credit = Math.Clamp(28800 - used, 0, PracticeSeconds) * efficiency / 100;
        return (int)(Math.Min(100, (xp + credit) / 7200) - current) * 8;
    }
    internal static int EffectiveAt(ContractWorld w,EntityId actor,DevelopmentAbility ability,long at)=>Math.Clamp(Value(w,(w.Development?.Awards??[]).Where(a=>a.At<at),actor,ability),1,100);
    internal static void Sync(ContractWorld w){if(Enabled(w))w.Development=w.Development! with{Awards=Derive(w)};}
    internal static DevelopmentSkill PreferredPractice(ContractWorld w,EntityId actor)=>Enum.GetValues<DevelopmentSkill>().OrderByDescending(s=>Base(w,actor,Ability(s))).ThenBy(s=>ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/practice/"+w.Seed+"/"+actor.Value+"/"+s)),StringComparer.Ordinal).First();
    internal static Intent PracticeForNpc(ContractWorld w,EntityId actor,string id,DevelopmentSkill skill){
        Need(Enabled(w)&&NewGameNpcRoutine.PurposeEnabled(w)&&NewGameNpcRoutine.IsActor(w,actor)&&Enum.IsDefined(skill),"NPC practice policy is required");
        var site=PersonalCompanions.PhysicalSiteAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1);Need(site!=null,"NPC must be at a site");long at=w.ElapsedSeconds;
        return new(new(id),actor,actor,site!.Value,ActionKind.Train,null,null,0,null,new(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),at,1,PracticeSeconds,1,false,TaskKind.BindingOrder){TrainingSkill=skill};
    }
    public static Intent Practice(ContractWorld w,string id,DevelopmentSkill skill){
        Need(Enabled(w)&&Enum.IsDefined(skill),"修行方式が有効ではありません");
        var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);Need(site.Kind==LocationKind.AtSite&&site.Site!=null,"拠点に到着してから修行してください");
        long at=w.ElapsedSeconds;
        return new(new(id),w.PlayerControl,w.PlayerControl,site.Site!.Value,ActionKind.Train,null,null,0,null,new(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),at,1,PracticeSeconds,1,false,TaskKind.BindingOrder){TrainingSkill=skill};
    }
    internal static void ValidatePractice(ContractWorld w,Intent i,long at,bool pending){using var _cc=pending?ControlContext.Keep():ControlContext.At(w.Requests.Single(r=>r.Id==i.RequestId).CommitSequence);
        Need(Enabled(w)&&(i.Actor==w.PlayerControl||NewGameNpcRoutine.PurposeEnabled(w)&&(pending?NewGameNpcRoutine.IsActor(w,i.Actor):NewGameNpcRoutine.WasActor(w,i.Actor,at)))&&i.Executor==i.Actor&&i.Action==ActionKind.Train&&i.TrainingSkill!=null&&Enum.IsDefined(i.TrainingSkill.Value),"Invalid personal training");
        Need(i.WorkSeconds==PracticeSeconds&&i.EffectiveAt==at&&i.DeliverySeconds==1&&i.ReportSeconds==1&&!i.Important&&i.Kind==TaskKind.BindingOrder&&i.Deadline==new DeadlinePolicy(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),"Training duration or deadline changed");
        if(pending){Need(i==(i.Actor==w.PlayerControl?Practice(w,i.RequestId.Value,i.TrainingSkill!.Value):PracticeForNpc(w,i.Actor,i.RequestId.Value,i.TrainingSkill!.Value)),"Training location changed");Need(!w.Tasks.Any(t=>t.Executor==i.Actor&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)),"先に現在の用件を終えるか中断してください");}
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
            var abilities=BaseAbilities.Select(a=>new DevelopmentValue(Name(a),Value(w,rows,person,a),rows.Where(x=>x.Actor==person&&Ability(x.Skill)==a).Sum(x=>x.Experience),FamilyBirth.People(w).Single(p=>p.Id==person.Value).Attributes==null?"試作の固定初期値＋実行記録（史実評価ではありません）":"登録能力＋実行記録",w.ElapsedSeconds,"本人の正確値")).ToArray();
            var skills=Enum.GetValues<DevelopmentSkill>().Select(s=>new DevelopmentValue(Name(s),Skill(rows,person,s),Xp(rows,person,s),"完了した修行・点検の実時間",w.ElapsedSeconds,"本人の正確値")).ToArray();
            return new(person,true,abilities,skills,"無料修行は30分。完了時のみ経験を確定し、1日最大8時間分。未完了分は加算しません。経験7200で技能1、対応経験360000で基礎能力1（上限100）。勲功・役職とは別です。健康・負傷・加齢の補正は未導入です。");
        }
        // Only received factual reports enter this view. Never read the subject's private profile or XP.
        var facts=w.Outbox.Where(o=>o.Sender==person&&o.Recipient==observer&&o.Kind=="contract-report"&&o.Body=="任務完了"&&w.Messaging.Knowledge.Any(k=>k.CharacterId==observer.Value&&k.MessageId==o.Id.Value)).OrderByDescending(o=>o.At).ToArray();
        var seen=facts.FirstOrDefault(o=>w.Tasks.Any(t=>t.Id==o.Task&&t.Action==ActionKind.Inspect));
        var unknown=BaseAbilities.Select(a=>Estimate(w,rows,observer,person,a)??new DevelopmentValue(Name(a),null,0,"未確認",null,"不明")).ToArray();
        var estimates=Enum.GetValues<DevelopmentSkill>().Select(s=>s==DevelopmentSkill.IntelligenceGathering&&seen!=null?new DevelopmentValue(Name(s),null,0,"本人から届いた点検完了報告："+seen.Id,seen.At,w.ElapsedSeconds-seen.At>30*86400?"古い実績・熟練値は未確認":"点検完了の実績あり・熟練値は未確認"):new DevelopmentValue(Name(s),null,0,"未確認",null,"不明")).ToArray();
        return new(person,false,unknown,estimates,"所属・登用だけで能力や内心は公開されません。報告のない実行や、相手の真の数値は表示しません。");
    }
    public static void Validate(ContractWorld w){
        if(!Enabled(w)){Need(w.Development==null&&!w.Tasks.Any(t=>t.Action==ActionKind.Train),"Development data without opt-in policy");return;}
        Need(w.Development is {Schema:1,Policy:Policy,Awards:not null},"Missing development state");
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
