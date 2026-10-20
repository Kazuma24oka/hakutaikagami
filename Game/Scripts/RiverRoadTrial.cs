using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class RiverRoadTrial : Control
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
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/RiverRoadV1/runtime-local");
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>new[]{"--river-check","--river-restore","--river-video"}.Contains(a))??"";
    string Scenario=>"river";
    long Final=>100000;
    readonly Dictionary<string,MeshInstance3D> courierMarkers=new();
    readonly Dictionary<string,Officer3DCustomActor> courierActors=new();
    readonly Dictionary<EntityId,Officer3DCustomActor> nearbyActors=new();
    string retryAddress="village",destination="okazaki",inspectEdge="nakajima-crossing";
    string SaveRoot{get{if(!testing)return ProjectSettings.GlobalizePath("user://river-road-v1-manual");var text=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..];if(!Guid.TryParseExact(text,"N",out var key))throw new Exception("Invalid test key");return output+"/saves/"+key.ToString("N");}}
    public override void _Ready()
    {
        testing=Mode!="";var initial=RiverRoads.Create();host=new(new(initial));store=new(SaveRoot,RiverRoads.Create().Definitions);
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 中島橋周辺を参考にした渡河・経路の試作"});
        col.AddChild(new Label{Text="位置は推定（±250m）。橋の形状・仮設渡し・通行止めは検証用で、1560年の復元ではありません。",Modulate=new Color("dfbf80")});
        var bar=new HFlowContainer();col.AddChild(bar);if(Mode=="--river-video")bar.Visible=false;
        Add(bar,"destination","行先：岡崎城",()=>{destination=destination=="village"?"inabayama":destination=="inabayama"?"okazaki":"village";buttons["destination"].Text="行先："+RiverRoads.Sites.Single(s=>s.Id==RegionalCampaign.Site(destination)).Name;});
        Add(bar,"dispatch","奉行への移動命令を発送",()=>Submit(RegionalCampaign.Move(Session.DiagnosticSnapshot(),"request:regional-move",destination)));
        Add(bar,"delegate","書役へ納入を委任",()=>Submit(RegionalBranch.Order(Session.DiagnosticSnapshot(),"request:regional-delegate",ActionKind.Transfer,ContractFixture.Clerk)));
        Add(bar,"escort","書役へ同行を命令",()=>Submit(RegionalCampaign.Move(Session.DiagnosticSnapshot(),"request:regional-escort",destination,ContractFixture.Clerk)));
        Add(bar,"address","再送先：善照寺",()=>{retryAddress=retryAddress=="village"?"fork":retryAddress=="fork"?"castle":"village";buttons["address"].Text="再送先："+(retryAddress=="village"?"善照寺":retryAddress=="fork"?"熱田":"清洲");});
        Add(bar,"retry","失敗した書状を再送",()=>{var mid=Session.GetCourierView(ContractFixture.Lord).LastOrDefault(v=>v.Status=="失敗報告を受領")?.Message;if(mid==null){notice.Text="まだ失敗報告を受け取っていません";return;}var r=Session.RetryCourier(ContractFixture.Lord,mid,retryAddress,Session.Seconds,Session.Revision);notice.Text=r.VisibleReason;});
        Add(bar,"road","確認する道：中島橋周辺",()=>{var edges=RiverRoads.Edges;inspectEdge=edges[(Array.FindIndex(edges,e=>e.Id==inspectEdge)+1)%edges.Length].Id;buttons["road"].Text="確認する道："+EdgeName(inspectEdge);});
        Add(bar,"inspect","現地で通行確認",()=>notice.Text=Session.InspectRoad(observer,inspectEdge,Session.Revision).VisibleReason);
        Add(bar,"cancel","取消を発送",()=>SendControl(ActionKind.Cancel));Add(bar,"redirect","帰城変更を発送",()=>SendControl(ActionKind.Redirect));Add(bar,"open","奉行本人が書状を開封",OpenControl);
        Add(bar,"step","1秒",()=>Advance(Session.Seconds+1));Add(bar,"ten","10分",()=>Advance(Session.Seconds+600));Add(bar,"auto","自動進行",()=>automatic=!automatic);
        Add(bar,"speed","速度切替",()=>speed=speed==1?60:speed==60?360:1);Add(bar,"pause","停止・再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:river-roads-v1"),new(fraction,speed));notice.Text="位置・経路版・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{Load("manual");notice.Text="保存地点で停止して復旧しました";});
        Add(bar,"nation","全国",()=>span=2800000);Add(bar,"region","尾張 50km",()=>span=50000);Add(bar,"wide","尾張・美濃・三河 100km",()=>span=100000);Add(bar,"near","本人 12m",()=>span=12);Add(bar,"rotate","回転",()=>yaw+=30);
        Add(bar,"observer","奉行・書役・主君 切替",()=>observer=observer==ContractFixture.Officer?ContractFixture.Clerk:observer==ContractFixture.Clerk?ContractFixture.Lord:ContractFixture.Officer);
        status=new Label();notice=new Label{Text="徒歩1m/s・使者2m/sは試作係数。随行表示は重なりを避けて横にずらします。距離は拠点間の大円距離で、道路実長ではありません。"};col.AddChild(status);col.AddChild(notice);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,Mode=="--river-video"?380:500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)span=Mathf.Max(12,span/1.5f);if(m.ButtonIndex==MouseButton.WheelDown)span=Mathf.Min(2800000,span*1.5f);Refresh();}};
        world=new Node3D();viewport.AddChild(world);world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in catalog.Landmarks.Select(l=>new PublishedPoint(l.Id,l.Coordinate)).Concat(RegionalBranch.Catalog.Nodes.Where(n=>!catalog.Landmarks.Any(l=>l.Id=="geo:"+n.Stop.Id)).Select(n=>new PublishedPoint("geo:"+n.Stop.Id,new(n.Stop.LatitudeE7,n.Stop.LongitudeE7))))){var n=new Node3D();world.AddChild(n);n.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)});markers.Add((n,site));}
        foreach(var edge in RiverRoads.Edges){var mesh=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e2c17e"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=false}};world.AddChild(mesh);roads.Add(mesh);}
        actor=new Officer3DCustomActor();world.AddChild(actor);camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);if(Mode=="--river-video")details.Visible=false;var credit=new Label{Text=RegionalBranch.Catalog.Credit,AutowrapMode=TextServer.AutowrapMode.WordSmart};credit.AddThemeFontSizeOverride("font_size",Mode=="--river-video"?9:12);col.AddChild(credit);Refresh();if(Mode=="--river-video")CallDeferred(MethodName.BeginVideo);else if(testing)CallDeferred(MethodName.RunChecks);
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
        var a=JapanGeography.Relative(new(RiverRoads.Nodes.Single(n=>n.Id==edge.From).Position.EastMm,RiverRoads.Nodes.Single(n=>n.Id==edge.From).Position.NorthMm),origin);var b=JapanGeography.Relative(new(RiverRoads.Nodes.Single(n=>n.Id==edge.To).Position.EastMm,RiverRoads.Nodes.Single(n=>n.Id==edge.To).Position.NorthMm),origin);
        double dx=b.EastMeters-a.EastMeters,dz=b.NorthMeters-a.NorthMeters,length=Math.Sqrt(dx*dx+dz*dz);
        double mid=-(a.EastMeters*dx+a.NorthMeters*dz)/(length*length),radius=span*4/length;
        double lo=Math.Max(0,mid-radius),hi=Math.Min(1,mid+radius);if(lo>hi)return(Vector3.Zero,Vector3.Zero,false);
        return(new((float)(a.EastMeters+dx*lo),0,-(float)(a.NorthMeters+dz*lo)),new((float)(a.EastMeters+dx*hi),0,-(float)(a.NorthMeters+dz*hi)),true);
    }
    static string EdgeName(string id)=>id switch{"road"=>"清洲～熱田","pass"=>"熱田～善照寺（直通）","kasadera-access"=>"熱田～笠寺","kasadera-tange"=>"笠寺～丹下","tange-fort"=>"丹下～善照寺","narumi-access"=>"善照寺～鳴海道筋（推定）","bridge-approach"=>"橋の北岸へ（推定）","nakajima-crossing"=>"中島橋周辺の渡河（推定）","mikawa-approach"=>"岡崎方面（概略）","ford-approach"=>"仮設渡しへ（検証用）","temporary-ford"=>"仮設渡しで渡河（検証用）","ford-return"=>"南岸の道へ（検証用）","mino-concept"=>"清洲～稲葉山（概略・渡河未復元）","mikawa-concept"=>"善照寺～岡崎（概略・渡河未復元）",_=>"不明な経路"};
    static string TaskText(string text)=>text.Replace("Running","実行中").Replace("Completed","完了").Replace("Stopped","停止");
    void Refresh()
    {
        if(applied!=observer){actor.Apply(Officer3DCustomActor.Preset(observer==ContractFixture.Lord?"commander":observer==ContractFixture.Clerk?"veteran":"red_guard"));applied=observer;}
        var view=Session.GetBranchView(observer);buttons["dispatch"].Disabled=Session.GetActorView(ContractFixture.Lord).Tasks.Any(t=>t.Executor==ContractFixture.Officer);buttons["escort"].Disabled=Session.GetActorView(ContractFixture.Lord).Tasks.Any(t=>t.Executor==ContractFixture.Clerk);Rebase();
        var markerScale=Mathf.Max(1,span/160);foreach(var (node,site) in markers)node.Scale=Vector3.One*markerScale;
        int index=0;foreach(var edge in RiverRoads.Edges){var segment=VisibleLeg(edge);var mesh=roads[index++];mesh.Visible=segment.Visible;if(!segment.Visible)continue;var a=segment.A;var b=segment.B;((StandardMaterial3D)mesh.MaterialOverride).NoDepthTest=false;((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor=new Color(view.KnownBlockedEdges.Contains(edge.Id)?"c6554d":view.PlannedEdges.Contains(edge.Id)?"e2c17e":"718798");((BoxMesh)mesh.Mesh).Size=new(Mathf.Max(.8f,span/700),.04f,(b-a).Length());var layer=view.PlannedEdges.Contains(edge.Id)?2:view.KnownBlockedEdges.Contains(edge.Id)?1:0;mesh.Position=(a+b)/2+new Vector3(0,.03f+layer*Mathf.Max(.02f,span/100000),0);mesh.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        var dir=new Vector3(view.Heading.EastMm,0,-view.Heading.NorthMm);actor.Rotation=new(0,Mathf.Atan2(dir.X,dir.Z),0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;clipTime=view.Walking?(RenderDistance()/1000.0/actor.WalkStride*length)%length:(Session.Seconds+fraction)%length;actor.Freeze(clip,clipTime);actor.Visible=span<=80;
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;var target=span>1000000?At(new(330000000,1380000000)):span>5000?(AtRoute(RiverRoads.Nodes.Single(n=>n.Id=="castle").Position)+AtRoute(RiverRoads.Nodes.Single(n=>n.Id=="village").Position))/2:Vector3.Zero;var angle=Mathf.DegToRad(yaw);camera.Position=target+new Vector3(Mathf.Sin(angle)*.5f,.8660254f,Mathf.Cos(angle)*.5f)*span*2;camera.LookAt(target);
        foreach(var m in courierMarkers.Values)m.Visible=false;foreach(var a in courierActors.Values)a.Visible=false;
        foreach(var c in Session.GetCourierView(observer).Where(c=>c.Position!=null)){
            if(!courierMarkers.TryGetValue(c.Attempt,out var marker)){marker=new MeshInstance3D{Mesh=new SphereMesh{Radius=.28f,Height=.56f},MaterialOverride=MapMaterial("72d8e8")};world.AddChild(marker);courierMarkers[c.Attempt]=marker;var model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset("veteran"));courierActors[c.Attempt]=model;}
            marker.Visible=span>80;marker.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(0,.5f,0);marker.Scale=Vector3.One*Mathf.Max(1,span/300);
            var courier=courierActors[c.Attempt];courier.Visible=span<=80;courier.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(.65f,0,0);var walking=c.Status.StartsWith("移動");var name=walking?"walk":"idle";var duration=courier.Player.GetAnimation(courier.Clips[name]).Length;courier.Freeze(name,walking?((c.DistanceMm!.Value+c.StepDistanceMm!.Value*fraction)/1000.0/courier.WalkStride*duration)%duration:(Session.Seconds+fraction)%duration);if(c.Heading!=null)courier.Rotation=new(0,Mathf.Atan2(c.Heading.EastMm,-c.Heading.NorthMm),0);
        }
        foreach(var model in nearbyActors.Values)model.Visible=false;
        foreach(var other in Session.GetNearbyActors(observer)){
            if(!nearbyActors.TryGetValue(other.Actor,out var model)){model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset(other.Actor==ContractFixture.Lord?"commander":other.Actor==ContractFixture.Officer?"red_guard":"veteran"));nearbyActors[other.Actor]=model;}
            model.Visible=span<=80;model.Position=AtRoute(new(other.Position.EastMm+(long)((other.NextPosition.EastMm-other.Position.EastMm)*fraction),other.Position.NorthMm+(long)((other.NextPosition.NorthMm-other.Position.NorthMm)*fraction)))+new Vector3(1.3f+Array.FindIndex(Session.GetNearbyActors(observer),x=>x.Actor==other.Actor)*1.3f,0,0);model.Rotation=new(0,Mathf.Atan2(other.Heading.EastMm,-other.Heading.NorthMm),0);var animation=other.Walking?"walk":"idle";var modelDuration=model.Player.GetAnimation(model.Clips[animation]).Length;model.Freeze(animation,other.Walking?((other.DistanceMm+other.StepDistanceMm*fraction)/1000.0/model.WalkStride*modelDuration)%modelDuration:(Session.Seconds+fraction)%modelDuration);
        }
        var geo=JapanGeography.Inverse(origin);status.Text=$"世界時間 {Session.Seconds}秒 / {(Session.Paused?"停止":"進行可能")} / {speed}倍 / {(automatic?"自動":"手動")} / {(observer==ContractFixture.Lord?"主君":observer==ContractFixture.Clerk?"書役本人":"奉行本人")}";
        if(Mode=="--river-video")status.Text=$"世界時間 {Session.Seconds}秒 / デモ収録（早送り区間あり） / 奉行本人";
        var letter=Session.GetActorView(observer).Messages.Any(m=>m.Sealed)?"未開封の書状あり":"未開封の書状なし";details.Text=$"本人位置 北緯{geo.LatitudeE7/1e7:0.0000000} 東経{geo.LongitudeE7/1e7:0.0000000} / 進行{view.DistanceMm/1000.0:N3}m / {(view.Walking?"歩行":"待機")}\n"+string.Join(" / ",view.Knowledge.Tasks.Select(t=>TaskText(t.KnownStatus)))+" / "+letter+"\n既知の通行止め："+(view.KnownBlockedEdges.Length==0?"情報なし":string.Join(" / ",view.KnownBlockedEdges.Select(EdgeName)))+" / 経路（通過済みを含む）："+string.Join(" ／ ",view.PlannedEdges.Select(EdgeName))+"\n"+(observer==ContractFixture.Lord?"配下の現在位置は表示しません。封鎖の情報も報告を受領してから判明します。":"追加した道は資料の手掛かりからの推定区間。実道路長・1560年の橋は未確定です。");
        DrawRiver();CourierDetails();
    }
    void CourierDetails(){details.Text+="\n伝令（手元の情報・近傍の目撃のみ）："+string.Join(" ／ ",Session.GetCourierView(observer).Select(c=>(RiverRoads.Nodes.Any(n=>n.Id==c.Destination)?RiverRoads.Sites.Single(s=>s.Id==RegionalCampaign.Site(c.Destination)).Name:c.Destination)+"："+c.Status));}
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception(r.VisibleReason);}
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){notice.Text="操作を完了できませんでした："+e.Message;if(testing)throw;}};}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    void Advance(long t){fraction=0;Session.AdvanceTo(t);Refresh();}
    public override void _Process(double delta){if(Mode=="--river-video"&&videoReady)VideoFrame();else if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta){if(automatic&&!Session.Paused){fraction+=Math.Min(delta,.25)*speed;var n=(long)fraction;if(n>0){Session.AdvanceTo(Session.Seconds+n);fraction=Session.Paused?0:fraction-n;}Refresh();}}
    void Load(string slot){var p=store.Prepare(slot);var old=Session;if(host.CommitLoad(p,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load failed");fraction=p.Presentation.DisplayFraction;speed=p.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Seconds=Session.Seconds,Observer=observer.Value,Origin=origin,Fraction=fraction,Span=span,Heading=actor.Rotation.Y,Clip=clip,Time=clipTime,Details=details.Text,Couriers=Session.GetCourierView(observer),Nearby=Session.GetNearbyActors(observer),Position=Session.GetBranchView(observer).Position,Distance=Session.GetBranchView(observer).DistanceMm,Plan=Session.GetBranchView(observer).PlannedEdges,Known=Session.GetBranchView(observer).KnownBlockedEdges});
    void Check(bool v,string text){if(!v)throw new Exception(text);checks.Add(text);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void SendControl(ActionKind kind){var target=Session.GetActorView(ContractFixture.Lord).Tasks.First(t=>t.Executor==ContractFixture.Officer).Id;Submit(RegionalCampaign.Control(Session.DiagnosticSnapshot(),"request:regional-"+(kind==ActionKind.Cancel?"cancel":"redirect"),target,kind));}
    void OpenControl(){var m=Session.GetActorView(ContractFixture.Officer).Messages.FirstOrDefault(m=>m.Sealed);if(m!=null&&Session.Open(ContractFixture.Officer,m.Id,Session.Revision).Code!=ResultCode.Accepted)throw new Exception("Opening failed");}
    void SaveStage(string stage){Refresh();store.Save(Session,Scenario+"-"+stage,new("world:river-roads-v1"),new(fraction,speed));System.IO.File.WriteAllText(output+"/"+Scenario+"-"+stage+"-pose.json",Pose());}
    MeshInstance3D river=null!,deck=null!,ford=null!,barrier=null!;bool videoReady;int videoFrame;long bridgeAt,fordStart,fordEnd;double movieClock;
    RoutePoint P(string id)=>RiverRoads.Nodes.Single(n=>n.Id==id).Position;
    MeshInstance3D Box(string color){var m=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=MapMaterial(color)};world.AddChild(m);return m;}
    void Span(MeshInstance3D m,RoutePoint a,RoutePoint b,float width,float height,float y){var x=AtRoute(a);var z=AtRoute(b);((BoxMesh)m.Mesh).Size=new(width,height,(z-x).Length());m.Position=(x+z)/2+new Vector3(0,y,0);m.Rotation=new(0,Mathf.Atan2((z-x).X,(z-x).Z),0);}
    void DrawRiver(){if(river==null){river=Box("426c81");deck=Box("93774f");ford=Box("7d9f9c");barrier=Box("c86048");}
        var a=P("bridge-west");var b=P("bridge-east");long east=(a.EastMm+b.EastMm)/2,north=(a.NorthMm+b.NorthMm)/2;long dx=b.EastMm-a.EastMm,dn=b.NorthMm-a.NorthMm;
        double len=Math.Sqrt((double)dx*dx+(double)dn*dn);var endA=new RoutePoint(east-(long)(dn/len*1800000),north+(long)(dx/len*1800000));var endB=new RoutePoint(east+(long)(dn/len*1800000),north-(long)(dx/len*1800000));Span(river,endA,endB,21,.015f,.022f);Span(deck,a,b,4,.045f,.055f);Span(ford,P("ford-west"),P("ford-east"),4,.03f,.045f);
        // Closure state is visible only after this observer's sighting/report. River/bridge art is public fixture geometry.
        barrier.Visible=Session.GetBranchView(observer).KnownBlockedEdges.Contains("nakajima-crossing");var ap=AtRoute(a);((BoxMesh)barrier.Mesh).Size=new(4,.7f,.25f);barrier.Position=ap+new Vector3(0,.4f,0);barrier.Rotation=deck.Rotation;
    }
    void StartJourney(){Submit(RiverRoads.Order(Session.DiagnosticSnapshot(),"request:river-move"));Advance(1);bridgeAt=Session.DiagnosticSnapshot().Branches!.Journeys.Single().Legs.Single(l=>l.Edge=="nakajima-crossing").StartAt;}
    void ToFord(){Advance(bridgeAt);var l=Session.DiagnosticSnapshot().Branches!.Journeys.Single().Legs.Single(l=>l.Edge=="temporary-ford");fordStart=l.StartAt;fordEnd=l.EndAt;}
    void BeginVideo(){System.IO.Directory.CreateDirectory(output);StartJourney();Advance(bridgeAt-6);span=600;notice.Text="資料の手掛かりを反映した経路へ。道筋・渡河位置は推定です。";notice.AddThemeFontSizeOverride("font_size",23);movieClock=Session.Seconds;videoReady=true;}
    void VideoFrame(){int f=videoFrame++;if(f<72){span=600;notice.Text="① 川と渡河点を経路に組み込む（位置推定・形状は検証用）";}
        else if(f<216){span=12;notice.Text="② 現地へ歩き、橋の入口で通行止めを確認";movieClock+=1.0/24;Advance((long)movieClock);fraction=movieClock-Math.Floor(movieClock);if(Session.Seconds>=bridgeAt){notice.Text="通行止めを目撃 → 仮設渡しへの経路へ変更（渡しは検証用）";}}
        else if(f<264){span=12;if(f==216)ToFord();notice.Text="通行止めを目撃。検証用の仮設渡しへ迂回します。";}
        else if(f<408){span=35;notice.Text="③ 迂回して仮設渡しへ（移動を早送り）";if(f==264){ToFord();movieClock=Session.Seconds;}movieClock+=(fordStart+8-bridgeAt)/144.0;Advance(Math.Min((long)movieClock,fordStart+8));fraction=movieClock-Math.Floor(movieClock);}
        else if(f<456){span=12;notice.Text="④ 渡河途中で保存 → 同じ位置・動作で復旧";if(f==408){Advance(fordStart+8);fraction=.375;SaveStage("video");}if(f==432)Load("river-video");}
        else if(f<552){span=12;notice.Text="⑤ 復旧後も同じ経路と世界時間で移動を続ける";if(f==456){Session.SetPaused(false);movieClock=Session.Seconds+.375;}movieClock+=2.0/24;Advance((long)movieClock);fraction=movieClock-Math.Floor(movieClock);}
        else{span=600;notice.Text="推定区間と検証用渡しを区別。史実の道・橋の復元は今後も照合を続けます。";}
        Refresh();if(f>=623){GD.Print("RIVER_VIDEO_PASSED 624 frames");GetTree().Quit();}}
    async void RunChecks(){try{System.IO.Directory.CreateDirectory(output);var stages=new[]{"approach","seen","ford","passed"};
        if(Mode.EndsWith("restore")){foreach(var stage in stages){span=stage=="approach"?150:70;Load("river-"+stage);Check(Session.Paused,"restore paused "+stage);Check(Pose()==System.IO.File.ReadAllText(output+"/river-"+stage+"-pose.json"),"restore exact view "+stage);await Capture("restored-"+stage);Session.SetPaused(false);Advance(Final);fraction=0;span=70;await Capture("continued-"+stage);}}
        else{StartJourney();Advance(bridgeAt-1);span=150;fraction=.375;SaveStage("approach");await Capture("approach");Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"bridge unknown before arrival");ToFord();span=70;fraction=.375;SaveStage("seen");await Capture("seen");Check(Session.GetBranchView(observer).KnownBlockedEdges.Contains("nakajima-crossing"),"closure seen at bank");Check(Session.GetBranchView(ContractFixture.Lord).KnownBlockedEdges.Length==0,"no immediate remote knowledge");Advance((fordStart+fordEnd)/2);fraction=.375;SaveStage("ford");await Capture("ford");Advance(fordEnd+1);fraction=.375;SaveStage("passed");await Capture("passed");Advance(Final);fraction=0;await Capture("final");Check(Session.GetActorView(observer).Location.Site==new EntityId("site:okazaki"),"physical arrival in Okazaki");}
        System.IO.File.WriteAllText(output+"/"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));GD.Print("RIVER_CHECK_PASSED "+checks.Count);GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
