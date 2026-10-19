using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.Common;

public static class OfficerWorldValidation
{
    static OfficerWorldStage ui=null!;
    static readonly List<string> captures=new();
    static readonly List<string> checks=new();
    static string Output=>ProjectSettings.GlobalizePath("res://../Art/Officer3D/WorldIntegrationV1/runtime-local");
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add(text);GD.Print("OFFICER_WORLD_CHECK "+text);}
    static async Task Frames(){await ui.ToSignal(ui.GetTree(),SceneTree.SignalName.ProcessFrame);await ui.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    static async Task Capture(string name){await Frames();var image=ui.GetViewport().GetTexture().GetImage();Check(image.SavePng(Output+"/"+name+".png")==Error.Ok,"capture "+name);captures.Add(name+".png");}
    static void Click(string key)=>ui.Actions[key].EmitSignal(Button.SignalName.Pressed);
    static void Select(int i){ui.Selection.Select(i);ui.Selection.EmitSignal(OptionButton.SignalName.ItemSelected,(long)i);}
    static string RenderState()=>JsonSerializer.Serialize(ui.Actors.ToDictionary(p=>p.Key,p=>new{visible=p.Value.Visible,x=p.Value.Position.X,z=p.Value.Position.Z,yaw=p.Value.Rotation.Y,clip=p.Value.Player.CurrentAnimation.ToString(),time=p.Value.Player.CurrentAnimationPosition,look=p.Value.Save()}));
    public static async Task Run(OfficerWorldStage stage,bool restore)
    {
        ui=stage;
        try
        {
            System.IO.Directory.CreateDirectory(Output);DisplayServer.WindowSetSize(new(1440,960));ui.Session.SetProcess(false);ui.SetProcess(false);
            if(restore)
            {
                Click("load");ui.Synchronize();
                Check(ui.Session.Simulation.SaveJson()==FileAccess.GetFileAsString("user://officer-world-expected.json"),"separate process world / appearances / battle / ledger / RNG restored");
                Check(RenderState()==FileAccess.GetFileAsString("user://officer-world-render-expected.json"),"separate process position / phase / appearance restored");
                await Capture("restart");GD.Print("OFFICER_WORLD_RESTORE_COMPLETE");ui.GetTree().Quit();return;
            }
            Check(ui.Session.Simulation.OfficerState!.Looks.Count==3,"three real officer IDs bound to appearances");
            Select(0);Click("look-red_guard");Select(1);Click("look-veteran");
            var roster=new Officer3DCustomRoster{Officers=new(){new("created-reference","作成外見の取込試験",Officer3DCustomActor.Preset("red_guard"))}};
            string importPath=ProjectSettings.GlobalizePath("user://officer-world-import-test.json");System.IO.File.WriteAllText(importPath,JsonSerializer.Serialize(roster));
            Select(2);ui.ImportCreatorFile(importPath);Click("apply-import");Check(ui.Session.Simulation.OfficerState!.Looks["character-003"].Equipment["torso"]=="red_lamellar","creator roster assigns appearance to selected existing officer only");
            Check(ui.Session.Simulation.RestoreWorld().Characters.Count==3,"import does not create political identities or authority");
            Select(0);ui.Synchronize();await Capture("officer-bindings");
            // Same scheduler is driven explicitly to test speed without native input or wall-clock races.
            ui.Session.Automatic=true;
            foreach(int speed in new[]{1,2,4}){Click("speed"+speed);long before=ui.Session.Simulation.Tick;for(int i=0;i<6;i++)ui.Session.AdvanceDisplayClock(.25);Check(ui.Session.Simulation.Tick-before==speed,"common clock speed "+speed);}
            ui.Session.Automatic=false;Click("start");Click("engage");Click("submit");
            var phases=new HashSet<FieldPhase>();long start=ui.Session.Simulation.Tick;
            for(int i=0;i<7;i++)
            {
                Click("advance");ui.Synchronize();var b=ui.Session.Simulation.OfficerState!.Battle!;foreach(var u in b.Units)phases.Add(u.Phase);
                Check(b.Tick==ui.Session.Simulation.Tick,"battle and tax share tick "+i);
                await Capture("battle-"+i.ToString("D2")+"-"+b.Units[1].Phase);
                if(i==0)Check(b.Directives.All(d=>d.Delivered)&&b.Losses.Length==0,"field directives arrive before fighting");
                if(i==0)
                {
                    Click("speed1");ui.Session.Automatic=true;long tick=ui.Session.Simulation.Tick;float x=ui.Actors["character-001"].Position.X;
                    for(int frame=0;frame<6;frame++){ui.Session.AdvanceDisplayClock(.125);ui.Synchronize();await Capture("walk-progress-"+frame);}
                    Check(ui.Session.Simulation.Tick==tick&&ui.Actors["character-001"].Position.X>x+.4f,"walk interpolates committed distance without advancing world early");
                    var actor=ui.Actors["character-001"];double expected=.5/actor.WalkStride*actor.Player.GetAnimation(actor.Clips["walk"]).Length;
                    Check(Math.Abs(actor.Player.CurrentAnimationPosition-expected%actor.Player.GetAnimation(actor.Clips["walk"]).Length)<.0001,"walk phase tracks source stride and actual distance");
                    ui.Session.Automatic=false;Click("speed4");
                }
                if(b.Units.All(u=>u.Phase==FieldPhase.Contact))Check(b.Losses.Length==0,"contact is distinct from damage");
            }
            Check(phases.Contains(FieldPhase.Marching)&&phases.Contains(FieldPhase.Contact)&&phases.Contains(FieldPhase.Engaged),"movement contact engagement rendered");
            Check(ui.Session.Simulation.Orders.Single().Reported&&ui.Session.Simulation.TreasuryRice==160,"tax delivery collection report continue during battle");
            // Save during combat, between ticks, paused. Normal save owns appearance+simulation together.
            Click("auto");ui.Session.AdvanceDisplayClock(.1875);Click("pause");ui.Synchronize();string screen=RenderState();
            string paused=ui.Session.Simulation.SaveJson();for(int i=0;i<8;i++)ui.Session.AdvanceDisplayClock(.25);ui.Synchronize();await Frames();
            Check(ui.Session.Simulation.SaveJson()==paused&&RenderState()==screen,"pause freezes world delivery battle transform and animation");
            Click("save");string saved=ui.Session.Simulation.SaveJson();ui.Assign(Officer3DCustomActor.Preset("commander"));Click("load");ui.Synchronize();
            Check(ui.Session.Simulation.SaveJson()==saved&&RenderState()==screen,"normal save restores colors parts battle pose and fractional clock");
            await Capture("paused-combat-restored");
            // A bad normal save must not replace any of the current session.
            string path=ProjectSettings.GlobalizePath("user://officer-world-test.json");System.IO.File.WriteAllText(path,saved.Replace("mature_v1","unknown-face"));Click("load");
            Check(ui.Session.Simulation.SaveJson()==saved,"invalid appearance in normal save rejected atomically");System.IO.File.WriteAllText(path,saved);
            Click("pause");ui.Session.Automatic=false;
            for(int i=0;i<12;i++){Click("advance");ui.Synchronize();var b=ui.Session.Simulation.OfficerState!.Battle!;foreach(var u in b.Units)phases.Add(u.Phase);await Capture("retreat-"+i.ToString("D2")+"-"+b.Units[1].Phase);}
            var end=ui.Session.Simulation.OfficerState!.Battle!;
            Check(phases.Contains(FieldPhase.Routing)&&phases.Contains(FieldPhase.Exited),"routed force travels to exit instead of disappearing at defeat");
            Check(end.Units.Length==2&&end.Units[1].Soldiers==24&&!ui.Actors[end.Units[1].CommanderId].Visible,"exited force retained with surviving soldiers");
            Check(ui.Session.Simulation.RestoreWorld().Characters.Count==3,"routing does not kill or change officer allegiance");
            Check(Math.Abs(ui.Actors["character-001"].WalkStride-ui.Actors["character-002"].WalkStride)<.001,"shared source walk stride across appearances");
            Select(0);Click("withdraw");Click("advance");ui.Synchronize();
            Check(ui.Session.Simulation.OfficerState!.Battle!.Units[0].Phase==FieldPhase.Withdrawing,"explicit withdrawal takes effect only after directive arrival");phases.Add(FieldPhase.Withdrawing);await Capture("ordered-withdrawal");
            // Restart proof captures a nonzero motion fraction, not just an integer checkpoint.
            ui.Session.Automatic=true;ui.Session.AdvanceDisplayClock(.1875);Click("pause");ui.Session.Automatic=false;ui.Synchronize();Click("save");
            using(var f=FileAccess.Open("user://officer-world-expected.json",FileAccess.ModeFlags.Write))f.StoreString(ui.Session.Simulation.SaveJson());
            using(var f=FileAccess.Open("user://officer-world-render-expected.json",FileAccess.ModeFlags.Write))f.StoreString(RenderState());
            await Capture("review");
            System.IO.File.WriteAllText(Output+"/results.json",JsonSerializer.Serialize(new{checks,captures,phases=phases.Select(p=>p.ToString()).Order().ToArray(),end_tick=ui.Session.Simulation.Tick,source_stride=ui.Actors["character-001"].WalkStride,hardware=RenderingServer.GetVideoAdapterName(),measurement="Actual normal Main + TaxPanel + OfficerWorldStage; dedicated test saves; internal signals without native input"},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("OFFICER_WORLD_TEST_COMPLETE");ui.GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());ui.GetTree().Quit(1);}
    }
}
