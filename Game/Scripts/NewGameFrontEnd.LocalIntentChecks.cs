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
    async Task PressLocalIntent(string id){game!.Press(id);if(id=="work")game.Press("work-confirm");await WaitDeparture();Check(game.LastLocalIntentError==null&&game.LastLocalIntentResult is ResultCode.Accepted or ResultCode.AlreadyAccepted,"actual local intent is accepted before subsequent action");}
    Intent LocalInput(string button){var w=game!.Session.DiagnosticSnapshot();return button=="inspect"?NewGameWorld.Inspect(w,"request:inspection/"+game.Session.Revision):NewGameWorld.LocalWork(w,"request:local-work/"+game.Session.Revision);}
    async Task CompleteLocal(string button)
    {
        var source=game!.Session.DiagnosticSnapshot();var expected=new WorldSession(source);Check(expected.Submit(LocalInput(button),expected.Revision).Code==ResultCode.Accepted,"existing local Submit accepts baseline");
        long pubs=game.LocalIntentPublications;var timer=Stopwatch.StartNew();game.Press(button);if(button=="work")game.Press("work-confirm");double click=timer.Elapsed.TotalMilliseconds;await WaitDeparture();
        Check(game.LastLocalIntentError==null&&game.LastLocalIntentResult==ResultCode.Accepted&&game.LocalIntentPublications==pubs+1,"actual local intent publishes exactly once");
        Check(ContractJson.CanonicalHash(expected.DiagnosticSnapshot())==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.Session.Paused,"local publication equals existing Submit without implicit unpause");
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-local.json":"check-local.json"),System.Text.Json.JsonSerializer.Serialize(new{Button=button,ClickMilliseconds=click,CompletionMilliseconds=timer.Elapsed.TotalMilliseconds,RenderFrames=game.LocalIntentRenderTicks,Scope="1000 stationary synthetic people plus selected local operation;not national autonomous AI/fps guarantee"}));
    }
    async Task LocalStep()
    {
        if(FlowCase=="local-work"){
            await CompleteLocal("work");game!.Press("ten");await WaitManualTime();
            Check(game.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null),"physical work arrival remains sealed before opening");Check(game.Session.Paused,"important work delivery still pauses world");return;
        }
        if(FlowCase=="local-fault"){
            string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot()),saved=game.ReviewPublishedGenerationId();game.LocalIntentTestFault=()=>throw new IOException("declared-local-failure");game.Press("inspect");await WaitDeparture();Check(game.LastLocalIntentError=="IOException"&&before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&saved==game.ReviewPublishedGenerationId(),"private local failure preserves current world and published save");await Capture("local-failure",true);game.LocalIntentTestFault=null;
        }
        if(FlowCase is "local-cancel" or "local-menu"){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();game!.LocalIntentTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("held private local operation");};var expectedLocal=new WorldSession(game.Session.DiagnosticSnapshot());expectedLocal.Submit(LocalInput("inspect"),expectedLocal.Revision);string expectedHash=ContractJson.CanonicalHash(expectedLocal.DiagnosticSnapshot());string before=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()),saved=game.ReviewPublishedGenerationId();long pubs=game.LocalIntentPublications;
            try{
                game.Press("inspect");await UntilPrefetch(()=>entered.IsSet,"private local result held before publish");for(int n=0;n<120;n++)await Frame();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&saved==game.ReviewPublishedGenerationId()&&game.LocalIntentPublications==pubs,"private local result remains invisible and unsaved");
                Check(!game.ButtonEnabled("inspect")&&!game.ButtonEnabled("work")&&!game.ButtonEnabled("save"),"local operation locks duplicate,other command and save");game.Press("inspect");game.Press("work");game.Press("save");game.Press("rotate");game.Press("play-guide");await Frame();Check(game.ReviewGuideVisible&&game.ReviewGuideCloseEnabled,"actual guide stays closable during local wait");game.ReviewCloseGuide();await Capture("local-pending",true);game.LocalIntentTestFault=null;
                if(FlowCase=="local-menu"){game.Press("menu");Press("continue");await Frame();game.Press("inspect");for(int n=0;n<100;n++)await Frame();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.PreparationMaximum==1,"replacement local waits behind canceled old private job");release.Set();await WaitDeparture();Check(expectedHash==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.LocalIntentPublications==pubs+1&&game.LastLocalIntentError==null,"only replacement local publishes");return;}
                game.Press("pause");release.Set();await WaitDeparture();await WaitClock();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&game.LocalIntentPublications==pubs,"stop discards held local without command receipt");
            }finally{release.Set();game.LocalIntentTestFault=null;}
        }
        await CompleteLocal("inspect");
    }
    async Task LocalPoint(int n){game!.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();game.SaveCheckpoint("local"+n);await Capture("s"+n,true);}
    async Task RunLocalIntentChecks()
    {
        if(OS.GetCmdlineUserArgs().Contains("--new-game-restore")){
            Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();
            for(int n=0;n<2;n++){game!.LoadCheckpoint("local"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"local exact saved pose restored"+n);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-world.sha256")),"local exact saved world restored"+n);await Capture("restored-s"+n,true);if(n==0)await LocalStep();game.ReviewFraction(0);game.Press("save");await WaitSave();game.Refresh();Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"local identical future after process restart"+n);await Capture("continued-s"+n,true);}
        }else{await BeginPopulationEntry(1000);await LocalPoint(0);await LocalStep();await LocalPoint(1);await Capture("final",true);}
        File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("RESPONSIVE_LOCAL_INTENT_PASSED "+FlowCase);
    }
}
