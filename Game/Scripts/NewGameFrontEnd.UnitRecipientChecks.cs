using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class NewGameFrontEnd
{
 int RecipientSlot=>FlowCase=="unitrec-right"?2:1;
 bool RecipientLost=>FlowCase=="unitrec-lost";
 TroopDispatch RecipientQuery()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Dispatches.First(d=>d.Recipient==NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot]);
 TroopReceipt RecipientReceipt()=>game!.Session.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Receipts.Single(r=>r.Id==RecipientQuery().Id);
 UnitReplyFlight RecipientFlight(UnitReplyLeg kind)=>game!.Session.DiagnosticSnapshot().NewMilitary!.ReplyRecovery!.Flights.Last(f=>f.Dispatch==RecipientQuery().Id&&f.Kind==kind);
 async Task UnitRecipientStep(int n){await Frame();var s=game!.Session;
  if(n==0){game.SelectKnownMilitary(NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot]);Check(game.ButtonEnabled("known-target-query"),"known own wing can be queried through actual selected-unit button");}
  if(n==1){game.Press("known-target-query");var d=RecipientQuery();var input=s.DiagnosticSnapshot().NewMilitary!.Requests.Last().Input;Check(d.Recipient==NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot]&&d.Destination==input.InquiryTarget!.Known.Position,"actual UI sends to selected wing and immutable known position");}
  if(n==2){game.Advance(RecipientQuery().DueAt);var r=RecipientReceipt();var w=s.DiagnosticSnapshot();Check(r.Delivered&&!r.Applied&&!MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value),"physical wing receipt is separate from player learning its reply");Check(!NewGameMilitary.KnownMilitary(w).Any(row=>row.Evidence=="letter/"+r.ReplyMessage),"unreceived wing reply never updates ordinary knowledge");}
  if(n==3){if(RecipientLost)game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(RecipientReceipt().ReplyDueAt);Check(MessageFlow.Knows(s.DiagnosticSnapshot().Messaging,RecipientReceipt().ReplyMessage,s.PlayerControl.Value)!=RecipientLost,"reply meets stationary person or misses person who moved");}
  if(n==4){if(RecipientLost){var f=RecipientFlight(UnitReplyLeg.FailureReport);game.Advance(f.DueAt);var w=s.DiagnosticSnapshot();Check(MessageFlow.Knows(w.Messaging,f.Message,NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot].Value)&&!MessageFlow.Knows(w.Messaging,f.Message,NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[0].Value),"failure report physically returns to issuing wing, never main post");Check(RecipientFlight(UnitReplyLeg.Retry).Origin==RecipientQuery().Destination,"new reply copy starts at the actual issuing wing post");}else game.Advance(s.Seconds+1);}
  if(n==5){if(RecipientLost){var point=RecipientReceipt().ReplyDestination;game.ExerciseMilitaryLine(point,point,true);game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max()+1);}else game.Advance(s.Seconds+1);}
  if(n==6){if(RecipientLost)game.Advance(Math.Max(s.Seconds,RecipientFlight(UnitReplyLeg.Retry).DueAt));else game.Advance(s.Seconds+1);var r=RecipientReceipt();var rows=NewGameMilitary.KnownMilitary(s.DiagnosticSnapshot());Check(MessageFlow.Knows(s.DiagnosticSnapshot().Messaging,r.ReplyMessage,s.PlayerControl.Value)&&rows.Single(row=>row.Unit==NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot]).Evidence=="letter/"+r.ReplyMessage,"physically received wing reply updates exactly the selected wing");Check(rows.Where(row=>row.Unit!=NewGameMilitary.UnitIdsFor(game!.Session.DiagnosticSnapshot())[RecipientSlot]).All(row=>!row.Evidence.StartsWith("letter/")),"wing reply never invents main or other-wing reports");}
  if(n==7)game.ChooseMilitary(NewMilitaryAction.RemoteRecall);
  if(n==8)game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Last().DueAt+1);
  if(n==9){game.Advance(ArmyEnd()+1);Check(NewGameMilitary.AllOwnCurrentlySeen(s.DiagnosticSnapshot()),"arriving troops replace past wing letter with direct local observation");}
  if(n==10){game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Advance(s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Select(l=>l.EndAt).DefaultIfEmpty(s.Seconds).Max()+1);}
  if(n==11)game.ChooseMilitary(NewMilitaryAction.Attach);
  if(n==12){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
  if(n==13){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(NewGameMilitary.View(s.DiagnosticSnapshot()).CanRequest,"recipient-scoped mail preserves final command handover and closure");}
  if(n==14)game.Advance(s.Seconds+2);
  if(n==15)game.Press("rotate");
  game.Refresh();Check(game.RecognitionModelsMatch(),"recipient-scoped knowledge matches actual projected 3D at"+n);game.Press("save");await WaitSave();
 }
 async Task BeginUnitRecipientContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<=4;n++)await RecognitionStep(n);for(int n=0;n<16;n++){await UnitRecipientStep(n);game!.SaveCheckpoint("recipient"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreUnitRecipient(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,row=>row.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<16;n++){game!.LoadCheckpoint("recipient"+n);await Frame();await Frame();Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"same selected recipient/knowledge/person/3D after restore at"+n);await Capture("restored-s"+n,true);for(int k=n+1;k<16;k++)await UnitRecipientStep(k);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same recipient-owned message future after checkpoint"+n);await Capture("continued-s"+n,true);GD.Print("UNIT_RECIPIENT_RESTORED "+n);}}
 async Task RunUnitRecipientChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreUnitRecipient();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--unit-recipient-film")){game!.LoadCheckpoint("recipient2");game.ChooseMilitary(NewMilitaryAction.PersonalHome);game.Press("auto");string dir=Path.Combine(TestOutput,"film",TestKey??"normal");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"recipient-"+n.ToString("D3")+".png"));}game.Press("pause");File.WriteAllText(Path.Combine(TestOutput,"film-manifest.json"),System.Text.Json.JsonSerializer.Serialize(new{Directory=Path.GetRelativePath(TestOutput,dir),Frames=180,Checkpoint=2,Action="PersonalHome"}));}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("UNIT_RECIPIENT_PASSED "+FlowCase);}
}
