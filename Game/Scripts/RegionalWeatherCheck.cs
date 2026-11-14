using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class RegionalWeatherCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{string output=RecordPath.Globalize("res://../Docs/Design/RegionalWeatherV1/runtime-local/ui");Directory.CreateDirectory(output);var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateWeatherReview(Path.Combine(Path.GetTempPath(),"craft-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 var initial=game.Session.DiagnosticSnapshot();var site=new EntityId("site:nejo");var disaster=RegionalWeather.Events(initial,site,3*365*86400L).First(e=>e.Fields>0);game.Advance(disaster.At);game.Press("public-works");await Frame();game.ReviewWorksPress("build-RepairFields");await Frame();Check(game.Session.DiagnosticSnapshot().PublicWorks!.Orders.Any(q=>q.Input.Kind==WorksKind.RepairFields),"現地で復旧を引受");if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"weather.png"))==Error.Ok,"被害と普請の自前描画");}game.ReviewSocialClose();game.Advance(disaster.At+3*86400);game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"復旧途中保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"3D一致");game.Advance(disaster.At+7*86400);Check(RegionalWeather.Damage(game.Session.DiagnosticSnapshot(),site,WorksKind.RepairFields,game.Session.Seconds,game.Session.Revision+1)==0,"工期後に被害回復");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),pose);File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・現地被害・実費復旧・途中保存復旧",ComputerUse="未使用"}));GD.Print("REGIONAL_WEATHER_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
