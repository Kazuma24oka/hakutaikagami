using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class HouseReformCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"house-reform-ui-"+Guid.NewGuid().ToString("N")));
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
            await Menu();game.ReviewCourtCategory(3);await Frame();
            game.ReviewCourtPress("consult-reform");await Frame();
            var consult=game.Session.DiagnosticSnapshot().Court!.Events.Single(e=>e.Input.Action==CourtAction.ConsultReform);
            await Map();game.SaveCheckpoint("reform-sent");game.LoadCheckpoint("reform-sent");
            while(game.Session.Seconds<240){game.Session.SetPaused(false);var before=game.Session.Seconds;game.Session.AdvanceTo(240);Check(game.Session.Seconds>before,"時間進行");}
            game.Session.SetPaused(true);await Menu();game.ReviewCourtCategory(3);await Frame();
            game.ReviewCourtPress("reform-answer-"+consult.Input.Id);await Frame();
            game.ReviewCourtPress("law-"+consult.Input.Id);await Frame();
            await Map();game.ReviewFraction(0);var pose=game.Pose();
            game.SaveCheckpoint("reform-enacted");game.LoadCheckpoint("reform-enacted");
            Check(pose==game.Pose(),"復旧後の3D配置");
            await Menu();game.ReviewCourtCategory(3);await Frame();
            Check(Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("家法が発効済み")),"復旧後の改革表示");
            GD.Print("HOUSE_REFORM_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
