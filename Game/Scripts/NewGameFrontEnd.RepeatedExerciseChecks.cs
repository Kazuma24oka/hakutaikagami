using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 const int RepeatedExerciseCheckpoints=25;
 bool RepeatedLong=>FlowCase=="repeat-renewal";
 void RepeatForm(){var a=NewGameMilitary.Anchor(game!.Session.DiagnosticSnapshot());game.ExerciseMilitaryLine(new(a.X+20000,a.Y-12000),new(a.X+20000,a.Y+12000));game.Advance(ArmyEnd()+1);}
 void RepeatHome(){for(int n=0;n<8&&!NewGameMilitary.AtHome(game!.Session.DiagnosticSnapshot());n++)game.Advance(Math.Max(game.Session.Seconds+1,ArmyEnd()+1));Check(NewGameMilitary.AtHome(game!.Session.DiagnosticSnapshot()),"actual routed survivors reach home along recorded paths");}
 void RepeatHandover(){game!.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
 async Task RepeatedExerciseStep(int n){await Frame();var s=game!.Session;
  if(n==1)game.ChooseMilitary(NewMilitaryAction.CombatPermit);
  if(n==2){game.Advance(s.Seconds+2);Check(!game.OffersMilitary(NewMilitaryAction.CombatStart),"unopened repeat permit is not actor authorization");if(RepeatedLong)OpenAllServiceLetters();else{var original=s.DiagnosticSnapshot().NewMilitary!.Events.Single(e=>e.Effect==MilitaryEffect.CombatPermission).Message;game.ChooseMilitary(NewMilitaryAction.Inquire);var request=s.DiagnosticSnapshot().NewMilitary!.Requests.Last();game.Advance(s.Seconds+2);var reply=s.DiagnosticSnapshot().NewMilitary!.Events.Last(e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==request.Message).Message;game.SelectUnreadMail(new(reply));game.Press("open");Check(!MessageFlow.Knows(s.DiagnosticSnapshot().Messaging,original,s.PlayerControl.Value),"repeat quotation is opened through real selector without original permission knowledge");}Check(game.OffersMilitary(NewMilitaryAction.CombatStart),"received repeat permission or its quotation enables first episode");}
  if(n==3&&RepeatedLong)RepeatForm();
  if(n==4){game.ChooseMilitary(NewMilitaryAction.CombatStart);if(RepeatedLong)game.Press("military-frame");}
  if(n==5){game.Advance(s.Seconds+(RepeatedLong?10:1));if(RepeatedLong)Check(s.DiagnosticSnapshot().NewMilitary!.Rout!.Retreats.Length>0,"first episode has actual morale retreat without resetting strength");else game.ChooseMilitary(NewMilitaryAction.CombatEnd);}
  if(n==6&&RepeatedLong)RepeatHome();
  if(n==7&&RepeatedLong){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Rout!.Arrivals.Max(a=>a.At)+NewGameMilitary.RegroupSeconds+1);Check(!NewGameMilitary.RoutActive(s.DiagnosticSnapshot()),"first actual arrival and60seconds complete regroup");}
  if(n==8){if(RepeatedLong)RepeatHandover();else game.ChooseMilitary(NewMilitaryAction.CombatStart);}
  if(n==9){if(RepeatedLong){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(game.OffersMilitary(NewMilitaryAction.Request),"actual first military handover enables distinct next duty");}else{game.Advance(s.Seconds+1);game.ChooseMilitary(NewMilitaryAction.CombatEnd);Check(NewGameMilitary.ExerciseNumber(s.DiagnosticSnapshot())==2,"two short contacts share one valid repeat permission");}}
  if(n==10&&RepeatedLong)game.ChooseMilitary(NewMilitaryAction.Request);
  if(n==11&&RepeatedLong){game.Advance(s.Seconds+2);OpenAllServiceLetters();}
  if(n==12&&RepeatedLong){game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(s.Seconds+2);OpenAllServiceLetters();game.ChooseMilitary(NewMilitaryAction.Pickup);game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);Check(!game.OffersMilitary(NewMilitaryAction.CombatStart),"old case permission cannot start second duty contact");}
  if(n==13&&RepeatedLong)game.ChooseMilitary(NewMilitaryAction.CombatPermit);
  if(n==14&&RepeatedLong){game.Advance(s.Seconds+2);OpenAllServiceLetters();}
  if(n==15&&RepeatedLong)RepeatForm();
  if(n==16&&RepeatedLong){game.ChooseMilitary(NewMilitaryAction.CombatStart);game.Press("military-frame");var w=s.DiagnosticSnapshot();Check(NewGameMilitary.ExerciseNumber(w)==2&&NewGameMilitary.TotalStrength(w,s.Seconds)<100,"second field episode inherits first real casualties");}
  if(n==17&&RepeatedLong){game.Advance(s.Seconds+5);var w=s.DiagnosticSnapshot();long second=w.NewMilitary!.Requests.Last(q=>q.Input.Action==NewMilitaryAction.CombatStart).Revision;Check(w.NewMilitary.Rout!.Retreats.Any(r=>r.Revision>=second)&&NewGameMilitary.RoutActive(w),"second episode records its own new retreat while preserving first");}
  if(n==18&&RepeatedLong)RepeatHome();
  if(n==19&&RepeatedLong){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Rout!.Arrivals.Max(a=>a.At)+NewGameMilitary.RegroupSeconds+1);var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.Rout!.Regroups.Length==6&&!NewGameMilitary.RoutActive(w),"two sets of arrival and physical regroup remain in lifetime journal");}
  if(n==21)RepeatHandover();
  if(n==22){game.Advance(s.Seconds+3);OpenAllServiceLetters();var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.Events.Count(e=>e.Effect==MilitaryEffect.Returned)==(RepeatedLong?2:1),"same person actually returns command after repeated contacts");Check(w.ResourceLots.Sum(l=>l.Quantity)==NewGameMilitary.InitialStock-w.NewMilitary.Uses.Sum(u=>u.Consumed),"food consumption includes all episodes and duties");}
  if(n==23){game.Press("military-frame");Check(NewGameMilitary.ExerciseNumber(s.DiagnosticSnapshot())==2&&NewGameMilitary.TotalStrength(s.DiagnosticSnapshot(),s.Seconds)<100,"two episodes keep real surviving roster without reset");}
  if(n==24)game.Advance(s.Seconds+2);
  game.Press("save");game.Refresh();
 }
 async Task BeginRepeatedExerciseContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<RepeatedExerciseCheckpoints;n++){await RepeatedExerciseStep(n);game!.SaveCheckpoint("repeatedexercise"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreRepeatedExercise(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<RepeatedExerciseCheckpoints;n++){game!.LoadCheckpoint("repeatedexercise"+n);await Frame();await Frame();if(game.Pose()!=File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")))File.WriteAllText(Path.Combine(TestOutput,"mismatch-s"+n+"-pose.json"),game.Pose());Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual episode/strength/morale/courier/3D pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<RepeatedExerciseCheckpoints;next++)await RepeatedExerciseStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same two-contact/two-rout lifetime future from"+n);await Capture("continued-s"+n,true);GD.Print("REPEATED_EXERCISE_RESTORED "+n);}}
 async Task RunRepeatedExerciseChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreRepeatedExercise();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--repeated-exercise-film")){foreach(var row in new[]{(5,"first"),(17,"second")}){game!.LoadCheckpoint("repeatedexercise"+row.Item1);game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,row.Item2+"-"+n.ToString("D3")+".png"));}game.Press("pause");}}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("REPEATED_EXERCISE_PASSED "+FlowCase);}
}
