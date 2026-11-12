using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class ContinuousWorldTrial
{
    void BuildDistrictControlSurface()
    {
        var state=S.DiagnosticSnapshot().DistrictControl;
        if(state is null)return;
        var mapImage=NationalDistrictControlOverlay.Build(state);
        districtControlPixels=mapImage.MappedPixels;
        var origin=FieldWorld.ToWorld(new(0,0));
        var west=mapImage.NorthWest;var east=mapImage.SouthEast;
        var nw=new Vector3((west.EastMm-origin.EastMm)/1000f,-14.5f,-(west.NorthMm-origin.NorthMm)/1000f);
        var ne=new Vector3((east.EastMm-origin.EastMm)/1000f,-14.5f,-(west.NorthMm-origin.NorthMm)/1000f);
        var se=new Vector3((east.EastMm-origin.EastMm)/1000f,-14.5f,-(east.NorthMm-origin.NorthMm)/1000f);
        var sw=new Vector3((west.EastMm-origin.EastMm)/1000f,-14.5f,-(east.NorthMm-origin.NorthMm)/1000f);
        var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=new[]{nw,ne,se,sw};
        arrays[(int)Mesh.ArrayType.TexUV]=new[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down};
        arrays[(int)Mesh.ArrayType.Index]=new[]{0,1,2,0,2,3};
        var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        var material=new StandardMaterial3D{AlbedoTexture=mapImage.Texture,AlbedoColor=new Color(1,1,1,.86f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest};
        districtControlSurface=new MeshInstance3D{Mesh=mesh,MaterialOverride=material,Visible=false};map.AddChild(districtControlSurface);
    }

    async Task RunNationalControlChecks()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var world=S.DiagnosticSnapshot();var state=world.DistrictControl;
            Check(NationalDistrictControl.Enabled(world)&&state is {Schema:1,Policy:NationalDistrictControl.Policy},"1560年の新規全国開始だけ郡支配の版を持つ");
            Check(state!.Districts.Length==HakutaiDb.Districts.Count&&ContractJson.CanonicalHash(state)==ContractJson.CanonicalHash(NationalDistrictControl.Initial()),"全郡の支配・並立・根拠が開始データと一致");
            Check(districtControlSurface!=null&&districtControlPixels>100000,"同じ郡支配データから3D全国表示を生成");
            var savedHash=ContractJson.CanonicalHash(world);var savedSeconds=S.Seconds;
            Click("nation");Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(districtControlSurface!.Visible&&map.Visible,"全国へ引くと郡ごとの勢力色が見える");
            await Capture("world-control");
            Check(savedHash==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"全国表示は支配・兵糧・世界時刻を変えない");
            Save("district-control");
            S.SetPaused(false);Advance(S.Seconds+1);
            Check(S.Seconds>savedSeconds,"保存後の世界時計が進む");
            Load("district-control");
            var restored=S.DiagnosticSnapshot();
            Check(ContractJson.CanonicalHash(restored)==savedHash,"実保存復旧後に1560年の全郡支配状態が一致");
            Check(districtControlSurface.Visible&&districtControlPixels>100000,"復旧後も全国地図の郡支配色が表示される");
            await Capture("world-control-restored");
            File.WriteAllText(Path.Combine(Output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Version=NationalDistrictControl.Edition,Districts=state.Districts.Length,MappedPixels=districtControlPixels,WorldHash=ContractJson.CanonicalHash(restored),Checks=checks,ComputerUse="未使用"}));
            GD.Print("NATIONAL_DISTRICT_CONTROL_3D_PASS "+checks.Count);
            GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
