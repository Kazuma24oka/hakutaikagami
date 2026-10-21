using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    readonly List<Rect2> siteLabelRects=new();
    void DeclutterSiteLabels()
    {
        siteLabelRects.Clear();if(!map.Visible)return;
        var ns=NationalRoutes.Nodes;var bounds=new Rect2(Vector2.Zero,new Vector2(viewport.Size.X,viewport.Size.Y));
        foreach(int i in Enumerable.Range(0,ns.Length).OrderBy(i=>markers[i].Position.LengthSquared()).ThenBy(i=>ns[i].Id,StringComparer.Ordinal)){
            var label=siteLabels[i];label.Visible=false;if(span>=150000&&i%5!=0)continue;
            if(camera.IsPositionBehind(label.GlobalPosition))continue;
            var at=camera.UnprojectPosition(label.GlobalPosition);float pixels=label.PixelSize*viewport.Size.Y/span;
            var size=label.Font.GetStringSize(label.Text,fontSize:22)*pixels+new Vector2(12,12);
            var rect=new Rect2(at-size/2,size);if(!bounds.Encloses(rect)||siteLabelRects.Any(r=>r.Intersects(rect)))continue;
            label.Visible=true;siteLabelRects.Add(rect);
        }
    }
    async Task RunLodStyleChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);float[] scales={200,600,1500,30000};
            if(Mode.EndsWith("restore")){
                for(int n=0;n<scales.Length;n++){Load("style-"+n);Check(Pose()==System.IO.File.ReadAllText(Output+"/style-"+n+"-pose.json"),"appearance and camera restore "+n);await Capture("restored-"+n);}
            }else{
                for(int n=0;n<scales.Length;n++){
                    span=scales[n];follow=false;center=ContinuousWorld.Point(Node,100000,20000);ground.SetSkin("skin:baseline");Refresh();string hash=ContractJson.CanonicalHash(S.DiagnosticSnapshot());await Capture("base-"+n);
                    if(span>240){Check(siteLabelRects.Count>0,"readable site name exists "+n);Check(siteLabelRects.SelectMany((r,i)=>siteLabelRects.Skip(i+1).Select(q=>r.Intersects(q))).All(x=>!x),"projected site names never overlap "+n);}
                    Click("skin");Check(hash==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"appearance cannot mutate world "+n);Save("style-"+n);System.IO.File.WriteAllText(Output+"/style-"+n+"-pose.json",Pose());await Capture("swap-"+n);
                    Click("skin");await Capture("back-"+n);Check(hash==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"returning appearance preserves world "+n);
                }
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("LOD_STYLE_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
