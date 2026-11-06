using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class SupremeInvestiture
{
    static EntityId Kyoto=>CentralCourtCatalog.Kyoto;
    static EntityId Broker=>CentralCourtCatalog.CourtBroker;
    public static readonly CourtAction[] Petitions=[CourtAction.PetitionChancellor,CourtAction.PetitionRegent,CourtAction.PetitionShogun];
    public static string Title(CourtAction petition)=>petition switch {
        CourtAction.PetitionChancellor=>"太政大臣",CourtAction.PetitionRegent=>"関白",CourtAction.PetitionShogun=>"征夷大将軍",
        _=>throw new ArgumentOutOfRangeException(nameof(petition))};
    static bool Decision(CourtAction a)=>a is CourtAction.GrantHighOffice or CourtAction.RefuseHighOffice;
    internal static bool Handles(CourtAction a)=>Petitions.Contains(a)||Decision(a);
    internal static CourtEvent? LastGrant(ContractWorld w,CourtAction title,long at,long rev)
    {
        var events=HouseholdCourt.Events(w);
        return events.LastOrDefault(g=>g.Input.Action==CourtAction.GrantHighOffice && g.At<=at && g.Revision<=rev &&
            events.Any(p=>p.Input.Id==g.Input.Reference && p.Input.Action==title));
    }
    static bool Eligible(ContractWorld w,CourtAction title,EntityId applicant,EntityId house,long at,long rev)
    {
        if(HouseSuccession.Head(w,house,at,rev)!=applicant || Assassination.DeadAt(w,applicant,at,rev))return false;
        if(title==CourtAction.PetitionShogun)return SupremeOffice.ShogunAt(w,at,rev)==null && CourtPedigree.Qualified(w,applicant,at,rev);
        var holder=LastGrant(w,title,at,rev)?.Input.Person;
        if(title==CourtAction.PetitionRegent){
            holder??=CentralCourtCatalog.Konoe;
            return holder!=applicant && (applicant==CentralCourtCatalog.Konoe || RegentKin.Qualified(w,applicant,at,rev));
        }
        return holder==null || Assassination.DeadAt(w,holder.Value,at,rev);
    }
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.RegentEdition || place!=Kyoto || i.Site!=Kyoto ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev))
            throw new InvalidDataException("京の朝廷取次で手続きしてください");
        var events=HouseholdCourt.Events(w).Where(e=>e.Revision<rev && e.At<=at).ToArray();
        if(Petitions.Contains(i.Action)){
            if(i.Person!=Broker || actor==Broker || i.Reference!=null || !TransportWork.At(w,Broker,place,at,rev,true) ||
                Assassination.DeadAt(w,Broker,at,rev) || !Eligible(w,i.Action,actor,i.Owner,at,rev-1) ||
                events.Any(p=>Petitions.Contains(p.Input.Action) && p.Actor==actor && !events.Any(d=>Decision(d.Input.Action)&&d.Input.Reference==p.Input.Id)))
                throw new InvalidDataException("本人・取次・官職の資格・未裁可の奏請を確認してください");
            return;
        }
        var petition=events.SingleOrDefault(p=>p.Input.Id==i.Reference && Petitions.Contains(p.Input.Action));
        if(actor!=Broker || petition==null || petition.At>=at || petition.Actor!=i.Person || petition.Input.Owner!=i.Owner ||
            events.Any(d=>Decision(d.Input.Action)&&d.Input.Reference==i.Reference) ||
            (i.Action==CourtAction.GrantHighOffice && !Eligible(w,petition.Input.Action,i.Person,i.Owner,at,rev-1)))
            throw new InvalidDataException("同じ奏請について裁可時の資格を再確認してください");
    }
    public static IEnumerable<CourtInput> Responses(ContractWorld w){
        foreach(var p in HouseholdCourt.Events(w).Where(e=>Petitions.Contains(e.Input.Action) && e.At<w.ElapsedSeconds &&
            !HouseholdCourt.Events(w).Any(d=>Decision(d.Input.Action)&&d.Input.Reference==e.Input.Id))){
            var eligible=Eligible(w,p.Input.Action,p.Actor,p.Input.Owner,w.ElapsedSeconds,w.CommitSequence);
            foreach(var action in new[]{CourtAction.GrantHighOffice,CourtAction.RefuseHighOffice}){
                if(action==CourtAction.GrantHighOffice && !eligible || action==CourtAction.RefuseHighOffice && eligible && Broker!=w.PlayerControl)continue;
                yield return new(HouseholdCourt.Id(action.ToString(),p.Input.Id.Value),action,p.Actor,p.Input.Owner,Kyoto,Reference:p.Input.Id);
            }
        }
    }
    static string Letter(CourtInput i)=>HouseholdCourt.Id("investiture-letter",i.Id.Value).Value;
    static string Body(ContractWorld w,CourtInput i)=>Title(HouseholdCourt.Events(w).Single(e=>e.Input.Id==i.Reference).Input.Action)+
        (i.Action==CourtAction.GrantHighOffice?"への任官を裁可する。":"への奏請を退ける。");
    internal static void Send(ContractWorld w,CourtInput i){
        if(!Decision(i.Action))return;
        var letter=new MessageEnvelope(Letter(i),i.Id.Value,"court-investiture",Broker.Value,i.Person.Value,Body(w,i),Kyoto.Value,w.ElapsedSeconds){DeliverySite=Kyoto.Value};
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);
        w.SealedMessages=[..w.SealedMessages,new(letter.Id)];RegionalCampaign.Bind(w);
    }
    internal static void ValidateLetter(ContractWorld w,CourtEvent e){
        if(!Decision(e.Input.Action))return;
        var id=Letter(e.Input);var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==id);
        if(m==null || m.Kind!="court-investiture" || m.Body!=Body(w,e.Input) || m.SenderId!=e.Actor.Value || m.RecipientId!=e.Input.Person.Value ||
           m.CreatedAt!=e.At || m.CorrelationId!=e.Input.Id.Value || m.DeliverySite!=Kyoto.Value || !w.SealedMessages.Any(x=>x.Value==id))
            throw new InvalidDataException("任官の裁可原本と通知が一致しません");
    }
    public static IEnumerable<(string Id,string? Known)> Notices(ContractWorld w,EntityId reader){
        foreach(var e in HouseholdCourt.Events(w).Where(e=>Decision(e.Input.Action)&&e.Input.Person==reader)){
            var id=Letter(e.Input);
            if(w.Messaging.Receipts.Any(r=>r.MessageId==id&&r.ReceiverId==reader.Value))
                yield return(id,MessageFlow.Knows(w.Messaging,id,reader.Value)?Body(w,e.Input):null);
        }
    }
    public static bool CanPetition(ContractWorld w,CourtInput i,EntityId actor){
        try{Check(w,i,actor,Kyoto,w.ElapsedSeconds,w.CommitSequence+1);return true;}catch(InvalidDataException){return false;}
    }
}
