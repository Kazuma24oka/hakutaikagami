using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowCommunityEconomy()
    {
        var v=Session.CommunityEconomyView();if(v==null){message.Text="現地で当主として確認できる帳簿はありません。";return;}
        long shown=Session.Revision;var a=v.Accounts;string climate=a.ClimateAt is {} at?a.Climate+"（"+DateOnly.Parse(Snapshot().EpochDate).AddDays(checked((int)(at/86400))).ToString("yyyy年M月d日")+"観測）":"未観測";var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(500,330)};panel.AddChild(box);
        box.AddChild(new Label{Text=NewGameWorld.SiteName(v.Site.Value)+" — 村と家の現地帳簿"});
        box.AddChild(new Label{Text=$"人口 {a.Population}人　働き手 {a.Workforce}人（工事 {a.Reserved}人）\n村の米 {a.VillageRice}kg　家の蔵 {a.HouseRice}kg\n食糧不足 {a.Hunger}kg　未徴収年貢 {a.Assessed-a.Collected}kg\n水利 {a.Water}　確認済みの作柄 {climate}"});
        box.AddChild(new Label{Text=a.WorkDue is {} due?$"工事完成まで約{Math.Max(1,(due-v.ObservedAt+86399)/86400)}日":a.WorkRemaining>0?"工事休止中。先に食糧不足を解消します。":"工事は20人・米200kg・7日。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var amount=new SpinBox{MinValue=1,MaxValue=1000000,Step=1,Value=100,Prefix="米 ",Suffix=" kg"};box.AddChild(amount);
        void Add(string label,EconomyAction action,Func<long> quantity){var b=new Button{Text=label};box.AddChild(b);b.Pressed+=()=>{var n=quantity();popup.Hide();Submit(Session.ManageCommunity(new(new("request:community-ui/"+shown),v.Site,action,n),Session.PlayerControl,shown));};}
        Add("村から家へ年貢を納める",EconomyAction.CollectLevy,()=>(long)amount.Value);Add("家から村へ救済米を渡す",EconomyAction.RelieveVillage,()=>(long)amount.Value);Add("水利工事を始める（米200kg）",EconomyAction.ImproveWater,()=>200);
        box.AddChild(new Label{Text=v.Explanation+"\n村の一月分の食糧は徴収できません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(570,470));
    }
    void ShowCommunityReport(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)return;var p=ContractJson.Read<CommunityReport>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));var observed=DateOnly.Parse(Snapshot().EpochDate).AddDays(checked((int)(p.ObservedAt/86400))).ToString("yyyy年M月d日");var sender=new EntityId(Snapshot().Messaging.Messages.Single(m=>m.Id==id.Value).SenderId);
        PresentWork("村の食糧不足の知らせ",NewGameWorld.SiteName(p.Site.Value)+"では、観測時に食糧が"+p.Need+"kg不足していました。",observed+"時点の報告です。現在の残量や他地域の帳簿を示すものではありません。",sender,sender,"現地から届いた書状","食糧不足の報告");Choice("community-close","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
