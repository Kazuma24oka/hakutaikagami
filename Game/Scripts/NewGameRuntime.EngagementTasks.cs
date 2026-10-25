using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    PopupPanel? engagementPopup;
    OptionButton? engagementPicker;
    Button? engagementConfirm;
    EntityId[] engagementTargets=[];
    void OpenEngagementSelection()
    {
        var w=Snapshot();if(!NewGameMilitary.CanRequestEngagement(w))return;
        engagementPopup=new PopupPanel();AddChild(engagementPopup);
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("1d2b34"),BorderColor=new Color("b7a16a"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=14,ContentMarginBottom=14});engagementPopup.AddChild(panel);var box=new VBoxContainer{CustomMinimumSize=new(480,180)};panel.AddChild(box);
        box.AddChild(new Label{Text="交戦任務の相談",HorizontalAlignment=HorizontalAlignment.Center});
        box.AddChild(new Label{Text="対象勢力を選び、主君へ条件を問い合わせます。\n許可の有効期間は相談から10分。\n実際の攻撃には敵対関係と現地での接触も必要です。"});
        engagementPicker=new OptionButton();box.AddChild(engagementPicker);engagementPicker.AddItem("対象勢力を選択してください");
        var targets=w.Definitions.Organizations.Where(o=>o.Id!=w.NewMilitary!.Terms.Owner).OrderBy(o=>o.Name,StringComparer.Ordinal).ToArray();engagementTargets=targets.Select(o=>o.Id).ToArray();
        foreach(var t in targets)engagementPicker.AddItem(t.Name);
        engagementConfirm=new Button{Text="この条件で相談を送る",Disabled=true};box.AddChild(engagementConfirm);
        engagementPicker.ItemSelected+=i=>engagementConfirm.Disabled=i<=0;
        engagementConfirm.Pressed+=()=>{
            int i=engagementPicker.Selected-1;if(i<0||i>=engagementTargets.Length)return;
            var input=new NewMilitaryInput(new("request:engagement-ui/"+Session.Revision),NewMilitaryAction.RequestEngagement){Engagement=new(NewGameMilitary.EngagementTaskContract,engagementTargets[i],Session.Seconds,checked(Session.Seconds+600))};
            engagementPopup.Hide();Submit(Session.DecideNewMilitary(input,Session.PlayerControl,Session.Revision));
        };
        var cancel=new Button{Text="戻る"};box.AddChild(cancel);cancel.Pressed+=()=>engagementPopup.Hide();
        engagementPopup.PopupHide+=()=>engagementPopup.QueueFree();engagementPopup.PopupCentered(new Vector2I(540,260));
    }
    internal bool ReviewEngagementPopupLayout(){GD.Print("ENGAGEMENT_POPUP_SIZE "+engagementPopup?.Size);return engagementPopup is {Visible:true}&&engagementPopup.Size.X<=600&&engagementPopup.Size.Y<=300;}
    internal void ChooseEngagementTarget(EntityId target)
    {
        int i=Array.IndexOf(engagementTargets,target);if(i<0||engagementPicker==null||engagementConfirm==null||engagementPopup?.Visible!=true)throw new InvalidOperationException("交戦任務の対象選択画面ではありません");
        engagementPicker.Select(i+1);engagementPicker.EmitSignal(OptionButton.SignalName.ItemSelected,i+1);engagementConfirm.EmitSignal(Button.SignalName.Pressed);
    }
    static string EngagementLetterSpeech(ContractWorld w,MilitaryNotice notice)
    {
        var t=notice.Engagement??throw new InvalidDataException("交戦任務の条件がありません");
        string target=w.Definitions.Organizations.Single(o=>o.Id==t.TargetOwner).Name;
        return target+"を対象とする交戦任務を許可する。有効期限まで残り"+TimeSpan.FromSeconds(Math.Max(0,t.Until-w.ElapsedSeconds)).ToString(@"mm\:ss")+"。敵対関係と現地での接触を別に確認すること。";
    }
}
