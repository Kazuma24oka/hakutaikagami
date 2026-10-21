using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    void BuildCampaignUI(HFlowContainer row)
    {
        foreach(var id in (EngagementStartEnabled?new[]{"own-line","own-return","own-engage"}:new[]{"own-line","own-return"})){var b=buttons[id];b.GetParent().RemoveChild(b);row.AddChild(b);}
        if(SupplyStartEnabled){Add(row,"supply-pack","兵糧を携行",()=>Require(S.HandleSupply(new("request:pack/"+S.Revision),S.PlayerControl,true,FieldSupply.Capacity-FieldSupply.Carried(S.DiagnosticSnapshot()).Quantity,S.Revision)));Add(row,"supply-return","兵糧残量を返還",()=>Require(S.HandleSupply(new("request:unpack/"+S.Revision),S.PlayerControl,false,FieldSupply.Carried(S.DiagnosticSnapshot()).Quantity,S.Revision)));}
        if(EngagementStartEnabled)Add(row,"field-report","自隊の現況を発送",()=>Require(S.ReportEngagement(new("request:field-report/"+S.Seconds),S.PlayerControl,S.Revision)));
    }
    void RefreshCampaignControls(ContractWorld w)
    {
        bool active=FieldPresence.PlayerDeployed(w);if(SupplyStartEnabled){bool local=FieldSupply.AtDepot(w);long amount=FieldSupply.Carried(w).Quantity;buttons["supply-pack"].Visible=local&&amount<FieldSupply.Capacity;buttons["supply-pack"].Disabled=!local||amount>=FieldSupply.Capacity||w.ResourceLots.Single(l=>l.Id==FieldSupply.Depot).Quantity<FieldSupply.Capacity-amount;buttons["supply-return"].Visible=local&&amount>0;buttons["supply-return"].Disabled=!local||amount==0;}if(EngagementStartEnabled){buttons["own-engage"].Visible=active;buttons["own-engage"].Disabled=!active||!EngagementMission.CanEngage(w,new(new("request:engage-preview"),S.PlayerControl,S.PlayerControl,RegionalAction.Engage,new(0,0),new(10000,0)),w.ElapsedSeconds);buttons["field-report"].Disabled=w.Engagement!.Reports.Any(r=>r.At==S.Seconds);}buttons["site"].Visible=true;buttons["site"].Text=active?"本隊を収めて在所へ":"本隊を率いて野外へ";
        buttons["site"].Disabled=active?w.Region!.Units.Where(u=>u.Commander==S.PlayerControl).Any(u=>u.Position!=u.Home||u.Route.Any(l=>l.EndAt>w.ElapsedSeconds)):!CreatedCampaign.CanDeploy(w)||OfficerLoop.Busy(w,S.PlayerControl);
        buttons["depart"].Disabled=active||OfficerLoop.Busy(w,S.PlayerControl);
        buttons["own-line"].Visible=buttons["own-return"].Visible=active;
        buttons["own-line"].Disabled=!CreatedCampaign.CanCommand(w,new(new("request:campaign-preview"),S.PlayerControl,S.PlayerControl,RegionalAction.Move,new(0,0),new(10000,0)),w.ElapsedSeconds);
        if(!active){workDispatcher.Visible=false;buttons["work-send"].Visible=false;workRole.Text="本人："+w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name+"（作成・織田家の本隊配置任務）";var inbox=RegionalWork.Inbox(S);workText.Text=inbox.Length==0?"本人の受信なし / 在所で仕事・出陣を選択":string.Join(" / ",inbox.TakeLast(2).Select(c=>MissionText(c.Text)));details.Text=EngagementStartEnabled?"架空演習 / 帰還兵"+w.Region!.Units.Where(u=>u.Commander==S.PlayerControl).Sum(u=>u.Soldiers)+" / 現況発送"+w.Engagement!.Reports.Length+"通（主君の現在の認識は表示しません）":"本隊は清洲で待機 / 許可：配置・帰還。交戦・他隊命令の許可なし。";buttons["work-open"].Visible=buttons["work-accept"].Visible=buttons["work-decline"].Visible=true;buttons["retainer-interrupt"].Visible=buttons["retainer-explain"].Visible=true;}if(EngagementStartEnabled)workRole.Text=workRole.Text.Replace("本隊配置任務","架空演習の交戦任務");if(SupplyStartEnabled){buttons["site"].Disabled|=!active&&FieldSupply.Carried(w).Quantity==0;string ration=(FieldSupply.Carried(w).Quantity/(double)FieldSupply.PersonDay).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);details.Text=active?details.Text+" / 兵糧"+ration+"人日":"主家の演習兵糧 / 携行"+ration+"人日 / 城在庫"+(w.ResourceLots.Single(l=>l.Id==FieldSupply.Depot).Quantity/FieldSupply.PersonDay)+"人日";}
    }
    void RefreshCampaignWork(ContractWorld w)
    {
        workDispatcher.Visible=false;buttons["work-send"].Visible=false;councilNotice.Visible=false;
        var inbox=RegionalWork.Inbox(S);buttons["work-open"].Visible=inbox.Any(c=>c.CanOpen);buttons["work-open"].Disabled=!inbox.Any(c=>c.CanOpen);
        buttons["work-accept"].Visible=buttons["work-decline"].Visible=inbox.Any(c=>c.CanDecide);buttons["work-accept"].Disabled=buttons["work-decline"].Disabled=!inbox.Any(c=>c.CanDecide);
        RefreshRetainerUI();buttons["retainer-interrupt"].Visible=KnownRetainerTask()!=null;buttons["retainer-explain"].Visible=OwnInterruptedTask()!=null;
        workRole.Text="本人："+w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name+"（作成・本隊配置任務）";
        workText.Text=(inbox.Length==0?"未着の書状内容は表示しません。":string.Join("\n",inbox.TakeLast(2).Select(c=>MissionText(c.Text))));
        RefreshCampaignControls(w);if(EngagementStartEnabled){workRole.Text=workRole.Text.Replace("本隊配置任務","架空演習の交戦任務");}
    }
}
