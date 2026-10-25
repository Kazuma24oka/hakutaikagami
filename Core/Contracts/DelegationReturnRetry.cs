using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static class DelegationReturnRetry
{
    static bool Kind(MessageEnvelope m)=>m.Kind is "region-report" or DelegationMission.ArrivalKind;
    static bool FailedKnownAt(ContractWorld w,MessageAttempt a,string sender,long at)=>a.FailureAt is {} failed&&failed<=at&&w.Messaging.Messages.Any(m=>m.Kind=="delivery-failure"&&m.Body==a.Id&&m.RecipientId==sender&&MessageFlow.LearnedAt(w.Messaging,m.Id,sender) is {} known&&known<=at);
    internal static FieldPoint KnownAddress(ContractWorld w,long at)
    {
        var attempt=w.Messaging.Attempts.Select((a,index)=>(Attempt:a,Index:index)).Where(x=>x.Attempt.Status==AttemptStatus.Delivered&&x.Attempt.FinishedAt<=at&&w.Messaging.Messages.Any(m=>m.Id==x.Attempt.MessageId&&m.SenderId==w.PlayerControl.Value&&m.RecipientId==ContractFixture.Clerk.Value&&m.Kind is "region-order" or DelegationInquiry.QueryKind)).OrderBy(x=>x.Attempt.SentAt).ThenBy(x=>x.Index).Select(x=>x.Attempt).LastOrDefault()??throw new InvalidDataException("No personally received return address");
        return w.Region!.Deliveries.Single(d=>d.Attempt==attempt.Id).Origin;
    }
    internal static string[] Apply(ContractWorld w,DelegationQuestion question)
    {
        var list=new List<string>();
        foreach(var m in w.Messaging.Messages.Where(m=>Kind(m)&&m.CorrelationId==question.Directive.Value&&m.SenderId==ContractFixture.Clerk.Value&&m.RecipientId==w.PlayerControl.Value).ToArray())
        {
            var previous=MessageFlow.Latest(w.Messaging,m.Id);if(previous.Status is not (AttemptStatus.Lost or AttemptStatus.Rejected)||previous.Number>=MessageFlow.MaxAttempts||previous.LateDueAt!=null||!FailedKnownAt(w,previous,m.SenderId,w.ElapsedSeconds))continue;
            try{var address=KnownAddress(w,w.ElapsedSeconds);var origin=RegionalBattle.Position(w,ContractFixture.Clerk);var path=RegionalGround.Plan(origin,address,w.ElapsedSeconds,courier:true,scenery:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??w.ElapsedSeconds+1);MessageFlow.Retry(w.Messaging,m.Id,m.SenderId,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));w.Region=w.Region! with{Deliveries=[..w.Region.Deliveries,new(MessageFlow.Latest(w.Messaging,m.Id).Id,origin,address,path,due)]};list.Add(m.Id);}
            catch(Exception e)when(e is InvalidDataException or InvalidOperationException or OverflowException){/* No invented reason about the distant recipient. The reply reports only accepted sends. */}
        }
        return list.ToArray();
    }
    internal static void Validate(ContractWorld w,MessageEnvelope[] answers)
    {
        var typed=answers.Select(m=>ContractJson.Read<DelegationAnswer>(System.Text.Encoding.UTF8.GetBytes(m.Body))).ToArray();
        foreach(var answer in typed){var q=w.Messaging.Messages.Single(m=>m.Id==DelegationInquiry.MessageId(answer.Query));var question=ContractJson.Read<DelegationQuestion>(System.Text.Encoding.UTF8.GetBytes(q.Body));if(question.Resend!=(answer.Retried!=null)||answer.Retried is {} ids&&(ids.Distinct().Count()!=ids.Length||ids.Any(id=>!w.Messaging.Messages.Any(m=>m.Id==id&&Kind(m)&&m.CorrelationId==answer.Directive.Value&&m.SenderId==ContractFixture.Clerk.Value&&m.RecipientId==w.PlayerControl.Value))))throw new InvalidDataException("Return retry summary exceeds the received inquiry");if(answer.Retried is {} sent)foreach(var id in sent)if(!w.Messaging.Attempts.Any(a=>a.MessageId==id&&a.Number>1&&a.SentAt==answer.At))throw new InvalidDataException("Return summary invents a dispatch");}
        foreach(var a in w.Messaging.Attempts.Where(a=>a.Number>1&&w.Messaging.Messages.Any(m=>m.Id==a.MessageId&&Kind(m)&&m.SenderId==ContractFixture.Clerk.Value)))
        {
            var m=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);var previous=w.Messaging.Attempts.Single(x=>x.MessageId==a.MessageId&&x.Number==a.Number-1);if(a.Number>3||previous.LateDueAt!=null||!FailedKnownAt(w,previous,m.SenderId,a.SentAt)||typed.Count(r=>r.At==a.SentAt&&r.Retried?.Contains(m.Id)==true)!=1||w.Messaging.Receipts.Any(r=>r.MessageId==m.Id&&r.ReceivedAt<=a.SentAt))throw new InvalidDataException("Return retry lacks known failure and one received resend request");if(w.Region!.Deliveries.Single(d=>d.Attempt==a.Id).Address!=KnownAddress(w,a.SentAt))throw new InvalidDataException("Return retry uses a hidden or stale sender address");
        }
    }
}
