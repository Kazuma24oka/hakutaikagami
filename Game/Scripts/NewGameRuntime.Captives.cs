using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    PopupPanel? captivesPopup;
    void ShowCaptives()
    {
        var w=Snapshot();if(!BattleCaptivity.Enabled(w))return;
        var popup=new PopupPanel{Exclusive=true};AddChild(popup);captivesPopup=popup;
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);
        var box=new VBoxContainer{CustomMinimumSize=new(460,0)};panel.AddChild(box);box.AddThemeConstantOverride("separation",8);
        void Text(string text)=>box.AddChild(new Label{Text=text});
        string House(EntityId id)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==id)?.Name??id.Value;
        string Site(EntityId id)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id==id)?.Name??id.Value;
        long revision=Session.Revision;
        void Act(HBoxContainer row,string label,bool disabled,Func<CommandResult> run)
        {
            var button=new Button{Text=label,Disabled=disabled};row.AddChild(button);
            button.Pressed+=()=>{var r=run();popup.Hide();Refresh();ShowCaptives();message.Text=r.VisibleReason;};
        }
        var cards=Session.Captives();var ransoms=Session.Ransoms();
        if(cards.Length==0)Text("捕虜なし");
        foreach(var c in cards)
        {
            Text(PersonName(c.Person)+"　"+House(c.FromOwner)+"　"+Site(c.Site)+"　"+(c.Held?"留置":"—"));
            foreach(var r in ransoms.Where(r=>r.Person==c.Person))Text("身代金 "+r.Amount+"文　"+r.Stage);
            if(!c.Held)continue;
            var row=new HBoxContainer();box.AddChild(row);
            Act(row,"解放",!c.CanAct,()=>Session.ManageCaptive(new(new("request:captive-ui/release/"+revision),CaptiveAction.Release,c.Person),revision));
            Act(row,"登用",!c.CanAct||c.Declined,()=>Session.ManageCaptive(new(new("request:captive-ui/recruit/"+revision),CaptiveAction.Recruit,c.Person),revision));
            Act(row,"処断",!c.CanAct,()=>Session.ManageCaptive(new(new("request:captive-ui/execute/"+revision),CaptiveAction.Execute,c.Person),revision));
            if(ransoms.Any(r=>r.Person==c.Person))continue;
            var ask=new HBoxContainer();box.AddChild(ask);
            var amount=new SpinBox{MinValue=BattleCaptivity.RansomMin,MaxValue=BattleCaptivity.RansomMax,Step=100,Value=500,Suffix="文"};ask.AddChild(amount);
            Act(ask,"身代金",!NewGameWork.Enabled(w),()=>Session.DemandRansom(new("request:captive-ui/ransom/"+revision),c.Person,(long)amount.Value,revision));
        }
        var close=new Button{Text="閉じる"};box.AddChild(close);close.Pressed+=()=>popup.Hide();popup.Size=new Vector2I(480,1);popup.PopupCentered(new Vector2I(480,1));
    }
}
