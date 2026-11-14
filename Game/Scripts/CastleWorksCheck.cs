using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class CastleWorksCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{
 string output=RecordPath.Globalize("res://../Docs/Design/CastleWorksV1/runtime-local/ui");Directory.CreateDirectory(output);
 var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateCraftReview(Path.Combine(Path.GetTempPath(),"bandit-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
 var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 game.Press("public-works");await Frame();game.ReviewWorksPress("districts");await Frame();game.ReviewWorksPress("district-Gate");await Frame();game.ReviewWorksPress("fort-Gatehouse");await Frame();
 var site=new EntityId("site:sakai");var order=game.Session.DiagnosticSnapshot().PublicWorks!.Orders.Single(q=>q.Input.Site==site&&q.Input.Section==CastleSection.Gate);Check(order.Due>game.Session.Seconds,"城門の普請中");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"construction.png"))==Error.Ok,"普請画面の描画");}
 game.ReviewSocialClose();game.Press("save");await Until(()=>!game.IsSaving,"普請途中の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());
 File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・城門の普請・途中保存復旧",ComputerUse="未使用"}));GD.Print("CASTLE_WORKS_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
