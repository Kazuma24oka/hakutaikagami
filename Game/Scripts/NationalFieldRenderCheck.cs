using Godot;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    internal object ReviewNationalFieldProjection()
    {
        var w=Snapshot();if(!WorldGround.National(w)||!terrain.NationalGround)throw new InvalidDataException("全国の地形の版が描画へ届いていません");
        var center=FieldWorld.FromWorld(RenderPosition(Branch(Session.PlayerControl)));
        var actor=actors[Session.PlayerControl];float expected=WorldGround.Height(w,center)/1000f;
        if(Math.Abs(actor.Position.Y-expected)>.003f)throw new InvalidDataException("本人の模型と移動用地形の高さが違います");
        int vertices=0;
        foreach(var chunk in terrain.GetChildren().OfType<Node3D>())foreach(var model in chunk.GetChildren().OfType<MeshInstance3D>().Where(m=>m.HasMeta("surface_kind")))foreach(var vertex in model.Mesh.GetFaces())
        {
            var p=model.GlobalTransform*vertex;var point=new FieldPoint(center.X+(long)Math.Round(p.X*1000),center.Y-(long)Math.Round(p.Z*1000));
            if(Math.Abs(p.Y-WorldGround.Height(w,point)/1000f)>.004f)throw new InvalidDataException("実際の地形メッシュと移動用地形の高さが違います");vertices++;
        }
        if(vertices==0||terrain.Picks.Count==0)throw new InvalidDataException("実際の地形がありません");
        return new{Vertices=vertices,PickTriangles=terrain.Picks.Count,ActorHeight=actor.Position.Y,ExpectedHeight=expected,Geometry=terrain.GeometryFingerprint(),People=w.Characters.Length,Sites=w.Definitions.Sites.Length,Clock=w.ElapsedSeconds};
    }
}
public partial class NationalFieldRenderCheck:Control
{
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override async void _Ready()
    {
        string output=System.Environment.GetEnvironmentVariable("HAKUTAI_NATIONAL_FIELD_QA_DIR")??ProjectSettings.GlobalizePath("res://../Docs/Design/NationalWarfareV1/runtime-local/native-field");
        try
        {
            var world=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(output,"world.json")));
            var runtime=new NewGameRuntime();runtime.ReviewConfigureScale(world);AddChild(runtime);
            var watch=Stopwatch.StartNew();while(runtime.IsSaving){if(watch.Elapsed.TotalSeconds>180)throw new TimeoutException("開始保存");await Frame();}
            runtime.Press("near");for(int i=0;i<30;i++)await Frame();runtime.ReviewFraction(0);
            var projection=runtime.ReviewNationalFieldProjection();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output,"view.png"))!=Error.Ok)throw new IOException("自前描画を保存できません");
            File.WriteAllText(Path.Combine(output,"pose.json"),runtime.Pose());
            var result=new{Status="PASSED",Projection=projection,Scope="全国の配置途中の保存を実際のゲームへ読み込み、模型・実メッシュ・選択用三角形と移動用地形を照合",ComputerUse="未使用"};
            File.WriteAllText(Path.Combine(output,"render-result.json"),System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("NATIONAL_FIELD_RENDER_PASS");GetTree().Quit();
        }
        catch(Exception e){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"render-failure.txt"),e.ToString());GD.PrintErr(e);GetTree().Quit(1);}
    }
}
