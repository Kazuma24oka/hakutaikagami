using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 const int KnownTargetCheckpoints=16;
 bool TargetLetter=>FlowCase=="target-letter";
 int TargetSlot=>TargetLetter?0:2;
 long TargetJourneyEnd()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Journeys.Last().Route.Select(l=>l.EndAt).DefaultIfEmpty(game.Session.Seconds).Max();
 NewMilitaryRequest TargetFirstMove()=>game!.Session.DiagnosticSnapshot().NewMilitary!.Requests.First(q=>q.Input.KnownTarget!=null);
 async Task KnownTargetStep(int n){await Frame();var s=game!.Session;
  if(n==0){game.SelectKnownMilitary(NewGameMilitary.UnitIds[TargetSlot]);var row=NewGameMilitary.SelectKnownTarget(s.DiagnosticSnapshot(),NewGameMilitary.UnitIds[TargetSlot])!;Check(row.Evidence.StartsWith(TargetLetter?"letter/":"observation/"),"selection uses actual learned report or direct observation provenance");Check(game.ButtonEnabled("known-target-go"),"detached person can choose a recorded destination");}
  if(n==1)game.ChooseMilitary(NewMilitaryAction.RemoteRecall);
  if(n==2){game.Press("known-target-go");var q=TargetFirstMove();Check(q.Input.Action==NewMilitaryAction.PersonalMove&&q.Input.A==q.Input.KnownTarget!.Position,"real target button records immutable known destination and provenance");Check(!game.ButtonEnabled("known-target-go"),"walking person is not given a second immediate move");}
  if(n==3){long end=TargetJourneyEnd();game.Advance(s.Seconds+Math.Max(1,(end-s.Seconds)/2));}
  if(n==4){var d=s.DiagnosticSnapshot().NewMilitary!.Withdrawal!.Dispatches.Last();game.Advance(Math.Max(s.Seconds,d.DueAt));}
  if(n==5){var order=s.DiagnosticSnapshot().NewMilitary!.Orders.Last();long start=order.Routes[TargetSlot].Select(l=>l.StartAt).DefaultIfEmpty(s.Seconds).Min();game.Advance(Math.Max(s.Seconds,start+5));var w=s.DiagnosticSnapshot();var q=TargetFirstMove();Check(w.NewMilitary!.Withdrawal!.Journeys.First(j=>j.Id==q.Input.Id).Route[^1].To==q.Input.KnownTarget!.Position&&NewGameMilitary.Position(w,TargetSlot,s.Seconds)!=q.Input.A,"target troop moves but existing personal destination never follows its true position");}
  if(n==6){game.Advance(Math.Max(s.Seconds,TargetJourneyEnd()+1));var q=TargetFirstMove();Check(NewGameMilitary.PersonPosition(s.DiagnosticSnapshot(),s.Seconds)==q.Input.A&&NewGameMilitary.Detached(s.DiagnosticSnapshot()),"person arrives at recorded old point without auto-attaching to moved troops");}
  if(n==7)game.Advance(Math.Max(s.Seconds,ArmyEnd()+1));
  if(n==8)game.ChooseMilitary(NewMilitaryAction.InquireUnitHome);
  if(n==9)game.Advance(s.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Dispatches.Last().DueAt);
  if(n==10){game.Advance(s.DiagnosticSnapshot().NewMilitary!.UnitInquiry!.Receipts.Last().ReplyDueAt);game.SelectKnownMilitary(NewGameMilitary.UnitIds[0]);Check(NewGameMilitary.SelectKnownTarget(s.DiagnosticSnapshot(),NewGameMilitary.UnitIds[0])!.Position==NewGameMilitary.Home(s.DiagnosticSnapshot(),0),"only physically received main-unit report supplies return location");}
  if(n==11){game.Press("known-target-go");Check(s.DiagnosticSnapshot().NewMilitary!.Requests.Last().Input.KnownTarget!=null,"second explicit move uses updated known main-unit destination");}
  if(n==12){game.Advance(TargetJourneyEnd()+1);game.ChooseMilitary(NewMilitaryAction.Attach);Check(!NewGameMilitary.Detached(s.DiagnosticSnapshot()),"physical one-meter reunion remains a separate explicit action");}
  if(n==13){game.ChooseMilitary(NewMilitaryAction.Enter);game.ChooseMilitary(NewMilitaryAction.Unload);game.ChooseMilitary(NewMilitaryAction.Report);game.ChooseMilitary(NewMilitaryAction.Return);}
  if(n==14){game.Advance(s.Seconds+3);OpenAllServiceLetters();Check(NewGameMilitary.View(s.DiagnosticSnapshot()).CanRequest,"known target movement preserves command handover and closure");}
  if(n==15)game.Advance(s.Seconds+2);
  game.Refresh();Check(game.RecognitionModelsMatch(),"target selection/movement uses projected models at"+n);game.Press("save");await WaitSave();
 }
 async Task BeginKnownTargetContinuation(){for(int n=1;n<=7;n++)await MilitaryStep(n);for(int n=0;n<=(TargetLetter?7:4);n++)await RecognitionStep(n);for(int n=0;n<KnownTargetCheckpoints;n++){await KnownTargetStep(n);game!.SaveCheckpoint("knowntarget"+n);await Capture("s"+n,true);}await Capture("final",true);}
 async Task RestoreKnownTarget(){Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();for(int n=0;n<KnownTargetCheckpoints;n++){game!.LoadCheckpoint("knowntarget"+n);await Frame();await Frame();if(game.Pose()!=File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")))File.WriteAllText(Path.Combine(TestOutput,"mismatch-s"+n+"-pose.json"),game.Pose());Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s"+n+"-pose.json")),"same selected ID/source/fixed destination/world/3D at"+n);await Capture("restored-s"+n,true);for(int k=n+1;k<KnownTargetCheckpoints;k++)await KnownTargetStep(k);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same no-GPS future from"+n);await Capture("continued-s"+n,true);GD.Print("KNOWN_TARGET_RESTORED "+n);}}
 async Task RunKnownTargetChecks(){if(OS.GetCmdlineUserArgs().Contains("--new-game-restore"))await RestoreKnownTarget();else await BeginNewMilitaryFlow();if(OS.GetCmdlineUserArgs().Contains("--known-target-film")){game!.LoadCheckpoint("knowntarget2");game.Press("auto");string dir=Path.Combine(TestOutput,"film");Directory.CreateDirectory(dir);for(int n=0;n<180;n++){await Frame();ViewportCapture.Save(GetViewport(),Path.Combine(dir,"known-"+n.ToString("D3")+".png"));}game.Press("pause");}File.WriteAllText(Path.Combine(TestOutput,OS.GetCmdlineUserArgs().Contains("--new-game-restore")?"restore-checks.json":"check-checks.json"),System.Text.Json.JsonSerializer.Serialize(assertions));GD.Print("KNOWN_TARGET_PASSED "+FlowCase);}
}
