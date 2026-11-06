using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class TitleToPlayCheck:Node
{
 public override async void _Ready(){try{var entry=new NewGameFrontEnd();AddChild(entry);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);var game=await entry.ReviewTitleToPlay(Path.Combine(Path.GetTempPath(),"title-to-play-"+Guid.NewGuid().ToString("N")));var w=game.Session.DiagnosticSnapshot();if(!CastleWorks.Enabled(w)||w.NewGame!.EconomyPolicy!=HouseSuccession.Policy)throw new InvalidDataException("開始設定が不一致です");var saved=ContractJson.Write(w);game.Press("load");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(!saved.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())))throw new InvalidDataException("開始保存を復旧できません");var dir=ProjectSettings.GlobalizePath("res://../Docs/Design/TitleToPlayFix/runtime-local");Directory.CreateDirectory(dir);if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(ViewportCapture.Save(GetViewport(),Path.Combine(dir,"started.png"))!=Error.Ok)throw new IOException("描画に失敗しました");}GD.Print("TITLE_TO_PLAY_PASS");GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
