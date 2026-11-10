using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class SocialWorldCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Until(Func<bool> done,string label){var timer=Stopwatch.StartNew();while(!done()){if(timer.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
    static void Check(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    public override async void _Ready()
    {
        try{
            string output=ProjectSettings.GlobalizePath("res://../Docs/Design/SocialWorldV1/runtime-local/ui");Directory.CreateDirectory(output);
            var entry=new NewGameFrontEnd();AddChild(entry);await Frame();await Frame();
            var start=entry.CreateJudgmentReview(Path.Combine(Path.GetTempPath(),"npc-judgment-ui-"+Guid.NewGuid().ToString("N")));
            Check(start.Profile.Judgment==new NpcJudgmentSettings(NpcJudgment.Policy,HistoryMode.Fictional,NpcAttention.Beginner){UseInspectionReferences=true,Challenge=NpcChallenge.Standard},"開始画面の選択と保存定義");
            await Frame();if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"settings.png"))==Error.Ok,"設定画面の自前描画");}
            entry.QueueFree();await Frame();var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Frame();
            await Until(()=>!game.IsSaving,"初期保存");game.Advance(10);
            var world=game.Session.DiagnosticSnapshot();var task=world.Tasks.Single(t=>t.Executor==world.PlayerControl&&t.Issuer!=t.Executor);
            Check(task.Agreement==AgreementPhase.AwaitingRecognition,"本人の開封を待つ");
            var message=world.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="contract-order");
            game.SelectUnreadMail(new(message.Id));game.Press("open");await Frame();game.Press("work-reply");await Frame();
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"offer.png"))==Error.Ok,"会話と返答の自前描画");}
            game.Press("work-assent");game.Press("work-reply");await Frame();
            Check(game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Id==task.Id).Agreement==AgreementPhase.Active,"会話からの本人承諾");
            game.Advance(30);world=game.Session.DiagnosticSnapshot();var reply=world.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="assessment-notice");
            game.SelectUnreadMail(new(reply.Id));game.Press("open");await Frame();game.Press("work-reply");game.Press("work-pay");game.Press("work-reply");await Frame();
            Check(NewGameWork.Paid(game.Session.DiagnosticSnapshot(),task.Id),"既存会話から実在銭の受渡し");
            game.Press("work-close");
            game.Press("social-world");await Frame();game.ReviewSocialOpenPerson();await Frame();game.ReviewSocialAction(SocialWorldAction.Visit);await Frame();
            game.ReviewSocialOpenPerson();await Frame();long giftBefore=NewGameWork.Balance(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl);game.ReviewSocialAction(SocialWorldAction.DispatchGift);await Frame();Check(NewGameWork.Balance(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl)==giftBefore-20,"実物の銭の贈答");
            game.Press("temple-world");await Frame();long donationBefore=NewGameWork.Balance(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl);game.ReviewSocialAction(SocialWorldAction.Donate);await Frame();Check(NewGameWork.Balance(game.Session.DiagnosticSnapshot(),game.Session.PlayerControl)==donationBefore-20,"実在資金の寄進");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"temple.png"))==Error.Ok,"寺社相談の自前描画");}
            game.Press("social-world");await Frame();game.ReviewSocialOpenPerson();await Frame();game.ReviewSocialAction(SocialWorldAction.TellRumor);await Frame();Check(game.Session.DiagnosticSnapshot().SocialWorld!.Events.Any(e=>e.Input.Action==SocialWorldAction.TellRumor),"知った噂を現地で伝達");game.Press("renown-world");await Frame();
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"renown.png"))==Error.Ok,"本人の評判の自前描画");}game.ReviewSocialClose();
Check(game.ReviewNotebookHasGoals,"手帳から既存の目標と待遇へ到達");var chronicle=game.Session.PersonalChronicle(game.Session.PlayerControl);Check(chronicle.Length>0,"開封後の年代記");foreach(var verb in new[]{"verb-meet","verb-write","verb-entrust","verb-move","verb-notebook","verb-list","verb-notebook"}){game.Press(verb);await Frame();}game.Press("chronicle");await Frame();await Frame();if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"chronicle.png"))==Error.Ok,"年代記の自前描画");}await Frame();await Frame();game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"実保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();
            game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"復旧後の世界一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());Check(chronicle.SequenceEqual(game.Session.PersonalChronicle(game.Session.PlayerControl)),"年代記の復旧一致");Check(pose==game.Pose(),"復旧後の3D表示一致");
            File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始の設定部品・現地の依頼・本人承諾・点検・返報・報酬・保存復旧",Pose=pose,ComputerUse="未使用",History=start.Profile.Judgment!.History.ToString(),Attention=start.Profile.Judgment.Attention.ToString()}));
            GD.Print("SOCIAL_WORLD_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
