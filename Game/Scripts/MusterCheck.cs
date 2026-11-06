using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class MusterCheck : Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"muster-ui-"+Guid.NewGuid().ToString("N")));
            front.QueueFree();await Frame();
            EntityId actor=new("created:security-one"),head=new("created:security-leader");
            var profile=start.Store.New(start.Profile.Setup,start.Profile.Registered,actor.Value,42,historicalCatalog:HistoricalStartCatalog.Current,
                inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:HouseSuccession.Policy);
            var world=NewCampaignStore.Build(profile);
            var notice=new CourtInput(new("request:review-muster"),CourtAction.SendMusterNotice,actor,new("created:security-house"),new("site:castle"),Amount:6,When:120);
            world.CommitSequence++;
            HouseholdCourt.Execute(world,notice,head);
            Check(start.Store.Saves(profile).Save(new WorldSession(world),"manual",world.WorldId).Published,"初期保存");
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();
            async Task Court(int tab){game.Press("household-court");await Frame();game.ReviewCourtCategory(tab);await Frame();}
            bool Shows(string text)=>game.GetChildren().OfType<AcceptDialog>().Single(d=>d.Visible)
                .FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains(text));
            await Court(4);Check(!Shows("軍役6人"),"受領前に予約が見える");
            game.ReviewCourtCategory(0);await Frame();game.ReviewCourtPress("read-document");await Frame();
            game.ReviewCourtCategory(4);await Frame();Check(Shows("軍役6人"),"受領後の予約");
            game.ReviewSocialClose();game.SaveCheckpoint("reserved");game.LoadCheckpoint("reserved");await Court(4);
            Check(Shows("軍役6人"),"復旧した予約");game.ReviewSocialClose();
            while(game.Session.Seconds<130){long before=game.Session.Seconds;game.Session.SetPaused(false);game.Session.AdvanceTo(130);Check(game.Session.Seconds>before,"集合時刻までの進行");}
            game.Session.SetPaused(true);game.ReviewFraction(0);await Court(4);
            game.ReviewCourtPress("attend/"+notice.Id.Value);await Frame();
            const string result="軍役6人のうち0人で参集（遅参）";
            Check(Shows(result),"参集の実数と遅参");game.ReviewSocialClose();
            var pose=game.Pose();game.SaveCheckpoint("attended");game.LoadCheckpoint("attended");Check(pose==game.Pose(),"参集後の配置");
            await Court(4);Check(Shows(result),"復旧後の参集結果");
            GD.Print("MUSTER_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
