using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    static void ValidateCancellationShape(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.Cancellation;Need((x!=null)==s.Requests.Any(q=>CancellationAction(q.Input.Action)),"取消入力と台帳が違います");if(x==null)return;
        Need(x.Contract==UnitCancellationContract&&x.Dispatches!=null&&x.Receipts!=null&&x.Replies!=null&&x.Receives!=null&&x.Effects!=null&&x.Notices!=null&&x.NoticeArrivals!=null&&x.Holdings!=null,"取消台帳の契約/必須項目が不正です");
        Need(x.Dispatches.Length==s.Requests.Count(q=>CancellationAction(q.Input.Action))&&x.Dispatches.Select(d=>d.Id).Distinct().Count()==x.Dispatches.Length&&x.Receipts.Select(d=>d.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(d=>d.Id).Distinct().Count()==x.Replies.Length,"取消の原本/初回到達が欠落・重複しています");
        foreach(var d in x.Dispatches){Need(d.Role==CancellationRole&&d.Recipient==UnitIds[0]&&d.At>=0&&d.At<=w.ElapsedSeconds&&d.Revision>0&&d.Revision<=w.CommitSequence&&d.DueAt>d.At&&d.Origin!=null&&d.Destination!=null&&d.Route!=null&&s.Requests.Any(q=>q.Input.Id==d.Id&&CancellationAction(q.Input.Action)),"取消の実発送が不正です");var note=CancellationNote(w,d);Need(note.Contract==UnitCancellationContract&&note.Role==CancellationRole&&note.Request==d.Id&&s.Withdrawal!.Dispatches.Any(t=>t.Id==note.Target&&t.Message==note.Original)&&note.RequestedAt>=d.At,"取消の対象/効力要求が不正です");}
        Need(x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds),"取消の初回受令/返書到達が不足しています");
        var messages=x.Dispatches.Select(d=>d.Message).ToHashSet();var delivered=w.Messaging.Attempts.Where(a=>messages.Contains(a.MessageId)&&a.Status==AttemptStatus.Delivered).ToArray();
        Need(x.Receives.Select(r=>r.Id).Distinct().Count()==x.Receives.Length&&x.Receives.Select(r=>r.Attempt).Distinct().Count()==x.Receives.Length&&x.Receives.Length==delivered.Length,"取消の実受令が欠落・重複しています");
        foreach(var r in x.Receives)Need(r.Point!=null&&r.ReturnTo!=null&&r.At<=w.ElapsedSeconds&&r.Revision>0&&r.Revision<=w.CommitSequence&&x.Dispatches.Any(d=>d.Id==r.Cancellation&&delivered.Any(a=>a.Id==r.Attempt&&a.MessageId==d.Message&&a.FinishedAt==r.At)),"取消の実受令証拠が不正です");
        var effective=x.Dispatches.Where(d=>w.Messaging.Receipts.Any(r=>r.MessageId==d.Message&&Math.Max(r.ReceivedAt,CancellationNote(w,d).RequestedAt)<=w.ElapsedSeconds)).ToArray();
        Need(x.Effects.Select(e=>e.Id).Distinct().Count()==x.Effects.Length&&x.Effects.Length==effective.Length,"取消効力が欠落・重複しています");
        foreach(var e in x.Effects)Need(effective.Any(d=>d.Id==e.Id&&CancellationNote(w,d).Original==e.Original&&Math.Max(w.Messaging.Receipts.Single(r=>r.MessageId==d.Message).ReceivedAt,CancellationNote(w,d).RequestedAt)==e.At)&&e.Revision>0&&e.Revision<=w.CommitSequence&&Enum.IsDefined(e.Outcome)&&e.Outcome!=UnitCancelDisposition.Waiting&&(e.StopOrder!=null)==(e.Outcome==UnitCancelDisposition.Stopped)&&e.Positions!=null&&e.Positions.Length==3&&e.Positions.All(p=>p!=null),"取消の効力/停止位置が不正です");
        Need(x.Notices.Select(f=>f.Id).Distinct().Count()==x.Notices.Length&&x.Notices.Select(f=>f.Attempt).Distinct().Count()==x.Notices.Length&&x.NoticeArrivals.Select(a=>a.Id).Distinct().Count()==x.NoticeArrivals.Length,"取消の通知便が重複しています");
        int expectedNotices=x.Receives.Count(r=>r.Late)+x.Effects.Count(e=>e.At>x.Receives.Where(r=>r.Cancellation==e.Id).Min(r=>r.At));Need(x.Notices.Length==expectedNotices,"取消の遅着受令/将来効力の物理返報が不足しています");
        foreach(var f in x.Notices)Need(x.Dispatches.Any(d=>d.Id==f.Cancellation)&&(f.Phase is "late-receipt" or "effect")&&f.At>=0&&f.At<=w.ElapsedSeconds&&f.Revision>0&&f.Revision<=w.CommitSequence&&f.DueAt>f.At&&f.Origin!=null&&f.Destination!=null&&f.Route!=null&&EntityId.IsValid(f.Carrier.Value)&&w.Messaging.Messages.Any(m=>m.Id==f.Message),"取消の返報経路が不正です");
        Need(x.NoticeArrivals.Length==x.Notices.Count(f=>f.DueAt<=w.ElapsedSeconds),"取消返報の到達が不足しています");foreach(var a in x.NoticeArrivals)Need(x.Notices.Any(f=>f.Id==a.Id&&f.DueAt==a.At)&&a.At<=w.ElapsedSeconds&&a.Revision>0&&a.Revision<=w.CommitSequence,"取消返報の時点が不正です");
        Need(Same(x.Holdings,ExpectedCancellationHoldings(w)),"取消の保持紙/運搬者が違います");
    }
    static void ValidateCancellation(ContractWorld w,bool replay)
    {
        var s=w.NewMilitary!;var x=s.Cancellation;if(x==null||!replay)return;
        foreach(var q in s.Requests.Where(q=>CancellationAction(q.Input.Action))){var p=BeforeState(w,q.At,q.Revision-1);p.CommitSequence=q.Revision;Dispatch(p,q.Input);Need(Same(p.NewMilitary!.Cancellation,CancellationBefore(s,q.At,q.Revision))&&Same(UnitTransport(p),UnitTransport(BeforeState(w,q.At,q.Revision))),"取消の対象/原文/既知宛先を原入力から再現できません");}
        foreach(var g in x.Receives.Select(r=>(r.At,r.Revision)).Concat(x.Effects.Select(e=>(e.At,e.Revision))).Concat(x.Notices.Select(f=>(f.At,f.Revision))).Concat(x.NoticeArrivals.Select(a=>(a.At,a.Revision))).Distinct()){
            var p=BeforeState(w,g.At,g.Revision-1);p.CommitSequence=g.Revision;BeforeUnitMessages(p);MessageFlow.Advance(p.Messaging,p.ElapsedSeconds);UpdateRout(p);UpdateCancellation(p);UpdateWithdrawal(p);UpdateCancellationHoldings(p);UpdateCommandHolding(p);UpdateHeldDelivery(p);UpdateReplyRecovery(p);
            var expected=BeforeState(w,g.At,g.Revision);Need(Same(p.NewMilitary!.Cancellation,expected.NewMilitary!.Cancellation)&&Same(p.NewMilitary.Orders,expected.NewMilitary.Orders)&&Same(UnitTransport(p),UnitTransport(expected)),"取消の実受令/効力/対象だけの停止/物理返報を再現できません");
        }
        var current=ContractJson.Copy(w);UpdateCancellation(current);UpdateCancellationHoldings(current);Need(Same(current.NewMilitary!.Cancellation,x)&&Same(current.NewMilitary.Orders,s.Orders)&&Same(UnitTransport(current),UnitTransport(w)),"取消の更新が不足しています");
    }
}
