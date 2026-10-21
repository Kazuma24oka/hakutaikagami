using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class ContinuousWorldTrial
{
    readonly Dictionary<EntityId,MeshInstance3D> forceMarkers=new();
    void RefreshForceMarkers(ContractWorld w)
    {
        foreach(var marker in forceMarkers.Values)marker.Visible=false;
        if(!w.Region!.Active||span<=240||span>30000)return;
        foreach(var v in RegionalBattle.View(w,ContractFixture.Officer).Where(v=>v.Soldiers>0)){
            var unit=w.Region.Units.Single(u=>u.Id==v.Unit);
            if(!forceMarkers.TryGetValue(v.Unit,out var marker)){
                marker=new(){Mesh=new SphereMesh{Radius=1,Height=2,RadialSegments=8,Rings=4},MaterialOverride=new StandardMaterial3D{ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=true,RenderPriority=3}};world.AddChild(marker);forceMarkers[v.Unit]=marker;
            }
            marker.Visible=true;float size=5*camera.Size/viewport.Size.X;marker.Scale=Vector3.One*size;
            marker.Position=P(v.Position)+Vector3.Up*size;
            ((StandardMaterial3D)marker.MaterialOverride).AlbedoColor=new Color(!v.Live?"999999":unit.Side==1?"d47768":unit.Commander==ContractFixture.Officer?"e3c66f":"719fc3");
        }
    }
    void DeclutterForceLabels()
    {
        if(span<=240||span>5000)return;var occupied=siteLabelRects.ToList();var bounds=new Rect2(Vector2.Zero,new Vector2(viewport.Size.X,viewport.Size.Y));
        foreach(var pair in labels.OrderBy(p=>p.Key.Value,StringComparer.Ordinal)){
            var label=pair.Value;if(!label.Visible)continue;label.Font=Theme.DefaultFont;
            var pixels=label.PixelSize*viewport.Size.X/camera.Size;var size=label.Font.GetStringSize(label.Text,fontSize:24)*pixels+new Vector2(12,12);var rect=new Rect2(camera.UnprojectPosition(label.GlobalPosition)-size/2,size);
            if(!bounds.Encloses(rect)||occupied.Any(r=>r.Intersects(rect)))label.Visible=false;else occupied.Add(rect);
        }
    }
    string ForceMarkerPose()=>System.Text.Json.JsonSerializer.Serialize(new{Scene=Pose(),Known=RegionalBattle.View(S.DiagnosticSnapshot(),ContractFixture.Officer),Markers=forceMarkers.Where(p=>p.Value.Visible).OrderBy(p=>p.Key.Value,StringComparer.Ordinal).Select(p=>new{p.Key,p.Value.Position,p.Value.Scale,Color=((StandardMaterial3D)p.Value.MaterialOverride).AlbedoColor})});
    void ValidateForceMarkers()
    {
        var w=S.DiagnosticSnapshot();var views=RegionalBattle.View(w,ContractFixture.Officer).Where(v=>v.Soldiers>0).ToArray();
        var visible=forceMarkers.Where(p=>p.Value.Visible).ToArray();
        Check(visible.Select(p=>p.Key).ToHashSet().SetEquals(span>240&&span<=30000?views.Select(v=>v.Unit):Array.Empty<EntityId>()),"only observed forces receive mid markers");
        foreach(var p in visible){var v=views.Single(v=>v.Unit==p.Key);var expected=P(v.Position)+Vector3.Up*p.Value.Scale.X;Check(p.Value.Position.DistanceTo(expected)<.002f,"marker position uses observed location "+p.Key.Value);if(!v.Live)Check(((StandardMaterial3D)p.Value.MaterialOverride).AlbedoColor==new Color("999999"),"stale observation is gray "+p.Key.Value);}
    }
    async Task RunForceLodChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode.EndsWith("restore")){
                for(int i=0;i<4;i++){Load("force-"+i);ValidateForceMarkers();Check(ForceMarkerPose()==System.IO.File.ReadAllText(Output+"/force-"+i+"-pose.json"),"known forces and symbols restore "+i);await Capture("restored-"+i);}
            }else{
                Click("move");long[] at={0,20,60,120};
                for(int i=0;i<4;i++){
                    Advance(at[i]);span=600;Refresh();ValidateForceMarkers();string display=ForceMarkerPose();var decoy=S.DiagnosticSnapshot();decoy.Region=decoy.Region! with{Units=decoy.Region.Units.Select(u=>u.Commander==ContractFixture.Officer?u:u with{Position=new(u.Position.X+10000000,u.Position.Y),Goal=new(u.Goal.X+10000000,u.Goal.Y),Soldiers=0}).ToArray()};RefreshForceMarkers(decoy);Check(display==ForceMarkerPose(),"unobserved true positions and losses cannot change symbols");var hidden=S.DiagnosticSnapshot();var removed=RegionalBattle.View(hidden,ContractFixture.Officer).First().Unit;hidden.Region=hidden.Region! with{Sights=hidden.Region.Sights.Where(s=>s.Observer!=ContractFixture.Officer||s.Unit!=removed).ToArray()};RefreshForceMarkers(hidden);Check(!forceMarkers[removed].Visible,"true unit without an observation receives no symbol");Refresh();string hash=ContractJson.CanonicalHash(S.DiagnosticSnapshot());
                    span=200;Refresh();Check(forceMarkers.Values.All(m=>!m.Visible),"detail hides strategic symbols");span=600;Refresh();Check(hash==ContractJson.CanonicalHash(S.DiagnosticSnapshot()),"LOD cannot mutate battle");
                    Save("force-"+i);System.IO.File.WriteAllText(Output+"/force-"+i+"-pose.json",ForceMarkerPose());await Capture("stage-"+i);
                }
                Check(RegionalBattle.View(S.DiagnosticSnapshot(),ContractFixture.Officer).Any(v=>!v.Live),"moving away retains an old observation for gray symbol test");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode.EndsWith("restore")?"restore":"check")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{surface.GlobalPosition.X,surface.GlobalPosition.Y,surface.Size.X,surface.Size.Y}}));GD.Print("FORCE_LOD_PASSED");GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
