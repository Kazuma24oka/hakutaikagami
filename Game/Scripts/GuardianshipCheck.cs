using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class GuardianshipCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"guardianship-ui-"+Guid.NewGuid().ToString("N")),true);
            front.QueueFree();await Frame();
            var chosen=start.Profile.Registered.Select(p=>p.Id switch {
                "created:security-leader"=>p with{Id="created:elder",BirthYear=1430},
                "created:security-one"=>p with{BirthYear=1520,Site="site:castle",BirthParents=["created:elder"]},
                _=>p with{Lord="created:elder"}}).ToArray();
            var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var plan=new WorldSetupRequest(chosen,[new("created:elder","created:security-house",["site:castle"],false)]);
            var setup=NewGameSetup.Confirm(scenario,plan,NewGameSetup.Preview(scenario,plan));
            var profile=start.Store.New(setup,chosen,"created:security-one",351,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,
                economyPolicy:HouseSuccession.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
                socialPolicy:CharacterRelations.Policy,comingOfAge:15);
            var w=NewCampaignStore.Build(profile);const long death=43200;
            var onset=new WorldSession(w);start.Store.Saves(profile).Save(onset,"manual",w.WorldId);
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();
            PopupPanel Panel()=>game.GetChildren().OfType<PopupPanel>().Single(w=>w.Visible);
            async Task Map(){Panel().Hide();await Frame();}
            async Task House(){game.Press("house-succession");await Frame();}
            async Task Click(string suffix)
            {
                var button=Panel().FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.EndsWith(suffix,StringComparison.Ordinal));
                button.EmitSignal(Button.SignalName.Pressed);await Frame();
            }
            void Advance(long at)
            {
                while(game.Session.Seconds<at){var before=game.Session.Seconds;game.Session.SetPaused(false);game.Session.AdvanceTo(at);Check(game.Session.Seconds>before,"世界時間が止まりました");}
                game.Session.SetPaused(true);game.ReviewFraction(0);
            }
            Advance(death);await House();await Click("本拠で家督を申し立てる");await Map();
            game.SaveCheckpoint("claimed");game.LoadCheckpoint("claimed");await House();
            await Click("に意向を尋ねる");
            bool Readable()=>Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>
                l.Text.Contains("：申立を支持") || l.Text.Contains("：申立に異議"));
            Check(Readable(),"家臣の現地返答を表示できません");await Map();
            var pose=game.Pose();game.SaveCheckpoint("answered");game.LoadCheckpoint("answered");var restored=game.Pose();
            Check(pose==restored,"復旧で配置が変わりました");await House();Check(Readable(),"家臣の現地返答が復元されません");
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/GuardianshipV1/runtime-local/ui");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),restored);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(Panel().GetViewport(),Path.Combine(output,"opened.png"))==Error.Ok,"開封画面の描画");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("GUARDIANSHIP_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
