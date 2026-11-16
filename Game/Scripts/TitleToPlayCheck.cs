using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class TitleToPlayCheck:Node
{
 async Task CheckPoliticalMenus(NewGameRuntime game)
 {
     var before=ContractJson.Write(game.Session.DiagnosticSnapshot());
     game.Press("household-court");
     var section=game.FindChildren("*","OptionButton",true,false).OfType<OptionButton>()
         .Single(b=>Enumerable.Range(0,b.ItemCount).Any(i=>b.GetItemText(i)=="中央への取次"));
     int court=Enumerable.Range(0,section.ItemCount).Single(i=>section.GetItemText(i)=="中央への取次");
     section.Select(court);
     section.EmitSignal(OptionButton.SignalName.ItemSelected,(long)court);
     await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
     RequireLabel(game,"京で取次役に面会");
     game.ReviewSocialClose();
     game.Press("battlefield-rules");
     RequireLabel(game,"一族に分隊を預けるには");
     game.ReviewSocialClose();
     game.Press("truce-negotiation");
     var treaty=game.FindChildren("*","Button",true,false).OfType<Button>()
         .Single(b=>b.Text=="独立条項を選んで正式条約を交渉" && b.IsVisibleInTree());
     treaty.EmitSignal(Button.SignalName.Pressed);
     var coalition=game.FindChildren("*","CheckButton",true,false).OfType<CheckButton>()
         .Single(b=>b.Text=="包囲網の書状" && b.IsVisibleInTree());
     coalition.ButtonPressed=true;
     if(!before.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())))
         throw new InvalidDataException("用件を閲覧しただけで世界が変化しました");
     game.ReviewSocialClose();
 }
 static void RequireLabel(Node root,string fragment)
 {
     if(!root.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.IsVisibleInTree() && l.Text.Contains(fragment)))
         throw new InvalidDataException("画面に必要な案内がありません："+fragment);
 }
 public override async void _Ready(){try{var entry=new NewGameFrontEnd();AddChild(entry);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);var game=await entry.ReviewTitleToPlay(Path.Combine(Path.GetTempPath(),"title-to-play-"+Guid.NewGuid().ToString("N")));var w=game.Session.DiagnosticSnapshot();if(!CastleWorks.Enabled(w)||w.NewGame!.EconomyPolicy!=HouseSuccession.Policy)throw new InvalidDataException("開始設定が不一致です");await CheckPoliticalMenus(game);var saved=ContractJson.Write(w);game.Press("load");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(!saved.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())))throw new InvalidDataException("開始保存を復旧できません");var dir=RecordPath.Globalize("res://../Docs/Design/TitleToPlayFix/runtime-local");Directory.CreateDirectory(dir);if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(ViewportCapture.Save(GetViewport(),Path.Combine(dir,"started.png"))!=Error.Ok)throw new IOException("描画に失敗しました");}GD.Print("TITLE_TO_PLAY_PASS");GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
