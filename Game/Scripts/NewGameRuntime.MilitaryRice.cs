using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowMilitaryRice()
    {
        var v=Session.MilitaryEconomyView();if(v==null)return;long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();popup.AddChild(panel);panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));var box=new VBoxContainer{CustomMinimumSize=new(490,320)};panel.AddChild(box);
        box.AddChild(new Label{Text=NewGameWorld.SiteName(v.Site.Value)+" — 家と軍用倉の兵糧"});box.AddChild(new Label{Text=$"軍用倉 {v.DepotPersonSeconds/(decimal)MilitaryEconomy.PersonSecondsPerKg:0.######}kg相当\n補給支援の地域労働 {v.SupportWorkers}人"});if(v.CanFund)box.AddChild(new Label{Text=$"家の自由米 {v.HouseFreeKg}kg（既存の給付引当を除く）"});box.AddChild(new Label{Text="家の米を渡してから、軍務の『積載』で部隊へ載せます。試作配給は兵1人1日0.6kg、100人なら1日60kg。遠隔補充はできません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});box.AddChild(new Label{Text="整数kgで受け渡します。消費後の端数は軍用倉に残り、返納で消えません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var amount=new SpinBox{MinValue=1,MaxValue=1000,Step=1,Value=60,Suffix=" kg"};box.AddChild(amount);var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(bool returning){var result=Session.ManageMilitaryRice(new(new("request:military-rice-ui/"+shown),v.Depot,(long)amount.Value,returning),Session.PlayerControl,shown);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowMilitaryRice();}
        void Button(string label,Action run,bool disabled=false){var b=new Button{Text=label,Disabled=disabled};box.AddChild(b);b.Pressed+=run;}
        Button("家の米を軍用倉へ渡す",()=>Run(false),!v.CanFund||v.HouseFreeKg<=0);Button("未使用の整数kgを家へ返す",()=>Run(true),!v.CanRefund);Button("読み直す",()=>{popup.Hide();ShowMilitaryRice();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(550,470));
    }
}
