using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowCampSupply()
    {
        var w=Snapshot();if(!FieldConvoy.Enabled(w)||w.NewMilitary==null)return;long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();popup.AddChild(panel);panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));var scroll=new ScrollContainer{CustomMinimumSize=new(500,360),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(490,0)};scroll.AddChild(box);box.AddChild(new Label{Text="野営地への現物補給"});box.AddChild(new Label{Text="現行の拠点周囲120mの戦場が対象です。出陣前に現地の家臣へ米と集合地点を任せ、部隊が合流すると受け取れます。本人・部隊の容量が足りなければ待機し、1日後には未使用米を持って帰還します。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Button(string label,Action run,bool disabled=false){var b=new Button{Text=label,Disabled=disabled};box.AddChild(b);b.Pressed+=run;}
        void Run(CampConvoyInput input){var r=Session.ManageCampConvoy(input,Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCampSupply();}
        var local=Session.MilitaryEconomyView();if(local is {} v&&v.CanFund&&w.NewMilitary.Terms.Principal==Session.PlayerControl&&NewGameMilitary.CustodyAt(w,w.ElapsedSeconds,w.CommitSequence+1)&&!NewGameMilitary.Deployed(w)){
            var people=NpcFreight.Candidates(w,v.Site);if(people.Length>0){var who=new OptionButton();foreach(var p in people)who.AddItem(PersonName(p));box.AddChild(who);var east=new SpinBox{MinValue=-120,MaxValue=120,Step=1,Value=30,Suffix=" m：拠点から東（西は負数）"};var north=new SpinBox{MinValue=-120,MaxValue=120,Step=1,Value=-10,Suffix=" m：拠点から北（南は負数）"};var kg=new SpinBox{MinValue=1,MaxValue=Math.Min(100,v.HouseFreeKg),Step=1,Value=Math.Min(10,v.HouseFreeKg),Suffix=" kg：家の未引当米"};box.AddChild(east);box.AddChild(north);box.AddChild(kg);Button("この家臣・集合地点・米で現地合意する",()=>{var h=NewGameMilitary.Home(w,0);Run(new(new("request:camp-supply-ui/"+shown),CampConvoyAction.Dispatch,Carrier:people[who.Selected],Camp:new(h.X+(long)east.Value*1000,h.Y+(long)north.Value*1000),RiceKg:(long)kg.Value));},v.HouseFreeKg<=0);}
        }
        foreach(var card in Session.CampConvoyView().Reverse().Take(4)){var h=NewGameMilitary.Home(w,0);box.AddChild(new Label{Text=$"担当：{PersonName(card.Carrier)} / 合意した米 {card.AgreedKg}kg\n集合地点：拠点から東{(card.Camp.X-h.X)/1000m:0}m・北{(card.Camp.Y-h.Y)/1000m:0}m\n{card.Status}",AutowrapMode=TextServer.AutowrapMode.WordSmart});Button("本人に会って取りやめを伝える",()=>Run(new(new("request:camp-cancel-ui/"+shown),CampConvoyAction.CancelAtMeeting,card.Job)),!card.CanCancel);}
        Button("読み直す",()=>{popup.Hide();ShowCampSupply();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(560,520));
    }
}
