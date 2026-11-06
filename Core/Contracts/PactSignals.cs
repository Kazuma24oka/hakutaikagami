using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record PactResponse(EntityId Pact,bool Acted,long At,long Revision);
public static class PactSignals
{
    public const string Call="secret-pact-call",Reply="secret-pact-reply";
    public static string Letter(SecretPact pact,string kind)=>InformationSchemes.Id(kind,pact.Id.Value).Value;
    public static bool Sent(ContractWorld w,SecretPact p)=>w.Messaging.Messages.Any(m=>m.Id==Letter(p,Call));
    internal static void Send(ContractWorld w,SecretPact p,bool outbound,string body)
    {
        var sender=outbound?p.Proposer:p.Partner;var recipient=outbound?p.Partner:p.Proposer;
        var kind=outbound?Call:Reply;
        var address=FamilyBirth.People(w).Single(person=>person.Id==recipient.Value).Site;
        var letter=new MessageEnvelope(Letter(p,kind),p.Id.Value,kind,sender.Value,recipient.Value,body,address,w.ElapsedSeconds);
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        RegionalCampaign.Bind(w);
        HouseholdCourt.RecordForeignLetter(w,letter);
    }

    internal static void Dispatch(ContractWorld w,SecretPact p)
    {
        if(p.Purpose==PactPurpose.RiseTogether && JointIndependence.Declaration(w,p)==null)
            w.Declarations=[..w.Declarations??[],new(p.Id,p.Proposer,p.House,InformationSchemes.Id("independent-house",p.Id.Value),
                SocialWorld.Site(w,p.Proposer)!.Value,w.ElapsedSeconds,w.CommitSequence){CheckGarrison=true}];
        PactSignals.Send(w,p,true,p.Purpose==PactPurpose.RiseTogether?"独立を宣言した。約定に応じるか返書を乞う。":
            DefectionPacts.Name(p.Purpose)+"との約定について、今の意向を伺います。");
    }

    static string ReplyText(bool acted)=>acted?"合図を受け、約した行動を取りました。":"今回は応じられません。";

