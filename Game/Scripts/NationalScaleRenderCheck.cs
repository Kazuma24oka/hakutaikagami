using Godot;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    internal void ReviewConfigureScale(ContractWorld world)
    {
        var d=world.NewGame!;campaigns=new(Path.Combine(Path.GetTempPath(),"scale-render-"+Guid.NewGuid().ToString("N")));
        Profile=new(Guid.NewGuid().ToString("N"),d.Start,d.Registered,d.Selected.Value,d.Seed,ContractJson.CanonicalHash(world));
        host=new(new WorldSession(world));saves=new(Path.Combine(Path.GetTempPath(),"scale-render-save-"+Guid.NewGuid().ToString("N")),world.Definitions);loading=false;
    }
}
public partial class NationalScaleRenderCheck:Control
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override async void _Ready()
    {
        try
        {
            var input=System.Environment.GetEnvironmentVariable("HAKUTAI_SCALE_WORLD")??throw new InvalidDataException("検査用世界が未指定です");
            var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(input));int armies=NpcArmies.Armies(w).Length;
            if(w.Characters.Length<3000||w.Definitions.Sites.Length<1000||armies<500)throw new InvalidDataException("規模条件を満たしません");
            var runtime=new NewGameRuntime();runtime.ReviewConfigureScale(w);AddChild(runtime);
            var load=Stopwatch.StartNew();while(runtime.IsSaving){if(load.Elapsed.TotalSeconds>180)throw new TimeoutException("開始保存");await Frame();}
            var output=ProjectSettings.GlobalizePath("res://../Docs/Design/NationalWarfareV1/runtime-local/fps");Directory.CreateDirectory(output);
            var rows=new List<object>();
            foreach(var mode in new[]{"near","nation"})
            {
                runtime.Press(mode);for(int i=0;i<90;i++)await Frame();
                var frames=new List<double>();var sample=Stopwatch.StartNew();long previous=Stopwatch.GetTimestamp();
                for(int i=0;i<240||sample.Elapsed.TotalSeconds<3;i++){await Frame();long now=Stopwatch.GetTimestamp();frames.Add((now-previous)*1000.0/Stopwatch.Frequency);previous=now;}
                var ordered=frames.Order().ToArray();double seconds=frames.Sum()/1000.0;
                rows.Add(new{Mode=mode,Frames=frames.Count,Seconds=seconds,Fps=frames.Count/seconds,P95FrameMs=ordered[(int)(ordered.Length*.95)],Width=GetViewportRect().Size.X,Height=GetViewportRect().Size.Y});
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,mode+".png"))!=Error.Ok)throw new IOException("自前描画の保存に失敗しました");
            }
            var result=new{Status="PASSED",People=w.Characters.Length,Sites=w.Definitions.Sites.Length,Armies=armies,Rows=rows,Renderer=RenderingServer.GetVideoAdapterName(),Scope="旧軍事規則の検査用500家の保存をゲームへ読み込み、停止中の近景と全国地図の実機FPS。全員の同時歩行や交戦中のFPS、全国全機能の版でのFPSではない。",ComputerUse="未使用"};
            File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("NATIONAL_SCALE_RENDER_PASS "+System.Text.Json.JsonSerializer.Serialize(result));GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
