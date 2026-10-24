using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    long? manualAdvanceTarget;
    int manualAdvancePhase;
    Task<PreparedPause>? manualAdvancePause;
    Task<PreparedAdvance>? manualAdvanceWork;
    CancellationTokenSource? manualAdvanceStop;
    readonly List<(BaseButton Button,bool Disabled)> manualAdvanceDisabled=[];
    internal bool IsManualAdvancing=>manualAdvanceTarget.HasValue;
    internal bool ManualAdvanceWorkerPending=>manualAdvancePause!=null||manualAdvanceWork!=null;
    internal Action? ManualAdvanceTestFault {get;set;}
    internal string? LastManualAdvanceError {get;private set;}
    internal long ManualAdvanceRenderTicks {get;private set;}
    internal long ManualAdvancePublications {get;private set;}
    void RequestManualAdvance(long to)
    {
        if(IsSaving||IsClockControlling||to<=Session.Seconds)return;
        automatic=false;fraction=0;CancelAdvancePrefetch();advanceClockActive=false;
        manualAdvanceTarget=to;manualAdvancePhase=0;LastManualAdvanceError=null;ManualAdvanceRenderTicks=0;
        manualAdvanceDisabled.Clear();var allowed=buttons.Where(p=>ClockAllowed(p.Key)).Select(p=>(BaseButton)p.Value).ToHashSet();
        foreach(var button in SaveDescendants(this).OfType<BaseButton>())if(!allowed.Contains(button)){manualAdvanceDisabled.Add((button,button.Disabled));button.Disabled=true;}
        message.Text="指定時刻まで進行を確認中… 停止すると、確定済みの現在位置で止まります。";
    }
    void RetainManualAdvanceInputLock(){if(IsManualAdvancing)foreach(var pair in manualAdvanceDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=true;}
    void ReleaseManualAdvanceUi()
    {
        manualAdvanceTarget=null;foreach(var pair in manualAdvanceDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=pair.Disabled;manualAdvanceDisabled.Clear();
    }
    void CancelManualAdvance(bool announce=true)
    {
        bool pending=IsManualAdvancing;manualAdvanceStop?.Cancel();ReleaseManualAdvanceUi();
        if(pending&&announce&&message!=null&&IsInstanceValid(message))message.Text="指定時刻への進行を取り消しました。確定済みの現在位置を保持しています。";
    }
    void ReapManualAdvance()
    {
        if(manualAdvancePause?.IsCompleted==true){_=manualAdvancePause.Exception;manualAdvancePause=null;}
        if(manualAdvanceWork?.IsCompleted==true){_=manualAdvanceWork.Exception;manualAdvanceWork=null;}
        if(!ManualAdvanceWorkerPending){manualAdvanceStop?.Dispose();manualAdvanceStop=null;}
    }
    bool PollManualAdvance(double delta)
    {
        if(!IsManualAdvancing){if(!ManualAdvanceWorkerPending)return false;ReapManualAdvance();return ManualAdvanceWorkerPending;}
        long target=manualAdvanceTarget??throw new InvalidOperationException("No manual target");
        ManualAdvanceRenderTicks++;
        try{
            ClearCompletedAdvance();if(advanceJob!=null)return true;
            // Canceled prior work must leave before any new private worker starts.
            if(manualAdvanceStop?.IsCancellationRequested==true){ReapManualAdvance();if(ManualAdvanceWorkerPending)return true;}
            if(manualAdvancePhase is 0 or 2){
                bool paused=manualAdvancePhase==2;
                if(manualAdvancePause==null){var capture=Session.CapturePause(paused,Session.Revision);manualAdvanceStop=new();var token=manualAdvanceStop.Token;manualAdvancePause=Task.Run(()=>capture.Prepare(token));}
                if(!manualAdvancePause.IsCompleted)return true;
                var ready=manualAdvancePause.GetAwaiter().GetResult();if(host.CommitPause(ready,out var display)!=ResultCode.Accepted)throw new InvalidOperationException("Manual pause source changed");
                InstallWorldDisplay(display!);manualAdvancePause=null;manualAdvanceStop!.Dispose();manualAdvanceStop=null;
                if(paused){bool failed=LastManualAdvanceError!=null;ReleaseManualAdvanceUi();message.Text=failed?"時間を進められませんでした。確定済みの世界を停止しました。再操作できます。":"指定時刻または停止すべき出来事で止まりました。";Refresh();return true;}
                manualAdvancePhase=1;Refresh();return true;
            }
            if(manualAdvanceWork==null){var capture=Session.CaptureAdvance(target,Session.Revision);manualAdvanceStop=new();var token=manualAdvanceStop.Token;var fault=ManualAdvanceTestFault;manualAdvanceWork=Task.Run(()=>capture.Prepare(token,fault));}
            if(!manualAdvanceWork.IsCompleted)return true;
            long prior=Session.Seconds;var prepared=manualAdvanceWork.GetAwaiter().GetResult();if(host.CommitAdvance(prepared,out var publication)!=ResultCode.Accepted)throw new InvalidOperationException("Manual advance source changed");
            InstallAdvanceDisplay(publication!);ManualAdvancePublications++;manualAdvanceWork=null;manualAdvanceStop!.Dispose();manualAdvanceStop=null;
            if(Session.Paused||Session.Seconds>=target)manualAdvancePhase=2;
            else if(Session.Seconds==prior)throw new InvalidOperationException("Manual time made no progress");
            Refresh();return true;
        }catch(Exception error){
            LastManualAdvanceError=error.GetType().Name;GD.Print("MANUAL_ADVANCE_UI_FAILURE "+LastManualAdvanceError);
            manualAdvanceStop?.Cancel();ReapManualAdvance();
            if(manualAdvancePhase==1){manualAdvancePhase=2;return true;}
            ReleaseManualAdvanceUi();message.Text="時間の変更を確定できませんでした。確定済みの世界を保持しています。停止や再操作ができます。";Refresh();return true;
        }
    }
    void AbandonManualAdvanceOnExit()
    {
        CancelManualAdvance(false);
        if(manualAdvancePause!=null)_=manualAdvancePause.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        if(manualAdvanceWork!=null)_=manualAdvanceWork.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        manualAdvancePause=null;manualAdvanceWork=null;manualAdvanceStop?.Dispose();manualAdvanceStop=null;
    }
}
