using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    void MilitaryDecision(MilitaryAction action)=>Require(S.DecideMilitary(new(new("request:military-ui/"+S.Revision),action),S.PlayerControl,S.Revision));
    void BuildMilitaryUI(HFlowContainer row)
    {
        Add(row,"military-retry","軍務の書状を再送",()=>{var card=CareerMilitary.Retryable(S,S.PlayerControl)!;Require(S.RetryMilitaryRequest(card.Message,card.Attempt,S.PlayerControl,S.Revision));});
        Add(row,"military-request","軍務を申し出る",()=>MilitaryDecision(MilitaryAction.Request));
        Add(row,"military-open","軍務の封書を開く",()=>Require(S.Open(S.PlayerControl,CareerMilitary.View(S,S.PlayerControl).Unopened!.Value,S.Revision)));
        Add(row,"military-accept","軍務条件に回答",()=>MilitaryDecision(MilitaryAction.Accept));
        Add(row,"military-pickup","現地で指揮を引受",()=>MilitaryDecision(MilitaryAction.Pickup));
        Add(row,"military-return","指揮の返還を願う",()=>MilitaryDecision(MilitaryAction.Return));
    }
    void RefreshMilitaryUI(ContractWorld w)
    {
        buttons["military-retry"].Visible=CareerMilitary.Retryable(S,S.PlayerControl)!=null;buttons["career-status"].Text="身分・軍務を照会";buttons["career-status"].Visible|=w.Military!.Requests.Length>0;
        var view=CareerMilitary.View(S,S.PlayerControl);bool local=FieldSupply.AtDepot(w)&&RegionalWork.NearbyDispatchers(S).Contains(RetainerStart.Lord),active=FieldPresence.PlayerDeployed(w);
        buttons["military-request"].Visible=view.CanRequest;buttons["military-request"].Disabled=!local;
        buttons["military-open"].Visible=view.Unopened!=null;buttons["military-accept"].Visible=view.CanAccept;
        buttons["military-pickup"].Visible=view.CanPickup;buttons["military-pickup"].Disabled=!local;
        buttons["military-return"].Visible=view.CanReturn;buttons["field-report"].Visible=view.Custody;buttons["field-report"].Disabled=w.Engagement!.Reports.Any(r=>r.At==S.Seconds);
        buttons["supply-pack"].Visible&=view.Custody&&!w.Military!.Requests.Any(q=>q.Input.Action==MilitaryAction.Return);buttons["supply-return"].Visible&=view.Custody;
        bool returning=w.Military!.Requests.Any(q=>q.Input.Action==MilitaryAction.Return);
        buttons["site"].Disabled|=returning;
        if(returning&&!active&&w.Engagement!.Reports.Any(r=>r.Sender==S.PlayerControl&&r.At>=System.Math.Max(w.Military.Requests.Single(q=>q.Input.Action==MilitaryAction.Pickup).At,w.Supply!.Sorties.LastOrDefault()?.Until??0)))buttons["field-report"].Visible=false;
        buttons["site"].Visible=view.Custody;buttons["depart"].Disabled=active||OfficerLoop.Busy(w,S.PlayerControl);
        workText.Text="身分："+(CareerCard?.Status??"浪人")+" / 軍務："+view.Status;
        var inbox=RegionalWork.Inbox(S);if(inbox.Length>0&&!active)workText.Text+="\n"+MissionText(inbox.Last().Text);
        details.Text=view.Custody?"主家兵100 / 配置・保持・帰還だけ / 携行兵糧"+(FieldSupply.Carried(w).Quantity/(double)FieldSupply.PersonDay).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"人日 / 帰還・残量返還・現況返報が引継ぎ条件":"同じ人物・世界で継続 / 所属と軍務は別 / 兵・物資の私有化なし / 交戦なし";
    }
}
