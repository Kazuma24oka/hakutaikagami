using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowRegionalTrade()
    {
        if(MaterialMarkets.Enabled(Snapshot())){ShowMaterialTrade();return;}
        var v=Session.RegionalTradeView();if(v==null){message.Text="現地の商人に会ってから取引してください。";return;}long shown=Session.Revision;
        var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(500,300)};panel.AddChild(box);
        box.AddChild(new Label{Text=NewGameWorld.SiteName(v.Site.Value)+" — 米の取引"});box.AddChild(new Label{Text=$"現地の売値 {v.BuyPrice}文/kg・買取 {v.SellPrice}文/kg\n自由在庫 {v.RiceAvailable}kg　商人の自由支払資金 {v.CashAvailable}文\n本人の使える私財 {v.PrivateAvailable}文（引当分を除く）"});
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(TradeAction action,EntityId? contract=null,EntityId? dest=null,long amount=0){var result=Session.TradeRice(new(new("request:trade-ui/"+shown),action,v.Site,contract,dest,amount),Session.PlayerControl,shown);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=result.VisibleReason;return;}popup.Hide();message.Text=result.VisibleReason;Refresh();ShowRegionalTrade();}
        void AddChoice(string text,TradeAction action,EntityId? contract=null){var b=new Button{Text=text};box.AddChild(b);b.Pressed+=()=>Run(action,contract);}
        long received=HouseSalary.Enabled(Snapshot())?HouseSalary.PrivateRice(Snapshot(),Session.PlayerControl):0;
        var d=v.Deals.LastOrDefault(x=>x.Phase is not(TradePhase.Cancelled or TradePhase.Expired or TradePhase.Settled or TradePhase.Supplied));
        if(d==null){if(received>0){var ownAmount=new SpinBox{MinValue=1,MaxValue=Math.Min(received,RegionalTrade.Capacity),Step=1,Value=Math.Min(20,received),Suffix=" kg（受領済み私有米）"};box.AddChild(ownAmount);var ownSale=new Button{Text="受領した私有米をこの商人に持ち込む"};box.AddChild(ownSale);ownSale.Pressed+=()=>Run(TradeAction.OfferOwnRice,dest:v.Site,amount:(long)ownAmount.Value);}var amount=new SpinBox{MinValue=1,MaxValue=RegionalTrade.Capacity,Step=1,Value=20,Suffix=" kg"};box.AddChild(amount);var sites=CommunityEconomy.Sites.Where(s=>s!=v.Site).ToArray();var to=new OptionButton();foreach(var site in sites)to.AddItem(NewGameWorld.SiteName(site.Value));box.AddChild(to);var quote=new Button{Text="現地の購入条件を聞く（まだ支払わない）"};box.AddChild(quote);quote.Pressed+=()=>Run(TradeAction.Quote,dest:sites[to.Selected],amount:(long)amount.Value);}
        else{
            var names=new[]{"条件確認中","米と銭を引当済み","私有米を携行中","買取条件を確認中","納入済み・代金未受領","精算済み","取消済み","引当期限切れ","行先取りやめ・米は携行中","家の蔵へ供給済み"};
            box.AddChild(new Label{Text=$"{names[(int)d.Phase]}：{d.Amount}kg\n{NewGameWorld.SiteName(d.Source.Value)} → {NewGameWorld.SiteName(d.Destination.Value)}\n購入代金 {d.Amount*d.PurchasePrice}文"});
            if(d.Phase==TradePhase.Offered&&v.Site==d.Source)AddChoice("この条件で米と代金を引き当てる（見積から1日以内）",TradeAction.Accept,d.Id);
            if(d.Phase==TradePhase.Accepted&&v.Site==d.Source)AddChoice("代金を払い、米を受け取る（見積から7日以内）",TradeAction.Collect,d.Id);
            if(d.Phase is TradePhase.Carrying or TradePhase.ReturnRequired){if(v.Site==d.Source||d.Phase!=TradePhase.ReturnRequired&&v.Site==d.Destination)AddChoice("この商人に買取条件を聞く",TradeAction.OfferSale,d.Id);if(d.Phase==TradePhase.Carrying&&Session.CommunityEconomyView()!=null)AddChoice("私有米をこの家の蔵へ入れる（私財への返金なし）",TradeAction.SupplyHouse,d.Id);}
            if(d.Phase==TradePhase.SaleQuoted&&d.SaleSite==v.Site){box.AddChild(new Label{Text=$"買取代金 {d.Amount*d.SalePrice}文（見積から1日以内）"});AddChoice("米を納める（代金はまだ受け取らない）",TradeAction.Deliver,d.Id);}
            if(d.Phase==TradePhase.Delivered&&d.SaleSite==v.Site)AddChoice($"納入済みの売却代金 {d.Amount*d.SalePrice}文を受け取り精算する",TradeAction.Settle,d.Id);
            if(d.Phase is TradePhase.Offered or TradePhase.Accepted or TradePhase.Carrying or TradePhase.SaleQuoted)AddChoice("取引・行先を取りやめる（受取済みの米は消えない）",TradeAction.Cancel,d.Id);
        }
        var reload=new Button{Text="現地の条件を読み直す"};box.AddChild(reload);reload.Pressed+=()=>{popup.Hide();ShowRegionalTrade();};
        box.AddChild(new Label{Text="米は本人と一緒に既存の道を移動します。遠隔売却はできません。\n取引価格・在庫は試作値。家の米・軍用備蓄と自動共有しません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(580,490));
    }
}
