using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitDirectiveCancellationInput(string Contract,EntityId Original,KnownMilitaryTarget Known,long EffectiveAt);
public sealed record UnitDirectiveCancellationNote(EntityId Request,EntityId Original,string OriginalMessage,long EffectiveAt,string Contract,string? ReplyPolicy,string? TransportPolicy);
public sealed record UnitDirectiveCancelAnswer(EntityId Cancellation,string Original,long At,long ReceivedAt,long RequestedAt,long? EffectiveAt,UnitCancelDisposition Outcome);
public sealed record UnitDirectiveCancelEffect(EntityId Id,EntityId Original,long At,long Revision,UnitCancelDisposition Outcome,EntityId? StopOrder,FieldPoint Position);
public sealed record UnitDirectiveCancelNotice(EntityId Id,EntityId Cancellation,string Message,string Attempt,long At,long Revision,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitDirectiveCancellationState(string Contract,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies,UnitDirectiveCancelEffect[] Effects,UnitDirectiveCancelNotice[] Notices,UnitHeldOrderAck[] Arrivals,UnitReplyHolding[] Holdings);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveCancellationContract="explicit-scoped-original-physical-cancel-max-receipt-requested-time-v1";
    public const string UnitDirectiveCancellationRole="standing-unit-reception/cancel-scoped-original-v1";
    static bool DirectiveCancellationAction(NewMilitaryAction a)=>a==NewMilitaryAction.CancelUnitDirective;
    static UnitDirectiveCancellationNote DirectiveCancelNote(ContractWorld w,TroopDispatch d)=>ContractJson.Read<UnitDirectiveCancellationNote>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));
    public static bool UnitDirectiveCancellationAllowed(ContractWorld w,EntityId original)=>UnitDirectiveAllowed(w)&&DirectiveOriginal(w,original) is {} root&&SameMilitaryCase(w,root)&&SelectKnownTarget(w,root.Recipient)!=null;
    static void ValidateDirectiveCancellationInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveCancellationAction(i.Action)==(i.DirectiveCancellation!=null),"分隊原本取消の操作と本文が一致しません");if(i.DirectiveCancellation is not {} p)return;
        Need(p.Contract==UnitDirectiveCancellationContract&&p.Known!=null&&p.EffectiveAt>=w.ElapsedSeconds&&DirectiveOriginal(w,p.Original) is {} root&&root.Recipient==p.Known.Unit&&SelectKnownTarget(w,p.Known.Unit)==p.Known,"分隊原本取消の版・対象・既知宛先・効力時刻が不正です");
        Need(i.Amount==0&&i.A==null&&i.B==null&&i.Directive==null&&i.DirectiveRetry==null&&i.DirectiveHandoff==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.TargetOriginal==null&&i.RequestedEffectiveAt==null&&i.OriginalPolicy==null,"別の指示を分隊原本取消に混入できません");
    }
    static void ApplyDirectiveCancellation(ContractWorld w,NewMilitaryInput i)
    {
        if(!DirectiveCancellationAction(i.Action))return;var p=i.DirectiveCancellation!;var root=DirectiveOriginal(w,p.Original)!;var from=PersonPosition(w,w.ElapsedSeconds);var to=p.Known.Position;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);string message=UnitMessage(i.Id,"directive-cancel");
        MessageFlow.Send(w.Messaging,new(message,i.Id.Value,"unit-post-directive-cancel",w.PlayerControl.Value,root.Recipient.Value,Text(new UnitDirectiveCancellationNote(i.Id,p.Original,root.Message,p.EffectiveAt,UnitDirectiveCancellationContract,i.PostPolicy,i.TransportPolicy)),w.NewMilitary!.Terms.Site.Value,w.ElapsedSeconds,i.DeliverBy),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        var state=w.NewMilitary.UnitDirective!;var x=state.Cancellation??new(UnitDirectiveCancellationContract,[],[],[],[],[],[],[]);
        w.NewMilitary=w.NewMilitary with{UnitDirective=state with{Cancellation=x with{Dispatches=[..x.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,root.Recipient,UnitDirectiveCancellationRole,new("evidence:directive-cancel/"+Hash(Text(p))),from,to,route,due,message,MessageFlow.Latest(w.Messaging,message).Id)]}}};
    }
    static UnitDirectiveCancellationState? DirectiveCancellationBefore(UnitDirectiveCancellationState? x,long at,long rev)
    {
        if(x==null)return null;var ds=x.Dispatches.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray();return ds.Length==0?null:x with{Dispatches=ds,Receipts=x.Receipts.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray(),Replies=x.Replies.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray(),Effects=x.Effects.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray(),Notices=x.Notices.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray(),Arrivals=x.Arrivals.Where(a=>Before(a.At,a.Revision,at,rev)).ToArray(),Holdings=x.Holdings.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray()};
    }
    static bool DirectiveOriginalCancelled(ContractWorld w,EntityId original)=>w.NewMilitary?.UnitDirective?.Cancellation?.Effects.Any(e=>e.Original==original&&e.Outcome is UnitCancelDisposition.Suppressed or UnitCancelDisposition.Stopped or UnitCancelDisposition.AlreadyStopped)==true;
    static long? DirectiveCancelBoundary(ContractWorld w,long target)
    {
        var x=w.NewMilitary?.UnitDirective?.Cancellation;if(x==null)return null;
        return DirectiveCancellationOriginals(w).Where(d=>!x.Effects.Any(e=>e.Id==d.Id)&&w.Messaging.Receipts.Any(r=>r.MessageId==d.Message)).Select(d=>Math.Max(w.Messaging.Receipts.Single(r=>r.MessageId==d.Message).ReceivedAt,DirectiveCancelNote(w,d).EffectiveAt)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    static (UnitCancelDisposition Outcome,EntityId? Order) StopDirectiveOriginal(ContractWorld w,TroopDispatch cancellation,UnitDirectiveCancellationNote note)
    {
        var root=DirectiveOriginal(w,note.Original)!;int index=ReceivingUnitIndex(root);Need(index>=0,"取消対象の受信隊がありません");
        if(!SameMilitaryCase(w,root)||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence))return(UnitCancelDisposition.AuthorityEnded,null);
        if(DirectiveOriginalCancelled(w,root.Id))return(UnitCancelDisposition.AlreadyStopped,null);
        var order=w.NewMilitary!.Orders.SingleOrDefault(o=>o.Id==UnitOrder(root.Id));if(order==null)return(UnitCancelDisposition.Suppressed,null);
        if(EffectiveOrder(w,index,w.ElapsedSeconds)?.Id!=order.Id)return(UnitCancelDisposition.Superseded,null);
        if((order.Routes[index].LastOrDefault()?.EndAt??0)<=w.ElapsedSeconds)return(UnitCancelDisposition.TooLate,null);
        var origins=Enumerable.Range(0,3).Select(i=>Position(w,i,w.ElapsedSeconds)).ToArray();var routes=Enumerable.Range(0,3).Select(i=>i==index?[]:EffectiveOrder(w,i,w.ElapsedSeconds)?.Routes[i].ToArray()??[]).ToArray();var id=new EntityId("order:directive-cancel/"+Hash(cancellation.Id.Value));
        w.NewMilitary=w.NewMilitary with{Orders=[..w.NewMilitary.Orders,new(id,NewMilitaryAction.CancelUnitDirective,w.ElapsedSeconds,w.CommitSequence,origins,routes){AffectedUnits=[index]}]};return(UnitCancelDisposition.Stopped,id);
    }
    static UnitDirectiveCancelAnswer DirectiveCancelAnswer(ContractWorld w,TroopDispatch cancellation)
    {
        var note=DirectiveCancelNote(w,cancellation);var received=w.Messaging.Receipts.Single(r=>r.MessageId==cancellation.Message).ReceivedAt;var effect=w.NewMilitary!.UnitDirective!.Cancellation!.Effects.SingleOrDefault(e=>e.Id==note.Request);
        return new(note.Request,note.OriginalMessage,w.ElapsedSeconds,received,note.EffectiveAt,effect?.At,effect?.Outcome??UnitCancelDisposition.Waiting);
    }
    public static string UnitDirectiveCancellationStatus(ContractWorld w,EntityId original){var root=DirectiveCancellationOriginals(w).LastOrDefault(d=>DirectiveCancelNote(w,d).Original==original);return root==null?"":UnitDirectiveCancellationOriginalStatus(w,root.Id);}
    public static string UnitDirectiveCancellationOriginalStatus(ContractWorld w,EntityId cancellation)
    {
        var root=DirectiveCancellationOriginal(w,cancellation);if(root==null)return "";var x=w.NewMilitary!.UnitDirective!.Cancellation!;
        var known=x.Receipts.Where(r=>x.Dispatches.Single(d=>d.Id==r.Id).Message==root.Message&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value)).ToArray();
        var r=known.Where(r=>r.Delivered).OrderBy(r=>r.At).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).LastOrDefault();
        var n=x.Notices.Where(n=>n.Cancellation==root.Id&&MessageFlow.Knows(w.Messaging,n.Message,w.PlayerControl.Value)).OrderBy(n=>n.At).LastOrDefault();
        string? message=n!=null&&(r==null||n.At>=r.At)?n.Message:r?.ReplyMessage;
        if(message==null){var latest=x.Dispatches.Last(d=>d.Message==root.Message);if(latest.Id!=root.Id&&!known.Any(r=>r.Id==latest.Id))return "取消を再送：返書未受領";return known.Any(r=>!r.Delivered)?"取消の不達報告を受領：指定地点で正式受領できず":"取消を発送：返書未受領";}
        var answer=ContractJson.Read<UnitDirectiveCancelAnswer>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==message).Body));
        return "取消の返書（開始"+answer.At+"秒）："+(answer.Outcome switch{UnitCancelDisposition.Waiting=>"受令済み・指定した効力時刻を待つ",UnitCancelDisposition.Suppressed=>"未実行の対象原本を取消",UnitCancelDisposition.Stopped=>"対象原本による移動だけを停止",UnitCancelDisposition.AlreadyStopped=>"対象原本は取消済み",UnitCancelDisposition.TooLate=>"行動完了のため巻き戻さず",UnitCancelDisposition.Superseded=>"新しい命令を維持",_=>"指揮権終了のため変更せず"});
    }
    static void BeforeDirectiveCancelNotices(ContractWorld w)
    {
        foreach(var n in w.NewMilitary?.UnitDirective?.Cancellation?.Notices.Where(n=>n.DueAt==w.ElapsedSeconds)??[]){var a=w.Messaging.Attempts.Single(a=>a.Id==n.Attempt);if(a.Status==AttemptStatus.InTransit&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),n.Destination)>1000)MessageFlow.Fail(w.Messaging,n.Message);}
    }
    static void UpdateDirectiveCancellation(ContractWorld w)
    {
        if(w.NewMilitary?.UnitDirective?.Cancellation is not {} state)return;
        foreach(var n in state.Notices.Where(n=>n.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.UnitDirective.Cancellation!.Arrivals.Any(a=>a.Attempt==n.Attempt)).ToArray()){
            Need(n.DueAt==w.ElapsedSeconds,"分隊取消結果返書の到達を飛ばしました");var x=w.NewMilitary.UnitDirective.Cancellation!;bool received=w.Messaging.Attempts.Single(a=>a.Id==n.Attempt).Status==AttemptStatus.Delivered;
            w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=x with{Arrivals=[..x.Arrivals,new(n.Attempt,w.ElapsedSeconds,w.CommitSequence,received)]}}};
        }
        foreach(var d in DirectiveCancellationOriginals(w).Where(d=>!w.NewMilitary.UnitDirective.Cancellation!.Effects.Any(e=>e.Id==d.Id)&&w.Messaging.Receipts.Any(r=>r.MessageId==d.Message)).OrderBy(d=>d.Id.Value,StringComparer.Ordinal).ToArray()){
            var note=DirectiveCancelNote(w,d);long received=w.Messaging.Receipts.Single(r=>r.MessageId==d.Message).ReceivedAt;long effective=Math.Max(received,note.EffectiveAt);if(effective>w.ElapsedSeconds)continue;Need(effective==w.ElapsedSeconds,"分隊取消の効力境界を飛ばしました");
            var result=StopDirectiveOriginal(w,d,note);int index=ReceivingUnitIndex(d);var x=w.NewMilitary.UnitDirective.Cancellation!;
            w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=x with{Effects=[..x.Effects,new(d.Id,note.Original,w.ElapsedSeconds,w.CommitSequence,result.Outcome,result.Order,Position(w,index,w.ElapsedSeconds))]}}};
            if(effective<=received)continue;
            var origin=Position(w,index,w.ElapsedSeconds);var destination=DirectiveCancellationReturnOrigin(w,d);var route=RegionalGround.Plan(origin,destination,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);var id=new EntityId("notice:directive-cancel/"+Hash(d.Id.Value));string message=UnitMessage(id,"directive-cancel-effect");
            MessageFlow.Send(w.Messaging,new(message,d.Id.Value,"unit-post-directive-cancel-effect",d.Recipient.Value,w.PlayerControl.Value,Text(DirectiveCancelAnswer(w,d)),w.Messaging.Messages.Single(m=>m.Id==d.Message).Scope,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
            x=w.NewMilitary.UnitDirective.Cancellation!;w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=x with{Notices=[..x.Notices,new(id,d.Id,message,MessageFlow.Latest(w.Messaging,message).Id,w.ElapsedSeconds,w.CommitSequence,origin,destination,route,due,UnitCourier(id))]}}};
        }
        var current=w.NewMilitary.UnitDirective.Cancellation!;var papers=current.Arrivals.Where(a=>!a.Received).Select(a=>{var n=current.Notices.Single(n=>n.Attempt==a.Attempt);return new UnitReplyHolding(n.Message,n.Attempt,n.Carrier,n.Destination,a.At,a.Revision);}).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
        w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=current with{Holdings=papers}}};
    }
}
