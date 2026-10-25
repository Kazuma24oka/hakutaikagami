using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    async Task RunTerrainPerformance()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);string label=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--perf-label="))[13..];
            var rows=new List<object>();string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());follow=false;
            for(int pass=0;pass<2;pass++)for(int i=0;i<NationalRoutes.Nodes.Length;i++){
                var node=NationalRoutes.Nodes[i];center=ContinuousWorld.Origin(node.Id);span=pass==0?1500:30000;yaw=20;
                int previousBuilds=midGround.Builds;var watch=System.Diagnostics.Stopwatch.StartNew();Refresh();watch.Stop();
                Check(midGround.Triangles==2048&&ground.Resident==0,"nationwide mid budget "+pass+"/"+node.Id);
                Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"nationwide camera preserves world "+pass+"/"+node.Id);
                var warm=System.Diagnostics.Stopwatch.StartNew();Refresh();warm.Stop();
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                rows.Add(new{Node=node.Id,Pass=pass,Span=span,RefreshMs=watch.Elapsed.TotalMilliseconds,WarmRefreshMs=warm.Elapsed.TotalMilliseconds,Triangles=midGround.Triangles,midGround.Builds,NewBuild=midGround.Builds>previousBuilds,midGround.VertexHeightMs,midGround.KindAtMs,midGround.MeshCommitMs,midGround.BuildMs,WorkingBytes=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,VideoBytes=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)});
                if(i==0||i==54||i==107)await Capture(label+"-"+pass+"-"+i);
                if(i%20==0)GD.Print("TERRAIN_PERF_PROGRESS "+pass+"/"+i);
            }
            System.IO.File.WriteAllText(Output+"/"+label+".json",System.Text.Json.JsonSerializer.Serialize(new{Rows=rows,Checks=checks,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("TERRAIN_PERF_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
