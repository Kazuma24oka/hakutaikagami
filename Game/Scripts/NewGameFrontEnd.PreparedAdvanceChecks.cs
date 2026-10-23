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
    int PrefetchPoints=>FlowCase=="prefetch-1000"?3:2;
    async Task UntilPrefetch(Func<bool> ready,string reason,int frames=1800){for(int n=0;n<frames&&!ready();n++)await Frame();Check(ready(),reason);}
    async Task DrainPrefetch(){await UntilPrefetch(()=>!game!.AdvanceWorkerPending,"canceled/private worker is reaped without publication");}
    async Task PrefetchRunTo(long target)
    {
        if(!game!.ReviewAutomatic)game.Press("auto");
        await UntilPrefetch(()=>game.Session.Seconds>=target||!game.ReviewAutomatic,"actual1x simulation reaches target or important pause");
        Check(game.Session.Seconds==target,"actual1x publication stops at requested whole-second checkpoint");game.Press("pause");await DrainPrefetch();game.ReviewFraction(0);
    }
    async Task PrefetchPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("prefetch"+n);await Capture("s"+n,true);}
    async Task BeginPrefetchFlow()
    {
        await BeginPopulationEntry(FlowCase=="prefetch-notice"?0:1000);
        if(FlowCase=="prefetch-1000"){game!.SetDestination("fork");game.Press("depart");}
        if(FlowCase=="prefetch-notice")game!.ChooseMilitary(NewMilitaryAction.Request);
        await PrefetchPoint(0);for(int n=1;n<PrefetchPoints;n++){await PrefetchStep(n);await PrefetchPoint(n);}await Capture("final",true);
    }
    async Task PrefetchStep(int point)
    {
        if(FlowCase=="prefetch-1000"){
            long target=point==1?3:5;var before=game!.Session.DiagnosticSnapshot();long published=game.AdvancePublications;
            var frames=new System.Collections.Generic.List<double>();game.Press("auto");var expected=new WorldSession(game.Session.DiagnosticSnapshot());var clock=Stopwatch.StartNew();double prior=0;
            for(int n=0;n<1800&&game.Session.Seconds<target;n++){await Frame();double now=clock.Elapsed.TotalMilliseconds;frames.Add(now-prior);prior=now;}
            Check(game.Session.Seconds==target,"actual1x reaches deterministic travel time with grouped1000people");game.Press("pause");await DrainPrefetch();game.ReviewFraction(0);
            for(long t=expected.Seconds+1;t<=target;t++)expected.AdvanceTo(t);expected.SetPaused(true);
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual1x prepared publications equal same synchronous Core transactions");Check(game.AdvancePublications-published==target-before.ElapsedSeconds&&game.LastAdvanceError==null,"one publication per elapsed second and no worker failure");
            File.WriteAllText(Path.Combine(TestOutput,"automatic-"+point+".json"),System.Text.Json.JsonSerializer.Serialize(new{Frames=frames,Target=target,Published=game.AdvancePublications-published,Scope="Actual1x on this PC; synthetic stationary1000 plus selected-person travel, not autonomous national NPC simulation. Input clicks/faster modes remain synchronous."}));return;
        }
        if(FlowCase is "prefetch-stale" or "prefetch-save"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();long published=game!.AdvancePublications;long rendered=game.AdvanceRenderTicks;
            game.AdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("QA held future not released");};
            try{
                game.Press("auto");await UntilPrefetch(()=>entered.IsSet,"actual worker reached private future commit gate");string source=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());for(int n=0;n<160;n++)await Frame();
                Check(game.Session.Seconds==0&&source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.AdvancePublications==published,"held private future is not exposed or committed at/after next tick");Check(game.AdvanceRenderTicks-rendered>=160,"actual UI/render loop continues while private computation is held");
                game.Press("rotate");Check(source==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual camera control stays available without mutating the source world");
                if(FlowCase=="prefetch-save"){game.Press("save");await WaitSave();Check(game.ReviewManualSave().Seconds==0,"actual save contains confirmed world, not held future");}else game.Press("pause");
                string retained=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());release.Set();await DrainPrefetch();Check(retained==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.AdvancePublications==published,"discarded held computation cannot overwrite pause or actual save");
                if(FlowCase=="prefetch-save"){game.Press("load");Check(retained==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"normal load restores confirmed generation while rejecting old speculation");}
            }finally{release.Set();game.AdvanceTestFault=null;}
            game.ReviewFraction(0);return;
        }
        if(FlowCase=="prefetch-fault"){
            var before=game!.Session.DiagnosticSnapshot();long failures=game.AdvanceFailures;game.AdvanceTestFault=()=>throw new IOException("declared-qa-private-future-fault");game.Press("auto");
            await UntilPrefetch(()=>game.LastAdvanceError!=null,"actual UI catches failed private preparation");await DrainPrefetch();var failed=game.Session.DiagnosticSnapshot();
            Check(failed.ElapsedSeconds==before.ElapsedSeconds&&failed.CommitSequence==before.CommitSequence+2&&failed.Paused&&!game.ReviewAutomatic&&game.AdvanceFailures==failures+1,"failed future changes only explicit auto-start/pause, no partial time or effects");before.CommitSequence=failed.CommitSequence;
            Check(ContractJson.CanonicalHash(before)==ContractJson.CanonicalHash(failed),"all source facts survive private worker failure");await Capture("failure",true);
            game.AdvanceTestFault=null;var expected=new WorldSession(failed);expected.SetPaused(false);expected.AdvanceTo(1);expected.AdvanceTo(2);expected.SetPaused(true);await PrefetchRunTo(2);
            Check(game.LastAdvanceError==null&&ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"actual auto retry recovers same exact world without duplicate effects");return;
        }
        if(FlowCase=="prefetch-notice"){
            game!.Press("auto");var expected=new WorldSession(game.Session.DiagnosticSnapshot());expected.AdvanceTo(100);Check(expected.Paused&&expected.Seconds<100,"existing actual military letter has a physical important-arrival boundary");
            await UntilPrefetch(()=>game.Session.Paused&&!game.ReviewAutomatic,"actual prepared1x stops at important letter");
            Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),"prepared1x preserves exact synchronous important-letter boundary and knowledge");Check(!game.OffersMilitary(NewMilitaryAction.Accept),"arrival is not opening or assent");await Capture("notice-arrived",true);game.Press("open");Check(game.OffersMilitary(NewMilitaryAction.Accept),"actual opened offer enables answer only now");
            game.Press("auto");Check(game.ReviewAutomatic&&!game.Session.Paused,"one actual auto-button press resumes after important pause");await PrefetchRunTo(expected.Seconds+1);return;
        }
        throw new InvalidOperationException("Unknown prefetch QA case");
    }
    async Task RestorePrefetchFlow()
    {
        Press("load-list");await Frame();string instance=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));int index=Array.FindIndex(savedRows,r=>r.Profile.Instance==instance);Check(index>=0,"prefetch campaign appears after process restart");savedList.Select(index);Press("load");await Frame();
        for(int n=0;n<PrefetchPoints;n++){
            game!.LoadCheckpoint("prefetch"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"prefetch saved numeric/person/panel pose restored "+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"prefetch confirmed Core generation restored "+n);await Capture("restored-s"+n,true);
            for(int step=n+1;step<PrefetchPoints;step++){await PrefetchStep(step);game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();}
            game.Press("save");await WaitSave();game.Refresh();
            Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"prefetch same confirmed continuation after restore "+n);await Capture("continued-s"+n,true);
        }
    }
    async Task RunPrefetchChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestorePrefetchFlow();else await BeginPrefetchFlow();
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("PREPARED_ADVANCE_PASSED "+FlowCase);
    }
}
