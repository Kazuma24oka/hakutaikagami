using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowWorkAssessment(EntityId? selected=null)
    {
        var cards=AssessmentNotice.Inbox(Session,Session.PlayerControl).Where(c=>c.Known!=null).OrderByDescending(c=>c.Known!.Version).ThenByDescending(c=>Snapshot().Messaging.Messages.Single(m=>m.Id==c.Message.Value).CreatedAt).ToArray();if(cards.Length==0)return;
        var card=selected is {} wanted?cards.Single(c=>c.Message==wanted):cards.FirstOrDefault(c=>WorkProgression.Completed(c.Known!.Review.Achievement)&&NewGameWork.Reward(Snapshot(),Snapshot().Tasks.Single(t=>t.Id==c.Known.Task))>0&&!NewGameWork.Paid(Snapshot(),c.Known.Task))??cards[0];
        var p=card.Known!;var task=Snapshot().Tasks.Single(t=>t.Id==p.Task);bool completed=WorkProgression.Completed(p.Review.Achievement);long fee=completed?NewGameWork.Reward(Snapshot(),task):0;bool paid=NewGameWork.Paid(Snapshot(),task.Id);shownWork=null;
        var nextOffer=TransportWork.OfferAt(Snapshot(),new("job:progress-preview"));bool nextJob=nextOffer?.Issuer==p.Assessor&&nextOffer.Amount==200;
        PresentWork("依頼主からの返答",p.Review.Achievement==WorkProgression.Achievement?nextJob?"点検の成果を認める。今度は銭200文の運搬も任せたい。":"点検の成果を認める。これからも仕事を頼みたい。":completed?fee>0?paid?"報告は受け取った。約束の手当も渡し終えたな。":"報告と陸路の記録を受け取った。約束の手当を現地で渡そう。":"報告と陸路の記録を受け取った。ご苦労だった。":"今回は完了の報告にはなっていないな。用件の結果は受け取った。",fee>0?paid?"報酬：銭20文 受領済み":"報酬：銭20文 未受領（依頼主と現地で受取）":"別払いなし",p.Assessor,p.Assessor,"返答","次の行動");
        workCardHeading.Text=NewGameWorld.SiteName(task.Scope.Value);renderingWorkReply=true;workReplyChoice.Clear();workReplyIds=cards.Select(c=>c.Message).ToArray();for(int n=0;n<cards.Length;n++){var q=cards[n].Known!;var t=Snapshot().Tasks.Single(t=>t.Id==q.Task);long at=Snapshot().Messaging.Messages.Single(m=>m.Id==cards[n].Message.Value).CreatedAt;workReplyChoice.AddItem(PersonName(q.Assessor)+" ／開始"+at+"秒の返答 ／"+(NewGameWork.Reward(Snapshot(),t)>0&&WorkProgression.Completed(q.Review.Achievement)?NewGameWork.Paid(Snapshot(),t.Id)?"受領済み":"報酬未受領":"別払いなし"));}workReplyChoice.Select(Array.IndexOf(workReplyIds,card.Message));workReplyChoice.Show();renderingWorkReply=false;LayoutBottomTalk();
        if(fee>0&&!paid){Choice("work-pay","約束の報酬を受け取ろう",()=>{Submit(Session.CollectWorkPayment(new("work-payment:ui/"+Session.Revision),task.Id,Session.PlayerControl,Session.Revision));ShowWorkAssessment(card.Message);});buttons["work-pay"].Disabled=!NewGameWork.CanRequest(Session,task.Id);buttons["work-pay"].TooltipText="依頼主と同じ拠点に戻って受け取ります。";}
        bool replied=Snapshot().Messaging.Messages.Any(m=>m.Kind==AssessmentNotice.ResponseKind&&m.SenderId==Session.PlayerControl.Value&&m.Body.Contains(card.Message.Value,StringComparison.Ordinal));
        if(!replied)Choice("work-received","返答を承知したと伝える",()=>{Submit(Session.RespondAssessment(card.Message,Session.PlayerControl,AssessmentNotice.Reply.Received,Session.Revision));CloseBottomTalk();});
        if(WorkDuty.Candidate(Snapshot()) is {} dutyPrincipal)Choice("work-duty-offer","今後の役目について聞こう",()=>ShowWorkDutyOffer(dutyPrincipal));
        else if(nextJob&&(fee==0||paid))Choice("work-next-transport","次の運搬の話を聞こう",()=>{CloseBottomTalk();Submit(Session.RequestTransport(new("job:earned-ui/"+Session.Revision),Session.Revision));ShowTransportWork();});
        Choice("work-reply-later","地図へ戻る",CloseBottomTalk);
    }
    string[] KnownFailedWorkLetters(){var w=Snapshot();if(!NewGameWork.Enabled(w))return [];return w.Messaging.Messages.Where(m=>m.SenderId==Session.PlayerControl.Value&&m.Kind is "route-observation" or "contract-report" or "assessment-notice" or "assessment-response").Where(m=>NewGameWork.SupportsRetry(w,m,Session.PlayerControl,NewGameWorld.Node(m.Scope))).Where(m=>{var a=w.Messaging.Attempts.Where(a=>a.MessageId==m.Id).MaxBy(a=>a.Number);return a!=null&&HakutaiKagami.Core.Messaging.MessageFlow.FailureKnown(w.Messaging,a.Id,Session.PlayerControl.Value);}).Select(m=>m.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();}
    void RetryKnownWorkLetter(){var id=KnownFailedWorkLetters().FirstOrDefault();if(id==null)return;var m=Snapshot().Messaging.Messages.Single(m=>m.Id==id);Submit(Session.RetryCourier(Session.PlayerControl,id,NewGameWorld.Node(m.Scope),Session.Seconds+1,Session.Revision));}
    void RefreshWorkCycleGuidance(VisibleTask? task,int unread)
    {
        if(!NewGameWork.Enabled(Snapshot()))return;
        var known=AssessmentNotice.LatestKnown(Session,Session.PlayerControl);bool unpaid=known.Any(p=>WorkProgression.Completed(p.Review.Achievement)&&NewGameWork.Reward(Snapshot(),Snapshot().Tasks.Single(t=>t.Id==p.Task))>0&&!NewGameWork.Paid(Snapshot(),p.Task));
        buttons["work-receipts"].Text=unpaid?"返答を見て報酬を受け取る":"これまでの返答を見る";
        if(unread>0||IsSaving||IsClockControlling||task==null||!task.KnownStatus.Contains("Completed"))return;
        if(task.Issuer==Session.PlayerControl){workSummary.Text="自主点検を終えました。\n結果を見て、次の行先を選べます。";return;}
        var receipt=known.SingleOrDefault(p=>p.Task==task.Id);if(receipt==null)return;
        workSummary.Text=NewGameWork.Reward(Snapshot(),Snapshot().Tasks.Single(t=>t.Id==task.Id))>0&&!NewGameWork.Paid(Snapshot(),task.Id)?"依頼主の返答を確認済み。\n次は、現地で報酬を受け取る。":"仕事を終えました。\n次の仕事か行先を選べます。";
    }
    string ReadableWorkLetter(VisibleMessage letter)
    {
        var m=Snapshot().Messaging.Messages.Single(m=>m.Id==letter.Id.Value);
        return m.Kind switch{AssessmentNotice.Kind=>"依頼主から結果への返答",AssessmentNotice.ResponseKind=>"返答を承知した旨の書状","route-observation"=>letter.KnownBody!.StartsWith("route-block:")?"通行不可の点検記録":"通行可の点検記録","delivery-failure"=>"配送不達の報告","service-notice"=>"仕官に関する返答",_=>m.Kind.StartsWith("new-military-",StringComparison.Ordinal)?"軍務に関する書状":letter.KnownBody!};
    }
    internal long ReviewPrivateCoins=>NewGameWork.Balance(Snapshot(),Session.PlayerControl);
}
