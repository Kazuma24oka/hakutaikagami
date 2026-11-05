using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class AbilityEffectsCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{string output=ProjectSettings.GlobalizePath("res://../Docs/Design/AbilityEffectsV1/runtime-local/ui");Directory.CreateDirectory(output);var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateAbilityReview(Path.Combine(Path.GetTempPath(),"craft-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 game.Press("broker-work");await Frame();game.ReviewBrokerPress("small-Purchase-person-nobunaga");await Frame();var terms=BrokerWork.Jobs(game.Session.DiagnosticSnapshot()).Single(j=>j.Terms.Worker==game.Session.PlayerControl).Terms;Check(terms.Fee==60&&terms.Abilities!=null,"能力を反映した引受条件");if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"ability.png"))==Error.Ok,"能力と買付条件の描画");}game.ReviewSocialClose();game.Advance(100);game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"準備途中の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());game.Advance(terms.Abilities!.ReadyAt);game.Press("broker-work");await Frame();foreach(var key in new[]{"buy","deliver","settle"}){game.ReviewBrokerPress(key);await Frame();}Check(BrokerWork.Jobs(game.Session.DiagnosticSnapshot()).Single(j=>j.Terms.Id==terms.Id).Phase=="精算済み","実買付・納品・精算");File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・能力による準備時間と報酬・買付精算・途中保存復旧",ComputerUse="未使用"}));GD.Print("ABILITY_EFFECTS_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
