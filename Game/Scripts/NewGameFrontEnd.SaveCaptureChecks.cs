using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    int SaveCapturePoints=>FlowCase=="savecapture-normal"?3:2;
    async Task SaveCapturePoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("savecapture"+n);await Capture("s"+n,true);}
    async Task BeginSaveCaptureFlow()
    {
        await BeginPopulationEntry(1000);game!.SetDestination("fork");await PressDeparture();await SaveCapturePoint(0);
        for(int n=1;n<SaveCapturePoints;n++){await SaveCaptureStep(n);await SaveCapturePoint(n);}await Capture("final",true);
    }
    async Task SaveCaptureStep(int point)
    {
        long target=point==1?3:5;await ClockPress(false);await UntilPrefetch(()=>game!.Session.Seconds>=target,"actual1x reaches automatic save checkpoint");Check(game!.Session.Seconds==target,"exact automatic checkpoint before saving");game.ReviewFraction(0);
        var before=game.Session.DiagnosticSnapshot();var expected=new WorldSession(before);expected.SetPaused(true);string expectedHash=ContractJson.CanonicalHash(expected.DiagnosticSnapshot());string beforeHash=ContractJson.CanonicalHash(before);string pose=game.Pose();var old=game.ReviewManualSave();int starts=game.ManualSaveStarts;
        bool prepFault=FlowCase=="savecapture-prepare-fault",writeFault=FlowCase=="savecapture-write-fault";
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        if(prepFault)game.SavePrepareTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("QA held save preparation");throw new IOException("declared-save-preparation-fault");};
        if(writeFault)game.SaveTestFault=stage=>{if(stage==SaveStage.PayloadWritten)throw new IOException("declared-save-payload-fault");};
        var timer=Stopwatch.StartNew();game.Press("save");double click=timer.Elapsed.TotalMilliseconds;double capture=game.LastSaveCaptureMilliseconds;
        Check(game.IsSaving&&!game.ButtonEnabled("save")&&!game.ButtonEnabled("load")&&!game.ButtonEnabled("menu")&&!game.ButtonEnabled("auto"),"actual save request immediately locks competing operations");
        foreach(string id in new[]{"save","load","ten","auto","menu"})game.Press(id);
        Check(game.ManualSaveStarts==starts+1&&beforeHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"duplicate signals do not publish or mutate source before first render poll");
        try{
            if(prepFault){
                await UntilPrefetch(()=>entered.IsSet,"actual save preparation reaches private held gate");for(int n=0;n<160;n++)await Frame();
                Check(beforeHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.SaveRenderTicks>=160,"held private preparation never changes confirmed world while actual rendering continues");
                Check(game.ReviewPublishedGenerationId()==old.GenerationId,"private unconfirmed preparation never publishes a save");await Capture("save-prepare-pending",true);release.Set();
            }
            int frames=0;var intervals=new System.Collections.Generic.List<double>();double prior=timer.Elapsed.TotalMilliseconds;
            while(game.IsSaving){await Frame();frames++;double now=timer.Elapsed.TotalMilliseconds;intervals.Add(now-prior);prior=now;Check(game.Session.Seconds==before.ElapsedSeconds,"actual save pipeline does not advance world time");if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("save capture pipeline");}
            if(prepFault||writeFault){
                Check(game.LastSaveError=="IOException"&&game.LastSavePublished==null&&game.ReviewManualSave().GenerationId==old.GenerationId,"declared failure retains previous published save");
                Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==(prepFault?beforeHash:expectedHash),"preparation failure keeps source; payload failure keeps confirmed pause only");await Capture("save-declared-failure",true);
                game.SavePrepareTestFault=null;game.SaveTestFault=null;game.Press("save");await WaitSave();
            }else Check(frames>=3&&game.SaveRenderTicks>=3,"real preparation and writer permit multiple rendered frames without injected delay");
            Check(game.LastSaveError==null&&game.LastSavePublished.HasValue&&game.Session.Paused,"actual successful save has a confirmed truthful publication result");
            Check(expectedHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"confirmed source equals existing synchronous pause effects exactly");
            if(game.LastSavePublished==true){game.Press("load");game.Refresh();Check(expectedHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&pose==game.Pose(),"actual load restores exact saved world and complete numeric pose");}
            else Check(old.Seconds>game.Session.Seconds&&game.ReviewManualSave().GenerationId==old.GenerationId&&game.SaveUiMessage.Contains("上書きしませんでした"),"older replay keeps newer selected save and states protection truthfully");
            File.WriteAllText(Path.Combine(TestOutput,"savecapture-"+point+".json"),System.Text.Json.JsonSerializer.Serialize(new{RequestClickMilliseconds=click,MainThreadCaptureMilliseconds=capture,TotalSeconds=timer.Elapsed.TotalSeconds,Frames=intervals,PrepareFault=prepFault,WriteFault=writeFault,Scope=prepFault?"Held gate is correctness only; not a rendering/performance benchmark.":"Actual1000 static synthetic people and selected travel; no artificial preparation delay. Timing includes QA/load and is not a fixed-fps claim."}));
        }finally{release.Set();game.SavePrepareTestFault=null;game.SaveTestFault=null;}
        game.ReviewFraction(0);
    }
    async Task RestoreSaveCaptureFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<SaveCapturePoints;n++){
            game!.LoadCheckpoint("savecapture"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"save capture numeric presentation restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"save capture full world restored "+n);await Capture("restored-s"+n,true);
            for(int k=n+1;k<SaveCapturePoints;k++){await SaveCaptureStep(k);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same future after actual save capture restart "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunSaveCaptureChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreSaveCaptureFlow();else await BeginSaveCaptureFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("SAVE_CAPTURE_PASSED "+FlowCase);
    }
}
