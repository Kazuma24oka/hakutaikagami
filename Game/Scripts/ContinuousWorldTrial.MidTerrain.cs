using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    async Task RunMidTerrainChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            float[] scales={200,250,600,1500,8000,30000,2800000};
            if(Mode.EndsWith("restore")){
                for(int i=0;i<scales.Length;i++){
                    Load("mid-"+i);Check(Pose()==System.IO.File.ReadAllText(Output+"/mid-"+i+"-pose.json"),"camera/world/model restore "+i);
                    await Capture("restored-"+i);
                }
            }else{
                string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());
                for(int i=0;i<scales.Length;i++){
                    span=scales[i];yaw=20+i*30;follow=false;center=ContinuousWorld.Point(Node,65000+i*20000,-5000+i*15000);Refresh();
                    Check(midGround.Triangles==(span>240&&span<=30000?2048:0),"fixed mid geometry budget "+i);
                    Check(ground.Resident==(span<=240?25:0),"exclusive detailed tile budget "+i);
                    int builds=midGround.Builds;Refresh();Check(midGround.Builds==builds,"unchanged camera does not rebuild "+i);
                    Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"camera/LOD cannot mutate world "+i);
                    Save("mid-"+i);System.IO.File.WriteAllText(Output+"/mid-"+i+"-pose.json",Pose());await Capture("stage-"+i);
                }
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,midGround.Builds,midGround.Triangles,midGround.Step,RefreshMs=timings.ToArray(),ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));
            GD.Print("MID_TERRAIN_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
