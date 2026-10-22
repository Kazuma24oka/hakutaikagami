using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum UnitCancelDisposition { Waiting, Suppressed, Stopped, AlreadyStopped, TooLate, Superseded, AuthorityEnded }
public sealed record UnitPostCancellation(EntityId Request,EntityId Target,string Original,long RequestedAt,string Contract,string Role)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportPolicy {get;init;}
}
public sealed record UnitCancelAnswer(EntityId Cancellation,string Original,long At,long EffectiveAt,UnitCancelDisposition Outcome);
public sealed record UnitCancelReceive(EntityId Id,EntityId Cancellation,string Attempt,long At,long Revision,bool Late,FieldPoint Point,FieldPoint ReturnTo,EntityId Carrier);
public sealed record UnitCancelEffect(EntityId Id,string Original,long At,long Revision,UnitCancelDisposition Outcome,EntityId? StopOrder,FieldPoint[] Positions);
public sealed record UnitCancelNotice(EntityId Id,EntityId Cancellation,EntityId Reference,string Phase,string Message,string Attempt,long At,long Revision,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitCancelNoticeArrival(EntityId Id,long At,long Revision,bool Received);
public sealed record UnitCancellationState(string Contract,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies,UnitCancelReceive[] Receives,UnitCancelEffect[] Effects,UnitCancelNotice[] Notices,UnitCancelNoticeArrival[] NoticeArrivals,UnitReplyHolding[] Holdings);
public static partial class NewGameMilitary
{
    public const string UnitCancellationContract="targeted-recall-physical-cancellation-inclusive-receipt-effective-boundary-no-rollback-v1";
    public const string CancellationRole="standing-unit-reception/cancel-own-recall";
    public static bool CancellationAction(NewMilitaryAction a)=>a is NewMilitaryAction.CancelRecallKnown or NewMilitaryAction.CancelRecallHome;
    static bool CancelRetryAction(NewMilitaryAction a)=>a is NewMilitaryAction.RetryCancelKnown or NewMilitaryAction.RetryCancelHome;
    static bool RetryHomeAction(NewMilitaryAction a)=>a is NewMilitaryAction.RetryRecallHome or NewMilitaryAction.RetryCancelHome;
    public static EntityId? LatestRecallTarget(ContractWorld w)=>w.NewMilitary?.Withdrawal?.Dispatches.LastOrDefault()?.Id;
    public static bool CancellationAllowed(ContractWorld w,NewMilitaryAction action,EntityId? target=null)=>CancellationAction(action)&&Detached(w)&&Deployed(w)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&(target is {} id?w.NewMilitary!.Withdrawal!.Dispatches.Any(d=>d.Id==id):LatestRecallTarget(w)!=null);
    static UnitPostCancellation CancellationNote(ContractWorld w,TroopDispatch d){try{return ContractJson.Read<UnitPostCancellation>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));}catch(System.Text.Json.JsonException e){throw new InvalidDataException("取消原文が不正です",e);}}
    static void ApplyCancellation(ContractWorld w,NewMilitaryInput i)
    {
        if(!CancellationAction(i.Action))return;var original=w.NewMilitary!.Withdrawal!.Dispatches.Single(d=>d.Id==i.TargetOriginal);
        var sight=w.NewMilitary.Withdrawal.Sightings.Last();var from=PersonPosition(w,w.ElapsedSeconds);var to=i.Action==NewMilitaryAction.CancelRecallHome?Home(w,0):sight.Position;
        var path=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??0);string message=UnitMessage(i.Id,"cancel");
        var body=new UnitPostCancellation(i.Id,original.Id,original.Message,i.RequestedEffectiveAt??w.ElapsedSeconds,UnitCancellationContract,CancellationRole){ReplyPolicy=i.PostPolicy,TransportPolicy=i.TransportPolicy};
        MessageFlow.Send(w.Messaging,new(message,i.Id.Value,"unit-post-cancel",w.PlayerControl.Value,UnitIds[0].Value,Text(body),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds,i.DeliverBy),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        var x=w.NewMilitary.Cancellation??new(UnitCancellationContract,[],[],[],[],[],[],[],[]);w.NewMilitary=w.NewMilitary with{Cancellation=x with{Dispatches=[..x.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,UnitIds[0],CancellationRole,i.Action==NewMilitaryAction.CancelRecallHome?w.NewMilitary.Terms.Site:sight.Id,from,to,path,due,message,MessageFlow.Latest(w.Messaging,message).Id)]}};
    }
    static bool CancelledRecall(ContractWorld w,string original)=>w.NewMilitary?.Cancellation?.Effects.Any(e=>e.Original==original&&e.Outcome!=UnitCancelDisposition.AuthorityEnded)==true;
    static bool SupersedesRecall(ContractWorld w,NewMilitaryOrder order,TroopDispatch original)=>order.Revision>original.Revision&&!(w.NewMilitary?.Cancellation?.Effects.Any(e=>e.StopOrder==order.Id&&e.Original!=original.Message)??false);
    static UnitCancelAnswer CancellationAnswer(ContractWorld w,TroopDispatch d)
    {
        var note=CancellationNote(w,d);var effect=w.NewMilitary!.Cancellation!.Effects.SingleOrDefault(e=>e.Id==note.Request);
        var receipt=w.Messaging.Receipts.Single(r=>r.MessageId==d.Message);long effective=Math.Max(receipt.ReceivedAt,note.RequestedAt);
        return new(note.Request,note.Original,w.ElapsedSeconds,effect?.At??effective,effect?.Outcome??UnitCancelDisposition.Waiting);
    }
    static string UnitReplyBody(ContractWorld w,TroopDispatch d,bool applied)=>d.Role==ReceivingRole?Text(new UnitPostAnswer(d.Message,true,applied)):d.Role==CancellationRole?Text(CancellationAnswer(w,d)):Text(UnitQueryAnswer(w,d));
    static bool CancellationReceiptEvidence(ContractWorld w,string original,string sender){var x=w.NewMilitary?.Cancellation;if(x==null)return false;var known=UnitReceipts(w).Where(r=>r.Delivered&&UnitDispatches(w).Single(d=>d.Id==r.Id).Role==CancellationRole&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,sender)).Select(r=>w.Messaging.Messages.Single(m=>m.Id==r.ReplyMessage)).Concat(x.Notices.Where(f=>MessageFlow.Knows(w.Messaging,f.Message,sender)).Select(f=>w.Messaging.Messages.Single(m=>m.Id==f.Message)));foreach(var m in known){var a=ContractJson.Read<UnitCancelAnswer>(System.Text.Encoding.UTF8.GetBytes(m.Body));if(x.Dispatches.Single(d=>d.Id==a.Cancellation).Message==original)return true;if(a.Original==original&&a.Outcome is UnitCancelDisposition.Stopped or UnitCancelDisposition.TooLate or UnitCancelDisposition.Superseded)return true;}return false;}
    static UnitCancellationState? CancellationBefore(NewMilitaryState s,long at,long rev)=>s.Cancellation is not {} x||!s.Requests.Any(q=>CancellationAction(q.Input.Action)&&Before(q.At,q.Revision,at,rev))?null:x with{Dispatches=x.Dispatches.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Receipts=x.Receipts.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Replies=x.Replies.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Receives=x.Receives.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Effects=x.Effects.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Notices=x.Notices.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),NoticeArrivals=x.NoticeArrivals.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Holdings=x.Holdings.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray()};
    static long? CancellationBoundary(ContractWorld w,long target)=>(w.NewMilitary?.Cancellation?.Dispatches??[]).Where(d=>w.Messaging.Receipts.Any(r=>r.MessageId==d.Message)&&!w.NewMilitary!.Cancellation!.Effects.Any(e=>e.Id==d.Id)).Select(d=>Math.Max(w.Messaging.Receipts.Single(r=>r.MessageId==d.Message).ReceivedAt,CancellationNote(w,d).RequestedAt)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    public static string CancellationStatus(ContractWorld w)
    {
        var root=w.NewMilitary?.Cancellation?.Dispatches.LastOrDefault();if(root==null)return "";
        var known=UnitReceipts(w).Where(r=>UnitDispatches(w).Single(d=>d.Id==r.Id).Message==root.Message&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value)).Select(r=>w.Messaging.Messages.Single(m=>m.Id==r.ReplyMessage)).Concat((w.NewMilitary!.Cancellation!.Notices??[]).Where(f=>f.Cancellation==root.Id&&MessageFlow.Knows(w.Messaging,f.Message,w.PlayerControl.Value)).Select(f=>w.Messaging.Messages.Single(m=>m.Id==f.Message))).OrderBy(m=>m.CreatedAt).ThenBy(m=>m.Id,StringComparer.Ordinal).LastOrDefault();
        if(known==null)return " / 取消伝令の返書は未受領";if(known.Kind=="delivery-failure")return " / 取消伝令の不達報告を受領";
        var a=ContractJson.Read<UnitCancelAnswer>(System.Text.Encoding.UTF8.GetBytes(known.Body));return " / 取消返書（開始"+a.At+"秒時点）："+(a.Outcome switch{UnitCancelDisposition.Waiting=>"受令・効力待ち",UnitCancelDisposition.Suppressed=>"未着手の対象命令を抑止",UnitCancelDisposition.Stopped=>"対象の残りの移動を停止",UnitCancelDisposition.AlreadyStopped=>"対象は取消済み",UnitCancelDisposition.TooLate=>"対象は完了済み・巻き戻さず",UnitCancelDisposition.Superseded=>"新しい別命令を維持",_=>"指揮権終了・変更せず"});
    }
}
