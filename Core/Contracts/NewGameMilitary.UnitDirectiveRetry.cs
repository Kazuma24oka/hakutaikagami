using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record UnitDirectiveRetryInput(string Contract,EntityId Original,KnownMilitaryTarget Known);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveRetryContract="explicit-own-unit-original-sender-known-failure-fixed-copy-v1";
    static bool DirectiveRetryAction(NewMilitaryAction a)=>a==NewMilitaryAction.RetryUnitDirective;
    static TroopDispatch? DirectiveOriginal(ContractWorld w,EntityId id)=>w.NewMilitary?.UnitDirective?.Dispatches.SingleOrDefault(d=>d.Id==id&&w.NewMilitary.Requests.Any(q=>q.Input.Id==id&&q.Input.Action==NewMilitaryAction.SendUnitDirective));
    public static EntityId? LatestDirectiveOriginal(ContractWorld w,EntityId unit)=>w.NewMilitary?.UnitDirective?.Dispatches.LastOrDefault(d=>d.Recipient==unit&&SameMilitaryCase(w,d)&&DirectiveOriginal(w,d.Id)!=null)?.Id;
    public static bool UnitDirectiveRetryAllowed(ContractWorld w,EntityId original)
    {
        if(!UnitDirectiveAllowed(w)||DirectiveOriginal(w,original) is not {} root||!SameMilitaryCase(w,root))return false;
        var known=SelectKnownTarget(w,root.Recipient);if(known==null)return false;
        var a=MessageFlow.Latest(w.Messaging,root.Message);var m=w.Messaging.Messages.Single(m=>m.Id==root.Message);
        if(!KnownFailedAttempt(a,true)||a.Number>=MessageFlow.MaxAttempts||!MessageFlow.FailureKnown(w.Messaging,a.Id,w.PlayerControl.Value)||ReceiptConfirmedToSender(w,root.Message,w.PlayerControl.Value))return false;
        // Availability uses only the person's own location and a known fixed destination.
        if(m.DeliverBy==null)return true;if(m.DeliverBy<w.ElapsedSeconds+1)return false;
        try{var route=RegionalGround.Plan(PersonPosition(w,w.ElapsedSeconds),known.Position,w.ElapsedSeconds,courier:true,atlas:true);return Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0)<=m.DeliverBy;}catch(InvalidDataException){return false;}
    }
    static void ValidateDirectiveRetryInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveRetryAction(i.Action)==(i.DirectiveRetry!=null),"分隊原本再送の操作と本文が一致しません");if(i.DirectiveRetry is not {} x)return;
        Need(x.Contract==UnitDirectiveRetryContract&&EntityId.IsValid(x.Original.Value)&&x.Known!=null,"分隊原本再送の版・根拠が不正です");var root=DirectiveOriginal(w,x.Original);
        Need(root!=null&&root.Recipient==x.Known.Unit&&SelectKnownTarget(w,x.Known.Unit)==x.Known,"同じ受信隊の既知宛先で原本を指定してください");
        Need(i.Directive==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.TargetOriginal==null&&i.DeliverBy==null&&i.RequestedEffectiveAt==null&&i.PostPolicy==null&&i.OriginalPolicy==null&&i.TransportPolicy==UnitSenderEvidenceContract,"再送で原本・元期限・受信方針を変更できません");
    }
    static void ApplyDirectiveRetry(ContractWorld w,NewMilitaryInput i)
    {
        if(!DirectiveRetryAction(i.Action))return;var x=i.DirectiveRetry!;var root=DirectiveOriginal(w,x.Original)!;var from=PersonPosition(w,w.ElapsedSeconds);var to=x.Known.Position;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);
        MessageFlow.Retry(w.Messaging,root.Message,w.PlayerControl.Value,w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)),true);
        var state=w.NewMilitary!.UnitDirective!;w.NewMilitary=w.NewMilitary with{UnitDirective=state with{Dispatches=[..state.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,root.Recipient,root.Role,new("evidence:unit-directive-retry/"+Hash(Text(x))),from,to,route,due,root.Message,MessageFlow.Latest(w.Messaging,root.Message).Id)]}};
    }
}
