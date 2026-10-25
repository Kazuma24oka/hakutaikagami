namespace HakutaiKagami.Core.Contracts;

// Contract reducer only. The world adapter must obtain presence and stock transaction
// proofs from the authoritative session; UI assertions are never authority.
public enum HandoverAction { Accept, Collect, Deliver, Report, Pay, Cancel, Return }
public enum HandoverPhase { Offered, Accepted, Carrying, Delivered, Reported, Paid, Cancelled, ReturnRequired, Returned }
public sealed record TransportTerms(EntityId Id,EntityId Issuer,EntityId Carrier,EntityId Recipient,
    EntityId Source,EntityId Destination,EntityId Owner,EntityId SourceLot,string Resource,string Unit,
    long Amount,long Fee,long OfferedAt,long AcceptBy,long DeliverBy);
public sealed record HandoverInput(EntityId Id,HandoverAction Action,EntityId Actor,EntityId Site,
    EntityId Counterparty,EntityId? Transaction,long At,long ExpectedRevision);
public sealed record HandoverEntry(HandoverInput Input,long Revision);
public sealed record TransportHandoverState(int Schema,string Contract,TransportTerms Terms,HandoverEntry[] Entries);
public sealed record HandoverView(HandoverPhase Phase,EntityId Holder,long FeeOwed,long Revision,long LastAt);
public sealed record HandoverDecision(TransportHandoverState State,bool Repeated);

public static class TransportHandover
{
    public const string Contract="local-transport-fixed-owner-custody-receipt-report-fee-v1";
    static void Need(bool value,string message){if(!value)throw new InvalidDataException(message);}
    public static TransportHandoverState Offer(TransportTerms t)
    {
        Need(new[]{t.Id,t.Issuer,t.Carrier,t.Recipient,t.Source,t.Destination,t.Owner,t.SourceLot}.All(x=>EntityId.IsValid(x.Value)),"Invalid transport identity");
        Need(t.Issuer!=t.Carrier&&t.Carrier!=t.Recipient&&t.Source!=t.Destination,"Transport parties and endpoints must be distinct");
        Need(t.Owner==t.Issuer,"Third-party goods require a separate authorization contract");
        Need(!string.IsNullOrWhiteSpace(t.Resource)&&!string.IsNullOrWhiteSpace(t.Unit)&&t.Resource.Length<=64&&t.Unit.Length<=32,"Missing goods unit");
        Need(t.Amount>0&&t.Fee>=0&&t.OfferedAt>=0&&t.AcceptBy>=t.OfferedAt&&t.DeliverBy>=t.AcceptBy,"Invalid quantity, fee or deadlines");
        return new(1,Contract,t,[]);
    }
    static HandoverView Start(TransportTerms t)=>new(HandoverPhase.Offered,t.Issuer,0,0,t.OfferedAt);
    static HandoverView Step(TransportTerms t,HandoverView v,HandoverInput i)
    {
        Need(EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action),"Invalid handover operation");
        Need(i.ExpectedRevision==v.Revision&&i.At>=v.LastAt,"Stale revision or reversed time");
        Need(EntityId.IsValid(i.Actor.Value)&&EntityId.IsValid(i.Site.Value)&&EntityId.IsValid(i.Counterparty.Value),"Invalid local receipt");
        bool physical=i.Action is HandoverAction.Collect or HandoverAction.Deliver or HandoverAction.Pay or HandoverAction.Return;
        Need(physical==(i.Transaction!=null)&&(!physical||EntityId.IsValid(i.Transaction!.Value.Value)),"A physical handover requires its exact stock/payment transaction");
        bool source=i.Site==t.Source&&i.Counterparty==t.Issuer;
        bool destination=i.Site==t.Destination&&i.Counterparty==t.Recipient;
        var phase=v.Phase;var holder=v.Holder;long debt=v.FeeOwed;
        switch(i.Action){
        case HandoverAction.Accept:
            Need(i.Actor==t.Carrier&&source&&phase==HandoverPhase.Offered&&i.At<=t.AcceptBy,"Cannot accept this offer");phase=HandoverPhase.Accepted;break;
        case HandoverAction.Collect:
            Need(i.Actor==t.Carrier&&source&&phase==HandoverPhase.Accepted&&i.At<=t.DeliverBy,"Cannot collect cargo");phase=HandoverPhase.Carrying;holder=t.Carrier;break;
        case HandoverAction.Deliver:
            Need(i.Actor==t.Carrier&&destination&&phase==HandoverPhase.Carrying&&i.At<=t.DeliverBy,"Cannot deliver cargo");phase=HandoverPhase.Delivered;holder=t.Recipient;break;
        case HandoverAction.Report:
            Need(i.Actor==t.Carrier&&source&&phase==HandoverPhase.Delivered,"Delivery and issuer recognition are separate");phase=HandoverPhase.Reported;debt=t.Fee;break;
        case HandoverAction.Pay:
            Need(i.Actor==t.Carrier&&source&&phase==HandoverPhase.Reported,"Cannot settle before recognized report");phase=HandoverPhase.Paid;debt=0;break;
        case HandoverAction.Cancel:
            Need(i.Actor==t.Issuer&&i.Site==t.Source&&i.Counterparty==t.Carrier&&phase is HandoverPhase.Offered or HandoverPhase.Accepted or HandoverPhase.Carrying,"Cancellation requires the issuer and actual local carrier");phase=phase==HandoverPhase.Carrying?HandoverPhase.ReturnRequired:HandoverPhase.Cancelled;break;
        case HandoverAction.Return:
            Need(i.Actor==t.Carrier&&source&&(phase==HandoverPhase.ReturnRequired||phase==HandoverPhase.Carrying&&i.At>t.DeliverBy),"Cannot return this cargo");phase=HandoverPhase.Returned;holder=t.Issuer;break;
        default:throw new InvalidDataException("Unknown transport action");
        }
        return new(phase,holder,debt,checked(v.Revision+1),i.At);
    }
    public static HandoverView Validate(TransportHandoverState s)
    {
        Need(s.Schema==1&&s.Contract==Contract&&s.Entries!=null,"Unknown transport contract");Offer(s.Terms);var v=Start(s.Terms);
        var ids=new HashSet<EntityId>();var transactions=new HashSet<EntityId>();
        foreach(var e in s.Entries!){Need(ids.Add(e.Input.Id),"Duplicate receipt operation");if(e.Input.Transaction is {} id)Need(transactions.Add(id),"Reused physical transaction");v=Step(s.Terms,v,e.Input);Need(e.Revision==v.Revision,"Receipt revision mismatch");}
        return v;
    }
    public static HandoverDecision Apply(TransportHandoverState s,HandoverInput i)
    {
        var v=Validate(s);var old=s.Entries.SingleOrDefault(e=>e.Input.Id==i.Id);
        if(old!=null){Need(old.Input==i,"Handover id payload conflict");return new(s,true);}
        Need(i.Transaction==null||s.Entries.All(e=>e.Input.Transaction!=i.Transaction),"Physical transaction already used");
        var next=Step(s.Terms,v,i);return new(s with{Entries=[..s.Entries,new(i,next.Revision)]},false);
    }
    public static byte[] Save(TransportHandoverState s){Validate(s);return ContractJson.Write(s);}
    public static TransportHandoverState Restore(byte[] bytes){var s=ContractJson.Read<TransportHandoverState>(bytes);Validate(s);return s;}
}
