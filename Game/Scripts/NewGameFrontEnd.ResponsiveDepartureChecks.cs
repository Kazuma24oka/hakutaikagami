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
    async Task WaitDeparture(){await UntilPrefetch(()=>!game!.IsDeparting&&!game.DepartureWorkerPending,"actual departure request and private worker complete",4000);}
    async Task PressDeparture(){game!.Press("depart");await WaitDeparture();Check(game.LastDepartureError==null&&game.LastDepartureResult is ResultCode.Accepted or ResultCode.AlreadyAccepted,"actual departure is accepted before subsequent action");}
    async Task DeparturePoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("departure"+n);await Capture("s"+n,true);}
    async Task BeginDepartureFlow(){await BeginPopulationEntry(1000);await DeparturePoint(0);await DepartureStep();await DeparturePoint(1);await Capture("final",true);}
    async Task CompleteMeasuredDeparture()
    {
        game!.SetDestination("fork");var source=game.Session.DiagnosticSnapshot();var input=TravelWorld.Move(source,"request:travel/"+game.Session.Revision,"fork");var expected=new WorldSession(source);var result=expected.Submit(input,expected.Revision);Check(result.Code==ResultCode.Accepted,"baseline existing departure is a valid accepted request");expected.SetPaused(false);
        var watch=Stopwatch.StartNew();game.Press("depart");double click=watch.Elapsed.TotalMilliseconds;await WaitDeparture();
        Check(game.LastDepartureError==null&&game.LastDepartureResult==ResultCode.Accepted,"prepared actual departure accepted without worker failure");
        Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"departure publication exactly equals existing Submit then SetPaused(false)");
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-departure.json":"check-departure.json"),System.Text.Json.JsonSerializer.Serialize(new{ClickMilliseconds=click,CompletionMilliseconds=watch.Elapsed.TotalMilliseconds,RenderTicks=game.DepartureRenderTicks,MaximumPreparation=game.PreparationMaximum,Scope="Actual1000 stationary registered people and selected departure;click is not completion/fps/national AI guarantee."}));
    }
    async Task DepartureStep()
    {
        if(FlowCase=="depart-normal"){
            await CompleteMeasuredDeparture();var expected=new WorldSession(game!.Session.DiagnosticSnapshot());expected.SetPaused(false);expected.AdvanceTo(expected.Seconds+10);expected.SetPaused(true);game.Press("ten");await WaitManualTime();
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"accepted departure connects to actual road movement with same Core world");game.ReviewFraction(0);return;
        }
        if(FlowCase=="depart-auto"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();game!.AdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held old automatic future before departure");};
            long pubs=game.AdvancePublications,discarded=game.AdvanceDiscards;
            try{
                await ClockPress(false);await UntilPrefetch(()=>entered.IsSet,"old automatic preparation held before departure");game.AdvanceTestFault=null;game.SetDestination("fork");var source=game.Session.DiagnosticSnapshot();var expected=new WorldSession(source);expected.Submit(TravelWorld.Move(source,"request:travel/"+game.Session.Revision,"fork"),expected.Revision);expected.SetPaused(false);
                game.Press("depart");for(int n=0;n<100;n++)await Frame();Check(ContractJson.CanonicalHash(source)==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"departure waits for canceled old automatic worker without publishing either future");await Capture("departure-auto-pending",true);release.Set();await WaitDeparture();
                Check(game.LastDepartureError==null&&game.ReviewAutomatic&&game.AdvancePublications==pubs&&game.AdvanceDiscards>discarded,"actual departure cancels old future and restores prior automatic preference");
                game.Press("pause");await WaitClock();expected.SetPaused(true);Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"automatic to departure then stop preserves exact confirmed request world");
            }finally{release.Set();game.AdvanceTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase=="depart-fault"){
            string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot()),generation=game.ReviewPublishedGenerationId();game.SetDestination("fork");game.DepartureTestFault=()=>throw new IOException("declared-departure-fault");game.Press("depart");await WaitDeparture();
            Check(game.LastDepartureError=="IOException"&&before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&generation==game.ReviewPublishedGenerationId(),"failed fully computed private departure changes neither current world nor save");await Capture("departure-failure",true);game.DepartureTestFault=null;await CompleteMeasuredDeparture();game.ReviewFraction(0);return;
        }
        if(FlowCase is "depart-cancel" or "depart-menu" or "depart-load"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();game!.DepartureTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held fully computed private departure");};
            var source=game.Session.DiagnosticSnapshot();string before=ContractJson.CanonicalHash(source),generation=game.ReviewPublishedGenerationId();long pubs=game.DeparturePublications;
            // Public load validation can inspect the published save without exposing any private departure future.
            var savedHost=new WorldSessionHost(new WorldSession(source));var preparedSave=game.ReviewManualSave();Check(savedHost.CommitLoad(preparedSave,savedHost.Current,savedHost.Current.Revision)==ResultCode.Accepted,"published save can be independently prepared for expected load");string savedHash=ContractJson.CanonicalHash(savedHost.Current.DiagnosticSnapshot());
            try{
                game.SetDestination("fork");game.Press("depart");await UntilPrefetch(()=>entered.IsSet,"fully computed departure is held before publication");for(int n=0;n<160;n++)await Frame();
                Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.DeparturePublications==pubs&&generation==game.ReviewPublishedGenerationId(),"private accepted departure never changes visible world or published save");
                Check(!game.ButtonEnabled("depart")&&!game.ButtonEnabled("inspect")&&!game.ButtonEnabled("save"),"departure locks duplicate/world/save controls");game.Press("depart");game.Press("inspect");game.Press("save");
                Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"forced duplicate disabled signals cannot submit world changes");game.Press("rotate");game.Press("play-guide");await Frame();Check(game.ReviewGuideVisible&&game.ReviewGuideCloseEnabled,"camera and actual closable guide remain usable during departure wait");game.ReviewCloseGuide();await Frame();await Capture("departure-pending",true);
                game.DepartureTestFault=null;
                if(FlowCase=="depart-cancel"){
                    game.Press("pause");release.Set();await WaitDeparture();await WaitClock();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.DeparturePublications==pubs,"actual pause rejects private departure without any receipt or time effect");await CompleteMeasuredDeparture();
                }else if(FlowCase=="depart-menu"){
                    game.Press("menu");Press("continue");await Frame();game.SetDestination("fork");game.Press("depart");for(int n=0;n<160;n++)await Frame();
                    Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.DeparturePublications==pubs&&game.PreparationMaximum==1,"immediate return queues replacement departure while canceled old worker drains");release.Set();await WaitDeparture();
                    var expected=new WorldSession(source);expected.Submit(TravelWorld.Move(source,"request:travel/"+source.CommitSequence,"fork"),source.CommitSequence);expected.SetPaused(false);Check(game.LastDepartureError==null&&game.DeparturePublications==pubs+1&&ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"only replacement departure publishes exactly once after immediate menu return");
                }else{
                    var old=game;string id=old.Profile.Instance;game.Press("menu");Press("load-list");await Frame();savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();Check(switching.Visible&&!switching.GetOkButton().Disabled,"actual load confirmation is available");switching.GetOkButton().EmitSignal(Button.SignalName.Pressed);await Frame();await Frame();
                    Check(!ReferenceEquals(old,game)&&!IsInstanceValid(old)&&!old.PreparationDisposed,"actual load replaces runtime while old private departure still drains");Check(savedHash==ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot())&&generation==game.ReviewPublishedGenerationId(),"actual load displays exactly the selected published save,including protected newer replay saves");await Capture("departure-load-before-release",true);release.Set();await UntilPrefetch(()=>old.PreparationDisposed,"loaded-away runtime drains and disposes old departure worker");
                    Check(old.DeparturePublications==pubs&&savedHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"old departure cannot publish into the loaded replacement life");if(!TravelWorld.Active(game.Session.DiagnosticSnapshot()))await CompleteMeasuredDeparture();
                }
            }finally{release.Set();if(game!=null)game.DepartureTestFault=null;}
            game!.ReviewFraction(0);return;
        }
        throw new InvalidOperationException("unknown departure case");
    }
    async Task RestoreDepartureFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<2;n++){
            game!.LoadCheckpoint("departure"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"departure exact saved pose restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"departure exact saved world restored "+n);await Capture("restored-s"+n,true);
            if(n==0){await DepartureStep();game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"departure identical future after process restart "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunDepartureChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreDepartureFlow();else await BeginDepartureFlow();File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("RESPONSIVE_DEPARTURE_PASSED "+FlowCase);}
}
