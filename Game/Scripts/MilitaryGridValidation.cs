using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Simulation;

public static class MilitaryGridValidation
{
    static MilitaryGridStage ui=null!;
    static readonly List<string> checks=new(),captures=new();
    static string Output=>ProjectSettings.GlobalizePath("res://../Art/Officer3D/MilitaryGridV1/runtime-local");
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks.Add(message);GD.Print("MILITARY_CHECK "+message);}
    static void Click(string id)=>ui.Actions[id].EmitSignal(Button.SignalName.Pressed);
    static void Observer(int index){ui.ObserverChoice.Select(index);ui.ObserverChoice.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);}
    static void Step(){Click("advance");ui.Synchronize();}
    static MilitaryState State=>ui.Session.Simulation.MilitaryDiagnostic!;
    static MilitaryUnit Unit(string id="unit-red")=>State.Units.Single(u=>u.Id==id);
    static async Task Frames(){await ui.ToSignal(ui.GetTree(),SceneTree.SignalName.ProcessFrame);await ui.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    static async Task Capture(string name){await Frames();Check(ui.GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png")==Error.Ok,"capture "+name);captures.Add(name+".png");}
    static string Render()=>JsonSerializer.Serialize(new{ui.Observer,view=ui.LastView,actors=ui.Actors.Where(p=>p.Value.Visible).ToDictionary(p=>p.Key,p=>new{x=p.Value.Position.X,z=p.Value.Position.Z,yaw=p.Value.Rotation.Y,clip=p.Value.Player.CurrentAnimation.ToString(),time=p.Value.Player.CurrentAnimationPosition,look=p.Value.Save()}),ghosts=ui.Ghosts.Where(p=>p.Value.Visible).ToDictionary(p=>p.Key,p=>new{x=p.Value.Position.X,z=p.Value.Position.Z})});
    public static async Task Run(MilitaryGridStage stage,bool restore)
    {
        ui=stage;
        try
        {
            System.IO.Directory.CreateDirectory(Output);DisplayServer.WindowSetSize(new(1600,1000));ui.Session.SetProcess(false);ui.SetProcess(false);
            Click("fixture-toggle");
            if(restore)
            {
                Click("load");ui.Synchronize();Check(ui.Session.Simulation.SaveJson()==FileAccess.GetFileAsString("user://military-grid-expected.json"),"separate-process normal save restores army grants orders messages intel path ledger RNG and clock");
                Check(Render()==FileAccess.GetFileAsString("user://military-grid-render-expected.json"),"separate-process filtered rendering restores exact visible pose and memory");await Capture("restart");GD.Print("MILITARY_RESTORE_COMPLETE");ui.GetTree().Quit();return;
            }
            Click("start");Check(ui.LastView!.Units.All(u=>u.Own)&&!ui.Actors["unit-blue"].Visible&&!ui.Ghosts["unit-blue"].Visible,"unknown enemy has no model label or ghost");await Capture("initial-red-unseen");
            Observer(2);Check(ui.LastView!.Units.Length==0&&ui.Actors.Values.All(a=>!a.Visible),"third officer initially knows no battlefield units");await Capture("initial-third-unseen");Observer(0);
            var roster=new Officer3DCustomRoster{Officers=new(){new("import-test","赤具足の取込",Officer3DCustomActor.Preset("red_guard"))}};
            ui.CreatorImportPath=ProjectSettings.GlobalizePath("user://military-grid-import-test.json");System.IO.File.WriteAllText(ui.CreatorImportPath,JsonSerializer.Serialize(roster));Click("import");Click("apply-import");Observer(1);Click("look-veteran");Observer(0);
            Check(ui.Session.Simulation.AppearanceFor("character-001").Equipment["torso"]=="red_lamellar","creator appearance assignment retained in current normal scene");
            Click("attack");string id=State.Orders.Single().Id;string mid=MilitaryFlow.OrderMessage(id);Click("fail");Step();
            Check(Unit().Cell==new MapCell(1,2)&&!ui.LastView!.Orders.Single().RetryAvailable,"lost military order does not move unit or instantly inform issuer");await Capture("lost-unrecognized");
            string before=ui.Session.Simulation.SaveJson();Click("retry");Check(ui.Session.Simulation.SaveJson()==before,"GUI rejects retry before delivered failure report");
            Click("failure-report");Check(!ui.LastView!.Orders.Single().RetryAvailable,"in-flight failure report grants no knowledge");Step();Check(ui.LastView!.Orders.Single().RetryAvailable,"failure report reception enables retry decision");await Capture("failure-known");
            Click("retry");Click("late");Click("opponent");Click("submit");Step();
            Check(State.Applied.Count(x=>x==id)==1&&ui.Session.Simulation.FormalReceipts.Count(r=>r.MessageId==mid)==1,"late original and retry share one receipt and effect");
            await Capture("red-marching");
            while(!ui.LastView!.Units.Any(u=>!u.Own&&u.Current)&&ui.Session.Simulation.Tick<12){Step();await Capture("approach-"+ui.Session.Simulation.Tick);}
            Check(ui.LastView!.Units.Any(u=>!u.Own&&u.Current&&u.Soldiers==60),"enemy appears only on direct sight near bridge");await Capture("first-sighting");
            long observed=ui.Session.Simulation.Tick;Click("intel");Observer(2);Check(ui.LastView!.Units.Length==0,"report dispatch does not instantly share intelligence");Observer(0);
            // Save a moving, partly interpolated frame while the report is still in transit.
            Click("speed4");ui.Session.Automatic=true;ui.Session.AdvanceDisplayClock(.1875);Click("pause");ui.Synchronize();string render=Render(),paused=ui.Session.Simulation.SaveJson();
            for(int i=0;i<8;i++)ui.Session.AdvanceDisplayClock(.25);ui.Synchronize();Check(Render()==render&&ui.Session.Simulation.SaveJson()==paused,"pause freezes common clock routing deliveries intelligence and animation");
            Click("save");string checkpoint=ui.Session.Simulation.SaveJson();Click("look-commander");Click("load");ui.Synchronize();Check(ui.Session.Simulation.SaveJson()==checkpoint&&Render()==render,"normal save restores appearance route progress report-in-flight and fractional pose");await Capture("paused-moving-restored");
            string savePath=ProjectSettings.GlobalizePath("user://military-grid-test.json");System.IO.File.WriteAllText(savePath,checkpoint.Replace("river-crossing-v1","invalid-map"));Click("load");Check(ui.Session.Simulation.SaveJson()==checkpoint,"unknown map in normal save rejects entire load");System.IO.File.WriteAllText(savePath,checkpoint);
            var mirror=TaxSimulation.LoadJson(checkpoint);mirror.SetPaused(false);Click("pause");ui.Session.Automatic=false;
            for(int i=0;i<3;i++){Step();mirror.AdvanceOneTick();Check(ui.Session.Simulation.SaveJson()==mirror.SaveJson(),"post-load common-clock continuation matches "+i);await Capture("contact-and-fight-"+i);}
            Observer(2);var report=ui.LastView!.Units.Single(u=>u.UnitId=="unit-blue");Check(!report.Current&&report.ObservedAt==observed&&report.Soldiers==60&&Unit("unit-blue").Soldiers==36,"delayed report contains observed old values rather than live enemy state");
            Check(!ui.Actors["unit-blue"].Visible&&ui.Ghosts["unit-blue"].Visible,"reported enemy uses dated memory marker rather than live model");await Capture("report-received-stale");
            Observer(0);while(ui.Session.Simulation.Tick<12)Step();Check(ui.LastView!.Units.Single(u=>u.UnitId=="unit-blue").Present==false,"clear sight of former position records absence without revealing new position");await Capture("red-lost-sight");Click("intel");
            Observer(2);await Capture("third-still-old-report");while(ui.Session.Simulation.Tick<14)Step();Check(Unit("unit-blue").Phase==MilitaryPhase.Exited&&ui.Ghosts["unit-blue"].Visible,"unseen enemy exit does not automatically erase another officer's old report");await Capture("exit-not-globally-known");Step();
            Check(!ui.Ghosts["unit-blue"].Visible&&ui.LastView!.Units.Single(u=>u.UnitId=="unit-blue").Present==false,"delivered absence report updates only recipient knowledge");await Capture("absence-report-received");
            Check(ui.Session.Simulation.Orders.Single().Reported&&ui.Session.Simulation.TreasuryRice==160,"tax collection and result report progress during military battle");
            Observer(0);ui.DestinationChoice.Select(4);Click("move");Step();Step();Check(Unit().Cell.X==6,"weighted movement crosses the bridge instead of river");await Capture("bridge-crossing");Step();Step();Click("withdraw");Step();Step();Step();
            Check(Unit().Phase==MilitaryPhase.Withdrawing&&Unit().Route.Contains(new MapCell(0,3)),"withdrawal route leads to own army exit");await Capture("withdraw-over-bridge");
            ui.Session.Automatic=true;ui.Session.AdvanceDisplayClock(.1875);Click("pause");ui.Session.Automatic=false;ui.Synchronize();Click("save");string saved=ui.Session.Simulation.SaveJson(),savedRender=Render();
            using(var f=FileAccess.Open("user://military-grid-expected.json",FileAccess.ModeFlags.Write))f.StoreString(saved);
            using(var f=FileAccess.Open("user://military-grid-render-expected.json",FileAccess.ModeFlags.Write))f.StoreString(savedRender);
            // Independent UI cancellation/deadline scenarios leave the saved route checkpoint intact.
            Click("reset");Click("start");Click("attack");Click("cancel");Step();var position=Unit().Cell;await Capture("cancel-awaiting-effect");Step();Check(Unit().Cell==position&&State.Orders.First().Status==MilitaryOrderStatus.Cancelled,"GUI cancellation stops future movement at specified effect time");await Capture("cancel-effective");
            Click("reset");Click("start");Click("deadline");Step();Step();Check(Unit().Cell==new MapCell(1,2)&&State.Orders.Single().Status==MilitaryOrderStatus.Expired,"GUI military deadline expires without executing late order");await Capture("deadline-expired");
            Click("load");ui.Synchronize();Check(ui.Session.Simulation.SaveJson()==saved&&Render()==savedRender,"normal checkpoint survives later fixture runs");await Capture("review");
            System.IO.File.WriteAllText(Output+"/results.json",JsonSerializer.Serialize(new{checks,captures,hardware=RenderingServer.GetVideoAdapterName(),world_tick=ui.Session.Simulation.Tick,report_observed_at=observed,measurement="Actual normal Main + shared TaxPanel clock / MessageFlow / filtered military view; dedicated test saves; no native input"},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("MILITARY_TEST_COMPLETE");ui.GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());ui.GetTree().Quit(1);}
    }
}
