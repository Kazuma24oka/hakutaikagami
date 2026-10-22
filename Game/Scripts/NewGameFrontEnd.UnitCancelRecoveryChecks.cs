using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 const int UnitCancelRecoveryCheckpoints=16;
 UnitCancelNotice CancelResult()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Cancellation!.Notices.Single();
 async Task UnitCancelRecoveryStep(int n){await Frame();var s=game!.Session;
  if(n<=6){await UnitCancelStep(n);return;}
  if(n==7){game.Advance(OriginalCommand().DueAt+3);Check(s.DiagnosticSnapshot().NewMilitary!.Cancellation!.Effects.Single().Outcome==UnitCancelDisposition.Stopped,"requested physical cancellation stops remaining route before notice");}
  if(n==8){var home=NewGameMilitary.Home(s.DiagnosticSnapshot(),0);game.ExercisePersonalMove(new(home.X+5000,home.Y));}
  if(n==9){game.Advance(CancelResult().DueAt);var w=s.DiagnosticSnapshot();Check(!w.NewMilitary!.Cancellation!.NoticeArrivals.Single().Received&&!MessageFlow.Knows(w.Messaging,CancelResult().Message,w.PlayerControl.Value),"actual player absence rejects effect notice without result knowledge");Check(NewGameMilitary.HeldPapers(w,s.Seconds).Any(h=>h.Message==CancelResult().Message),"real failed courier retains effect notice");Check(!NewGameMilitary.CancellationStatus(w).Contains("残りの移動を停止"),"unknown remote result is not displayed after failed notice");}
  if(n==10){game.Advance(Math.Max(s.Seconds,PersonalEnd()+1));game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(PersonalEnd()+1);}
  if(n==11){Check(game.OffersMilitary(NewMilitaryAction.TakeHeldReply),"actual normal nearby courier pickup option");game.ChooseMilitary(NewMilitaryAction.TakeHeldReply);Check(s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last().Message==CancelResult().Message,"selected newest physical held result paper");}
  if(n==12){game.Advance(s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last().DueAt);var w=s.DiagnosticSnapshot();Check(MessageFlow.Knows(w.Messaging,CancelResult().Message,w.PlayerControl.Value)&&NewGameMilitary.CancellationStatus(w).Contains("残りの移動を停止"),"actual recovered notice alone updates player result knowledge");Check(w.NewMilitary!.Cancellation!.Effects.Length==1&&w.NewMilitary.Orders.Count(o=>o.Id.Value.StartsWith("order:unit-cancel/"))==1,"physical result recovery does not repeat stop or order");}
  if(n==13){var p=s.DiagnosticSnapshot().NewMilitary!.Cancellation!.Effects.Single().Positions[0];game.ExercisePersonalMove(p);game.Advance(PersonalEnd()+1);game.ChooseMilitary(NewMilitaryAction.Attach);game.ChooseMilitary(NewMilitaryAction.Reclaim);game.ChooseMilitary(NewMilitaryAction.Recall);}
  if(n==14){game.Advance(ArmyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
  if(n==15){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(s.DiagnosticSnapshot().NewMilitary!.Events.Any(e=>e.Effect==MilitaryEffect.Returned),"same officer and surviving units finish physical handover after notice recovery");game.Press("military-frame");}
  game.Press("save");game.Refresh();
 }
 async Task BeginUnitCancelRecoveryContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<UnitCancelRecoveryCheckpoints;n++){await UnitCancelRecoveryStep(n);game!.SaveCheckpoint("unitcancelrecovery"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreUnitCancelRecovery(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<UnitCancelRecoveryCheckpoints;n++){game!.LoadCheckpoint("unitcancelrecovery"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual failed result holder and recovery pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<UnitCancelRecoveryCheckpoints;next++)await UnitCancelRecoveryStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same failed-result recovery and handover future from"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_CANCEL_RECOVERY_RESTORED "+n);}}
 async Task RunUnitCancelRecoveryChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitCancelRecovery();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-cancel-recovery-film")){game!.LoadCheckpoint("unitcancelrecovery8");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"notice-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_CANCEL_RECOVERY_PASSED "+FlowCase);}
}
