using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool ReviewNativeMovie=>OS.GetCmdlineUserArgs().Contains("--play-review-movie");
    bool ReviewReplay=>OS.GetCmdlineUserArgs().Contains("--new-game-restore");
    readonly Stopwatch reviewWatch=new();
    readonly List<object> reviewFrames=[];
    Label reviewCaption=null!;
    string reviewFilmDirectory="";
    async Task ReviewFrame()
    {
        await Frame();if(ReviewReplay)return;int index=reviewFrames.Count;string file=Path.Combine(reviewFilmDirectory,index.ToString("D5")+".png");if(!ReviewNativeMovie)ViewportCapture.Save(GetViewport(),file);reviewFrames.Add(new{Index=index,ProcessFrame=Engine.GetProcessFrames(),WallSeconds=reviewWatch.Elapsed.TotalSeconds,WorldSeconds=game!.Session.Seconds,Revision=game.Session.Revision,Paused=game.Session.Paused,Caption=reviewCaption.Text});
    }
    void ReviewSay(string text){reviewCaption.Text=(ReviewNativeMovie?"操作確認（固定30fps録画）：":"実動作記録：")+text;}
    async Task ReviewHold(double seconds){if(ReviewReplay)return;if(ReviewNativeMovie){for(int n=0;n<(int)Math.Ceiling(seconds*30);n++)await ReviewFrame();return;}double end=reviewWatch.Elapsed.TotalSeconds+seconds;while(reviewWatch.Elapsed.TotalSeconds<end)await ReviewFrame();}
    async Task ReviewWait(Func<bool> ready,string reason){var timer=Stopwatch.StartNew();while(!ready()&&timer.Elapsed.TotalSeconds<45)await ReviewFrame();Check(ready(),reason);}
    async Task ReviewClock(bool paused){game!.Press(paused?"pause":"auto");await ReviewWait(()=>!game.IsClockControlling,"recorded actual clock control completes");Check(game.LastClockError==null,"recorded clock control has no failure");}
    async Task ReviewAuto(int seconds,string id)
    {
        var records=new List<(long From,long Target)>();var saved=new List<(long From,long Target)>();string path=Path.Combine(TestOutput,"targets-"+id+".json");
        if(ReviewReplay){using var doc=JsonDocument.Parse(File.ReadAllText(path));foreach(var e in doc.RootElement.EnumerateArray())saved.Add((e.GetProperty("From").GetInt64(),e.GetProperty("Target").GetInt64()));Check(saved.Count>0,"actual public automatic inputs exist for replay");game!.AdvanceTargetTestHook=from=>records.Count<saved.Count?saved[records.Count].From==from?saved[records.Count].Target:throw new InvalidOperationException("review replay source mismatch"):checked(from+1);}
        game!.AdvancePublishedTestHook=(from,target,reached,processed,reason)=>records.Add((from,target));
        try{long target=game.Session.Seconds+seconds;await ReviewClock(false);await ReviewWait(()=>ReviewReplay?records.Count>=saved.Count:game.Session.Seconds>=target||!game.ReviewAutomatic,"actual automatic review reaches target or important arrival");await ReviewClock(true);await ReviewWait(()=>!game.AdvanceWorkerPending,"review canceled private future drained");Check(game.LastAdvanceError==null,"review automatic work has no failure");if(!ReviewReplay)File.WriteAllText(path,JsonSerializer.Serialize(records.Select(r=>new{r.From,r.Target})));else Check(records.SequenceEqual(saved),"same public automatic input sequence after process restart");}
        finally{game.AdvanceTargetTestHook=null;game.AdvancePublishedTestHook=null;}game.ReviewFraction(0);
    }
    async Task ReviewSave(){game!.Press("save");await ReviewWait(()=>!game.IsSaving,"recorded actual save completes");Check(game.LastSaveError==null,"recorded actual save succeeds");game.ReviewFraction(0);}
    async Task ReviewTail(){ReviewSay("同じ武将で道を進む（1倍）");await ReviewAuto(2,"tail");game!.ReviewFraction(0);}
    async Task RunPlayReviewChecks()
    {
        var panel=new PanelContainer{Position=new(18,860),Size=new(1244,32),ZIndex=100,MouseFilter=Control.MouseFilterEnum.Ignore};panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("15202a")});reviewCaption=new(){Text="実動作記録",AutowrapMode=TextServer.AutowrapMode.WordSmart};panel.AddChild(reviewCaption);AddChild(panel);
        if(ReviewReplay){
            Press("load-list");await Frame();string id=File.ReadAllText(Path.Combine(TestOutput,"campaign-instance.txt"));savedList.Select(Array.FindIndex(savedRows,r=>r.Profile.Instance==id));Press("load");await Frame();await Frame();game!.ReviewFraction(0);Check(game.Pose()==File.ReadAllText(Path.Combine(TestOutput,"s0-pose.json")),"recorded manual save restores exact selected-person3D");Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"s0-world.sha256")),"recorded manual save restores exact world");await Capture("restored-s0",true);await ReviewTail();await Capture("continued-s0",true);Check(ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())==File.ReadAllText(Path.Combine(TestOutput,"final-world.sha256")),"same recorded automatic inputs preserve full saved continuation");
        }else{
            await BeginPopulationEntry(0,"person:takeda-nobushige");reviewFilmDirectory=Path.Combine(TestOutput,"film",TestKey!);Directory.CreateDirectory(reviewFilmDirectory);reviewWatch.Start();ReviewSay("武田家の家臣として開始");await ReviewHold(1);
            ReviewSay("現地で仕事を相談する");game!.Press("work");{game.Press("work-reply");game.Press("work-confirm");game.Press("work-reply");}await ReviewWait(()=>!game.IsDeparting&&!game.DepartureWorkerPending,"recorded work request completes");Check(game.LastLocalIntentError==null&&game.LastLocalIntentResult==ResultCode.Accepted,"recorded work is accepted");await ReviewAuto(1,"letter");Children(game).OfType<TabContainer>().Single().CurrentTab=2;await Frame();Check(game.Session.GetActorView(game.Session.PlayerControl).Messages.Any(m=>m.KnownBody==null),"important physical letter arrives sealed and stops whole world");ReviewSay("書状が届くと停止。開いて内容を知る");await ReviewHold(1.2);game.Press("open");game.Press("work-close");await ReviewHold(.7);
            ReviewSay("自動進行を再開し、仕事と返報を進める（1倍）");await ReviewAuto(7,"work");Check(SelectedInspectionCompleted(),"recorded selected officer completes own work,not an NPC success");await ReviewHold(.7);
            ReviewSay("保存し、同じ人物・世界を読み直す");await ReviewSave();var before=ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());string pose=game.Pose();game.Press("load");await Frame();await Frame();Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&pose==game.Pose(),"actual manual save/load retains exact world and3D");await ReviewHold(.8);
            ReviewSay("行先を選んで出発する");game.SetDestination("castle");game.Press("depart");await ReviewWait(()=>!game.IsDeparting&&!game.DepartureWorkerPending,"recorded actual departure completes");Check(game.LastDepartureError==null&&game.LastDepartureResult==ResultCode.Accepted,"recorded actual departure accepted");await ReviewAuto(3,"travel");Check(game.Session.GetBranchView(game.Session.PlayerControl).Walking,"recorded selected officer actually walks the road");
            ReviewSay("全国表示でも同じ場所・同じ時刻");game.Press("nation");await ReviewHold(1);game.Press("near");game.Press("rotate");await ReviewSave();await Capture("s0",true);ReviewSay("同じ武将で道を進む（1倍）");await ReviewTail();await Capture("final",true);await ReviewHold(.5);reviewWatch.Stop();File.WriteAllText(Path.Combine(TestOutput,"film-manifest.json"),JsonSerializer.Serialize(new{Directory=Path.GetRelativePath(TestOutput,reviewFilmDirectory),NativeMovie=ReviewNativeMovie,FixedRecordingFps=ReviewNativeMovie?30:0,Frames=reviewFrames,DurationSeconds=reviewWatch.Elapsed.TotalSeconds,Scope="Actual Godot UI through real scripted control signals. Native movie mode uses engine fixed30fps simulation-time recording and disables real-time synchronization;not wall-clock fps/latency/nationalAI evidence. PNG mode is actual wall-time capture but adds overhead. No OS mouse tools. Latest saved continuation is replayed in separate process with exact actual public targets."}));
        }
        File.WriteAllText(Path.Combine(TestOutput,ReviewReplay?"restore-checks.json":"check-checks.json"),JsonSerializer.Serialize(assertions));GD.Print("PLAY_REVIEW_PASSED "+FlowCase);
    }
}
