using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    int BatchSpeed=>FlowCase switch{"batch-10"=>10,"batch-60"=>60,_=>360};
    async Task BatchPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("batch"+n);await Capture("s"+n,true);}
    async Task BeginBatchFlow()
    {
        await BeginPopulationEntry(FlowCase=="batch-notice"?0:1000);game!.ReviewSelectSpeed(BatchSpeed);
        if(FlowCase=="batch-notice")game.ChooseMilitary(NewMilitaryAction.Request);else{game.SetDestination("fork");game.Press("depart");}
        await BatchPoint(0);await BatchStep();await BatchPoint(1);await Capture("final",true);
    }
    async Task BatchStep()
    {
        if(FlowCase is "batch-10" or "batch-60" or "batch-360"){
            bool replay=OS.GetCmdlineUserArgs().Contains("--new-game-restore");
            var terrainTimes=new List<double>();game!.TerrainBuildTestHook=value=>terrainTimes.Add(value);
            var saved=new List<(long From,long Target)>();
            if(replay){using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestOutput,"check-batch-observation.json")));foreach(var item in doc.RootElement.GetProperty("Targets").EnumerateArray())saved.Add((item.GetProperty("From").GetInt64(),item.GetProperty("Target").GetInt64()));Check(saved.Count>0,"forward public target inputs exist for exact restart replay");}
            var records=new List<(long From,long Target,long Reached,int Processed,string Reason)>();game!.AdvancePublishedTestHook=(a,b,c,d,e)=>records.Add((a,b,c,d,e));
            if(replay)game.AdvanceTargetTestHook=from=>{if(records.Count>=saved.Count)return checked(from+1);var next=saved[records.Count];if(next.From!=from)throw new InvalidOperationException("Saved advance target source mismatch");return next.Target;};
            try{
                await ClockPress(false);var expected=new WorldSession(game.Session.DiagnosticSnapshot());var frames=new List<double>();var timer=Stopwatch.StartNew();double previous=0;
                for(int n=0;n<1800&&(replay?records.Count<saved.Count:timer.Elapsed.TotalSeconds<3)&&game.ReviewAutomatic;n++){await Frame();double now=timer.Elapsed.TotalMilliseconds;frames.Add(now-previous);previous=now;}
                double observed=timer.Elapsed.TotalSeconds;long reached=game.Session.Seconds;await ClockPress(true);await DrainPrefetch();
                Check(records.Count>0&&frames.Count>=3&&game.LastAdvanceError==null,"actual highspeed preparation publishes and rendering continues without injected delay");
                foreach(var record in records){Check(expected.Seconds==record.From,"recorded publication begins at same actual source time");var result=expected.AdvanceTo(record.Target);Check(result.ReachedAt==record.Reached&&result.Processed==record.Processed&&result.StopReason==record.Reason,"same target/budget preserves exact event boundary and stop reason");}
                expected.SetPaused(true);Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual bounded batches equal the same synchronous Core transaction sequence");
                File.WriteAllText(Path.Combine(TestOutput,(replay?"restore":"check")+"-batch-observation.json"),System.Text.Json.JsonSerializer.Serialize(new{SelectedSpeed=game.ReviewSpeed,ObservedSeconds=observed,ReachedSeconds=reached,Frames=frames,TerrainUpdatesMilliseconds=terrainTimes,Targets=records.Select(r=>new{r.From,r.Target,r.Reached,r.Processed,r.Reason}),Scope="Actual1000 static synthetic people and selected travel. Rate depends on CPU/event work; no fixed-fps/national NPC guarantee. Expected replay runs after measurement."}));
            }finally{game.AdvancePublishedTestHook=null;game.AdvanceTargetTestHook=null;game.TerrainBuildTestHook=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase is "batch-switch" or "batch-save"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            game!.AdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("QA held fast future");};
            try{
                await ClockPress(false);await UntilPrefetch(()=>entered.IsSet,"actual fast preparation reaches private gate");var before=game.Session.DiagnosticSnapshot();long published=game.AdvancePublications,discarded=game.AdvanceDiscards;
                for(int n=0;n<160;n++)await Frame();Check(before.ElapsedSeconds==game.Session.Seconds&&ContractJson.CanonicalHash(before)==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"held highspeed private future is never displayed as current world");await Capture("batch-pending",true);
                var expected=new WorldSession(before);expected.SetPaused(true);
                if(FlowCase=="batch-switch"){
                    game.ReviewSelectSpeed(1);Check(game.ReviewSpeed==1&&game.AdvanceDiscards==discarded+1,"actual dropdown speed change itself cancels old360 preparation");game.Press("pause");release.Set();await WaitClock();
                }else{game.Press("save");Check(game.IsSaving,"actual save owns pending highspeed cancellation");release.Set();await WaitSave();Check(game.LastSavePublished==true,"only confirmed world is published by real save");game.Press("load");}
                await DrainPrefetch();Check(game.AdvancePublications==published&&game.Session.Seconds==before.ElapsedSeconds,"old360 future cannot publish after switch or save/load");Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"switch/save keeps exact confirmed pause effects without fast jump");
            }finally{release.Set();game.AdvanceTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase=="batch-notice"){
            await ClockPress(false);var expected=new WorldSession(game!.Session.DiagnosticSnapshot());expected.AdvanceTo(360);
            Check(expected.Paused&&expected.Seconds<360,"existing actual important military letter has a physical pause boundary");await UntilPrefetch(()=>game.Session.Paused&&!game.ReviewAutomatic,"actual360 stops at important letter,not requested target");
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"highspeed preserves same actual important-letter world");Check(!game.OffersMilitary(NewMilitaryAction.Accept),"highspeed receipt is not opening");game.Press("open");Check(game.OffersMilitary(NewMilitaryAction.Accept),"opened actual offer enables answer");
            var resumed=new WorldSession(game.Session.DiagnosticSnapshot());resumed.SetPaused(false);resumed.SetPaused(true);await ClockPress(false);await ClockPress(true);Check(ContractJson.CanonicalHash(resumed.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"one-click resume then stop preserves exact known source");game.ReviewFraction(0);return;
        }
        throw new InvalidOperationException("Unknown batch QA case");
    }
    async Task RestoreBatchFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<2;n++){
            game!.LoadCheckpoint("batch"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"saved highspeed and numeric pose restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"saved full world restored "+n);await Capture("restored-s"+n,true);
            if(n==0){await BatchStep();game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();
            // Replay the recorded public target input sequence rather than a
            // variable number of transactions within the same wallclock duration.
            Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same deterministic cancellation/letter continuation after restart "+n);
            await Capture("continued-s"+n,true);
        }
    }
    async Task RunBatchChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreBatchFlow();else await BeginBatchFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("BATCHED_ADVANCE_PASSED "+FlowCase);
    }
}
