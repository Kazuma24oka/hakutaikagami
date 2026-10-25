using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class ContinuousWorldTrial
{
    string FieldModelPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=Pose(),Packed=ground.PackedAssetCount,Notices=ground.Notices});
    async Task RunFieldModelChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);var areas=new[]{new FieldPoint(100000,50000),new FieldPoint(250000,220000),new FieldPoint(-125000,-125000)};
            if(Mode.EndsWith("restore")){
                for(int i=0;i<3;i++){Load("field-"+i);Check(FieldModelPose()==System.IO.File.ReadAllText(Output+"/field-"+i+"-pose.json"),"same world and packed model restore "+i);await Capture("restored-"+i);}
            }else{
                follow=false;span=160;yaw=20;string before=ContractJson.CanonicalHash(S.DiagnosticSnapshot());
                for(int i=0;i<3;i++){
                    center=ContinuousWorld.Point(Node,areas[i].X,areas[i].Y);ground.SetSkin("skin:swap-demo");Refresh();var tiles=ground.TileIds;await Capture("old-"+i);
                    ground.SetSkin("skin:baseline");Refresh();Check(ground.PackedAssetCount>0&&ground.Notices.Length==0,"actual static models pass footprint/clearance validation "+i);Check(tiles.SequenceEqual(ground.TileIds),"appearance keeps authoritative tile IDs "+i);Check(ContractJson.CanonicalHash(S.DiagnosticSnapshot())==before,"models cannot mutate terrain or movement "+i);
                    Save("field-"+i);System.IO.File.WriteAllText(Output+"/field-"+i+"-pose.json",FieldModelPose());await Capture("stage-"+i);
                    ground.SetSkin("skin:swap-demo");Refresh();ground.SetSkin("skin:baseline");Refresh();await Capture("back-"+i);
                }
                var test=new SceneryVisualAssets();var visual=test.Catalog.Visuals.Single(v=>v.Id=="visual:field-pine");var placement=new SceneryPlacement("test:tree","test:tree","tree",SceneryAppearance.Appearance("tree"),new(0,0),new(-500,-500),new(500,500),1000,1000,10000,0,"variant:a",1);
                Check(test.Instantiate(visual with{Variants=new[]{new ScenerySceneVariant("variant:a","res://Assets/Scenery/Models/field-missing-test.tscn")}},placement)==null&&test.Notices.Count>0,"missing model requests existing procedural fallback");
                test.Notices.Clear();Check(test.Instantiate(visual with{AuthoringWidthMm=1},placement)==null&&test.Notices.Count>0,"unsafe footprint cannot replace movement truth");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("FIELD_MODELS_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
