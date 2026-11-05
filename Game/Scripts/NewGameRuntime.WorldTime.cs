using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    AcceptDialog? timeSettings;
    AcceptDialog? historySummary;
    long? displayedSummary;
    static string TimeKindName(TimeNoticeKind kind)=>kind switch{
        TimeNoticeKind.ImportantLetter=>"重要な書状の到着",TimeNoticeKind.Deadline=>"期限の直前",TimeNoticeKind.Arrival=>"目的地への到着",TimeNoticeKind.EnemyApproach=>"認識した敵軍の接近",TimeNoticeKind.Battle=>"目の前の合戦",TimeNoticeKind.Encounter=>"遭遇・襲撃",TimeNoticeKind.Council=>"評定の知らせ",TimeNoticeKind.Death=>"訃報",_=>"1650年の総括"};
    void BuildWorldTime(HFlowContainer bar)
    {
        if(!WorldTimeControl.Enabled(Snapshot()))return;
        Add(bar,"time-next","次の知らせまで",()=>RequestManualAdvance(checked(Session.Seconds+3*WorldCalendar.SecondsPerDay),true));
        Add(bar,"time-settings","時間の設定",ShowTimeSettings);
        Add(bar,"history-summary","1650年の総括",ShowHistorySummary);
    }
    void ShowTimeSettings()
    {
        RequestClockPause(true);
        timeSettings?.QueueFree();timeSettings=new(){Title="自動進行の停止・減速",Exclusive=true};AddChild(timeSettings);
        var box=new VBoxContainer();timeSettings.AddChild(box);
        box.AddChild(new Label{Text="本人が知った出来事だけが対象です。\n「次の知らせまで」は設定にかかわらず止まります（最大3日）。"});
        foreach(var rule in Snapshot().TimeControl!.Rules.Where(r=>r.Kind!=TimeNoticeKind.HistoricalEnd)){
            var row=new HBoxContainer();box.AddChild(row);row.AddChild(new Label{Text=TimeKindName(rule.Kind),CustomMinimumSize=new(210,0)});
            var choice=new OptionButton();foreach(var name in new[]{"通知のみ","60倍へ減速","停止"})choice.AddItem(name);choice.Select((int)rule.Response);row.AddChild(choice);
            choice.ItemSelected+=i=>{if(IsClockControlling){choice.Select((int)Snapshot().TimeControl!.Rules.Single(r=>r.Kind==rule.Kind).Response);return;}CancelAdvancePrefetch();Submit(Session.ConfigureTime(Snapshot().TimeControl!.Rules.Select(r=>r.Kind==rule.Kind?r with{Response=(TimeResponse)i}:r).ToArray(),Session.Revision));};
        }
        timeSettings.PopupCentered(new(520,400));
    }
    void RefreshWorldTime()
    {
        if(!WorldTimeControl.Enabled(Snapshot()))return;
        var state=Snapshot().TimeControl!;
        if(buttons.TryGetValue("history-summary",out var button))button.Visible=state.SummaryAt!=null;
        if(state.SummaryAt is {} at&&state.ContinuedAt==null&&displayedSummary!=at){displayedSummary=at;CallDeferred(nameof(ShowHistorySummary));}
    }
    void ShowHistorySummary()
    {
        var w=Snapshot();if(w.TimeControl?.SummaryAt==null)return;
        historySummary?.QueueFree();historySummary=new(){Title="1650年の総括",Exclusive=true};AddChild(historySummary);
        // 本人の実行と本人に届いた記録だけを数える。全国の未認識の成果を表示しない。
        var own=w.TimeControl.Summary!.CompletedOwnTasks;
        var known=w.TimeControl.Summary!.KnownRecords;
        historySummary.DialogText=$"{WorldCalendar.Display(w.EpochDate,w.TimeControl.SummaryAt.Value)}\n本人が完了した用件：{own}\n本人が得た記録：{known}\n\nこの先は史実の制御を外した世界として継続できます。";
        historySummary.OkButtonText="閉じる（停止を維持）";
        if(w.TimeControl.ContinuedAt==null){var b=historySummary.AddButton("仮想の世界として継続",true,"continue");historySummary.CustomAction+=a=>{if(a!="continue")return;CancelAdvancePrefetch();Submit(Session.ContinueAfterHistory(Session.Revision));historySummary.Hide();Refresh();};}
        historySummary.PopupCentered(new(580,300));
    }
}
