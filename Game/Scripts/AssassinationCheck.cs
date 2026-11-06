using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class AssassinationCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{
 string output=ProjectSettings.GlobalizePath("res://../Docs/Design/AssassinationV1/runtime-local/ui");Directory.CreateDirectory(output);
 var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"scheme-operation-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
 var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 game.Press("information-schemes");await Frame();game.ReviewSchemePress("contact-menu");await Frame();
 game.ReviewSchemePress("see-created-security-one");await Frame();game.ReviewSchemePress("seek-created-security-one");await Frame();
 game.ReviewSchemePress("strike-created-security-one");await Frame();
 var world=game.Session.DiagnosticSnapshot();var conclusion=world.Schemes!.Events.Single(e=>Assassination.IsConclusion(e.Input.Action));
 Check(conclusion.Input.Subject==new EntityId("created:security-one")&&conclusion.Actor==world.PlayerControl,"確認した相手との接触戦");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"encounter.png"))==Error.Ok,"接触戦の結果画面");}
 game.ReviewSocialClose();game.Press("save");await Until(()=>!game.IsSaving,"接触戦後の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());
 File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・現地で目撃と接触・暗殺と復旧",ComputerUse="未使用"}));GD.Print("ASSASSINATION_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
