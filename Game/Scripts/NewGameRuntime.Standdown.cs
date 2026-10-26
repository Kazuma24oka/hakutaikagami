using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowSecretCooperation()
    {
        var w=Snapshot();var people=RegionalCampaign.Nearby(w,w.PlayerControl).Where(p=>p.Actor!=w.PlayerControl&&w.NewGame!.Start.Factions.Any(f=>f.Head==p.Actor.Value)).Select(p=>p.Actor).ToArray();var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(500,280)};panel.AddChild(box);box.AddChild(new Label{Text="本人だけの秘密の協力"});box.AddChild(new Label{Text="精算した情報交換のある当主本人へ、出撃見合わせを相談します。\n協力金100文、1〜24時間。未出陣の本人の新規軍引受だけが対象です。\n敵対意思・所属・兵の所有は変わりません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        if(people.Length==0)box.AddChild(new Label{Text="現地に相談できる当主本人がいません。"});else{var person=new OptionButton();foreach(var id in people)person.AddItem(w.Definitions.Characters.Single(c=>c.Id==id).Name);box.AddChild(person);var hours=new SpinBox{MinValue=1,MaxValue=24,Step=1,Value=1,Prefix="見合わせる時間 ",Suffix="時間"};box.AddChild(hours);var ask=new Button{Text="本人の条件を聞く"};box.AddChild(ask);long shown=Session.Revision;ask.Pressed+=()=>{popup.Hide();try{var id=new EntityId("request:standdown-ui/"+shown);Submit(Session.NegotiateStanddown(new(id,people[person.Selected],StanddownAction.Ask,null,(int)hours.Value),Session.PlayerControl,shown));ShowStanddownQuote(id);}catch(Exception e){message.Text="操作できません："+e.Message;}Refresh();};}
        var cards=Session.StanddownView();box.AddChild(new Label{Text=string.Join("\n",cards.Select(c=>w.Definitions.Characters.Single(p=>p.Id==c.Person).Name+"："+c.Stage+(c.Until is {} until?"（残り約"+Math.Max(0,(until-Session.Seconds+59)/60)+"分）":""))),AutowrapMode=TextServer.AutowrapMode.WordSmart});var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(570,360));
    }
    void ShowStanddownQuote(EntityId id)
    {
        var c=Session.StanddownView().Single(c=>c.Quote==id);long shown=Session.Revision;PresentWork("本人との協力条件","私が新しく軍を引き受けるのは、"+c.Hours+"時間見合わせよう。協力金は"+c.Fee+"文だ。","確認期限は提示から30ゲーム秒。既に出た軍や他人が率いる部隊は動かしません。期間後は本人が改めて判断します。",c.Person,c.Person,"個人間の秘密の約束","現地で条件を確認");if(c.CanAccept)Choice("standdown-accept","私財から支払い、この条件に合意する",()=>{Submit(Session.NegotiateStanddown(new(new("request:standdown-accept-ui/"+shown),c.Person,StanddownAction.Accept,id),Session.PlayerControl,shown));CloseBottomTalk();});Choice("standdown-leave","今回は見送る",CloseBottomTalk);LayoutBottomTalk();
    }
}
