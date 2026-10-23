using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 const int UnitHeldCheckpoints=14;
 async Task HeldWalk(FieldPoint p){game!.ExercisePersonalMove(p);game.Advance(PersonalEnd()+1);await Frame();}
 async Task UnitHeldStep(int n){await Frame();var s=game!.Session;
  if(n<=6){await UnitReplyStep(n);return;}
  if(n==7){if(FlowCase=="held-after")game.ChooseMilitary(NewMilitaryAction.PersonalHome);var f=s.DiagnosticSnapshot().NewMilitary!.ReplyRecovery!.Flights.Single();game.Advance(f.DueAt);if(FlowCase=="held-after")game.Advance(s.DiagnosticSnapshot().NewMilitary!.ReplyRecovery!.Flights.Last().DueAt);if(ReplyMoved)game.Advance(Math.Max(s.Seconds,PersonalEnd()+1));else await HeldWalk(NewGameMilitary.HeldPapers(s.DiagnosticSnapshot(),s.Seconds).First().Position);Check(NewGameMilitary.HeldPapers(s.DiagnosticSnapshot(),s.Seconds).Length==(ReplyMoved?2:1),"only actual retained papers are selectable at the carrier");}
  if(n==8){game.ChooseMilitary(FlowCase=="held-report"?NewMilitaryAction.ReturnHeldReportHome:NewMilitaryAction.TakeHeldReply);var t=s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last();Check(t.Cargo.Length==(ReplyMoved?2:1),"one physical carrier moves every paper in its custody");}
  if(n==9){var t=s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last();game.Advance(Math.Max(s.Seconds,t.DueAt-1));Check(NewGameMilitary.CourierFrames(s.DiagnosticSnapshot(),s.Seconds).Select(c=>c.Carrier).Distinct().Count()==NewGameMilitary.CourierFrames(s.DiagnosticSnapshot(),s.Seconds).Length,"no physical carrier appears in two places mid-route");}
  if(n==10){var t=s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last();game.Advance(t.DueAt);var w=s.DiagnosticSnapshot();Check(w.NewMilitary!.HeldDelivery!.Arrivals.Last().Received,"actual intended recipient receives the held original");if(FlowCase=="held-report")Check(!MessageFlow.Knows(w.Messaging,CurrentInquiryReply().ReplyMessage,w.PlayerControl.Value),"report receipt at the unit does not reveal reply contents to distant player");else Check(w.Messaging.Receipts.Count(a=>a.MessageId==CurrentInquiryReply().ReplyMessage)==1,"late original and retry have one formal receipt");}
  if(n==11&&ReplyMoved){if(FlowCase=="held-report"){await HeldWalk(NewGameMilitary.Home(s.DiagnosticSnapshot(),0));game.ChooseMilitary(NewMilitaryAction.TakeHeldReply);}else game.ChooseMilitary(NewMilitaryAction.ReturnHeldReportHome);}
  if(n==12){var t=s.DiagnosticSnapshot().NewMilitary!.HeldDelivery!.Transfers.Last();game.Advance(Math.Max(s.Seconds,t.DueAt));var w=s.DiagnosticSnapshot();Check(w.Messaging.Receipts.Count(a=>a.MessageId==CurrentInquiryReply().ReplyMessage)==1,"both physical handover orders preserve one formal reply receipt");if(FlowCase=="held-open")Check(w.NewMilitary!.ReplyRecovery!.Flights.Any(f=>f.Kind==UnitReplyLeg.Retry)&&w.Messaging.Receipts.Count(r=>r.MessageId==CurrentInquiryReply().ReplyMessage)==1,"distant sender has no receipt evidence; bounded copy does not duplicate recipient receipt");}
  if(n==13){game.Advance(s.Seconds+3);game.Press("military-frame");}
  game.Press("save");await WaitSave();game.Refresh();
 }
 async Task BeginUnitHeldContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<UnitHeldCheckpoints;n++){await UnitHeldStep(n);game!.SaveCheckpoint("unitheld"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreUnitHeld(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<UnitHeldCheckpoints;n++){game!.LoadCheckpoint("unitheld"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"actual person/unit/all-paper carrier pose restored at"+n);await Capture("restored-s"+n,true);for(int next=n+1;next<UnitHeldCheckpoints;next++)await UnitHeldStep(next);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same late-original/report/retry future from"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_HELD_RESTORED "+n);}}
 async Task RunUnitHeldChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitHeld();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-held-film")){game!.LoadCheckpoint("unitheld8");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"held-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_HELD_PASSED "+FlowCase);}
}
