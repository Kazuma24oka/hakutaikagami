using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowTruceNegotiation()
    {
        var w=Snapshot();if(OwnPoliticalHouse(w) is not {} own||!TruceNegotiations.Enabled(w))return;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(500,300)};panel.AddChild(box);box.AddChild(new Label{Text="不戦期間を交渉する"});box.AddChild(new Label{Text="双方が同じ条件に承諾し、書状が届いてから成立します。\n成立を知った当事者は自家の敵対意思を撤回します。\n相手や遠方の部隊が今知ったとは限りません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var targets=w.Definitions.Organizations.Where(o=>o.Id!=own).OrderBy(o=>o.Name,StringComparer.Ordinal).ToArray();var target=new OptionButton();target.AddItem("相手の勢力を選択");foreach(var t in targets)target.AddItem(t.Name);box.AddChild(target);var days=new SpinBox{MinValue=1,MaxValue=90,Step=1,Value=30,Prefix="提示する期間 ",Suffix="日"};box.AddChild(days);box.AddChild(new Label{Text="相手は30日までを初期の判断上限とし、長い提示には対案を返します。\n返答は7日以内の到達が必要です（短い提示では期間終了まで）。\n相手が本拠を離れていると書状が届かない場合があります。",AutowrapMode=TextServer.AutowrapMode.WordSmart});var send=new Button{Text="この条件を本拠へ発送",Disabled=true};box.AddChild(send);target.ItemSelected+=n=>send.Disabled=n<1;long shown=Session.Revision;
        send.Pressed+=()=>{int n=target.Selected-1;if(n<0||n>=targets.Length)return;popup.Hide();try{Submit(Session.NegotiateTruce(new(new("request:truce-ui/"+shown),TruceAction.Propose,own,targets[n].Id,null,(int)days.Value),Session.PlayerControl,shown));}catch(Exception e){message.Text="操作できません："+e.Message;}Refresh();};
        var cards=Session.TruceView();var status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,Text=cards.Length==0?"まだ提示した条件はありません。":string.Join("\n",cards.Select(c=>(w.Definitions.Organizations.Single(o=>o.Id==c.Other).Name)+"／第"+c.Version+"版："+c.Stage))};box.AddChild(status);var close=new Button{Text="地図へ戻る"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(570,430));
    }
    void ShowTruceLetter(EntityId id)
    {
        var known=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(known.KnownBody==null)throw new InvalidOperationException("交渉の未開封書状です");var w=Snapshot();var p=w.Truces!.Papers.Single(p=>p.Message==id.Value);var body=ContractJson.Read<TruceBody>(System.Text.Encoding.UTF8.GetBytes(known.KnownBody));string line=body.Action switch{TruceAction.Counter=>"長い期間の約束は避けたい。30日までなら、この条件で話を進められる。",TruceAction.Accept=>"提示された条件に承諾する。",TruceAction.Confirm=>"同じ条件への承諾が届き、不戦の合意が成立したことを確認した。",TruceAction.Decline=>"この条件には承諾できない。",_=>"この期間、互いに戦を仕掛けないことを提案する。"};var card=Session.TruceView().SingleOrDefault(c=>c.Paper==p.Input.Id);long shown=Session.Revision;
        PresentWork("不戦交渉の書状",line,"第"+body.Terms.Version+"版／残る不戦期間 約"+Math.Max(0,(body.Terms.Until-Session.Seconds+86399)/86400)+"日。成立・本人の認識・履行は別に確認する。",p.Author,p.Author,"相手の条件・返答","確認した書状");
        if(card?.CanAccept==true&&OwnPoliticalHouse(w) is {} own){void Answer(TruceAction action){Submit(Session.NegotiateTruce(new(new("request:truce-answer/"+shown),action,own,p.Input.Owner,p.Input.Id),Session.PlayerControl,shown));CloseBottomTalk();}Choice("truce-accept","この版の条件に承諾して返書する",()=>Answer(TruceAction.Accept));Choice("truce-decline","この条件は辞退する",()=>Answer(TruceAction.Decline));}
        Choice("truce-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
