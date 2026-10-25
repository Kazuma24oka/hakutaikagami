using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitDirectiveCancelRetryInput(string Contract,EntityId Cancellation,KnownMilitaryTarget Known);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveCancelRetryContract="explicit-cancellation-original-known-failure-fixed-copy-deadline-v1";
    static bool DirectiveCancelRetryAction(NewMilitaryAction action)=>action==NewMilitaryAction.RetryUnitDirectiveCancellation;
    static TroopDispatch? DirectiveCancellationOriginal(ContractWorld w,EntityId id)=>w.NewMilitary?.UnitDirective?.Cancellation?.Dispatches.SingleOrDefault(d=>d.Id==id&&w.NewMilitary.Requests.Any(q=>q.Input.Id==id&&DirectiveCancellationAction(q.Input.Action)));
    static TroopDispatch[] DirectiveCancellationOriginals(ContractWorld w)=>w.NewMilitary?.UnitDirective?.Cancellation?.Dispatches.Where(d=>DirectiveCancellationOriginal(w,d.Id)!=null).ToArray()??[];
    public static EntityId[] CancellationOriginalsFor(ContractWorld w,EntityId directive)=>DirectiveCancellationOriginals(w).Where(d=>DirectiveCancelNote(w,d).Original==directive&&SameMilitaryCase(w,d)).Select(d=>d.Id).ToArray();
    public static bool UnitDirectiveCancelRetryAllowed(ContractWorld w,EntityId cancellation)
    {
        if(!UnitDirectiveAllowed(w)||DirectiveCancellationOriginal(w,cancellation) is not {} root||!SameMilitaryCase(w,root))return false;
        var known=SelectKnownTarget(w,root.Recipient);if(known==null)return false;var a=MessageFlow.Latest(w.Messaging,root.Message);var m=w.Messaging.Messages.Single(m=>m.Id==root.Message);
        if(!KnownFailedAttempt(a,true)||a.Number>=MessageFlow.MaxAttempts||!MessageFlow.FailureKnown(w.Messaging,a.Id,w.PlayerControl.Value)||ReceiptConfirmedToSender(w,root.Message,w.PlayerControl.Value))return false;
        if(m.DeliverBy==null)return true;if(m.DeliverBy<w.ElapsedSeconds+1)return false;
        try{var route=RegionalGround.Plan(PersonPosition(w,w.ElapsedSeconds),known.Position,w.ElapsedSeconds,courier:true,atlas:true);return Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0)<=m.DeliverBy;}catch(InvalidDataException){return false;}
    }
    static void ValidateDirectiveCancelRetryInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveCancelRetryAction(i.Action)==(i.DirectiveCancelRetry!=null),"分隊取消原本の再送操作と参照が一致しません");if(i.DirectiveCancelRetry is not {} p)return;
        Need(p.Contract==UnitDirectiveCancelRetryContract&&EntityId.IsValid(p.Cancellation.Value)&&p.Known!=null,"分隊取消原本の版・参照が不正です");var root=DirectiveCancellationOriginal(w,p.Cancellation);
        Need(root!=null&&root.Recipient==p.Known.Unit&&SelectKnownTarget(w,p.Known.Unit)==p.Known,"取消原本と同じ隊の確認済み固定宛先を指定してください");
        Need(i.Amount==0&&i.A==null&&i.B==null&&i.Directive==null&&i.DirectiveRetry==null&&i.DirectiveHandoff==null&&i.DirectiveCancellation==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.TargetOriginal==null&&i.RequestedEffectiveAt==null&&i.DeliverBy==null&&i.OriginalPolicy==null&&i.PostPolicy==null&&i.TransportPolicy==UnitSenderEvidenceContract,"再送で取消対象・効力時刻・期限・方針を変更できません");
    }
    static void ApplyDirectiveCancelRetry(ContractWorld w,NewMilitaryInput i)
    {
        if(!DirectiveCancelRetryAction(i.Action))return;var p=i.DirectiveCancelRetry!;var root=DirectiveCancellationOriginal(w,p.Cancellation)!;var from=PersonPosition(w,w.ElapsedSeconds);var to=p.Known.Position;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);
        MessageFlow.Retry(w.Messaging,root.Message,w.PlayerControl.Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)),true);
        var state=w.NewMilitary!.UnitDirective!;var x=state.Cancellation!;w.NewMilitary=w.NewMilitary with{UnitDirective=state with{Cancellation=x with{Dispatches=[..x.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,root.Recipient,root.Role,new("evidence:directive-cancel-retry/"+Hash(Text(p))),from,to,route,due,root.Message,MessageFlow.Latest(w.Messaging,root.Message).Id)]}}};
    }
    // Future effect notices return to the actual first accepted copy's sender post.
    // Handoff-origin support is deliberately retained for the following physical-paper slice.
    static FieldPoint DirectiveCancellationReturnOrigin(ContractWorld w,TroopDispatch root)
    {
        var receipt=w.Messaging.Receipts.Single(r=>r.MessageId==root.Message);
        var a=w.Messaging.Attempts.Where(a=>a.MessageId==root.Message&&a.Status==AttemptStatus.Delivered&&a.FinishedAt==receipt.ReceivedAt).OrderBy(a=>a.Id,StringComparer.Ordinal).First();
        if(a.ArrivedLate)return w.NewMilitary!.HeldDelivery!.Transfers.Single(t=>t.Attempt==a.Id).Origin;
        return w.NewMilitary!.UnitDirective!.Cancellation!.Dispatches.Single(d=>d.Attempt==a.Id).Origin;
    }
}
