using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static class PeaceMediation
{
    public const string Proposal="mediated-peace-proposal";
    public const string Response="mediated-peace-response";
    static bool IsVote(CourtAction a)=>a is CourtAction.AcceptPeaceProposal or CourtAction.RejectPeaceProposal;
    internal static bool Handles(CourtAction a)=>IsVote(a) || a is CourtAction.AskPeaceMediation or CourtAction.SendPeaceProposal or CourtAction.DeclinePeaceMediation;
    static bool Broker(EntityId id)=>id==CentralCourtCatalog.CourtBroker || id==CentralCourtCatalog.ShogunBroker;
    static bool Supports(ContractWorld w,EntityId broker,EntityId applicant,long at,long rev)=>SocialWorld.ConsentBonus(w,broker,applicant,at,rev)>=2;
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(IsVote(i.Action)){CheckVote(w,i,actor,place,at,rev);return;}
        if(w.NewGame?.HistoricalCatalog!=CentralCourtCatalog.ShogunateEdition || !TruceNegotiations.Enabled(w) ||
            place!=CentralCourtCatalog.Kyoto || place!=i.Site || i.OtherHouse is not {} other || i.Owner==other || i.Amount is <1 or >90 ||
            !HouseSuccession.Factions(w,at,rev-1).Any(f=>f.Id==other.Value) ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev))
            throw new InvalidDataException("京で異なる二家と1〜90日の不戦期間を指定してください");
        var prior=HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.At<=at && e.Revision<rev).ToArray();
        if(i.Action==CourtAction.AskPeaceMediation){
            if(actor==i.Person || !Broker(i.Person) || i.Reference!=null || HouseSuccession.Head(w,i.Owner,at,rev-1)!=actor ||
                !TransportWork.At(w,i.Person,place,at,rev,true) || Assassination.DeadAt(w,i.Person,at,rev) ||
                !SocialWorld.Ledger(w).Any(e=>e.Input.Action==SocialWorldAction.Visit && e.Actor==actor && e.Input.Person==i.Person && e.Revision<rev) ||
                prior.Any(e=>e.Actor==actor && e.Input.Action==i.Action && e.Input.OtherHouse==other && e.Input.Person==i.Person &&
                    !prior.Any(r=>r.Input.Reference==e.Input.Id)))
                throw new InvalidDataException("当主が面識のある取次に現地で相談してください");
            return;
        }
        var request=prior.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.AskPeaceMediation);
        if(request==null || request.Input.Person!=actor || request.Actor!=i.Person || request.Input.Owner!=i.Owner ||
            request.Input.OtherHouse!=other || request.Input.Amount!=i.Amount || request.At>=at || prior.Any(e=>e.Input.Reference==request.Input.Id) ||
            (actor!=w.PlayerControl && (i.Action==CourtAction.SendPeaceProposal)!=Supports(w,actor,i.Person,at,rev)))
            throw new InvalidDataException("取次本人が依頼原本と同じ二家・期間について回答してください");
    }
    public static IEnumerable<CourtInput> Answers(ContractWorld w,bool player)
    {
        var rows=HouseholdCourt.Events(w);
        foreach(var e in rows.Where(e=>e.Input.Action==CourtAction.AskPeaceMediation && (e.Input.Person==w.PlayerControl)==player &&
            e.At<w.ElapsedSeconds && !rows.Any(r=>Handles(r.Input.Action) && r.Input.Reference==e.Input.Id))){
            foreach(var action in new[]{CourtAction.SendPeaceProposal,CourtAction.DeclinePeaceMediation}){
                if(!player && (action==CourtAction.SendPeaceProposal)!=Supports(w,e.Input.Person,e.Actor,w.ElapsedSeconds,w.CommitSequence))continue;
                yield return e.Input with{Id=HouseholdCourt.Id(action.ToString(),e.Input.Id.Value),Action=action,Person=e.Actor,Reference=e.Input.Id};
            }
        }
    }
    static IEnumerable<MessageEnvelope> Letters(ContractWorld w,CourtInput i,EntityId actor,long at,long rev)
    {
        foreach(var house in new[]{i.Owner,i.OtherHouse!.Value}){
            var faction=HouseSuccession.Factions(w,at,rev-1).Single(f=>f.Id==house.Value);
            var id=HouseholdCourt.Id("peace-proposal",i.Id.Value+"/"+house.Value);
            yield return new MessageEnvelope(id.Value,i.Id.Value,Proposal,actor.Value,faction.Head,
                i.Owner.Value+"・"+i.OtherHouse.Value.Value+"の両家へ。不戦"+i.Amount+"日間の仲裁案。",faction.Home,at){DeliverySite=faction.Home};
        }
    }
    static CourtInput Vote(CourtEvent proposal,MessageEnvelope letter,CourtAction action)
    {
        var source=proposal.Input;
        var first=HouseholdCourt.Id("peace-proposal",source.Id.Value+"/"+source.Owner.Value).Value==letter.Id;
        return new(HouseholdCourt.Id(action.ToString(),letter.Id),action,proposal.Actor,first?source.Owner:source.OtherHouse!.Value,
            new(letter.DeliverySite!),Amount:source.Amount,Reference:source.Id){OtherHouse=first?source.OtherHouse:source.Owner};
    }
    static void CheckVote(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        var rows=HouseholdCourt.Events(w).Where(e=>e.At<=at && e.Revision<rev);
        var source=rows.SingleOrDefault(e=>e.Input.Action==CourtAction.SendPeaceProposal && e.Input.Id==i.Reference);
        if(source==null || HouseSuccession.Head(w,i.Owner,at,rev-1)!=actor || place!=i.Site ||
            !TransportWork.At(w,actor,place,at,rev,true) || Assassination.DeadAt(w,actor,at,rev) ||
            rows.Any(e=>IsVote(e.Input.Action) && e.Input.Reference==i.Reference && e.Input.Owner==i.Owner))
            throw new InvalidDataException("宛先の現当主が同じ仲裁案に一度だけ回答できます");
        var letter=Letters(w,source.Input,source.Actor,source.At,source.Revision).SingleOrDefault(m=>m.RecipientId==actor.Value && Vote(source,m,i.Action)==i);
        if(letter==null || MessageFlow.LearnedAt(w.Messaging,letter.Id,actor.Value) is not {} opened || opened>at)
            throw new InvalidDataException("自家宛の仲裁案を開封してから返答してください");
    }
    public static IEnumerable<CourtInput> Votes(ContractWorld w,EntityId actor)
    {
        foreach(var e in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.SendPeaceProposal))
            foreach(var letter in Letters(w,e.Input,e.Actor,e.At,e.Revision).Where(m=>m.RecipientId==actor.Value && MessageFlow.Knows(w.Messaging,m.Id,actor.Value))){
                foreach(var action in new[]{CourtAction.AcceptPeaceProposal,CourtAction.RejectPeaceProposal}){
                    var vote=Vote(e,letter,action);
                    try{CheckVote(w,vote,actor,vote.Site,w.ElapsedSeconds,w.CommitSequence+1);}
                    catch(InvalidDataException){continue;}
                    yield return vote;
                }
            }
    }
    internal static void ReadAndRespond(ContractWorld w)
    {
        foreach(var letter in w.Messaging.Messages.Where(m=>m.Kind==Proposal && m.RecipientId!=w.PlayerControl.Value &&
            w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==m.RecipientId))){
            var actor=new EntityId(letter.RecipientId);
            if(!MessageFlow.Knows(w.Messaging,letter.Id,actor.Value))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(letter.Id,actor.Value,w.ElapsedSeconds,letter.Id)];
            foreach(var vote in Votes(w,actor).Where(i=>i.Reference?.Value==letter.CorrelationId &&
                (i.Action==CourtAction.AcceptPeaceProposal)==(i.Amount<=TruceNegotiations.MaximumNpcDays)).ToArray())
                TransactionalWorld.TryApply(w,c=>HouseholdCourt.Execute(c,vote,actor));
        }
    }
    static MessageEnvelope Reply(CourtInput i,EntityId actor,long at)=>new(HouseholdCourt.Id("peace-answer",i.Id.Value).Value,i.Reference!.Value.Value,
        Response,actor.Value,i.Person.Value,i.Action==CourtAction.AcceptPeaceProposal?"同じ条件の仲裁案を承諾する。":"仲裁案を拒む。",
        CentralCourtCatalog.Kyoto.Value,at){DeliverySite=CentralCourtCatalog.Kyoto.Value};
    internal static void Send(ContractWorld w,CourtInput i,EntityId actor){
        if(IsVote(i.Action)){
            var reply=Reply(i,actor,w.ElapsedSeconds);MessageFlow.Send(w.Messaging,reply,w.ElapsedSeconds);
            w.SealedMessages=[..w.SealedMessages,new(reply.Id)];RegionalCampaign.Bind(w);return;
        }
        if(i.Action!=CourtAction.SendPeaceProposal)return;
        foreach(var letter in Letters(w,i,actor,w.ElapsedSeconds,w.CommitSequence)){
            MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        }
        RegionalCampaign.Bind(w);
    }
    internal static void Validate(ContractWorld w,CourtEvent e){
        if(IsVote(e.Input.Action)){
            var reply=Reply(e.Input,e.Actor,e.At);
            if(!w.Messaging.Messages.Contains(reply) || !w.SealedMessages.Any(id=>id.Value==reply.Id))throw new InvalidDataException("仲裁への返書が原本と異なります");
            return;
        }
        if(e.Input.Action!=CourtAction.SendPeaceProposal)return;
        foreach(var letter in Letters(w,e.Input,e.Actor,e.At,e.Revision))
            if(!w.Messaging.Messages.Contains(letter) || !w.SealedMessages.Any(id=>id.Value==letter.Id))
                throw new InvalidDataException("仲裁案の原本と各家宛の封書が一致しません");
    }
}