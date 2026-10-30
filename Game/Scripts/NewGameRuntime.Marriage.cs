using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowMarriage()
    {
        var world=Snapshot();if(!AdultMarriage.Enabled(world))return;
        var popup=new PopupPanel();AddChild(popup);var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};popup.AddChild(scroll);scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action,bool disabled=false){var b=new Button{Text=text,Disabled=disabled,CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=action;}
        Text("成人の婚姻相談");Text("開始時未婚と明示した成人登録武将同士が現地にいるときに相談します。史実人物の未確認の配偶者情報は推測しません。本人同意、家の承諾、実施は別です。成立しても所属・家督・役職・所領は自動では変わりません。");
        long revision=Session.Revision;var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Act(MarriageAction action,EntityId? other=null,EntityId? root=null,EntityId? house=null){
            var r=Session.DecideMarriage(new(new("request:marriage-ui/"+Guid.NewGuid().ToString("N")),action,other,root,house),Session.PlayerControl,revision);
            if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowMarriage();
        }
        foreach(var card in AdultMarriage.View(world)){
            Text("当人："+PersonName(card.First)+" ／ 相手："+PersonName(card.Second));Text(card.Status);
            foreach(var house in card.OwnHouseApprovals)Button("自分が代表する家として承諾する",()=>Act(MarriageAction.ApproveHouse,root:card.Id,house:house));
            if(card.CanImplement)Button("現地で婚姻を実施する",()=>Act(MarriageAction.Implement,root:card.Id));
            if(card.CanWithdraw)Button("この相談を取り下げる",()=>Act(MarriageAction.Withdraw,root:card.Id));
        }
        var candidates=AdultMarriage.Candidates(world);if(candidates.Length==0)Text("いま現地で相談できる成人はいません。");
        foreach(var person in candidates){Text(PersonName(person));Button("婚姻を相談する",()=>Act(MarriageAction.Propose,person));}
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(610,590));
    }
}
