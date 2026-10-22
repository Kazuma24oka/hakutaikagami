using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitPostOrder(EntityId Request,string Action,string Role,EntityId AddressEvidence);
public sealed record UnitPostAnswer(string Original,bool Delivered,bool Applied);
public sealed record UnitTransportSnapshot(MessageEnvelope[] Messages,MessageAttempt[] Attempts,FormalReceipt[] Receipts,PersonalKnowledge[] Knowledge);
public static partial class NewGameMilitary
{
    static bool UnitEnvelope(MessageEnvelope m)=>m.Kind.StartsWith("unit-post-",StringComparison.Ordinal)||m.Kind=="delivery-failure"&&m.Id.StartsWith("message:unit-post/",StringComparison.Ordinal);
    public static bool HandlesUnitMessage(ContractWorld w,string id)=>w.NewMilitary?.Withdrawal!=null&&w.Messaging.Messages.Any(m=>m.Id==id&&UnitEnvelope(m));
    static string UnitMessage(EntityId request,string kind)=>"message:unit-post/"+kind+"/"+Hash(request.Value);
    static EntityId UnitCourier(EntityId request)=>new("courier:unit-post/"+Hash(request.Value));
    static EntityId UnitOrder(EntityId request)=>new("order:unit-post/"+Hash(request.Value));
    public static string[] MessageParticipants(ContractWorld w)=>w.NewMilitary?.Withdrawal is {} x&&x.Dispatches.Length>0?[UnitIds[0].Value,..x.Dispatches.Select(d=>UnitCourier(d.Id).Value)]:[];
    static FieldPoint PhysicalPerson(ContractWorld w,long at)=>DeployedAt(w,at)?PersonPosition(w,at):FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,at));
    internal static void BeforeUnitMessages(ContractWorld w)
    {
        if(w.NewMilitary?.Withdrawal is not {} x)return;
        foreach(var d in x.Dispatches){var a=MessageFlow.Latest(w.Messaging,d.Message);if(a.Status!=AttemptStatus.InTransit||a.DueAt!=w.ElapsedSeconds)continue;
            bool present=CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&Deployed(w)&&StrengthAt(w,0,w.ElapsedSeconds)>0&&RegionalGround.Distance(Position(w,0,w.ElapsedSeconds),d.Destination)<=1000;
            if(!present)MessageFlow.Fail(w.Messaging,d.Message);
        }
        foreach(var r in x.Receipts){var a=MessageFlow.Latest(w.Messaging,r.ReplyMessage);if(a.Status==AttemptStatus.InTransit&&a.DueAt==w.ElapsedSeconds&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),r.ReplyDestination)>1000)MessageFlow.Fail(w.Messaging,r.ReplyMessage);}
    }
    static UnitTransportSnapshot UnitTransport(ContractWorld w)
    {
        var messages=w.Messaging.Messages.Where(UnitEnvelope).ToArray();var keys=messages.Select(m=>m.Id).ToHashSet();
        return new(messages.OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray(),w.Messaging.Attempts.Where(a=>keys.Contains(a.MessageId)).OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray(),w.Messaging.Receipts.Where(r=>keys.Contains(r.MessageId)).OrderBy(r=>r.MessageId,StringComparer.Ordinal).ToArray(),w.Messaging.Knowledge.Where(k=>keys.Contains(k.MessageId)).OrderBy(k=>k.MessageId,StringComparer.Ordinal).ThenBy(k=>k.CharacterId,StringComparer.Ordinal).ToArray());
    }
    static void RewindUnitTransport(ContractWorld w)
    {
        var x=w.NewMilitary?.Withdrawal;var messages=w.Messaging.Messages.Where(UnitEnvelope).Select(m=>m.Id).ToHashSet();
        var keep=(x?.Dispatches.Select(d=>d.Message)??[]).Concat(x?.Receipts.Select(r=>r.ReplyMessage)??[]).ToHashSet();
        w.Messaging.Messages=w.Messaging.Messages.Where(m=>!messages.Contains(m.Id)||keep.Contains(m.Id)).ToArray();
        w.Messaging.Attempts=w.Messaging.Attempts.Where(a=>!messages.Contains(a.MessageId)).Concat((x?.Dispatches??[]).SelectMany(d=>{
            var r=x!.Receipts.SingleOrDefault(r=>r.Id==d.Id);var a=new MessageAttempt(d.Attempt,d.Message,1,d.At,d.DueAt,d.Recipient.Value);
            if(r!=null)a=a with{Status=r.Delivered?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!r.Delivered,FinishedAt=r.At,FailureAt=r.Delivered?null:r.At};
            if(r==null)return new[]{a};var reply=x.Replies.SingleOrDefault(t=>t.Id==d.Id);var b=new MessageAttempt(r.ReplyAttempt,r.ReplyMessage,1,r.At,r.ReplyDueAt,w.PlayerControl.Value);
            if(reply!=null)b=b with{Status=reply.Received?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!reply.Received,FinishedAt=reply.At,FailureAt=reply.Received?null:reply.At};return new[]{a,b};
        })).ToArray();
        // Preserve original global order while deterministically rebuilding only unit-post transport fields.
        var rank=w.Messaging.Messages.Select((m,i)=>(m.Id,i)).ToDictionary(x=>x.Id,x=>x.i);w.Messaging.Attempts=w.Messaging.Attempts.OrderBy(a=>rank[a.MessageId]).ThenBy(a=>a.Number).ToArray();
        w.Messaging.Receipts=w.Messaging.Receipts.Where(r=>!messages.Contains(r.MessageId)).Concat((x?.Receipts??[]).Where(r=>r.Delivered).Select(r=>{var d=x!.Dispatches.Single(d=>d.Id==r.Id);return new FormalReceipt(d.Message,d.Recipient.Value,d.Recipient.Value,r.At,false);})).Concat((x?.Replies??[]).Where(r=>r.Received).Select(r=>new FormalReceipt(x!.Receipts.Single(t=>t.Id==r.Id).ReplyMessage,w.PlayerControl.Value,w.PlayerControl.Value,r.At,false))).OrderBy(r=>r.ReceivedAt).ThenBy(r=>r.MessageId,StringComparer.Ordinal).ToArray();
        w.Messaging.Knowledge=w.Messaging.Knowledge.Where(k=>!messages.Contains(k.MessageId)).Concat((x?.Receipts??[]).Where(r=>r.Delivered).Select(r=>{var d=x!.Dispatches.Single(d=>d.Id==r.Id);return new PersonalKnowledge(d.Message,d.Recipient.Value,r.At,d.Message);})).Concat((x?.Replies??[]).Where(r=>r.Received).Select(r=>{var id=x!.Receipts.Single(t=>t.Id==r.Id).ReplyMessage;return new PersonalKnowledge(id,w.PlayerControl.Value,r.At,id);})).ToArray();
    }
    static void ValidateUnitTransport(ContractWorld w)
    {
        var x=w.NewMilitary!.Withdrawal!;var expected=x.Dispatches.Select(d=>d.Message).Concat(x.Receipts.Select(r=>r.ReplyMessage)).ToHashSet();
        Need(w.Messaging.Messages.Where(UnitEnvelope).Select(m=>m.Id).ToHashSet().SetEquals(expected),"部隊宛の原本台帳が一致しません");
        Need(w.Messaging.Attempts.Where(a=>expected.Contains(a.MessageId)).All(a=>a.Number==1&&a.LateDueAt==null),"部隊宛の未登録の再送/遅着です");
        var copy=BeforeState(w,w.ElapsedSeconds,w.CommitSequence);
        Need(Same(UnitTransport(w),UnitTransport(copy)),"部隊宛の正式受領/試行/知識と業務台帳が違います");
    }
}
