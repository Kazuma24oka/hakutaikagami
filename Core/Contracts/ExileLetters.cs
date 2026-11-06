using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static class ExileLetters
{
    public const string Kind="shogun-exile-claim";
    static CourtInput Claim(CourtEvent order)=>new(HouseholdCourt.Id("exile-claim",order.Input.Id.Value),CourtAction.SendExileClaim,
        order.Actor,order.Input.Owner,order.Input.Site,Reference:order.Input.Id);
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        var order=ShogunExile.Orders(w).SingleOrDefault(e=>e.Input.Id==i.Reference && e.Revision<rev);
        if(order==null || order.Input.Person!=actor || Claim(order)!=i || place!=i.Site ||
            Assassination.DeadAt(w,actor,at,rev) || !TransportWork.At(w,actor,place,at,rev,true) ||
            !ShogunExile.Departed(w,actor,at,rev-1) || HouseholdCourt.Events(w).Any(e=>e.Input.Id==i.Id && e.Revision<rev))
            throw new InvalidDataException("追放先に着いた本人が追放を命じた相手に一度だけ申し立てます");
    }
    public static IEnumerable<(CourtInput Letter,EntityId Author)> Pending(ContractWorld w,bool player)
    {
        foreach(var order in ShogunExile.Orders(w).Where(e=>(e.Input.Person==w.PlayerControl)==player)){
            var letter=Claim(order);
            try{Check(w,letter,order.Input.Person,order.Input.Site,w.ElapsedSeconds,w.CommitSequence+1);}
            catch(InvalidDataException){continue;}
            yield return (letter,order.Input.Person);
        }
    }
    static MessageEnvelope Envelope(CourtInput i,EntityId actor,long at)=>new(HouseholdCourt.Id("exile-letter",i.Id.Value).Value,i.Id.Value,Kind,
        actor.Value,i.Person.Value,"京を離れた今も将軍の正統は我にある。追放の撤回を求める。",i.Site.Value,at){DeliverySite=CentralCourtCatalog.Kyoto.Value};
    internal static void Send(ContractWorld w,CourtInput i,EntityId actor){
        var letter=Envelope(i,actor,w.ElapsedSeconds);
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);w.SealedMessages=[..w.SealedMessages,new(letter.Id)];RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,CourtEvent e){
        var expected=Envelope(e.Input,e.Actor,e.At);
        if(!w.Messaging.Messages.Contains(expected)||!w.SealedMessages.Any(x=>x.Value==expected.Id))
            throw new InvalidDataException("追放先の御内書と原本が一致しません");
    }
}