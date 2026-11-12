using Godot;
using System;
using HakutaiKagami.Core.Contracts;
// 操作する人物の死去と、引き継ぐ人の選択（ControlSuccession）。
public partial class NewGameRuntime
{
    PopupPanel? successionPopup;
    void CheckSuccession(ContractWorld w){if(successionPopup==null&&ControlSuccession.Awaiting(w))ShowSuccession(w);}
    internal void ShowSuccession(ContractWorld w)
    {
        var popup=new PopupPanel{Exclusive=true};successionPopup=popup;AddChild(popup);
        var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};popup.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action){var b=new Button{Text=text,CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=action;}
        Text(PersonName(w.PlayerControl)+"　死去");Text(WorldCalendar.Display(w.EpochDate,w.ElapsedSeconds));
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
        var candidates=ControlSuccession.Candidates(w);
        void End()=>Button("年代記を閉じる",()=>{popup.Hide();Suspend();MenuRequested?.Invoke();});
        if(candidates.Length==0){Text("年代記の終わり");End();}
        foreach(var heir in candidates)Button(PersonName(heir)+"で続ける",()=>{
            var r=Session.ContinueAs(heir,Session.Revision);
            if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}
            successionPopup=null;popup.Hide();Refresh();
        });
        if(candidates.Length>0)End();
        box.AddChild(feedback);popup.PopupHide+=()=>{popup.QueueFree();if(successionPopup==popup)successionPopup=null;};popup.PopupCentered(new Vector2I(460,320));
    }
    void ShowRetire()
    {
        var w=Snapshot();if(!ControlSuccession.Enabled(w))return;
        var popup=new PopupPanel();AddChild(popup);var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};popup.AddChild(box);
        var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
        box.AddChild(new Label{Text="隠居"});
        foreach(var heir in ControlSuccession.RetireCandidates(w)){var b=new Button{Text=PersonName(heir)+"に譲る",CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=()=>{
            var r=Session.ContinueAs(heir,Session.Revision);if(r.Code!=ResultCode.Accepted){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();};}
        var back=new Button{Text="地図へ戻る",CustomMinimumSize=new Vector2(0,34)};box.AddChild(back);back.Pressed+=()=>popup.Hide();
        box.AddChild(feedback);popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(420,280));
    }
}
