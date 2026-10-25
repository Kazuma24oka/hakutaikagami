using Godot;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    internal bool LifeView {get;set;}
    internal void HubRefresh()=>Refresh();
    void RefreshLifeUI(ContractWorld w)
    {
        foreach(var id in new[]{"region","follow","quality","skin","one"})if(buttons.TryGetValue(id,out var hidden))hidden.Visible=false;
        buttons["nation"].Text="全国を見る";buttons["near"].Text="現在地へ";buttons["rotate"].Text="視点を回す";buttons["ten"].Text="10秒進める";buttons["auto"].Text=automatic?"自動進行をやめる":"自動で時間を進める";buttons["resume"].Text=S.Paused?"停止を解除":"時間を停止";
        buttons["depart"].Text="この行先へ出発";buttons["load"].Text="保存から再開";buttons["own-line"].Text="本隊を配置する";buttons["own-return"].Text="本隊を帰還させる";
        status.Text="世界時間：開始から"+S.Seconds+"秒 / "+(S.Paused?"停止中":automatic?"自動で進行中":"手動で進行");
        if(notice.Text.StartsWith("地面を左ドラッグ"))notice.Text="届いた書状を開き、状況に合う行動を選びます。時間を進めると返事や任務が進みます。";
        var role=Career.View(S,S.PlayerControl);var military=CareerMilitary.View(S,S.PlayerControl);string name=w.Definitions.Characters.Single(c=>c.Id==S.PlayerControl).Name;
        workRole.Text=name+" / "+role.Role.Replace("（本人が通知を認識）","").Replace("（本人の認識）","");workRole.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        workText.Text="身分："+(role.Cases.LastOrDefault()?.Status??"仕官の申し出前");if(w.Military!.Requests.Length>0)workText.Text+="\n軍務："+military.Status;var inbox=RegionalWork.Inbox(S);if(inbox.Length>0)workText.Text+="\n"+MissionText(inbox.Last().Text);workText.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        bool deployed=FieldPresence.PlayerDeployed(w);
        if(!deployed&&span<=240)foreach(var label in labels.Values)label.Visible=false;
        if(military.Custody){double days=FieldSupply.Carried(w).Quantity/(100d*FieldSupply.PersonDay);details.Text="兵糧：約"+days.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"日分（100人分） / 帰還後は現況を報告し、残る兵糧と指揮を返します。";}
        else if(military.CanPickup)details.Text="許可を読んだ後、清洲で指揮を引き受けます。所属しただけでは部隊は動かせません。";
        else details.Text="返事や命令は届いた書状で確かめます。行先を選んで移動し、同じ人物で人生を続けられます。";
    }
}
