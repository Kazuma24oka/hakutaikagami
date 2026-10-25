using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 Task<SaveResult>? manualSaveJob;
 Task<PreparedPause>? savePrepareJob;
 PauseCapture? saveCapture;
 CancellationTokenSource? savePrepareStop;
 SavePresentation? saveView;
 GenerationSaveStore? saveTarget;
 Action<SaveStage>? saveFault;
 bool saveWaiting;
 readonly List<(BaseButton Button,bool Disabled)> saveDisabled=[];
 readonly Stopwatch saveClock=new();double savePulse;
 internal bool IsSaving=>saveWaiting||savePrepareJob!=null||manualSaveJob!=null;
 internal string? LastSaveError {get;private set;}
 internal bool? LastSavePublished {get;private set;}
 internal string SaveUiMessage=>message.Text;
 internal double LastSaveSeconds {get;private set;}
 internal double LastSaveCaptureMilliseconds {get;private set;}
 internal long SaveRenderTicks {get;private set;}
 internal int ManualSaveStarts {get;private set;}
 internal Action<SaveStage>? SaveTestFault {get;set;}
 internal Action? SavePrepareTestFault {get;set;}
 internal PreparedLoad ReviewManualSave(){if(IsSaving)throw new InvalidOperationException("save pending");return saves.Prepare("manual");}
 internal string ReviewPublishedGenerationId()=>saves.Prepare("manual").GenerationId;
 static IEnumerable<Node> SaveDescendants(Node root){foreach(Node child in root.GetChildren()){yield return child;foreach(var descendant in SaveDescendants(child))yield return descendant;}}
 void RequestManualSave()
 {
  if(IsSaving||IsClockControlling)return;CancelAdvancePrefetch();automatic=false;advanceClockActive=false;
  LastSaveError=null;LastSavePublished=null;saveClock.Restart();
  // Opaque capture and scalar presentation only. No world copy/validation on UI.
  saveCapture=Session.CapturePause(true,Session.Revision);saveView=CurrentPresentation();saveTarget=saves;saveFault=SaveTestFault;savePrepareStop=new();saveWaiting=true;
  saveDisabled.Clear();foreach(var button in SaveDescendants(this).OfType<BaseButton>()){saveDisabled.Add((button,button.Disabled));button.Disabled=true;}
  SaveRenderTicks=0;savePulse=0;ManualSaveStarts++;message.Text="保存中… 世界を停止し、保存内容を確認しています。";
  LastSaveCaptureMilliseconds=saveClock.Elapsed.TotalMilliseconds;
 }
 void RetainSaveInputLock(){if(IsSaving)foreach(var pair in saveDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=true;}
 void ClearSaveContext()
 {
  saveWaiting=false;savePrepareJob=null;manualSaveJob=null;savePrepareStop?.Dispose();savePrepareStop=null;saveCapture=null;saveView=null;saveTarget=null;saveFault=null;
 }
 void CancelManualSavePreparationOnExit()
 {
  savePrepareStop?.Cancel();
  if(savePrepareJob!=null)_=savePrepareJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
  // A file writer already saving an accepted world retains the existing atomic
  // store protocol. Private preparation alone can never publish a save.
  if(manualSaveJob!=null)_=manualSaveJob.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
  ClearSaveContext();
 }
 void FinishManualSave()
 {
  LastSaveSeconds=saveClock.Elapsed.TotalSeconds;saveClock.Stop();ClearSaveContext();
  foreach(var pair in saveDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=pair.Disabled;saveDisabled.Clear();Refresh();
 }
 bool PollManualSave(double delta)
 {
  if(!IsSaving)return false;SaveRenderTicks++;savePulse+=delta;
  try{
   if(saveWaiting){
    ClearCompletedAdvance();
    if(advanceJob==null){var capture=saveCapture!;var token=savePrepareStop!.Token;var fault=SavePrepareTestFault;savePrepareJob=preparations.Run(()=>capture.Prepare(token,fault),token);saveWaiting=false;}
   }
   if(savePrepareJob?.IsCompleted==true){
    var ready=savePrepareJob.GetAwaiter().GetResult();var code=host.CommitPause(ready,out var display);
    if(code!=ResultCode.Accepted)throw new InvalidOperationException("Save source changed");
    InstallWorldDisplay(display!);savePrepareJob=null;savePrepareStop!.Dispose();savePrepareStop=null;
    // Only an accepted confirmed world reaches the file writer. With inputs
    // locked and automatic progression suspended, this session cannot change.
    var frozen=Session;var id=display!.WorldId;var target=saveTarget!;var presentation=saveView!;var fault=saveFault;
    manualSaveJob=preparations.Run(()=>target.Save(frozen,"manual",id,presentation,fault),CancellationToken.None);
   }
   if(manualSaveJob?.IsCompleted==true){
    var result=manualSaveJob.GetAwaiter().GetResult();LastSavePublished=result.Published;message.Text=result.Published?"同じ人物・世界の進行を保存しました。":"より新しい保存があるため上書きしませんでした。現在の進行は維持しています。";FinishManualSave();return false;
   }
   message.Text="保存中"+new string('・',(int)(savePulse*4)%4+1)+" 世界の停止と保存内容を確認しています。";RetainSaveInputLock();return true;
  }catch(Exception error){
   LastSaveError=error.GetType().Name;LastSavePublished=null;savePrepareStop?.Cancel();message.Text="保存できませんでした。現在の世界と前の保存は保持しています。もう一度保存できます。";GD.Print("SAVE_UI_FAILURE "+LastSaveError);FinishManualSave();return false;
  }
 }
}
