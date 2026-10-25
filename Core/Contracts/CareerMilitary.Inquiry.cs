using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record MilitaryStatusNotice(string Inquiry,string? Original,string? OriginalBody);
public sealed record MilitaryRetryCard(string Message,string Attempt);
public static partial class CareerMilitary
{
    static string StatusMessage(CareerRequest q)=>"message:military/status/"+Hash(q.Input.Id.Value);
    static MilitaryStatusNotice ReadStatus(ContractWorld w,MilitaryEvent e)=>ContractJson.Read<MilitaryStatusNotice>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==e.Message).Body));
    internal static void UpdateInquiries(ContractWorld w)
    {
        if(w.Military!.Requests.Length==0)return;
        foreach(var q in w.Career!.Requests.Where(q=>q.Input.Action==CareerAction.Inquire&&MessageFlow.Knows(w.Messaging,q.Message,Lord.Value)).ToArray()){
            if(w.Military.Events.Any(e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==q.Message))continue;
            var actual=w.Military.Events.LastOrDefault(e=>e.Effect!=MilitaryEffect.StatusSent);var original=actual?.Message;var body=new MilitaryStatusNotice(q.Message,original,original==null?null:w.Messaging.Messages.Single(m=>m.Id==original).Body);string id=StatusMessage(q);
            MessageFlow.Send(w.Messaging,new(id,q.Input.Id.Value,"military-status",Lord.Value,w.PlayerControl.Value,Text(body),ContractFixture.Castle.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];
            w.Military=w.Military with{Events=[..w.Military.Events,new(MilitaryEffect.StatusSent,w.ElapsedSeconds,w.CommitSequence,q.Message,id)]};
        }
    }
    static void ValidateStatus(ContractWorld w,MilitaryEvent e,int index)
    {
        var q=w.Career!.Requests.SingleOrDefault(q=>q.Message==e.Source&&q.Input.Action==CareerAction.Inquire);if(q==null||q.Revision>=e.Revision||MessageFlow.LearnedAt(w.Messaging,e.Source,Lord.Value) is not {} learned||learned>e.At||w.Military!.Requests.All(q=>q.Revision>=e.Revision))throw new InvalidDataException("Military status lacks own physically received inquiry");
        var original=w.Military.Events.Take(index).LastOrDefault(x=>x.Effect!=MilitaryEffect.StatusSent);var id=original?.Message;var body=new MilitaryStatusNotice(e.Source,id,id==null?null:w.Messaging.Messages.Single(m=>m.Id==id).Body);var m=w.Messaging.Messages.Single(m=>m.Id==e.Message);
        if(e.Message!=StatusMessage(q)||m.Kind!="military-status"||m.CorrelationId!=q.Input.Id.Value||m.SenderId!=Lord.Value||m.RecipientId!=w.PlayerControl.Value||m.CreatedAt!=e.At||m.Scope!=ContractFixture.Castle.Value||m.DeliverBy!=null||m.Body!=Text(body)||!w.SealedMessages.Any(x=>x.Value==m.Id))throw new InvalidDataException("Military status differs from principal's historical own record");
    }
    public static MilitaryRetryCard? Retryable(WorldSession s,EntityId actor)=>Retryable(s.DiagnosticSnapshot(),actor);
    internal static MilitaryRetryCard? Retryable(ContractWorld w,EntityId actor)
    {
        if(!Enabled(w)||actor!=w.PlayerControl)return null;
        foreach(var q in w.Military!.Requests.Reverse().Where(q=>q.Message!=null)){
            var m=w.Messaging.Messages.Single(m=>m.Id==q.Message);var a=MessageFlow.Latest(w.Messaging,m.Id);
            if(a.Status is AttemptStatus.Lost or AttemptStatus.Rejected&&a.Number<MessageFlow.MaxAttempts&&MessageFlow.FailureKnown(w.Messaging,a.Id,actor.Value)&&!w.Messaging.Receipts.Any(r=>r.MessageId==m.Id)&&(m.DeliverBy==null||m.DeliverBy>=w.ElapsedSeconds+1))return new(m.Id,a.Id);
        }return null;
    }
    static void ValidateRetries(ContractWorld w)
    {
        foreach(var q in w.Military!.Requests.Where(q=>q.Message!=null)){
            var m=w.Messaging.Messages.Single(m=>m.Id==q.Message);var attempts=w.Messaging.Attempts.Where(a=>a.MessageId==q.Message).OrderBy(a=>a.Number).ToArray();
            if(attempts.Length>MessageFlow.MaxAttempts||!attempts.Select(a=>a.Number).SequenceEqual(Enumerable.Range(1,attempts.Length)))throw new InvalidDataException("Invalid military retry sequence");
            foreach(var a in attempts.Skip(1)){
                var prior=attempts.Single(p=>p.Number==a.Number-1);
                bool failureKnown=w.Messaging.Messages.Any(f=>f.Kind=="delivery-failure"&&f.Body==prior.Id&&f.RecipientId==w.PlayerControl.Value&&MessageFlow.LearnedAt(w.Messaging,f.Id,w.PlayerControl.Value) is {} learned&&learned<=a.SentAt);
                if(prior.FailureAt==null||prior.FailureAt>a.SentAt||!failureKnown||w.Messaging.Receipts.Any(r=>r.MessageId==q.Message&&r.ReceivedAt<=a.SentAt)||m.DeliverBy<a.SentAt+1)throw new InvalidDataException("Military retry preceded known failure or followed receipt/deadline");
            }
        }
    }
}
