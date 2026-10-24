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
    async Task WaitManualTime(){await UntilPrefetch(()=>!game!.IsManualAdvancing&&!game.ManualAdvanceWorkerPending,"actual explicit-time operation and private workers complete",4000);}
    async Task ManualTimePoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("manualtime"+n);await Capture("s"+n,true);}
    async Task BeginManualTimeFlow()
    {
        await BeginPopulationEntry(FlowCase=="manual-notice"?0:1000);
        if(FlowCase=="manual-notice")game!.ChooseMilitary(NewMilitaryAction.Request);else{game!.SetDestination("fork");game.Press("depart");}
        if(FlowCase=="manual-arrive"){game!.Press("ten");await WaitManualTime();Check(game.ButtonEnabled("arrive"),"actual initial departure has entered the road and exposes arrival time");}
        await ManualTimePoint(0);await ManualTimeStep();await ManualTimePoint(1);await Capture("final",true);
    }
    async Task ManualTimeStep()
    {
        if(FlowCase=="manual-auto"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var expected=new WorldSession(game!.Session.DiagnosticSnapshot());long target=expected.Seconds+10;expected.SetPaused(false);expected.AdvanceTo(target);expected.SetPaused(true);
            long oldPublications=game.AdvancePublications,oldDiscards=game.AdvanceDiscards;
            game.AdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held previous automatic future");};
            try{
                await ClockPress(false);await UntilPrefetch(()=>entered.IsSet,"old automatic future reaches private gate before manual request");await Capture("manual-auto-pending",true);
                game.Press("ten");release.Set();await WaitManualTime();
                Check(game.LastManualAdvanceError==null&&game.Session.Paused&&game.AdvancePublications==oldPublications&&game.AdvanceDiscards>oldDiscards,"manual request cancels old automatic future before starting its own worker");
                Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"switch from held auto to manual preserves exact requested existing Core world");
            }finally{release.Set();game.AdvanceTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase is "manual-ten" or "manual-arrive" or "manual-notice"){
            string button=FlowCase=="manual-arrive"?"arrive":"ten";long from=game!.Session.Seconds;
            long target=button=="arrive"?game.Session.GetBranchView(game.Session.PlayerControl).ArrivalAt!.Value+2:from+10;
            var expected=new WorldSession(game.Session.DiagnosticSnapshot());expected.SetPaused(false);
            while(expected.Seconds<target&&!expected.Paused){long prior=expected.Seconds;expected.AdvanceTo(target);if(expected.Seconds==prior)break;}expected.SetPaused(true);
            var watch=Stopwatch.StartNew();game.Press(button);double click=watch.Elapsed.TotalMilliseconds;
            await WaitManualTime();Check(game.LastManualAdvanceError==null&&game.Session.Paused&&!game.ReviewAutomatic,"explicit-time action confirms stopped current world");
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"same explicit request equals existing synchronous Core transaction effects");
            File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-action.json":"check-action.json"),System.Text.Json.JsonSerializer.Serialize(new{Button=button,ClickMilliseconds=click,CompletionMilliseconds=watch.Elapsed.TotalMilliseconds,RenderTicks=game.ManualAdvanceRenderTicks,From=from,To=game.Session.Seconds,Scope="Actual1000 static people plus selected travel,or3-person letter case;click is not completion time or fps guarantee."}));
            if(FlowCase=="manual-notice"){
                Check(game.Session.Seconds<target&&!game.OffersMilitary(NewMilitaryAction.Accept),"important physical letter stops explicit-time advance before target without opening");
                game.Press("open");Check(game.OffersMilitary(NewMilitaryAction.Accept),"actual open enables response after manual stop");await ClockPress(false);await ClockPress(true);
            }
            game.ReviewFraction(0);return;
        }
        if(FlowCase is "manual-cancel" or "manual-menu"){
            game!.Press("play-guide");await Frame();Check(game.ReviewGuideVisible,"existing guide opens before manual operation");game.ReviewCloseGuide();await Frame();
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var left=new ManualResetEventSlim();
            game!.ManualAdvanceTestFault=()=>{entered.Set();try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held manual advance");}finally{left.Set();}};
            try{
                long pubs=game.ManualAdvancePublications;game.Press("ten");await UntilPrefetch(()=>entered.IsSet,"actual private manual advance reaches held gate");
                string source=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());var expected=new WorldSession(game.Session.DiagnosticSnapshot());expected.SetPaused(true);
                for(int n=0;n<160;n++)await Frame();Check(source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.ManualAdvanceRenderTicks>=160,"held manual future never replaces confirmed world while rendering continues");await Capture("manual-pending",true);
                Check(!game.ButtonEnabled("save")&&!game.ButtonEnabled("depart")&&!game.ButtonEnabled("ten"),"world controls and duplicate explicit request locked while pending");
                game.Press("rotate");await Frame();game.Press("play-guide");await Frame();Check(game.ReviewGuideVisible,"guide remains usable during explicit-time wait");Check(game.ReviewGuideCloseEnabled,"previously created guide close stays enabled during manual wait");await Capture("manual-guide-reused",true);game.ReviewCloseGuide();await Frame();
                if(FlowCase=="manual-menu"){
                    game.Press("menu");Check(!game.Visible&&canvas.Visible&&!game.IsManualAdvancing,"menu cancels explicit-time operation and suspends actual runtime");release.Set();await UntilPrefetch(()=>left.IsSet,"abandoned manual worker leaves gate");for(int n=0;n<20;n++)await Frame();
                    Check(game.ManualAdvancePublications==pubs,"suspended private manual future cannot publish");Press("continue");await Frame();await WaitManualTime();
                }else{game.Press("pause");release.Set();await WaitManualTime();await WaitClock();}
                Check(game.LastManualAdvanceError==null&&game.Session.Paused&&game.ManualAdvancePublications==pubs,"actual stop or menu cancellation rejects private time publication");
                Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"cancellation retains exact confirmed world and legitimate pause revision");
            }finally{release.Set();game.ManualAdvanceTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase=="manual-fault"){
            var source=game!.Session.DiagnosticSnapshot();var expected=new WorldSession(source);expected.SetPaused(false);expected.SetPaused(true);
            game.ManualAdvanceTestFault=()=>throw new IOException("declared-manual-time-fault");game.Press("ten");await WaitManualTime();
            Check(game.LastManualAdvanceError=="IOException"&&game.Session.Paused&&!game.ReviewAutomatic,"failed private manual advance stops exact confirmed generation");
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"failed private manual advance has no time or future effects");await Capture("manual-failure",true);game.ManualAdvanceTestFault=null;
            long target=game.Session.Seconds+10;expected=new WorldSession(game.Session.DiagnosticSnapshot());expected.SetPaused(false);expected.AdvanceTo(target);expected.SetPaused(true);
            game.Press("ten");await WaitManualTime();Check(game.LastManualAdvanceError==null&&ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"retry after declared manual failure equals normal existing Core");game.ReviewFraction(0);return;
        }
        throw new InvalidOperationException("Unknown manual-time case");
    }
    async Task RestoreManualTimeFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<2;n++){
            game!.LoadCheckpoint("manualtime"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"manual-time saved numeric pose restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"manual-time full saved world restored "+n);await Capture("restored-s"+n,true);
            if(n==0){await ManualTimeStep();game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"manual-time identical continuation after restart "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunManualTimeChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreManualTimeFlow();else await BeginManualTimeFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("MANUAL_TIME_PASSED "+FlowCase);
    }
}
