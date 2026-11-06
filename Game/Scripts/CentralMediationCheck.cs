using Godot;
using HakutaiKagami.Core.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class CentralMediationCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Saved(NewGameRuntime game)
    {
        var timer=Stopwatch.StartNew();
        while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("仲裁の保存");await Frame();}
    }
    static void Require(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
    public override async void _Ready()
    {
        try{
            var site=TempleMilitary.Site;
            var applicant=new RegisteredOfficer("created:mediation-ui",null,"堺の当主",1520,new(60,50,70,40),StartTemplate.Daimyo,site.Value,null,"堺家",
                HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1));
            var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var request=new WorldSetupRequest([applicant],[new(applicant.Id,"org:mediation-ui",[site.Value],false)]);
            var setup=NewGameSetup.Confirm(scenario,request,NewGameSetup.Preview(scenario,request));
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-mediation-ui-").FullName);
            var profile=store.New(setup,[applicant],applicant.Id,42,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,worldArmyPolicy:NpcArmies.CompactCombatPolicy,trucePolicy:TruceNegotiations.Policy);
            var game=new NewGameRuntime();game.Configure(store,profile,false);AddChild(game);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Saved(game);
            var session=game.Session;var broker=CentralCourtCatalog.TempleBroker;
            void Good(CommandResult r)=>Require(r.Code==ResultCode.Accepted,r.VisibleReason);
            Good(session.ManageSocial(new(new("request:ui-visit"),SocialWorldAction.Visit,broker),session.Revision));
            Good(session.ManageSocial(new(new("request:ui-gift"),SocialWorldAction.DispatchGift,broker,Amount:100),session.Revision));
            game.Advance(session.Seconds+3);game.Refresh();
            async Task Court(){game.Press("household-court");await Frame();game.ReviewCourtCategory(5);await Frame();}
            await Court();game.ReviewCourtPress("peace/"+broker.Value);game.ReviewSocialClose();
            var asked=session.DiagnosticSnapshot();Require(asked.Court!.Events.Any(e=>e.Input.Action==CourtAction.AskPeaceMediation),"堺の家中画面から仲裁を依頼");
            game.ReviewFraction(0);var pose=game.Pose();game.Press("save");await Saved(game);game.Press("load");await Frame();game.ReviewFraction(0);
            Require(ContractJson.Write(asked).SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && pose==game.Pose(),"仲裁依頼と3D配置の実保存復旧");
            game.Advance(game.Session.Seconds+5);session=game.Session;
            var letter=session.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.Kind==PeaceMediation.Proposal&&m.RecipientId==applicant.Id);
            for(int n=0;n<8 && !session.DiagnosticSnapshot().Messaging.Receipts.Any(r=>r.MessageId==letter.Id);n++){
                game.Advance(session.Seconds+2);await Frame();
            }
            Good(session.Open(session.PlayerControl,new(letter.Id),session.Revision));game.Refresh();
            var answer=PeaceMediation.Votes(session.DiagnosticSnapshot(),session.PlayerControl).Single(i=>i.Action==CourtAction.AcceptPeaceProposal);
            await Court();game.ReviewCourtPress(answer.Id.Value);game.ReviewSocialClose();
            Require(session.DiagnosticSnapshot().Messaging.Messages.Any(m=>m.Kind==PeaceMediation.Response && m.SenderId==applicant.Id && m.DeliverySite==site.Value),"家中画面の承諾で堺へ返書を発送");
            GD.Print("CENTRAL_MEDIATION_UI_PASS");GetTree().Quit();
        }catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
