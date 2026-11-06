using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class HouseRestorationCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"restoration-ui-"+Guid.NewGuid().ToString("N")));
            front.QueueFree();await Frame();
            var chosen=start.Profile.Registered.Select(p=>p.Id=="created:security-two"?p with { Role=StartTemplate.Castellan }:p).ToArray();
            var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var plan=new WorldSetupRequest(chosen,[new("created:security-leader","created:security-house",scenario.Castles.Where(c=>c.Faction=="org:oda").Select(c=>c.Id).ToArray(),true)]);
            var setup=NewGameSetup.Confirm(scenario,plan,NewGameSetup.Preview(scenario,plan));
            EntityId actor=new("person:nobunaga"),partner=new("created:security-two");
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
            async Task Court(){game.Press("household-court");await Frame();game.ReviewCourtCategory(3);await Frame();}
            await Court();
            game.ReviewCourtPress("restore/org:oda/"+partner.Value);await Frame();await Map();
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
            game.Session.SetPaused(true);game.ReviewFraction(0);await Court();
            var application=HouseRestoration.ContinuityApplications(game.Session.DiagnosticSnapshot(),actor).Single();
            game.ReviewCourtPress(application.Id.Value);await Frame();await Map();
            var pose=game.Pose();game.SaveCheckpoint("restored-house");game.LoadCheckpoint("restored-house");
            Check(pose==game.Pose(),"再興後の配置");await Court();
            Check(Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("再興後の家の原本を照合済み")),"再興後の表示");
            GD.Print("HOUSE_RESTORATION_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
