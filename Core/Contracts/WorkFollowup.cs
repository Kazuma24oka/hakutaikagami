namespace HakutaiKagami.Core.Contracts;
public sealed record FollowupTerms(long Amount,long Fee,string Reason);
// Outcomes are local mutual handovers, not omniscient ratings or political blame.
public static class WorkFollowup
{
    public const string Policy="local-work-known-outcome-next-consignment-v3";
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.WorkPolicy==Policy;
    static bool Before(long time,long revision,long at,long cut)=>time<at||time==at&&revision<cut;
    static TransportJob[] Previous(ContractWorld w,long at,long revision)=>TransportWork.Jobs(w).Where(j=>Before(j.Handover.Terms.OfferedAt,j.CreatedRevision,at,revision)).Select(j=>j with{Handover=j.Handover with{Entries=j.Handover.Entries.Where(e=>Before(e.Input.At,j.Receipts.Single(p=>p.Operation==e.Input.Id).Revision,at,revision)).ToArray()}}).ToArray();
    static bool Terminal(TransportJob j)=>TransportHandover.Validate(j.Handover).Phase is HandoverPhase.Paid or HandoverPhase.Returned or HandoverPhase.Cancelled;
    public static bool Available(ContractWorld w,long at,long revision)=>Previous(w,at,revision).All(Terminal);
    public static FollowupTerms Terms(ContractWorld w,EntityId issuer,long at=long.MaxValue,long revision=long.MaxValue)
    {
        var prior=Previous(w,at,revision).Where(j=>j.Handover.Terms.Issuer==issuer).OrderBy(j=>j.CreatedRevision).LastOrDefault();
        long amount;string reason;
        if(prior==null){amount=WorkProgression.Recognized(w,issuer,at,revision)>0?200:100;reason=amount==200?"点検の成果を認めて、預ける銭を増やしたい。":"まずは小口の運搬を頼みたい。";}
        else {
            var phase=TransportHandover.Validate(prior.Handover).Phase;
            amount=phase switch{HandoverPhase.Paid=>Math.Min(300,prior.Handover.Terms.Amount+100),HandoverPhase.Returned=>100,_=>prior.Handover.Terms.Amount};
            reason=phase switch{HandoverPhase.Paid=>amount>prior.Handover.Terms.Amount?"前の運搬は届けと報告まで済んだ。今度は預ける銭を増やしたい。":"前の運搬も無事に済んだ。同じ規模の運搬を頼みたい。",HandoverPhase.Returned=>"預り銭の返却は確認した。次は小口の運搬から相談したい。",HandoverPhase.Cancelled=>"前の話は荷物を預ける前に取りやめた。同じ条件でもう一度相談したい。",_=>"先の運搬の受渡しと精算を済ませてほしい。"};
        }
        return new(amount,amount==100?20:amount==200?30:40,reason);
    }
}
