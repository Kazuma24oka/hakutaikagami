using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class SceneryAssetLifetimeChecks : Node3D
{
    async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    public override async void _Ready(){try{
        string mode=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--lifetime="))?[11..]??"baseline";string dir=ProjectSettings.GlobalizePath("res://../Docs/Design/FieldSupplyV1/runtime-local/lifetime");Directory.CreateDirectory(dir);
        var camera=new Camera3D{Position=new(100,120,100),Current=true,Far=5000};AddChild(camera);camera.LookAt(Vector3.Zero);AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-30,0),ShadowEnabled=false});
        var renderer=new ContinuousTerrainRenderer();AddChild(renderer);int packed=0;var centers=new[]{"castle","kofu","odawara","castle"};
        for(int n=0;n<12;n++){foreach(var node in centers){renderer.Update(ContinuousWorld.Point(node,0,0),200);if(renderer.Resident!=25||renderer.Notices.Length!=0)throw new Exception("Ground assets missing or fallback active");packed+=renderer.PackedAssetCount;GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Frame();}}
        if(packed<=0)throw new Exception("No actual packed assets exercised");if(mode.StartsWith("pinned")&&(SceneryVisualAssets.RetainedResourceCount<=0||SceneryVisualAssets.RetainedResourceCount>new SceneryVisualAssets().ResourceLimit))throw new Exception("Retained resources exceed finite catalog");GetViewport().GetTexture().GetImage().SavePng(dir+"/"+mode+".png");
        File.WriteAllText(dir+"/"+mode+".json",System.Text.Json.JsonSerializer.Serialize(new{Updates=48,CreatedChunks=renderer.Created,EvictedChunks=renderer.Evicted,PackedInstances=packed,Resident=renderer.Resident,RetainedResources=SceneryVisualAssets.RetainedResourceCount,ResourceLimit=new SceneryVisualAssets().ResourceLimit,Notices=renderer.Notices}));GD.Print("ASSET_LIFETIME_PASSED");GetTree().Quit();
    }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
