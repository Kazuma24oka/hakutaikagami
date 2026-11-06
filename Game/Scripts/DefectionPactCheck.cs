using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class DefectionPactCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"pact-ui-"+Guid.NewGuid().ToString("N")));
            front.QueueFree();await Frame();
            var chosen=start.Profile.Registered;
            var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var plan=new WorldSetupRequest(chosen,[new("created:security-leader","created:security-house",["site:castle"],false)]);
            var setup=NewGameSetup.Confirm(scenario,plan,NewGameSetup.Preview(scenario,plan));
            EntityId actor=new("created:security-one"),partner=new("created:security-two");
            NewCampaignProfile Create(ulong seed)=>start.Store.New(setup,chosen,actor.Value,seed,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,
                economyPolicy:HouseSuccession.Policy,companionPolicy:PersonalCompanions.Policy,developmentPolicy:CharacterDevelopment.Policy,
                socialPolicy:CharacterRelations.Policy,comingOfAge:15);
            var profile=Create(0);var w=NewCampaignStore.Build(profile);
            for(ulong seed=0;seed<100;seed++)
            {
                profile=Create(seed);w=NewCampaignStore.Build(profile);
                var probe=new WorldSession(w);probe.NegotiatePact(new("request:probe"),partner,PactPurpose.RiseTogether,probe.Revision);
                if(probe.DiagnosticSnapshot().SecretPacts?.SingleOrDefault()?.Accepted==true)break;
            }
            start.Store.Saves(profile).Save(new WorldSession(w),"manual",w.WorldId);
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();
            AcceptDialog Panel()=>game.GetChildren().OfType<AcceptDialog>().Single(w=>w.Visible);
            async Task Map(){Panel().Hide();await Frame();}
            async Task Menu(){game.Press("information-schemes");await Frame();}
            async Task Press(string key){game.ReviewSchemePress(key);await Frame();}
            await Menu();
            var choices=Panel().FindChildren("*","OptionButton",true,false).OfType<OptionButton>().ToArray();
            var person=choices.First(c=>Enumerable.Range(0,c.ItemCount).Any(i=>c.GetItemText(i)=="警固役 two"));
            person.Select(Enumerable.Range(0,person.ItemCount).First(i=>person.GetItemText(i)=="警固役 two"));
            var terms=choices.First(c=>Enumerable.Range(0,c.ItemCount).Any(i=>c.GetItemText(i)=="共に独立する"));
            terms.Select((int)PactPurpose.RiseTogether);
            await Press("consult-pact");
            var pact=game.Session.DiagnosticSnapshot().SecretPacts!.Single();
            Check(pact.Accepted && pact.Partner==partner,"参加の密約");
            await Press("call-"+pact.Id);await Map();
            game.SaveCheckpoint("in-transit");game.LoadCheckpoint("in-transit");
            while(game.Session.Seconds<120){var before=game.Session.Seconds;game.Session.SetPaused(false);game.Session.AdvanceTo(120);Check(game.Session.Seconds>before,"世界時間");}
            game.Session.SetPaused(true);game.ReviewFraction(0);await Menu();
            await Press("open-"+pact.Id);
            bool Readable()=>Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("約した行動")||l.Text.Contains("今回は応じられません"));
            Check(Readable(),"開封した返答");await Map();
            var pose=game.Pose();game.SaveCheckpoint("answered");game.LoadCheckpoint("answered");var restored=game.Pose();
            Check(pose==restored,"復旧で配置が変わりました");await Menu();Check(Readable(),"返答が復元されません");
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/DefectionPactV1/runtime-local/ui");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),restored);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(Panel().GetViewport(),Path.Combine(output,"opened.png"))==Error.Ok,"開封画面の描画");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("DEFECTION_PACT_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
