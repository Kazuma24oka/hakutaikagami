using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitHeldTransfer(EntityId Id,long At,long Revision,NewMilitaryAction Action,string Message,string Attempt,EntityId Carrier,string[] Cargo,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt);
public sealed record UnitHeldArrival(EntityId Id,long At,long Revision,FieldPoint RecipientPosition,bool Present,bool Received);
public sealed record UnitHeldDeliveryState(string Contract,UnitHeldTransfer[] Transfers,UnitHeldArrival[] Arrivals);
public static partial class NewGameMilitary
{
    public const string UnitHeldContract="present-holder-actual-paper-registered-late-handover-once-per-attempt-v1";
    static bool HeldAction(NewMilitaryAction a)=>a is NewMilitaryAction.TakeHeldReply or NewMilitaryAction.ReturnHeldReportHome;
    public static UnitReplyHolding[] HeldPapers(ContractWorld w,long at)
    {
        var rows=new List<UnitReplyHolding>();var x=w.NewMilitary?.HeldDelivery;
        foreach(var h in w.NewMilitary?.ReplyRecovery?.Holdings.Where(h=>h.At<=at)??[]){
            var moves=x?.Transfers.Where(t=>t.At<=at&&t.Cargo.Contains(h.Attempt)).OrderBy(t=>t.Revision).ToArray()??[];
            if(moves.Any(t=>t.Attempt==h.Attempt&&x!.Arrivals.Any(a=>a.Id==t.Id&&a.At<=at&&a.Received)))continue;
            var last=moves.LastOrDefault();rows.Add(last==null?h:h with{Position=RegionalGround.At(last.Route,last.Origin,at)});
        }
        return rows.OrderBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();
    }
    static UnitReplyHolding? PickHeld(ContractWorld w,NewMilitaryAction action)
    {
        if(!HeldAction(action)||w.NewMilitary?.ReplyRecovery==null)return null;
        var person=PhysicalPerson(w,w.ElapsedSeconds);if(person!=PhysicalPerson(w,w.ElapsedSeconds+1))return null;
        return HeldPapers(w,w.ElapsedSeconds).Where(h=>{
            var a=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);var m=w.Messaging.Messages.Single(m=>m.Id==h.Message);
            bool allowed=action==NewMilitaryAction.TakeHeldReply?m.RecipientId==w.PlayerControl.Value:m.Kind=="delivery-failure"&&m.RecipientId==UnitIds[0].Value&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence)&&Deployed(w);
            return allowed&&a.Status==AttemptStatus.Lost&&a.LateDueAt==null&&RegionalGround.Distance(person,h.Position)<=1000&&!(w.NewMilitary.HeldDelivery?.Transfers.Any(t=>t.Carrier==h.Holder&&t.DueAt>w.ElapsedSeconds)??false);
        }).OrderByDescending(h=>h.At).ThenBy(h=>h.Attempt,StringComparer.Ordinal).FirstOrDefault();
    }
    public static bool HeldAllowed(ContractWorld w,NewMilitaryAction action)=>PickHeld(w,action)!=null;
    static void ApplyHeld(ContractWorld w,NewMilitaryInput i)
    {
        if(!HeldAction(i.Action))return;var h=PickHeld(w,i.Action)!;var to=i.Action==NewMilitaryAction.TakeHeldReply?PhysicalPerson(w,w.ElapsedSeconds):Home(w,0);
        var cargo=HeldPapers(w,w.ElapsedSeconds).Where(p=>p.Holder==h.Holder).Select(p=>p.Attempt).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var route=RegionalGround.Plan(h.Position,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);var attempt=w.Messaging.Attempts.Single(a=>a.Id==h.Attempt);
        MessageFlow.ScheduleLate(w.Messaging,h.Message,attempt.Number,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        var x=w.NewMilitary!.HeldDelivery??new(UnitHeldContract,[],[]);w.NewMilitary=w.NewMilitary with{HeldDelivery=x with{Transfers=[..x.Transfers,new(i.Id,w.ElapsedSeconds,w.CommitSequence,i.Action,h.Message,h.Attempt,h.Holder,cargo,h.Position,to,route,due)]}};
    }
    static FieldPoint HeldRecipient(ContractWorld w,UnitHeldTransfer t)=>w.Messaging.Messages.Single(m=>m.Id==t.Message).RecipientId==w.PlayerControl.Value?PhysicalPerson(w,w.ElapsedSeconds):Position(w,0,w.ElapsedSeconds);
    static bool HeldPresent(ContractWorld w,UnitHeldTransfer t,FieldPoint p)=>RegionalGround.Distance(p,t.Destination)<=1000&&(w.Messaging.Messages.Single(m=>m.Id==t.Message).RecipientId==w.PlayerControl.Value||UnitPostPresent(w,t.Destination));
    static void BeforeHeldMessages(ContractWorld w)
    {
        foreach(var t in w.NewMilitary?.HeldDelivery?.Transfers.Where(t=>t.DueAt==w.ElapsedSeconds)??[]){if(HeldPresent(w,t,HeldRecipient(w,t)))continue;
            // Same explicit late-arrival presence rejection as legacy CourierRecovery. Never fake a receiver/proxy or freeze the clock.
            w.Messaging.Attempts=w.Messaging.Attempts.Select(a=>a.Id==t.Attempt?a with{Status=AttemptStatus.Rejected,FinishedAt=w.ElapsedSeconds}:a).ToArray();
        }
    }
    static void UpdateHeldDelivery(ContractWorld w)
    {
        foreach(var t in w.NewMilitary?.HeldDelivery?.Transfers.Where(t=>t.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.HeldDelivery.Arrivals.Any(a=>a.Id==t.Id)).ToArray()??[]){
            Need(t.DueAt==w.ElapsedSeconds,"保持紙の受渡し境界を飛ばしました");var a=w.Messaging.Attempts.Single(a=>a.Id==t.Attempt);var p=HeldRecipient(w,t);bool present=HeldPresent(w,t,p);var x=w.NewMilitary!.HeldDelivery!;
            w.NewMilitary=w.NewMilitary with{HeldDelivery=x with{Arrivals=[..x.Arrivals,new(t.Id,w.ElapsedSeconds,w.CommitSequence,p,present,a.Status==AttemptStatus.Delivered&&a.ArrivedLate)]}};
        }
    }
    static UnitHeldDeliveryState? HeldBefore(NewMilitaryState? s,long at,long rev)=>s?.HeldDelivery is not {} x||!s.Requests.Any(q=>HeldAction(q.Input.Action)&&Before(q.At,q.Revision,at,rev))?null:x with{Transfers=x.Transfers.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray(),Arrivals=x.Arrivals.Where(t=>Before(t.At,t.Revision,at,rev)).ToArray()};
    static FieldPoint? LateReportPoint(ContractWorld w,string message){var t=w.NewMilitary?.HeldDelivery?.Transfers.LastOrDefault(t=>t.Message==message&&w.NewMilitary.HeldDelivery.Arrivals.Any(a=>a.Id==t.Id&&a.Received));return t?.Destination;}
    static void ValidateHeldShape(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.HeldDelivery;Need((x!=null)==s.Requests.Any(q=>HeldAction(q.Input.Action)),"保持紙の受渡しと原判断が違います");if(x==null)return;
        Need(x.Contract==UnitHeldContract&&x.Transfers.Length==s.Requests.Count(q=>HeldAction(q.Input.Action))&&x.Transfers.Select(t=>t.Id).Distinct().Count()==x.Transfers.Length&&x.Transfers.Select(t=>t.Attempt).Distinct().Count()==x.Transfers.Length&&x.Arrivals.Select(a=>a.Id).Distinct().Count()==x.Arrivals.Length,"保持紙の受渡しが欠落/重複しています");
        foreach(var t in x.Transfers)Need(EntityId.IsValid(t.Id.Value)&&HeldAction(t.Action)&&t.At>=0&&t.At<=w.ElapsedSeconds&&t.Revision>0&&t.Revision<=w.CommitSequence&&t.DueAt>t.At&&t.Origin!=null&&t.Destination!=null&&t.Route!=null&&t.Cargo!=null&&t.Cargo.Distinct().Count()==t.Cargo.Length&&t.Cargo.Contains(t.Attempt)&&w.Messaging.Attempts.Any(a=>a.Id==t.Attempt&&a.MessageId==t.Message),"保持紙の実経路が不正です");
        Need(x.Arrivals.Length==x.Transfers.Count(t=>t.DueAt<=w.ElapsedSeconds),"保持紙の実到達が欠落しています");
        foreach(var a in x.Arrivals)Need(a.RecipientPosition!=null&&x.Transfers.Any(t=>t.Id==a.Id&&t.DueAt==a.At)&&a.At<=w.ElapsedSeconds&&a.Revision>0&&a.Revision<=w.CommitSequence,"保持紙の到達証拠が不正です");
    }
    static void ValidateHeldDelivery(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.HeldDelivery;if(x==null||!replay)return;
        foreach(var q in s.Requests.Where(q=>HeldAction(q.Input.Action))){var prior=BeforeState(w,q.At,q.Revision-1);prior.CommitSequence=q.Revision;Dispatch(prior,q.Input);Need(Same(prior.NewMilitary!.HeldDelivery,HeldBefore(s,q.At,q.Revision))&&Same(UnitTransport(prior),UnitTransport(BeforeState(w,q.At,q.Revision))),"保持物/本人の現地から受渡しを再現できません");}
        foreach(var group in x.Arrivals.Select(a=>(a.At,a.Revision)).Distinct()){
            var prior=BeforeState(w,group.At,group.Revision-1);prior.CommitSequence=group.Revision;BeforeUnitMessages(prior);MessageFlow.Advance(prior.Messaging,prior.ElapsedSeconds);UpdateRout(prior);UpdateWithdrawal(prior);UpdateHeldDelivery(prior);UpdateReplyRecovery(prior);
            Need(Same(prior.NewMilitary!.HeldDelivery,HeldBefore(s,group.At,group.Revision))&&Same(prior.NewMilitary.ReplyRecovery,ReplyRecoveryBefore(s,group.At,group.Revision)),"保持原本の実受領/不在/紙の所在を再現できません");
            Need(Same(UnitTransport(prior),UnitTransport(BeforeState(w,group.At,group.Revision))),"保持原本の遅着と共通正式受領が違います");
        }
        var current=ContractJson.Copy(w);UpdateHeldDelivery(current);Need(Same(current.NewMilitary!.HeldDelivery,x),"保持紙の到達更新が不足しています");
    }
}
