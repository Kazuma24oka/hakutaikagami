using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitDirectiveCancelHandoffInput(string Contract,EntityId Cancellation,string Attempt,EntityId Carrier,KnownMilitaryTarget Known);
public sealed record UnitDirectiveCancelLateReceipt(EntityId Transfer,EntityId Cancellation,long At,long Revision,bool Received,string? ReplyMessage,string? ReplyAttempt,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitDirectiveCancelHoldingState(string Contract,long At,long Revision,UnitReplyHolding[] Holdings,UnitDirectiveCancelLateReceipt[] Outcomes,UnitHeldOrderAck[] Acks);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveCancelHoldingContract="declared-retained-scoped-cancellation-original-fixed-handoff-once-v1";
    static bool DirectiveCancelHandoffAction(NewMilitaryAction a)=>a==NewMilitaryAction.DeliverHeldUnitDirectiveCancellation;
    static bool HoldsDirectiveCancellation(ContractWorld w,TroopDispatch d)=>d.Role==UnitDirectiveCancellationRole&&DirectiveCancelNote(w,d).HoldingPolicy==UnitDirectiveCancelHoldingContract;
    static UnitDirectiveCancelHoldingState? DirectiveCancelHoldingBefore(UnitDirectiveCancelHoldingState? x,long at,long rev)=>x==null||!Before(x.At,x.Revision,at,rev)?null:x with{Holdings=x.Holdings.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray(),Outcomes=x.Outcomes.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Acks=x.Acks.Where(a=>Before(a.At,a.Revision,at,rev)).ToArray()};
    static UnitDirectiveCancelLateReceipt[] DirectiveCancelAckFlights(ContractWorld w)=>w.NewMilitary?.UnitDirective?.Cancellation?.Holding?.Outcomes.Where(f=>f.Received).ToArray()??[];
    static UnitReplyHolding[] ExpectedDirectiveCancelPapers(ContractWorld w)=>ReturnedOriginalPapers(w,d=>HoldsDirectiveCancellation(w,d)).Concat((w.NewMilitary?.UnitDirective?.Cancellation?.Holding?.Acks??[]).Where(a=>!a.Received).Select(a=>{var f=DirectiveCancelAckFlights(w).Single(f=>f.ReplyAttempt==a.Attempt);return new UnitReplyHolding(f.ReplyMessage!,a.Attempt,f.Carrier,f.Destination,a.At,a.Revision);})).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
    static void UpdateDirectiveCancelHolding(ContractWorld w)
    {
        if(w.NewMilitary?.UnitDirective?.Cancellation?.Holding==null)return;
        foreach(var f in DirectiveCancelAckFlights(w).Where(f=>f.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.UnitDirective.Cancellation.Holding.Acks.Any(a=>a.Attempt==f.ReplyAttempt)).ToArray()){
            Need(f.DueAt==w.ElapsedSeconds,"取消原本の遅着返書到達を飛ばしました");var x=w.NewMilitary.UnitDirective.Cancellation.Holding;bool received=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt).Status==AttemptStatus.Delivered;
            w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=w.NewMilitary.UnitDirective.Cancellation with{Holding=x with{Acks=[..x.Acks,new(f.ReplyAttempt!,w.ElapsedSeconds,w.CommitSequence,received)]}}}};
        }
        var current=w.NewMilitary.UnitDirective.Cancellation.Holding!;w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=w.NewMilitary.UnitDirective.Cancellation with{Holding=current with{Holdings=ExpectedDirectiveCancelPapers(w)}}}};
    }
    static void BeforeDirectiveCancelLateAcks(ContractWorld w)
    {
        foreach(var f in DirectiveCancelAckFlights(w).Where(f=>f.DueAt==w.ElapsedSeconds)){var a=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt);if(a.Status==AttemptStatus.InTransit&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),f.Destination)>1000)MessageFlow.Fail(w.Messaging,f.ReplyMessage!);}
    }
    static UnitReplyHolding? PickDirectiveCancelPaper(ContractWorld w,UnitDirectiveCancelHandoffInput p)
    {
        if(w.NewMilitary?.UnitDirective?.Cancellation?.Holding==null||!RecognitionEnabled(w)||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)||!Deployed(w)||DirectiveCancellationOriginal(w,p.Cancellation) is not {} root||!SameMilitaryCase(w,root)||root.Recipient!=p.Known.Unit||SelectKnownTarget(w,p.Known.Unit)!=p.Known)return null;
        var person=PhysicalPerson(w,w.ElapsedSeconds);if(person!=PhysicalPerson(w,w.ElapsedSeconds+1))return null;
        var h=HeldPapers(w,w.ElapsedSeconds).SingleOrDefault(h=>h.Message==root.Message&&h.Attempt==p.Attempt&&h.Holder==p.Carrier);if(h==null||!DirectiveCarrierIdle(w,h.Holder)||RegionalGround.Distance(person,h.Position)>1000)return null;
        var d=UnitDispatches(w).Single(d=>d.Attempt==h.Attempt);var a=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);var m=w.Messaging.Messages.Single(m=>m.Id==h.Message);
        if(!HoldsDirectiveCancellation(w,d)||a.Status!=AttemptStatus.Lost||a.LateDueAt!=null)return null;
        try{var route=RegionalGround.Plan(h.Position,p.Known.Position,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);return m.DeliverBy==null||due<=m.DeliverBy?h:null;}catch(InvalidDataException){return null;}
    }
    public static UnitDirectiveCancelHandoffInput? SelectDirectiveCancellationPaper(ContractWorld w,EntityId cancellation)
    {
        if(DirectiveCancellationOriginal(w,cancellation) is not {} root||SelectKnownTarget(w,root.Recipient) is not {} known)return null;
        foreach(var h in HeldPapers(w,w.ElapsedSeconds).Where(h=>h.Message==root.Message).OrderBy(h=>h.At).ThenBy(h=>h.Attempt,StringComparer.Ordinal)){var p=new UnitDirectiveCancelHandoffInput(UnitDirectiveCancelHoldingContract,cancellation,h.Attempt,h.Holder,known);if(PickDirectiveCancelPaper(w,p)!=null)return p;}return null;
    }
    public static bool DirectiveCancellationHandoffAllowed(ContractWorld w,UnitDirectiveCancelHandoffInput? p)=>p!=null&&p.Contract==UnitDirectiveCancelHoldingContract&&p.Known!=null&&PickDirectiveCancelPaper(w,p)!=null;
    static void ValidateDirectiveCancelHandoffInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveCancelHandoffAction(i.Action)==(i.DirectiveCancelHandoff!=null),"取消原本受渡しの操作と実物参照が一致しません");if(i.DirectiveCancelHandoff is not {} p)return;
        Need(p.Contract==UnitDirectiveCancelHoldingContract&&EntityId.IsValid(p.Cancellation.Value)&&EntityId.IsValid(p.Carrier.Value)&&!string.IsNullOrWhiteSpace(p.Attempt)&&p.Known!=null,"取消原本の版・保持者・根拠が不正です");
        Need(i.Amount==0&&i.A==null&&i.B==null&&i.Directive==null&&i.DirectiveRetry==null&&i.DirectiveHandoff==null&&i.DirectiveCancellation==null&&i.DirectiveCancelRetry==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.TargetOriginal==null&&i.PostPolicy==null&&i.OriginalPolicy==null&&i.TransportPolicy==null&&i.DeliverBy==null&&i.RequestedEffectiveAt==null,"取消原本の受渡しで本文・指定時刻・期限・方針を変更できません");
    }
    static void ApplyDirectiveCancelHandoff(ContractWorld w,NewMilitaryInput i)
    {
        var p=i.DirectiveCancelHandoff!;var h=PickDirectiveCancelPaper(w,p)!;var to=p.Known.Position;var cargo=HeldPapers(w,w.ElapsedSeconds).Where(x=>x.Holder==h.Holder).Select(x=>x.Attempt).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var route=RegionalGround.Plan(h.Position,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);var attempt=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);MessageFlow.ScheduleLate(w.Messaging,h.Message,attempt.Number,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));var x=w.NewMilitary!.HeldDelivery??new(UnitHeldContract,[],[]);
        w.NewMilitary=w.NewMilitary with{HeldDelivery=x with{Transfers=[..x.Transfers,new(i.Id,w.ElapsedSeconds,w.CommitSequence,i.Action,h.Message,h.Attempt,h.Holder,cargo,h.Position,to,route,due)]}};
    }
    static void HeldDirectiveCancellationOutcome(ContractWorld w,UnitHeldTransfer t,bool received)
    {
        if(w.Messaging.Messages.Single(m=>m.Id==t.Message).Kind!="unit-post-directive-cancel")return;
        var d=UnitDispatches(w).Single(d=>d.Attempt==t.Attempt);Need(HoldsDirectiveCancellation(w,d)&&w.NewMilitary!.UnitDirective!.Cancellation!.Holding!=null,"保持していない取消原本を遅着させました");var note=DirectiveCancelNote(w,d);string? reply=null,attempt=null;FieldLeg[] route=[];long due=0;
        if(received){UpdateDirectiveCancellation(w);route=RegionalGround.Plan(t.Destination,t.Origin,w.ElapsedSeconds,courier:true,atlas:true);due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);reply=UnitMessage(t.Id,"directive-cancel-late-reply");MessageFlow.Send(w.Messaging,new(reply,note.Request.Value,"unit-post-directive-cancel-late-reply",d.Recipient.Value,w.PlayerControl.Value,Text(DirectiveCancelAnswer(w,d)),w.Messaging.Messages.Single(m=>m.Id==d.Message).Scope,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));attempt=MessageFlow.Latest(w.Messaging,reply).Id;}
        var x=w.NewMilitary!.UnitDirective!.Cancellation!.Holding!;w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Cancellation=w.NewMilitary.UnitDirective.Cancellation with{Holding=x with{Outcomes=[..x.Outcomes,new(t.Id,note.Request,w.ElapsedSeconds,w.CommitSequence,received,reply,attempt,t.Destination,t.Origin,route,due,t.Carrier)]}}}};
    }
    static UnitDirectiveCancelLateReceipt? KnownDirectiveCancellationAck(ContractWorld w,string original,string sender)=>DirectiveCancelAckFlights(w).Where(f=>DirectiveCancellationOriginal(w,f.Cancellation)?.Message==original&&MessageFlow.Knows(w.Messaging,f.ReplyMessage!,sender)).OrderBy(f=>f.At).ThenBy(f=>f.Transfer.Value,StringComparer.Ordinal).LastOrDefault();
    static void ValidateDirectiveCancelHolding(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.UnitDirective?.Cancellation?.Holding;var first=s.Requests.FirstOrDefault(q=>q.Input.OriginalPolicy==UnitDirectiveCancelHoldingContract);Need((x!=null)==(first!=null),"取消原本の保持方針と実物台帳が違います");if(x==null)return;
        Need(x.Contract==UnitDirectiveCancelHoldingContract&&x.At==first!.At&&x.Revision==first.Revision&&x.Holdings!=null&&x.Outcomes!=null&&x.Acks!=null&&Same(x.Holdings,ExpectedDirectiveCancelPapers(w)),"取消原本の保持者・所在・返書が欠落または改ざんされています");
        var transfers=(s.HeldDelivery?.Transfers??[]).Where(t=>t.DueAt<=w.ElapsedSeconds&&w.Messaging.Messages.Single(m=>m.Id==t.Message).Kind=="unit-post-directive-cancel").ToArray();Need(x.Outcomes.Length==transfers.Length&&x.Outcomes.Select(f=>f.Transfer).Distinct().Count()==x.Outcomes.Length&&x.Acks.Select(a=>a.Attempt).Distinct().Count()==x.Acks.Length,"取消原本の遅着・返書が欠落または重複しています");
        foreach(var f in x.Outcomes)Need(transfers.Any(t=>t.Id==f.Transfer&&t.DueAt==f.At&&DirectiveCancelNote(w,UnitDispatches(w).Single(d=>d.Attempt==t.Attempt)).Request==f.Cancellation)&&f.Revision>0&&f.Revision<=w.CommitSequence&&(f.Received?f.ReplyMessage!=null&&f.ReplyAttempt!=null&&f.DueAt>f.At:f.ReplyMessage==null&&f.ReplyAttempt==null&&f.DueAt==0&&f.Route.Length==0),"取消原本の遅着返書が不正です");
        Need(x.Acks.Length==x.Outcomes.Count(f=>f.Received&&f.DueAt<=w.ElapsedSeconds),"取消遅着返書の到達が欠落しています");foreach(var a in x.Acks)Need(x.Outcomes.Any(f=>f.ReplyAttempt==a.Attempt&&f.DueAt==a.At)&&a.Revision>0&&a.Revision<=w.CommitSequence,"取消遅着返書の時点が不正です");
        if(!replay)return;
        foreach(var g in x.Outcomes.Select(f=>(f.At,f.Revision)).Concat(x.Acks.Select(a=>(a.At,a.Revision))).Distinct()){
            var p=BeforeState(w,g.At,g.Revision-1);p.CommitSequence=g.Revision;BeforeUnitMessages(p);MessageFlow.Advance(p.Messaging,p.ElapsedSeconds);UpdateRout(p);UpdateCancellation(p);UpdateWithdrawal(p);UpdateCancellationHoldings(p);UpdateCommandHolding(p);UpdateHeldDelivery(p);UpdateReplyRecovery(p);var expected=BeforeState(w,g.At,g.Revision);
            Need(Same(p.NewMilitary!.UnitDirective!.Cancellation,expected.NewMilitary!.UnitDirective!.Cancellation)&&Same(UnitTransport(p),UnitTransport(expected))&&Same(p.NewMilitary.Orders,expected.NewMilitary.Orders),"取消原本の実物遅着・一度だけの効力・返書を再現できません");
        }
    }
}
