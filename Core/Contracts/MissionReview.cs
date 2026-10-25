namespace HakutaiKagami.Core.Contracts;

// Receiving a claim does not prove merit or fault. This view never changes the world.
public static class MissionReview
{
    public const string Ruleset="officer-mission-review-v1";
    public const string Contract="known-self-interruption-separate-explanation-provisional-evidence-review-v1";
    public const string ExplanationKind="mission-explanation";
    public const string InterruptedReport="担当者が任務を中断（本人返報）";
    public enum Explanation { OwnDecision, ClaimedDanger }
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||Career.Enabled(w)||RetainerStart.Enabled(w)||CreatedCampaign.Enabled(w)||AssessmentNotice.Enabled(w);
    public static ContractWorld Create()
    {
        var w=IncomingMission.Create();w.WorldId=new("world:"+Ruleset);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);return w;
    }
    internal static bool KnownOrder(ContractWorld w,TaskState t,EntityId actor)=>
        t.Executor==actor&&w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==WorldSession.Child("message",t.Id,"order").Value);
    public static bool CanInterrupt(WorldSession session,EntityId task,EntityId actor)
    {
        var w=session.DiagnosticSnapshot();var t=w.Tasks.SingleOrDefault(t=>t.Id==task);
        return Enabled(w)&&t!=null&&t.Action==ActionKind.Inspect&&t.Kind==TaskKind.BindingOrder&&KnownOrder(w,t,actor)&&
            (t.Execution is ExecutionPhase.Waiting or ExecutionPhase.Running)&&w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult);
    }
    public static string Warning(WorldSession session,EntityId actor)=>session.GetActorView(actor).Tasks.Any(t=>CanInterrupt(session,t.Id,actor))?
        "既知任務との不一致：点検を中断すると未達になり得ます。成果・理由・責任は別審査です。":"中断できる既知の担当任務はありません。";
    public static string ExplanationText(Explanation reason)=>reason switch{
        Explanation.OwnDecision=>"自己判断で担当任務を中断したとの本人申告（正当性は未審査）",
        Explanation.ClaimedDanger=>"危険回避のため担当任務を中断したとの本人申告（危険の事実は未確認）",
        _=>throw new ArgumentOutOfRangeException(nameof(reason))};
    internal static EntityId ExplanationId(EntityId task)=>WorldSession.Child("message",task,"explanation");
    public sealed record Review(EntityId Task,string Achievement,string Difference,string Reason,string Liability,EntityId[] Evidence);
    public static Review[] View(WorldSession session,EntityId assessor)
    {
        var v=session.GetActorView(assessor);var w=session.DiagnosticSnapshot();
        return v.Tasks.Where(t=>t.Issuer==assessor&&w.Requests.Single(r=>w.Tasks.Single(x=>x.Id==t.Id).RequestId==r.Id).Intent.Action==ActionKind.Inspect).Select(t=>{
            var report=v.Messages.SingleOrDefault(m=>m.Id==WorldSession.Child("message",t.Id,"report")&&m.KnownBody!=null);
            var explanation=v.Messages.SingleOrDefault(m=>m.Id==ExplanationId(t.Id)&&m.KnownBody!=null);
            return FromClaims(t.Id,report,explanation);
        }).ToArray();
    }
    internal static Review FromClaims(EntityId task,VisibleMessage? report,VisibleMessage? explanation)
    {
            string achievement=report==null?"結果未確認":report.KnownBody=="任務完了"?"点検完了の申告を受領":"未達・中断等の申告を受領";
            string difference=report?.KnownBody switch{
                null=>"指示との差異は未確認",
                InterruptedReport=>"担当者による中断の申告あり（責任未確定）",
                "期限超過"=>"期限未達の申告あり（故意は未判定）",
                "任務完了"=>"差異の申告なし（違反なしの確定ではない）",
                _=>"原因・指示との差異は照会が必要"};
            return new Review(task,achievement,difference,explanation?.KnownBody??"本人の理由書状は未認識","責任・処分は未審査",new[]{report?.Id,explanation?.Id}.Where(id=>id!=null).Select(id=>id!.Value).ToArray());
    }

}
