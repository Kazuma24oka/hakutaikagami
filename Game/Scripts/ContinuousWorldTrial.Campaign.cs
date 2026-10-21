using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    void BuildCampaignUI(HFlowContainer row)
    {
        foreach(var id in new[]{"own-line","own-return"}){var b=buttons[id];b.GetParent().RemoveChild(b);row.AddChild(b);}
    }
    void RefreshCampaignControls(ContractWorld w)
    {
        bool active=w.Region!.Active;buttons["site"].Visible=true;buttons["site"].Text=active?"本隊を収めて在所へ":"本隊を率いて野外へ";
        buttons["site"].Disabled=active?w.Region.Units.Where(u=>u.Commander==S.PlayerControl).Any(u=>u.Position!=u.Home||u.Route.Any(l=>l.EndAt>w.ElapsedSeconds)):!CreatedCampaign.CanDeploy(w)||OfficerLoop.Busy(w,S.PlayerControl);
        buttons["depart"].Disabled=active||OfficerLoop.Busy(w,S.PlayerControl);
        buttons["own-line"].Visible=buttons["own-return"].Visible=active;
        buttons["own-line"].Disabled=!CreatedCampaign.CanCommand(w,new(new("request:campaign-preview"),S.PlayerControl,S.PlayerControl,RegionalAction.Move,new(0,0),new(10000,0)),w.ElapsedSeconds);
        if(!active){workDispatcher.Visible=false;buttons["work-send"].Visible=false;workRole.Text="本人："+w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name+"（作成・織田家の本隊配置任務）";var inbox=RegionalWork.Inbox(S);workText.Text=inbox.Length==0?"本人の受信なし / 在所で仕事・出陣を選択":string.Join(" / ",inbox.TakeLast(2).Select(c=>MissionText(c.Text)));details.Text="本隊は清洲で待機 / 許可：配置・帰還。交戦・他隊命令の許可なし。";buttons["work-open"].Visible=buttons["work-accept"].Visible=buttons["work-decline"].Visible=true;buttons["retainer-interrupt"].Visible=buttons["retainer-explain"].Visible=true;}
    }
    void RefreshCampaignWork(ContractWorld w)
    {
        workDispatcher.Visible=false;buttons["work-send"].Visible=false;councilNotice.Visible=false;
        var inbox=RegionalWork.Inbox(S);buttons["work-open"].Visible=inbox.Any(c=>c.CanOpen);buttons["work-open"].Disabled=!inbox.Any(c=>c.CanOpen);
        buttons["work-accept"].Visible=buttons["work-decline"].Visible=inbox.Any(c=>c.CanDecide);buttons["work-accept"].Disabled=buttons["work-decline"].Disabled=!inbox.Any(c=>c.CanDecide);
        RefreshRetainerUI();buttons["retainer-interrupt"].Visible=KnownRetainerTask()!=null;buttons["retainer-explain"].Visible=OwnInterruptedTask()!=null;
        workRole.Text="本人："+w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name+"（作成・本隊配置任務）";
        workText.Text=(inbox.Length==0?"未着の書状内容は表示しません。":string.Join("\n",inbox.TakeLast(2).Select(c=>MissionText(c.Text))));
        RefreshCampaignControls(w);
    }
}
