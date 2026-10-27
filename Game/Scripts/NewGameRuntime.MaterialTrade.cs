using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowMaterialTrade()
    {
        var v=Session.RegionalTradeView();if(v==null){message.Text="現地の市場で取引してください。";return;}long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(510,360),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(500,0)};scroll.AddChild(box);
        box.AddChild(new Label{Text=NewGameWorld.SiteName(v.Site.Value)+" — 物資の取引"});var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(TradeAction action,EntityId? contract=null,EntityId? to=null,long n=0,string? kind=null){var result=Session.TradeRice(new(new("request:goods-ui/"+shown),action,v.Site,contract,to,n){Commodity=kind},Session.PlayerControl,shown);if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=result.VisibleReason;return;}popup.Hide();Refresh();ShowMaterialTrade();}
        void Button(string title,Action run){var b=new Button{Text=title};box.AddChild(b);b.Pressed+=run;}
        box.AddChild(new Label{Text=$"本人の自由な私財 {v.PrivateAvailable}文 / 商人の自由資金 {v.CashAvailable}文"});
        var house=MaterialMarkets.LocalHouse(Snapshot());if(house!=null)box.AddChild(new Label{Text=$"この家の木材 {house.Wood}kg・武具 {house.Weapons}組\n水利工事には米に加え木材10kgを実消費します。"});
        var d=v.Deals.LastOrDefault(x=>x.Phase is not(TradePhase.Cancelled or TradePhase.Expired or TradePhase.Settled or TradePhase.Supplied));
        if(d==null){var goods=new[]{new CommodityQuote("rice","米","kg",1,v.BuyPrice,v.SellPrice,v.RiceAvailable)}.Concat(v.Goods!).ToArray();var choose=new OptionButton();foreach(var g in goods)choose.AddItem(g.Name);box.AddChild(choose);var info=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(info);var amount=new SpinBox{MinValue=1,MaxValue=100,Step=1,Value=10};box.AddChild(amount);
            void Update(){var g=goods[choose.Selected];amount.MaxValue=100/g.MassKg;amount.Suffix=" "+g.Unit;info.Text=$"現地売値 {g.BuyPrice}文/{g.Unit}・買取 {g.SellPrice}文/{g.Unit}\n自由在庫 {g.Available}{g.Unit}・携行換算 {g.MassKg}kg/{g.Unit}";}choose.ItemSelected+=_=>Update();Update();
            var sites=MaterialMarkets.Sites(Snapshot());var destination=new OptionButton();foreach(var site in sites)destination.AddItem(NewGameWorld.SiteName(site.Value));box.AddChild(destination);Button("この数量の購入条件を聞く（まだ支払わない）",()=>Run(TradeAction.Quote,to:sites[destination.Selected],n:(long)amount.Value,kind:choose.Selected==0?null:goods[choose.Selected].Kind));
            long rice=HouseSalary.PrivateRice(Snapshot(),Session.PlayerControl);if(rice>0)Button("受領済み私有米を持ち込む（最大100kg）",()=>Run(TradeAction.OfferOwnRice,to:v.Site,n:Math.Min(rice,100)));
        }else{string item=MaterialMarkets.Name(d.Commodity),unit=MaterialMarkets.Unit(d.Commodity);box.AddChild(new Label{Text=$"{item} {d.Amount}{unit}（{d.Amount*MaterialMarkets.MassKg(d.Commodity)}kg相当）\n{NewGameWorld.SiteName(d.Source.Value)} → {NewGameWorld.SiteName(d.Destination.Value)}\n購入代金 {d.PurchasePrice*d.Amount}文",AutowrapMode=TextServer.AutowrapMode.WordSmart});
            switch(d.Phase){case TradePhase.Offered:box.AddChild(new Label{Text="購入条件を確認中"});if(v.Site==d.Source)Button("物資と代金を引き当てる",()=>Run(TradeAction.Accept,d.Id));break;
                case TradePhase.Accepted:box.AddChild(new Label{Text="物資と銭を引当済み"});if(v.Site==d.Source)Button("代金を払い、現物を受け取る",()=>Run(TradeAction.Collect,d.Id));break;
                case TradePhase.Carrying:case TradePhase.ReturnRequired:box.AddChild(new Label{Text=d.Phase==TradePhase.ReturnRequired?"行先取りやめ・現物は携行中":"本人が私有物を携行中"});if(v.Site==d.Source||d.Phase!=TradePhase.ReturnRequired&&v.Site==d.Destination)Button("この商人の買取条件を聞く",()=>Run(TradeAction.OfferSale,d.Id));if(d.Phase==TradePhase.Carrying&&house!=null&&Snapshot().NewGame!.Start.Factions.Any(f=>f.Head==Session.PlayerControl.Value))Button("この家の在庫へ納入する（私財への返金なし）",()=>Run(TradeAction.SupplyHouse,d.Id));break;
                case TradePhase.SaleQuoted:if(v.Site==d.SaleSite){box.AddChild(new Label{Text=$"買取条件 {d.SalePrice*d.Amount}文（1日以内）"});Button("現物を納める（代金はまだ受け取らない）",()=>Run(TradeAction.Deliver,d.Id));}break;
                case TradePhase.Delivered:box.AddChild(new Label{Text="納入済み・売却代金の受取待ち"});if(v.Site==d.SaleSite)Button($"代金 {d.SalePrice*d.Amount}文を受け取り精算",()=>Run(TradeAction.Settle,d.Id));break;}
            if(d.Phase is TradePhase.Offered or TradePhase.Accepted or TradePhase.Carrying or TradePhase.SaleQuoted)Button("取引・行先を取りやめる（受取済みの現物は残る）",()=>Run(TradeAction.Cancel,d.Id));
        }
        box.AddChild(new Label{Text="携行は全商品共通100kg・運搬中の取引は一件。\n現地以外の現在在庫・相場は表示しません。\n市場の所在地/初期在庫/相場/生産係数は調整用の試作です。",AutowrapMode=TextServer.AutowrapMode.WordSmart});Button("現地の条件を読み直す",()=>{popup.Hide();ShowMaterialTrade();});Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(580,530));
    }
}
