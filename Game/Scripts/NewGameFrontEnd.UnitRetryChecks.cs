using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
    const int UnitRetryCheckpoints=12;
    async Task UnitRetryStep(int n){await Frame();var s=game!.Session;
        if(n==1){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);}
        if(n==2){game.ChooseMilitary(NewMilitaryAction.Recall);game.ChooseMilitary(NewMilitaryAction.Handoff);game.ChooseMilitary(NewMilitaryAction.Withdraw);game.Press("military-frame");}
        if(n==3){game.Advance(Math.Max(PersonalEnd(),ArmyEnd())+1);Check(NewGameMilitary.AtHome(s.DiagnosticSnapshot()),"known home is now physically occupied without revealing it to distant sender");}
        if(n==4)game.ChooseMilitary(NewMilitaryAction.RemoteRecall);
        if(n==5){var d=NewGameMilitary.UnitDispatches(s.DiagnosticSnapshot()).Last();game.Advance(d.DueAt);Check(!NewGameMilitary.UnitReceipts(s.DiagnosticSnapshot()).Last().Delivered&&!NewGameMilitary.UnitRetryAllowed(s.DiagnosticSnapshot(),NewMilitaryAction.RetryRecallHome),"failure does not become sender knowledge before physical report arrival");}
        if(n==6){var r=NewGameMilitary.UnitReceipts(s.DiagnosticSnapshot()).Last();game.Advance(r.ReplyDueAt);Check(NewGameMilitary.UnitRetryAllowed(s.DiagnosticSnapshot(),NewMilitaryAction.RetryRecallKnown),"failure report formally enables a retry");}
        if(n==7){game.ChooseMilitary(NewMilitaryAction.RetryRecallKnown);var w=s.DiagnosticSnapshot();var d=NewGameMilitary.UnitDispatches(w).Last();Check(d.Destination==w.NewMilitary!.Withdrawal!.Sightings.Last().Position&&d.Destination!=NewGameMilitary.Position(w,0,s.Seconds),"retry uses declared old observation, never the unit's true current coordinate");}
        if(n==8){var d=NewGameMilitary.UnitDispatches(s.DiagnosticSnapshot()).Last();game.Advance(d.DueAt);var r=NewGameMilitary.UnitReceipts(s.DiagnosticSnapshot()).Last();game.Advance(r.ReplyDueAt);Check(!r.Delivered,"second physical attempt misses the unchanged old address");}
        if(n==9){game.ChooseMilitary(NewMilitaryAction.RetryRecallHome);var w=s.DiagnosticSnapshot();var d=NewGameMilitary.UnitDispatches(w).Last();var first=w.NewMilitary!.Withdrawal!.Dispatches.Single();Check(d.Message==first.Message&&d.Attempt.EndsWith("/attempt/3")&&d.Destination==NewGameMilitary.Home(w,0),"third delivery keeps original identity and explicitly chooses known home");}
        if(n==10){var d=NewGameMilitary.UnitDispatches(s.DiagnosticSnapshot()).Last();game.Advance(d.DueAt);var r=NewGameMilitary.UnitReceipts(s.DiagnosticSnapshot()).Last();game.Advance(r.ReplyDueAt);var w=s.DiagnosticSnapshot();Check(r.Delivered&&r.Applied&&MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value),"receiver executes once and physical return reaches sender");Check(NewGameMilitary.UnitReceipts(w).Count(t=>t.Applied)==1&&!NewGameMilitary.UnitRetryAllowed(w,NewMilitaryAction.RetryRecallHome),"no second execution or fourth attempt");}
        if(n==11){game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"survivors, physical cargo return and received report complete the same command handover");}
        game.Press("save");game.Refresh();
    }
    async Task BeginUnitRetryContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<UnitRetryCheckpoints;n++){await UnitRetryStep(n);game!.SaveCheckpoint("unitretry"+n);await Capture("s"+n,true);}await Capture("final",true);}
    async Task RestoreUnitRetry(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<UnitRetryCheckpoints;n++){game!.LoadCheckpoint("unitretry"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual original/retry courier and unit pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<UnitRetryCheckpoints;next++)await UnitRetryStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same original, attempts, receipt and return future from"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_RETRY_RESTORED "+n);}}
    async Task RunUnitRetryChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitRetry();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-retry-film")){game!.LoadCheckpoint("unitretry7");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"retry-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_RETRY_PASSED "+FlowCase);}
}
