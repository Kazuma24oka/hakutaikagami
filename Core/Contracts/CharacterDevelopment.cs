using System.Text;
namespace HakutaiKagami.Core.Contracts;

public enum DevelopmentAbility { Leadership, Valor, Intelligence, Politics, Negotiation }
public enum DevelopmentSkill { Agriculture, Commerce, Construction, Logistics, Tactics, IntelligenceGathering, Etiquette }
public sealed record DevelopmentAward(EntityId Task,EntityId Actor,DevelopmentSkill Skill,long At,long Seconds,int Efficiency,long Experience);
public sealed record DevelopmentState(int Schema,string Policy,DevelopmentAward[] Awards);
public sealed record DevelopmentValue(string Name,int? Value,long Experience,string Source,long? ObservedAt,string Confidence);
public sealed record DevelopmentCard(EntityId Person,bool Own,DevelopmentValue[] Abilities,DevelopmentValue[] Skills,string Note);

public static class CharacterDevelopment
{
    public const string Policy="five-base-seven-skills-completed-time-evidence-v1";
    public const int PracticeSeconds=1800;
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.DevelopmentPolicy==Policy;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Development=new(1,Policy,[]);}
    public static DevelopmentAbility Ability(DevelopmentSkill skill)=>skill switch{
        DevelopmentSkill.Agriculture or DevelopmentSkill.Construction=>DevelopmentAbility.Politics,
        DevelopmentSkill.Commerce or DevelopmentSkill.Etiquette=>DevelopmentAbility.Negotiation,
        DevelopmentSkill.Logistics=>DevelopmentAbility.Leadership,
        DevelopmentSkill.Tactics=>DevelopmentAbility.Valor,
        _=>DevelopmentAbility.Intelligence};
    public static string Name(DevelopmentAbility a)=>a switch{DevelopmentAbility.Leadership=>"統率",DevelopmentAbility.Valor=>"武勇",DevelopmentAbility.Intelligence=>"知略",DevelopmentAbility.Politics=>"政務",_=>"交渉"};
    public static string Name(DevelopmentSkill s)=>s switch{DevelopmentSkill.Agriculture=>"農政",DevelopmentSkill.Commerce=>"商取引",DevelopmentSkill.Construction=>"普請",DevelopmentSkill.Logistics=>"兵站",DevelopmentSkill.Tactics=>"戦術",DevelopmentSkill.IntelligenceGathering=>"情報",_=>"礼法"};
    static int Base(ContractWorld w,EntityId actor,DevelopmentAbility ability){
        var p=w.NewGame!.Start.People.Single(x=>x.Id==actor.Value);var a=p.Attributes;
        if(a!=null)return ability switch{DevelopmentAbility.Leadership=>a.Leadership,DevelopmentAbility.Valor=>a.Valor,DevelopmentAbility.Intelligence=>a.Intelligence,DevelopmentAbility.Politics=>a.Politics,_=>a.Negotiation??50};
        // Stable provisional balance values, explicitly not historical assessments.
        string hash=ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/"+w.NewGame.Seed+"/"+actor.Value+"/"+ability));
        return 35+(int)(Convert.ToUInt32(hash[..8],16)%41);
    }
    static long Xp(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>rows.Where(a=>a.Actor==actor&&a.Skill==skill).Sum(a=>a.Experience);
    static int Skill(IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentSkill skill)=>(int)Math.Min(100,Xp(rows,actor,skill)/7200);
    static int Value(ContractWorld w,IEnumerable<DevelopmentAward> rows,EntityId actor,DevelopmentAbility ability)=>Math.Min(100,Base(w,actor,ability)+(int)(rows.Where(a=>a.Actor==actor&&Ability(a.Skill)==ability).Sum(a=>a.Experience)/360000));
    static DevelopmentAward[] Derive(ContractWorld w){
        var rows=new List<DevelopmentAward>();
        foreach(var task in w.Tasks.Where(t=>t.Execution==ExecutionPhase.Completed&&t.InternalReason=="completed"&&t.Action is ActionKind.Train or ActionKind.Inspect).OrderBy(t=>t.FinishedAt).ThenBy(t=>t.Revision).ThenBy(t=>t.Id.Value,StringComparer.Ordinal)){
            var i=w.Requests.Single(r=>r.Id==task.RequestId).Intent;
            var action=w.Actions.Single(a=>a.Task==task.Id);
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
    internal static void Sync(ContractWorld w){if(Enabled(w))w.Development=w.Development! with{Awards=Derive(w)};}
    public static Intent Practice(ContractWorld w,string id,DevelopmentSkill skill){
        Need(Enabled(w)&&Enum.IsDefined(skill),"修行方式が有効ではありません");
        var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);Need(site.Kind==LocationKind.AtSite&&site.Site!=null,"拠点に到着してから修行してください");
        long at=w.ElapsedSeconds;
        return new(new(id),w.PlayerControl,w.PlayerControl,site.Site!.Value,ActionKind.Train,null,null,0,null,new(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),at,1,PracticeSeconds,1,false,TaskKind.BindingOrder){TrainingSkill=skill};
    }
    internal static void ValidatePractice(ContractWorld w,Intent i,long at,bool pending){
        Need(Enabled(w)&&i.Actor==w.PlayerControl&&i.Executor==i.Actor&&i.Action==ActionKind.Train&&i.TrainingSkill!=null&&Enum.IsDefined(i.TrainingSkill.Value),"Invalid personal training");
        Need(i.WorkSeconds==PracticeSeconds&&i.EffectiveAt==at&&i.DeliverySeconds==1&&i.ReportSeconds==1&&!i.Important&&i.Kind==TaskKind.BindingOrder&&i.Deadline==new DeadlinePolicy(DeadlineCriterion.Completion,at+PracticeSeconds+60,false,null,at+PracticeSeconds+60),"Training duration or deadline changed");
        if(pending){Need(i==Practice(w,i.RequestId.Value,i.TrainingSkill!.Value),"Training location changed");Need(!w.Tasks.Any(t=>t.Executor==i.Actor&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)),"先に現在の用件を終えるか中断してください");}
    }
    public static DevelopmentCard? View(ContractWorld w,EntityId observer,EntityId person){
        if(!Enabled(w)||!w.Characters.Any(c=>c.Id==observer)||!w.Characters.Any(c=>c.Id==person))return null;
        bool own=observer==person;var rows=w.Development!.Awards;
        if(own){
            var abilities=Enum.GetValues<DevelopmentAbility>().Select(a=>new DevelopmentValue(Name(a),Value(w,rows,person,a),rows.Where(x=>x.Actor==person&&Ability(x.Skill)==a).Sum(x=>x.Experience),w.NewGame!.Start.People.Single(p=>p.Id==person.Value).Attributes==null?"試作の固定初期値＋実行記録（史実評価ではありません）":"登録能力＋実行記録",w.ElapsedSeconds,"本人の正確値")).ToArray();
            var skills=Enum.GetValues<DevelopmentSkill>().Select(s=>new DevelopmentValue(Name(s),Skill(rows,person,s),Xp(rows,person,s),"完了した修行・点検の実時間",w.ElapsedSeconds,"本人の正確値")).ToArray();
            return new(person,true,abilities,skills,"無料修行は30分。完了時のみ経験を確定し、1日最大8時間分。未完了分は加算しません。経験7200で技能1、対応経験360000で基礎能力1（上限100）。勲功・役職とは別です。健康・負傷・加齢の補正は未導入です。");
        }
        // Only received factual reports enter this view. Never read the subject's private profile or XP.
        var facts=w.Outbox.Where(o=>o.Sender==person&&o.Recipient==observer&&o.Kind=="contract-report"&&o.Body=="任務完了"&&w.Messaging.Knowledge.Any(k=>k.CharacterId==observer.Value&&k.MessageId==o.Id.Value)).OrderByDescending(o=>o.At).ToArray();
        var seen=facts.FirstOrDefault(o=>w.Tasks.Any(t=>t.Id==o.Task&&t.Action==ActionKind.Inspect));
        var unknown=Enum.GetValues<DevelopmentAbility>().Select(a=>new DevelopmentValue(Name(a),null,0,"未確認",null,"不明")).ToArray();
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
