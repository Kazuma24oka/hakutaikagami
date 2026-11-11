using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
// 通常開始の地図で、ドット絵の武将が平時は和服、戦いでは甲冑になり、時刻に合ったコマを描くかを確かめる。
public partial class DotOfficerCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 async Task Capture(string output,string name){if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,name))==Error.Ok,"自前描画 "+name);}
 public override async void _Ready(){try{
  string output=ProjectSettings.GlobalizePath("res://../Docs/Design/DotOfficerV1/runtime-local/ui");Directory.CreateDirectory(output);
  Check(DotOfficerActor.Available,"ドット絵の束がある");
  var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateBanditReview(Path.Combine(Path.GetTempPath(),"dot-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
  var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
  game.Advance(2);game.ReviewFraction(0);await Frame();
  var me=game.Session.PlayerControl.Value;
  string Pose()=>game.Pose();
  var peace=System.Text.Json.JsonDocument.Parse(Pose()).RootElement.GetProperty("Dots").EnumerateArray().ToArray();
  Check(peace.Any(d=>d.GetProperty("Id").GetString()==me&&d.GetProperty("Key").GetString()!.EndsWith("/peace")),"平時の本人は和服");
  await Capture(output,"peace.png");
  game.Press("bandit-encounter");await Frame();game.ReviewBanditPress("encounter");await Frame();game.ReviewBanditPress("fight");await Frame();game.ReviewSocialClose();game.Advance(game.Session.Seconds+2);game.ReviewFraction(0);await Frame();
  var war=System.Text.Json.JsonDocument.Parse(Pose()).RootElement.GetProperty("Dots").EnumerateArray().ToArray();
  var mine=war.Single(d=>d.GetProperty("Id").GetString()==me);Check(mine.GetProperty("Key").GetString()!.Contains("/war"),"戦いでは甲冑");
  var soldiers=System.Text.Json.JsonDocument.Parse(Pose()).RootElement.GetProperty("SoldierDots").EnumerateArray().Select(d=>d.GetProperty("Key").GetString()!).ToArray();
  Check(soldiers.Length>0&&soldiers.All(k=>k.StartsWith("generic/ronin/")),"賊も甲冑のドット絵");
  await Capture(output,"war.png");
  var a=Pose();game.ReviewFraction(0);await Frame();Check(a==Pose(),"同じ時刻なら同じ絵");
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Peace=peace.Select(d=>d.GetProperty("Key").GetString()).ToArray(),War=war.Select(d=>d.GetProperty("Key").GetString()).ToArray(),Soldiers=soldiers,ComputerUse="未使用"}));
  GD.Print("DOT_OFFICER_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
