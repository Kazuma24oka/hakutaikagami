using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public enum TimeNoticeKind { ImportantLetter, Deadline, Arrival, EnemyApproach, Battle, Encounter, Council, Death, HistoricalEnd }
public enum TimeResponse { Off, Slow, Pause }
public sealed record TimeRule(TimeNoticeKind Kind,TimeResponse Response);
public sealed record TimeNotice(TimeNoticeKind Kind,string Source,long At,TimeResponse Response);
public sealed record TimeSummary(int CompletedOwnTasks,int KnownRecords);
public sealed record WorldTimeState(string Policy,TimeRule[] Rules,TimeNotice[] Notices,int? Pending,long? SummaryAt,long? ContinuedAt,TimeSummary? Summary);

public static class WorldTimeControl
{
    public const string Policy="own-notice-calendar-v1";
    public static readonly int[] Speeds=[1,10,60,360,43200,86400,259200];
    public static readonly string[] SpeedLabels=["1倍","10倍","60倍","360倍（旧設定）","半日/秒","1日/秒","3日/秒"];
    public static WorldTimeState Initial()=>new(Policy,Enum.GetValues<TimeNoticeKind>().Select(k=>new TimeRule(k,k is TimeNoticeKind.Arrival or TimeNoticeKind.EnemyApproach?TimeResponse.Slow:TimeResponse.Pause)).ToArray(),[],null,null,null,null);
    public static bool Enabled(ContractWorld w)=>w.TimeControl!=null;
    public static TimeNotice? Pending(ContractWorld w)=>w.TimeControl?.Pending is {} n?w.TimeControl.Notices[n]:null;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    internal static IEnumerable<(string Source,long At)> Deadlines(ContractWorld w)
    {
        foreach(var t in w.Tasks){
            bool own=t.Issuer==w.PlayerControl;
            bool received=t.Executor==w.PlayerControl&&MessageFlow.Knows(w.Messaging,WorldSession.Child("message",t.Id,"order").Value,w.PlayerControl.Value);
            if(!own&&!received)continue;
            bool terminal=t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed;
            if(terminal&&(t.Executor==w.PlayerControl||own&&t.Report==ReportingPhase.KnownByRecipient))continue;
            // 未着の遠方の完了を参照して本人の期限表示を変えない。
            var i=w.Requests.Single(r=>r.Id==t.RequestId).Intent;
            long at=own?(i.Deadline.FromOpening?i.Deadline.AbsoluteLatestAt:i.Deadline.At??i.Deadline.AbsoluteLatestAt):t.DeadlineAt??t.AbsoluteLatestAt;
            if(at>0)yield return(t.Id.Value+"/"+at,at-1);
        }
    }
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;
        var times=Deadlines(w).Select(d=>d.At).ToList();
        if(w.TimeControl!.SummaryAt==null)times.Add(WorldCalendar.HistoricalEndSeconds(w.EpochDate));
        return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    static void Add(ContractWorld w,TimeNoticeKind kind,string source)
    {
        var state=w.TimeControl!;
        if(state.Notices.Any(n=>n.Kind==kind&&n.Source==source))return;
        var response=state.Rules.Single(r=>r.Kind==kind).Response;
        if(kind==TimeNoticeKind.HistoricalEnd)response=TimeResponse.Pause;
        var notice=new TimeNotice(kind,source,w.ElapsedSeconds,response);
        int? pending=state.Pending;
        if(response!=TimeResponse.Off){
            if(pending==null||response>state.Notices[pending.Value].Response)pending=state.Notices.Length;

        }
        w.TimeControl=state with{Notices=[..state.Notices,notice],Pending=pending};
    }
    internal static void Observe(ContractWorld prior,ContractWorld w)
    {
        if(!Enabled(w))return;
        foreach(var a in w.Messaging.Attempts.Where(a=>a.Status==AttemptStatus.Delivered&&a.FinishedAt==w.ElapsedSeconds&&a.ReceiverId==w.PlayerControl.Value&&w.SealedMessages.Any(m=>m.Value==a.MessageId)).OrderBy(a=>a.MessageId,StringComparer.Ordinal))Add(w,TimeNoticeKind.ImportantLetter,a.MessageId);
        foreach(var d in Deadlines(w).Where(d=>d.At==w.ElapsedSeconds))Add(w,TimeNoticeKind.Deadline,d.Source);
        foreach(var t in w.Tasks.Where(t=>t.Executor==w.PlayerControl&&t.Action==ActionKind.Move&&t.Execution==ExecutionPhase.Completed&&t.FinishedAt==w.ElapsedSeconds))Add(w,TimeNoticeKind.Arrival,t.Id.Value);
        // 評定・訃報等は、将来の発生源も同じ物理書状の認識境界に接続する。
        foreach(var k in w.Messaging.Knowledge.Where(k=>k.CharacterId==w.PlayerControl.Value&&k.LearnedAt==w.ElapsedSeconds)){
            var m=w.Messaging.Messages.Single(m=>m.Id==k.MessageId);
            TimeNoticeKind? kind=m.Kind switch{"world-council"=>TimeNoticeKind.Council,"world-death"=>TimeNoticeKind.Death,"world-encounter"=>TimeNoticeKind.Encounter,_=>null};
            if(kind is {} value)Add(w,value,m.Id);
        }
        if(NpcArmies.Enabled(w)&&w.NpcArmies!.Musters.Length>0){
            var before=NpcArmies.Sights(prior,prior.PlayerControl);
            var own=w.Memberships.Where(m=>m.Subject==w.PlayerControl&&m.From<=w.ElapsedSeconds&&m.Until>w.ElapsedSeconds).Select(m=>m.Target).ToHashSet();
            var known=FactionStances.Known(w,w.PlayerControl);
            foreach(var sight in NpcArmies.Sights(w,w.PlayerControl)){
                bool hostile=known.Any(k=>k.EffectiveFrom<=w.ElapsedSeconds&&k.Until>w.ElapsedSeconds&&(own.Contains(k.FromOwner)&&k.ToOwner==sight.Owner||own.Contains(k.ToOwner)&&k.FromOwner==sight.Owner));
                if(hostile&&!before.Any(s=>s.Army==sight.Army))Add(w,TimeNoticeKind.EnemyApproach,sight.Army.Value+"/"+w.ElapsedSeconds);
                if(sight.Attacking&&!before.Any(s=>s.Army==sight.Army&&s.Attacking))Add(w,TimeNoticeKind.Battle,sight.Army.Value+"/"+w.ElapsedSeconds);
            }
        }
        if(w.TimeControl!.SummaryAt==null&&w.ElapsedSeconds>=WorldCalendar.HistoricalEndSeconds(w.EpochDate)){
            w.TimeControl=w.TimeControl with{SummaryAt=w.ElapsedSeconds,Summary=new(w.Tasks.Count(t=>t.Executor==w.PlayerControl&&t.Execution==ExecutionPhase.Completed),w.Knowledge.Count(k=>k.Character==w.PlayerControl))};Add(w,TimeNoticeKind.HistoricalEnd,"calendar:1650");
        }
    }
    public static void Validate(ContractWorld w,bool intermediate=false)
    {
        if(NewGameWorld.Enabled(w))Need((w.NewGame!.TimePolicy==Policy)==(w.TimeControl!=null)&&w.NewGame.TimePolicy is null or Policy,"開始方針と時間設定が一致しません");
        if(w.TimeControl is not {} s)return;
        Need(s.Policy==Policy&&s.Rules!=null&&s.Notices!=null,"時間設定の版が不正です");
        Need(s.Rules!.Length==Enum.GetValues<TimeNoticeKind>().Length&&s.Rules.Select(r=>r.Kind).Distinct().Count()==s.Rules.Length&&s.Rules.All(r=>Enum.IsDefined(r.Kind)&&Enum.IsDefined(r.Response)),"時間設定が欠落しています");
        Need(s.Notices!.All(n=>Enum.IsDefined(n.Kind)&&Enum.IsDefined(n.Response)&&!string.IsNullOrWhiteSpace(n.Source)&&n.At>=0&&n.At<=w.ElapsedSeconds)&&s.Notices.Select(n=>(n.Kind,n.Source)).Distinct().Count()==s.Notices.Length,"通知記録が不正です");
        Need(s.Notices.Zip(s.Notices.Skip(1)).All(p=>p.First.At<=p.Second.At),"通知の時刻が逆転しています");
        Need(s.Pending==null||s.Pending>=0&&s.Pending<s.Notices.Length&&s.Notices[s.Pending.Value].Response!=TimeResponse.Off&&(w.Paused||intermediate),"停止通知が不正です");
        Need(s.SummaryAt==null?s.ContinuedAt==null: s.SummaryAt>=WorldCalendar.HistoricalEndSeconds(w.EpochDate)&&s.SummaryAt<=w.ElapsedSeconds,"総括の時刻が不正です");
        Need((s.SummaryAt==null)==(s.Summary==null)&& (s.Summary==null||s.Summary.CompletedOwnTasks>=0&&s.Summary.KnownRecords>=0),"総括の記録が不正です");
        Need(s.ContinuedAt==null||s.ContinuedAt>=s.SummaryAt&&s.ContinuedAt<=w.ElapsedSeconds,"継続の時刻が不正です");
    }
}

public sealed partial class WorldSession
{
    public CommandResult ConfigureTime(TimeRule[] rules,long revision)
    {
        lock(gate){var id=new EntityId("request:time-settings");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");if(!WorldTimeControl.Enabled(state))return new(ResultCode.NotAvailable,id,state.CommitSequence,"旧保存には新しい時間規則を適用しません");
            try{Change(w=>w.TimeControl=w.TimeControl! with{Rules=rules.ToArray()});return new(ResultCode.Accepted,id,state.CommitSequence,"時間の停止・減速設定を保存しました");}catch(InvalidDataException){return new(ResultCode.InvalidInput,id,state.CommitSequence,"時間設定を確認してください");}}
    }
    public CommandResult ContinueAfterHistory(long revision)
    {
        lock(gate){var id=new EntityId("request:continue-history");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");if(state.TimeControl is not{SummaryAt:not null,ContinuedAt:null})return new(ResultCode.NotAvailable,id,state.CommitSequence,"継続を選ぶ総括はありません");Change(w=>w.TimeControl=w.TimeControl! with{ContinuedAt=w.ElapsedSeconds,Pending=null});return new(ResultCode.Accepted,id,state.CommitSequence,"史実制御を外して継続します");}
    }
}
