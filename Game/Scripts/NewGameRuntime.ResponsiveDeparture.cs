using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    SubmitCapture? pendingDeparture;
    Task<PreparedSubmit>? departureJob;
    CancellationTokenSource? departureStop;
    bool departureCanceled,departureResume,preparedLocalIntent;
    string preparedIntentLabel="出発";
    readonly List<(BaseButton Button,bool Disabled)> departureDisabled=[];
    internal bool IsDeparting=>pendingDeparture!=null||departureJob!=null&&!departureCanceled;
    internal bool DepartureWorkerPending=>departureJob!=null;
    internal string? LastDepartureError {get;private set;}
    internal ResultCode? LastDepartureResult {get;private set;}
    internal Action? DepartureTestFault {get;set;}
    internal long DepartureRenderTicks {get;private set;}
    internal long DeparturePublications {get;private set;}
    internal long LocalIntentPublications {get;private set;}
    internal Action? LocalIntentTestFault {get;set;}
    internal string? LastLocalIntentError=>preparedLocalIntent?LastDepartureError:null;
    internal ResultCode? LastLocalIntentResult=>preparedLocalIntent?LastDepartureResult:null;
    internal long LocalIntentRenderTicks=>preparedLocalIntent?DepartureRenderTicks:0;
    void RequestDeparture(Intent input)=>RequestPreparedIntent(input,true,"出発",false);
    void RequestLocalIntent(Intent input,string label)=>RequestPreparedIntent(input,false,label,true);
    void RequestPreparedIntent(Intent input,bool unpause,string label,bool local)
    {
        if(IsSaving||IsClockControlling)return;
        StartPreparedCapture(Session.CaptureSubmit(input,Session.Revision,unpause),label,local);
    }
    void StartPreparedCapture(SubmitCapture capture,string label,bool local)
    {
        if(IsSaving||IsClockControlling||IsDeparting)return;
        preparedLocalIntent=local;preparedIntentLabel=label;pendingDeparture=capture;departureResume=automatic;automatic=false;fraction=0;CancelAdvancePrefetch();advanceClockActive=false;
        LastDepartureError=null;LastDepartureResult=null;DepartureRenderTicks=0;
        departureDisabled.Clear();var allowed=buttons.Where(p=>ClockAllowed(p.Key)).Select(p=>(BaseButton)p.Value).ToHashSet();
        foreach(var button in SaveDescendants(this).OfType<BaseButton>())if(!allowed.Contains(button)){departureDisabled.Add((button,button.Disabled));button.Disabled=true;}
        message.Text=local?label+"を確認中… 確定する前なら停止・メニューで取り消せます。":"出発の指示を確認中… 確定する前なら停止・メニューで取り消せます。";
    }
    void RetainDepartureInputLock(){if(IsDeparting)foreach(var pair in departureDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=true;}
    void ReleaseDepartureUi(){foreach(var pair in departureDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=pair.Disabled;departureDisabled.Clear();}
    void CancelDeparture(bool announce=true)
    {
        bool pending=IsDeparting;pendingDeparture=null;if(departureJob!=null){departureCanceled=true;departureStop?.Cancel();}ReleaseDepartureUi();
        if(pending&&announce&&message!=null&&IsInstanceValid(message))message.Text=preparedLocalIntent?"未確定の"+preparedIntentLabel+"を取り消しました。現在の世界を保持しています。":"未確定の出発指示を取り消しました。現在の世界を保持しています。";
    }
    void ClearDepartureJob(){if(departureJob==null||!departureJob.IsCompleted)return;_=departureJob.Exception;departureJob=null;departureStop?.Dispose();departureStop=null;departureCanceled=false;}
    bool PollDeparture(double delta)
    {
        if(IsDeparting||DepartureWorkerPending)DepartureRenderTicks++;
        if(departureJob!=null&&departureCanceled){if(!departureJob.IsCompleted)return true;ClearDepartureJob();}
        if(!IsDeparting)return false;
        try{
            ClearCompletedAdvance();if(advanceJob!=null)return true;
            if(departureJob==null){var capture=pendingDeparture!;pendingDeparture=null;departureStop=new();var token=departureStop.Token;var fault=preparedLocalIntent?LocalIntentTestFault:DepartureTestFault;departureCanceled=false;departureJob=preparations.Run(()=>capture.Prepare(token,fault),token);}
            if(!departureJob.IsCompleted)return true;
            var ready=departureJob.GetAwaiter().GetResult();if(host.CommitSubmit(ready,out var publication)!=ResultCode.Accepted)throw new InvalidOperationException("Departure source changed");
            InstallWorldDisplay(publication!.Display);LastDepartureResult=publication.Result.Code;if(preparedLocalIntent)LocalIntentPublications++;else DeparturePublications++;automatic=departureResume&&!Session.Paused;
            message.Text=publication.Result.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted?publication.Result.VisibleReason:(preparedLocalIntent?preparedIntentLabel+"を受け付けられません：":"出発できません：")+publication.Result.VisibleReason;
            ClearDepartureJob();ReleaseDepartureUi();Refresh();return true;
        }catch(Exception error){
            LastDepartureError=error.GetType().Name;departureStop?.Cancel();pendingDeparture=null;ClearDepartureJob();automatic=departureResume&&!Session.Paused;
            ReleaseDepartureUi();message.Text=preparedLocalIntent?preparedIntentLabel+"を確定できませんでした。現在の世界を保持しています。もう一度操作できます。":"出発を確定できませんでした。現在の世界を保持しています。もう一度操作できます。";GD.Print((preparedLocalIntent?"LOCAL_INTENT_UI_FAILURE ":"DEPARTURE_UI_FAILURE ")+LastDepartureError);Refresh();return true;
        }
    }
    void AbandonDepartureOnExit(){CancelDeparture(false);if(departureJob!=null)_=departureJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);departureJob=null;departureStop?.Dispose();departureStop=null;}
}
