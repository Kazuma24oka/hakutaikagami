using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitPostOrder(EntityId Request,string Action,string Role,EntityId AddressEvidence)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? HoldingPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportPolicy {get;init;}
}
public sealed record UnitPostAnswer(string Original,bool Delivered,bool Applied);
public sealed record UnitTransportSnapshot(MessageEnvelope[] Messages,MessageAttempt[] Attempts,FormalReceipt[] Receipts,PersonalKnowledge[] Knowledge);
public static partial class NewGameMilitary
{
    static bool UnitEnvelope(MessageEnvelope m)=>m.Kind.StartsWith("unit-post-",StringComparison.Ordinal)||m.Kind=="delivery-failure"&&m.Id.StartsWith("message:unit-post/",StringComparison.Ordinal);
    public static bool HandlesUnitMessage(ContractWorld w,string id)=>w.NewMilitary?.Withdrawal!=null&&w.Messaging.Messages.Any(m=>m.Id==id&&UnitEnvelope(m));
    static string UnitMessage(EntityId request,string kind)=>"message:unit-post/"+kind+"/"+Hash(request.Value);
    static EntityId UnitCourier(EntityId request)=>new("courier:unit-post/"+Hash(request.Value));
    static EntityId UnitOrder(EntityId request)=>new("order:unit-post/"+Hash(request.Value));
    public static string[] MessageParticipants(ContractWorld w)=>w.NewMilitary?.Withdrawal is {}&&UnitDispatches(w).Length>0?[UnitIds[0].Value,..UnitDispatches(w).Select(d=>d.Recipient.Value).Where(id=>id!=UnitIds[0].Value).Distinct(StringComparer.Ordinal),..UnitDispatches(w).Select(d=>UnitCourier(d.Id).Value).Concat((w.NewMilitary?.ReplyRecovery?.Flights??[]).Select(f=>f.Carrier.Value)).Concat((w.NewMilitary?.Cancellation?.Notices??[]).Select(f=>f.Carrier.Value)).Distinct(StringComparer.Ordinal)]:[];
    static FieldPoint PhysicalPerson(ContractWorld w,long at)=>DeployedAt(w,at)?PersonPosition(w,at):FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,at));
    internal static void BeforeUnitMessages(ContractWorld w)
    {
        BeforeCancellationNotices(w);BeforeHeldMessages(w);BeforeHeldOrderAcks(w);BeforeReplyRecovery(w);if(w.NewMilitary?.Withdrawal is not {} x)return;
        foreach(var d in UnitDispatches(w)){var a=w.Messaging.Attempts.Single(a=>a.Id==d.Attempt);if(a.Status!=AttemptStatus.InTransit||a.DueAt!=w.ElapsedSeconds)continue;
            bool present=UnitPostPresent(w,d.Destination,d.Recipient);
            if(!present)MessageFlow.Fail(w.Messaging,d.Message);
        }
        foreach(var r in UnitReceipts(w)){var a=MessageFlow.Latest(w.Messaging,r.ReplyMessage);if(a.Status==AttemptStatus.InTransit&&a.DueAt==w.ElapsedSeconds&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),r.ReplyDestination)>1000)MessageFlow.Fail(w.Messaging,r.ReplyMessage);}
    }
    static UnitTransportSnapshot UnitTransport(ContractWorld w)
    {
        var messages=w.Messaging.Messages.Where(UnitEnvelope).ToArray();var keys=messages.Select(m=>m.Id).ToHashSet();
        return new(messages.OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray(),w.Messaging.Attempts.Where(a=>keys.Contains(a.MessageId)).OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray(),w.Messaging.Receipts.Where(r=>keys.Contains(r.MessageId)).OrderBy(r=>r.MessageId,StringComparer.Ordinal).ToArray(),w.Messaging.Knowledge.Where(k=>keys.Contains(k.MessageId)).OrderBy(k=>k.MessageId,StringComparer.Ordinal).ThenBy(k=>k.CharacterId,StringComparer.Ordinal).ToArray());
    }
    static void RewindUnitTransport(ContractWorld w)
    {
        var keys=w.Messaging.Messages.Where(UnitEnvelope).Select(m=>m.Id).ToHashSet();var dispatches=UnitDispatches(w);var receipts=UnitReceipts(w);var replies=UnitReplies(w);var flights=w.NewMilitary?.ReplyRecovery?.Flights??[];
        var cancelNotices=w.NewMilitary?.Cancellation?.Notices??[];var acks=w.NewMilitary?.CommandHolding?.Outcomes.Where(f=>f.Received).ToArray()??[];var keep=cancelNotices.Select(f=>f.Message).Concat(acks.Select(f=>f.ReplyMessage!)).Concat(dispatches.Select(d=>d.Message)).Concat(receipts.Select(r=>r.ReplyMessage)).Concat(flights.Select(f=>f.Message)).ToHashSet();
        w.Messaging.Messages=w.Messaging.Messages.Where(m=>!keys.Contains(m.Id)||keep.Contains(m.Id)).ToArray();var generated=new List<MessageAttempt>();
        foreach(var d in dispatches){
            var r=receipts.SingleOrDefault(r=>r.Id==d.Id);var a=new MessageAttempt(d.Attempt,d.Message,int.Parse(d.Attempt.Split('/').Last(),System.Globalization.CultureInfo.InvariantCulture),d.At,d.DueAt,d.Recipient.Value);
            if(r!=null)a=a with{Status=r.ArrivalStatus??(r.Delivered?AttemptStatus.Delivered:AttemptStatus.Lost),FailOnArrival=(r.ArrivalStatus??(r.Delivered?AttemptStatus.Delivered:AttemptStatus.Lost))==AttemptStatus.Lost,FinishedAt=r.At,FailureAt=r.Delivered?null:r.At};generated.Add(a);
            if(r==null)continue;var observed=replies.SingleOrDefault(t=>t.Id==d.Id);var b=new MessageAttempt(r.ReplyAttempt,r.ReplyMessage,1,r.At,r.ReplyDueAt,w.PlayerControl.Value);
            if(observed!=null)b=b with{Status=observed.Received?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!observed.Received,FinishedAt=observed.At,FailureAt=observed.Received?null:observed.At};generated.Add(b);
        }
        foreach(var f in flights){
            var observed=w.NewMilitary!.ReplyRecovery!.Arrivals.SingleOrDefault(a=>a.Attempt==f.Attempt);string receiver=f.Kind==UnitReplyLeg.FailureReport?UnitDispatches(w).Single(d=>d.Id==f.Dispatch).Recipient.Value:w.PlayerControl.Value;
            var a=new MessageAttempt(f.Attempt,f.Message,int.Parse(f.Attempt.Split('/').Last(),System.Globalization.CultureInfo.InvariantCulture),f.At,f.DueAt,receiver);
            if(observed!=null)a=a with{Status=observed.Delivered?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!observed.Delivered,FinishedAt=observed.At,FailureAt=observed.Delivered?null:observed.At};generated.Add(a);
        }
        foreach(var f in acks){var observed=w.NewMilitary!.CommandHolding!.Acks.SingleOrDefault(a=>a.Attempt==f.ReplyAttempt);var a=new MessageAttempt(f.ReplyAttempt!,f.ReplyMessage!,1,f.At,f.DueAt,w.PlayerControl.Value);if(observed!=null)a=a with{Status=observed.Received?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!observed.Received,FinishedAt=observed.At,FailureAt=observed.Received?null:observed.At};generated.Add(a);}
        foreach(var f in cancelNotices){var observed=w.NewMilitary!.Cancellation!.NoticeArrivals.SingleOrDefault(a=>a.Id==f.Id);var a=new MessageAttempt(f.Attempt,f.Message,1,f.At,f.DueAt,w.PlayerControl.Value);if(observed!=null)a=a with{Status=observed.Received?AttemptStatus.Delivered:AttemptStatus.Lost,FailOnArrival=!observed.Received,FinishedAt=observed.At,FailureAt=observed.Received?null:observed.At};generated.Add(a);}
        foreach(var t in w.NewMilitary?.HeldDelivery?.Transfers??[]){
            int index=generated.FindIndex(a=>a.Id==t.Attempt);Need(index>=0,"保持原本の配送試行がありません");var observed=w.NewMilitary!.HeldDelivery!.Arrivals.SingleOrDefault(a=>a.Id==t.Id);var a=generated[index] with{LateDueAt=t.DueAt};
            if(observed!=null)a=a with{Status=observed.Received?AttemptStatus.Delivered:AttemptStatus.Rejected,FinishedAt=observed.At,ArrivedLate=observed.Received};generated[index]=a;
        }
        var rank=w.Messaging.Messages.Select((m,i)=>(m.Id,i)).ToDictionary(x=>x.Id,x=>x.i);Need(generated.All(a=>rank.ContainsKey(a.MessageId)),"配送便の原本が欠落しています");
        w.Messaging.Attempts=w.Messaging.Attempts.Where(a=>!keys.Contains(a.MessageId)).Concat(generated).OrderBy(a=>rank[a.MessageId]).ThenBy(a=>a.Number).ToArray();
        var received=generated.Where(a=>a.Status==AttemptStatus.Delivered).GroupBy(a=>a.MessageId).Select(g=>g.OrderBy(a=>a.FinishedAt).ThenBy(a=>a.Id,StringComparer.Ordinal).First()).Select(a=>new FormalReceipt(a.MessageId,w.Messaging.Messages.Single(m=>m.Id==a.MessageId).RecipientId,a.ReceiverId,a.FinishedAt!.Value,false)).ToArray();
        w.Messaging.Receipts=w.Messaging.Receipts.Where(r=>!keys.Contains(r.MessageId)).Concat(received).OrderBy(r=>r.ReceivedAt).ThenBy(r=>r.MessageId,StringComparer.Ordinal).ToArray();
        w.Messaging.Knowledge=w.Messaging.Knowledge.Where(k=>!keys.Contains(k.MessageId)).Concat(received.Select(r=>new PersonalKnowledge(r.MessageId,r.ReceiverId,r.ReceivedAt,r.MessageId))).ToArray();
    }
    static void ValidateUnitTransport(ContractWorld w)
    {
        var dispatches=UnitDispatches(w);var receipts=UnitReceipts(w);var expected=dispatches.Select(d=>d.Message).Concat(receipts.Select(r=>r.ReplyMessage)).Concat((w.NewMilitary?.ReplyRecovery?.Flights??[]).Select(f=>f.Message)).Concat((w.NewMilitary?.CommandHolding?.Outcomes??[]).Where(f=>f.Received).Select(f=>f.ReplyMessage!)).Concat((w.NewMilitary?.Cancellation?.Notices??[]).Select(f=>f.Message)).ToHashSet();
        Need(w.Messaging.Messages.Where(UnitEnvelope).Select(m=>m.Id).ToHashSet().SetEquals(expected),"部隊宛の原本台帳が一致しません");
        Need(w.Messaging.Attempts.Where(a=>expected.Contains(a.MessageId)).All(a=>(a.LateDueAt==null||(w.NewMilitary?.HeldDelivery?.Transfers??[]).Any(t=>t.Attempt==a.Id&&t.DueAt==a.LateDueAt))&&(dispatches.Any(d=>d.Attempt==a.Id)||receipts.Any(r=>r.ReplyAttempt==a.Id&&a.Number==1)||(w.NewMilitary?.CommandHolding?.Outcomes??[]).Any(f=>f.ReplyAttempt==a.Id&&a.Number==1)||(w.NewMilitary?.Cancellation?.Notices??[]).Any(f=>f.Attempt==a.Id&&a.Number==1)||(w.NewMilitary?.ReplyRecovery?.Flights??[]).Any(f=>f.Attempt==a.Id))),"部隊宛の未登録の再送/遅着です");
        var copy=BeforeState(w,w.ElapsedSeconds,w.CommitSequence);
        Need(Same(UnitTransport(w),UnitTransport(copy)),"部隊宛の正式受領/試行/知識と業務台帳が違います");
    }
}
