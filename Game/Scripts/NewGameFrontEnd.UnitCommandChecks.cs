using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 const int UnitCommandCheckpoints=15;
 bool CommandMissed=>FlowCase=="command-missed";
 TroopDispatch OriginalCommand()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Single();
 TroopReceipt OriginalCommandFailure()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Receipts.Single();
 UnitHeldOrderOutcome OriginalCommandLate()=>game!.Session.DiagnosticSnapshot().NewMilitary!.CommandHolding!.Outcomes.Single();
 async Task UnitCommandStep(int n){await Frame();var s=game!.Session;
  if(n==1){var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);}
  if(n==2){game.ChooseMilitary(NewMilitaryAction.Recall);game.ChooseMilitary(NewMilitaryAction.Handoff);game.ChooseMilitary(NewMilitaryAction.Withdraw);}
  if(n==3)game.Advance(PersonalEnd()+1);
  if(n==4){if(FlowCase is "command-kofu" or "evidence-copy" or "evidence-quote"){var home=NewGameMilitary.Home(s.DiagnosticSnapshot(),0);game.ExercisePersonalMove(new(home.X+5000,home.Y));game.Advance(PersonalEnd()+1);}game.ChooseMilitary(NewMilitaryAction.RemoteRecall);Check(s.DiagnosticSnapshot().NewMilitary!.CommandHolding!=null,"normal new command explicitly records original holding policy");}
  if(n==5){game.Advance(OriginalCommand().DueAt);Check(!OriginalCommandFailure().Delivered,"actual unit absence fails original without remote order effect");if(CommandMissed){var home=NewGameMilitary.Home(s.DiagnosticSnapshot(),0);game.ExercisePersonalMove(new(home.X+5000,home.Y));}}
  if(n==6){game.Advance(OriginalCommandFailure().ReplyDueAt);var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.CommandHolding!.Holdings.Length==(CommandMissed?2:1),"actual courier returns holding original and only undelivered report");Check(!w.Messaging.Receipts.Any(r=>r.MessageId==OriginalCommand().Message),"holding original never fabricates unit receipt");}
  if(n==7&&CommandMissed){game.Advance(Math.Max(s.Seconds,PersonalEnd()+1));game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(PersonalEnd()+1);}
  if(n==8){game.ChooseMilitary(CommandMissed?NewMilitaryAction.TakeHeldReply:FlowCase=="command-retry"?NewMilitaryAction.RetryRecallHome:NewMilitaryAction.DeliverHeldRecallHome);}
  if(n==9){if(FlowCase=="command-retry"){game.Advance(s.DiagnosticSnapshot().NewMilitary!.UnitRetry!.Dispatches.Single().DueAt);game.ChooseMilitary(NewMilitaryAction.DeliverHeldRecallHome);}else{game.Advance(s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last().DueAt);if(CommandMissed){game.ChooseMilitary(NewMilitaryAction.DeliverHeldRecallHome);var home=NewGameMilitary.Home(s.DiagnosticSnapshot(),0);game.ExercisePersonalMove(new(home.X+5000,home.Y));}}}
  if(n==10){var t=s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last();game.Advance(Math.Max(s.Seconds,t.DueAt));var f=OriginalCommandLate();var w=s.DiagnosticSnapshot();Check(f.Received&&w.Messaging.Receipts.Count(r=>r.MessageId==OriginalCommand().Message)==1,"late original and retry share one actual formal receipt");Check(!w.NewMilitary!.Withdrawal!.Receipts.Single().Delivered,"first failure journal remains unchanged");Check(w.NewMilitary.Orders.Count(o=>o.Id.Value.StartsWith("order:unit-post/"))==1,"physical original and retry execute once");}
  if(n==11){game.Advance(Math.Max(s.Seconds,OriginalCommandLate().DueAt));var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.CommandHolding!.Acks.Single().Received!=CommandMissed,"late receipt acknowledgement also requires the actual person at destination");}
  if(n==12&&CommandMissed){game.Advance(Math.Max(s.Seconds,PersonalEnd()+1));game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(PersonalEnd()+1);}
  if(n==13&&CommandMissed)game.ChooseMilitary(NewMilitaryAction.TakeHeldReply);
  if(n==14){if(CommandMissed)game.Advance(s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last().DueAt);var w=s.DiagnosticSnapshot();Check(MessageFlow.Knows(w.Messaging,OriginalCommandLate().ReplyMessage!,w.PlayerControl.Value),"physical acknowledgement eventually reaches the same player without repeating command");game.Press("military-frame");}
  game.Press("save");game.Refresh();
 }
 async Task BeginUnitCommandContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<UnitCommandCheckpoints;n++){await UnitCommandStep(n);game!.SaveCheckpoint("unitcommand"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreUnitCommand(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<UnitCommandCheckpoints;n++){game!.LoadCheckpoint("unitcommand"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual original/report/ack holder and unit pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<UnitCommandCheckpoints;next++)await UnitCommandStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same original/late effect/ack future from"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_COMMAND_RESTORED "+n);}}
 async Task RunUnitCommandChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitCommand();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-command-film")){game!.LoadCheckpoint("unitcommand5");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"command-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_COMMAND_PASSED "+FlowCase);}
}
