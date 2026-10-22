using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitCommandHoldingState(string Contract,UnitReplyHolding[] Holdings,UnitHeldOrderOutcome[] Outcomes,UnitHeldOrderAck[] Acks);
public static partial class NewGameMilitary
{
    public const string UnitCommandHoldingContract="declared-order-retained-original-physical-late-effect-and-reply-v1";
    static bool HoldsOriginal(ContractWorld w,TroopDispatch d)=>d.Role==ReceivingRole&&ContractJson.Read<UnitPostOrder>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body)).HoldingPolicy==UnitCommandHoldingContract;
    static void EnableCommandHolding(ContractWorld w,NewMilitaryInput input){if(input.OriginalPolicy==UnitCommandHoldingContract&&w.NewMilitary!.CommandHolding==null)w.NewMilitary=w.NewMilitary with{CommandHolding=new(UnitCommandHoldingContract,[],[],[])};}
    static UnitReplyHolding[] ExpectedCommandHoldings(ContractWorld w)=>UnitDispatches(w).Where(d=>HoldsOriginal(w,d)).SelectMany(d=>{
        var r=UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id&&!r.Delivered);var seen=r==null?null:UnitReplies(w).SingleOrDefault(a=>a.Id==d.Id);
        if(r==null||seen==null)return Array.Empty<UnitReplyHolding>();
        var original=new UnitReplyHolding(d.Message,d.Attempt,UnitCourier(d.Id),r.ReplyDestination,seen.At,seen.Revision);
        return seen.Received?[original]:new[]{original,new UnitReplyHolding(r.ReplyMessage,r.ReplyAttempt,UnitCourier(d.Id),r.ReplyDestination,seen.At,seen.Revision)};
    }).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
    static void UpdateCommandHolding(ContractWorld w){if(w.NewMilitary?.CommandHolding==null)return;UpdateHeldOrderAcks(w);var x=w.NewMilitary.CommandHolding;w.NewMilitary=w.NewMilitary with{CommandHolding=x with{Holdings=ExpectedCommandHoldings(w).Concat(HeldOrderAckPapers(w)).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray()}};}
    static UnitCommandHoldingState? CommandHoldingBefore(NewMilitaryState s,long at,long rev)=>s.CommandHolding is not {} x||!s.Requests.Any(q=>q.Input.OriginalPolicy==UnitCommandHoldingContract&&Before(q.At,q.Revision,at,rev))?null:x with{Holdings=x.Holdings.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray(),Outcomes=x.Outcomes.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray(),Acks=x.Acks.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray()};
    static void ValidateCommandHolding(ContractWorld w){var s=w.NewMilitary!;var x=s.CommandHolding;Need((x!=null)==s.Requests.Any(q=>q.Input.OriginalPolicy==UnitCommandHoldingContract),"原命令の保持方針と台帳が違います");if(x==null)return;Need(x.Contract==UnitCommandHoldingContract&&x.Holdings!=null&&x.Outcomes!=null&&x.Acks!=null&&Same(x.Holdings,ExpectedCommandHoldings(w).Concat(HeldOrderAckPapers(w)).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray()),"実際に持ち帰った命令原本/未受領報告が欠落・改ざんされています");}
}
