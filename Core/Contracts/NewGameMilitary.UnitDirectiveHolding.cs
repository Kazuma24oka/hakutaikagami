using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitDirectiveHandoffInput(string Contract,EntityId Original,string Attempt,EntityId Carrier,KnownMilitaryTarget Known);
public sealed record UnitDirectiveLateOutcome(EntityId Transfer,EntityId Original,long At,long Revision,bool Received,UnitDirectiveOutcome? Outcome,EntityId? Order,string? ReplyMessage,string? ReplyAttempt,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitDirectiveHoldingState(string Contract,long At,long Revision,UnitReplyHolding[] Holdings,UnitDirectiveLateOutcome[] Outcomes,UnitHeldOrderAck[] Acks);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveHoldingContract="declared-retained-directive-original-physical-fixed-handover-once-v1";
    static bool DirectiveHandoffAction(NewMilitaryAction a)=>a==NewMilitaryAction.DeliverHeldUnitDirective;
    static bool HoldsDirectiveOriginal(ContractWorld w,TroopDispatch d)=>d.Role==UnitDirectiveRole&&ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body)).HoldingPolicy==UnitDirectiveHoldingContract;
    static void EnableDirectiveHolding(ContractWorld w,NewMilitaryInput i)
    {
        if(i.OriginalPolicy!=UnitDirectiveHoldingContract)return;var s=w.NewMilitary!.UnitDirective!;
        if(s.Holding==null)w.NewMilitary=w.NewMilitary with{UnitDirective=s with{Holding=new(UnitDirectiveHoldingContract,w.ElapsedSeconds,w.CommitSequence,[],[],[])}};
    }
    static UnitDirectiveHoldingState? DirectiveHoldingBefore(UnitDirectiveHoldingState? x,long at,long rev)=>x==null||!Before(x.At,x.Revision,at,rev)?null:x with{Holdings=x.Holdings.Where(h=>Before(h.At,h.Revision,at,rev)).ToArray(),Outcomes=x.Outcomes.Where(f=>Before(f.At,f.Revision,at,rev)).ToArray(),Acks=x.Acks.Where(a=>Before(a.At,a.Revision,at,rev)).ToArray()};
    static UnitDirectiveLateOutcome[] DirectiveAckFlights(ContractWorld w)=>w.NewMilitary?.UnitDirective?.Holding?.Outcomes.Where(f=>f.Received).ToArray()??[];
    static UnitReplyHolding[] ExpectedDirectivePapers(ContractWorld w)
    {
        var originals=UnitDispatches(w).Where(d=>HoldsDirectiveOriginal(w,d)).SelectMany(d=>{
            var r=UnitReceipts(w).SingleOrDefault(r=>r.Id==d.Id&&!r.Delivered);var returned=r==null?null:UnitReplies(w).SingleOrDefault(a=>a.Id==d.Id);if(r==null||returned==null)return Array.Empty<UnitReplyHolding>();
            var paper=new UnitReplyHolding(d.Message,d.Attempt,UnitCourier(d.Id),r.ReplyDestination,returned.At,returned.Revision);
            return returned.Received?[paper]:new[]{paper,new UnitReplyHolding(r.ReplyMessage,r.ReplyAttempt,UnitCourier(d.Id),r.ReplyDestination,returned.At,returned.Revision)};
        });
        var acks=(w.NewMilitary?.UnitDirective?.Holding?.Acks??[]).Where(a=>!a.Received).Select(a=>{var f=DirectiveAckFlights(w).Single(f=>f.ReplyAttempt==a.Attempt);return new UnitReplyHolding(f.ReplyMessage!,a.Attempt,f.Carrier,f.Destination,a.At,a.Revision);});
        return originals.Concat(acks).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
    }
    static void UpdateDirectiveHolding(ContractWorld w)
    {
        if(w.NewMilitary?.UnitDirective?.Holding==null)return;
        foreach(var f in DirectiveAckFlights(w).Where(f=>f.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.UnitDirective.Holding.Acks.Any(a=>a.Attempt==f.ReplyAttempt)).ToArray()){
            Need(f.DueAt==w.ElapsedSeconds,"分隊原本の遅着返書到達を飛ばしました");var x=w.NewMilitary.UnitDirective.Holding;bool received=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt).Status==AttemptStatus.Delivered;
            w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Holding=x with{Acks=[..x.Acks,new(f.ReplyAttempt!,w.ElapsedSeconds,w.CommitSequence,received)]}}};
        }
        var current=w.NewMilitary.UnitDirective.Holding!;w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Holding=current with{Holdings=ExpectedDirectivePapers(w)}}};
    }
    static void BeforeDirectiveLateAcks(ContractWorld w)
    {
        foreach(var f in DirectiveAckFlights(w).Where(f=>f.DueAt==w.ElapsedSeconds)){
            var a=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt);if(a.Status==AttemptStatus.InTransit&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),f.Destination)>1000)MessageFlow.Fail(w.Messaging,f.ReplyMessage!);
        }
    }
    static bool DirectiveCarrierIdle(ContractWorld w,EntityId carrier)=>!(w.NewMilitary?.HeldDelivery?.Transfers.Any(t=>t.Carrier==carrier&&t.DueAt>w.ElapsedSeconds)??false)&&!DirectiveAckFlights(w).Any(f=>f.Carrier==carrier&&f.DueAt>w.ElapsedSeconds)&&!(w.NewMilitary?.CommandHolding?.Outcomes.Any(f=>f.Carrier==carrier&&f.Received&&f.DueAt>w.ElapsedSeconds)??false)&&!(w.NewMilitary?.Cancellation?.Notices.Any(f=>f.Carrier==carrier&&f.DueAt>w.ElapsedSeconds)??false);
    static UnitReplyHolding? PickDirectivePaper(ContractWorld w,UnitDirectiveHandoffInput p)
    {
        if(w.NewMilitary?.UnitDirective?.Holding==null||!RecognitionEnabled(w)||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)||!Deployed(w)||DirectiveOriginal(w,p.Original) is not {} root||!SameMilitaryCase(w,root)||root.Recipient!=p.Known.Unit||SelectKnownTarget(w,p.Known.Unit)!=p.Known)return null;
        var person=PhysicalPerson(w,w.ElapsedSeconds);if(person!=PhysicalPerson(w,w.ElapsedSeconds+1))return null;
        var h=HeldPapers(w,w.ElapsedSeconds).SingleOrDefault(h=>h.Message==root.Message&&h.Attempt==p.Attempt&&h.Holder==p.Carrier);
        if(h==null||!DirectiveCarrierIdle(w,h.Holder)||RegionalGround.Distance(person,h.Position)>1000)return null;
        var d=UnitDispatches(w).Single(d=>d.Attempt==h.Attempt);var attempt=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);var message=w.Messaging.Messages.Single(m=>m.Id==h.Message);
        if(!HoldsDirectiveOriginal(w,d)||attempt.Status!=AttemptStatus.Lost||attempt.LateDueAt!=null)return null;
        try{var route=RegionalGround.Plan(h.Position,p.Known.Position,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);return message.DeliverBy==null||due<=message.DeliverBy?h:null;}catch(InvalidDataException){return null;}
    }
    public static UnitDirectiveHandoffInput? SelectDirectivePaper(ContractWorld w,EntityId original)
    {
        if(DirectiveOriginal(w,original) is not {} root||SelectKnownTarget(w,root.Recipient) is not {} known)return null;
        foreach(var h in HeldPapers(w,w.ElapsedSeconds).Where(h=>h.Message==root.Message).OrderBy(h=>h.At).ThenBy(h=>h.Attempt,StringComparer.Ordinal)){
            var input=new UnitDirectiveHandoffInput(UnitDirectiveHoldingContract,original,h.Attempt,h.Holder,known);if(PickDirectivePaper(w,input)!=null)return input;
        }
        return null;
    }
    public static bool DirectiveHandoffAllowed(ContractWorld w,UnitDirectiveHandoffInput? p)=>p!=null&&p.Contract==UnitDirectiveHoldingContract&&p.Known!=null&&PickDirectivePaper(w,p)!=null;
    static void ValidateDirectiveHandoffInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveHandoffAction(i.Action)==(i.DirectiveHandoff!=null),"分隊原本受渡しの操作と実物参照が一致しません");if(i.DirectiveHandoff is not {} p)return;
        Need(p.Contract==UnitDirectiveHoldingContract&&EntityId.IsValid(p.Original.Value)&&EntityId.IsValid(p.Carrier.Value)&&!string.IsNullOrWhiteSpace(p.Attempt)&&p.Known!=null,"分隊原本の版・保持者・根拠が不正です");
        Need(i.Directive==null&&i.DirectiveRetry==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.TargetOriginal==null&&i.PostPolicy==null&&i.OriginalPolicy==null&&i.TransportPolicy==null&&i.DeliverBy==null&&i.RequestedEffectiveAt==null,"原本受渡しで指示・期限・方針を変更できません");
    }
    static void ApplyDirectiveHandoff(ContractWorld w,NewMilitaryInput i)
    {
        var p=i.DirectiveHandoff!;var h=PickDirectivePaper(w,p)!;var to=p.Known.Position;var cargo=HeldPapers(w,w.ElapsedSeconds).Where(x=>x.Holder==h.Holder).Select(x=>x.Attempt).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var route=RegionalGround.Plan(h.Position,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);var attempt=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);
        MessageFlow.ScheduleLate(w.Messaging,h.Message,attempt.Number,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));var x=w.NewMilitary!.HeldDelivery??new(UnitHeldContract,[],[]);
        w.NewMilitary=w.NewMilitary with{HeldDelivery=x with{Transfers=[..x.Transfers,new(i.Id,w.ElapsedSeconds,w.CommitSequence,i.Action,h.Message,h.Attempt,h.Holder,cargo,h.Position,to,route,due)]}};
    }
    static void HeldDirectiveOutcome(ContractWorld w,UnitHeldTransfer t,bool received)
    {
        if(w.Messaging.Messages.Single(m=>m.Id==t.Message).Kind!="unit-post-directive")return;
        var d=UnitDispatches(w).Single(d=>d.Attempt==t.Attempt);Need(HoldsDirectiveOriginal(w,d)&&w.NewMilitary!.UnitDirective!.Holding!=null,"保持していない分隊原本を遅着させました");
        var note=ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));UnitDirectiveOutcome? outcome=null;EntityId? order=null;string? reply=null,attempt=null;FieldLeg[] route=[];long due=0;
        if(received){var result=DecideDirectedArrival(w,d);outcome=result.Outcome;order=result.Order;route=RegionalGround.Plan(t.Destination,t.Origin,w.ElapsedSeconds,courier:true,atlas:true);due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);reply=UnitMessage(t.Id,"directive-late-reply");
            MessageFlow.Send(w.Messaging,new(reply,note.Request.Value,"unit-post-directive-late-reply",d.Recipient.Value,w.PlayerControl.Value,DirectiveAnswerBody(w,d,outcome.Value),w.Messaging.Messages.Single(m=>m.Id==d.Message).Scope,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));attempt=MessageFlow.Latest(w.Messaging,reply).Id;
        }
        var x=w.NewMilitary!.UnitDirective!.Holding!;w.NewMilitary=w.NewMilitary with{UnitDirective=w.NewMilitary.UnitDirective with{Holding=x with{Outcomes=[..x.Outcomes,new(t.Id,note.Request,w.ElapsedSeconds,w.CommitSequence,received,outcome,order,reply,attempt,t.Destination,t.Origin,route,due,t.Carrier)]}}};
    }
    static UnitDirectiveLateOutcome? KnownDirectiveAck(ContractWorld w,string original,string sender)=>DirectiveAckFlights(w).Where(f=>DirectiveOriginal(w,f.Original)?.Message==original&&MessageFlow.Knows(w.Messaging,f.ReplyMessage!,sender)).OrderBy(f=>f.At).ThenBy(f=>f.Transfer.Value,StringComparer.Ordinal).LastOrDefault();
    static string DirectiveOutcomeText(UnitDirectiveOutcome outcome)=>outcome switch{UnitDirectiveOutcome.Applied=>"受令・指示を実行",UnitDirectiveOutcome.Superseded=>"新しい命令を維持",UnitDirectiveOutcome.AuthorityEnded=>"指揮権終了で実行せず",UnitDirectiveOutcome.RoutActive=>"敗走中のため実行せず",UnitDirectiveOutcome.AlreadyApplied=>"同じ原本を重複実行せず",_=>"経路を確保できず指示を実行せず"};
    static void ValidateDirectiveHolding(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.UnitDirective?.Holding;var first=s.Requests.FirstOrDefault(q=>q.Input.OriginalPolicy==UnitDirectiveHoldingContract);Need((x!=null)==(first!=null),"分隊原本の保持方針と実物台帳が違います");if(x==null)return;
        Need(x.Contract==UnitDirectiveHoldingContract&&x.At==first!.At&&x.Revision==first.Revision&&x.Holdings!=null&&x.Outcomes!=null&&x.Acks!=null&&Same(x.Holdings,ExpectedDirectivePapers(w)),"分隊原本の保持者・所在・返書が欠落または改ざんされています");
        var transfers=(s.HeldDelivery?.Transfers??[]).Where(t=>t.DueAt<=w.ElapsedSeconds&&w.Messaging.Messages.Single(m=>m.Id==t.Message).Kind=="unit-post-directive").ToArray();
        Need(x.Outcomes.Length==transfers.Length&&x.Outcomes.Select(f=>f.Transfer).Distinct().Count()==x.Outcomes.Length&&x.Acks.Select(a=>a.Attempt).Distinct().Count()==x.Acks.Length,"分隊原本の遅着・返書が欠落または重複しています");
        foreach(var f in x.Outcomes)Need(transfers.Any(t=>t.Id==f.Transfer&&t.DueAt==f.At)&&f.Revision>0&&f.Revision<=w.CommitSequence&&(f.Received?f.Outcome!=null&&Enum.IsDefined(f.Outcome.Value)&&(f.Order!=null)==(f.Outcome==UnitDirectiveOutcome.Applied)&&f.ReplyMessage!=null&&f.ReplyAttempt!=null&&f.DueAt>f.At:f.Outcome==null&&f.Order==null&&f.ReplyMessage==null&&f.ReplyAttempt==null&&f.DueAt==0&&f.Route.Length==0),"分隊原本の遅着効力と返書が不正です");
        Need(x.Acks.Length==x.Outcomes.Count(f=>f.Received&&f.DueAt<=w.ElapsedSeconds),"分隊遅着返書の到達が欠落しています");foreach(var a in x.Acks)Need(x.Outcomes.Any(f=>f.ReplyAttempt==a.Attempt&&f.DueAt==a.At)&&a.Revision>0&&a.Revision<=w.CommitSequence,"分隊遅着返書の時点が不正です");
        if(!replay)return;
        foreach(var g in x.Outcomes.Select(f=>(f.At,f.Revision)).Concat(x.Acks.Select(a=>(a.At,a.Revision))).Distinct()){
            var p=BeforeState(w,g.At,g.Revision-1);p.CommitSequence=g.Revision;BeforeUnitMessages(p);MessageFlow.Advance(p.Messaging,p.ElapsedSeconds);UpdateRout(p);UpdateCancellation(p);UpdateWithdrawal(p);UpdateCancellationHoldings(p);UpdateCommandHolding(p);UpdateHeldDelivery(p);UpdateReplyRecovery(p);
            var expected=BeforeState(w,g.At,g.Revision);Need(Same(p.NewMilitary!.UnitDirective!.Holding,expected.NewMilitary!.UnitDirective!.Holding)&&Same(UnitTransport(p),UnitTransport(expected))&&Same(p.NewMilitary.Orders,expected.NewMilitary.Orders),"分隊原本の実物遅着・一度だけの効力・返書を再現できません");
        }
    }
}
