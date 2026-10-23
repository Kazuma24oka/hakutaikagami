using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitHeldOrderOutcome(EntityId Transfer,string Original,long At,long Revision,bool Received,bool Applied,string? ReplyMessage,string? ReplyAttempt,FieldPoint Origin,FieldPoint Destination,FieldLeg[] Route,long DueAt,EntityId Carrier);
public sealed record UnitHeldOrderAck(string Attempt,long At,long Revision,bool Received);
public static partial class NewGameMilitary
{
    static bool HeldOrderMessage(ContractWorld w,UnitHeldTransfer t)=>w.Messaging.Messages.Single(m=>m.Id==t.Message).Kind=="unit-post-order";
    static void HeldOrderOutcome(ContractWorld w,UnitHeldTransfer t,bool received)
    {
        if(!HeldOrderMessage(w,t))return;
        var d=w.NewMilitary!.Withdrawal!.Dispatches.Single(d=>d.Message==t.Message);bool applied=received&&SameMilitaryCase(w,d)&&!CancelledRecall(w,d.Message)&&!w.NewMilitary.Orders.Any(o=>o.Id==UnitOrder(d.Id)||SupersedesRecall(w,o,d));
        if(applied)Order(w,new(UnitOrder(d.Id),NewMilitaryAction.Recall));
        string? reply=null,attempt=null;FieldLeg[] path=[];long due=0;
        if(received){path=RegionalGround.Plan(t.Destination,t.Origin,w.ElapsedSeconds,courier:true,atlas:true);due=Math.Max(w.ElapsedSeconds+1,path.LastOrDefault()?.EndAt??0);reply=UnitMessage(t.Id,"late-reply");MessageFlow.Send(w.Messaging,new(reply,d.Id.Value,"unit-post-late-reply",UnitIds[0].Value,w.PlayerControl.Value,Text(new UnitPostAnswer(d.Message,true,applied)),w.NewMilitary.Terms.Site.Value,w.ElapsedSeconds),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));attempt=MessageFlow.Latest(w.Messaging,reply).Id;}
        var x=w.NewMilitary.CommandHolding!;w.NewMilitary=w.NewMilitary with{CommandHolding=x with{Outcomes=[..x.Outcomes,new(t.Id,t.Message,w.ElapsedSeconds,w.CommitSequence,received,applied,reply,attempt,t.Destination,t.Origin,path,due,t.Carrier)]}};
    }
    static void BeforeHeldOrderAcks(ContractWorld w){foreach(var f in w.NewMilitary?.CommandHolding?.Outcomes.Where(f=>f.Received&&f.DueAt==w.ElapsedSeconds)??[]){var a=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt);if(a.Status==AttemptStatus.InTransit&&RegionalGround.Distance(PhysicalPerson(w,w.ElapsedSeconds),f.Destination)>1000)MessageFlow.Fail(w.Messaging,f.ReplyMessage!);}}
    static UnitReplyHolding[] HeldOrderAckPapers(ContractWorld w)=>(w.NewMilitary?.CommandHolding?.Acks??[]).Where(a=>!a.Received).Select(a=>{var f=w.NewMilitary!.CommandHolding!.Outcomes.Single(f=>f.ReplyAttempt==a.Attempt);return new UnitReplyHolding(f.ReplyMessage!,a.Attempt,f.Carrier,f.Destination,a.At,a.Revision);}).ToArray();
    static void UpdateHeldOrderAcks(ContractWorld w){foreach(var f in w.NewMilitary?.CommandHolding?.Outcomes.Where(f=>f.Received&&f.DueAt<=w.ElapsedSeconds&&!w.NewMilitary.CommandHolding.Acks.Any(a=>a.Attempt==f.ReplyAttempt)).ToArray()??[]){Need(f.DueAt==w.ElapsedSeconds,"原命令の受令返書到達を飛ばしました");var x=w.NewMilitary!.CommandHolding!;bool received=w.Messaging.Attempts.Single(a=>a.Id==f.ReplyAttempt).Status==AttemptStatus.Delivered;w.NewMilitary=w.NewMilitary with{CommandHolding=x with{Acks=[..x.Acks,new(f.ReplyAttempt!,w.ElapsedSeconds,w.CommitSequence,received)]}};}}
    static FieldPoint HeldCarrierPosition(ContractWorld w,UnitReplyHolding h,long at)
    {
        var motions=(w.NewMilitary?.HeldDelivery?.Transfers??[]).Where(t=>t.Carrier==h.Holder&&t.Cargo.Contains(h.Attempt)&&t.At<=at).Select(t=>(t.At,t.Revision,t.Origin,t.Route))
            .Concat((w.NewMilitary?.CommandHolding?.Outcomes??[]).Where(t=>t.Carrier==h.Holder&&t.Received&&t.At>=h.At&&t.At<=at).Select(t=>(t.At,t.Revision,t.Origin,t.Route))).Concat((w.NewMilitary?.Cancellation?.Notices??[]).Where(t=>t.Carrier==h.Holder&&t.At>=h.At&&t.At<=at).Select(t=>(t.At,t.Revision,t.Origin,t.Route))).OrderBy(t=>t.Revision).ToArray();
        return motions.Length==0?h.Position:RegionalGround.At(motions[^1].Route,motions[^1].Origin,at);
    }
    static void ValidateHeldOrderOutcomes(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.CommandHolding;if(x==null)return;
        Need(x.Outcomes!=null&&x.Acks!=null&&x.Outcomes.Select(o=>o.Transfer).Distinct().Count()==x.Outcomes.Length&&x.Acks.Select(a=>a.Attempt).Distinct().Count()==x.Acks.Length,"原命令の遅着と受令返書が重複しています");
        var transfers=(s.HeldDelivery?.Transfers??[]).Where(t=>t.DueAt<=w.ElapsedSeconds&&HeldOrderMessage(w,t)).ToArray();Need(x.Outcomes.Length==transfers.Length,"原命令遅着の実効力が欠落しています");
        foreach(var f in x.Outcomes)Need(transfers.Any(t=>t.Id==f.Transfer&&t.DueAt==f.At)&&f.Origin!=null&&f.Destination!=null&&f.Route!=null&&f.At<=w.ElapsedSeconds&&f.Revision>0&&f.Revision<=w.CommitSequence&&(f.Received?f.ReplyMessage!=null&&f.ReplyAttempt!=null&&f.DueAt>f.At:!f.Applied&&f.ReplyMessage==null&&f.ReplyAttempt==null&&f.Route.Length==0&&f.DueAt==0),"原命令の受令/不在と返書が不正です");
        Need(x.Acks.Length==x.Outcomes.Count(f=>f.Received&&f.DueAt<=w.ElapsedSeconds),"原命令の受令返書到達が欠落しています");
        foreach(var a in x.Acks)Need(x.Outcomes.Any(f=>f.ReplyAttempt==a.Attempt&&f.DueAt==a.At)&&a.Revision>0&&a.Revision<=w.CommitSequence,"原命令の受令返書時点が不正です");
        if(!replay)return;
        foreach(var g in x.Outcomes.Select(f=>(f.At,f.Revision)).Concat(x.Acks.Select(a=>(a.At,a.Revision))).Distinct()){
            var p=BeforeState(w,g.At,g.Revision-1);p.CommitSequence=g.Revision;BeforeUnitMessages(p);MessageFlow.Advance(p.Messaging,p.ElapsedSeconds);UpdateRout(p);UpdateCancellation(p);UpdateWithdrawal(p);UpdateCancellationHoldings(p);UpdateCommandHolding(p);UpdateHeldDelivery(p);UpdateReplyRecovery(p);
            var expected=BeforeState(w,g.At,g.Revision);Need(Same(p.NewMilitary!.CommandHolding,expected.NewMilitary!.CommandHolding)&&Same(UnitTransport(p),UnitTransport(expected))&&Same(p.NewMilitary.Orders,expected.NewMilitary.Orders),"原命令の実到達/一度だけの効力/物理返書を再現できません");
        }
    }
}
