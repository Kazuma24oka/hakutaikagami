using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class RegionalRouteTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;WorldSession Session=>host.Current;
    JapanCatalog catalog=null!;Node3D world=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;Officer3DCustomActor actor=null!;
    readonly List<(MeshInstance3D Mesh,GeoCoordinate[] Ring,int[] Indices,float Y)> shores=new();
    readonly List<(Node3D Node,GeoLandmark Site)> markers=new();readonly List<MeshInstance3D> roads=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    GeoMillimeters origin=new(0,0);float span=50000,yaw;double fraction;int speed=1;bool testing,automatic;
    EntityId observer=ContractFixture.Officer;EntityId? applied;string clip="idle";double clipTime;
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/RegionalRouteV1/runtime-local");
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a is "--regional-check" or "--regional-restore")??"";
    long Duration=>RegionalRoute.Legs.Sum(l=>(l.LengthMm+l.FootMmPerSecond-1)/l.FootMmPerSecond);
    long Arrival=>1+Duration;
    long Final=>Arrival+RegionalRoute.Legs.Sum(l=>(l.LengthMm+l.CourierMmPerSecond-1)/l.CourierMmPerSecond);
    string SaveRoot{get{if(!testing)return ProjectSettings.GlobalizePath("user://regional-route-v1-manual");var text=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..];if(!Guid.TryParseExact(text,"N",out var key))throw new Exception("Invalid test key");return output+"/saves/"+key.ToString("N");}}
    public override void _Ready()
    {
        testing=Mode!="";host=new(new(RegionalRoute.Create()));store=new(SaveRoot,RegionalRoute.Create().Definitions);
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        foreach(var stop in RegionalRoute.Catalog.Stops.Where(s=>!catalog.Landmarks.Any(l=>l.Id=="geo:"+s.Id)))catalog=catalog with{Landmarks=[..catalog.Landmarks,new("geo:"+stop.Id,stop.Name,new(stop.LatitudeE7,stop.LongitudeE7),"https://www.wikidata.org/wiki/"+stop.Id,stop.Revision,stop.Precision,"DisplayCandidateOnly")]};
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 尾張：清洲城 → 熱田神宮 → 善照寺砦"});
        col.AddChild(new Label{Text="実在拠点を結ぶ概略経路。歴史上の道の線形・地形は未復元 / 登場人物と権限は検証用",Modulate=new Color("dfbf80")});
        var bar=new HFlowContainer();col.AddChild(bar);
        Add(bar,"dispatch","移動・書役への委任を発送",()=>{Submit(RegionalRoute.Order(Session.DiagnosticSnapshot(),"request:regional-move"));Submit(RegionalRoute.Order(Session.DiagnosticSnapshot(),"request:regional-delegate",ActionKind.Transfer,ContractFixture.Clerk));});
        Add(bar,"step","1秒",()=>Advance(Session.Seconds+1));Add(bar,"ten","10分",()=>Advance(Session.Seconds+600));Add(bar,"auto","自動進行",()=>automatic=!automatic);
        Add(bar,"speed","速度切替",()=>speed=speed==1?60:speed==60?360:1);Add(bar,"pause","停止・再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:owari-route-v1"),new(fraction,speed));notice.Text="位置・経路版・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{Load("manual");notice.Text="保存地点で停止して復旧しました";});
        Add(bar,"nation","全国",()=>span=2800000);Add(bar,"region","尾張 50km",()=>span=50000);Add(bar,"near","本人 12m",()=>span=12);Add(bar,"rotate","回転",()=>yaw+=30);
        Add(bar,"observer","本人・主君 切替",()=>observer=observer==ContractFixture.Officer?ContractFixture.Lord:ContractFixture.Officer);
        status=new Label();notice=new Label{Text="徒歩1m/s・使者2m/sは試作係数。距離は拠点間の大円距離で、道路実長ではありません。"};col.AddChild(status);col.AddChild(notice);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)span=Mathf.Max(12,span/1.5f);if(m.ButtonIndex==MouseButton.WheelDown)span=Mathf.Min(2800000,span*1.5f);Refresh();}};
        world=new Node3D();viewport.AddChild(world);world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in catalog.Landmarks){var n=new Node3D();world.AddChild(n);n.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)});markers.Add((n,site));}
        foreach(var leg in RegionalRoute.Legs){var mesh=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e2c17e"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=false}};world.AddChild(mesh);roads.Add(mesh);}
        actor=new Officer3DCustomActor();world.AddChild(actor);camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);Refresh();if(testing)CallDeferred(MethodName.RunChecks);
    }
    RoutePoint Sample()=>RouteContract.Point((long)RenderDistance(),RegionalRoute.Legs);
    double RenderDistance()
    {
        var v=Session.GetRouteActorView(observer);if(!v.Walking)return v.DistanceMm;
        var w=Session.DiagnosticSnapshot();// authoritative interpolation uses only this actor's public offset.
        long d=v.DistanceMm;var leg=RegionalRoute.Legs[RouteContract.Segment(d,RegionalRoute.Legs)];
        double next;
        // Never extrapolate across a waypoint with the previous leg's speed.
        var boundary=RegionalRoute.Legs.Take(RouteContract.Segment(d,RegionalRoute.Legs)+1).Sum(l=>l.LengthMm);next=d+Math.Min(leg.FootMmPerSecond,boundary-d)*fraction;
        return next;
    }
    GeoMillimeters RenderOrigin(){var p=Sample();return new(p.EastMm,p.NorthMm);}
    Vector3 AtRoute(RoutePoint p){var d=JapanGeography.Relative(new(p.EastMm,p.NorthMm),origin);return new((float)d.EastMeters,0,-(float)d.NorthMeters);}
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
        origin=RenderOrigin();
        foreach(var s in shores){var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);arrays[(int)Mesh.ArrayType.Vertex]=s.Ring.Select(p=>At(p,s.Y)).ToArray();arrays[(int)Mesh.ArrayType.Index]=s.Indices;var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);s.Mesh.Mesh=mesh;}
        foreach(var (node,site) in markers)node.Position=At(site.Coordinate);
        actor.Position=Vector3.Zero;
    }

    (Vector3 A,Vector3 B,bool Visible) VisibleLeg(RouteLeg leg)
    {
        // Clip in double precision BEFORE converting to float, avoiding kilometer-long near-view triangles.
        var a=JapanGeography.Relative(new(leg.From.EastMm,leg.From.NorthMm),origin);var b=JapanGeography.Relative(new(leg.To.EastMm,leg.To.NorthMm),origin);
        double dx=b.EastMeters-a.EastMeters,dz=b.NorthMeters-a.NorthMeters,length=Math.Sqrt(dx*dx+dz*dz);
        double mid=-(a.EastMeters*dx+a.NorthMeters*dz)/(length*length),radius=span*4/length;
        double lo=Math.Max(0,mid-radius),hi=Math.Min(1,mid+radius);if(lo>hi)return(Vector3.Zero,Vector3.Zero,false);
        return(new((float)(a.EastMeters+dx*lo),0,-(float)(a.NorthMeters+dz*lo)),new((float)(a.EastMeters+dx*hi),0,-(float)(a.NorthMeters+dz*hi)),true);
    }
    void Refresh()
    {
        if(applied!=observer){actor.Apply(Officer3DCustomActor.Preset(observer==ContractFixture.Lord?"commander":"red_guard"));applied=observer;}
        var view=Session.GetRouteActorView(observer);buttons["dispatch"].Disabled=Session.GetActorView(ContractFixture.Lord).Tasks.Any();Rebase();
        var markerScale=Mathf.Max(1,span/160);foreach(var (node,site) in markers)node.Scale=Vector3.One*markerScale;
        int index=0;foreach(var leg in RegionalRoute.Legs){var segment=VisibleLeg(leg);var mesh=roads[index++];mesh.Visible=segment.Visible;if(!segment.Visible)continue;var a=segment.A;var b=segment.B;((StandardMaterial3D)mesh.MaterialOverride).NoDepthTest=span>80;((BoxMesh)mesh.Mesh).Size=new(Mathf.Max(.8f,span/700),.04f,(b-a).Length());mesh.Position=(a+b)/2+new Vector3(0,.03f,0);mesh.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        var heading=RegionalRoute.Legs[RouteContract.Segment(view.DistanceMm,RegionalRoute.Legs)];var dir=AtRoute(heading.To)-AtRoute(heading.From);actor.Rotation=new(0,Mathf.Atan2(dir.X,dir.Z),0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;clipTime=view.Walking?(RenderDistance()/1000.0/actor.WalkStride*length)%length:(Session.Seconds+fraction)%length;actor.Freeze(clip,clipTime);actor.Visible=span<=80;
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;var target=span>100000?At(new(330000000,1380000000)):span>80?(AtRoute(RegionalRoute.Legs[0].From)+AtRoute(RegionalRoute.Legs[^1].To))/2:Vector3.Zero;var angle=Mathf.DegToRad(yaw);camera.Position=target+new Vector3(Mathf.Sin(angle)*.5f,.8660254f,Mathf.Cos(angle)*.5f)*span*2;camera.LookAt(target);
        var geo=JapanGeography.Inverse(origin);status.Text=$"世界時間 {Session.Seconds}秒 / {(Session.Paused?"停止":"進行可能")} / {speed}倍 / {(automatic?"自動":"手動")} / {(observer==ContractFixture.Lord?"主君":"奉行本人")}";
        details.Text=$"本人位置 北緯{geo.LatitudeE7/1e7:0.0000000} 東経{geo.LongitudeE7/1e7:0.0000000} / 進行{view.DistanceMm/1000.0:N3}m / {clip}\n"+string.Join(" / ",view.Knowledge.Tasks.Select(t=>t.KnownStatus))+"\n"+(observer==ContractFixture.Lord?"配下の現在位置は表示しません。結果は返報を受領してから判明します。":"途中地点は熱田神宮。地図の線は概略経路で、川・橋・高低差は未復元です。");
    }
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception(r.VisibleReason);}
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){notice.Text="操作を完了できませんでした："+e.Message;if(testing)throw;}};}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    void Advance(long t){fraction=0;Session.AdvanceTo(t);Refresh();}
    public override void _Process(double delta){if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta){if(automatic&&!Session.Paused){fraction+=Math.Min(delta,.25)*speed;var n=(long)fraction;if(n>0){Session.AdvanceTo(Session.Seconds+n);fraction=Session.Paused?0:fraction-n;}Refresh();}}
    void Load(string slot){var p=store.Prepare(slot);var old=Session;if(host.CommitLoad(p,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load failed");fraction=p.Presentation.DisplayFraction;speed=p.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Seconds=Session.Seconds,Observer=observer.Value,Origin=origin,Fraction=fraction,Span=span,Heading=actor.Rotation.Y,Clip=clip,Time=clipTime,Details=details.Text,Position=Session.GetRouteActorView(observer).Position,Distance=Session.GetRouteActorView(observer).DistanceMm});
    void Check(bool v,string text){if(!v)throw new Exception(text);checks.Add(text);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(output);var nodeId=world.GetInstanceId();var actorId=actor.GetInstanceId();
            if(Mode=="--regional-restore"){
                foreach(var stage in new[]{"first","turn","second","arrived"}){observer=ContractFixture.Officer;span=12;Load(stage);Check(Session.Paused,"restore paused "+stage);Check(Pose()==System.IO.File.ReadAllText(output+"/"+stage+"-pose.json"),"separate process exact canonical position, camera, animation phase and visible knowledge "+stage);await Capture("restored-"+stage);
                    Session.SetPaused(false);fraction=0;Advance(Final);observer=ContractFixture.Lord;Refresh();Check(Pose()==System.IO.File.ReadAllText(output+"/final-pose.json"),"continued visible report and position "+stage);await Capture("continued-"+stage);}
            }else{
                span=2800000;await Capture("national");Click("dispatch");Check(Session.Seconds==0,"dispatch does not move clock");Advance(1);Check(Session.GetRouteActorView(observer).Walking,"receipt starts physical travel");span=50000;await Capture("regional-departure");
                span=12;for(int i=0;i<6;i++){Advance(101+i);await Capture("walk-"+i.ToString("D2"));}
                automatic=true;var elapsed=Session.Seconds;for(int i=0;i<4;i++)UpdateClock(.25);automatic=false;Check(Session.Seconds==elapsed+1&&fraction==0,"automatic progress shares integer clock and subsecond interpolation");
                var turn=1+(RegionalRoute.Legs[0].LengthMm+999)/1000;
                foreach(var stage in new[]{("first",1001L),("turn",turn),("second",turn+1000),("arrived",Arrival)}){observer=ContractFixture.Officer;if(stage.Item1=="turn"){Advance(stage.Item2-1);fraction=.375;Check(Math.Abs(RenderDistance()-11654210)<.001,"short final step interpolates physical distance and animation stride before turn");}Advance(stage.Item2);fraction=stage.Item1=="arrived"?0:.375;span=12;Refresh();var expected=Pose();store.Save(Session,stage.Item1,new("world:owari-route-v1"),new(fraction,speed));System.IO.File.WriteAllText(output+"/"+stage.Item1+"-pose.json",expected);await Capture(stage.Item1);
                    Advance(Session.Seconds+5);Load(stage.Item1);Check(Session.Paused&&Pose()==expected,"same process restores pose "+stage.Item1);await Capture("same-restored-"+stage.Item1);Session.SetPaused(false);}
                Check(Session.GetRouteActorView(observer).Knowledge.Location.Site==ContractFixture.Village,"arrives at geographically bound destination");
                observer=ContractFixture.Lord;Advance(Final-1);Check(Session.GetActorView(observer).Tasks.Any(t=>t.KnownStatus.Contains("結果未確認")),"issuer cannot see result before physical report arrival");await Capture("report-pending");Advance(Final);Check(Session.GetActorView(observer).Tasks.Count(t=>t.KnownStatus=="任務完了")==2,"movement and delegation reports known after delivery");span=12;Refresh();System.IO.File.WriteAllText(output+"/final-pose.json",Pose());await Capture("final");
                observer=ContractFixture.Officer;span=2800000;var before=ContractJson.Write(Session.DiagnosticSnapshot());await Capture("national-arrival");span=12;Refresh();Check(before.SequenceEqual(ContractJson.Write(Session.DiagnosticSnapshot())),"zoom leaves shared simulation unchanged");
            }
            Check(world.GetInstanceId()==nodeId&&actor.GetInstanceId()==actorId,"world and actor persist across views and loads");
            System.IO.File.WriteAllText(output+"/"+(Mode=="--regional-restore"?"restore-checks":"checks")+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,Arrival,Final,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));GD.Print("REGIONAL_ROUTE_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
