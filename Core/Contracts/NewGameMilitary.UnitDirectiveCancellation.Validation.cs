using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    static void ValidateDirectiveCancellation(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.UnitDirective?.Cancellation;
        Need((x!=null)==s.Requests.Any(q=>DirectiveCancellationAction(q.Input.Action)),"分隊原本取消の入力と台帳が一致しません");if(x==null)return;
        Need(x.Contract==UnitDirectiveCancellationContract&&x.Dispatches!=null&&x.Receipts!=null&&x.Replies!=null&&x.Effects!=null&&x.Notices!=null&&x.Arrivals!=null&&x.Holdings!=null,"分隊原本取消の契約・必須項目が不正です");
        Need(x.Dispatches.Length==s.Requests.Count(q=>DirectiveCancellationAction(q.Input.Action)||DirectiveCancelRetryAction(q.Input.Action))&&x.Dispatches.Select(d=>d.Id).Distinct().Count()==x.Dispatches.Length,"分隊原本取消の発送が欠落・重複しています");
        foreach(var d in x.Dispatches){
            var q=s.Requests.SingleOrDefault(q=>q.Input.Id==d.Id);Need(q!=null&&(DirectiveCancellationAction(q.Input.Action)||DirectiveCancelRetryAction(q.Input.Action))&&d.Role==UnitDirectiveCancellationRole&&d.At==q.At&&d.Revision==q.Revision&&d.DueAt>d.At&&d.Origin!=null&&d.Destination!=null&&d.Route!=null,"分隊原本取消の発送が不正です");
            var n=DirectiveCancelNote(w,d);var root=DirectiveOriginal(w,n.Original);Need(n.Contract==UnitDirectiveCancellationContract&&DirectiveCancellationOriginal(w,n.Request) is {} cancellation&&cancellation.Message==d.Message&&root!=null&&root.Message==n.OriginalMessage&&root.Recipient==d.Recipient&&n.EffectiveAt>=cancellation.At,"分隊原本取消の対象・文面・時刻が不正です");
        }
        Need(x.Receipts.Select(r=>r.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(r=>r.Id).Distinct().Count()==x.Replies.Length&&x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds),"分隊取消の初回受領・返書が欠落・重複しています");
        var effective=DirectiveCancellationOriginals(w).Where(d=>w.Messaging.Receipts.Any(r=>r.MessageId==d.Message&&Math.Max(r.ReceivedAt,DirectiveCancelNote(w,d).EffectiveAt)<=w.ElapsedSeconds)).ToArray();
        Need(x.Effects.Select(e=>e.Id).Distinct().Count()==x.Effects.Length&&x.Effects.Length==effective.Length,"分隊取消の効力が欠落・重複しています");
        foreach(var e in x.Effects)Need(effective.Any(d=>d.Id==e.Id&&DirectiveCancelNote(w,d).Original==e.Original&&Math.Max(w.Messaging.Receipts.Single(r=>r.MessageId==d.Message).ReceivedAt,DirectiveCancelNote(w,d).EffectiveAt)==e.At)&&e.Revision>0&&e.Revision<=w.CommitSequence&&Enum.IsDefined(e.Outcome)&&e.Outcome!=UnitCancelDisposition.Waiting&&(e.StopOrder!=null)==(e.Outcome==UnitCancelDisposition.Stopped)&&e.Position!=null,"分隊取消の効力・停止命令が不正です");
        Need(x.Notices.Select(n=>n.Id).Distinct().Count()==x.Notices.Length&&x.Notices.Select(n=>n.Attempt).Distinct().Count()==x.Notices.Length&&x.Notices.Length==x.Effects.Count(e=>e.At>w.Messaging.Receipts.Single(r=>r.MessageId==x.Dispatches.Single(d=>d.Id==e.Id).Message).ReceivedAt),"分隊取消の将来効力の物理通知が欠落・重複しています");
        foreach(var n in x.Notices)Need(x.Effects.Any(e=>e.Id==n.Cancellation&&e.At==n.At&&e.Revision==n.Revision)&&n.DueAt>n.At&&n.Origin!=null&&n.Destination!=null&&n.Route!=null&&EntityId.IsValid(n.Carrier.Value),"分隊取消通知の時点・経路が不正です");
        Need(x.Arrivals.Select(a=>a.Attempt).Distinct().Count()==x.Arrivals.Length&&x.Arrivals.Length==x.Notices.Count(n=>n.DueAt<=w.ElapsedSeconds),"分隊取消通知の到達が欠落・重複しています");
        foreach(var a in x.Arrivals)Need(x.Notices.Any(n=>n.Attempt==a.Attempt&&n.DueAt==a.At)&&a.Revision>0&&a.Revision<=w.CommitSequence,"分隊取消通知の到達時点が不正です");
        var papers=x.Arrivals.Where(a=>!a.Received).Select(a=>{var n=x.Notices.Single(n=>n.Attempt==a.Attempt);return new UnitReplyHolding(n.Message,n.Attempt,n.Carrier,n.Destination,a.At,a.Revision);}).OrderBy(h=>h.Revision).ThenBy(h=>h.Attempt,StringComparer.Ordinal).ToArray();Need(Same(x.Holdings,papers),"分隊取消通知の実保持者が不正です");
        if(!replay)return;
        foreach(var q in s.Requests.Where(q=>DirectiveCancellationAction(q.Input.Action)||DirectiveCancelRetryAction(q.Input.Action))){var p=BeforeState(w,q.At,q.Revision-1);p.CommitSequence=q.Revision;Dispatch(p,q.Input);var expected=BeforeState(w,q.At,q.Revision);Need(Same(p.NewMilitary!.UnitDirective!.Cancellation,expected.NewMilitary!.UnitDirective!.Cancellation)&&Same(UnitTransport(p),UnitTransport(expected)),"分隊取消の発送を原入力から再現できません");}
        foreach(var g in x.Effects.Select(e=>(e.At,e.Revision)).Concat(x.Arrivals.Select(a=>(a.At,a.Revision))).Distinct()){
            var p=BeforeState(w,g.At,g.Revision-1);p.CommitSequence=g.Revision;BeforeUnitMessages(p);MessageFlow.Advance(p.Messaging,p.ElapsedSeconds);UpdateRout(p);UpdateCancellation(p);UpdateWithdrawal(p);UpdateCancellationHoldings(p);UpdateCommandHolding(p);UpdateHeldDelivery(p);UpdateReplyRecovery(p);
            var expected=BeforeState(w,g.At,g.Revision);Need(Same(p.NewMilitary!.UnitDirective!.Cancellation,expected.NewMilitary!.UnitDirective!.Cancellation)&&Same(p.NewMilitary.Orders,expected.NewMilitary.Orders)&&Same(UnitTransport(p),UnitTransport(expected)),"分隊取消の現地効力・原本限定の停止・物理通知を再現できません");
        }
        var current=ContractJson.Copy(w);UpdateDirectiveCancellation(current);Need(Same(current.NewMilitary!.UnitDirective!.Cancellation,x)&&Same(current.NewMilitary.Orders,s.Orders)&&Same(UnitTransport(current),UnitTransport(w)),"分隊取消の現在更新が不足しています");
    }
}
