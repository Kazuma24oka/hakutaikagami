using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class FamilyNewsCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    static void Advance(WorldSession s,long target)
    {
        while(s.Seconds<target){s.SetPaused(false);long before=s.Seconds;s.AdvanceTo(target);Check(s.Seconds>before,"世界時間が進みません");}
        s.SetPaused(true);
    }
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"family-ui-"+Guid.NewGuid().ToString("N")),true);
            front.QueueFree();await Frame();
            var profile=start.Store.New(start.Profile.Setup,start.Profile.Registered,start.Profile.Selected,start.Profile.Seed,historicalCatalog:start.Profile.HistoricalCatalog,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,comingOfAge:15);
            var world=NewCampaignStore.Build(profile);
            long newYear=366L*86400;world.ElapsedSeconds=newYear-1;
            var s=new WorldSession(world);Advance(s,newYear);
            var state=s.DiagnosticSnapshot();var letter=state.Messaging.Messages.Single(m=>m.Kind==FamilyNotice.MaturityKind);
            start.Store.Saves(profile).Save(s,"manual",state.WorldId);
            var game=new NewGameRuntime();game.Configure(start.Store,profile,true);AddChild(game);await Frame();
            async Task<Window> OpenNews()
            {
                game.Press("family-news");await Frame();
                return game.GetChildren().OfType<Window>().Single(w=>w.Visible && w.Title=="家族の知らせ");
            }
            var panel=await OpenNews();
            bool Shows(Window dialog,string text)=>dialog.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text==text);
            Check(!Shows(panel,letter.Body) && !panel.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text=="届いた封書を開く"),"配送中の内容が漏れています");
            game.ReviewSocialClose();Advance(game.Session,state.Messaging.Attempts.Single(a=>a.MessageId==letter.Id).DueAt);
            game.ReviewFraction(0);panel=await OpenNews();
            Check(!Shows(panel,letter.Body),"開封前に内容が見えています");
            panel.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text=="届いた封書を開く").EmitSignal(Button.SignalName.Pressed);
            await Frame();panel=game.GetChildren().OfType<Window>().Single(w=>w.Visible && w.Title=="家族の知らせ");
            Check(Shows(panel,letter.Body),"開封後に元服の報告を読めません");
            game.ReviewSocialClose();game.SaveCheckpoint("family-opened");var pose=game.Pose();game.LoadCheckpoint("family-opened");
            Check(pose==game.Pose(),"復旧で3D表示が変わりました");panel=await OpenNews();Check(Shows(panel,letter.Body),"開封済み記録が失われました");
            var output=RecordPath.Globalize("res://../Docs/Design/FamilyLifeV1/runtime-local/ui");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);
            File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(panel.GetViewport(),Path.Combine(output,"opened.png"))==Error.Ok,"開封後の画面描画");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("FAMILY_NEWS_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
