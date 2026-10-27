using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowHouseTreasury()
    {
        var v=Session.HouseTreasuryView();if(v==null)return;long shown=Session.Revision;
        var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();popup.AddChild(panel);panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));
        var scroll=new ScrollContainer{CustomMinimumSize=new(490,330),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(480,0)};scroll.AddChild(box);
        box.AddChild(new Label{Text="家の資金・現地調達 — "+NewGameWorld.SiteName(v.Site.Value)});
        box.AddChild(new Label{Text=$"家の資金 {v.HouseCash}文 / 本人の私財 {v.PrivateCash}文\n今月予算 {v.MonthlyBudget}文 / 操作できる残枠 {v.Remaining}文\n家の木材 {v.Materials.Wood}kg / 武具 {v.Materials.Weapons}組",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        box.AddChild(new Label{Text="商人の現地在庫を家の蔵へ受け取り、その場で家の資金から支払います。遠隔調達・借金・兵の生成はしません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(TreasuryAction action,string? kind=null,long quantity=0,long amount=0){var result=Session.ManageHouseTreasury(new(new("request:house-ui/"+shown),action,v.Site,kind,quantity,amount),Session.PlayerControl,shown);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowHouseTreasury();}
        void Button(string label,Action run,bool disabled=false){var b=new Button{Text=label,Disabled=disabled};box.AddChild(b);b.Pressed+=run;}
        if(v.CanSetBudget){var budget=new SpinBox{MinValue=0,MaxValue=1000,Step=10,Value=v.MonthlyBudget,Suffix=" 文/月"};box.AddChild(budget);Button("家全体の月次調達予算を設定",()=>Run(TreasuryAction.SetBudget,amount:(long)budget.Value));var fund=new SpinBox{MinValue=1,MaxValue=Math.Max(1,v.PrivateCash),Step=1,Value=Math.Min(100,Math.Max(1,v.PrivateCash)),Suffix=" 文"};box.AddChild(fund);Button("本人の自由な私財から家へ拠出",()=>Run(TreasuryAction.Fund,amount:(long)fund.Value),v.PrivateCash<=0);}
        else box.AddChild(new Label{Text="代理官の調達上限は月200文。予算変更・私財拠出はできません。"});
        var goods=new OptionButton();goods.AddItem("米（kg）");goods.AddItem("木材（kg）");goods.AddItem("武具（組）");box.AddChild(goods);var quantity=new SpinBox{MinValue=1,MaxValue=1000,Step=1,Value=10};box.AddChild(quantity);
        var prices=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(prices);void Quote(){var trade=Session.RegionalTradeView();if(trade==null)return;string? kind=goods.Selected==0?null:goods.Selected==1?"wood":"weapons";long price=trade.BuyPrice;if(kind!=null&&trade.Goods!=null)foreach(var g in trade.Goods)if(g.Kind==kind)price=g.BuyPrice;prices.Text=$"単価 {price}文 / 合計 {price*(long)quantity.Value}文（確認後に在庫・予算を再検査）";}goods.ItemSelected+=_=>Quote();quantity.ValueChanged+=_=>Quote();Quote();
        Button("現地で受領・支払を確定",()=>Run(TreasuryAction.Buy,goods.Selected==0?null:goods.Selected==1?"wood":"weapons",(long)quantity.Value),!v.CanBuy);Button("読み直す",()=>{popup.Hide();ShowHouseTreasury();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(550,530));
    }
}
