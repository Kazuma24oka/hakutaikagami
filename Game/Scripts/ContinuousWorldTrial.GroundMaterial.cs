using Godot;
using System;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    void CheckRenderedGround()
    {
        using var frame=viewport.GetTexture().GetImage();var point=camera.UnprojectPosition(P(center));Check(new Rect2(Vector2.Zero,new(viewport.Size.X,viewport.Size.Y)).HasPoint(point),"ground probe is inside actual viewport");var color=frame.GetPixel((int)point.X,(int)point.Y);var background=new Color("364f5c");Check(Math.Abs(color.R-background.R)+Math.Abs(color.G-background.G)+Math.Abs(color.B-background.B)>.08,"ground material actually draws at the projected world point");
    }
    async Task RunGroundMaterialChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);int samples=0;var points=new[]{ContinuousWorld.Point("castle",79000,-5000),ContinuousWorld.Point("castle",81000,-5000),ContinuousWorld.Point("sannohe",79000,-5000),ContinuousWorld.Point("funai",-81000,-5000)};
            if(Mode.EndsWith("restore")){
                for(int i=0;i<points.Length;i++){Load("ground-"+i);Check(Pose()==System.IO.File.ReadAllText(Output+"/ground-"+i+"-pose.json"),"restored texture/world/camera "+i);samples+=ground.ValidateMaterialUvs(center);await Capture("restored-"+i);CheckRenderedGround();}
            }else{
                follow=false;span=160;yaw=20;string hash=ContractJson.CanonicalHash(S.DiagnosticSnapshot());
                var texture=GD.Load<StandardMaterial3D>("res://Assets/Scenery/Materials/field-earth-material.tres").AlbedoTexture;using var image=texture.GetImage();Check(image.GetWidth()==128&&image.GetHeight()==128&&image.HasMipmaps(),"authored grain includes deterministic mipmaps");
                for(int x=0;x<128;x+=8)for(int y=0;y<128;y+=8){var c=image.GetPixel(x,y);Check(c.R==c.G&&c.G==c.B,"texture cannot add a separate color palette");}
                for(int i=0;i<points.Length;i++){
                    center=points[i];Refresh();Check(ground.RoadTexturePreserved,"road texture identity preserved "+i);samples+=ground.ValidateMaterialUvs(center);Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==hash,"material/UV cannot mutate movement "+i);
                    Save("ground-"+i);System.IO.File.WriteAllText(Output+"/ground-"+i+"-pose.json",Pose());await Capture("stage-"+i);CheckRenderedGround();
                }
                center=points[0];Refresh();samples+=ground.ValidateMaterialUvs(center);await Capture("back-0");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,UvSamples=samples,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("GROUND_MATERIAL_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
