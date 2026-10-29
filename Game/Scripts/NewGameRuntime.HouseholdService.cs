using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowHouseholdService()
    {
        if(Session.HouseholdServiceView() is not {} v)return;var w=Snapshot();long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(520,330),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(500,0)};scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string label,Action run,bool disabled=false){var b=new Button{Text=label,Disabled=disabled};box.AddChild(b);b.Pressed+=run;}
        Text("個人の奉公と、家への所属");Text(v.Status);Text("この合意では家への所属・役職・土地・兵・未払いを移しません。旧主従が相互に残る場合は、元の奉公を個別に解いてから新しい奉公を合意します。");var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(HouseholdServiceInput i){var r=Session.ManageHouseholdService(i,Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowHouseholdService();}
        foreach(var b in v.Releases){Text(SuccessionPerson(w,b.Subject)+" → "+SuccessionPerson(w,b.Principal));Button("両者の同意で、この奉公だけを解く",()=>Run(new(new("request:house-personal-release-ui/"+shown),HouseholdServiceAction.Release,v.Owner!.Value,b.Other,b.Source)),!b.CanRelease);}
        if(v.Owner is {} owner){var head=HouseSuccession.Head(w,owner);Button(SuccessionPerson(w,head)+"への個人奉公を新たに合意する",()=>Run(new(new("request:house-personal-pledge-ui/"+shown),HouseholdServiceAction.Pledge,owner,head)),!v.CanPledge);}
        Text("当事者の現地立会いと同意、軍務・返報の引継ぎが必要です。行政と軍務は、それぞれの原本で確認します。");Button("家督・引継ぎへ戻る",()=>{popup.Hide();ShowHouseSuccession();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(570,460));
    }
}
