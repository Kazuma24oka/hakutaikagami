using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class HistoricalChronicleCheck:Node
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Until(Func<bool> done,string label){var timer=Stopwatch.StartNew();while(!done()){if(timer.Elapsed.TotalSeconds>300)throw new TimeoutException(label);await Frame();}}
    static void Check(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    public override async void _Ready()
    {
        try{
            bool owari=System.Environment.GetEnvironmentVariable("HAKUTAI_OWARI_CHECK")=="1";
            string output=ProjectSettings.GlobalizePath(owari?"res://../Docs/Design/OwariHistoricalOpportunityV1/runtime-local/ui":"res://../Docs/Design/HistoricalChronicleV1/runtime-local/ui");Directory.CreateDirectory(output);
            var entry=new NewGameFrontEnd();AddChild(entry);await Frame();await Frame();
            var start=entry.CreateJudgmentReview(Path.Combine(Path.GetTempPath(),"npc-judgment-ui-"+Guid.NewGuid().ToString("N")),owari);
            if(owari)Check(start.Profile.Judgment?.EventEdition==HistoricalEventRules.OwariEdition,"史実条件の新規開始");
            else Check(start.Profile.Judgment==new NpcJudgmentSettings(NpcJudgment.Policy,HistoryMode.Fictional,NpcAttention.Beginner){UseInspectionReferences=true,Challenge=NpcChallenge.Standard},"開始画面の選択と保存定義");
            await Frame();if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"settings.png"))==Error.Ok,"設定画面の自前描画");}
            entry.QueueFree();await Frame();var game=new NewGameRuntime();game.Configure(start.Store,start.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Frame();
            await Until(()=>!game.IsSaving,"初期保存");game.Advance(10);
            var world=game.Session.DiagnosticSnapshot();var task=world.Tasks.Single(t=>t.Executor==world.PlayerControl&&t.Issuer!=t.Executor);
            if(owari)Check(HistoricalEventRules.IsOwariOrder(world,task.RequestId),"信長の史実条件付き偵察判断から依頼");
            Check(task.Agreement==AgreementPhase.AwaitingRecognition,"本人の開封を待つ");
            var message=world.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="contract-order");
            game.SelectUnreadMail(new(message.Id));game.Press("open");await Frame();game.Press("work-reply");await Frame();
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"offer.png"))==Error.Ok,"会話と返答の自前描画");}
            if(owari){
                Check(game.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.IsVisibleInTree() && l.Text.Contains("清洲を出る道")),"開封後に清洲の依頼を会話に表示");
            }
            game.Press("work-assent");game.Press("work-reply");await Frame();
            Check(game.Session.DiagnosticSnapshot().Tasks.Single(t=>t.Id==task.Id).Agreement==AgreementPhase.Active,"会話からの本人承諾");
            if(owari){
                game.Press("work-close");game.ReviewFraction(0);
                game.Press("save");await Until(()=>!game.IsSaving,"偵察途中の保存");
                var inProgress=ContractJson.Write(game.Session.DiagnosticSnapshot());var position=game.Pose();
                game.Press("load");await Frame();game.ReviewFraction(0);
                Check(inProgress.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())) && game.Pose()==position,"偵察の途中復旧と位置");
            }
            game.Advance(30);world=game.Session.DiagnosticSnapshot();var reply=world.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="assessment-notice");
            game.SelectUnreadMail(new(reply.Id));game.Press("open");await Frame();game.Press("work-reply");game.Press("work-pay");game.Press("work-reply");await Frame();
            Check(NewGameWork.Paid(game.Session.DiagnosticSnapshot(),task.Id),"既存会話から実在銭の受渡し");
            game.Press("work-close");var chronicle=game.Session.PersonalChronicle(game.Session.PlayerControl);Check(chronicle.Length>0,"開封後の年代記");game.Press("chronicle");await Frame();await Frame();if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"chronicle.png"))==Error.Ok,"年代記の自前描画");}await Frame();await Frame();game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"実保存");var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());string pose=game.Pose();
            game.Press("load");await Frame();game.ReviewFraction(0);Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"復旧後の世界一致");File.WriteAllText(Path.Combine(output,"before-pose.json"),pose);File.WriteAllText(Path.Combine(output,"restored-pose.json"),game.Pose());Check(chronicle.SequenceEqual(game.Session.PersonalChronicle(game.Session.PlayerControl)),"年代記の復旧一致");Check(pose==game.Pose(),"復旧後の3D表示一致");
            File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始の設定部品・現地の依頼・本人承諾・点検・返報・報酬・保存復旧",Pose=pose,ComputerUse="未使用",History=start.Profile.Judgment!.History.ToString(),Attention=start.Profile.Judgment.Attention.ToString()}));
            GD.Print(owari?"OWARI_SCOUTING_UI_PASS":"HISTORICAL_CHRONICLE_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
