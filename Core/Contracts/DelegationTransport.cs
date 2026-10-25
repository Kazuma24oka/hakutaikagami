using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// Transport adapter only: immutable business messages remain owned by their original issuer.
public static class DelegationTransport
{
    internal static RegionalDelivery ReturnDelivery(ContractWorld w,string message)
    {
        var delivered=w.Messaging.Attempts.Where(a=>a.MessageId==message&&a.Status==AttemptStatus.Delivered).OrderBy(a=>a.FinishedAt).ThenBy(a=>a.Number).FirstOrDefault();
        return w.Region!.Deliveries.Single(d=>d.Attempt==(delivered??MessageFlow.Latest(w.Messaging,message)).Id);
    }
    internal static void Retry(ContractWorld w,string message,string failed,EntityId actor,FieldPoint address)
    {
        if(!DelegationMission.Enabled(w)||actor!=w.PlayerControl)throw new InvalidDataException("Only the player may resend their own delegation order");
        var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==message)??throw new InvalidDataException("Unknown issued message");
        if(m.SenderId!=actor.Value||m.Kind!="region-order"||!w.Region!.Commands.Any(c=>c.Message==message&&DelegationMission.Remote(w,c.Request)))throw new InvalidDataException("Not a personally issued delegated order");
        var previous=MessageFlow.Latest(w.Messaging,message);
        if(previous.Id!=failed||previous.LateDueAt!=null)throw new InvalidDataException("Latest failed physical attempt required");
        var unit=w.Region.Units.First(u=>u.Commander==new EntityId(m.RecipientId));
        var known=RegionalBattle.View(w,actor).Where(v=>v.Unit==unit.Id).OrderByDescending(v=>v.ObservedAt).FirstOrDefault()?.Position??DelegationMission.InitialAddress(w,actor,new(m.RecipientId));
        if(known!=address)throw new InvalidDataException("Recipient address must come from personal knowledge");
        var origin=RegionalBattle.Position(w,actor);var path=RegionalGround.Plan(origin,address,w.ElapsedSeconds,courier:true,scenery:true,atlas:true);
        long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??w.ElapsedSeconds+1);
        MessageFlow.Retry(w.Messaging,message,actor.Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        w.Region=w.Region with{Deliveries=[..w.Region.Deliveries,new(MessageFlow.Latest(w.Messaging,message).Id,origin,address,path,due)]};
    }
}
