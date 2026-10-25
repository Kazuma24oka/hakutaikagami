using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    async Task LeasePoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("lease"+n);await Capture("s"+n,true);}
    async Task BeginLeaseFlow(){await BeginPopulationEntry(1000);await LeasePoint(0);await LeaseStep();await LeasePoint(1);await Capture("final",true);}
    async Task LeaseStep()
    {
        var originalWorld=game!.Session.DiagnosticSnapshot();string source=ContractJson.CanonicalHash(originalWorld);string generation=game.ReviewPublishedGenerationId();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var left=new ManualResetEventSlim();
        Action hold=()=>{entered.Set();try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held abandoned preparation");}finally{left.Set();}};
        if(FlowCase=="lease-exit"){
            var probe=new NewGameRuntime{Visible=false};probe.Configure(campaigns,game.Profile,true);AddChild(probe);await Frame();probe.ClockTestFault=hold;probe.Press("auto");
            try{
                await UntilPrefetch(()=>entered.IsSet,"second actual runtime reaches private preparation before exit");Check(probe.PreparationActive==1&&probe.PreparationMaximum==1,"actual exiting runtime owns one active lease");probe.QueueFree();await Frame();await Frame();
                Check(!IsInstanceValid(probe)&&!probe.PreparationDisposed,"actual runtime exits before held private worker finishes,lease not prematurely disposed");release.Set();await UntilPrefetch(()=>probe.PreparationDisposed,"exited runtime disposes tracked queue only after held worker leaves");
                Check(probe.PreparationActive==0&&probe.PreparationMaximum==1&&left.IsSet,"abandoned exited worker is observed and releases final lease");
                Check(source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&generation==game.ReviewPublishedGenerationId(),"exiting private runtime changes neither retained life nor published save");
            }finally{release.Set();if(IsInstanceValid(probe))probe.QueueFree();}
            File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-queue.json":"check-queue.json"),System.Text.Json.JsonSerializer.Serialize(new{Active=probe.PreparationActive,Maximum=probe.PreparationMaximum,Disposed=probe.PreparationDisposed,ActualRuntimeFreed=!IsInstanceValid(probe)}));
            await Capture("lease-exit-complete",true);game.ReviewFraction(0);return;
        }
        game.ClockTestFault=hold;long publications=game.ClockPublications;
        try{
            game.Press("auto");await UntilPrefetch(()=>entered.IsSet,"old actual clock future holds preparation lease");game.Press("menu");game.ClockTestFault=null;Press("continue");await Frame();
            Check(game.Visible&&!canvas.Visible&&!game.IsClockControlling&&source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"immediate menu return retains source while abandoned private work still runs");
            if(FlowCase=="lease-clock")game.Press("auto");else if(FlowCase=="lease-manual")game.Press("ten");else if(FlowCase=="lease-save")game.Press("save");else throw new InvalidOperationException("unknown lease case");
            await UntilPrefetch(()=>game.PreparationWaiting>0,"new actual preparation waits for abandoned worker lease");
            for(int n=0;n<160;n++)await Frame();
            Check(game.PreparationActive==1&&game.PreparationMaximum==1&&game.PreparationWaiting>0,"only one Core preparation executes while replacement remains queued");
            Check(source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.ClockPublications==publications,"neither abandoned nor queued private future publishes during gate");
            Check(generation==game.ReviewPublishedGenerationId(),"queued preparation cannot replace published save");await Capture("lease-pending",true);
            if(FlowCase=="lease-clock")game.Press("pause");release.Set();
            if(FlowCase=="lease-clock")await WaitClock();else if(FlowCase=="lease-manual")await WaitManualTime();else await WaitSave();
            await UntilPrefetch(()=>left.IsSet&&game.PreparationActive==0&&game.PreparationWaiting==0,"old and replacement work complete without overlapping leases");
            if(FlowCase=="lease-manual"){
                var expected=new WorldSession(originalWorld);expected.SetPaused(false);expected.AdvanceTo(expected.Seconds+10);expected.SetPaused(true);
                Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"queued manual publication immediately equals existing synchronous Core effects without reloading or repairing state");
            }else Check(source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"queued stop or save retains exact confirmed world");
            Check(game.PreparationMaximum==1&&game.LastClockError==null&&game.LastManualAdvanceError==null&&game.LastSaveError==null,"no hidden preparation failure and maximum active lease stays one");
        }finally{release.Set();game.ClockTestFault=null;}
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-queue.json":"check-queue.json"),System.Text.Json.JsonSerializer.Serialize(new{Active=game.PreparationActive,Maximum=game.PreparationMaximum,Waiting=game.PreparationWaiting,Started=game.PreparationStarted}));
        game.ReviewFraction(0);
    }
    async Task RestoreLeaseFlow()
    {
        Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
        for(int n=0;n<2;n++){
            game!.LoadCheckpoint("lease"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"lease exact saved pose restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"lease exact saved world restored "+n);await Capture("restored-s"+n,true);
            if(n==0){await LeaseStep();game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"lease same continuation after restart "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunLeaseChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreLeaseFlow();else await BeginLeaseFlow();File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("PREPARATION_QUEUE_PASSED "+FlowCase);}
}
