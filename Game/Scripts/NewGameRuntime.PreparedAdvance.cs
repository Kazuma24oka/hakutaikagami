using System;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using Godot;
public partial class NewGameRuntime
{
    Task<PreparedAdvance>? advanceJob;
    AdvanceCapture? advanceCapture;
    CancellationTokenSource? advanceStop;
    bool advanceCanceled,advanceClockActive;
    int advanceSpeed;
    internal Action<long,long,long,int,string>? AdvancePublishedTestHook {get;set;}
    double advanceClock;
    internal Action? AdvanceTestFault {get;set;}
    internal long AdvancePublications {get;private set;}
    internal long AdvanceDiscards {get;private set;}
    internal long AdvanceRenderTicks {get;private set;}
    internal long AdvanceFailures {get;private set;}
    internal string? LastAdvanceError {get;private set;}
    internal bool AdvanceWorkerPending=>advanceJob!=null;
    internal bool ReviewAutomatic=>automatic;
    void CancelAdvancePrefetch()
    {
        if(advanceJob==null||advanceCanceled)return;
        advanceCanceled=true;advanceStop!.Cancel();AdvanceDiscards++;
    }
    void ClearCompletedAdvance()
    {
        if(advanceJob==null||!advanceJob.IsCompleted)return;
        _=advanceJob.Exception;advanceJob=null;advanceCapture=null;advanceStop?.Dispose();advanceStop=null;advanceCanceled=false;
    }
    void StartAdvancePrefetch()
    {
        if(advanceJob!=null)return;
        var source=Session;long quantum=speed==1?1:Math.Clamp((long)Math.Max(Math.Ceiling(speed/5d),Math.Floor(advanceClock)),1,speed);
        long target=checked(source.Seconds+quantum);
        if(AdvanceTargetTestHook is {} choose){target=choose(source.Seconds);if(target<=source.Seconds||target-source.Seconds>speed)throw new InvalidOperationException("Invalid bounded QA advance target");}
        advanceSpeed=speed;advanceCapture=source.CaptureAdvance(target,source.Revision);advanceStop=new();advanceCanceled=false;
        var capture=advanceCapture;var token=advanceStop.Token;var fault=AdvanceTestFault;
        // Core only: no Godot nodes, live-session mutation or future view access.
        advanceJob=preparations.Run(()=>capture.Prepare(token,fault),token);
    }
    void InstallAdvanceDisplay(AdvancePublication publication)=>InstallWorldDisplay(publication.Display);
    void InstallWorldDisplay(ContractWorld display)
    {
        // Independent display copy was built on the worker, not on this frame.
        cachedSession=Session;cachedWorld=display;cachedRevision=cachedWorld.CommitSequence;cachedSeconds=cachedWorld.ElapsedSeconds;cachedPaused=cachedWorld.Paused;
        cachedBranches.Clear();cachedServiceView=null;cachedNearby=null;
    }
    bool PollPreparedAdvance(double delta)
    {
        if(!automatic||Session.Paused){CancelAdvancePrefetch();ClearCompletedAdvance();advanceClockActive=false;return false;}
        if(!advanceClockActive){advanceClock=fraction;advanceClockActive=true;}
        advanceClock+=Math.Min(delta,.25)*speed;AdvanceRenderTicks++;
        if(advanceJob!=null&&(!advanceCapture!.Matches(Session)||advanceCanceled||advanceSpeed!=speed)){
            CancelAdvancePrefetch();ClearCompletedAdvance();
        }
        try{
            StartAdvancePrefetch();
            // A result can be ready early; never publish or inspect it before its
            // captured authorized boundary. Normal inputs remain enabled and make
            // the capture stale rather than competing with the worker's world.
            if(advanceClock>=advanceCapture!.Target-advanceCapture.SourceSeconds&&advanceJob!.IsCompleted&&!advanceCanceled){
                var captured=advanceCapture!;var ready=advanceJob.GetAwaiter().GetResult();
                var code=host.CommitAdvance(ready,out var publication);
                if(code==ResultCode.Accepted){
                    long moved=publication!.Result.ReachedAt-captured.SourceSeconds;
                    InstallAdvanceDisplay(publication);AdvancePublications++;advanceClock=Math.Max(0,advanceClock-moved);
                    AdvancePublishedTestHook?.Invoke(captured.SourceSeconds,captured.Target,publication.Result.ReachedAt,publication.Result.Processed,publication.Result.StopReason);
                    if(LastAdvanceError!=null){LastAdvanceError=null;message.Text="自動進行を再開しました。";}
                    if(Session.Paused){automatic=false;advanceClock=0;}
                    if(moved==0&&!Session.Paused)throw new InvalidOperationException("Time update made no progress");
                }else AdvanceDiscards++;
                ClearCompletedAdvance();
                if(automatic&&!Session.Paused)StartAdvancePrefetch();
            }
        }catch(Exception error){
            LastAdvanceError=error.GetType().Name;AdvanceFailures++;automatic=false;advanceClock=0;CancelAdvancePrefetch();ClearCompletedAdvance();Session.SetPaused(true);
            message.Text="時間を進められませんでした。現在の世界と保存は保持しています。自動進行をやり直せます。";GD.Print("ADVANCE_UI_FAILURE "+LastAdvanceError);
        }
        // At most the already-authorized next-second interpolation is displayed.
        // Higher speeds use bounded batches. Never interpolate across an unconfirmed
        // integer event boundary or expose private future facts. Actual rate is CPU-dependent.
        fraction=Math.Clamp(advanceClock,0,.999999999);Refresh();return true;
    }
    public override void _ExitTree()
    {
        AbandonManualAdvanceOnExit();
        CancelManualSavePreparationOnExit();
        CancelClockControl(false);
        CancelAdvancePrefetch();
        if(advanceJob!=null){_ =advanceJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);advanceJob=null;}
        advanceStop?.Dispose();advanceStop=null;
        preparations.CloseWhenIdle();
    }
}
