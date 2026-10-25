using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowCompanions()
    {
        var cards=Session.CompanionView();var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(500,300)};panel.AddChild(box);box.AddChild(new Label{Text="同行者・配下との協力"});
        box.AddChild(new Label{Text="個人の協力契約です。所属・土地・兵の引渡しではありません。\n一日20文、初日分を私財から前払い。未払いは終了後も残ります。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        if(cards.Length==0){box.AddChild(new Label{Text="現在、現地で協力を相談できる相手はいません。\n浪人や直属家臣との面会時に相談できます。"});}
        else{
            var choice=new OptionButton();foreach(var c in cards)choice.AddItem(c.Name+" — "+c.Status);box.AddChild(choice);var status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(status);
            var destination=new OptionButton();destination.AddItem("同行してほしい行先を選択");var sites=BranchRoutes.GraphNodes(Snapshot()).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();foreach(var n in sites)destination.AddItem(NewGameWorld.SiteName(RegionalCampaign.Site(n.Id).Value));box.AddChild(destination);
            void Run(CompanionAction action,long amount=0,string? to=null){var c=cards[choice.Selected];popup.Hide();Submit(Session.ManageCompanion(new(new("request:companion-ui/"+Session.Revision),c.Person,action,amount,to),Session.PlayerControl,Session.Revision));}
            var agree=new Button{Text="協力を相談する（初日20文を支払う）"};box.AddChild(agree);agree.Pressed+=()=>Run(CompanionAction.Agree,20);
            var pay=new Button{Text="未払い手当を支払う"};box.AddChild(pay);pay.Pressed+=()=>Run(CompanionAction.Pay,cards[choice.Selected].Owed);
            var inspect=new Button{Text="この拠点の点検を頼む"};box.AddChild(inspect);inspect.Pressed+=()=>Run(CompanionAction.Inspect);
            var travel=new Button{Text="指定した行先への同行を頼む"};box.AddChild(travel);travel.Pressed+=()=>Run(CompanionAction.TravelTogether,to:sites[destination.Selected-1].Id);
            var end=new Button{Text="協力契約を終える（所属関係は変更しない）"};box.AddChild(end);end.Pressed+=()=>Run(CompanionAction.EndAgreement);
            void RefreshCard(){var c=cards[choice.Selected];status.Text=c.Name+"\n"+c.Kind+"／"+c.Status+"\n未払い "+c.Owed+"文"+(c.Nearby?"／現地で面会中":"／離れています。新しい相談は再会してから");bool local=c.Nearby&&!c.Busy;agree.Disabled=!local||c.Active||c.Owed>0;pay.Disabled=!local||c.Owed==0;inspect.Disabled=!local||!c.Active||c.Owed>0;travel.Disabled=inspect.Disabled||destination.Selected<1;end.Disabled=!local||!c.Active;}
            choice.ItemSelected+=_=>RefreshCard();destination.ItemSelected+=_=>RefreshCard();RefreshCard();
        }
        var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(570,520));
    }
    void ShowCompanionReport(EntityId id)
    {
        var paper=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(paper.KnownBody==null)throw new InvalidOperationException("未開封の返報です");var w=Snapshot();var m=w.Messaging.Messages.Single(m=>m.Id==id.Value);if(m.Kind!=PersonalCompanions.ReportKind)throw new InvalidOperationException("協力者の返報ではありません");PresentWork("協力者からの返報",paper.KnownBody,"現地で受け取った報告です。",new(m.SenderId),new(m.SenderId),"本人からの報告","確認した記録");Choice("companion-report-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
