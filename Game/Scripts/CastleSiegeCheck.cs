using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class CastleSiegeCheck:Node
{
 async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 async Task Until(Func<bool> done,string label){var t=Stopwatch.StartNew();while(!done()){if(t.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
 static void Check(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public override async void _Ready(){try{
 string output=ProjectSettings.GlobalizePath("res://../Docs/Design/CastleSiegeV1/runtime-local/ui");Directory.CreateDirectory(output);
 var entry=new NewGameFrontEnd();AddChild(entry);await Frame();var start=entry.CreateSiegeReview(Path.Combine(Path.GetTempPath(),"bandit-ui-"+Guid.NewGuid().ToString("N")));entry.QueueFree();await Frame();
 var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Until(()=>!game.IsSaving,"開始保存");
 var session=game.Session;
 void Accepted(CommandResult result)=>Check(result.Code==ResultCode.Accepted,result.VisibleReason);
 void Military(NewMilitaryAction action,long amount=0)=>Accepted(session.DecideNewMilitary(new(new("request:ui-siege-military/"+session.Revision),action,amount),session.PlayerControl,session.Revision));
 void OpenLetters(){foreach(var m in session.GetActorView(session.PlayerControl).Messages.Where(m=>m.KnownBody==null))Accepted(session.Open(session.PlayerControl,m.Id,session.Revision));}
 EntityId site=new("site:akutagawa");
 Accepted(session.DecideFactionStance(new(new("decision:ui-hostile"),FactionStanceAction.DeclareHostile,new("created:siege-house"),new("created:keep-house"),0,30*86400),session.PlayerControl,session.Revision));
 Accepted(session.DecideNewMilitary(new(new("request:ui-duty"),NewMilitaryAction.Request){AuthorityPolicy=NewGameMilitary.AuthorityContract,LedgerPolicy=NewGameMilitary.LedgerIdentityContract},session.PlayerControl,session.Revision));
 game.Advance(2);OpenLetters();Military(NewMilitaryAction.Accept);game.Advance(4);OpenLetters();Military(NewMilitaryAction.Pickup);
 Accepted(session.ManageMilitaryRice(new(new("request:ui-military-food"),session.MilitaryEconomyView()!.Depot,120),session.PlayerControl,session.Revision));
 Military(NewMilitaryAction.Load,NewGameMilitary.Capacity);
 Accepted(session.ManageArmyRoad(new(new("request:ui-march"),ArmyRoadAction.March,site),session.PlayerControl,session.Revision));
 game.Advance(7);game.Advance(session.DiagnosticSnapshot().Branches!.Journeys.Last().Legs[^1].EndAt+2);Military(NewMilitaryAction.Deploy);game.Refresh();
 game.Press("castle-siege");await Frame();game.ReviewWorksPress("siege-Begin");await Frame();game.ReviewWorksPress("siege-Assault");await Frame();
 var assault=session.DiagnosticSnapshot().Sieges!.Orders.Last();Check(assault.Input.Action==SiegeAction.Assault&&assault.Due>session.Seconds,"外堀の強襲途中");
 if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"assault.png"))==Error.Ok,"強襲画面の描画");}
 game.ReviewSocialClose();game.Press("save");await Until(()=>!game.IsSaving,"強襲途中の保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"途中復旧の世界一致");Check(pose==game.Pose(),"3D一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());
 game.Advance(assault.Due!.Value);game.Press("castle-siege");await Frame();
 game.ReviewWorksPress("siege-Parley");await Frame();game.ReviewWorksPress("siege-DeliverGate");await Frame();
 game.ReviewWorksPress("siege-DeliverKeep");await Frame();Check(game.Session.DiagnosticSnapshot().Sieges!.Orders.Last().Outcome=="本丸を占有","本丸の受領");
 File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常画面の攻囲・強襲・途中保存復旧・開城",ComputerUse="未使用"}));GD.Print("CASTLE_SIEGE_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
