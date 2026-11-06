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
 async Task CheckRoad(string output)
 {
    var entry=new NewGameFrontEnd(); AddChild(entry); await Frame();
    var start=entry.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"road-contact-"+Guid.NewGuid().ToString("N")));
    entry.QueueFree(); await Frame();
    var game=new NewGameRuntime(); game.Configure(start.Store,start.Profile,false); AddChild(game);
    game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); await Frame();
    await Until(()=>!game.IsSaving,"街道確認版の開始保存");
    var session=game.Session; var agent=new EntityId("created:security-one");
    void Accepted(CommandResult result)=>Check(result.Code==ResultCode.Accepted,result.VisibleReason);
    Accepted(session.ManageCompanion(new(new("request:road-agree"),agent,CompanionAction.Agree,20),session.PlayerControl,session.Revision));
    var from=SocialWorld.Site(session.DiagnosticSnapshot(),session.PlayerControl)!.Value;
    var world=session.DiagnosticSnapshot();
    var edge=BranchRoutes.GraphEdges(world).First(e=>!NationalRoutes.Sea(e.Id) &&
        (RegionalCampaign.Site(e.From)==from || RegionalCampaign.Site(e.To)==from));
    var to=RegionalCampaign.Site(edge.From)==from ? edge.To : edge.From;
    Accepted(session.ManageScheme(new(new("request:road-scout"),SchemeAction.SendScout,agent,session.PlayerControl,Destination:RegionalCampaign.Site(to)),session.Revision));
    if(BanditEncounter.NeedsContact(session.DiagnosticSnapshot()))
    {
        Accepted(session.ManageBandits(new(new("request:road-bandits"),BanditAction.Encounter,from),session.Revision));
        Accepted(session.ManageBandits(new(new("request:road-toll"),BanditAction.Pay,from),session.Revision));
    }
    var move=TravelWorld.Move(session.DiagnosticSnapshot(),"request:road-follow",to);
    Accepted(session.Submit(move,session.Revision));
    long at=move.EffectiveAt+move.DeliverySeconds+30;
    while(session.Seconds<at){long before=session.Seconds;session.SetPaused(false);session.AdvanceTo(at);Check(session.Seconds>before,"街道まで時計を進める");}
    session.SetPaused(true);game.ReviewFraction(0);
    game.Press("information-schemes");await Frame();game.ReviewSchemePress("contact-menu");await Frame();
    game.ReviewSchemePress("see-created-security-one");await Frame();
    game.ReviewSocialClose();game.Press("save");await Until(()=>!game.IsSaving,"街道で目撃後に保存");
    var saved=ContractJson.Write(session.DiagnosticSnapshot());var pose=game.Pose();
    game.Press("load");await Frame();game.ReviewFraction(0);
    Check(saved.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && pose==game.Pose(),"街道復旧の状態と3D座標");
    game.Press("information-schemes");await Frame();game.ReviewSchemePress("contact-menu");await Frame();
    game.ReviewSchemePress("seek-created-security-one");await Frame();game.ReviewSchemePress("strike-created-security-one");await Frame();
    var result=game.Session.DiagnosticSnapshot().Schemes!.Events.Single(e=>Assassination.IsConclusion(e.Input.Action));
    Check(result.Site.Value=="road:"+edge.Id,"目撃した街道での襲撃");
    File.WriteAllText(Path.Combine(output,"road-pose.json"),pose);
    GD.Print("ROAD_CONTACT_UI_PASS"); game.QueueFree(); await Frame();
 }
 public override async void _Ready(){try{
 string output=ProjectSettings.GlobalizePath("res://../Docs/Design/AssassinationV1/runtime-local/ui");Directory.CreateDirectory(output);await CheckRoad(output);
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
