using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

public partial class HeirPledgeCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public override async void _Ready()
    {
        try
        {
            var front=new NewGameFrontEnd();AddChild(front);await Frame();
            var start=front.CreateSecurityReview(Path.Combine(Path.GetTempPath(),"heir-ui-"+Guid.NewGuid().ToString("N")),true);
            front.QueueFree();await Frame();
            var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);await Frame();
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
            await House();await Click("を後継に指名");await Click("に誓紙を求める");await Map();
            game.Session.SetPaused(true);game.SaveCheckpoint("outbound");game.LoadCheckpoint("outbound");
            var state=game.Session.DiagnosticSnapshot();var request=state.Messaging.Messages.Single(m=>m.Kind==HeirPledges.RequestKind);
            Advance(state.Messaging.Attempts.Single(a=>a.MessageId==request.Id).DueAt);
            state=game.Session.DiagnosticSnapshot();var reply=state.Messaging.Messages.Single(m=>m.Kind==HeirPledges.ReplyKind);
            await House();Check(!Panel().FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text=="届いた返書を開く"),"未着の返書が表示されました");await Map();
            Advance(state.Messaging.Attempts.Single(a=>a.MessageId==reply.Id).DueAt);await House();await Click("届いた返書を開く");
            bool Readable()=>Panel().FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains(reply.Body));
            Check(Readable(),"開封しても返答が表示されません");await Map();
            var pose=game.Pose();game.SaveCheckpoint("opened");game.LoadCheckpoint("opened");var restored=game.Pose();Check(pose==restored,"復旧で配置が変わりました");await House();Check(Readable(),"既読の返答を復元できません");
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/HeirDesignationV1/runtime-local/ui");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),restored);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ViewportCapture.Save(Panel().GetViewport(),Path.Combine(output,"opened.png"))==Error.Ok,"開封画面の描画");
            File.WriteAllText(Path.Combine(output,"result.json"),"{\"Status\":\"PASSED\"}");
            GD.Print("HEIR_PLEDGE_UI_PASS");GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Quit(1);}
    }
}
