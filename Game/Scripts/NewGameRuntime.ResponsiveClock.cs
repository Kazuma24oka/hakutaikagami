using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    Task<PreparedPause>? clockJob;
    CancellationTokenSource? clockStop;
    bool? clockDesired;
    readonly List<(BaseButton Button,bool Disabled)> clockDisabled=[];
    internal bool IsClockControlling=>clockDesired.HasValue||IsManualAdvancing;
    internal string? LastClockError {get;private set;}
    internal Action? ClockTestFault {get;set;}
    internal long ClockRenderTicks {get;private set;}
    internal long ClockPublications {get;private set;}
    static bool ClockAllowed(string id)=>id is "pause" or "menu" or "rotate" or "near" or "play-guide";
    void RequestClockPause(bool paused)
    {
        if(IsSaving)return;CancelManualAdvance();LastClockError=null;
        automatic=false;CancelAdvancePrefetch();advanceClockActive=false;
        if(IsClockControlling){clockDesired=paused;clockStop?.Cancel();message.Text=paused?"停止を確認中…":"自動進行を準備中…";return;}
        if(Session.Paused==paused){automatic=!paused;return;}
        clockDesired=paused;LastClockError=null;ClockRenderTicks=0;
        clockDisabled.Clear();
        var allowed=buttons.Where(p=>ClockAllowed(p.Key)).Select(p=>(BaseButton)p.Value).ToHashSet();
        foreach(var button in SaveDescendants(this).OfType<BaseButton>())if(!allowed.Contains(button)){clockDisabled.Add((button,button.Disabled));button.Disabled=true;}
        message.Text=paused?"停止を確認中…":"自動進行を準備中…";
    }
    void RetainClockInputLock(){if(IsClockControlling)foreach(var pair in clockDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=true;}
    void ReleaseClockUi()
    {
        clockDesired=null;clockStop?.Dispose();clockStop=null;clockJob=null;
        foreach(var pair in clockDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=pair.Disabled;
        clockDisabled.Clear();
    }
    void CancelClockControl(bool announce=true)
    {
        bool pending=IsClockControlling;clockStop?.Cancel();
        if(clockJob!=null)_=clockJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        ReleaseClockUi();
        if(pending&&announce&&message!=null&&IsInstanceValid(message))message.Text="開始・停止の確認を取り消しました。現在の世界を保持しています。";
    }
    bool PollClockControl(double delta)
    {
        if(!clockDesired.HasValue)return false;
        ClockRenderTicks++;
        try{
            // Reap the canceled next-second job before starting another worker.
            ClearCompletedAdvance();
            if(advanceJob!=null)return true;
            if(clockJob!=null&&clockJob.IsCompleted&&clockStop!.IsCancellationRequested){_=clockJob.Exception;clockJob=null;clockStop.Dispose();clockStop=null;}
            if(clockJob==null){var capture=Session.CapturePause(clockDesired.Value,Session.Revision);clockStop=new();var token=clockStop.Token;var fault=ClockTestFault;clockJob=preparations.Run(()=>capture.Prepare(token,fault),token);}
            if(!clockJob.IsCompleted)return true;
            var ready=clockJob.GetAwaiter().GetResult();var code=host.CommitPause(ready,out var display);
            if(code!=ResultCode.Accepted)throw new InvalidOperationException("Clock source changed");
            InstallWorldDisplay(display!);ClockPublications++;automatic=!clockDesired.Value;advanceClockActive=false;
            message.Text=clockDesired.Value?"世界時間を停止しました。":"自動進行を開始しました。";ReleaseClockUi();Refresh();return true;
        }catch(Exception error){
            LastClockError=error.GetType().Name;automatic=false;clockStop?.Cancel();ReleaseClockUi();
            message.Text="開始・停止を確定できませんでした。現在の世界は保持しています。もう一度操作できます。";GD.Print("CLOCK_UI_FAILURE "+LastClockError);Refresh();return true;
        }
    }
}
