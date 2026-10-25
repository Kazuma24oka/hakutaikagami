using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    static void BeforeCancellationNotices(ContractWorld w){foreach(var f in w.NewMilitary?.Cancellation?.Notices.Where(f=>f.DueAt==w.ElapsedSeconds)??[]){var a=w.Messaging.Attempts.Single(a=>a.Id==f.Attempt);if(a.Status==AttemptStatus.InTransit&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),f.Destination)>1000)MessageFlow.Fail(w.Messaging,f.Message);}}
    static void AddCancellationNotice(ContractWorld w,TroopDispatch root,EntityId reference,string phase,FieldPoint origin,FieldPoint destination,EntityId? carrier=null)
    {
        var x=w.NewMilitary!.Cancellation!;if(x.Notices.Any(f=>f.Reference==reference&&f.Phase==phase))return;
        var id=new EntityId("notice:unit-cancel/"+Hash(reference.Value+"/"+phase));var path=RegionalGround.Plan(origin,destination,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??0);string message=UnitMessage(id,"cancel-result");
        MessageFlow.Send(w.Messaging,new(message,root.Id.Value,"unit-post-cancel-result",UnitIdsFor(w)[0].Value,w.PlayerControl.Value,Text(CancellationAnswer(w,root)),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        w.NewMilitary=w.NewMilitary with{Cancellation=x with{Notices=[..x.Notices,new(id,root.Id,reference,phase,message,MessageFlow.Latest(w.Messaging,message).Id,w.ElapsedSeconds,w.CommitSequence,origin,destination,path,due,carrier??UnitCourier(id))]}};
    }
    static void UpdateCancellation(ContractWorld w)
    {
        UpdateDirectiveCancellation(w);if(w.NewMilitary?.Cancellation==null)return;
        foreach(var f in w.NewMilitary.Cancellation.Notices.Where(f=>f.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.Cancellation.NoticeArrivals.Any(a=>a.Id==f.Id)).ToArray()){
            Need(f.DueAt==w.ElapsedSeconds,"取消結果の実到達を飛ばしました");var x=w.NewMilitary.Cancellation;
            w.NewMilitary=w.NewMilitary with{Cancellation=x with{NoticeArrivals=[..x.NoticeArrivals,new(f.Id,w.ElapsedSeconds,w.CommitSequence,w.Messaging.Attempts.Single(a=>a.Id==f.Attempt).Status==AttemptStatus.Delivered)]}};
        }
        foreach(var root in w.NewMilitary.Cancellation.Dispatches.ToArray())foreach(var a in w.Messaging.Attempts.Where(a=>a.MessageId==root.Message&&a.Status==AttemptStatus.Delivered&&!w.NewMilitary.Cancellation.Receives.Any(r=>r.Attempt==a.Id)).OrderBy(a=>a.Id,StringComparer.Ordinal).ToArray()){
            Need(a.FinishedAt==w.ElapsedSeconds,"取消の実受令を飛ばしました");EntityId id,carrier;FieldPoint point,back;
            if(a.ArrivedLate){var t=w.NewMilitary.HeldDelivery!.Transfers.Single(t=>t.Attempt==a.Id);id=t.Id;carrier=t.Carrier;point=t.Destination;back=t.Origin;}
            else{var d=UnitDispatches(w).Single(d=>d.Attempt==a.Id);id=d.Id;carrier=UnitCourier(d.Id);point=d.Destination;back=d.Origin;}
            var x=w.NewMilitary.Cancellation;w.NewMilitary=w.NewMilitary with{Cancellation=x with{Receives=[..x.Receives,new(id,root.Id,a.Id,w.ElapsedSeconds,w.CommitSequence,a.ArrivedLate,point,back,carrier)]}};
        }
        var originalOrders=w.NewMilitary.Orders.ToArray();var pending=w.NewMilitary.Cancellation.Dispatches.Where(d=>w.NewMilitary.Cancellation.Receives.Any(r=>r.Cancellation==d.Id)&&!w.NewMilitary.Cancellation.Effects.Any(e=>e.Id==d.Id)).OrderBy(d=>d.Id.Value,StringComparer.Ordinal).ToArray();
        foreach(var root in pending){var note=CancellationNote(w,root);long received=w.Messaging.Receipts.Single(r=>r.MessageId==root.Message).ReceivedAt;long effective=Math.Max(received,note.RequestedAt);if(effective>w.ElapsedSeconds)continue;Need(effective==w.ElapsedSeconds,"取消効力の境界を飛ばしました");
            var target=w.NewMilitary.Withdrawal!.Dispatches.Single(d=>d.Id==note.Target);var order=originalOrders.SingleOrDefault(o=>o.Id==UnitOrder(target.Id));var points=Enumerable.Range(0,3).Select(i=>Position(w,i,w.ElapsedSeconds)).ToArray();EntityId? stop=null;UnitCancelDisposition outcome;
            if(!SameMilitaryCase(w,target)||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence))outcome=UnitCancelDisposition.AuthorityEnded;
            else if(w.NewMilitary.Cancellation.Effects.Any(e=>e.Original==note.Original&&e.At<w.ElapsedSeconds&&e.Outcome!=UnitCancelDisposition.AuthorityEnded))outcome=UnitCancelDisposition.AlreadyStopped;
            else if(order==null)outcome=UnitCancelDisposition.Suppressed;
            else if(w.NewMilitary.UnitDirective!=null)(outcome,stop)=StopScopedRecall(w,order,note.Original);
            else if(originalOrders.Last().Id!=order.Id)outcome=UnitCancelDisposition.Superseded;
            else if(order.Routes.All(r=>r.Length==0||r[^1].EndAt<=w.ElapsedSeconds))outcome=UnitCancelDisposition.TooLate;
            else{outcome=UnitCancelDisposition.Stopped;stop=new EntityId("order:unit-cancel/"+Hash(note.Original+"/"+w.ElapsedSeconds));if(!w.NewMilitary.Orders.Any(o=>o.Id==stop))Order(w,new(stop.Value,NewMilitaryAction.Hold));}
            var x=w.NewMilitary.Cancellation;w.NewMilitary=w.NewMilitary with{Cancellation=x with{Effects=[..x.Effects,new(root.Id,note.Original,w.ElapsedSeconds,w.CommitSequence,outcome,stop,points)]}};
        }
        foreach(var receive in w.NewMilitary.Cancellation.Receives.Where(r=>r.Late).ToArray()){
            var root=w.NewMilitary.Cancellation.Dispatches.Single(d=>d.Id==receive.Cancellation);AddCancellationNotice(w,root,receive.Id,"late-receipt",receive.Point,receive.ReturnTo,receive.Carrier);
        }
        foreach(var effect in w.NewMilitary.Cancellation.Effects.ToArray()){
            var receive=w.NewMilitary.Cancellation.Receives.Where(r=>r.Cancellation==effect.Id).OrderBy(r=>r.At).ThenBy(r=>r.Attempt,StringComparer.Ordinal).First();if(effect.At<=receive.At)continue;
            var root=w.NewMilitary.Cancellation.Dispatches.Single(d=>d.Id==effect.Id);AddCancellationNotice(w,root,root.Id,"effect",Position(w,0,w.ElapsedSeconds),receive.ReturnTo);
        }
    }
    static UnitReplyHolding[] ExpectedCancellationHoldings(ContractWorld w)
    {
        var x=w.NewMilitary!.Cancellation!;var rows=new List<UnitReplyHolding>();
        foreach(var d in UnitDispatches(w).Where(d=>d.Role==CancellationRole)){
            var r=UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id);var reply=r==null?null:UnitReplies(w).SingleOrDefault(a=>a.Id==d.Id);if(r==null||reply==null)continue;
            if(!r.Delivered){rows.Add(new(d.Message,d.Attempt,UnitCourier(d.Id),r.ReplyDestination,reply.At,reply.Revision));if(!reply.Received)rows.Add(new(r.ReplyMessage,r.ReplyAttempt,UnitCourier(d.Id),r.ReplyDestination,reply.At,reply.Revision));}
            else if(!reply.Received&&ReplyPolicy(w,d)!=UnitReplyRecoveryContract)rows.Add(new(r.ReplyMessage,r.ReplyAttempt,UnitCourier(d.Id),r.ReplyDestination,reply.At,reply.Revision));
        }
        foreach(var a in x.NoticeArrivals.Where(a=>!a.Received)){var f=x.Notices.Single(f=>f.Id==a.Id);rows.Add(new(f.Message,f.Attempt,f.Carrier,f.Destination,a.At,a.Revision));}
        return rows.OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
    }
    static void UpdateCancellationHoldings(ContractWorld w){if(w.NewMilitary?.Cancellation is {} x)w.NewMilitary=w.NewMilitary with{Cancellation=x with{Holdings=ExpectedCancellationHoldings(w)}};}
}
