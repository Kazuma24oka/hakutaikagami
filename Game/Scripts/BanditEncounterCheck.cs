using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class BanditEncounterCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{
 string output=ProjectSettings.GlobalizePath("res://../Docs/Design/BanditEncounterV1/runtime-local/ui");Directory.CreateDirectory(output);
 var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateBanditReview(Path.Combine(Path.GetTempPath(),"bandit-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
 var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 game.Press("bandit-encounter");await Frame();game.ReviewBanditPress("encounter");await Frame();game.ReviewBanditPress("fight");await Frame();game.ReviewSocialClose();game.Advance(5);game.ReviewFraction(0);
 Check(BanditEncounter.Current(game.Session.DiagnosticSnapshot())?.Phase=="交戦中","接触戦が開始している");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"contact.png"))==Error.Ok,"接触戦の描画");}
 game.Press("save");await Until(()=>!game.IsSaving,"交戦途中の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"賊モデルを含む3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());
 game.Advance(40);Check(BanditEncounter.Current(game.Session.DiagnosticSnapshot())==null,"接触戦の離脱");Check(BanditEncounter.Injury(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl)>0,"負傷を保持");
 game.Press("bandit-encounter");await Frame();game.ReviewBanditPress("escort");await Frame();Check(GuardJobs.Jobs(game.Session.DiagnosticSnapshot()).Length==1,"護衛を引受");game.ReviewBanditPress("depart");await Frame();game.ReviewSocialClose();game.Advance(game.Session.Seconds+2);
 var w=game.Session.DiagnosticSnapshot();var job=GuardJobs.Jobs(w).Single();Check(job.Departure!=null,"双方が出発");long until=w.Branches!.Journeys.Max(j=>j.Legs[^1].EndAt)+2;game.Advance(until);game.Press("bandit-encounter");await Frame();game.ReviewBanditPress("settle");await Frame();Check(GuardJobs.Jobs(game.Session.DiagnosticSnapshot()).Single().Phase=="精算済み","双方の到着後に報酬精算");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"escort.png"))==Error.Ok,"護衛の精算描画");}
 File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・接触戦・途中保存復旧・実護衛・現地精算",ComputerUse="未使用"}));GD.Print("BANDIT_ENCOUNTER_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
