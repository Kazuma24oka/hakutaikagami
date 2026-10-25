using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    Label transportSummary=null!;
    void BuildTransportWork(VBoxContainer stack)
    {
        transportSummary=new(){Visible=false,AutowrapMode=TextServer.AutowrapMode.WordSmart};stack.AddChild(transportSummary);
        Add(stack,"work-transport","小口送金の運搬",ShowTransportWork);buttons["work-transport"].CustomMinimumSize=new(0,42);
    }
    TransportJob? CurrentTransport()=>TransportWork.Jobs(Snapshot()).LastOrDefault();
    void RefreshTransportWork()
    {
        var j=CurrentTransport();var offer=TransportWork.OfferAt(Snapshot(),new("job:transport-preview"));
        bool visible=j!=null||offer!=null;transportSummary.Visible=buttons["work-transport"].Visible=visible;buttons["work-transport"].Disabled=IsSaving||IsClockControlling||IsDeparting;
        if(j!=null){var t=j.Handover.Terms;transportSummary.Text="運搬："+TransportPhase(TransportHandover.Validate(j.Handover).Phase)+" / 預り銭 "+Snapshot().ResourceLots.Single(l=>l.Id==TransportWork.Cargo(t.Id)).Quantity+"文（私財と別）";}else transportSummary.Text="現地の小口送金を相談できます。";
    }
    static string TransportPhase(HandoverPhase p)=>p switch{HandoverPhase.Offered=>"条件確認",HandoverPhase.Accepted=>"荷物の引受待ち",HandoverPhase.Carrying=>"届け先へ運搬中",HandoverPhase.Delivered=>"荷主へ報告",HandoverPhase.Reported=>"運賃未受領",HandoverPhase.Paid=>"完了",HandoverPhase.ReturnRequired=>"荷物の返却待ち",HandoverPhase.Returned=>"返却済み",_=>"取消済み"};
    void ShowTransportWork()
    {
        if(IsSaving||IsClockControlling||IsDeparting)return;
        var j=CurrentTransport();
        if(j==null){var result=Session.RequestTransport(new("job:transport-ui/"+Session.Revision),Session.Revision);if(result.Code!=ResultCode.Accepted){message.Text="この拠点では運搬の依頼を準備できません。";return;}Refresh();j=CurrentTransport();}
        if(j==null)return;shownWork=null;var t=j.Handover.Terms;var v=TransportHandover.Validate(j.Handover);var source=Session;long revision=Session.Revision;
        string sourceName=Snapshot().Definitions.Sites.Single(s=>s.Id==t.Source).Name,destName=Snapshot().Definitions.Sites.Single(s=>s.Id==t.Destination).Name;
        string speech=v.Phase switch{HandoverPhase.Offered=>$"{PersonName(t.Recipient)}への預り銭を、{destName}まで運んでほしい。",HandoverPhase.Accepted=>"引き受けた荷物をここで預かろう。",HandoverPhase.Carrying=>$"{destName}の{PersonName(t.Recipient)}に渡そう。",HandoverPhase.Delivered=>$"受渡しは済んだ。{sourceName}の荷主へ報告しよう。",HandoverPhase.Reported=>"報告を確認した。運賃を受け取ってほしい。",HandoverPhase.Paid=>"運搬と運賃の受領まで済んだ。",_=>"預り銭の返却を確認しよう。"};
        bool carrierSpeaker=v.Phase!=HandoverPhase.Offered&&v.Phase!=HandoverPhase.Reported;
        PresentWork("小口送金の運搬",speech,$"荷物　銭{t.Amount}文（所有は{PersonName(t.Issuer)}）\n届け先　{destName} / {PersonName(t.Recipient)}\n運賃　報告後に{t.Fee}文。私財での立替なし。",t.Issuer,carrierSpeaker?Session.PlayerControl:t.Issuer,TransportPhase(v.Phase),"次の行動");workCardHeading.Text="固定した運搬条件";
        void Step(string id,string words,HandoverAction action)
        {
            Choice(id,words,()=>{
                if(!ReferenceEquals(Session,source)||Session.Revision!=revision)throw new InvalidOperationException("状況が変わりました。運搬条件を読み直してください。");
                var op=new EntityId("operation:transport-ui/"+Session.Revision);var input=new HandoverInput(op,action,action==HandoverAction.Cancel?t.Issuer:Session.PlayerControl,action==HandoverAction.Deliver?t.Destination:t.Source,action==HandoverAction.Deliver?t.Recipient:action==HandoverAction.Cancel?t.Carrier:t.Issuer,action is HandoverAction.Collect or HandoverAction.Deliver or HandoverAction.Pay or HandoverAction.Return?new EntityId("transfer:transport/"+op.Value.Replace(':','/')):null,Session.Seconds,v.Revision);
                var result=Session.HandoverTransport(t.Id,input,revision);if(result.Code!=ResultCode.Accepted)throw new InvalidOperationException("この場では受渡しできません。相手・拠点・期限を確認してください。");CloseBottomTalk();Refresh();ShowTransportWork();
            });
        }
        bool At(EntityId site,EntityId other)=>Session.GetNearbyActors(Session.PlayerControl).Any(p=>p.Actor==other)&&Snapshot().Locations.Single(l=>l.Actor==Session.PlayerControl) is {Kind:LocationKind.AtSite,Site:var here}&&here==site;
        if(v.Phase==HandoverPhase.Offered&&At(t.Source,t.Issuer)&&Session.Seconds<=t.AcceptBy)Step("transport-accept","この条件で引き受けよう",HandoverAction.Accept);
        if(v.Phase==HandoverPhase.Accepted&&At(t.Source,t.Issuer)&&Session.Seconds<=t.DeliverBy)Step("transport-collect","預り銭を受け取ろう",HandoverAction.Collect);
        if(v.Phase==HandoverPhase.Carrying&&At(t.Destination,t.Recipient)&&Session.Seconds<=t.DeliverBy)Step("transport-deliver","指定の相手に渡そう",HandoverAction.Deliver);
        if(v.Phase==HandoverPhase.Delivered&&At(t.Source,t.Issuer))Step("transport-report","荷主に受渡しを報告しよう",HandoverAction.Report);
        if(v.Phase==HandoverPhase.Reported&&At(t.Source,t.Issuer))Step("transport-pay","運賃を受け取ろう",HandoverAction.Pay);
        if((v.Phase is HandoverPhase.Offered or HandoverPhase.Accepted||v.Phase==HandoverPhase.Carrying&&Session.Seconds<=t.DeliverBy)&&At(t.Source,t.Issuer))Step("transport-cancel","ここで中止を相談しよう",HandoverAction.Cancel);
        if((v.Phase==HandoverPhase.ReturnRequired||v.Phase==HandoverPhase.Carrying&&Session.Seconds>t.DeliverBy)&&At(t.Source,t.Issuer))Step("transport-return","荷主へ預り銭を返そう",HandoverAction.Return);
        if(v.Phase==HandoverPhase.Carrying){Choice("transport-go","届け先を行先に設定する",()=>{CloseBottomTalk();SetDestination(NewGameWorld.Node(t.Destination.Value));});}
        if(v.Phase is HandoverPhase.Delivered or HandoverPhase.ReturnRequired){Choice("transport-home","荷主の拠点を行先に設定する",()=>{CloseBottomTalk();SetDestination(NewGameWorld.Node(t.Source.Value));});}
        if(v.Phase is HandoverPhase.Paid or HandoverPhase.Returned or HandoverPhase.Cancelled&&TransportWork.OfferAt(Snapshot(),new("job:next-preview"))!=null)
            Choice("transport-next","次の運搬を相談しよう",()=>{CloseBottomTalk();var result=Session.RequestTransport(new("job:next-transport/"+Session.Revision),Session.Revision);if(result.Code!=ResultCode.Accepted)throw new InvalidOperationException("今は新しい運搬を相談できません。");Refresh();ShowTransportWork();});
        Choice("transport-close","いったん戻る",CloseBottomTalk);
    }
    internal string ReviewTransportSummary=>transportSummary.Text;
}
