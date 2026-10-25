using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 bool DirectiveCancel=>FlowCase=="directive-cancel";
 int DirectiveSlot=>FlowCase=="directive-left"?1:FlowCase=="directive-right"?2:0;
 TroopDispatch LastDirective()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitDirective!.Dispatches.Last();
 TroopReceipt LastDirectiveReceipt()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitDirective!.Receipts.Last(r=>r.Id==LastDirective().Id);
 long PersonEnd()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Select(l=>l.EndAt).DefaultIfEmpty(game.Session.Seconds).Max();
 async Task DirectiveStep(int n){await Frame();var s=game!.Session;
  if(DirectiveCancel){
   if(n==0){game.SelectKnownMilitary(NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[0]);game.Press("known-target-go");game.Advance(PersonEnd()+1);}
   if(n==1){game.ChooseMilitary(NewMilitaryAction.RemoteRecall);game.Press("unit-directive-hold");var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.Withdrawal!.Dispatches.Last().DueAt==LastDirective().DueAt,"old army original and new main-post hold arrive in same physical boundary");}
   if(n==2){game.Advance(LastDirective().DueAt);var w=s.DiagnosticSnapshot();Check(LastDirectiveReceipt().Applied&&w.NewMilitary!.Orders.Last().AffectedUnits!.SequenceEqual(new[]{0}),"same-boundary new individual hold supersedes old recall only for main post");}
   if(n==3){game.ChooseMilitary(NewMilitaryAction.CancelRecallKnown);game.Advance(s.DiagnosticSnapshot().NewMilitary!.Cancellation!.Dispatches.Last().DueAt-1);}
   if(n==4){game.Advance(s.DiagnosticSnapshot().NewMilitary!.Cancellation!.Dispatches.Last().DueAt);var w=s.DiagnosticSnapshot();var e=w.NewMilitary!.Cancellation!.Effects.Last();Check(e.Outcome==UnitCancelDisposition.Stopped&&w.NewMilitary.Orders.Single(o=>o.Id==e.StopOrder).AffectedUnits!.SequenceEqual(new[]{1,2}),"cancel stops only still-effective old recall wings and preserves new main-post hold");}
   if(n==5){var before=s.DiagnosticSnapshot();var points=Enumerable.Range(0,3).Select(i=>NewGameMilitary.Position(before,i,s.Seconds)).ToArray();game.Advance(s.Seconds+20);var w=s.DiagnosticSnapshot();Check(Enumerable.Range(0,3).All(i=>NewGameMilitary.Position(w,i,s.Seconds)==points[i]),"all actual positions remain stopped without route rollback");}
   if(n==6)game.Press("rotate");if(n==7)game.Advance(s.Seconds+3);
  }else{
   if(n==0){game.SelectKnownMilitary(NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[DirectiveSlot]);Check(game.ButtonEnabled("unit-directive-move")&&game.ButtonEnabled("unit-directive-hold")&&game.ButtonEnabled("unit-directive-recall"),"selected known own unit exposes actual directive controls");}
   if(n==1){var w=s.DiagnosticSnapshot();var k=NewGameMilitary.SelectKnownTarget(w,NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[DirectiveSlot])!;int count=w.NewMilitary!.Orders.Length;game.ExerciseUnitDirectiveGoal(new(k.Position.X+8000,k.Position.Y));var input=s.DiagnosticSnapshot().NewMilitary!.Requests.Last().Input;Check(LastDirective().Recipient==k.Unit&&LastDirective().Destination==k.Position&&input.Directive!.Known==k&&RegionalGround.Distance(input.Directive.Goal!,new(k.Position.X+8000,k.Position.Y))<500&&s.DiagnosticSnapshot().NewMilitary!.Orders.Length==count,"actual terrain selection sends fixed own-recipient original without pre-receipt movement");}
   if(n==2){game.Advance(LastDirective().DueAt);var w=s.DiagnosticSnapshot();Check(LastDirectiveReceipt().Applied&&w.NewMilitary!.Orders.Last().AffectedUnits!.SequenceEqual(new[]{DirectiveSlot}),"physical directive receipt creates exactly one selected-unit motion scope");Check(!MessageFlow.Knows(w.Messaging,LastDirectiveReceipt().ReplyMessage,w.PlayerControl.Value)&&NewGameMilitary.UnitDirectiveStatus(w).Contains("返書未受領"),"ordinary UI never discloses execution before physical reply");}
   if(n==3){game.Advance(Math.Max(ArmyEnd(),LastDirectiveReceipt().ReplyDueAt)+1);Check(NewGameMilitary.UnitDirectiveStatus(s.DiagnosticSnapshot()).Contains("受令・指示を実行"),"received actual reply makes command result known");}
   if(n==4){game.Press("known-target-go");game.Advance(PersonEnd()+1);Check(NewGameMilitary.KnownMilitary(s.DiagnosticSnapshot()).Single(r=>r.Unit==NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[DirectiveSlot]).Live,"person reaches recorded position and directly observes moved target nearby");}
   if(n==5){game.Press("unit-directive-hold");game.Advance(LastDirective().DueAt);Check(LastDirectiveReceipt().Applied,"actual selected-unit hold is physically received");game.Advance(LastDirectiveReceipt().ReplyDueAt);}
   if(n==6){game.Press("unit-directive-recall");game.Advance(LastDirective().DueAt+1);var w=s.DiagnosticSnapshot();Check(LastDirectiveReceipt().Applied&&w.NewMilitary!.Orders.Last().AffectedUnits!.SequenceEqual(new[]{DirectiveSlot}),"actual selected-unit recall moves only selected unit");}
   if(n==7){game.Advance(Math.Max(ArmyEnd(),LastDirectiveReceipt().ReplyDueAt)+1);var w=s.DiagnosticSnapshot();Check(NewGameMilitary.Position(w,DirectiveSlot,s.Seconds)==NewGameMilitary.Home(w,DirectiveSlot),"selected unit physically returns to its own home slot");}
  }
  game.Refresh();Check(game.RecognitionModelsMatch(),"ordinary 3D follows recognized units at directive checkpoint"+n);game.Press("save");await WaitSave();
 }
 async Task BeginDirectiveContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<=4;n++)await RecognitionStep(n);for(int n=0;n<8;n++){await DirectiveStep(n);game!.SaveCheckpoint("directive"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreDirective(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,row=>row.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<8;n++){game!.LoadCheckpoint("directive"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"same unit scope/knowledge/3D after restore"+n);await Capture("restored-s"+n,true);for(int k=n+1;k<8;k++)await DirectiveStep(k);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same directive future after restore"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_DIRECTIVE_RESTORED "+n);}}
 async Task RunDirectiveChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreDirective();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-directive-film")){game!.LoadCheckpoint("directive6");game.Press("auto");string dir=Path.Combine(TestOutput,"film",TestKey??"normal");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"directive-"+n.ToString("D3")+".png"));}game.Press("pause");File.WriteAllText(Path.Combine(TestOutput,"film-manifest.json"),System.Text.Json.JsonSerializer.Serialize(new{Directory=Path.GetRelativePath(TestOutput,dir),Frames=180,Checkpoint=6,Action="selected-unit-recall"}));}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_DIRECTIVE_PASSED "+FlowCase);}
}
