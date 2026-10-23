using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
 Task<SaveResult>? manualSaveJob;
 readonly List<(BaseButton Button,bool Disabled)> saveDisabled=[];
 readonly Stopwatch saveClock=new();double savePulse;
 internal bool IsSaving=>manualSaveJob!=null;
 internal string? LastSaveError {get;private set;}
 internal bool? LastSavePublished {get;private set;}
 internal string SaveUiMessage=>message.Text;
 internal double LastSaveSeconds {get;private set;}
 internal double LastSaveCaptureMilliseconds {get;private set;}
 internal long SaveRenderTicks {get;private set;}
 internal int ManualSaveStarts {get;private set;}
 internal Action<SaveStage>? SaveTestFault {get;set;}
 internal PreparedLoad ReviewManualSave(){if(IsSaving)throw new InvalidOperationException("save pending");return saves.Prepare("manual");}
 static IEnumerable<Node> SaveDescendants(Node root){foreach(Node child in root.GetChildren()){yield return child;foreach(var descendant in SaveDescendants(child))yield return descendant;}}
 void RequestManualSave()
 {
  if(IsSaving)return;CancelAdvancePrefetch();
  automatic=false;Session.SetPaused(true);LastSaveError=null;LastSavePublished=null;saveClock.Restart();
  // Snapshot and presentation are captured together on the UI thread. The worker
  // owns this detached snapshot and never reads the live session or Godot nodes.
  var snapshot=Session.DiagnosticSnapshot();var display=CurrentPresentation();var store=saves;var fault=SaveTestFault;
  saveDisabled.Clear();foreach(var button in SaveDescendants(this).OfType<BaseButton>()){saveDisabled.Add((button,button.Disabled));button.Disabled=true;}
  SaveRenderTicks=0;savePulse=0;ManualSaveStarts++;message.Text="保存中… 世界を停止し、保存内容を確認しています。";
  manualSaveJob=Task.Run(()=>{var frozen=new WorldSession(snapshot);return store.Save(frozen,"manual",snapshot.WorldId,display,fault);});
  LastSaveCaptureMilliseconds=saveClock.Elapsed.TotalMilliseconds;
 }
 void RetainSaveInputLock(){if(IsSaving)foreach(var pair in saveDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=true;}
 bool PollManualSave(double delta)
 {
  if(manualSaveJob==null)return false;
  SaveRenderTicks++;savePulse+=delta;
  if(!manualSaveJob.IsCompleted){message.Text="保存中"+new string('・',(int)(savePulse*4)%4+1)+" 世界を停止し、保存内容を確認しています。";RetainSaveInputLock();return true;}
  try{var result=manualSaveJob.GetAwaiter().GetResult();LastSavePublished=result.Published;message.Text=result.Published?"同じ人物・世界の進行を保存しました。":"より新しい保存があるため上書きしませんでした。現在の進行は維持しています。";}
  catch(Exception error){LastSaveError=error.GetType().Name;message.Text="保存できませんでした。前の保存は保持しています。もう一度保存できます。";GD.Print("SAVE_UI_FAILURE "+LastSaveError);}
  finally{LastSaveSeconds=saveClock.Elapsed.TotalSeconds;saveClock.Stop();manualSaveJob=null;foreach(var pair in saveDisabled)if(IsInstanceValid(pair.Button))pair.Button.Disabled=pair.Disabled;saveDisabled.Clear();}
  Refresh();return false;
 }
}
