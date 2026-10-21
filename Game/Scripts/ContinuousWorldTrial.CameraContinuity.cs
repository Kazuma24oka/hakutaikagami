using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
public partial class ContinuousWorldTrial
{
    async Task RunCameraContinuityChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);float[] scales={239,241,29900,30100,499000,501000,2800000};var measurements=new System.Collections.Generic.List<object>();
            if(Mode.EndsWith("restore")){
                for(int i=0;i<scales.Length;i++){Load("camera-"+i);Check(Pose()==System.IO.File.ReadAllText(Output+"/camera-"+i+"-pose.json"),"same saved camera and world "+i);await Capture("restored-"+i);}
            }else{
                string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());follow=false;center=ContinuousWorld.Origin(Node);
                foreach(var pair in new[]{(239f,241f),(29900f,30100f),(499000f,501000f)}){
                    yaw=20;span=pair.Item1;Refresh();var a=camera.UnprojectPosition(new(-10,0,0));var b=camera.UnprojectPosition(new(10,0,0));float first=a.DistanceTo(b);
                    span=pair.Item2;Refresh();a=camera.UnprojectPosition(new(-10,0,0));b=camera.UnprojectPosition(new(10,0,0));float second=a.DistanceTo(b);
                    measurements.Add(new{From=pair.Item1,To=pair.Item2,FirstPixels=first,SecondPixels=second,NormalizedChange=Math.Abs(second/first*pair.Item2/pair.Item1-1)});Check(Math.Abs(second/first*pair.Item2/pair.Item1-1)<.02f,"boundary projection changes only with zoom "+pair);
                }
                for(int i=0;i<scales.Length;i++){
                    span=scales[i];yaw=i==6?20:20+i*30;center=i==6?new(0,0):ContinuousWorld.Origin(Node);Refresh();
                    Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"camera leaves world unchanged "+i);
                    if(i==6){var bounds=new Rect2(Vector2.Zero,new Vector2(viewport.Size.X,viewport.Size.Y));Check(shores.SelectMany(s=>s.Ring).All(g=>{var p=JapanGeography.Project(g);return bounds.HasPoint(camera.UnprojectPosition(Flat(new(p.EastMm,p.NorthMm))));}),"all coastline fits national camera");}
                    Save("camera-"+i);System.IO.File.WriteAllText(Output+"/camera-"+i+"-pose.json",Pose());await Capture("stage-"+i);
                }
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,Measurements=measurements,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("CAMERA_CONTINUITY_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
