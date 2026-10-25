using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record PersonalJourney(EntityId Id,long At,long Revision,FieldPoint Origin,FieldLeg[] Route);
public sealed record TroopSighting(EntityId Id,long At,long Revision,FieldPoint Position);
// A unit reception role is a post held by existing rank-and-file, not an invented character.
// It receives only a standing Recall directive, never another house's army or assets.
public sealed record TroopDispatch(EntityId Id,long At,long Revision,EntityId Recipient,string Role,EntityId AddressEvidence,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,string Message,string Attempt);
public sealed record TroopReceipt(EntityId Id,long At,long Revision,bool Delivered,bool Applied,FieldPoint ReplyDestination,FieldLeg[] ReplyRoute,long ReplyDueAt,string ReplyMessage,string ReplyAttempt)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AttemptStatus? ArrivalStatus {get;init;}
}
public sealed record TroopReply(EntityId Id,long At,long Revision,bool Received);
public sealed record PersonalWithdrawalState(string Contract,PersonalJourney[] Journeys,TroopSighting[] Sightings,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies);

public static partial class NewGameMilitary
{
    public const string WithdrawalContract="independent-person-unit-carrier-known-post-physical-recall-receipt-v1";
    static bool InExerciseRange(ContractWorld w,FieldPoint p){var a=Anchor(w);System.Numerics.BigInteger x=(System.Numerics.BigInteger)p.X-a.X,y=(System.Numerics.BigInteger)p.Y-a.Y;return x*x+y*y<=120000L*120000;}
    public const string ReceivingRole="standing-unit-reception/recall-only";
    static bool WithdrawalAction(NewMilitaryAction a)=>a is NewMilitaryAction.Handoff or NewMilitaryAction.Reclaim or NewMilitaryAction.Withdraw or NewMilitaryAction.Rejoin or NewMilitaryAction.Attach or NewMilitaryAction.Observe or NewMilitaryAction.RemoteRecall or NewMilitaryAction.PersonalHome or NewMilitaryAction.PersonalMove;
    public static bool DetachedAt(ContractWorld w,long at)=>CaseRequests(w,at).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Attach or NewMilitaryAction.Enter)?.Input.Action==NewMilitaryAction.Withdraw;
    public static bool Detached(ContractWorld w)=>DetachedAt(w,w.ElapsedSeconds);
    public static bool UnitCarries(ContractWorld w)=>CaseRequests(w).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Handoff or NewMilitaryAction.Reclaim)?.Input.Action==NewMilitaryAction.Handoff;
    public static FieldPoint PersonPosition(ContractWorld w,long at)
    {
        if(!DetachedAt(w,at))return Position(w,0,at);
        var j=w.NewMilitary!.Withdrawal!.Journeys.Last(j=>j.At<=at);
        return RegionalGround.At(j.Route,j.Origin,at);
    }
    public static bool PersonMoving(ContractWorld w)=>Detached(w)&&(w.NewMilitary!.Withdrawal!.Journeys.Last().Route.LastOrDefault()?.EndAt??0)>w.ElapsedSeconds;
    static bool Meeting(ContractWorld w)=>!PersonMoving(w)&&RegionalGround.Distance(PersonPosition(w,w.ElapsedSeconds),Position(w,0,w.ElapsedSeconds))<=1000;
    public static bool WithdrawalAllowed(ContractWorld w,NewMilitaryInput i)
    {
        if(!WithdrawalAction(i.Action)||w.NewMilitary==null||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)||!Deployed(w))return false;
        return i.Action switch{
            NewMilitaryAction.Handoff=>!UnitCarries(w)&&Meeting(w),
            NewMilitaryAction.Reclaim=>!Detached(w)&&UnitCarries(w)&&Meeting(w),
            NewMilitaryAction.Withdraw=>!Detached(w)&&(UnitCarries(w)||View(w).Carry==0),
            NewMilitaryAction.PersonalHome or NewMilitaryAction.PersonalMove=>Detached(w)&&!PersonMoving(w),
            NewMilitaryAction.Rejoin=>Detached(w)&&!PersonMoving(w)&&w.NewMilitary.Withdrawal!.Sightings.Length>0,
            NewMilitaryAction.Attach=>Detached(w)&&Meeting(w),
            NewMilitaryAction.Observe=>Meeting(w),
            NewMilitaryAction.RemoteRecall=>Detached(w)&&w.NewMilitary.Withdrawal!.Sightings.Length>0&&!w.NewMilitary.Withdrawal.Dispatches.Any(d=>SameMilitaryCase(w,d)&&!w.NewMilitary.Withdrawal.Replies.Any(a=>a.Id==d.Id)),
            _=>false};
    }
    static PersonalWithdrawalState EmptyWithdrawal()=>new(WithdrawalContract,[],[],[],[],[]);
    static void Sight(ContractWorld w,NewMilitaryInput i)
    {
        var x=w.NewMilitary!.Withdrawal!;
        w.NewMilitary=w.NewMilitary with{Withdrawal=x with{Sightings=[..x.Sightings,new(i.Id,w.ElapsedSeconds,w.CommitSequence,Position(w,0,w.ElapsedSeconds))]}};
    }
    static void ApplyWithdrawal(ContractWorld w,NewMilitaryInput i)
    {
        if(!WithdrawalAction(i.Action))return;
        w.NewMilitary=w.NewMilitary! with{Withdrawal=w.NewMilitary.Withdrawal??EmptyWithdrawal()};
        // Withdraw has already been appended: evaluate its origin before it made the person detached.
        if(i.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Rejoin or NewMilitaryAction.PersonalHome or NewMilitaryAction.PersonalMove){
            var x=w.NewMilitary.Withdrawal!;
            var from=i.Action==NewMilitaryAction.Withdraw?Position(w,0,w.ElapsedSeconds):RegionalGround.At(x.Journeys.Last().Route,x.Journeys.Last().Origin,w.ElapsedSeconds);
            var to=i.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.PersonalHome?Home(w,0):i.Action==NewMilitaryAction.PersonalMove?i.A!:x.Sightings.Last().Position;
            Need(InExerciseRange(w,to),"本人の野外移動はこの演習の120m範囲で指定します");
            var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,atlas:true);
            w.NewMilitary=w.NewMilitary with{Withdrawal=x with{Journeys=[..x.Journeys,new(i.Id,w.ElapsedSeconds,w.CommitSequence,from,route)]}};
        }
        if(i.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Observe or NewMilitaryAction.Attach)Sight(w,i);
        if(i.Action==NewMilitaryAction.RemoteRecall){
            var x=w.NewMilitary.Withdrawal!;var known=x.Sightings.Last();var from=PersonPosition(w,w.ElapsedSeconds);
            var route=RegionalGround.Plan(from,known.Position,w.ElapsedSeconds,courier:true,atlas:true);
            long due=Math.Max(checked(w.ElapsedSeconds+1),route.LastOrDefault()?.EndAt??0);var message=UnitMessage(i.Id,"order");
            MessageFlow.Send(w.Messaging,new(message,i.Id.Value,"unit-post-order",w.PlayerControl.Value,UnitIds[0].Value,Text(new UnitPostOrder(i.Id,"recall",ReceivingRole,known.Id){ReplyPolicy=i.PostPolicy,HoldingPolicy=i.OriginalPolicy,TransportPolicy=i.TransportPolicy}),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds,i.DeliverBy),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
            w.NewMilitary=w.NewMilitary with{Withdrawal=x with{Dispatches=[..x.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,UnitIds[0],ReceivingRole,known.Id,from,known.Position,route,due,message,MessageFlow.Latest(w.Messaging,message).Id)]}};
        }
    }
    static void UpdateWithdrawal(ContractWorld w)
    {
        if(w.NewMilitary?.Withdrawal==null)return;
        foreach(var d in UnitDispatches(w).Where(d=>d.DueAt<=w.ElapsedSeconds&&!UnitReceipts(w).Any(r=>r.Id==d.Id)).ToArray()){
            Need(d.DueAt==w.ElapsedSeconds,"部隊伝令の到着時点を飛ばしました");
            bool delivered=w.Messaging.Attempts.Single(a=>a.Id==d.Attempt).Status==AttemptStatus.Delivered;
            // Recipient-local truth is tested only on arrival, never used to choose the address.
            var original=d.Role==ReceivingRole?w.NewMilitary!.Withdrawal!.Dispatches.Single(t=>t.Message==d.Message):null;
            bool applied=original!=null&&SameMilitaryCase(w,original)&&delivered&&!CancelledRecall(w,d.Message)&&!UnitReceipts(w).Any(r=>r.Applied&&UnitDispatches(w).Single(t=>t.Id==r.Id).Message==d.Message)&&!w.NewMilitary.Orders.Any(o=>SupersedesRecall(w,o,original));
            if(applied)applied=ApplyStandingRecall(w,original!);
            if(delivered&&d.Role==UnitDirectiveRole)applied=ApplyDirectedArrival(w,d);
            var route=RegionalGround.Plan(d.Destination,d.Origin,w.ElapsedSeconds,courier:true,atlas:true);var x=w.NewMilitary!.Withdrawal!;
            long due=Math.Max(checked(w.ElapsedSeconds+1),route.LastOrDefault()?.EndAt??0);var reply=UnitMessage(d.Id,delivered?"reply":"failure");
            if(delivered)MessageFlow.Send(w.Messaging,new(reply,d.Id.Value,d.Role==UnitDirectiveCancellationRole?"unit-post-directive-cancel-reply":d.Role==UnitDirectiveRole?"unit-post-directive-reply":d.Role==ReceivingRole?"unit-post-reply":d.Role==CancellationRole?"unit-post-cancel-reply":"unit-post-inquiry-reply",d.Recipient.Value,w.PlayerControl.Value,UnitReplyBody(w,d,applied),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
            else MessageFlow.ReportFailure(w.Messaging,reply,d.Attempt,UnitCourier(d.Id).Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
            RecordUnitReceipt(w,new(d.Id,w.ElapsedSeconds,w.CommitSequence,delivered,applied,d.Origin,route,due,reply,MessageFlow.Latest(w.Messaging,reply).Id){ArrivalStatus=w.Messaging.Messages.Single(m=>m.Id==d.Message).DeliverBy!=null?w.Messaging.Attempts.Single(a=>a.Id==d.Attempt).Status:null});
        }
        foreach(var r in UnitReceipts(w).Where(r=>r.ReplyDueAt<=w.ElapsedSeconds&&!UnitReplies(w).Any(a=>a.Id==r.Id)).ToArray()){
            Need(r.ReplyDueAt==w.ElapsedSeconds,"部隊伝令の返書到着を飛ばしました");var x=w.NewMilitary.Withdrawal;
            bool received=MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value);
            RecordUnitReply(w,new(r.Id,w.ElapsedSeconds,w.CommitSequence,received));
        }
    }
    public static string PersonalStatus(ContractWorld w)
    {
        if(w.NewMilitary?.Withdrawal is not {} x)return "本人・本隊は同行中";
        var d=UnitDispatches(w).LastOrDefault(d=>d.Role==ReceivingRole&&SameMilitaryCase(w,d));var reply=d==null?null:UnitReplies(w).LastOrDefault(r=>r.Id==d.Id);var response=d==null?null:UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id);bool received=response!=null&&MessageFlow.Knows(w.Messaging,response.ReplyMessage,w.PlayerControl.Value);
        var late=d==null?null:w.NewMilitary?.CommandHolding?.Outcomes.LastOrDefault(f=>f.Original==d.Message&&f.ReplyMessage!=null&&MessageFlow.Knows(w.Messaging,f.ReplyMessage,w.PlayerControl.Value));
        string mail=late!=null?late.Applied?" / 遅着原本の受令・実行返書を受領":" / 遅着原本を受令・重複/旧命令は実行せず":d==null?"":received?UnitReceipts(w).Single(r=>r.Id==d.Id).Delivered?UnitReceipts(w).Single(r=>r.Id==d.Id).Applied?" / 撤退命令の受令返書を受領":" / 新しい現地命令を維持・旧撤退命令は実行せず":" / 撤退命令の不達報告を受領":" / 撤退伝令の返書は未受領";
        var deadline=d==null?null:w.Messaging.Messages.Single(m=>m.Id==d.Message).DeliverBy;if(deadline!=null)mail+=" / 撤退命令の受領期限：開始から"+deadline+"秒";
        return (Detached(w)?PersonMoving(w)?"本人だけ移動中":"本人は部隊と別行動":"本人・本隊は合流済み")+(UnitCarries(w)?" / 兵糧は本隊が運搬":" / 兵糧は本人が運搬")+mail;
    }
    public static LocationState CargoLocation(ContractWorld w)
    {
        var p=Position(w,0,w.ElapsedSeconds);var point=FieldWorld.ToWorld(p);var g=HakutaiKagami.Core.World.JapanGeography.Inverse(new(point.EastMm,point.NorthMm));
        return new(UnitIds[0],LocationKind.AtPosition,null,null,0,0,g.LatitudeE7,g.LongitudeE7,ContinuousWorld.Height(p),WithdrawalContract,0);
    }
    static PersonalWithdrawalState? WithdrawalBefore(PersonalWithdrawalState? x,long at,long rev)=>x==null?null:new(x.Contract,x.Journeys.Where(j=>Before(j.At,j.Revision,at,rev)).ToArray(),x.Sightings.Where(j=>Before(j.At,j.Revision,at,rev)).ToArray(),x.Dispatches.Where(j=>Before(j.At,j.Revision,at,rev)).ToArray(),x.Receipts.Where(j=>Before(j.At,j.Revision,at,rev)).ToArray(),x.Replies.Where(j=>Before(j.At,j.Revision,at,rev)).ToArray());
    static void ValidateWithdrawal(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.Withdrawal;
        Need((x!=null)==s.Requests.Any(q=>WithdrawalAction(q.Input.Action)),"本人退避の原判断と任意台帳が違います");if(x==null)return;
        Need(x.Contract==WithdrawalContract,"本人退避の契約が違います");
        Need(x.Journeys.Length==s.Requests.Count(q=>q.Input.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Rejoin or NewMilitaryAction.PersonalHome or NewMilitaryAction.PersonalMove)&&x.Sightings.Length==s.Requests.Count(q=>q.Input.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Observe or NewMilitaryAction.Attach)&&x.Dispatches.Length==s.Requests.Count(q=>q.Input.Action==NewMilitaryAction.RemoteRecall),"退避/観測/伝令の台帳が欠落しています");
        Need(x.Receipts.Select(r=>r.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(r=>r.Id).Distinct().Count()==x.Replies.Length,"部隊の受令/返書が重複しています");
        Need(x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds),"部隊の受令/返書の更新が不足しています");
        ValidateUnitTransport(w);
        if(!replay)return;
        foreach(var q in s.Requests.Where(q=>WithdrawalAction(q.Input.Action)||UnitRetryAction(q.Input.Action)||UnitInquiryAction(q.Input.Action)||HeldAction(q.Input.Action)||DirectiveAction(q.Input.Action)||DirectiveRetryAction(q.Input.Action))){
            var prior=BeforeState(w,q.At,q.Revision-1);prior.CommitSequence=q.Revision;Dispatch(prior,q.Input);
            if(q.Input.Action==NewMilitaryAction.RemoteRecall||UnitInquiryAction(q.Input.Action)||DirectiveAction(q.Input.Action)||DirectiveRetryAction(q.Input.Action)||DirectiveCancellationAction(q.Input.Action)||DirectiveCancelRetryAction(q.Input.Action)){var d=UnitDispatches(prior).Last();Need(Same(w.Messaging.Messages.Single(m=>m.Id==d.Message),prior.Messaging.Messages.Single(m=>m.Id==d.Message)),"部隊宛原本が改ざんされています");}
            Need(Same(prior.NewMilitary!.UnitDirective,DirectiveBefore(s.UnitDirective,q.At,q.Revision))&&Same(prior.NewMilitary!.HeldDelivery,HeldBefore(s,q.At,q.Revision))&&Same(prior.NewMilitary!.ReplyRecovery,ReplyRecoveryBefore(s,q.At,q.Revision))&&Same(prior.NewMilitary!.UnitInquiry,UnitInquiryBefore(s.UnitInquiry,q.At,q.Revision))&&Same(prior.NewMilitary!.UnitRetry,UnitRetryBefore(s.UnitRetry,q.At,q.Revision))&&Same(prior.NewMilitary!.Withdrawal,WithdrawalBefore(x,q.At,q.Revision)),"本人の経路・既知宛先・現地受渡しを原判断から再現できません");
        }
        foreach(var group in UnitReceipts(w).Select(r=>(r.At,r.Revision)).Concat(UnitReplies(w).Select(r=>(r.At,r.Revision))).Distinct()){
            Need(group.At<=w.ElapsedSeconds&&group.Revision<=w.CommitSequence&&group.Revision>0,"伝令の時点が不正です");
            var prior=BeforeState(w,group.At,group.Revision-1);prior.CommitSequence=group.Revision;BeforeUnitMessages(prior);MessageFlow.Advance(prior.Messaging,prior.ElapsedSeconds);UpdateRout(prior);UpdateCancellation(prior);UpdateWithdrawal(prior);UpdateCancellationHoldings(prior);UpdateCommandHolding(prior);UpdateHeldDelivery(prior);UpdateReplyRecovery(prior);
            var expectedState=BeforeState(w,group.At,group.Revision);Need(Same(UnitTransport(prior),UnitTransport(expectedState)),"部隊の返書原本・共通配送を再現できません");
            Need(Same(prior.NewMilitary!.UnitDirective,DirectiveBefore(s.UnitDirective,group.At,group.Revision))&&Same(prior.NewMilitary!.HeldDelivery,HeldBefore(s,group.At,group.Revision))&&Same(prior.NewMilitary!.ReplyRecovery,ReplyRecoveryBefore(s,group.At,group.Revision))&&Same(prior.NewMilitary!.UnitInquiry,UnitInquiryBefore(s.UnitInquiry,group.At,group.Revision))&&Same(prior.NewMilitary!.UnitRetry,UnitRetryBefore(s.UnitRetry,group.At,group.Revision))&&Same(prior.NewMilitary!.Withdrawal,WithdrawalBefore(x,group.At,group.Revision)),"伝令の実到達/返書を原経路から再現できません");
            Need(Same(prior.NewMilitary.Orders,w.NewMilitary!.Orders.Where(o=>Before(o.At,o.Revision,group.At,group.Revision)).ToArray()),"届いた撤退命令と部隊経路が違います");
        }
    }
}
