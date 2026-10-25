using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 readonly List<object> saveUiMeasurements=[];int saveUiProbe;
 string SaveProbePrefix=>OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore":"check";
 async Task ProbeResponsiveSave(bool advance=true)
 {
  var g=game!;var s=g.Session;var old=g.ReviewManualSave();if(advance)g.Advance(s.Seconds+2);var expected=ContractJson.CanonicalHash(s.DiagnosticSnapshot());string pose=g.Pose();long at=s.Seconds,revision=s.Revision;bool failure=FlowCase=="saveui-failure";int starts=g.ManualSaveStarts;int probe=saveUiProbe++;
  if(failure)g.SaveTestFault=stage=>{if(stage==SaveStage.PayloadWritten)throw new IOException("QA interrupted after immutable payload write");};
  var timer=Stopwatch.StartNew();g.Press("save");double captureMs=g.LastSaveCaptureMilliseconds;
  Check(g.IsSaving&&!g.ButtonEnabled("save")&&!g.ButtonEnabled("load")&&!g.ButtonEnabled("ten")&&!g.ButtonEnabled("menu"),"real save button locks competing actions while job is pending");
  foreach(string id in new[]{"save","load","ten","auto","menu"})g.Press(id);
  Check(g.ManualSaveStarts==starts+1&&g.Session==s&&s.Seconds==at&&s.Revision==revision,"duplicate save, load, clock and menu signals cannot replace captured generation");
  bool film=OS.GetCmdlineUserArgs().Contains("--responsive-save-film")&&probe==0;var frameTimes=new List<double>();string filmDir=Path.Combine(TestOutput,"film");if(film)Directory.CreateDirectory(filmDir);int frames=0;double maxFrameMs=0;double previous=timer.Elapsed.TotalMilliseconds;
  while(g.IsSaving){await Frame();frames++;double now=timer.Elapsed.TotalMilliseconds;maxFrameMs=Math.Max(maxFrameMs,now-previous);previous=now;Check(s.Seconds==at&&s.Revision==revision&&s.Paused&&g.Session.Seconds==at&&g.Session.Revision==revision&&g.Session.Paused,"source and current confirmed world remain stopped while rendered frames continue");if(film){frameTimes.Add(timer.Elapsed.TotalSeconds);ViewportCapture.Save(GetViewport(),Path.Combine(filmDir,"save-"+frames.ToString("D4")+".png"));}if(g.IsSaving&&(frames==1||frames==8))ViewportCapture.Save(GetViewport(),Path.Combine(TestOutput,"busy-"+SaveProbePrefix+"-"+probe+"-"+frames+".png"));if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("responsive save");}
  if(film)File.WriteAllText(Path.Combine(filmDir,"times.json"),System.Text.Json.JsonSerializer.Serialize(frameTimes));g.SaveTestFault=null;Check(frames>=3&&g.SaveRenderTicks>=3,"actual background validation/write permits multiple real rendered frames without injected delay");
  if(failure){Check(g.LastSaveError=="IOException"&&g.ReviewManualSave().GenerationId==old.GenerationId,"interrupted write leaves previous published save selected and retry available");Check(g.ButtonEnabled("save")&&g.ButtonEnabled("load"),"failure restores save/load controls");await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(TestOutput,"failure-visible-"+SaveProbePrefix+"-"+probe+".png"));g.Press("save");await WaitSave();}
  else Check(g.LastSaveError==null,"successful actual UI save has no failure");Check(g.LastSavePublished==true,"current revision really published before actual load");
  Check(ContractJson.CanonicalHash(s.DiagnosticSnapshot())==expected,"background save never changes live world, custody, knowledge or route");g.Press("load");g.Refresh();Check(g.Pose()==pose&&ContractJson.CanonicalHash(g.Session.DiagnosticSnapshot())==expected,"actual load button restores captured full world, target, presentation and 3D pose");
  saveUiMeasurements.Add(new{Probe=probe,FailureInjected=failure,RenderedFrames=frames,MainThreadCaptureMilliseconds=captureMs,MeasuredTotalSeconds=timer.Elapsed.TotalSeconds,MaximumInstrumentedFrameMilliseconds=maxFrameMs,NotPerformanceBenchmark=true,CapturedSeconds=at,CapturedRevision=revision});
 }
 async Task ResponsiveSaveStep(int n){await KnownTargetStep(n);if(n==3){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))game!.Advance(game.Session.Seconds+2);else await ProbeResponsiveSave();}}
 async Task BeginResponsiveSaveContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<=4;n++)await RecognitionStep(n);for(int n=0;n<16;n++){await ResponsiveSaveStep(n);game!.SaveCheckpoint("responsive"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreResponsiveSave(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();var newer=game!.ReviewManualSave();game.LoadCheckpoint("responsive0");game.Press("save");await WaitSave();Check(game.LastSavePublished==false&&game.ReviewManualSave().GenerationId==newer.GenerationId&&game.SaveUiMessage.Contains("上書きしませんでした"),"old checkpoint cannot overwrite newer save and UI states that truthfully");for(int n=0;n<16;n++){game!.LoadCheckpoint("responsive"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"same person/target/full 3D after responsive save at"+n);await Capture("restored-s"+n,true);for(int k=n+1;k<16;k++)await ResponsiveSaveStep(k);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same future after responsive save recovery at"+n);await Capture("continued-s"+n,true);GD.Print("RESPONSIVE_SAVE_RESTORED "+n);}await ProbeResponsiveSave(false);await Capture("restored-live-save",true);}
 async Task RunResponsiveSaveChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreResponsiveSave();else await BeginNewMilitaryFlow();File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-save-metrics.json":"check-save-metrics.json"),System.Text.Json.JsonSerializer.Serialize(saveUiMeasurements));File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("RESPONSIVE_SAVE_PASSED "+FlowCase);}
}
