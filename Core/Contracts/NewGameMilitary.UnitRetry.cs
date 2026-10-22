using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// Additional physical attempts preserve the original initial withdrawal journal byte-for-byte.
public sealed record UnitRetryState(string Contract,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies);
public static partial class NewGameMilitary
{
    public const string UnitRetryContract="same-original-known-failure-explicit-known-post-retry-v1";
    static bool UnitRetryAction(NewMilitaryAction a)=>a is NewMilitaryAction.RetryRecallKnown or NewMilitaryAction.RetryRecallHome or NewMilitaryAction.RetryCancelKnown or NewMilitaryAction.RetryCancelHome;
    public static TroopDispatch[] UnitDispatches(ContractWorld w)=>(w.NewMilitary?.Withdrawal?.Dispatches??[]).Concat(w.NewMilitary?.UnitRetry?.Dispatches??[]).Concat(w.NewMilitary?.UnitInquiry?.Dispatches??[]).Concat(w.NewMilitary?.Cancellation?.Dispatches??[]).OrderBy(d=>d.Revision).ToArray();
    public static TroopReceipt[] UnitReceipts(ContractWorld w)=>(w.NewMilitary?.Withdrawal?.Receipts??[]).Concat(w.NewMilitary?.UnitRetry?.Receipts??[]).Concat(w.NewMilitary?.UnitInquiry?.Receipts??[]).Concat(w.NewMilitary?.Cancellation?.Receipts??[]).OrderBy(d=>d.Revision).ThenBy(d=>d.Id.Value,StringComparer.Ordinal).ToArray();
    public static TroopReply[] UnitReplies(ContractWorld w)=>(w.NewMilitary?.Withdrawal?.Replies??[]).Concat(w.NewMilitary?.UnitRetry?.Replies??[]).Concat(w.NewMilitary?.UnitInquiry?.Replies??[]).Concat(w.NewMilitary?.Cancellation?.Replies??[]).OrderBy(d=>d.Revision).ThenBy(d=>d.Id.Value,StringComparer.Ordinal).ToArray();
    static TroopDispatch? RetryOriginal(ContractWorld w,NewMilitaryAction action,string? policy=null)
    {
        if(w.NewMilitary?.Withdrawal is not {} x)return null;
        var candidates=CancelRetryAction(action)?w.NewMilitary!.Cancellation?.Dispatches??[]:x.Dispatches;return candidates.LastOrDefault(d=>{
            var a=MessageFlow.Latest(w.Messaging,d.Message);
            return RetryWithinDeadline(w,d,action)&&KnownFailedAttempt(a,RetryUsesEvidence(w,d,policy))&&a.Number<MessageFlow.MaxAttempts&&(RetryUsesEvidence(w,d,policy)?!ReceiptConfirmedToSender(w,d.Message,w.PlayerControl.Value):!w.Messaging.Receipts.Any(r=>r.MessageId==d.Message))&&MessageFlow.FailureKnown(w.Messaging,a.Id,w.PlayerControl.Value);
        });
    }
    public static bool UnitRetryAllowed(ContractWorld w,NewMilitaryAction action,string? policy=null)=>UnitRetryAction(action)&&Detached(w)&&Deployed(w)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&RetryOriginal(w,action,policy)!=null;
    static void ApplyUnitRetry(ContractWorld w,NewMilitaryInput i)
    {
        if(!UnitRetryAction(i.Action))return;
        var original=RetryOriginal(w,i.Action,i.TransportPolicy)!;var from=PersonPosition(w,w.ElapsedSeconds);var sight=w.NewMilitary!.Withdrawal!.Sightings.Last();
        // Home is a declared known destination, never a lookup of the unit's actual position.
        var to=RetryHomeAction(i.Action)?Home(w,0):sight.Position;
        var evidence=RetryHomeAction(i.Action)?w.NewMilitary.Terms.Site:sight.Id;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(checked(w.ElapsedSeconds+1),route.LastOrDefault()?.EndAt??0);
        MessageFlow.Retry(w.Messaging,original.Message,w.PlayerControl.Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)),RetryUsesEvidence(w,original,i.TransportPolicy));
        var s=w.NewMilitary.UnitRetry??new UnitRetryState(UnitRetryContract,[],[],[]);
        w.NewMilitary=w.NewMilitary with{UnitRetry=s with{Dispatches=[..s.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,original.Recipient,original.Role,evidence,from,to,route,due,original.Message,MessageFlow.Latest(w.Messaging,original.Message).Id)]}};
    }
    static void RecordUnitReceipt(ContractWorld w,TroopReceipt row)
    {
        var s=w.NewMilitary!;
        if(s.Cancellation?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{Cancellation=s.Cancellation with{Receipts=[..s.Cancellation.Receipts,row]}};
        else if(s.UnitRetry?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{UnitRetry=s.UnitRetry with{Receipts=[..s.UnitRetry.Receipts,row]}};
        else if(s.UnitInquiry?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{UnitInquiry=s.UnitInquiry with{Receipts=[..s.UnitInquiry.Receipts,row]}};
        else w.NewMilitary=s with{Withdrawal=s.Withdrawal! with{Receipts=[..s.Withdrawal.Receipts,row]}};
    }
    static void RecordUnitReply(ContractWorld w,TroopReply row)
    {
        var s=w.NewMilitary!;
        if(s.Cancellation?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{Cancellation=s.Cancellation with{Replies=[..s.Cancellation.Replies,row]}};
        else if(s.UnitRetry?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{UnitRetry=s.UnitRetry with{Replies=[..s.UnitRetry.Replies,row]}};
        else if(s.UnitInquiry?.Dispatches.Any(d=>d.Id==row.Id)==true)w.NewMilitary=s with{UnitInquiry=s.UnitInquiry with{Replies=[..s.UnitInquiry.Replies,row]}};
        else w.NewMilitary=s with{Withdrawal=s.Withdrawal! with{Replies=[..s.Withdrawal.Replies,row]}};
    }
    static UnitRetryState? UnitRetryBefore(UnitRetryState? s,long at,long rev){if(s==null)return null;var dispatches=s.Dispatches.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray();return dispatches.Length==0?null:new(s.Contract,dispatches,s.Receipts.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray(),s.Replies.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray());}
    static void ValidateUnitRetry(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.UnitRetry;
        Need((x!=null)==s.Requests.Any(q=>UnitRetryAction(q.Input.Action)),"部隊宛の追加便と原判断が一致しません");if(x==null)return;
        Need(x.Contract==UnitRetryContract&&x.Dispatches.Length==s.Requests.Count(q=>UnitRetryAction(q.Input.Action)),"部隊宛の再配送原判断が欠落しています");
        Need(x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds),"部隊宛の追加便の受令/返書が不足しています");
        Need(x.Receipts.Select(r=>r.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(r=>r.Id).Distinct().Count()==x.Replies.Length,"追加便の受令/返書が重複しています");
    }
}
