using Godot;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    int ClockPoints=>FlowCase=="clock-1000"?3:2;
    async Task WaitClock(){await UntilPrefetch(()=>!game!.IsClockControlling,"actual clock-control publication completes");Check(game!.LastClockError==null,"actual clock control completed without error");}
    async Task ClockPress(bool paused){game!.Press(paused?"pause":"auto");await WaitClock();Check(game.Session.Paused==paused,"actual requested pause state confirmed");}
    async Task ClockPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("clock"+n);await Capture("s"+n,true);}
    async Task BeginClockFlow()
    {
        await BeginPopulationEntry(FlowCase=="clock-notice"?0:1000);
        if(FlowCase=="clock-1000"){game!.SetDestination("fork");game.Press("depart");await ClockPress(true);}
        if(FlowCase=="clock-notice")game!.ChooseMilitary(NewMilitaryAction.Request);
        await ClockPoint(0);for(int n=1;n<ClockPoints;n++){await ClockStep(n);await ClockPoint(n);}await Capture("final",true);
    }
    async Task ClockStep(int point)
    {
        if(FlowCase=="clock-1000"){
            long target=point==1?3:5;var expected=new WorldSession(game!.Session.DiagnosticSnapshot());expected.SetPaused(false);
            var click=Stopwatch.StartNew();game.Press("auto");double startClick=click.Elapsed.TotalMilliseconds;await WaitClock();double startComplete=click.Elapsed.TotalMilliseconds;long startFrames=game.ClockRenderTicks;
            await UntilPrefetch(()=>game.Session.Seconds>=target,"automatic prepared1x advances after responsive start");Check(game.Session.Seconds==target,"exact requested whole-second target");
            click.Restart();game.Press("pause");double stopClick=click.Elapsed.TotalMilliseconds;await WaitClock();double stopComplete=click.Elapsed.TotalMilliseconds;long stopFrames=game.ClockRenderTicks;await DrainPrefetch();game.ReviewFraction(0);
            for(long t=expected.Seconds+1;t<=target;t++)expected.AdvanceTo(t);expected.SetPaused(true);
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"responsive start and stop equal existing synchronous Core effects");
            File.WriteAllText(Path.Combine(TestOutput,"clock-click-"+point+".json"),System.Text.Json.JsonSerializer.Serialize(new{StartClickMilliseconds=startClick,StopClickMilliseconds=stopClick,StartCompletionMilliseconds=startComplete,StopCompletionMilliseconds=stopComplete,StartRenderTicks=startFrames,StopRenderTicks=stopFrames,Scope="Actual1000 static synthetic people and selected-person movement; click duration is not completion latency or national AI acceptance."}));return;
        }
        if(FlowCase=="clock-cancel"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot());long published=game.ClockPublications;
            game.ClockTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("QA held clock control");};
            try{
                game.Press("auto");await UntilPrefetch(()=>entered.IsSet,"actual clock preparation reaches held gate");for(int n=0;n<160;n++)await Frame();
                Check(game.ClockRenderTicks>=160&&before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"rendering continues and pending control does not expose private world");
                await Capture("clock-pending",true);
                using(var oldPose=System.Text.Json.JsonDocument.Parse(game.Pose())){
                    string oldBasis=oldPose.RootElement.GetProperty("Render").GetProperty("Basis").GetRawText();game.Press("rotate");await Frame();
                    using var newPose=System.Text.Json.JsonDocument.Parse(game.Pose());Check(oldBasis!=newPose.RootElement.GetProperty("Render").GetProperty("Basis").GetRawText(),"actual rendered camera basis changes while control is held");
                }
                Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"camera remains available while clock control is held");
                game.Press("play-guide");await Frame();Check(game.ReviewGuideVisible,"actual guide remains available while control is held");game.ReviewCloseGuide();await Frame();
                Check(!game.ButtonEnabled("save")&&!game.ButtonEnabled("depart"),"world-changing controls locked during authoritative control publication");
                game.Press("pause");release.Set();await WaitClock();Check(game.Session.Paused&&!game.ReviewAutomatic&&game.ClockPublications==published+1,"stop supersedes pending start without starting world time");
                Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"canceled start has no time,revision or world effects");
            }finally{release.Set();game.ClockTestFault=null;}
            entered.Reset();release.Reset();using var leftGate=new ManualResetEventSlim();
            game.ClockTestFault=()=>{entered.Set();try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("QA menu cancellation");}finally{leftGate.Set();}};
            try{
                game.Press("auto");await UntilPrefetch(()=>entered.IsSet,"second private control held before leaving game");game.Press("menu");
                Check(!game.Visible&&canvas.Visible&&!game.IsClockControlling,"actual menu cancels pending control and suspends same runtime");release.Set();await UntilPrefetch(()=>leftGate.IsSet,"abandoned worker leaves held gate");
                for(int n=0;n<20;n++)await Frame();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.ClockPublications==published+1,"abandoned private control cannot publish into suspended world");
                Press("continue");await Frame();Check(game.Visible&&!canvas.Visible&&before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual continue resumes same retained life after canceled start");
                Check(game.SaveUiMessage.Contains("取り消しました")&&!game.IsClockControlling,"continuing retained life does not show stale preparing status");
            }finally{release.Set();game.ClockTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase=="clock-fault"){
            string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot());game.ClockTestFault=()=>throw new IOException("declared-clock-control-fault");game.Press("auto");
            await UntilPrefetch(()=>!game.IsClockControlling&&game.LastClockError!=null,"failed actual clock preparation is reported");Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&!game.ReviewAutomatic,"failed clock control keeps exact confirmed world");await Capture("clock-failure",true);game.ClockTestFault=null;
            await ClockPress(false);await ClockPress(true);Check(game.Session.Seconds==0,"retry can start and stop before first second");game.ReviewFraction(0);return;
        }
        if(FlowCase=="clock-notice"){
            var expected=new WorldSession(game!.Session.DiagnosticSnapshot());expected.SetPaused(false);expected.AdvanceTo(100);await ClockPress(false);
            await UntilPrefetch(()=>game.Session.Paused&&!game.ReviewAutomatic,"important physical military letter pauses current clock");Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"responsive clock preserves exact important-arrival effects");
            Check(!game.OffersMilitary(NewMilitaryAction.Accept),"arrival does not open letter");game.Press("open");Check(game.OffersMilitary(NewMilitaryAction.Accept),"opened offer enables actual response");await ClockPress(false);await ClockPress(true);Check(game.Session.Seconds==expected.Seconds,"one resume and immediate stop keep actual letter boundary");game.ReviewFraction(0);return;
        }
        throw new InvalidOperationException("Unknown clock QA case");
    }
    async Task RestoreClockFlow()
    {
        Press("load-list");await Frame();string instance=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));int index=Array.FindIndex(savedRows,r=>r.Profile.Instance==instance);Check(index>=0,"clock campaign appears after restart");savedList.Select(index);Press("load");await Frame();
        for(int n=0;n<ClockPoints;n++){
            game!.LoadCheckpoint("clock"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"clock saved numeric presentation restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"clock saved world restored "+n);await Capture("restored-s"+n,true);
            for(int step=n+1;step<ClockPoints;step++){await ClockStep(step);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"clock exact continuation after restart "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunClockChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreClockFlow();else await BeginClockFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("RESPONSIVE_CLOCK_PASSED "+FlowCase);
    }
}
