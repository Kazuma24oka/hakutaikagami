using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class BattlefieldRulesCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool b,string s){if(!b)throw new InvalidDataException(s);}
 public override async void _Ready(){try{
 string output=RecordPath.Globalize("res://../Docs/Design/BattlefieldRulesV1/runtime-local/ui");Directory.CreateDirectory(output);var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateCraftReview(Path.Combine(Path.GetTempPath(),"battlefield-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 void Open(){foreach(var m in game.Session.GetActorView(game.Session.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())game.Session.Open(game.Session.PlayerControl,m.Id,game.Session.Revision);game.Refresh();}
 game.ChooseMilitary(NewMilitaryAction.Request);game.Advance(game.Session.Seconds+3);Open();game.ChooseMilitary(NewMilitaryAction.Accept);game.Advance(game.Session.Seconds+3);Open();game.ChooseMilitary(NewMilitaryAction.Pickup);
 game.Press("battlefield-rules");await Frame();game.ReviewBattlefieldPress("equip-0-Spear");game.ReviewBattlefieldPress("equip-1-Bow");game.ReviewBattlefieldPress("load-1-arrow");game.ReviewBattlefieldPress("equip-2-Cavalry");game.ReviewBattlefieldPress("load-2-fodder");await Frame();Check(BattlefieldRules.Loadout(game.Session.DiagnosticSnapshot(),NewGameMilitary.UnitIdsFor(game.Session.DiagnosticSnapshot())[1]).Kind==TroopKind.Bow,"弓を実受領");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"equipment.png"))==Error.Ok,"編成画面");}
 game.ReviewSocialClose();var funding=game.Session.ManageMilitaryRice(new(new("request:battlefield-review-rice"),NewGameMilitary.DepotFor(game.Session.DiagnosticSnapshot()),60),game.Session.PlayerControl,game.Session.Revision);Check(funding.Code==ResultCode.Accepted,funding.VisibleReason);game.Refresh();game.ChooseMilitary(NewMilitaryAction.Load);game.ChooseMilitary(NewMilitaryAction.Deploy);game.Press("battlefield-rules");game.ReviewBattlefieldPress("formation-Wedge");game.ReviewSocialClose();game.Advance(game.Session.Seconds+3);game.ReviewFraction(0);var w=game.Session.DiagnosticSnapshot();Check(w.NewMilitary!.Orders.Length>0,"実際の陣形命令");game.Press("save");await Until(()=>!game.IsSaving,"配置途中保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"保存復旧の一致");Check(pose==game.Pose(),"配置途中の3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());game.Advance(game.Session.Seconds+35);game.ReviewFraction(0);
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"formation.png"))==Error.Ok,"陣形の実描画");}
 File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始・装備受渡し・実陣形・途中保存復旧・3D一致",ComputerUse="未使用"}));GD.Print("BATTLEFIELD_RULES_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
