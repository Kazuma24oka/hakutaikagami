using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class RegionalBranchTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;WorldSession Session=>host.Current;
    JapanCatalog catalog=null!;Node3D world=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;Officer3DCustomActor actor=null!;
    readonly List<(MeshInstance3D Mesh,GeoCoordinate[] Ring,int[] Indices,float Y)> shores=new();
    sealed record PublishedPoint(string Id,GeoCoordinate Coordinate);
    readonly List<(Node3D Node,PublishedPoint Site)> markers=new();readonly List<MeshInstance3D> roads=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    GeoMillimeters origin=new(0,0);float span=50000,yaw;double fraction;int speed=1;bool testing,automatic;
    EntityId observer=ContractFixture.Officer;EntityId? applied;string clip="idle";double clipTime;
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/RegionalBranchV1/runtime-local");
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a is "--branchgeo-check" or "--branchgeo-restore" or "--branchgeo-open" or "--branchgeo-trapped")??"";
    long Fork=>1+BranchRoutes.Duration(["road"],w:RegionalBranch.Create());
    long Arrival=>1+BranchRoutes.Duration(["road","kasadera-access","kasadera-tange","tange-fort"],w:RegionalBranch.Create());
    long Final=>Arrival+BranchRoutes.Duration(["tange-fort","kasadera-tange","kasadera-access","road"],true,RegionalBranch.Create());
    string SaveRoot{get{if(!testing)return ProjectSettings.GlobalizePath("user://regional-branches-v1-manual");var text=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..];if(!Guid.TryParseExact(text,"N",out var key))throw new Exception("Invalid test key");return output+"/saves/"+key.ToString("N");}}
    public override void _Ready()
    {
        testing=Mode!="";var initial=RegionalBranch.Create(!OS.GetCmdlineUserArgs().Contains("--open-pass"));if(Mode=="--branchgeo-trapped")initial.Branches=initial.Branches! with{BlockedEdges=["pass","kasadera-access"]};host=new(new(initial));store=new(SaveRoot,RegionalBranch.Create(!OS.GetCmdlineUserArgs().Contains("--open-pass")).Definitions);
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 尾張：清洲城 → 熱田神宮 → 善照寺砦"});
        col.AddChild(new Label{Text="本人が受け取った情報と現地の目撃から経路を判断 / 道路形状・通行条件は検証用です",Modulate=new Color("dfbf80")});
        var bar=new HFlowContainer();col.AddChild(bar);
        Add(bar,"dispatch","移動・書役への委任を発送",()=>{Submit(RegionalBranch.Order(Session.DiagnosticSnapshot(),"request:regional-move"));Submit(RegionalBranch.Order(Session.DiagnosticSnapshot(),"request:regional-delegate",ActionKind.Transfer,ContractFixture.Clerk));});
        Add(bar,"step","1秒",()=>Advance(Session.Seconds+1));Add(bar,"ten","10分",()=>Advance(Session.Seconds+600));Add(bar,"auto","自動進行",()=>automatic=!automatic);
        Add(bar,"speed","速度切替",()=>speed=speed==1?60:speed==60?360:1);Add(bar,"pause","停止・再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:owari-branches-v1"),new(fraction,speed));notice.Text="位置・経路版・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{Load("manual");notice.Text="保存地点で停止して復旧しました";});
        Add(bar,"nation","全国",()=>span=2800000);Add(bar,"region","尾張 50km",()=>span=50000);Add(bar,"near","本人 12m",()=>span=12);Add(bar,"rotate","回転",()=>yaw+=30);
        Add(bar,"observer","本人・主君 切替",()=>observer=observer==ContractFixture.Officer?ContractFixture.Lord:ContractFixture.Officer);
        status=new Label();notice=new Label{Text="徒歩1m/s・使者2m/sは試作係数。距離は拠点間の大円距離で、道路実長ではありません。"};col.AddChild(status);col.AddChild(notice);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)span=Mathf.Max(12,span/1.5f);if(m.ButtonIndex==MouseButton.WheelDown)span=Mathf.Min(2800000,span*1.5f);Refresh();}};
        world=new Node3D();viewport.AddChild(world);world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in catalog.Landmarks.Select(l=>new PublishedPoint(l.Id,l.Coordinate)).Concat(RegionalBranch.Catalog.Nodes.Where(n=>!catalog.Landmarks.Any(l=>l.Id=="geo:"+n.Stop.Id)).Select(n=>new PublishedPoint("geo:"+n.Stop.Id,new(n.Stop.LatitudeE7,n.Stop.LongitudeE7))))){var n=new Node3D();world.AddChild(n);n.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)});markers.Add((n,site));}
        foreach(var edge in RegionalBranch.Edges){var mesh=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e2c17e"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=false}};world.AddChild(mesh);roads.Add(mesh);}
        actor=new Officer3DCustomActor();world.AddChild(actor);camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);var credit=new Label{Text=RegionalBranch.Catalog.Credit,AutowrapMode=TextServer.AutowrapMode.WordSmart};credit.AddThemeFontSizeOverride("font_size",12);col.AddChild(credit);Refresh();if(testing)CallDeferred(MethodName.RunChecks);
    }
    RoutePoint Sample(){var v=Session.GetBranchView(observer);return new(v.Position.EastMm+(long)((v.NextPosition.EastMm-v.Position.EastMm)*fraction),v.Position.NorthMm+(long)((v.NextPosition.NorthMm-v.Position.NorthMm)*fraction));}
    double RenderDistance(){var v=Session.GetBranchView(observer);return v.DistanceMm+v.StepDistanceMm*fraction;}
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

    (Vector3 A,Vector3 B,bool Visible) VisibleLeg(BranchEdge edge)
    {
        // Clip in double precision BEFORE converting to float, avoiding kilometer-long near-view triangles.
        var a=JapanGeography.Relative(new(RegionalBranch.Nodes.Single(n=>n.Id==edge.From).Position.EastMm,RegionalBranch.Nodes.Single(n=>n.Id==edge.From).Position.NorthMm),origin);var b=JapanGeography.Relative(new(RegionalBranch.Nodes.Single(n=>n.Id==edge.To).Position.EastMm,RegionalBranch.Nodes.Single(n=>n.Id==edge.To).Position.NorthMm),origin);
        double dx=b.EastMeters-a.EastMeters,dz=b.NorthMeters-a.NorthMeters,length=Math.Sqrt(dx*dx+dz*dz);
        double mid=-(a.EastMeters*dx+a.NorthMeters*dz)/(length*length),radius=span*4/length;
        double lo=Math.Max(0,mid-radius),hi=Math.Min(1,mid+radius);if(lo>hi)return(Vector3.Zero,Vector3.Zero,false);
        return(new((float)(a.EastMeters+dx*lo),0,-(float)(a.NorthMeters+dz*lo)),new((float)(a.EastMeters+dx*hi),0,-(float)(a.NorthMeters+dz*hi)),true);
    }
    static string EdgeName(string id)=>id switch{"road"=>"清洲→熱田","pass"=>"熱田→善照寺（直通）","kasadera-access"=>"熱田→笠寺","kasadera-tange"=>"笠寺→丹下","tange-fort"=>"丹下→善照寺",_=>"不明な経路"};
    static string TaskText(string text)=>text.Replace("Running","実行中").Replace("Completed","完了").Replace("Stopped","停止");
    void Refresh()
    {
        if(applied!=observer){actor.Apply(Officer3DCustomActor.Preset(observer==ContractFixture.Lord?"commander":"red_guard"));applied=observer;}
        var view=Session.GetBranchView(observer);buttons["dispatch"].Disabled=Session.GetActorView(ContractFixture.Lord).Tasks.Any();Rebase();
        var markerScale=Mathf.Max(1,span/160);foreach(var (node,site) in markers)node.Scale=Vector3.One*markerScale;
        int index=0;foreach(var edge in RegionalBranch.Edges){var segment=VisibleLeg(edge);var mesh=roads[index++];mesh.Visible=segment.Visible;if(!segment.Visible)continue;var a=segment.A;var b=segment.B;((StandardMaterial3D)mesh.MaterialOverride).NoDepthTest=false;((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor=new Color(view.KnownBlockedEdges.Contains(edge.Id)?"c6554d":view.PlannedEdges.Contains(edge.Id)?"e2c17e":"718798");((BoxMesh)mesh.Mesh).Size=new(Mathf.Max(.8f,span/700),.04f,(b-a).Length());var layer=view.PlannedEdges.Contains(edge.Id)?2:view.KnownBlockedEdges.Contains(edge.Id)?1:0;mesh.Position=(a+b)/2+new Vector3(0,.03f+layer*Mathf.Max(.02f,span/100000),0);mesh.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        var dir=new Vector3(view.Heading.EastMm,0,-view.Heading.NorthMm);actor.Rotation=new(0,Mathf.Atan2(dir.X,dir.Z),0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;clipTime=view.Walking?(RenderDistance()/1000.0/actor.WalkStride*length)%length:(Session.Seconds+fraction)%length;actor.Freeze(clip,clipTime);actor.Visible=span<=80;
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;var target=span>100000?At(new(330000000,1380000000)):span>80?(AtRoute(RegionalBranch.Nodes.Single(n=>n.Id=="castle").Position)+AtRoute(RegionalBranch.Nodes.Single(n=>n.Id=="village").Position))/2:Vector3.Zero;var angle=Mathf.DegToRad(yaw);camera.Position=target+new Vector3(Mathf.Sin(angle)*.5f,.8660254f,Mathf.Cos(angle)*.5f)*span*2;camera.LookAt(target);
        var geo=JapanGeography.Inverse(origin);status.Text=$"世界時間 {Session.Seconds}秒 / {(Session.Paused?"停止":"進行可能")} / {speed}倍 / {(automatic?"自動":"手動")} / {(observer==ContractFixture.Lord?"主君":"奉行本人")}";
        details.Text=$"本人位置 北緯{geo.LatitudeE7/1e7:0.0000000} 東経{geo.LongitudeE7/1e7:0.0000000} / 進行{view.DistanceMm/1000.0:N3}m / {(view.Walking?"歩行":"待機")}\n"+string.Join(" / ",view.Knowledge.Tasks.Select(t=>TaskText(t.KnownStatus)))+"\n既知の通行止め："+(view.KnownBlockedEdges.Length==0?"情報なし":string.Join(" / ",view.KnownBlockedEdges.Select(EdgeName)))+" / 予定経路："+string.Join(" ／ ",view.PlannedEdges.Select(EdgeName))+"\n"+(observer==ContractFixture.Lord?"配下の現在位置は表示しません。封鎖の情報も報告を受領してから判明します。":"地図の線は概略経路。川・橋・高低差は未復元です。");
    }
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception(r.VisibleReason);}
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){notice.Text="操作を完了できませんでした："+e.Message;if(testing)throw;}};}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    void Advance(long t){fraction=0;Session.AdvanceTo(t);Refresh();}
    public override void _Process(double delta){if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta){if(automatic&&!Session.Paused){fraction+=Math.Min(delta,.25)*speed;var n=(long)fraction;if(n>0){Session.AdvanceTo(Session.Seconds+n);fraction=Session.Paused?0:fraction-n;}Refresh();}}
    void Load(string slot){var p=store.Prepare(slot);var old=Session;if(host.CommitLoad(p,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load failed");fraction=p.Presentation.DisplayFraction;speed=p.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Seconds=Session.Seconds,Observer=observer.Value,Origin=origin,Fraction=fraction,Span=span,Heading=actor.Rotation.Y,Clip=clip,Time=clipTime,Details=details.Text,Position=Session.GetBranchView(observer).Position,Distance=Session.GetBranchView(observer).DistanceMm,Plan=Session.GetBranchView(observer).PlannedEdges,Known=Session.GetBranchView(observer).KnownBlockedEdges});
    void Check(bool v,string text){if(!v)throw new Exception(text);checks.Add(text);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void SaveStage(string stage){Refresh();store.Save(Session,stage,new("world:owari-branches-v1"),new(fraction,speed));System.IO.File.WriteAllText(output+"/"+stage+"-pose.json",Pose());}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(output);var nodeId=world.GetInstanceId();
            if(Mode=="--branchgeo-trapped"){
                Click("dispatch");Advance(Fork);span=12;var view=Session.GetBranchView(observer);Check(!view.Walking&&view.Position==RegionalBranch.Nodes.Single(n=>n.Id=="fork").Position,"all exits blocked safely stops at physical junction");Check(view.KnownBlockedEdges.Length==2,"only personally encountered closures are learned");await Capture("trapped");
            }else if(Mode=="--branchgeo-open"){
                Click("dispatch");Advance(1001);fraction=.375;span=12;await Capture("open-unseen-officer");observer=ContractFixture.Lord;await Capture("open-unseen-lord");
                Check(!Session.GetBranchView(ContractFixture.Lord).KnownBlockedEdges.Any(),"open graph has no closure knowledge");
            }else if(Mode=="--branchgeo-restore"){
                foreach(var stage in new[]{"unseen","detour"}){observer=ContractFixture.Officer;span=12;Load(stage);Check(Session.Paused,"separate restoration paused "+stage);System.IO.File.WriteAllText(output+"/actual-"+stage+"-pose.json",Pose());Check(Pose()==System.IO.File.ReadAllText(output+"/"+stage+"-pose.json"),"restored canonical position route knowledge heading and animation "+stage);await Capture("restored-"+stage);Session.SetPaused(false);fraction=0;Advance(Final);observer=ContractFixture.Lord;Refresh();Check(Pose()==System.IO.File.ReadAllText(output+"/final-pose.json"),"continued report and visible position "+stage);await Capture("continued-"+stage);}
            }else{
                span=2800000;await Capture("national");Click("dispatch");Advance(1001);fraction=.375;span=12;await Capture("closed-unseen-officer");SaveStage("unseen");var unseen=Pose();Advance(Session.Seconds+10);Load("unseen");Check(Session.Paused&&Pose()==unseen,"same process unseen closure restoration matches");await Capture("same-restored-unseen");Session.SetPaused(false);observer=ContractFixture.Lord;await Capture("closed-unseen-lord");
                observer=ContractFixture.Officer;Advance(Fork-1);Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"closure unknown before reaching physical junction");Check(Session.GetBranchView(observer).PlannedEdges.SequenceEqual(new[]{"road","pass"}),"initial plan does not consult hidden closure");
                Advance(Fork);var own=Session.GetBranchView(observer);Check(own.KnownBlockedEdges.SequenceEqual(new[]{"pass"}),"physical observation creates personal closure knowledge");Check(own.PlannedEdges.SequenceEqual(new[]{"road","kasadera-access","kasadera-tange","tange-fort"}),"reroute uses known evidence through Kasadera and Tange");Check(own.Position==RegionalBranch.Nodes.Single(n=>n.Id=="fork").Position,"rerouting retains physical junction position");await Capture("observed");span=50000;await Capture("regional-detour");span=12;
                Advance(Fork+3000);fraction=.375;SaveStage("detour");await Capture("detour");var expected=Pose();Advance(Session.Seconds+10);Load("detour");Check(Session.Paused&&Pose()==expected,"same process exact route pose and knowledge restoration");await Capture("same-restored-detour");Session.SetPaused(false);
                observer=ContractFixture.Lord;Advance(Fork+BranchRoutes.Duration(["road"],true,RegionalBranch.Create())-1);Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"issuer does not know closure one second before report");await Capture("lord-before-observation");Advance(Session.Seconds+1);Check(Session.GetBranchView(observer).KnownBlockedEdges.SequenceEqual(new[]{"pass"}),"issuer learns closure only after physical message delivery");await Capture("lord-after-observation");
                observer=ContractFixture.Officer;Advance(Arrival);Check(!Session.GetBranchView(observer).Walking&&Session.GetActorView(observer).Location.Site==ContractFixture.Village,"detour arrives at real destination");await Capture("arrival");observer=ContractFixture.Lord;Advance(Final-1);Check(Session.GetActorView(observer).Tasks.Any(t=>t.KnownStatus.Contains("結果未確認")),"completion remains unknown before result report");Advance(Final);Check(Session.GetActorView(observer).Tasks.Count(t=>t.KnownStatus=="任務完了")==2,"both movement and delegation results received");Refresh();System.IO.File.WriteAllText(output+"/final-pose.json",Pose());await Capture("final");
            }
            Check(world.GetInstanceId()==nodeId,"one world maintained throughout zoom and restoration");
            var name=Mode=="--branchgeo-restore"?"restore":Mode=="--branchgeo-open"?"open":Mode=="--branchgeo-trapped"?"trapped-checks":"checks";
            System.IO.File.WriteAllText(output+"/"+name+".json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,Fork,Arrival,Final,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));GD.Print("REGIONAL_BRANCH_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
