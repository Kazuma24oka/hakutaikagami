using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class HouseScaleCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"house-scale-ui-"+Guid.NewGuid().ToString("N")));
            front.QueueFree();await Frame();
            var chosen=start.Profile.Registered;
            var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var plan=new WorldSetupRequest(chosen,[new("created:security-leader","created:security-house",["site:castle","site:kofu","site:odawara"],false)]);
            var setup=NewGameSetup.Confirm(scenario,plan,NewGameSetup.Preview(scenario,plan));
            EntityId actor=new("created:security-leader"),owner=new("created:security-house");
            NewCampaignProfile Create(ulong seed)=>start.Store.New(setup,chosen,actor.Value,seed,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,
                economyPolicy:HouseSuccession.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
                socialPolicy:CharacterRelations.Policy,comingOfAge:15);
            var profile=Create(0);var w=NewCampaignStore.Build(profile);
            start.Store.Saves(profile).Save(new WorldSession(w),"manual",w.WorldId);
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();
            AcceptDialog Panel()=>game.GetChildren().OfType<AcceptDialog>().Single(w=>w.Visible);
            async Task Map(){Panel().Hide();await Frame();}
            async Task Menu(){game.Press("household-court");await Frame();game.ReviewCourtCategory(1);await Frame();}
            bool Shows(string label)=>Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains(label));
            await Menu();Check(Shows("宣言済み：国人衆"),"旧来の規模");
            var scale=Panel().FindChildren("*","OptionButton",true,false).OfType<OptionButton>()
                .Single(c=>c.ItemCount==4 && c.GetItemText(0)=="国人衆の規模");
            scale.Select((int)CourtScale.Daimyo);game.ReviewCourtPress("scale");await Frame();
            Check(Shows("宣言済み：大名"),"昇格表示");
            await Map();game.SaveCheckpoint("scale-sent");game.LoadCheckpoint("scale-sent");
            while(game.Session.Seconds<120){game.Session.SetPaused(false);var before=game.Session.Seconds;game.Session.AdvanceTo(120);Check(game.Session.Seconds>before,"時間進行");}
            game.Session.SetPaused(true);game.ReviewFraction(0);
            Check(HouseScaleNotices.Known(game.Session.DiagnosticSnapshot(),new("created:security-two"),owner)==CourtScale.Daimyo,"家臣の通知受領");
            var pose=game.Pose();game.SaveCheckpoint("scale-received");game.LoadCheckpoint("scale-received");var restored=game.Pose();
            Check(pose==restored,"復旧後の3D配置");await Menu();Check(Shows("宣言済み：大名"),"復旧後の昇格表示");
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/HouseScaleV1/runtime-local/ui");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),restored);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(Panel().GetViewport(),Path.Combine(output,"opened.png"))==Error.Ok,"開封画面の描画");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("HOUSE_SCALE_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
