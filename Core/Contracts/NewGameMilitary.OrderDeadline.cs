using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    public const long OrderDeliveryWindowSeconds=600;
    static bool RetryWithinDeadline(ContractWorld w,TroopDispatch d,NewMilitaryAction action)
    {
        var m=w.Messaging.Messages.Single(m=>m.Id==d.Message);if(m.DeliverBy==null)return true;
        if(m.DeliverBy<w.ElapsedSeconds+1)return false;
        var destination=RetryHomeAction(action)?Home(w,0):w.NewMilitary!.Withdrawal!.Sightings.Last().Position;
        var route=RegionalGround.Plan(PersonPosition(w,w.ElapsedSeconds),destination,w.ElapsedSeconds,courier:true,atlas:true);
        return Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0)<=m.DeliverBy;
    }
    static bool HeldWithinDeadline(ContractWorld w,MessageEnvelope m,UnitReplyHolding h,NewMilitaryAction action)
    {
        if(m.DeliverBy==null)return true;if(m.DeliverBy<w.ElapsedSeconds+1)return false;
        var to=action==NewMilitaryAction.TakeHeldReply?PhysicalPerson(w,w.ElapsedSeconds):Home(w,0);
        var route=RegionalGround.Plan(h.Position,to,w.ElapsedSeconds,courier:true,atlas:true);
        return Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0)<=m.DeliverBy;
    }
    static void ValidateUnitInitialStatuses(ContractWorld w)
    {
        foreach(var r in UnitReceipts(w)){var matches=UnitDispatches(w).Where(d=>d.Id==r.Id).ToArray();Need(matches.Length==1,"初回到達の発送原本が不明です");var d=matches[0];var messages=w.Messaging.Messages.Where(m=>m.Id==d.Message).ToArray();Need(messages.Length==1,"初回到達の書状が不明です");var m=messages[0];
            Need((m.DeliverBy!=null)==(r.ArrivalStatus!=null),"到達期限付き原本の初回判定がありません");
            if(r.ArrivalStatus is {} status)Need((status is AttemptStatus.Delivered or AttemptStatus.Lost or AttemptStatus.Rejected)&&r.Delivered==(status==AttemptStatus.Delivered),"初回到達判定と正式受領が違います");
        }
    }
}
