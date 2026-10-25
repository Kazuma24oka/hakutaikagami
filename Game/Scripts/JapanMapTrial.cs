using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

// Public static geography only: no access to hidden military positions or assignment of legacy sites.
public partial class JapanMapTrial : Control
{
    JapanCatalog catalog=null!;readonly WorldSession session=new(ContractFixture.Create());
    Node3D world=null!;Camera3D camera=null!;SubViewport viewport=null!;Label status=null!,details=null!;
    OptionButton choice=null!;Officer3DCustomActor ruler=null!;
    readonly List<(MeshInstance3D Mesh,GeoCoordinate[] Ring,int[] Indices,float Y)> shores=new();
    readonly List<(Node3D Node,GeoLandmark Site)> markers=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    GeoMillimeters origin=new(0,0);int selected;float span=2800000,yaw=0,tilt=60;bool testing;
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/JapanMapV1/runtime-local");
    public override void _Ready()
    {
        testing=OS.GetCmdlineUserArgs().Contains("--japan-check");
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        selected=Array.FindIndex(catalog.Landmarks,l=>l.Id=="geo:Q1357421");
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 全国 → 地域 → 現地 — 同じ場所を拡大する"});
        col.AddChild(new Label{Text="現代の概略海岸線と城・館の位置候補。1560年の城域・所属・街道は未確定。",Modulate=new Color("e4bd7c")});
        var bar=new HFlowContainer();col.AddChild(bar);
        Add(bar,"nation","全国",()=>SetSpan(2800000));Add(bar,"region","地域 120km",()=>SetSpan(120000));
        Add(bar,"area","周辺 4km",()=>SetSpan(4000));Add(bar,"local","現地 80m",()=>SetSpan(80));Add(bar,"near","比較 12m",()=>SetSpan(12));
        Add(bar,"left","左へ回転",()=>{yaw-=30;Refresh();});Add(bar,"right","右へ回転",()=>{yaw+=30;Refresh();});
        Add(bar,"tilt","傾き変更",()=>{tilt=tilt==60?40:60;Refresh();});
        Add(bar,"time","世界時間 +60秒",()=>{session.AdvanceTo(session.Seconds+60);Refresh();});
        choice=new OptionButton();foreach(var l in catalog.Landmarks)choice.AddItem(l.Name);bar.AddChild(choice);
        choice.ItemSelected+=i=>Focus((int)i);choice.Select(selected);
        status=new Label();col.AddChild(status);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)SetSpan(Mathf.Max(12,span/1.5f));if(m.ButtonIndex==MouseButton.WheelDown)SetSpan(Mathf.Min(3200000,span*1.5f));}};
        world=new Node3D();viewport.AddChild(world);
        world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});
        world.AddChild(new DirectionalLight3D{RotationDegrees=new(-60,-30,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in catalog.Landmarks){var n=new Node3D();world.AddChild(n);
            var pin=new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)};n.AddChild(pin);markers.Add((n,site));}
        ruler=new Officer3DCustomActor();world.AddChild(ruler);ruler.Apply(Officer3DCustomActor.Preset("red_guard"));ruler.Freeze("idle",0);
        camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,KeepAspect=Camera3D.KeepAspectEnum.Height};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);
        Focus(selected);if(testing)CallDeferred(MethodName.RunChecks);
    }
    static StandardMaterial3D MapMaterial(string color)=>new(){AlbedoColor=new Color(color),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
    void AddShore(GeoCoordinate[] ring,string color,float y)
    {
        // Triangulate in local degree coordinates, not million-meter float coordinates.
        var first=ring[0];var points=ring.Select(p=>new Vector2((p.LongitudeE7-first.LongitudeE7)/1e7f,-(p.LatitudeE7-first.LatitudeE7)/1e7f)).ToArray();
        var indices=Geometry2D.TriangulatePolygon(points);if(indices.Length==0)throw new InvalidOperationException("Coastline triangulation failed");
        var m=new MeshInstance3D{MaterialOverride=MapMaterial(color)};world.AddChild(m);shores.Add((m,ring,indices,y));
    }
    Vector3 At(GeoCoordinate c,float y=0){var p=JapanGeography.Relative(JapanGeography.Project(c),origin);return new((float)p.EastMeters,y,-(float)p.NorthMeters);}
    void Rebase()
    {
        origin=JapanGeography.Project(catalog.Landmarks[selected].Coordinate);
        foreach(var s in shores){var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);arrays[(int)Mesh.ArrayType.Vertex]=s.Ring.Select(p=>At(p,s.Y)).ToArray();arrays[(int)Mesh.ArrayType.Index]=s.Indices;var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);s.Mesh.Mesh=mesh;}
        foreach(var (node,site) in markers)node.Position=At(site.Coordinate);
        ruler.Position=new(1,0,0);
    }
    void Focus(int i){selected=i;choice.Select(i);Rebase();Refresh();}
    void SetSpan(float s){span=s;Refresh();}
    void Refresh()
    {
        var selectedSite=catalog.Landmarks[selected];var markerSize=Mathf.Max(1,span/100);
        foreach(var (node,site) in markers){node.Scale=Vector3.One*markerSize;((MeshInstance3D)node.GetChild(0)).MaterialOverride=MapMaterial(site.Id==selectedSite.Id?"f3d580":"ba8667");}
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;
        var angle=Mathf.DegToRad(yaw);var elevation=Mathf.DegToRad(tilt);
        var target=At(new GeoCoordinate(330000000,1380000000))*Mathf.Clamp(Mathf.Log(span/120000)/Mathf.Log(2800000f/120000),0,1);
        camera.Position=target+new Vector3(Mathf.Sin(angle)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(angle)*Mathf.Cos(elevation))*span*2;
        camera.LookAt(target);ruler.Visible=span<=80;
        status.Text=$"世界時間 {session.Seconds}秒（手動進行） / 視野の高さ {span:N0}m / 選択：{selectedSite.Name}";
        details.Text=$"北緯 {selectedSite.Coordinate.LatitudeE7/1e7:0.00000} / 東経 {selectedSite.Coordinate.LongitudeE7/1e7:0.00000}　出典：{selectedSite.Id[4..]}（座標精度 {selectedSite.CoordinatePrecision}度）\n"+
            (span<=4000?"現地の地形・建物は未作成。金色は縮尺に合わせた地点記号。比較用3D武将は人物の実配置ではありません。":"地点記号の大きさは縮尺に応じて変わります。軍勢・所属・未受領情報は表示していません。");
    }
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=action;}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
    async Task Capture(string name){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(output);var initial=ContractJson.Write(session.DiagnosticSnapshot());var worldId=world.GetInstanceId();var nodes=markers.Select(m=>m.Node.GetInstanceId()).ToArray();var shoreIds=shores.Select(s=>s.Mesh.GetInstanceId()).ToArray();
            Check(catalog.Landmarks.Length==30,"30 source-linked castle/site candidates");Check(shores.Count>=109,"all coastline rings triangulated");
            Check(catalog.Polygons.SelectMany(p=>p.Outer).All(p=>{var v=camera.UnprojectPosition(At(p));return v.X>=0&&v.X<=viewport.Size.X&&v.Y>=0&&v.Y<=viewport.Size.Y;}),"national view contains every extracted coastline vertex");
            await Capture("national");
            foreach(var level in new[]{"region","area","local","near"}){Click(level);Check(markers[selected].Node.Position==Vector3.Zero,"selected focus stable at "+level);Check(camera.UnprojectPosition(Vector3.Zero).DistanceTo((Vector2)viewport.Size/2f)<1,"screen focus within one pixel at "+level);await Capture(level);}
            // Real intermediate rendered frames along one continuous camera trajectory.
            for(int i=0;i<=24;i++){SetSpan((float)(12*Math.Pow(2800000.0/12,i/24.0)));await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Click("nation");await Capture("national-return");
            Check(world.GetInstanceId()==worldId&&nodes.SequenceEqual(markers.Select(m=>m.Node.GetInstanceId()))&&shoreIds.SequenceEqual(shores.Select(s=>s.Mesh.GetInstanceId())),"one world and persistent site/coast nodes throughout zoom");
            Check(initial.SequenceEqual(ContractJson.Write(session.DiagnosticSnapshot())),"zoom did not change clock, random state, tasks or knowledge");
            foreach(var id in new[]{"geo:Q11400268","geo:Q865913","geo:Q1357421","geo:Q8011484","geo:Q907052"}){Focus(Array.FindIndex(catalog.Landmarks,l=>l.Id==id));Click("region");await Capture("tour-"+id[4..]);Check(markers[selected].Node.Position.Length()<.001f,"floating-origin focus "+id);}
            foreach(var site in catalog.Landmarks){Focus(Array.IndexOf(catalog.Landmarks,site));var xy=camera.UnprojectPosition(Vector3.Zero);Check(xy.DistanceTo((Vector2)viewport.Size/2f)<1,"rebase projects to screen center "+site.Id);}
            Focus(Array.FindIndex(catalog.Landmarks,l=>l.Id=="geo:Q1357421"));Click("local");Click("right");Click("tilt");await Capture("rotated-local");
            Check(world.GetInstanceId()==worldId,"site tour and rotation preserve world");Check(initial.SequenceEqual(ContractJson.Write(session.DiagnosticSnapshot())),"all geography controls leave simulation unchanged");
            Click("time");Check(session.Seconds==60,"explicit time action alone advances same Core clock");
            var result=new{Checks=checks,WorldId=worldId,CoastlineNodes=shores.Count,Landmarks=catalog.Landmarks.Length,Frames=25,ViewportRect=new[]{viewport.GetParent<Control>().GlobalPosition.X,viewport.GetParent<Control>().GlobalPosition.Y,viewport.Size.X,viewport.Size.Y}};
            System.IO.File.WriteAllText(output+"/checks.json",System.Text.Json.JsonSerializer.Serialize(result));GD.Print("JAPAN_MAP_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
