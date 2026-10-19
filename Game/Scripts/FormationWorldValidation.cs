using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using HakutaiKagami.Core.Simulation;
public static class FormationWorldValidation
{
    static FormationWorldStage ui=null!;static readonly List<string> checks=new(),captures=new();
    static string Output=>ProjectSettings.GlobalizePath("res://../Art/Officer3D/FormationWorldV1/runtime-local");
    static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);checks.Add(reason);GD.Print("FORMATION_CHECK "+reason);}
    static void Click(string id)=>ui.Actions[id].EmitSignal(Button.SignalName.Pressed);
    static void Step(){Click("advance");ui.Synchronize();}
    static MilitaryState State=>ui.Session.Simulation.MilitaryDiagnostic!;
    static async Task Frames(){await ui.ToSignal(ui.GetTree(),SceneTree.SignalName.ProcessFrame);await ui.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    static async Task Capture(string id){await Frames();Check(ui.GetViewport().GetTexture().GetImage().SavePng(Output+"/"+id+".png")==Error.Ok,"capture "+id);captures.Add(id+".png");}
    static string Render()=>JsonSerializer.Serialize(new{ui.Observer,view=ui.LastView,actors=ui.Actors.Where(a=>a.Value.Visible).ToDictionary(a=>a.Key,a=>new{x=a.Value.Position.X,z=a.Value.Position.Z,clip=a.Value.Player.CurrentAnimation.ToString(),t=a.Value.Player.CurrentAnimationPosition}),markers=ui.Markers.Where(a=>a.Value.Visible).ToDictionary(a=>a.Key,a=>new{x=a.Value.Position.X,z=a.Value.Position.Z})});
    public static async Task Run(FormationWorldStage stage,bool restore)
    {
        ui=stage;
        try
        {
            System.IO.Directory.CreateDirectory(Output);DisplayServer.WindowSetSize(new(1600,1000));ui.Session.SetProcess(false);ui.SetProcess(false);await Frames();ui.SetZoom(14,true);
            if(restore){Click("load");ui.Synchronize();Check(ui.Session.Simulation.SaveJson()==FileAccess.GetFileAsString("user://formation-world-expected.json"),"separate-process shared save restores plan routes deliveries reports and clock");Check(Render()==FileAccess.GetFileAsString("user://formation-world-render.json"),"separate-process restores same filtered 3D positions and clips");await Capture("restart");GD.Print("FORMATION_RESTORE_COMPLETE");ui.GetTree().Quit();return;}
            Click("start");Check(ui.Session.Simulation.HasFormationMap&&State.Units.Length==3,"three field commanders and one army share ordinary TaxPanel session");ui.SetZoom(180,true);ui.Synchronize();await Capture("nation-before");
            var node=ui.View.GetChild(0);var signature=ui.Session.Simulation.SaveJson();ui.SetZoom(40,true);ui.Synchronize();await Capture("region-before");ui.SetZoom(14,true);ui.Synchronize();await Capture("near-before");
            Check(ui.View.GetChild(0)==node&&ui.Session.Simulation.SaveJson()==signature,"zoom keeps same scene world coordinates tick RNG and shared simulation");
            foreach(var cell in new[]{new MapCell(8,1),new(8,5),new(5,3)})Check(ui.GroundCell(ui.SurfacePoint(cell))==cell,"same camera ground projection roundtrip "+cell);
            Click("draw");var a=ui.SurfacePoint(new(8,1));var b=ui.SurfacePoint(new(8,5));
            ui.Surface.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=a});
            ui.Surface.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=b});
            Check(ui.DraftStart==new MapCell(8,1)&&ui.DraftEnd==new MapCell(8,5),"actual map drag handler sets line endpoints without native input");await Capture("line-draft");
            Check(State.Orders.Length==0,"drawing alone sends no command and moves no unit");Click("send");Check(State.Orders.Length==3&&State.Applied.Length==0,"one line dispatches three immutable shared-transport orders without teleporting");await Capture("sent-not-arrived");
            Step();Check(State.Applied.Length==1&&State.Units[1].Cell==new MapCell(1,3)&&State.Units[2].Cell==new MapCell(1,5),"tick1 only first recipient starts");await Capture("arrival-1");
            Step();Step();Check(State.Applied.Length==2&&State.Units[2].Cell==new MapCell(1,5),"tick3 central recipient starts while right waits");await Capture("arrival-3");
            Step();Step();Check(State.Applied.Length==3,"tick5 all three have personally received orders");await Capture("arrival-5");
            ui.Session.Automatic=true;ui.Session.AdvanceDisplayClock(.1875);Click("pause");ui.Synchronize();Click("save");var save=ui.Session.Simulation.SaveJson();string pose=Render();for(int i=0;i<5;i++)ui.Session.AdvanceDisplayClock(.25);Check(pose==Render()&&save==ui.Session.Simulation.SaveJson(),"pause freezes delivery motion and animation phase");
            Click("load");ui.Synchronize();Check(ui.Session.Simulation.SaveJson()==save&&Render()==pose,"ordinary save/load preserves in-flight plans partial motion and filtered render");await Capture("paused-restored");
            FileAccess.Open("user://formation-world-expected.json",FileAccess.ModeFlags.Write)!.Dispose();System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://formation-world-expected.json"),save);System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://formation-world-render.json"),pose);
            var mirror=TaxSimulation.LoadJson(save);mirror.SetPaused(false);Click("pause");ui.Session.Automatic=false;
            for(int i=0;i<5;i++){Step();mirror.AdvanceOneTick();Check(ui.Session.Simulation.SaveJson()==mirror.SaveJson(),"save continuation deterministic "+i);}await Capture("bridge-queue");
            for(int i=0;i<45&&State.FormationArrivals.Length<3;i++){Step();Check(State.Units.Select(u=>u.Cell).Distinct().Count()==3,"traffic no overlap tick"+State.Tick);if(i is 3 or 8 or 13)await Capture("deployment-"+i);}
            Check(State.FormationArrivals.Length==3&&State.Units.All(u=>u.Cell.X==8&&u.Route.Length==0),"all three units reach distinct assigned slots across bridge");Step();Step();
            Check(ui.LastView!.Units.Count(u=>u.Current)==3&&ui.Actors.Values.All(x=>x.Visible),"commander directly sees all three finished formations");await Capture("deployed-near");
            var final=ui.Session.Simulation.SaveJson();ui.SetZoom(40,true);ui.Synchronize();await Capture("deployed-region");ui.SetZoom(180,true);ui.Synchronize();await Capture("deployed-nation");Check(ui.Session.Simulation.SaveJson()==final&&ui.View.GetChild(0)==node,"zoomed-out deployment uses identical units and world scene");
            ui.SetZoom(14);for(int i=0;i<12;i++){ui._Process(.05);await Capture("zoom-"+i.ToString("00"));}
            Check(ui.Session.Simulation.SaveJson()==final&&ui.View.GetChild(0)==node&&ui.Camera.Size<16,"continuous camera transition leaves all world state untouched");
            // Preserve the moving checkpoint for an independent restart, not just this process's final state.
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://formation-world-test.json"),save);
            System.IO.File.WriteAllText(Output+"/results.json",JsonSerializer.Serialize(new{checks,captures},new JsonSerializerOptions{WriteIndented=true}));GD.Print("FORMATION_TEST_COMPLETE");ui.GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());ui.GetTree().Quit(1);}
    }
}
