using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;
// 遊ぶ画面の重さの測り：全国の開始で、自動進行（1倍・60倍）の間の1フレームの時間を測る。
public partial class PerfProbe:Control
{
    public override async void _Ready()
    {
        var root=RecordPath.Globalize("res://../Docs/Design/ScreensV1/runtime-local");
        try
        {
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(root,"ready.json")));
            if(System.Environment.GetEnvironmentVariable("HAKUTAI_PLAY_VALIDATION")=="0")WorldSession.ValidateTransitions=false;GD.Print("PERF validate="+WorldSession.ValidateTransitions);var runtime=new NewGameRuntime();runtime.ReviewConfigureScreens(w);AddChild(runtime);
            for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            string Measure(string name,double seconds)
            {
                return name;
            }
            var sw=System.Diagnostics.Stopwatch.StartNew();int frames=0;
            while(sw.Elapsed.TotalSeconds<5){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);frames++;}
            GD.Print($"PERF idle fps={frames/5.0:F1}");
            GD.Print("PERF parts "+runtime.ReviewRefreshCost());GD.Print("PERF parts2 "+runtime.ReviewRefreshCost());var t=System.Diagnostics.Stopwatch.StartNew();
            t.Restart();var snap=runtime.Session.DiagnosticSnapshot();GD.Print($"PERF snapshot {t.Elapsed.TotalMilliseconds:F0}ms");
            {var s=runtime.Session;foreach(var step in new[]{1,1,10,60,600}){var tt=System.Diagnostics.Stopwatch.StartNew();s.SetPaused(false);s.AdvanceTo(s.Seconds+step,100000);s.SetPaused(true);GD.Print($"PERF advance +{step}s {tt.Elapsed.TotalMilliseconds:F0}ms");}}
            foreach(var speed in new[]{1,60})
            {
                runtime.ReviewSelectSpeed(speed);runtime.Press("auto");var prep=System.Diagnostics.Stopwatch.StartNew();while(runtime.IsClockControlling&&prep.Elapsed.TotalSeconds<60)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);GD.Print($"PERF prepare {prep.Elapsed.TotalSeconds:F1}s paused={runtime.Session.Paused} err={runtime.LastClockError}");sw.Restart();frames=0;long s0=runtime.Session.Seconds;var worst=0.0;var last=sw.Elapsed.TotalMilliseconds;
                while(sw.Elapsed.TotalSeconds<10){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);frames++;var now=sw.Elapsed.TotalMilliseconds;worst=Math.Max(worst,now-last);last=now;}
                GD.Print($"PERF auto x{speed} fps={frames/10.0:F1} worst={worst:F0}ms game={runtime.Session.Seconds-s0}s");
                runtime.Press("pause");while(runtime.IsClockControlling)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            GetTree().Quit(0);
        }
        catch(Exception e){GD.PrintErr(e.ToString());GetTree().Quit(1);}
    }
}
public partial class NewGameRuntime
{
    internal string ReviewRefreshCost()
    {
        var sb=new System.Text.StringBuilder();var t=new System.Diagnostics.Stopwatch();
        void M(string n,Action a){cachedSession=null;t.Restart();try{a();}catch(Exception e){sb.Append(n+"!"+e.GetType().Name+" ");return;}sb.Append($"{n}={t.Elapsed.TotalMilliseconds:F0} ");}
        void K(string n,Action a){t.Restart();try{a();}catch(Exception e){sb.Append(n+"!"+e.GetType().Name+" ");return;}sb.Append($"{n}={t.Elapsed.TotalMilliseconds:F0} ");}
        M("snapshot",()=>Snapshot());
        K("checkSucc",()=>CheckSuccession(Snapshot()));K("worldTime",RefreshWorldTime);
        K("sights",()=>NpcArmies.Sights(Snapshot(),Session.PlayerControl));K("nearby",()=>NearbyDisplay());
        K("branches",()=>{foreach(var p in NearbyDisplay())Branch(p.Actor);});
        K("military",()=>RenderMilitary(Snapshot(),new FieldPoint(0,0)));K("bandits",()=>RenderBandits(Snapshot(),new FieldPoint(0,0)));K("dots",()=>SyncSoldierDots(Snapshot()));
        K("service",()=>ServiceDisplay());K("mail",RefreshMail);K("refMil",RefreshMilitary);K("auth",RefreshAuthorityUI);K("target",RefreshKnownTarget);K("refService",RefreshService);
        K("labels",ResolveWorldLabels);K("work",RefreshWorkExperience);
        var w0=Snapshot();K("w.standdown",()=>PrivateStanddown.Enabled(w0));K("w.defense",()=>TreatyDefense.View(w0));K("w.supply",()=>TreatySupply.View(w0));K("w.contact",()=>SelectWorkContact());K("w.dutyCand",()=>WorkDuty.Candidate(w0));K("w.dutyActive",()=>WorkDuty.Active(w0));
        K("w.known",()=>KnownWork());K("w.failed",()=>KnownFailedWorkLetters());K("w.notice",()=>AssessmentNotice.LatestKnown(Session,Session.PlayerControl));K("w.milwork",RefreshMilitaryWork);K("w.supplywork",RefreshSupplyWork);K("w.transport",RefreshTransportWork);K("w.voyage",RefreshVoyageTravel);
        K("v.castellan",()=>Session.CastellanView());K("v.castle",()=>Session.CastleView());K("v.succ",()=>HouseSuccession.ViewForProjection(Snapshot()));
        K("castles",()=>RefreshCastleMarkers(Snapshot(),new FieldPoint(0,0)));K("castellan",()=>RefreshCastellanMarkers(Snapshot(),new FieldPoint(0,0)));K("succMarkers",()=>RefreshHouseSuccessionMarkers(Snapshot(),new FieldPoint(0,0)));
        K("looks",()=>{foreach(var p in NearbyDisplay())DotOfficerLooks.For(Snapshot(),p.Actor,false);});
        K("whole",Refresh);
        return sb.ToString();
    }
}
