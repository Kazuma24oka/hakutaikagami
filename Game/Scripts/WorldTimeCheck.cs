using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using HakutaiKagami.Core.Contracts;
public partial class WorldTimeCheck:Node
{
    NewGameRuntime game=null!;
    string output="";
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    async Task Until(Func<bool> done,string label){var timer=Stopwatch.StartNew();while(!done()){if(timer.Elapsed.TotalSeconds>600)throw new TimeoutException(label);await Frame();}GD.Print("時間操作確認: "+label);}
    static void Check(bool ok,string label){if(!ok)throw new InvalidDataException(label);}
    public override async void _Ready()
    {
        try{
            output=RecordPath.Globalize("res://../Docs/Design/WorldTimeV1/runtime-local/ui");Directory.CreateDirectory(output);
            var entry=new NewGameFrontEnd();var campaign=entry.CreateWorldTimeReview(Path.Combine(Path.GetTempPath(),"world-time-ui-"+Guid.NewGuid().ToString("N")));entry.Free();
            game=new();game.Configure(campaign.Store,campaign.Profile,false);AddChild(game);game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);await Frame();await Frame();
            Check(game.Session.DiagnosticSnapshot().NewGame!.NpcRoutinePolicy==NpcJudgment.Policy,"通常開始のNPC方針");Check(game.Session.DiagnosticSnapshot().TimeControl!=null,"通常開始の時間方針");
            game.ReviewSelectSpeed(86400);Check(game.ReviewSpeed==86400,"日の速度を選択");
            game.SetDestination("fork");game.Press("depart");await Until(()=>!game.IsDeparting&&!game.DepartureWorkerPending,"出発確定");Check(game.LastDepartureResult==ResultCode.Accepted,"出発受付");
            var timer=Stopwatch.StartNew();game.Press("auto");await Until(()=>!game.IsClockControlling,"自動進行開始");
            await Until(()=>game.Session.DiagnosticSnapshot().TimeControl!.Notices.Any(n=>n.Kind==TimeNoticeKind.Arrival),"通常開始から日の速度で到着");
            await Until(()=>!game.IsClockControlling&&game.ReviewSpeed==60,"到着後60倍へ減速");game.Press("pause");await Until(()=>!game.IsClockControlling&&!game.AdvanceWorkerPending,"確定位置で停止");
            Check(game.LastAdvanceError==null,"先読み失敗なし");game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"実保存");
            string pose=game.Pose();var bytes=ContractJson.Write(game.Session.DiagnosticSnapshot());game.Press("load");await Frame();game.ReviewFraction(0);
            Check(pose==game.Pose(),"保存復旧後の3D配置と表示速度一致");Check(bytes.SequenceEqual(ContractJson.Write(game.Session.DiagnosticSnapshot())),"保存世界の完全一致");
            long waitingFrom=game.Session.Seconds,publications=game.ManualAdvancePublications;
            game.SetDestination("castle");game.Press("depart");await Until(()=>!game.IsDeparting&&!game.DepartureWorkerPending,"帰路の出発確定");
            game.NextNoticeChunkSeconds=1000;game.Press("time-next");
            await Until(()=>!game.IsManualAdvancing&&!game.ManualAdvanceWorkerPending,"計算区切りを越えて次の到着通知まで継続");
            Check(game.Session.Seconds>waitingFrom+1000&&game.ManualAdvancePublications-publications>1,"区切りだけでは待機を終了しない");
            Check(WorldTimeControl.Pending(game.Session.DiagnosticSnapshot())?.Kind==TimeNoticeKind.Arrival,"実際の到着通知で終了");
            Check(game.Session.GetActorView(game.Session.PlayerControl).Location.Site==new EntityId("site:castle"),"帰路の実到着");
            game.NextNoticeChunkSeconds=3*WorldCalendar.SecondsPerDay;
            game.Press("time-settings");await Until(()=>!game.IsClockControlling,"時間設定の停止");
            var dialog=Officer3DCustomActor.Descendants(game).OfType<AcceptDialog>().Single(d=>d.Title=="自動進行の停止・減速");
            var option=Officer3DCustomActor.Descendants(dialog).OfType<OptionButton>().First();option.Select(0);option.EmitSignal(OptionButton.SignalName.ItemSelected,0L);dialog.Hide();
            Check(game.Session.DiagnosticSnapshot().TimeControl!.Rules.First().Response==TimeResponse.Off,"実設定操作");
            using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
                game.AdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("先読み検査の解除待ち");};
                try{game.ReviewSelectSpeed(43200);game.Press("auto");await Until(()=>entered.IsSet,"未確定の先読みを保持");
                    long at=game.Session.Seconds;game.ReviewSelectSpeed(259200);game.Press("pause");release.Set();await Until(()=>!game.IsClockControlling&&!game.AdvanceWorkerPending,"速度切替で古い先読みを破棄");Check(game.Session.Seconds==at,"未確定の未来を公開しない");
                }finally{release.Set();game.AdvanceTestFault=null;}
            }

            using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
                game.ManualAdvanceTestFault=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("出来事待ち検査の解除待ち");};
                try{game.Press("time-next");await Until(()=>entered.IsSet,"次の知らせ待ちの計算を保持");long at=game.Session.Seconds;game.Press("pause");release.Set();await Until(()=>!game.IsClockControlling&&!game.ManualAdvanceWorkerPending,"出来事待ちを取消");Check(game.Session.Seconds==at,"取消した出来事待ちの未来を公開しない");}
                finally{release.Set();game.ManualAdvanceTestFault=null;}
            }
            game.ReviewFraction(0);game.Press("save");await Until(()=>!game.IsSaving,"設定と日の速度を実保存");game.Press("load");await Frame();
            Check(game.ReviewSpeed==259200&&game.Session.DiagnosticSnapshot().TimeControl!.Rules.First().Response==TimeResponse.Off,"設定と日の速度の復旧");
            File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Scope="通常開始の実データ・ゲーム内部の出発/自動進行/保存/ロード操作",ElapsedMilliseconds=timer.ElapsedMilliseconds,game.Session.Seconds,Speed=game.ReviewSpeed,Pose=game.Pose(),ComputerUse="未使用"}));
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);Check(ViewportCapture.Save(GetViewport(),Path.Combine(output,"arrival.png"))==Error.Ok,"自前描画保存");}
            GD.Print("WORLD_TIME_UI_PASS");game.QueueFree();await Frame();GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
