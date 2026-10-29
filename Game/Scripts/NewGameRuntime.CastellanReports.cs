using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameRuntime
{
    void AppendCastellanReports(VBoxContainer box,Action<CommandResult> finish)
    {
        if(Session.CastellanReportsView() is not {} v)return;var w=Snapshot();long rev=Session.Revision;
        void Text(string s)=>box.AddChild(new Label{Text=s,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string s,Action a){var b=new Button{Text=s,CustomMinimumSize=new(0,36)};b.Pressed+=a;box.AddChild(b);}
        Text("引継ぎ報告 — 到着・開封後に当時の数量が分かります");
        foreach(var c in Session.CastellanView()!.Appointments.Where(c=>c.CanReceive))Button("現地で確認した保管数量を当主へ封書で報告",()=>finish(Session.SendCastellanReport(new(new("request:castellan-report-ui/"+rev),CastellanReportAction.Report,c.Id),Session.PlayerControl,rev)));
        foreach(var m in w.Messaging.Messages.Where(m=>m.Kind==CastellanReports.Kind&&m.RecipientId==Session.PlayerControl.Value&&!MessageFlow.Knows(w.Messaging,m.Id,Session.PlayerControl.Value)&&w.Messaging.Attempts.Any(a=>a.MessageId==m.Id&&a.Status==AttemptStatus.Delivered)))Button("到着した城主報告を開封",()=>finish(Session.Open(Session.PlayerControl,new(m.Id),rev)));
        foreach(var p in v.Reports.Reverse().Take(4)){var r=p.Observation;Text($"保管確認 {r.ObservedAt}秒 ／ 発送 {p.At}秒\n所有家 {CastleOwnerName(w,r.Owner)}：米 {r.Rice}kg・木材 {r.Wood}・武具 {r.Arms}・家金 {r.Cash}\n当時の報告です。現在量や現在の権限を保証するものではありません。");if(v.Retryable.Contains(p.Id))Button("不達を認識済みの同じ報告を再送",()=>finish(Session.SendCastellanReport(new(new("request:castellan-report-retry-ui/"+rev),CastellanReportAction.Resend,p.Appointment,p.Id),Session.PlayerControl,rev)));}
    }
}
