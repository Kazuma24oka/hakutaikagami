namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    // Shared physical return rule for explicitly retained originals. A missed visit alone
    // creates no local paper: the original courier must finish the actual return leg.
    static UnitReplyHolding[] ReturnedOriginalPapers(ContractWorld w,Func<TroopDispatch,bool> retains)=>UnitDispatches(w).Where(retains).SelectMany(d=>{
        var receipt=UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id&&!r.Delivered);
        var returned=receipt==null?null:UnitReplies(w).SingleOrDefault(r=>r.Id==d.Id);
        if(receipt==null||returned==null)return Array.Empty<UnitReplyHolding>();
        var paper=new UnitReplyHolding(d.Message,d.Attempt,UnitCourier(d.Id),receipt.ReplyDestination,returned.At,returned.Revision);
        return returned.Received?[paper]:new[]{paper,new UnitReplyHolding(receipt.ReplyMessage,receipt.ReplyAttempt,UnitCourier(d.Id),receipt.ReplyDestination,returned.At,returned.Revision)};
    }).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
}
