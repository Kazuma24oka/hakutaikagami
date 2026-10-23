using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
    const int RoutCheckpoints=12;
    async Task RoutStep(int n){await Frame();var s=game!.Session;
        if(n==1){game.ChooseMilitary(NewMilitaryAction.CombatPermit);game.Advance(s.Seconds+2);OpenAllServiceLetters();}
        if(n==2){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);Check(NewGameMilitary.CanStartCombat(s.DiagnosticSnapshot()),"opened physical permit survives the actual field march");}
        if(n==3){game.ChooseMilitary(NewMilitaryAction.CombatStart);Check(s.DiagnosticSnapshot().NewMilitary!.Rout?.Contract==NewGameMilitary.RoutContract,"normal contact input records the new motion contract explicitly");}
        if(n==4){game.Advance(s.Seconds+1);game.ChooseMilitary(NewMilitaryAction.Handoff);game.ChooseMilitary(NewMilitaryAction.Withdraw);game.Press("military-frame");}
        if(n==5){long start=s.DiagnosticSnapshot().NewMilitary!.Requests.Single(q=>q.Input.Action==NewMilitaryAction.CombatStart).At;game.Advance(start+10);var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.Rout!.Retreats.Length>0&&NewGameMilitary.Moving(w),"morale collapse generates actual terrain retreat routes");Check(NewGameMilitary.Detached(w)&&NewGameMilitary.PersonPosition(w,s.Seconds)!=NewGameMilitary.Position(w,0,s.Seconds),"automatic army rout does not teleport the independent officer");}
        if(n==6){game.Advance(PersonalEnd()+1);Check(NewGameMilitary.PersonPosition(s.DiagnosticSnapshot(),s.Seconds)==NewGameMilitary.Home(s.DiagnosticSnapshot(),0),"same officer arrives before unit handover");}
        if(n==7)game.ChooseMilitary(NewMilitaryAction.RemoteRecall);
        if(n==8){var d=s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Last();game.Advance(d.DueAt);var r=s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Last();game.Advance(Math.Max(r.ReplyDueAt,ArmyEnd()+1));var w=s.DiagnosticSnapshot();Check(!r.Applied&&MessageFlow.FailureKnown(w.Messaging,d.Attempt,w.PlayerControl.Value),"old known post is not corrected to the routed unit's true location; failure physically returns");Check(NewGameMilitary.AtHome(w)&&NewGameMilitary.RoutActive(w),"physical unit arrival is separate from completed regrouping");}
        if(n==9){long until=s.DiagnosticSnapshot().NewMilitary!.Rout!.Arrivals.Max(a=>a.At)+NewGameMilitary.RegroupSeconds;game.Advance(until);Check(!NewGameMilitary.RoutActive(s.DiagnosticSnapshot())&&NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds)<100,"regrouping recovers morale without resurrecting soldiers");game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);}
        if(n==10){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
        if(n==11){game.Advance(s.Seconds+3);OpenAllServiceLetters();var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned)&&w.NewMilitary.Reports.Last().Soldiers==NewGameMilitary.TotalStrength(w,s.Seconds),"received survivor report and empty cargo complete command handover");Check(w.Characters.All(c=>c.Life==LifeState.Adult),"rout never becomes officer death or political defection");}
        game.Press("save");await WaitSave();game.Refresh();
    }
    async Task BeginRoutContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<RoutCheckpoints;n++){await RoutStep(n);game!.SaveCheckpoint("rout"+n);await Capture("s"+n,true);}await Capture("final",true);}
    async Task RestoreRout(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<RoutCheckpoints;n++){game!.LoadCheckpoint("rout"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"restored actual person/unit/courier/terrain pose at rout checkpoint"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<RoutCheckpoints;next++)await RoutStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same complete rout and regroup future from"+n);await Capture("continued-s"+n,true);GD.Print("ROUT_RESTORED "+n);}}
    async Task RunRoutChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreRout();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--rout-film")){game!.LoadCheckpoint("rout5");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"rout-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("ROUT_REGROUP_PASSED "+FlowCase);}
}
