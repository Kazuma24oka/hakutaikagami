using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowTreatyDefense()
    {
        var w=Snapshot();long shown=Session.Revision;var popup=new PopupPanel();AddChild(popup);var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",WorkBox("1d2b34","b7a16a",14));popup.AddChild(panel);var scroll=new ScrollContainer{CustomMinimumSize=new(540,410),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(scroll);var box=new VBoxContainer{CustomMinimumSize=new(510,0)};scroll.AddChild(box);
        void Text(string s)=>box.AddChild(new Label{Text=s,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string s,Action a){var b=new Button{Text=s};box.AddChild(b);b.Pressed+=a;}
        void Run(TreatyDefenseInput i){Submit(Session.ManageTreatyDefense(i,Session.PlayerControl,shown));popup.Hide();Refresh();}
        Text("防衛援軍・確認した報告");Text("返書で確認した日時の情報です。軍の現在地は現地で見えた範囲で確認します。");
        foreach(var c in TreatyDefense.View(w)){
            Text(c.Status+(c.ObservedAt is {} at?" ／ "+TreatyDate(at):""));
            if(c.KnownTerms is {} t)Text(NewGameWorld.SiteName(t.Source.Value)+"から "+t.Soldiers+"人 ／ 守備先 "+NewGameWorld.SiteName(t.Destination.Value)+" ／ 守備 "+t.DutySeconds/60+"分 ／ 出陣時の兵糧 "+t.CarryDays+"日分 ／ 元期限 "+TreatyDate(t.Until));
            if(c.CanCancel)Button("出陣・守備取りやめを既知の宛先へ発送",()=>Run(new(new("request:defense-cancel-ui/"+shown+"/"+c.Root.Value),TreatyDefenseAction.Cancel,c.Root)));
        }
        foreach(var paper in w.Treaties?.Defense?.Papers.Where(p=>p.Sender==Session.PlayerControl)??Array.Empty<TreatyDefensePaper>()){
            var retry=new TreatyDefenseInput(new("request:defense-retry-ui/"+shown+"/"+paper.Id),TreatyDefenseAction.Resend,paper.Root,Original:paper.Id);
            if(TreatyDefense.Can(w,retry,Session.PlayerControl))Button("不達を確認した同じ原本を再送",()=>Run(retry));
        }
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(580,490));
    }
    void ShowTreatyDefenseLetter(EntityId id)
    {
        var k=Branch(Session.PlayerControl).Knowledge.Messages.Single(m=>m.Id==id);if(k.KnownBody==null)return;var p=ContractJson.Read<TreatyDefensePaper>(System.Text.Encoding.UTF8.GetBytes(k.KnownBody));
        string line=p.Kind switch{TreatyDefensePaperKind.Dispatched=>"元の駐屯兵と家の兵糧を引き受け、守備先へ出発した。現地に着いたこととは分けて報告する。",TreatyDefensePaperKind.Arrived=>"指定の守備先へ軍が到着した。約束した期間の守備に入る。",TreatyDefensePaperKind.Returning=>"元の軍と兵糧を保持し、実際の道を通って帰還する。",TreatyDefensePaperKind.Returned=>"軍が元の拠点へ帰還し、未使用の兵糧を現地で返した。",TreatyDefensePaperKind.Refused=>p.Reason??"現地の兵・兵糧・行程が条件を満たせず、今回の出陣は引き受けられない。",TreatyDefensePaperKind.Cancel=>"この要請の出陣・守備を取りやめたい。書状の到達と軍の帰還は別に確認する。",_=>"受領返書に続き、先の防衛援軍の実行可否を確認したい。"};
        string detail="報告日時 "+TreatyDate(p.ObservedAt)+(p.Terms is {} t?" ／ "+t.Soldiers+"人 ／ 守備先 "+NewGameWorld.SiteName(t.Destination.Value)+" ／ 元期限 "+TreatyDate(t.Until):"");
        PresentWork("防衛援軍の書状",line,detail,p.Sender,p.Sender,"防衛援軍の差出人","本人が開封した報告");Choice("defense-status","確認した援軍報告を見る",()=>{CloseBottomTalk();ShowTreatyDefense();});Choice("defense-map","地図へ戻る",CloseBottomTalk);LayoutBottomTalk();
    }
}
