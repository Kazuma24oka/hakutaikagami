using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class FieldWorldTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;WorldSession Session=>host.Current;
    JapanCatalog catalog=null!;Node3D world=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;Officer3DCustomActor actor=null!;
    readonly List<(MeshInstance3D Mesh,GeoCoordinate[] Ring,int[] Indices,float Y)> shores=new();
    sealed record PublishedPoint(string Id,GeoCoordinate Coordinate);
    readonly List<(Node3D Node,PublishedPoint Site)> markers=new();readonly List<MeshInstance3D> roads=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    GeoMillimeters origin=new(0,0);float span=2800000,yaw;double fraction;int speed=1;bool testing,automatic;
    EntityId observer=ContractFixture.Officer;EntityId? applied;string clip="idle";double clipTime;
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/FieldWorldV1/runtime-local");
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>new[]{"--field-check","--field-restore","--field-video"}.Contains(a))??"";
    string Scenario=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--case="))?[7..]??"port";
    OptionButton destinations=null!;
    long Final=>20000000;
    readonly Dictionary<string,MeshInstance3D> courierMarkers=new();
    readonly Dictionary<string,Officer3DCustomActor> courierActors=new();
    readonly Dictionary<EntityId,Officer3DCustomActor> nearbyActors=new();
    string destination="sumoto";
    string SaveRoot{get{if(!testing)return ProjectSettings.GlobalizePath("user://field-world-v1-manual");var text=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..];if(!Guid.TryParseExact(text,"N",out var key))throw new Exception("Invalid test key");return output+"/saves/"+key.ToString("N");}}
    public override void _Ready()
    {
        testing=Mode!="";var initial=FieldWorld.Create();host=new(new(initial));store=new(SaveRoot,FieldWorld.Create().Definitions);span=100;
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 陸上の道外移動・自由な布陣"});
        col.AddChild(new Label{Text="同じ全国座標と世界時間。清洲近傍160mの検証区画（地形は合成）。分隊の代表を表示しています。",Modulate=new Color("dfbf80")});
        var bar=new HFlowContainer();col.AddChild(bar);if(Mode=="--field-video")bar.Visible=false;
        Add(bar,"formation","森・斜面へ布陣",()=>Order(new(25000,15000),new(65000,35000)));
        Add(bar,"roadline","道沿いへ布陣",()=>Order(new(5000,5000),new(65000,5000)));
        Add(bar,"return","帰着・陣形を戻す",()=>Order(new(5000,5000),new(5000,25000)));
        Add(bar,"step","1秒",()=>Advance(Session.Seconds+1));Add(bar,"ten","1日進行",()=>Advance(Session.Seconds+86400));Add(bar,"auto","自動進行",()=>automatic=!automatic);
        Add(bar,"speed","速度切替",()=>speed=speed==1?360:speed==360?10000:1);Add(bar,"pause","停止・再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:national-land-field-v1"),new(fraction,speed));notice.Text="位置・経路版・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{Load("manual");notice.Text="保存地点で停止して復旧しました";});
        Add(bar,"nation","全国",()=>span=2800000);Add(bar,"region","現在地 50km",()=>span=50000);Add(bar,"wide","現在地 100km",()=>span=100000);Add(bar,"near","本人 12m",()=>span=12);Add(bar,"rotate","回転",()=>yaw+=30);

        status=new Label();notice=new Label{Text="地面を左ドラッグして布陣線を指定。自隊3分隊のみを動かします。薄茶：道／緑：平地・森／橙：斜面／青・黒：通行不可。"};col.AddChild(status);col.AddChild(notice);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,Mode=="--field-video"?380:500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)span=Mathf.Max(12,span/1.5f);if(m.ButtonIndex==MouseButton.WheelDown)span=Mathf.Min(2800000,span*1.5f);Refresh();}};
        world=new Node3D();viewport.AddChild(world);world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in NationalRoutes.Sites){var n=new Node3D();world.AddChild(n);n.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)});n.AddChild(new Label3D{Text=site.Name,FontSize=26,PixelSize=.15f,Position=new(0,1.2f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true});markers.Add((n,new PublishedPoint(site.Id.Value,new(site.LatitudeE7,site.LongitudeE7))));}
        foreach(var edge in NationalRoutes.Edges){var mesh=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e2c17e"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=false}};world.AddChild(mesh);roads.Add(mesh);}
        actor=new Officer3DCustomActor();world.AddChild(actor);camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);if(Mode=="--field-video")details.Visible=false;var credit=new Label{Text="全国の概略接続。海岸線は現代の位置参照。所有・政治境界は未設定。蝦夷地南部・琉球も地理的な接続です。\n出典：文化庁 歴史の道／国立歴史民俗博物館 中世海道／座標CC0・推定位置。"+RegionalBranch.Catalog.Credit,AutowrapMode=TextServer.AutowrapMode.WordSmart};credit.AddThemeFontSizeOverride("font_size",Mode=="--field-video"?9:12);col.AddChild(credit);BuildField();Refresh();if(Mode=="--field-video")CallDeferred(MethodName.BeginVideo);else if(testing)CallDeferred(MethodName.RunChecks);
    }
    RoutePoint Sample(){var v=Session.GetBranchView(observer);return new(v.Position.EastMm+(long)((v.NextPosition.EastMm-v.Position.EastMm)*fraction),v.Position.NorthMm+(long)((v.NextPosition.NorthMm-v.Position.NorthMm)*fraction));}
    double RenderDistance(){var v=Session.GetBranchView(observer);return (v.DistanceMm+v.StepDistanceMm*fraction)/8;}
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
        var a=JapanGeography.Relative(new(NationalRoutes.Nodes.Single(n=>n.Id==edge.From).Position.EastMm,NationalRoutes.Nodes.Single(n=>n.Id==edge.From).Position.NorthMm),origin);var b=JapanGeography.Relative(new(NationalRoutes.Nodes.Single(n=>n.Id==edge.To).Position.EastMm,NationalRoutes.Nodes.Single(n=>n.Id==edge.To).Position.NorthMm),origin);
        double dx=b.EastMeters-a.EastMeters,dz=b.NorthMeters-a.NorthMeters,length=Math.Sqrt(dx*dx+dz*dz);
        double mid=-(a.EastMeters*dx+a.NorthMeters*dz)/(length*length),radius=span*4/length;
        double lo=Math.Max(0,mid-radius),hi=Math.Min(1,mid+radius);if(lo>hi)return(Vector3.Zero,Vector3.Zero,false);
        return(new((float)(a.EastMeters+dx*lo),0,-(float)(a.NorthMeters+dz*lo)),new((float)(a.EastMeters+dx*hi),0,-(float)(a.NorthMeters+dz*hi)),true);
    }
    static string EdgeName(string id)=>id.StartsWith("nation-",StringComparison.Ordinal)?NationalRoutes.Name(id)+(NationalRoutes.Sea(id)?"（海路・推定）":"（陸路・推定）"):id switch{"futamura-remnant"=>"二村山旧道（公表約200m・仮線形）","futamura-access-preview"=>"二村山への接続（検証用仮線）","road"=>"清洲～熱田","pass"=>"熱田～善照寺（直通）","kasadera-access"=>"熱田～笠寺","kasadera-tange"=>"笠寺～丹下","tange-fort"=>"丹下～善照寺","narumi-access"=>"善照寺～鳴海道筋（推定）","bridge-approach"=>"橋の北岸へ（推定）","nakajima-crossing"=>"中島橋周辺の渡河（推定）","mikawa-approach"=>"岡崎方面（概略）","ford-approach"=>"仮設渡しへ（検証用）","temporary-ford"=>"仮設渡しで渡河（検証用）","ford-return"=>"南岸の道へ（検証用）","mino-concept"=>"清洲～稲葉山（概略・渡河未復元）","mikawa-concept"=>"善照寺～岡崎（概略・渡河未復元）",_=>"不明な経路"};
    static string TaskText(string text)=>text.Replace("Running","実行中").Replace("Completed","完了").Replace("Stopped","停止");
    void Refresh()
    {
        if(applied!=observer){actor.Apply(Officer3DCustomActor.Preset(observer==ContractFixture.Lord?"commander":observer==ContractFixture.Clerk?"veteran":"red_guard"));applied=observer;}
        var view=Session.GetBranchView(observer);Rebase();
                var markerScale=Mathf.Max(1,span/160);foreach(var (node,site) in markers){node.Scale=Vector3.One*markerScale;node.GetChild<Label3D>(1).Visible=span>=2000&&(span<250000||new[]{"site:kyoto","site:edo","site:sannohe","site:kasugayama","site:yamaguchi","site:katsuyama","site:shuri"}.Contains(site.Id));}
        int index=0;foreach(var edge in NationalRoutes.Edges){var segment=VisibleLeg(edge);var mesh=roads[index++];mesh.Visible=segment.Visible;if(!segment.Visible)continue;var a=segment.A;var b=segment.B;((StandardMaterial3D)mesh.MaterialOverride).NoDepthTest=false;((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor=new Color(view.KnownBlockedEdges.Contains(edge.Id)?"c6554d":NationalRoutes.Sea(edge.Id)?"55bce0":view.PlannedEdges.Contains(edge.Id)?"e2c17e":"b89b68");((BoxMesh)mesh.Mesh).Size=new(Mathf.Max(.8f,span/700),.04f,(b-a).Length());var layer=view.PlannedEdges.Contains(edge.Id)?2:view.KnownBlockedEdges.Contains(edge.Id)?1:0;mesh.Position=(a+b)/2+new Vector3(0,.03f+(layer+1)*Mathf.Max(.02f,span/10000),0);mesh.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        var dir=new Vector3(view.Heading.EastMm,0,-view.Heading.NorthMm);actor.Rotation=new(0,Mathf.Atan2(dir.X,dir.Z),0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;clipTime=view.Walking?(RenderDistance()/1000.0/actor.WalkStride*length)%length:(Session.Seconds+fraction)%length;actor.Freeze(clip,clipTime);actor.Visible=span<=80&&!OnSea();
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;var target=span>1000000?At(new(330000000,1380000000)):Vector3.Zero;var angle=Mathf.DegToRad(yaw);camera.Position=target+new Vector3(Mathf.Sin(angle)*.5f,.8660254f,Mathf.Cos(angle)*.5f)*span*2;camera.LookAt(target);
        foreach(var m in courierMarkers.Values)m.Visible=false;foreach(var a in courierActors.Values)a.Visible=false;
        foreach(var c in Session.GetCourierView(observer).Where(c=>c.Position!=null&&!Session.DiagnosticSnapshot().Messaging.Messages.Any(m=>m.Id==c.Message&&m.SenderId==m.RecipientId))){
            if(!courierMarkers.TryGetValue(c.Attempt,out var marker)){marker=new MeshInstance3D{Mesh=new SphereMesh{Radius=.28f,Height=.56f},MaterialOverride=MapMaterial("72d8e8")};world.AddChild(marker);courierMarkers[c.Attempt]=marker;var model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset("veteran"));courierActors[c.Attempt]=model;}
            marker.Visible=span>80;marker.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(0,.5f,0);marker.Scale=Vector3.One*Mathf.Max(1,span/300);
            var courier=courierActors[c.Attempt];courier.Visible=span<=80&&!OnSea();courier.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(.65f,0,0);var walking=c.Status.StartsWith("移動");var name=walking?"walk":"idle";var duration=courier.Player.GetAnimation(courier.Clips[name]).Length;courier.Freeze(name,walking?((c.DistanceMm!.Value+c.StepDistanceMm!.Value*fraction)/1000.0/courier.WalkStride*duration)%duration:(Session.Seconds+fraction)%duration);if(c.Heading!=null)courier.Rotation=new(0,Mathf.Atan2(c.Heading.EastMm,-c.Heading.NorthMm),0);
        }
        foreach(var model in nearbyActors.Values)model.Visible=false;
        foreach(var other in Session.GetNearbyActors(observer)){
            if(!nearbyActors.TryGetValue(other.Actor,out var model)){model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset(other.Actor==ContractFixture.Lord?"commander":other.Actor==ContractFixture.Officer?"red_guard":"veteran"));nearbyActors[other.Actor]=model;}
            model.Visible=span<=80&&!OnSea();model.Position=AtRoute(new(other.Position.EastMm+(long)((other.NextPosition.EastMm-other.Position.EastMm)*fraction),other.Position.NorthMm+(long)((other.NextPosition.NorthMm-other.Position.NorthMm)*fraction)))+new Vector3(1.3f+Array.FindIndex(Session.GetNearbyActors(observer),x=>x.Actor==other.Actor)*1.3f,0,0);model.Rotation=new(0,Mathf.Atan2(other.Heading.EastMm,-other.Heading.NorthMm),0);var animation=other.Walking?"walk":"idle";var modelDuration=model.Player.GetAnimation(model.Clips[animation]).Length;model.Freeze(animation,other.Walking?((other.DistanceMm+other.StepDistanceMm*fraction)/1000.0/model.WalkStride*modelDuration)%modelDuration:(Session.Seconds+fraction)%modelDuration);
        }
        status.Text=$"世界時間 {Session.Seconds}秒 / {(Session.Paused?"停止":"進行可能")} / {speed}倍";
        var w=Session.DiagnosticSnapshot();details.Text="検証用の地形（史実の地形ではありません）。道2m/s、平地1m/s、森林0.5m/s、斜面0.4m/s。水面・崖は通行不可。\n"+string.Join(" ／ ",Enumerable.Range(0,3).Select(i=>{var u=FieldWorld.Sample(w,i,Session.Seconds);return $"分隊{i+1}：{u.Terrain} / {(u.Moving?"移動中":"整列・待機")}";}))+"\n布陣線は任意座標。探索は10mセルの中継点を使い、目標点はセル中心へ丸めません。";
        DrawField();

    }
    void CourierDetails(){details.Text+="\n伝令（手元の情報・近傍の目撃のみ）："+string.Join(" ／ ",Session.GetCourierView(observer).Select(c=>(NationalRoutes.Nodes.Any(n=>n.Id==c.Destination)?NationalRoutes.Sites.Single(s=>s.Id==RegionalCampaign.Site(c.Destination)).Name:c.Destination)+"："+c.Status));}
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception(r.VisibleReason);}
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){notice.Text="操作を完了できませんでした："+e.Message;if(testing)interactionError=e;}};}
    Exception? interactionError;
    void Click(string id){interactionError=null;buttons[id].EmitSignal(Button.SignalName.Pressed);if(interactionError!=null)throw new Exception("Game button failed: "+id,interactionError);}
    void Advance(long t){fraction=0;Session.AdvanceTo(t);Refresh();}
    public override void _Process(double delta){if(Mode=="--field-video"&&videoReady)VideoFrame();else if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta){if(automatic&&!Session.Paused){fraction+=Math.Min(delta,.25)*speed;var n=(long)fraction;if(n>0){Session.AdvanceTo(Session.Seconds+n);fraction=Session.Paused?0:fraction-n;}Refresh();}}
    void Load(string slot){var p=store.Prepare(slot);var old=Session;if(host.CommitLoad(p,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load failed");fraction=p.Presentation.DisplayFraction;speed=p.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Seconds=Session.Seconds,Observer=observer.Value,Origin=origin,Fraction=fraction,Span=span,Heading=actor.Rotation.Y,Clip=clip,Time=clipTime,Details=details.Text,Decisions=Session.DiagnosticSnapshot().TravelDecisions,Stops=Session.DiagnosticSnapshot().TrafficStops,Field=Session.DiagnosticSnapshot().Field,Couriers=Session.GetCourierView(observer),Nearby=Session.GetNearbyActors(observer),Position=Session.GetBranchView(observer).Position,Distance=Session.GetBranchView(observer).DistanceMm,Plan=Session.GetBranchView(observer).PlannedEdges,Known=Session.GetBranchView(observer).KnownBlockedEdges});
    void Check(bool v,string text){if(!v)throw new Exception(text);checks.Add(text);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+Scenario+"-"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void SaveStage(string stage){Refresh();store.Save(Session,Scenario+"-"+stage,new("world:national-land-field-v1"),new(fraction,speed));System.IO.File.WriteAllText(output+"/"+Scenario+"-"+stage+"-pose.json",Pose());}
    MeshInstance3D? vessel,journeyPin;
    bool OnSea(){var j=Session.DiagnosticSnapshot().Branches!.Journeys.LastOrDefault(j=>j.Actor==observer);return j!=null&&NationalRoutes.Sea((j.Legs.FirstOrDefault(l=>Session.Seconds<l.EndAt)??j.Legs[^1]).Edge)&&Session.GetBranchView(observer).Walking;}
    void DrawVessel(){if(vessel==null){vessel=new MeshInstance3D{Mesh=new BoxMesh{Size=new(1.1f,.4f,3)},MaterialOverride=MapMaterial("e1b478")};world.AddChild(vessel);}if(journeyPin==null){journeyPin=new MeshInstance3D{Mesh=new SphereMesh{Radius=.6f,Height=1.2f},MaterialOverride=MapMaterial("ffdf70")};world.AddChild(journeyPin);}journeyPin.Visible=span>80;journeyPin.Position=new(0,Mathf.Max(.5f,span/1000),0);journeyPin.Scale=Vector3.One*Mathf.Max(1,span/400);vessel.Visible=span<=80&&OnSea();vessel.Position=new(0,.3f,0);vessel.Rotation=actor.Rotation;}
    readonly List<(MeshInstance3D Mesh,FieldPoint Point)> tiles=new();readonly List<Officer3DCustomActor> detachments=new();readonly List<Label> fieldLabels=new();readonly List<MeshInstance3D> fieldLines=new();FieldPoint? dragStart;
    void BuildField(){actor.Scale=Vector3.One*8;for(int i=0;i<3;i++){var label=new Label{Text=i==0?"本隊 40":i==1?"左分隊 30":"右分隊 30",MouseFilter=MouseFilterEnum.Ignore,Modulate=i==0?new Color("ffbf8b"):new Color("fff0b5")};label.AddThemeFontSizeOverride("font_size",18);AddChild(label);fieldLabels.Add(label);}for(int x=-6;x<=9;x++)for(int y=-6;y<=9;y++){var terrain=FieldWorld.Terrain(x,y);var color=terrain switch{"road"=>"cdbb86","grass"=>"82996d","forest"=>"3e7147","slope"=>"ba9763","water"=>"477eab",_=>"3a4146"};var tile=new MeshInstance3D{Mesh=new BoxMesh{Size=new(9.9f,terrain=="cliff"?2:.04f,9.9f)},MaterialOverride=MapMaterial(color)};world.AddChild(tile);tiles.Add((tile,new(x*10000L+5000,y*10000L+5000)));}for(int i=1;i<3;i++){var model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset(i==1?"veteran":"commander"));model.Scale=Vector3.One*8;detachments.Add(model);}viewport.GetParent<SubViewportContainer>().GuiInput+=e=>{if(e is InputEventMouseButton m&&m.ButtonIndex==MouseButton.Left){var container=viewport.GetParent<Control>();var pixel=m.Position*new Vector2(viewport.Size.X/container.Size.X,viewport.Size.Y/container.Size.Y);var a=camera.ProjectRayOrigin(pixel);var d=camera.ProjectRayNormal(pixel);if(Math.Abs(d.Y)<.0001)return;var p=a+d*(-a.Y/d.Y);var point=new FieldPoint((long)Math.Round(p.X*1000)+origin.EastMm-NationalRoutes.Nodes.Single(n=>n.Id=="castle").Position.EastMm+5000,(long)Math.Round(-p.Z*1000)+origin.NorthMm-NationalRoutes.Nodes.Single(n=>n.Id=="castle").Position.NorthMm+5000);if(m.Pressed)dragStart=point;else if(dragStart is {} start){dragStart=null;try{Order(start,point);Refresh();}catch(Exception ex){notice.Text=ex.Message;}}}};}
    void DrawField(){if(tiles.Count==0)return;foreach(var other in nearbyActors.Values)other.Visible=false;foreach(var t in tiles){t.Mesh.Position=AtRoute(FieldWorld.ToWorld(t.Point));t.Mesh.Visible=span<=500;}if(span<=500){foreach(var road in roads)road.Visible=false;foreach(var n in markers)n.Node.Visible=false;actor.Visible=true;}var w=Session.DiagnosticSnapshot();for(int i=1;i<3;i++){var u=FieldWorld.Sample(w,i,Session.Seconds);var p=new FieldPoint(u.Position.X+(long)((u.Next.X-u.Position.X)*fraction),u.Position.Y+(long)((u.Next.Y-u.Position.Y)*fraction));var model=detachments[i-1];model.Visible=span<=500;model.Position=AtRoute(FieldWorld.ToWorld(p));var dx=u.Next.X-u.Position.X;var dy=u.Next.Y-u.Position.Y;if(dx==0&&dy==0&&w.Field!.Orders.LastOrDefault()?.Routes[i].Legs.LastOrDefault() is {} last){dx=last.To.X-last.From.X;dy=last.To.Y-last.From.Y;}model.Rotation=new(0,dx==0&&dy==0?0:Mathf.Atan2(dx,-dy),0);var name=u.Moving?"walk":"idle";var length=model.Player.GetAnimation(model.Clips[name]).Length;model.Freeze(name,u.Moving?((u.Distance+Math.Sqrt((double)dx*dx+(double)dy*dy)*fraction)/1000/model.WalkStride/8*length)%length:(Session.Seconds+fraction)%length);}for(int i=0;i<3;i++){var u=FieldWorld.Sample(w,i,Session.Seconds);var anchor=AtRoute(FieldWorld.ToWorld(new(u.Position.X+(long)((u.Next.X-u.Position.X)*fraction),u.Position.Y+(long)((u.Next.Y-u.Position.Y)*fraction))));var screen=camera.UnprojectPosition(anchor);var container=viewport.GetParent<Control>();var labelPoint=container.GlobalPosition+screen*new Vector2(container.Size.X/viewport.Size.X,container.Size.Y/viewport.Size.Y)-new Vector2(fieldLabels[i].Size.X/2,28);fieldLabels[i].Position=new(Mathf.Round(labelPoint.X),Mathf.Round(labelPoint.Y));fieldLabels[i].Visible=span>=30&&span<=500;}foreach(var line in fieldLines)line.QueueFree();fieldLines.Clear();var order=w.Field!.Orders.LastOrDefault();if(order!=null){var a=AtRoute(FieldWorld.ToWorld(order.Request.A));var b=AtRoute(FieldWorld.ToWorld(order.Request.B));var line=new MeshInstance3D{Mesh=new BoxMesh{Size=new(.35f,.08f,(b-a).Length())},MaterialOverride=MapMaterial("ffdd72"),Position=(a+b)/2+new Vector3(0,.1f,0),Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0)};world.AddChild(line);fieldLines.Add(line);}}
    void Order(FieldPoint a,FieldPoint b){var r=Session.SetFormation(new(new("request:field/"+Session.DiagnosticSnapshot().Field!.Orders.Length),observer,a,b),Session.Revision);notice.Text=r.VisibleReason;if(testing&&r.Code!=ResultCode.Accepted)throw new Exception(r.VisibleReason);}
    void DragLine(FieldPoint a,FieldPoint b){var c=viewport.GetParent<Control>();Vector2 Pixel(FieldPoint p)=>camera.UnprojectPosition(AtRoute(FieldWorld.ToWorld(p)))*new Vector2(c.Size.X/viewport.Size.X,c.Size.Y/viewport.Size.Y);c.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=Pixel(a)});c.EmitSignal(Control.SignalName.GuiInput,new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=Pixel(b)});}
    long TerrainMoment(int unit,string terrain){var l=Session.DiagnosticSnapshot().Field!.Orders.Last().Routes[unit].Legs.First(l=>l.Terrain==terrain);return l.StartAt+(l.EndAt-l.StartAt)/2;}
    bool videoReady;int videoFrame;double movieClock;
    void BeginVideo(){System.IO.Directory.CreateDirectory(output);Click("formation");span=100;videoReady=true;}
    void VideoFrame(){int f=videoFrame++;if(f==0)movieClock=0;if(f==120)movieClock=7;if(f==240)movieClock=TerrainMoment(0,"forest");if(f==336)movieClock=TerrainMoment(2,"slope");if(f<432){movieClock+=1.0/24;Advance((long)movieClock);fraction=movieClock-Math.Floor(movieClock);}if(f==432){fraction=.375;SaveStage("video");}if(f==480){Load(Scenario+"-video");Session.SetPaused(false);}if(f==528)Advance(400);notice.Text=f<120?"① 道を外れ、任意の線へ自隊3分隊を布陣":f<240?"② 道は速く、道外の平地は減速":f<336?"③ 森ではさらに減速。水面・崖は迂回":f<432?"④ 斜面の分隊も同じ世界時間で移動":f<528?"⑤ 途中保存・復旧で位置と地形経路を保持":"⑥ 任意の布陣線へ整列。海路は従来の固定航路";Refresh();if(f>=623){GD.Print("FIELD_VIDEO_PASSED 624 frames");GetTree().Quit();}}
    async void RunChecks(){try{System.IO.Directory.CreateDirectory(output);var stages=new[]{"moving","forest","slope","arranged"};if(Mode.EndsWith("restore")){foreach(var stage in stages){span=100;Load(Scenario+"-"+stage);Check(Pose()==System.IO.File.ReadAllText(output+"/"+Scenario+"-"+stage+"-pose.json"),"restore exact field position and pose "+stage);await Capture("restored-"+stage);Session.SetPaused(false);Advance(400);fraction=0;await Capture("continued-"+stage);}}else{DragLine(new(25000,15000),new(65000,35000));Check(Session.DiagnosticSnapshot().Field!.Orders.Length==1,"ground drag actually creates formation");Check(Math.Abs(Session.DiagnosticSnapshot().Field!.Orders[0].Request.A.X-25000)<50,"drag projection preserves world coordinates");Click("formation");foreach(var item in new[]{("moving",7L),("forest",TerrainMoment(0,"forest")),("slope",TerrainMoment(2,"slope")),("arranged",200L)}){Advance(item.Item2);if(item.Item1=="forest")Check(FieldWorld.Sample(Session.DiagnosticSnapshot(),0,Session.Seconds).Terrain=="forest","actual moving sample in forest");if(item.Item1=="slope")Check(FieldWorld.Sample(Session.DiagnosticSnapshot(),2,Session.Seconds).Terrain=="slope","actual moving sample on slope");fraction=.375;SaveStage(item.Item1);await Capture(item.Item1);}Advance(400);fraction=0;await Capture("final");Check(Enumerable.Range(0,3).All(i=>!FieldWorld.Sample(Session.DiagnosticSnapshot(),i,400).Moving),"three detachments reach free formation slots");Check(Session.DiagnosticSnapshot().Field!.Orders.Last().Routes.SelectMany(r=>r.Legs).Any(l=>l.Terrain=="forest"),"forest slowdown actually used");Check(Session.DiagnosticSnapshot().Field!.Orders.Last().Routes.SelectMany(r=>r.Legs).Any(l=>l.Terrain=="slope"),"slope slowdown actually used");}System.IO.File.WriteAllText(output+"/"+Scenario+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));GD.Print("FIELD_CHECK_PASSED "+checks.Count);GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
