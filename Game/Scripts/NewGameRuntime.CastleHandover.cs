using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    readonly Dictionary<EntityId,Node3D> castleMarkers=new();
    string CastleOwnerName(ContractWorld w,EntityId id)=>w.NewGame!.Start.Factions.Single(f=>f.Id==id.Value).Name;
    void RefreshCastleMarkers(ContractWorld w,FieldPoint center)
    {
        foreach(var marker in castleMarkers.Values)marker.Visible=false;
        if(Session.CastleView() is not {} v)return;
        foreach(var card in v.Castles.Where(c=>c.Local)){
            var j=CastleHandover.Jobs(w).Single(j=>j.Terms.Id==card.Id);if(j.Gate==null)continue;
            if(!castleMarkers.TryGetValue(card.Id,out var marker)){marker=new Node3D();stage.AddChild(marker);marker.AddChild(new Label3D{Position=new Vector3(0,4,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=26,PixelSize=.025f,Modulate=new Color("f1d18e"),OutlineSize=5});castleMarkers[card.Id]=marker;}
            marker.Visible=true;var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==card.Site).Position);WorldArmyProjection.Apply(marker,point,center);
            ((Label3D)marker.GetChild(0)).Text=$"{(j.Occupied==null?"城門引渡し":"本丸引渡し記録")}：{CastleOwnerName(w,card.Occupier)}\n所有記録：{CastleOwnerName(w,card.Owner)}（蔵・財産は別）";
        }
    }
    void ShowCastleHandover()
    {
        if(Session.CastleView() is not {} v)return;var w=Snapshot();long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(580,460),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(560,0)};box.AddThemeConstantOverride("separation",10);scroll.AddChild(box);
        void Text(string s)=>box.AddChild(new Label{Text=s,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string s,Action action,bool disabled=false){var b=new Button{Text=s,Disabled=disabled,CustomMinimumSize=new(0,36)};box.AddChild(b);b.Pressed+=action;}
        Text("城の引渡し — 城門・本丸と財産を分けます");Text("本人が旧主側として現地で合意する試作です。相手の実部隊と指揮官が現地に必要です。城門や本丸を渡しても、家臣・兵・蔵・給付債務は一括移転しません。");var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
        void Run(CastleInput i){var r=Session.ManageCastle(i,Session.PlayerControl,shown);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCastleHandover();}
        if(v.Site is {} site&&v.LocalRecipients.Length>0){var recipients=new OptionButton();foreach(var id in v.LocalRecipients)recipients.AddItem(CastleOwnerName(w,id));box.AddChild(recipients);Button("安全な退出・財産保全の同じ条件を現地で提示",()=>Run(new(new("request:castle-offer-ui/"+shown),CastleAction.Offer,site,RecipientOwner:v.LocalRecipients[recipients.Selected])));}
        else Text("本人の締結権と、相手軍・指揮官の現地到着を確認してから提示できます。全国AIの開城判断・攻城戦全体は後続です。");
        foreach(var c in v.Castles.Reverse().Take(4)){
            Text($"所有記録：{CastleOwnerName(w,c.Owner)} ／ 引渡し相手：{CastleOwnerName(w,c.Occupier)}\n{c.Status}\n{(c.Local?"現地確認":"立会記録")}：世界経過 {c.WitnessAt}秒");
            if(c.Local)Text($"この試作で明示登録された現地兵：旧主側 {c.IncumbentSoldiers} ／ 相手側 {c.RecipientSoldiers}。未登録の史実守備兵をゼロと断定するものではありません。");
            void Step(CastleAction a)=>Run(new(new("request:castle-ui/"+a+"/"+shown),a,c.Site,c.Id));
            Button("同じ返答・条件を本人確認",()=>Step(CastleAction.Confirm),!c.CanConfirm);Button("城門を引き渡す",()=>Step(CastleAction.Gate),!c.CanGate);Button("次の1秒境界に本丸引渡しを予定",()=>Step(CastleAction.RequestKeep),!c.CanRequestKeep);Button("現況を確認して立会記録へ残す",()=>Step(CastleAction.Observe),!c.Local);Button("引渡し前に取りやめ",()=>Step(CastleAction.Cancel),!c.CanCancel);
        }
        Text("本丸引渡しの予定後も、世界は通常の進行操作で進みます。旧主側の部隊・救援が現地に残れば未引渡し分を保留します。");Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(620,550));
    }
}
