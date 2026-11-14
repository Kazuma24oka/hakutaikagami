using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
// 全国1560年の開始画面と地形経路の重ね描き、織田信長の出発を確認する（2026-11-12、v111）。
public partial class NationalStartCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 async Task Capture(string output,string name){for(int i=0;i<4;i++)await Frame();if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,name))==Error.Ok,"画像の書き出し");}
 public override async void _Ready(){try{
  string output=RecordPath.Globalize("res://../Docs/Design/NationalStartV1/runtime-local/ui");Directory.CreateDirectory(output);
  var entry=new NewGameFrontEnd();AddChild(entry);await Frame();await Frame();entry.ReviewUseRoot(Path.Combine(output,"data-"+Guid.NewGuid().ToString("N")));
  await entry.ReviewNationalStartMap(Frame);await Capture(output,"national-routes.png");
  string goal=await entry.ReviewNationalStart(Frame);await Capture(output,"departed.png");
  var game=entry.ReviewGame!;game.Advance(game.Session.Seconds+600);{var sw=game.Session.DiagnosticSnapshot();Check(sw.Branches!.Journeys.Any(j=>j.Actor==game.Session.PlayerControl),$"旅が始まる 時刻{sw.ElapsedSeconds} 停止{sw.Paused} 依頼{string.Join(",",sw.Requests.Where(r=>r.Intent.Actor==game.Session.PlayerControl).Select(r=>r.Intent.Action+" "+r.Id))} 結果{game.LastDepartureResult} 時計{game.IsClockControlling}");}await Capture(output,"travelling.png");
  File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="スタート→新規ゲーム→全国→織田信長→始める→近くの城へ出発→10分進める",Goal=NewGameWorld.SiteName("site:"+goal),People=game.Session.DiagnosticSnapshot().Characters.Length}));
  GD.Print("NATIONAL_START_UI_PASS");GetTree().Quit();
 }catch(Exception e){GD.PrintErr("NATIONAL_START_UI_FAIL "+e);GetTree().Quit(1);}}
}
