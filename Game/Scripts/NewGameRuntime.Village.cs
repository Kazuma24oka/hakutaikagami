using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowVillage()
    {
        var v=Session.VillageView();if(v==null){message.Text="村との相談は現地で行います。";return;}long shown=Session.Revision;
        var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);
        var scroll=new ScrollContainer{CustomMinimumSize=new(500,350),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(490,0)};scroll.AddChild(box);
        box.AddChild(new Label{Text=NewGameWorld.SiteName(v.Site.Value)+" — 村との相談"});
        box.AddChild(new Label{Text=v.Representative+"「"+v.Reaction+"」",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(VillageAction action,EntityId? proposal=null,long amount=0){var r=Session.ConsultVillage(new(new("request:village-ui/"+shown),action,v.Site,proposal,amount),Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowVillage();}
        var amount=new SpinBox{MinValue=1,MaxValue=3000,Step=1,Value=200,Suffix=" kg"};box.AddChild(amount);
        void Button(string text,Action action,bool disabled=false){var b=new Button{Text=text,Disabled=disabled};box.AddChild(b);b.Pressed+=action;}
        Button("私：この量の米で救済を相談する",()=>Run(VillageAction.ProposeRelief,amount:(long)amount.Value),!v.CanPromise);
        Button("私：水利工事への協力を相談する（200kg・20人）",()=>Run(VillageAction.ProposeWater,amount:200),!v.CanPromise);
        var q=v.Proposals.LastOrDefault();if(q!=null){box.AddChild(new Label{Text=(q.Term%2==0?"村役人":"水利世話人")+"の返答（相談時点）：\n"+q.Reply,AutowrapMode=TextServer.AutowrapMode.WordSmart});Button("私：この返答の条件で約束する",()=>Run(VillageAction.Confirm,q.Id),!q.Agreed||Session.Seconds>q.At+86400||v.Term!=q.Term||v.Promises.Any(p=>p.Id==q.Id));}
        foreach(var c in v.Promises){box.AddChild(new Label{Text=$"{c.Kind}：{c.Status}\n受渡し/着手 {c.Delivered}/{c.Amount}kg",AutowrapMode=TextServer.AutowrapMode.WordSmart});bool pending=c.Delivered<c.Amount&&Session.Seconds<=c.DeliverBy&&c.Status=="約束の履行待ち";Button(c.Kind=="水利工事"?"合意した工事に着手する":"上の量を現地で渡す",()=>Run(VillageAction.Deliver,c.Id,c.Kind=="水利工事"?200:(long)amount.Value),!v.CanPromise||!pending);Button("未履行分の終了を伝える",()=>Run(VillageAction.Cancel,c.Id),!v.CanPromise||c.Delivered>=c.Amount||c.Status.Contains("終了"));}
        if(!v.CanPromise)box.AddChild(new Label{Text="この家の処分権がないため、米や労働の約束はできません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        Button("相談を読み直す",()=>{popup.Hide();ShowVillage();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(560,500));
    }
}
