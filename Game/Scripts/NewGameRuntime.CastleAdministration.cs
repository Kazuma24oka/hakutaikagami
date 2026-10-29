using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    readonly Dictionary<EntityId,Node3D> castellanMarkers=new();
    void RefreshCastellanMarkers(ContractWorld w,FieldPoint center)
    {
        foreach(var m in castellanMarkers.Values)m.Visible=false;
        if(Session.CastellanView() is not {} v)return;
        foreach(var c in v.Appointments.Where(c=>c.Local)){
            if(!castellanMarkers.TryGetValue(c.Id,out var marker)){marker=new Node3D();stage.AddChild(marker);marker.AddChild(new Label3D{Position=new Vector3(0,6,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=.02f,Modulate=new Color("c3e4d2"),OutlineSize=5});castellanMarkers[c.Id]=marker;}
            marker.Visible=true;var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==c.Site).Position);WorldArmyProjection.Apply(marker,point,center);((Label3D)marker.GetChild(0)).Text=$"行政担当：{w.NewGame!.Start.People.Single(p=>p.Id==c.Person.Value).Name}\n{c.Status}";
        }
    }
    void ShowCastleAdministration()
    {
        if(Session.CastellanView() is not {} v)return;var w=Snapshot();long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(560,400),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(540,0)};box.AddThemeConstantOverride("separation",10);scroll.AddChild(box);
        void Text(string s)=>box.AddChild(new Label{Text=s,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string s,Action action,bool disabled=false){var b=new Button{Text=s,Disabled=disabled,CustomMinimumSize=new(0,36)};box.AddChild(b);b.Pressed+=action;}
        Text("城主・行政と保管の引継ぎ");Text("現地で30日間の担当を合意します。家の所有物・兵・直属・未払いは移りません。保管確認はその時点の数量を記録し、物資を増やしません。");var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Done(CommandResult r){if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCastleAdministration();}
        void Run(CastellanInput i)=>Done(Session.ManageCastellan(i,Session.PlayerControl,shown));
        if(v.Site is {} site&&v.Candidates.Length>0){var people=new OptionButton();foreach(var id in v.Candidates)people.AddItem(w.NewGame!.Start.People.Single(p=>p.Id==id.Value).Name);box.AddChild(people);Button("現地で城主担当を合意・交代",()=>{var other=v.Candidates[people.Selected];var head=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Head==w.PlayerControl.Value);var agent=head!=null?other:Session.PlayerControl;Run(new(new("request:castellan-appoint-ui/"+shown),CastellanAction.Appoint,site,agent));});}
        foreach(var c in v.Appointments.Reverse().Take(4)){Text($"{w.NewGame!.Start.People.Single(p=>p.Id==c.Person.Value).Name} — {c.Status}\n合意 {c.At}秒 ／ 期限 {c.Until}秒");if(c.Receipt is {} r)Text($"保管確認 {r.ObservedAt}秒（所有家 {CastleOwnerName(w,r.Owner)}）：米 {r.Rice}kg・木材 {r.Wood}・武具 {r.Arms}・家金 {r.Cash}。現在量とは別です。");Button("担当者として現地の保管数量を確認",()=>Run(new(new("request:castellan-receive-ui/"+shown),CastellanAction.ReceiveStore,c.Site,Appointment:c.Id)),!c.CanReceive);Button("現地で担当を解除",()=>Run(new(new("request:castellan-release-ui/"+shown),CastellanAction.Release,c.Site,Appointment:c.Id)),!c.CanRelease);}
        AppendCastellanReports(box,Done);Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(600,510));
    }
}
