using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowLocalEconomy()
    {
        var v=Session.LocalEconomyView();if(v==null){message.Text="この地域の帳簿を現地で確認できる当主ではありません。";return;}
        var a=v.Accounts;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(490,320)};panel.AddChild(box);
        box.AddChild(new Label{Text="清洲の村と家の蔵 — 現地帳簿"});box.AddChild(new Label{Text=$"村の米 {a.VillageRice}kg　家の蔵 {a.HouseRice}kg\n未徴収年貢 {a.Assessed-a.Collected}kg　水利 {a.Water}\n次の収穫まで約{Math.Max(1,(v.NextHarvest-v.ObservedAt+86399)/86400)}日"});
        box.AddChild(new Label{Text=a.WorkDue is {} due?$"水利工事中（完成まで約{Math.Max(1,(due-v.ObservedAt+86399)/86400)}日）":"水利工事は家の米200kgと7日。完成後の収穫が増える。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var amount=new SpinBox{MinValue=1,MaxValue=1000000,Step=1,Value=100,Prefix="米 ",Suffix=" kg"};box.AddChild(amount);
        void AddChoice(string text,EconomyAction action,Func<long> quantity){var b=new Button{Text=text};box.AddChild(b);b.Pressed+=()=>{long n=quantity();popup.Hide();Submit(Session.ManageLocalEconomy(new(new("request:economy-ui/"+Session.Revision),action,n),Session.PlayerControl,Session.Revision));};}
        AddChoice("指定量を年貢として村から家の蔵へ納める",EconomyAction.CollectLevy,()=>(long)amount.Value);AddChoice("指定量を家の蔵から村へ救済する",EconomyAction.RelieveVillage,()=>(long)amount.Value);AddChoice("米200kgで水利工事を始める",EconomyAction.ImproveWater,()=>200);
        box.AddChild(new Label{Text=v.Explanation+"\n村の保護備蓄500kgは徴収できません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(550,430));
    }
}