    internal static void Receive(ContractWorld w)
    {
        foreach(var p in w.SecretPacts??[])
        {
            var id=Letter(p,Call);
            if(!p.Accepted || (w.PactResponses??[]).Any(r=>r.Pact==p.Id) ||
                !w.Messaging.Receipts.Any(r=>r.MessageId==id && r.ReceiverId==p.Partner.Value) ||
                Assassination.DeadAt(w,p.Partner,w.ElapsedSeconds,w.CommitSequence))continue;
            if(!MessageFlow.Knows(w.Messaging,id,p.Partner.Value))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id,p.Partner.Value,w.ElapsedSeconds,id)];
            var acted=DefectionPacts.HouseAt(w,p.Partner,w.ElapsedSeconds,w.CommitSequence)==p.House &&
                !Assassination.DeadAt(w,p.Proposer,w.ElapsedSeconds,w.CommitSequence) &&
                !HostageCustody.HeldAt(w,p.Partner,w.ElapsedSeconds,w.CommitSequence) &&
                !NpcArmies.CommanderBusyAt(w,p.Partner,w.ElapsedSeconds,w.CommitSequence) &&
                CharacterRelations.DiagnosticScores(w,p.Partner,p.Proposer).Hostility<=
                    CharacterRelations.DiagnosticTrait(w,p.Partner,SocialTrait.Ambition);
            w.PactResponses=[..w.PactResponses??[],new(p.Id,acted,w.ElapsedSeconds,w.CommitSequence)];
            Send(w,p,false,ReplyText(acted));
        }
    }

    public static bool Withholds(ContractWorld w,EntityId person,long at,long rev)=>
        (w.PactResponses??[]).Any(r=>r.Acted && r.At<=at && r.Revision<=rev && at-r.At<86400 &&
            (w.SecretPacts??[]).Any(p=>p.Id==r.Pact && p.Partner==person && p.Purpose==PactPurpose.WithholdService));

    internal static TimedRelation[] CloseService(ContractWorld w,TimedRelation[] rows)
    {
        if(w.PactResponses==null)return rows;
        var result=(TimedRelation[])rows.Clone();
        foreach(var r in w.PactResponses.Where(r=>r.Acted))
        {
            var p=w.SecretPacts!.Single(p=>p.Id==r.Pact);
            if(p.Purpose!=PactPurpose.LeaveHouse)continue;
            for(int i=0;i<result.Length;i++)
            {
                var edge=result[i];
                if(edge.Subject==p.Partner && edge.From<=r.At && r.At<edge.Until &&
                    (edge.Kind=="membership" && edge.Target==p.House ||
                     edge.Kind=="service" && edge.Target==HouseSuccession.Head(w,p.House,r.At,r.Revision-1)))
                    result[i]=edge with{Until=r.At,Revision=r.Revision};
            }
        }
        return result;
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.PactResponses??[];var old=prior?.PactResponses??[];
        if(!rows.Take(old.Length).SequenceEqual(old) || rows.DistinctBy(r=>r.Pact).Count()!=rows.Length)
            throw new InvalidDataException("調略の実行原本が重複または変更されています");
        foreach(var r in rows)
        {
            var p=w.SecretPacts?.SingleOrDefault(p=>p.Id==r.Pact);
            if(p==null || !p.Accepted || r.At<p.At || r.At>w.ElapsedSeconds || r.Revision<=p.Revision || r.Revision>w.CommitSequence ||
                MessageFlow.LearnedAt(w.Messaging,Letter(p,Call),p.Partner.Value)!=r.At ||
                !w.Messaging.Messages.Any(m=>m.Id==Letter(p,Call) && m.CorrelationId==p.Id.Value && m.Kind==Call &&
                    m.CreatedAt>=p.At && m.CreatedAt<=r.At && m.SenderId==p.Proposer.Value && m.RecipientId==p.Partner.Value) ||
                !w.Messaging.Messages.Any(m=>m.Id==Letter(p,Reply) && m.CorrelationId==p.Id.Value && m.Kind==Reply &&
                    m.CreatedAt==r.At && m.SenderId==p.Partner.Value && m.RecipientId==p.Proposer.Value && m.Body==ReplyText(r.Acted)))
                throw new InvalidDataException("合図の受領・本人の行動・返答が一致しません");
        }
    }
}

public sealed partial class WorldSession
{
    public CommandResult SignalPact(EntityId pact,long shown)
    {
        lock(gate)
        {
            var p=state.SecretPacts?.SingleOrDefault(p=>p.Id==pact && p.Proposer==state.PlayerControl && p.Accepted);
            if(p==null)return new(ResultCode.NotAvailable,pact,state.CommitSequence,"実行を求められる合意を指定してください");
            if(PactSignals.Sent(state,p))return new(ResultCode.AlreadyAccepted,pact,state.CommitSequence,"合図は送付済みです");
            if(shown!=state.CommitSequence)return new(ResultCode.StateChanged,pact,state.CommitSequence,"表示を更新してください");
            if(SocialWorld.Site(state,p.Proposer)==null || HostageCustody.HeldAt(state,p.Proposer,state.ElapsedSeconds,shown) || Assassination.DeadAt(state,p.Proposer,state.ElapsedSeconds,shown))
                return new(ResultCode.NotAvailable,pact,shown,"現地で書状を送ってください");
            if(p.Purpose==PactPurpose.RiseTogether && JointIndependence.Declaration(state,p)==null &&
                DefectionPacts.HouseAt(state,p.Proposer,state.ElapsedSeconds,shown)!=p.House)
                return new(ResultCode.NotAvailable,pact,shown,"独立の対象となる旧所属が変わりました");
            Change(w=>PactSignals.Dispatch(w,p));
            return new(ResultCode.Accepted,pact,state.CommitSequence,"密書を送りました");
        }
    }
}
