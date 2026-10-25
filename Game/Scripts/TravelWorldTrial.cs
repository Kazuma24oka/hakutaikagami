using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public partial class TravelWorldTrial : Control
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
    readonly string output=ProjectSettings.GlobalizePath("res://../Docs/Design/TravelWorldV1/runtime-local");
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>new[]{"--travel-check","--travel-restore","--travel-video"}.Contains(a))??"";
    string Scenario=>"travel";
    long Final=>20000000;
    readonly Dictionary<string,MeshInstance3D> courierMarkers=new();
    readonly Dictionary<string,Officer3DCustomActor> courierActors=new();
    readonly Dictionary<EntityId,Officer3DCustomActor> nearbyActors=new();
    string destination="shuri";
    string SaveRoot{get{if(!testing)return ProjectSettings.GlobalizePath("user://travel-world-v1-manual");var text=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--save-key="))[11..];if(!Guid.TryParseExact(text,"N",out var key))throw new Exception("Invalid test key");return output+"/saves/"+key.ToString("N");}}
    public override void _Ready()
    {
        testing=Mode!="";var initial=TravelWorld.Create();host=new(new(initial));store=new(SaveRoot,TravelWorld.Create().Definitions);
        catalog=JapanGeography.Read(FileAccess.GetFileAsBytes("res://Data/Geography/japan-map-v1.json"));
        Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var e in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+e,16);
        var col=new VBoxContainer();margin.AddChild(col);col.AddChild(new Label{Text="百代鏡 / 到着地から次の旅へ・港で出航を待つ"});
        col.AddChild(new Label{Text="戦国期の位置関係を参考にした概略網。黄：陸路／青：海路。距離・道筋・所要は推定です。",Modulate=new Color("dfbf80")});
        var bar=new HFlowContainer();col.AddChild(bar);if(Mode=="--travel-video")bar.Visible=false;
        var destinations=new OptionButton();foreach(var site in NationalRoutes.Sites){destinations.AddItem(site.Name);destinations.SetItemMetadata(destinations.ItemCount-1,site.Id.Value);if(site.Id.Value=="site:shuri")destinations.Selected=destinations.ItemCount-1;}bar.AddChild(destinations);destinations.ItemSelected+=i=>{var site=new EntityId(destinations.GetItemMetadata((int)i).AsString());destination=RegionalCampaign.Destination(Session.DiagnosticSnapshot(),site);};
        Add(bar,"dispatch","本人が選んだ行先へ出発",()=>Go(destination));
        Add(bar,"hour","1時間進行",()=>Advance(Session.Seconds+3600));
        Add(bar,"step","1秒",()=>Advance(Session.Seconds+1));Add(bar,"ten","1日進行",()=>Advance(Session.Seconds+86400));Add(bar,"auto","自動進行",()=>automatic=!automatic);
        Add(bar,"speed","速度切替",()=>speed=speed==1?360:speed==360?10000:1);Add(bar,"pause","停止・再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:national-personal-travel-v1"),new(fraction,speed));notice.Text="位置・経路版・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{Load("manual");notice.Text="保存地点で停止して復旧しました";});
        Add(bar,"nation","全国",()=>span=2800000);Add(bar,"region","現在地 50km",()=>span=50000);Add(bar,"wide","現在地 100km",()=>span=100000);Add(bar,"near","本人 12m",()=>span=12);Add(bar,"rotate","回転",()=>yaw+=30);

        status=new Label();notice=new Label{Text="本人の行先を選んで出発。到着後はその地点から次の旅へ進めます。港は準備5分＋毎時便の試作です。"};col.AddChild(status);col.AddChild(notice);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,Mode=="--travel-video"?380:500)};col.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1200,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        surface.GuiInput+=e=>{if(e is InputEventMouseButton m&&m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)span=Mathf.Max(12,span/1.5f);if(m.ButtonIndex==MouseButton.WheelDown)span=Mathf.Min(2800000,span*1.5f);Refresh();}};
        world=new Node3D();viewport.AddChild(world);world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263d50"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.8f}});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.6f});
        foreach(var polygon in catalog.Polygons){AddShore(polygon.Outer,"7b9068",0);foreach(var hole in polygon.Holes)AddShore(hole,"263d50",.01f);}
        foreach(var site in NationalRoutes.Sites){var n=new Node3D();world.AddChild(n);n.AddChild(new MeshInstance3D{Mesh=new CylinderMesh{TopRadius=.22f,BottomRadius=.22f,Height=.3f},MaterialOverride=MapMaterial("d7b172"),Position=new(0,.15f,0)});n.AddChild(new Label3D{Text=site.Name,FontSize=26,PixelSize=.15f,Position=new(0,1.2f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true});markers.Add((n,new PublishedPoint(site.Id.Value,new(site.LatitudeE7,site.LongitudeE7))));}
        foreach(var edge in NationalRoutes.Edges){var mesh=new MeshInstance3D{Mesh=new BoxMesh(),MaterialOverride=new StandardMaterial3D{AlbedoColor=new("e2c17e"),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=false}};world.AddChild(mesh);roads.Add(mesh);}
        actor=new Officer3DCustomActor();world.AddChild(actor);camera=new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal};world.AddChild(camera);
        details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};col.AddChild(details);if(Mode=="--travel-video")details.Visible=false;var credit=new Label{Text="全国の概略接続。海岸線は現代の位置参照。所有・政治境界は未設定。蝦夷地南部・琉球も地理的な接続です。\n出典：文化庁 歴史の道／国立歴史民俗博物館 中世海道／座標CC0・推定位置。"+RegionalBranch.Catalog.Credit,AutowrapMode=TextServer.AutowrapMode.WordSmart};credit.AddThemeFontSizeOverride("font_size",Mode=="--travel-video"?9:12);col.AddChild(credit);Refresh();if(Mode=="--travel-video")CallDeferred(MethodName.BeginVideo);else if(testing)CallDeferred(MethodName.RunChecks);
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
        var view=Session.GetBranchView(observer);buttons["dispatch"].Disabled=TravelWorld.Active(Session.DiagnosticSnapshot())||Session.GetActorView(observer).Location.Site==RegionalCampaign.Site(destination);Rebase();
        var markerScale=Mathf.Max(1,span/160);foreach(var (node,site) in markers){node.Scale=Vector3.One*markerScale;node.GetChild<Label3D>(1).Visible=span>=2000&&(span<250000||new[]{"site:kyoto","site:edo","site:sannohe","site:kasugayama","site:yamaguchi","site:katsuyama","site:shuri"}.Contains(site.Id));}
        int index=0;foreach(var edge in NationalRoutes.Edges){var segment=VisibleLeg(edge);var mesh=roads[index++];mesh.Visible=segment.Visible;if(!segment.Visible)continue;var a=segment.A;var b=segment.B;((StandardMaterial3D)mesh.MaterialOverride).NoDepthTest=false;((StandardMaterial3D)mesh.MaterialOverride).AlbedoColor=new Color(view.KnownBlockedEdges.Contains(edge.Id)?"c6554d":NationalRoutes.Sea(edge.Id)?"55bce0":view.PlannedEdges.Contains(edge.Id)?"e2c17e":"b89b68");((BoxMesh)mesh.Mesh).Size=new(Mathf.Max(.8f,span/700),.04f,(b-a).Length());var layer=view.PlannedEdges.Contains(edge.Id)?2:view.KnownBlockedEdges.Contains(edge.Id)?1:0;mesh.Position=(a+b)/2+new Vector3(0,.03f+(layer+1)*Mathf.Max(.02f,span/10000),0);mesh.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        var dir=new Vector3(view.Heading.EastMm,0,-view.Heading.NorthMm);actor.Rotation=new(0,Mathf.Atan2(dir.X,dir.Z),0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;clipTime=view.Walking?(RenderDistance()/1000.0/actor.WalkStride*length)%length:(Session.Seconds+fraction)%length;actor.Freeze(clip,clipTime);actor.Visible=span<=80&&!OnSea();
        camera.Size=span;camera.Near=Mathf.Max(.01f,span/1000);camera.Far=span*10;var target=span>1000000?At(new(330000000,1380000000)):Vector3.Zero;var angle=Mathf.DegToRad(yaw);camera.Position=target+new Vector3(Mathf.Sin(angle)*.5f,.8660254f,Mathf.Cos(angle)*.5f)*span*2;camera.LookAt(target);
        foreach(var m in courierMarkers.Values)m.Visible=false;foreach(var a in courierActors.Values)a.Visible=false;
        foreach(var c in Session.GetCourierView(observer).Where(c=>c.Position!=null)){
            if(!courierMarkers.TryGetValue(c.Attempt,out var marker)){marker=new MeshInstance3D{Mesh=new SphereMesh{Radius=.28f,Height=.56f},MaterialOverride=MapMaterial("72d8e8")};world.AddChild(marker);courierMarkers[c.Attempt]=marker;var model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset("veteran"));courierActors[c.Attempt]=model;}
            marker.Visible=span>80;marker.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(0,.5f,0);marker.Scale=Vector3.One*Mathf.Max(1,span/300);
            var courier=courierActors[c.Attempt];courier.Visible=span<=80&&!OnSea();courier.Position=AtRoute(new(c.Position!.EastMm+(long)((c.NextPosition!.EastMm-c.Position.EastMm)*fraction),c.Position.NorthMm+(long)((c.NextPosition.NorthMm-c.Position.NorthMm)*fraction)))+new Vector3(.65f,0,0);var walking=c.Status.StartsWith("移動");var name=walking?"walk":"idle";var duration=courier.Player.GetAnimation(courier.Clips[name]).Length;courier.Freeze(name,walking?((c.DistanceMm!.Value+c.StepDistanceMm!.Value*fraction)/1000.0/courier.WalkStride*duration)%duration:(Session.Seconds+fraction)%duration);if(c.Heading!=null)courier.Rotation=new(0,Mathf.Atan2(c.Heading.EastMm,-c.Heading.NorthMm),0);
        }
        foreach(var model in nearbyActors.Values)model.Visible=false;
        foreach(var other in Session.GetNearbyActors(observer)){
            if(!nearbyActors.TryGetValue(other.Actor,out var model)){model=new Officer3DCustomActor();world.AddChild(model);model.Apply(Officer3DCustomActor.Preset(other.Actor==ContractFixture.Lord?"commander":other.Actor==ContractFixture.Officer?"red_guard":"veteran"));nearbyActors[other.Actor]=model;}
            model.Visible=span<=80&&!OnSea();model.Position=AtRoute(new(other.Position.EastMm+(long)((other.NextPosition.EastMm-other.Position.EastMm)*fraction),other.Position.NorthMm+(long)((other.NextPosition.NorthMm-other.Position.NorthMm)*fraction)))+new Vector3(1.3f+Array.FindIndex(Session.GetNearbyActors(observer),x=>x.Actor==other.Actor)*1.3f,0,0);model.Rotation=new(0,Mathf.Atan2(other.Heading.EastMm,-other.Heading.NorthMm),0);var animation=other.Walking?"walk":"idle";var modelDuration=model.Player.GetAnimation(model.Clips[animation]).Length;model.Freeze(animation,other.Walking?((other.DistanceMm+other.StepDistanceMm*fraction)/1000.0/model.WalkStride*modelDuration)%modelDuration:(Session.Seconds+fraction)%modelDuration);
        }
        var geo=JapanGeography.Inverse(origin);status.Text=$"世界時間 {Session.Seconds/86400}日 {Session.Seconds%86400/3600}時 {Session.Seconds%3600/60}分 / {(Session.Paused?"停止":"進行可能")} / {speed}倍 / {(automatic?"自動":"手動")} / {(observer==ContractFixture.Lord?"主君":observer==ContractFixture.Clerk?"書役本人":"奉行本人")}";
        if(Mode=="--travel-video")status.Text=$"世界時間 {Session.Seconds/86400}日 {Session.Seconds%86400/3600}時 {Session.Seconds%3600/60}分 / デモ収録（早送り区間あり） / 奉行本人";
        var letter=Session.GetActorView(observer).Messages.Any(m=>m.Sealed)?"未開封の書状あり":"未開封の書状なし";details.Text=$"参考位置（概略）北緯{geo.LatitudeE7/1e7:0.000} 東経{geo.LongitudeE7/1e7:0.000} / 進行{view.DistanceMm/1000000.0:N1}km / {(view.Walking?(OnSea()?"海上移動":"徒歩移動"):"待機")}\n"+string.Join(" / ",view.Knowledge.Tasks.Select(t=>TaskText(t.KnownStatus)))+" / "+letter+"\n既知の通行止め："+(view.KnownBlockedEdges.Length==0?"情報なし":string.Join(" / ",view.KnownBlockedEdges.Select(EdgeName)))+" / 経路（通過済みを含む）："+string.Join(" ／ ",view.PlannedEdges.Select(EdgeName))+"\n"+(observer==ContractFixture.Lord?"配下の現在位置は表示しません。封鎖の情報も報告を受領してから判明します。":"全国の接続は推定。出航は試作便：準備5分＋毎時出航。天候・運休・領土は未設定。");
        var w=Session.DiagnosticSnapshot();var leg=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==observer)?.Legs.FirstOrDefault(l=>Session.Seconds<l.EndAt);if(leg!=null&&Session.Seconds<TravelWorld.Departure(w,leg.Edge,leg.StartAt))details.Text+=$"\n出航待ち：あと{TravelWorld.Departure(w,leg.Edge,leg.StartAt)-Session.Seconds}秒（準備5分＋毎時便・試作値）";
        details.Text+="\n付近の人物："+string.Join(" ／ ",Session.GetNearbyActors(observer).Select(n=>w.Definitions.Characters.Single(c=>c.Id==n.Actor).Name+"（"+string.Join("・",w.Memberships.Where(m=>m.Subject==n.Actor).Select(m=>w.Definitions.Organizations.Single(o=>o.Id==m.Target).Name))+"）"));
        DrawVessel();CourierDetails();
    }
    void CourierDetails(){details.Text+="\n伝令（手元の情報・近傍の目撃のみ）："+string.Join(" ／ ",Session.GetCourierView(observer).Select(c=>(NationalRoutes.Nodes.Any(n=>n.Id==c.Destination)?NationalRoutes.Sites.Single(s=>s.Id==RegionalCampaign.Site(c.Destination)).Name:c.Destination)+"："+c.Status));}
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception(r.VisibleReason);}
    void Add(Container c,string id,string title,Action action){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){notice.Text="操作を完了できませんでした："+e.Message;if(testing)throw;}};}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    void Advance(long t){fraction=0;Session.AdvanceTo(t);Refresh();}
    public override void _Process(double delta){if(Mode=="--travel-video"&&videoReady)VideoFrame();else if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta){if(automatic&&!Session.Paused){fraction+=Math.Min(delta,.25)*speed;var n=(long)fraction;if(n>0){Session.AdvanceTo(Session.Seconds+n);fraction=Session.Paused?0:fraction-n;}Refresh();}}
    void Load(string slot){var p=store.Prepare(slot);var old=Session;if(host.CommitLoad(p,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load failed");fraction=p.Presentation.DisplayFraction;speed=p.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Seconds=Session.Seconds,Observer=observer.Value,Origin=origin,Fraction=fraction,Span=span,Heading=actor.Rotation.Y,Clip=clip,Time=clipTime,Details=details.Text,Couriers=Session.GetCourierView(observer),Nearby=Session.GetNearbyActors(observer),Position=Session.GetBranchView(observer).Position,Distance=Session.GetBranchView(observer).DistanceMm,Plan=Session.GetBranchView(observer).PlannedEdges,Known=Session.GetBranchView(observer).KnownBlockedEdges});
    void Check(bool v,string text){if(!v)throw new Exception(text);checks.Add(text);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(GetViewport().GetTexture().GetImage().SavePng(output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void SaveStage(string stage){Refresh();store.Save(Session,Scenario+"-"+stage,new("world:national-personal-travel-v1"),new(fraction,speed));System.IO.File.WriteAllText(output+"/"+Scenario+"-"+stage+"-pose.json",Pose());}
    MeshInstance3D? vessel,journeyPin;
    bool OnSea(){var j=Session.DiagnosticSnapshot().Branches!.Journeys.LastOrDefault(j=>j.Actor==observer);return j!=null&&NationalRoutes.Sea((j.Legs.FirstOrDefault(l=>Session.Seconds<l.EndAt)??j.Legs[^1]).Edge)&&Session.GetBranchView(observer).Walking;}
    void DrawVessel(){if(vessel==null){vessel=new MeshInstance3D{Mesh=new BoxMesh{Size=new(1.1f,.4f,3)},MaterialOverride=MapMaterial("e1b478")};world.AddChild(vessel);}if(journeyPin==null){journeyPin=new MeshInstance3D{Mesh=new SphereMesh{Radius=.6f,Height=1.2f},MaterialOverride=MapMaterial("ffdf70")};world.AddChild(journeyPin);}journeyPin.Visible=span>80;journeyPin.Position=new(0,Mathf.Max(.5f,span/1000),0);journeyPin.Scale=Vector3.One*Mathf.Max(1,span/400);vessel.Visible=span<=80&&OnSea();vessel.Position=new(0,.3f,0);vessel.Rotation=actor.Rotation;}
    bool videoReady;int videoFrame;long portAt,departAt,seaEnd;double movieClock;
    void Go(string node){Submit(TravelWorld.Move(Session.DiagnosticSnapshot(),"request:trip/"+Session.DiagnosticSnapshot().Requests.Length,node));}
    void FirstTrip(){destination="sakai";Click("dispatch");Advance(Session.Seconds+1);}
    void Arrive(){Advance(Session.DiagnosticSnapshot().Branches!.Journeys.Last().Legs[^1].EndAt);}
    void SecondTrip(){destination="sumoto";Click("dispatch");Advance(Session.Seconds+1);var l=Session.DiagnosticSnapshot().Branches!.Journeys.Last().Legs.First(l=>NationalRoutes.Sea(l.Edge));portAt=l.StartAt;departAt=TravelWorld.Departure(Session.DiagnosticSnapshot(),l.Edge,l.StartAt);seaEnd=l.EndAt;}
    void FinishTour(){Session.SetPaused(false);if(Session.DiagnosticSnapshot().Branches!.Journeys.Length==1)SecondTrip();if(Session.DiagnosticSnapshot().Branches!.Journeys.Length==2){Arrive();Go("castle");Advance(Session.Seconds+1);}Advance(Final);fraction=0;span=70;}
    void BeginVideo(){System.IO.Directory.CreateDirectory(output);FirstTrip();span=2800000;notice.AddThemeFontSizeOverride("font_size",20);videoReady=true;}
    void VideoFrame(){int f=videoFrame++;if(f<120){span=2800000;notice.Text="① 本人が清洲から堺へ。全国と現地で同じ世界時間";}
        else if(f<240){span=100000;notice.Text="② 堺へ到着。次の出発地は清洲ではなく現在地の堺";if(f==120)Arrive();}
        else if(f<336){span=12;notice.Text="③ 堺から淡路へ。港で乗船準備5分＋毎時の出航を待つ（試作便）";if(f==240){SecondTrip();Advance(portAt);movieClock=Session.Seconds;}movieClock+=1.0/24;Advance((long)movieClock);fraction=movieClock-Math.Floor(movieClock);}
        else if(f<432){span=100000;notice.Text="④ 出航時刻になって海上へ。同じ時計で船が進む";if(f==336)Advance(departAt+100);}
        else if(f<528){span=12;notice.Text="⑤ 海上で保存 → 同じ位置・時刻で復旧";if(f==432){fraction=.375;SaveStage("video");movieClock=Session.Seconds+.375;}if(f==480){Load("travel-video");Session.SetPaused(false);movieClock=Session.Seconds+fraction;}movieClock+=1.0/24;Advance((long)movieClock);fraction=movieClock-Math.Floor(movieClock);}
        else{span=2800000;notice.Text="⑥ 淡路到着後に清洲へ帰る。複数の旅を保存して継続";if(f==528)FinishTour();}
        Refresh();if(f>=623){GD.Print("TRAVEL_VIDEO_PASSED 624 frames");GetTree().Quit();}}
    async void RunChecks(){try{System.IO.Directory.CreateDirectory(output);var stages=new[]{"arrival","port","sea","island"};
        if(Mode.EndsWith("restore")){foreach(var stage in stages){span=70;Load("travel-"+stage);Check(Session.Paused,"restore paused "+stage);Check(Pose()==System.IO.File.ReadAllText(output+"/travel-"+stage+"-pose.json"),"restore exact view "+stage);await Capture("restored-"+stage);Session.SetPaused(false);FinishTour();await Capture("continued-"+stage);}}
        else{span=2800000;await Capture("nation");Check(Session.GetNearbyActors(observer).Any(n=>n.Actor==new EntityId("person:nobunaga")),"sourced Nobunaga is local at initial home");Check(!Session.GetNearbyActors(observer).Any(n=>n.Actor==new EntityId("person:shingen")),"remote historical person not visible");FirstTrip();Arrive();span=70;fraction=.375;SaveStage("arrival");await Capture("arrival");SecondTrip();Advance(portAt);Check(!Session.GetBranchView(observer).Walking,"port waiting uses idle pose");fraction=.375;SaveStage("port");await Capture("port");Advance(departAt+100);Check(OnSea(),"departure changes from officer to vessel");fraction=.375;SaveStage("sea");await Capture("sea");Arrive();fraction=.375;SaveStage("island");await Capture("island");FinishTour();await Capture("final");Check(Session.GetActorView(observer).Location.Site==ContractFixture.Castle,"three journeys return to Kiyosu");Check(Session.DiagnosticSnapshot().Branches!.Journeys.Length==3,"all three journeys retained");}
        System.IO.File.WriteAllText(output+"/"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));GD.Print("TRAVEL_CHECK_PASSED "+checks.Count);GetTree().Quit();}catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
}
