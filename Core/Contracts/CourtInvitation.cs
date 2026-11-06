using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static class CourtInvitation
{
    static EntityId Broker=>CentralCourtCatalog.CourtBroker;
    static EntityId Kyoto=>CentralCourtCatalog.Kyoto;
    internal static bool Handles(CourtAction a)=>a is CourtAction.InviteHegemon or CourtAction.DeferInvitation or CourtAction.DeclineImperialInvitation;
    static string Letter(CourtInput i)=>HouseholdCourt.Id("imperial-invitation",i.Id.Value).Value;
    const string Body="関白・太政大臣・征夷大将軍のうち、望む職を朝廷にお知らせください。";
    public static IEnumerable<CourtInput> Offers(ContractWorld w){
        if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.RegentEdition || !HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.DeclareScale && e.Input.Scale==CourtScale.Hegemon))yield break;
        foreach(var house in HouseSuccession.Factions(w)){
            var owner=new EntityId(house.Id);var head=new EntityId(house.Head);
            if(HouseholdCourt.Scale(w,owner)!=CourtScale.Hegemon || HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.InviteHegemon && e.Input.Owner==owner && e.Input.Person==head) ||
               !SupremeOffice.ControlsKyoto(w,head,owner,w.ElapsedSeconds,w.CommitSequence))continue;
            yield return new(HouseholdCourt.Id("invite-hegemon",owner.Value+"/"+head.Value),CourtAction.InviteHegemon,head,owner,new(house.Home));
        }
    }
    static bool Closed(ContractWorld w,CourtEvent offer,long at,long rev)=>HouseholdCourt.Events(w).Any(e=>e.At<=at && e.Revision<rev && e.Input.Reference==offer.Input.Id &&
        (e.Input.Action==CourtAction.DeclineImperialInvitation || SupremeInvestiture.Petitions.Contains(e.Input.Action)));
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev){
        if(i.Action==CourtAction.InviteHegemon){
            var faction=HouseSuccession.Factions(w,at,rev-1).SingleOrDefault(f=>f.Id==i.Owner.Value);
            if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.RegentEdition || actor!=Broker || place!=Kyoto || i.Reference!=null || faction?.Home!=i.Site.Value || faction.Head!=i.Person.Value ||
                HouseholdCourt.Scale(w,i.Owner,at,rev)!=CourtScale.Hegemon || !SupremeOffice.ControlsKyoto(w,i.Person,i.Owner,at,rev-1) ||
                !HouseTerritoryScale.Measure(w,i.Owner,at,rev-1).Supports(CourtScale.Hegemon) ||
                !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev) ||
                HouseholdCourt.Events(w).Any(e=>e.Revision<rev && e.Input.Action==i.Action && e.Input.Owner==i.Owner && e.Input.Person==i.Person))
                throw new InvalidDataException("京を押さえた覇者の本拠に一度だけ朝廷から打診します");
            return;
        }
        var offer=HouseholdCourt.Events(w).SingleOrDefault(e=>e.Input.Action==CourtAction.InviteHegemon && e.Input.Id==i.Reference && e.At<=at && e.Revision<rev);
        if(offer==null || offer.Input.Owner!=i.Owner || offer.Input.Person!=actor || offer.Input.Site!=place || place!=i.Site || i.Person!=Broker ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev) || Closed(w,offer,at,rev) ||
            MessageFlow.LearnedAt(w.Messaging,Letter(offer.Input),actor.Value) is not {} opened || opened>at ||
            (i.Action==CourtAction.DeferInvitation && HouseholdCourt.Events(w).Any(e=>e.Revision<rev && e.Input.Action==i.Action && e.Input.Reference==i.Reference)))
            throw new InvalidDataException("本人が本拠で開いた未回答の三職推任を確認してください");
    }
    internal static void Send(ContractWorld w,CourtInput i){
        if(i.Action!=CourtAction.InviteHegemon)return;
        var m=new MessageEnvelope(Letter(i),i.Id.Value,"imperial-invitation",Broker.Value,i.Person.Value,Body,Kyoto.Value,w.ElapsedSeconds){DeliverySite=i.Site.Value};
        MessageFlow.Send(w.Messaging,m,w.ElapsedSeconds);w.SealedMessages=[..w.SealedMessages,new(m.Id)];RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,CourtEvent e){
        if(e.Input.Action!=CourtAction.InviteHegemon)return;
        var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==Letter(e.Input));
        if(m==null || m.Kind!="imperial-invitation" || m.CreatedAt!=e.At || m.Body!=Body || m.SenderId!=Broker.Value || m.RecipientId!=e.Input.Person.Value ||
            m.CorrelationId!=e.Input.Id.Value || m.DeliverySite!=e.Input.Site.Value || !w.SealedMessages.Any(s=>s.Value==m.Id))throw new InvalidDataException("三職推任の原本と勅使の書状が一致しません");
    }
    public static IEnumerable<(CourtEvent Offer,string Letter,bool Known)> Available(ContractWorld w,EntityId actor){
        foreach(var e in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.InviteHegemon && e.Input.Person==actor && !Closed(w,e,w.ElapsedSeconds,w.CommitSequence+1))){
            var id=Letter(e.Input);
            if(w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==actor.Value))yield return(e,id,MessageFlow.Knows(w.Messaging,id,actor.Value));
        }
    }
}
