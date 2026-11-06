using Godot;
using HakutaiKagami.Core.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class HouseholdDetachmentCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Saved(NewGameRuntime game)
    {
        var timer=Stopwatch.StartNew();
        while(game.IsSaving){if(timer.Elapsed.TotalSeconds>120)throw new TimeoutException("軍務の保存");await Frame();}
    }
    static void Require(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
    public override async void _Ready()
    {
        try{
            var setup=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
            var store=new NewCampaignStore(Directory.CreateTempSubdirectory("hakutai-detachment-ui-").FullName);
            var profile=store.New(setup,[],"person:shingen",42,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,
                companionPolicy:PersonalCompanions.Policy,battlefieldPolicy:BattlefieldRules.Policy);
            var game=new NewGameRuntime();game.Configure(store,profile,false);AddChild(game);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Saved(game);
            var session=game.Session;var person=new EntityId(setup.People.First(p=>p.Lord=="person:shingen").Id);
            var agree=session.ManageCompanion(new(new("request:ui-follow"),person,CompanionAction.Agree,20),session.PlayerControl,session.Revision);
            Require(agree.Code==ResultCode.Accepted,agree.VisibleReason);game.Refresh();
            foreach(var action in new[]{NewMilitaryAction.Request,NewMilitaryAction.Accept}){
                game.ChooseMilitary(action);game.Advance(session.Seconds+3);
                foreach(var letter in session.GetActorView(session.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())
                    session.Open(session.PlayerControl,letter.Id,session.Revision);
                game.Refresh();
            }
            game.ChooseMilitary(NewMilitaryAction.Pickup);game.Press("battlefield-rules");await Frame();
            game.ReviewBattlefieldPress("captain-1-"+person.Value);game.ReviewSocialClose();
            game.Advance(session.Seconds+2);game.ReviewFraction(0);
            var w=session.DiagnosticSnapshot();var wing=NewGameMilitary.UnitIdsFor(w)[1];
            Require(HouseholdDetachment.Appointment(w,wing,session.Seconds,session.Revision+1)?.Actor==person,"左分隊の受任");
            var bytes=ContractJson.Write(w);var pose=game.Pose();game.Press("save");await Saved(game);
            game.Press("load");await Frame();game.ReviewFraction(0);
            Require(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && pose==game.Pose(),"受任中の保存と3D配置");
            game.Press("battlefield-rules");game.ReviewBattlefieldPress("release-1");game.ReviewSocialClose();
            var released=game.Session.DiagnosticSnapshot();
            Require(HouseholdDetachment.Appointment(released,wing,released.ElapsedSeconds,released.CommitSequence+1)==null,"復旧後の分隊指揮返還");
            GD.Print("HOUSEHOLD_DETACHMENT_UI_PASS");GetTree().Quit();
        }catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
