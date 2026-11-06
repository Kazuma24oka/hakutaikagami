using Godot;
using HakutaiKagami.Core.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class SupremeOfficeCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Saved(NewGameRuntime game)
    {
        var timer=Stopwatch.StartNew();
        while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("栄典の保存");await Frame();}
    }
    static void Require(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
    public override async void _Ready()
    {
        try{
            var site=CentralCourtCatalog.Kyoto;
            var recipient=new RegisteredOfficer("created:honor-ui",null,"京の当主",1520,new(50,50,50,50),StartTemplate.Daimyo,site.Value,null,"京家",
                HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1));
            var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var request=new WorldSetupRequest([recipient],[new(recipient.Id,"org:honor-ui",[site.Value],false)]);
            var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-honor-ui-").FullName);
            var profile=store.New(setup,[recipient],CentralCourtCatalog.Shogun.Value,42,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy);
            var game=new NewGameRuntime();game.Configure(store,profile,false);AddChild(game);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Saved(game);
            var session=game.Session;var honor=ShogunateHonors.Available(session.DiagnosticSnapshot(),session.PlayerControl)
                .Single(i=>i.Person.Value==recipient.Id && i.Action==CourtAction.GrantCompanionHonor);
            game.Press("household-court");await Frame();game.ReviewCourtCategory(5);await Frame();
            game.ReviewCourtPress(honor.Id.Value);game.ReviewSocialClose();
            var awarded=session.DiagnosticSnapshot();
            Require(ShogunateHonors.Witnessed(awarded,new(recipient.Id)).Single()=="御相伴衆","京で御相伴衆を授与");
            game.ReviewFraction(0);var pose=game.Pose();game.Press("save");await Saved(game);
            game.Press("load");await Frame();game.ReviewFraction(0);
            Require(ContractJson.Write(awarded).SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && pose==game.Pose(),"栄典と3D配置の実保存復旧");
            GD.Print("SUPREME_OFFICE_UI_PASS");GetTree().Quit();
        }catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}