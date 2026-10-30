using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowAdoption()
    {
        var world=Snapshot();if(!AdultAdoption.Enabled(world))return;
        var popup=new PopupPanel();AddChild(popup);var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};popup.AddChild(scroll);scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action,bool disabled=false){var b=new Button{Text=text,Disabled=disabled,CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=action;}
        Text("成人の養子相談");Text("双方が現地にいるときに相談します。本人同意、家の承諾、実施は別です。成立しても所属・家督・役職・所領は自動では変わりません。");
        long revision=Session.Revision;var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Act(AdoptionAction action,EntityId? other=null,bool asParent=true,EntityId? root=null,EntityId? house=null){
            var r=Session.DecideAdoption(new(new("request:adoption-ui/"+Guid.NewGuid().ToString("N")),action,other,asParent,root,house),Session.PlayerControl,revision);
            if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowAdoption();
        }
        foreach(var card in AdultAdoption.View(world)){
            Text("親："+PersonName(card.Parent)+" ／ 子："+PersonName(card.Child));Text(card.Status);
            foreach(var house in card.OwnHouseApprovals)Button("自分が代表する家として承諾する",()=>Act(AdoptionAction.ApproveHouse,root:card.Id,house:house));
            if(card.CanImplement)Button("現地で養子縁組を実施する",()=>Act(AdoptionAction.Implement,root:card.Id));
            if(card.CanWithdraw)Button("この相談を取り下げる",()=>Act(AdoptionAction.Withdraw,root:card.Id));
        }
        var candidates=AdultAdoption.Candidates(world);if(candidates.Length==0)Text("いま現地で相談できる成人はいません。");
        foreach(var person in candidates){Text(PersonName(person));Button("養子に迎える相談",()=>Act(AdoptionAction.Propose,person));Button("この人物の養子に入る相談",()=>Act(AdoptionAction.Propose,person,false));}
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(610,590));
    }
}
