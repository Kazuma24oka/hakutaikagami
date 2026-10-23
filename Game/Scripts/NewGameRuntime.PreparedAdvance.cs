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
        var source=Session;advanceCapture=source.CaptureAdvance(checked(source.Seconds+1),source.Revision);advanceStop=new();advanceCanceled=false;
        var capture=advanceCapture;var token=advanceStop.Token;var fault=AdvanceTestFault;
        // Core only: no Godot nodes, live-session mutation or future view access.
        advanceJob=Task.Run(()=>capture.Prepare(token,fault));
    }
    void InstallAdvanceDisplay(AdvancePublication publication)
    {
        // Independent display copy was built on the worker, not on this frame.
        cachedSession=Session;cachedWorld=publication.Display;cachedRevision=cachedWorld.CommitSequence;cachedSeconds=cachedWorld.ElapsedSeconds;cachedPaused=cachedWorld.Paused;
        cachedBranches.Clear();cachedServiceView=null;cachedNearby=null;
    }
    bool PollPreparedAdvance(double delta)
    {
        if(!automatic||Session.Paused||speed!=1){CancelAdvancePrefetch();ClearCompletedAdvance();advanceClockActive=false;return false;}
        if(!advanceClockActive){advanceClock=fraction;advanceClockActive=true;}
        advanceClock+=Math.Min(delta,.25);AdvanceRenderTicks++;
        if(advanceJob!=null&&(!advanceCapture!.Matches(Session)||advanceCanceled)){
            CancelAdvancePrefetch();ClearCompletedAdvance();
        }
        try{
            StartAdvancePrefetch();
            // A result can be ready early; never publish or inspect it before its
            // actual next-second boundary. Normal inputs remain enabled and make
            // the capture stale rather than competing with the worker's world.
            if(advanceClock>=1&&advanceJob!.IsCompleted&&!advanceCanceled){
                var captured=advanceCapture!;var ready=advanceJob.GetAwaiter().GetResult();
                var code=host.CommitAdvance(ready,out var publication);
                if(code==ResultCode.Accepted){
                    long moved=publication!.Result.ReachedAt-captured.SourceSeconds;
                    InstallAdvanceDisplay(publication);AdvancePublications++;advanceClock=Math.Max(0,advanceClock-moved);
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
        // Higher speed modes retain their existing synchronous/event-budget path.
        fraction=Math.Clamp(advanceClock,0,.999999999);Refresh();return true;
    }
    public override void _ExitTree()
    {
        CancelAdvancePrefetch();
        if(advanceJob!=null){_ =advanceJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);advanceJob=null;}
        advanceStop?.Dispose();advanceStop=null;
    }
}
