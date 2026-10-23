using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum UnitReplyLeg { FailureReport, Retry }
public sealed record UnitReplyFlight(EntityId Id,EntityId Dispatch,UnitReplyLeg Kind,string Message,string Attempt,string Basis,long At,long Revision,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitReplyArrival(string Attempt,long At,long Revision,bool Delivered);
public sealed record UnitReplyHolding(string Message,string Attempt,EntityId Holder,FieldPoint Position,long At,long Revision);
public sealed record UnitReplyRecoveryState(string Contract,UnitReplyFlight[] Flights,UnitReplyArrival[] Arrivals,UnitReplyHolding[] Holdings);
public static partial class NewGameMilitary
{
    public const string UnitReplyRecoveryContract="declared-reply-policy-observed-failure-physical-return-same-original-v1";
    static bool ReplyPolicyAction(NewMilitaryAction a)=>a==NewMilitaryAction.RemoteRecall||UnitInquiryAction(a)||CancellationAction(a)||DirectiveAction(a)||DirectiveCancellationAction(a);
    static string? ReplyPolicy(ContractWorld w,TroopDispatch d){var m=w.Messaging.Messages.Single(m=>m.Id==d.Message);return d.Role==UnitDirectiveCancellationRole?DirectiveCancelNote(w,d).ReplyPolicy:d.Role==UnitDirectiveRole?ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(m.Body)).ReplyPolicy:d.Role==ReceivingRole?ContractJson.Read<UnitPostOrder>(System.Text.Encoding.UTF8.GetBytes(m.Body)).ReplyPolicy:d.Role==CancellationRole?CancellationNote(w,d).ReplyPolicy:ContractJson.Read<UnitPostInquiry>(System.Text.Encoding.UTF8.GetBytes(m.Body)).ReplyPolicy;}
    static void EnableReplyRecovery(ContractWorld w,NewMilitaryInput i){if(i.PostPolicy==UnitReplyRecoveryContract&&w.NewMilitary!.ReplyRecovery==null)w.NewMilitary=w.NewMilitary with{ReplyRecovery=new(UnitReplyRecoveryContract,[],[],[])};}
    static UnitReplyRecoveryState? ReplyRecoveryBefore(NewMilitaryState? s,long at,long rev){if(s?.ReplyRecovery is not {} x||!s.Requests.Any(q=>q.Input.PostPolicy==UnitReplyRecoveryContract&&Before(q.At,q.Revision,at,rev)))return null;return x with{Flights=x.Flights.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Arrivals=x.Arrivals.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Holdings=x.Holdings.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray()};}
    static EntityId ReplyCarrier(TroopDispatch d,int number)=>number==1?UnitCourier(d.Id):new("courier:unit-reply/"+Hash(d.Id.Value+"/"+number));
    static bool UnitPostPresent(ContractWorld w,FieldPoint p,EntityId? recipient=null){int index=Array.IndexOf(UnitIds,recipient??UnitIds[0]);return index>=0&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&Deployed(w)&&StrengthAt(w,index,w.ElapsedSeconds)>0&&RegionalGround.Distance(Position(w,index,w.ElapsedSeconds),p)<=1000;}
    static void BeforeReplyRecovery(ContractWorld w){foreach(var f in w.NewMilitary?.ReplyRecovery?.Flights.Where(f=>f.Kind==UnitReplyLeg.FailureReport&&f.DueAt==w.ElapsedSeconds)??[]){var a=w.Messaging.Attempts.Single(a=>a.Id==f.Attempt);if(a.Status==AttemptStatus.InTransit&&!UnitPostPresent(w,f.Destination,UnitDispatches(w).Single(d=>d.Id==f.Dispatch).Recipient))MessageFlow.Fail(w.Messaging,f.Message);}}
    static void AddReplyFlight(ContractWorld w,UnitReplyFlight f){var x=w.NewMilitary!.ReplyRecovery!;w.NewMilitary=w.NewMilitary with{ReplyRecovery=x with{Flights=[..x.Flights,f]}};}
    static void UpdateReplyRecovery(ContractWorld w)
    {
        if(w.NewMilitary?.ReplyRecovery==null)return;
        foreach(var f in w.NewMilitary.ReplyRecovery.Flights.Where(f=>f.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.ReplyRecovery.Arrivals.Any(a=>a.Attempt==f.Attempt)).ToArray()){
            Need(f.DueAt==w.ElapsedSeconds,"返書回復の到達境界を飛ばしました");var a=w.Messaging.Attempts.Single(a=>a.Id==f.Attempt);var x=w.NewMilitary.ReplyRecovery;
            var holdings=x.Holdings;
            if(f.Kind==UnitReplyLeg.FailureReport){
                holdings=[..holdings,new(w.Messaging.Attempts.Single(t=>t.Id==f.Basis).MessageId,f.Basis,f.Carrier,f.Destination,w.ElapsedSeconds,w.CommitSequence)];
                if(a.Status!=AttemptStatus.Delivered)holdings=[..holdings,new(f.Message,f.Attempt,f.Carrier,f.Destination,w.ElapsedSeconds,w.CommitSequence)];
            }
            w.NewMilitary=w.NewMilitary with{ReplyRecovery=x with{Arrivals=[..x.Arrivals,new(f.Attempt,w.ElapsedSeconds,w.CommitSequence,a.Status==AttemptStatus.Delivered)],Holdings=holdings}};
        }
        foreach(var r in UnitReceipts(w).Where(r=>r.Delivered).ToArray()){
            var d=UnitDispatches(w).Single(d=>d.Id==r.Id);if(ReplyPolicy(w,d)!=UnitReplyRecoveryContract)continue;
            foreach(var a in w.Messaging.Attempts.Where(a=>a.MessageId==r.ReplyMessage&&a.Status==AttemptStatus.Lost&&a.FailureAt==w.ElapsedSeconds).ToArray()){
                if(w.NewMilitary.ReplyRecovery.Flights.Any(f=>f.Kind==UnitReplyLeg.FailureReport&&f.Basis==a.Id))continue;
                var previous=w.NewMilitary.ReplyRecovery.Flights.SingleOrDefault(f=>f.Kind==UnitReplyLeg.Retry&&f.Attempt==a.Id);
                var from=previous?.Destination??r.ReplyDestination;var to=previous?.Origin??d.Destination;
                var path=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??0);var id=new EntityId("courier-leg:unit-post/"+Hash(a.Id+"/failure"));string message=UnitMessage(id,"reply-failure");
                MessageFlow.ReportFailure(w.Messaging,message,a.Id,ReplyCarrier(d,a.Number).Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
                AddReplyFlight(w,new(id,d.Id,UnitReplyLeg.FailureReport,message,MessageFlow.Latest(w.Messaging,message).Id,a.Id,w.ElapsedSeconds,w.CommitSequence,from,to,path,due,ReplyCarrier(d,a.Number)));
            }
            var latest=MessageFlow.Latest(w.Messaging,r.ReplyMessage);
            if(!KnownFailedAttempt(latest,UsesSenderEvidence(w,d))||latest.Number>=MessageFlow.MaxAttempts||!MessageFlow.FailureKnown(w.Messaging,latest.Id,d.Recipient.Value)||!SenderHasNoReceipt(w,d,r.ReplyMessage,d.Recipient.Value))continue;
            var report=w.NewMilitary.ReplyRecovery.Flights.LastOrDefault(f=>f.Kind==UnitReplyLeg.FailureReport&&f.Basis==latest.Id&&(w.NewMilitary.ReplyRecovery.Arrivals.Any(a=>a.Attempt==f.Attempt&&a.Delivered)||LateReportPoint(w,f.Message)!=null));
            if(report==null)continue;var sendingPoint=LateReportPoint(w,report.Message)??report.Destination;if(!UnitPostPresent(w,sendingPoint,d.Recipient))continue;
            var route=RegionalGround.Plan(sendingPoint,r.ReplyDestination,w.ElapsedSeconds,courier:true,atlas:true);long until=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);
            MessageFlow.Retry(w.Messaging,r.ReplyMessage,d.Recipient.Value,w.ElapsedSeconds,checked((int)(until-w.ElapsedSeconds)),UsesSenderEvidence(w,d));var attempt=MessageFlow.Latest(w.Messaging,r.ReplyMessage);var flightId=new EntityId("courier-leg:unit-post/"+Hash(attempt.Id+"/retry"));
            AddReplyFlight(w,new(flightId,d.Id,UnitReplyLeg.Retry,r.ReplyMessage,attempt.Id,report.Message,w.ElapsedSeconds,w.CommitSequence,sendingPoint,r.ReplyDestination,route,until,ReplyCarrier(d,attempt.Number)));
        }
    }
    static void ValidateReplyRecoveryShape(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.ReplyRecovery;Need((x!=null)==s.Requests.Any(q=>q.Input.PostPolicy==UnitReplyRecoveryContract),"返書配送方針と任意台帳が違います");if(x==null)return;
        Need(x.Contract==UnitReplyRecoveryContract&&x.Flights.Select(f=>f.Id).Distinct().Count()==x.Flights.Length&&x.Flights.Select(f=>f.Attempt).Distinct().Count()==x.Flights.Length&&x.Arrivals.Select(a=>a.Attempt).Distinct().Count()==x.Arrivals.Length,"返書回復の便/到達が重複しています");
        foreach(var f in x.Flights){
            Need(Enum.IsDefined(f.Kind)&&EntityId.IsValid(f.Id.Value)&&EntityId.IsValid(f.Dispatch.Value)&&EntityId.IsValid(f.Carrier.Value)&&!string.IsNullOrEmpty(f.Message)&&!string.IsNullOrEmpty(f.Attempt)&&!string.IsNullOrEmpty(f.Basis)&&f.Origin!=null&&f.Destination!=null&&f.Route!=null&&f.At>=0&&f.At<=w.ElapsedSeconds&&f.DueAt>f.At&&f.Revision>0&&f.Revision<=w.CommitSequence&&w.Messaging.Messages.Any(m=>m.Id==f.Message),"返書回復の便が不正です");
            var dispatches=UnitDispatches(w).Where(d=>d.Id==f.Dispatch).ToArray();Need(dispatches.Length==1,"返書回復の元発送が欠落/重複しています");
            Need(int.TryParse((f.Kind==UnitReplyLeg.FailureReport?f.Basis:f.Attempt).Split('/').Last(),out var number)&&number is >=1 and <=MessageFlow.MaxAttempts&&f.Carrier==ReplyCarrier(dispatches[0],number),"返書回復の運搬役が原試行と違います");
        }
        Need(x.Holdings.Select(h=>h.Attempt).Distinct().Count()==x.Holdings.Length&&x.Holdings.Length==x.Flights.Where(f=>f.Kind==UnitReplyLeg.FailureReport&&f.DueAt<=w.ElapsedSeconds).Sum(f=>x.Arrivals.SingleOrDefault(a=>a.Attempt==f.Attempt) is {} arrived?arrived.Delivered?1:2:0),"持ち帰った返書原本が欠落/重複しています");
        Need(x.Arrivals.Length==x.Flights.Count(f=>f.DueAt<=w.ElapsedSeconds),"返書回復の到達が欠落しています");
        foreach(var a in x.Arrivals)Need(x.Flights.Any(f=>f.Attempt==a.Attempt&&f.DueAt==a.At)&&a.At<=w.ElapsedSeconds&&a.Revision>0&&a.Revision<=w.CommitSequence,"返書回復の到達時点が不正です");
        foreach(var r in UnitReceipts(w).Where(r=>r.Delivered)){var d=UnitDispatches(w).Single(d=>d.Id==r.Id);if(ReplyPolicy(w,d)!=UnitReplyRecoveryContract)continue;foreach(var a in w.Messaging.Attempts.Where(a=>a.MessageId==r.ReplyMessage&&a.FailureAt!=null))Need(x.Flights.Count(f=>f.Kind==UnitReplyLeg.FailureReport&&f.Basis==a.Id)==1,"実際の返書不達からの報告便が欠落しています");}
    }
    static void ValidateReplyRecovery(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.ReplyRecovery;if(x==null||!replay)return;
        foreach(var group in x.Flights.Select(f=>(f.At,f.Revision)).Concat(x.Arrivals.Select(a=>(a.At,a.Revision))).Distinct()){
            var prior=BeforeState(w,group.At,group.Revision-1);prior.CommitSequence=group.Revision;BeforeUnitMessages(prior);MessageFlow.Advance(prior.Messaging,prior.ElapsedSeconds);UpdateRout(prior);UpdateCancellation(prior);UpdateWithdrawal(prior);UpdateCancellationHoldings(prior);UpdateCommandHolding(prior);UpdateHeldDelivery(prior);UpdateReplyRecovery(prior);
            Need(Same(prior.NewMilitary!.ReplyRecovery,ReplyRecoveryBefore(s,group.At,group.Revision)),"返書の不達持ち帰り/実受領後再送を再現できません");
            Need(Same(UnitTransport(prior),UnitTransport(BeforeState(w,group.At,group.Revision))),"返書原本/追加試行/正式受領が共通配送と違います");
        }
        var current=ContractJson.Copy(w);UpdateReplyRecovery(current);Need(Same(current.NewMilitary!.ReplyRecovery,x)&&Same(UnitTransport(current),UnitTransport(w)),"必要な返書回復の更新が欠落しています");
    }
}
