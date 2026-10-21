using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
public partial class ContinuousWorldTrial
{
    void PanCamera(Vector2 relative)
    {
        if(surface.Size.X<=0||surface.Size.Y<=0)return;
        var pixel=new Vector2(viewport.Size.X,viewport.Size.Y)/2;var delta=relative*new Vector2(viewport.Size.X/surface.Size.X,viewport.Size.Y/surface.Size.Y);float height=ContinuousWorld.Height(center)/1000f;
        Vector3 Ground(Vector2 at){var origin=camera.ProjectRayOrigin(at);var direction=camera.ProjectRayNormal(at);if(Math.Abs(direction.Y)<.001f)throw new Exception("Camera cannot pan parallel to ground");return origin+direction*((height-origin.Y)/direction.Y);}
        var shift=Ground(pixel+delta)-Ground(pixel);follow=false;center=new(center.X-(long)Math.Round(shift.X*1000),center.Y+(long)Math.Round(shift.Z*1000));Refresh();
    }
    async Task RunCameraContinuityChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);float[] scales={239,241,29900,30100,499000,501000,2800000};var measurements=new System.Collections.Generic.List<object>();
            if(Mode.EndsWith("restore")){
                for(int i=0;i<scales.Length;i++){Load("camera-"+i);Check(Pose()==System.IO.File.ReadAllText(Output+"/camera-"+i+"-pose.json"),"same saved camera and world "+i);await Capture("restored-"+i);}
            }else{
                string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());follow=false;center=ContinuousWorld.Origin(Node);
                bool pan=OS.GetCmdlineUserArgs().Contains("--camera-pan");
                if(pan)foreach(float scale in new[]{200f,600f,30000f,2800000f})foreach(float angle in new[]{0f,90f,180f,270f}){
                    span=scale;yaw=angle;center=ContinuousWorld.Origin(Node);Refresh();var reference=center;var first=camera.UnprojectPosition(P(reference));var drag=new Vector2(40,-20);var expected=drag*new Vector2(viewport.Size.X/surface.Size.X,viewport.Size.Y/surface.Size.Y);PanCamera(drag);var error=(camera.UnprojectPosition(P(reference))-first-expected).Length();Check(error<1,"pan follows actual projection "+scale+"/"+angle+" error="+error);Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"pan cannot mutate world");
                }
                var panned=center;
                foreach(var pair in new[]{(239f,241f),(29900f,30100f),(499000f,501000f)}){
                    yaw=20;span=pair.Item1;Refresh();var a=camera.UnprojectPosition(new(-10,0,0));var b=camera.UnprojectPosition(new(10,0,0));float first=a.DistanceTo(b);
                    span=pair.Item2;Refresh();a=camera.UnprojectPosition(new(-10,0,0));b=camera.UnprojectPosition(new(10,0,0));float second=a.DistanceTo(b);
                    measurements.Add(new{From=pair.Item1,To=pair.Item2,FirstPixels=first,SecondPixels=second,NormalizedChange=Math.Abs(second/first*pair.Item2/pair.Item1-1)});Check(Math.Abs(second/first*pair.Item2/pair.Item1-1)<.02f,"boundary projection changes only with zoom "+pair);
                }
                for(int i=0;i<scales.Length;i++){
                    span=scales[i];yaw=i==6?20:20+i*30;center=i==6?new(0,0):pan?panned:ContinuousWorld.Origin(Node);Refresh();
                    Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"camera leaves world unchanged "+i);
                    if(i==6){var bounds=new Rect2(Vector2.Zero,new Vector2(viewport.Size.X,viewport.Size.Y));Check(shores.SelectMany(s=>s.Ring).All(g=>{var p=JapanGeography.Project(g);return bounds.HasPoint(camera.UnprojectPosition(Flat(new(p.EastMm,p.NorthMm))));}),"all coastline fits national camera");}
                    Save("camera-"+i);System.IO.File.WriteAllText(Output+"/camera-"+i+"-pose.json",Pose());await Capture("stage-"+i);
                }
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,Measurements=measurements,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("CAMERA_CONTINUITY_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
