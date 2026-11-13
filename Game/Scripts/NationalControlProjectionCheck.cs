using Godot;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HakutaiKagami.Core.Contracts;
public partial class NationalControlProjectionCheck:Control
{
    async System.Threading.Tasks.Task Capture(string file)
    {
        for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(DisplayServer.GetName()=="headless")throw new InvalidDataException("描画のある実行が必要です");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        if(GetViewport().GetTexture().GetImage().SavePng(file)!=Error.Ok)throw new IOException("地図描画の保存失敗");
    }
    public override async void _Ready()
    {
        try
        {
            string dir=ProjectSettings.GlobalizePath("res://../Docs/Design/NationalControlProjectionV1/runtime-local");
            var before=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(dir,"sealed.json")));
            var after=ContractJson.Read<ContractWorld>(File.ReadAllBytes(Path.Combine(dir,"opened.json")));
            var observer=new EntityId(File.ReadAllText(Path.Combine(dir,"observer.txt")).Trim());
            var initial=NationalControlProjection.For(before,observer)!;var opened=NationalControlProjection.For(after,observer)!;
            var a=NationalDistrictControlOverlay.Build(initial);var b=NationalDistrictControlOverlay.Build(opened);
            string hashA=Convert.ToHexString(SHA256.HashData(a.Texture.GetImage().GetData()));
            string hashB=Convert.ToHexString(SHA256.HashData(b.Texture.GetImage().GetData()));
            if(hashA==hashB)throw new InvalidDataException("開封後の勢力色が変わっていません");
            var map=new NewGameMap{Setup=before.NewGame!.Start,NationalMovement=true,ControlState=initial,ShowControl=true,ShowPins=false,ShowRoutes=false};
            AddChild(map);map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            await Capture(Path.Combine(dir,"sealed-map.png"));
            map.SetControlState(opened,NationalControlProjection.CastleOwnersFor(after,observer));
            await Capture(Path.Combine(dir,"opened-map.png"));
            File.WriteAllText(Path.Combine(dir,"ui-result.json"),System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Before=hashA,After=hashB,a.MappedPixels,Scope="開封前後の同じ全国地図・色テクスチャ・自前描画",ComputerUse="未使用"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("NATIONAL_CONTROL_PROJECTION_UI_PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
