using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Simulation;

// One scene and world coordinates at all zooms. Battlefield facts come only from the observer read model.
public partial class FormationWorldStage : VBoxContainer
{
    public TaxPanel Session {get;private set;}=null!;
    public SubViewport View {get;private set;}=null!;
    public SubViewportContainer Surface {get;private set;}=null!;
    public Camera3D Camera {get;private set;}=null!;
    public Dictionary<string,Button> Actions {get;}=new();
    public Dictionary<string,Officer3DCustomActor> Actors {get;}=new();
    public Dictionary<string,MeshInstance3D> Markers {get;}=new();
    public MilitaryView? LastView {get;private set;}
    public MapCell DraftStart {get;private set;}=new(8,1);
    public MapCell DraftEnd {get;private set;}=new(8,5);
    public Label Notice {get;private set;}=null!;
    public Label Status {get;private set;}=null!;
    public Label Details {get;private set;}=null!;
    public string Observer {get;private set;}="character-001";
    public OptionButton Perspective {get;private set;}=null!;
    public float TargetZoom {get;private set;}=180;
    Node3D world=null!,lines=null!,focusFrame=null!;bool drawing,dragging;Vector2 dragStart;
    readonly Dictionary<string,Label3D> labels=new();readonly Dictionary<string,string> looks=new();
    readonly List<(Label3D Label,bool National)> placeLabels=new();
    public static Vector3 PositionOf(MapCell c)=>new((c.X-5)*1.5f,0,(c.Z-3)*1.5f);
    public void Initialize(TaxPanel session)
    {
        bool test=OS.GetCmdlineUserArgs().Any(a=>a is "--formation-world-test" or "--formation-world-restore");FormationWorldGate.Require(!test);Session=session;
        if(OS.GetCmdlineUserArgs().Contains("--formation-world-show"))DisplayServer.WindowSetSize(new(1600,1000));
        AddChild(new Label{Text="作戦線と部隊の展開 / 全国から同じ場所へ"});
        AddChild(new Label{Text="架空の全国概略図・3部隊の布陣試作。全国の進軍・歴史地理は未実装。",Modulate=new Color("d9c58c")});
        var bar=new HFlowContainer();AddChild(bar);
        Add(bar,"start","演習を配置",()=>Session.ChangeIntegration(s=>s.StartFormationMap()));
        Add(bar,"reset","初期化",()=>Session.InvokeFrom3D("reset"));
        foreach(var (id,title) in new[]{("advance","1刻進める"),("pause","停止 / 再開"),("save","通常保存"),("load","通常復旧")})Add(bar,id,title,()=>Session.InvokeFrom3D(id));
        Add(bar,"auto","自動進行 切替",()=>Session.Automatic=!Session.Automatic);
        foreach(int speed in new[]{1,2,4})Add(bar,"speed"+speed,speed+"倍",()=>Session.SetClockSpeed(speed));
        var control=new HFlowContainer();AddChild(control);
        Add(control,"nation","全国",()=>SetZoom(180));Add(control,"region","地域",()=>SetZoom(40));Add(control,"near","布陣を見る",()=>SetZoom(14));
        Add(control,"draw","線を引く（近景で左ドラッグ）",()=>{SetZoom(14);drawing=true;Notice.Text="近景で地面を左ドラッグして布陣線を指定してください。発送前に担当位置を確認できます。";});
        Add(control,"send","布陣命令を発送",()=>{Session.ChangeIntegration(s=>s.SendFormation("formation/"+s.Tick,"character-001",DraftStart,DraftEnd,[1,3,5]));drawing=false;});
        Perspective=new OptionButton();control.AddChild(Perspective);foreach(var c in Session.Simulation.RestoreWorld().Definition.Characters)Perspective.AddItem(c.Name+"の視点（検証）");
        Perspective.ItemSelected+=i=>{Observer="character-00"+(i+1);Synchronize();};
        Status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(Status);
        Notice=new Label{Text="演習を配置 → 布陣を見る → 線を引く → 発送 → 時刻を進める。初期の線も発送できます。",AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(Notice);
        var row=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};AddChild(row);
        Surface=new SubViewportContainer{Stretch=true,CustomMinimumSize=new(600,450),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,MouseFilter=MouseFilterEnum.Stop};row.AddChild(Surface);
        View=new SubViewport{OwnWorld3D=true,Size=new(1150,700),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};Surface.AddChild(View);world=new Node3D();View.AddChild(world);
        Surface.GuiInput+=OnMapInput;
        var environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("243b48"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.5f};
        environment.Sky=new Sky{SkyMaterial=new ProceduralSkyMaterial()};environment.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;
        world.AddChild(new WorldEnvironment{Environment=environment});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.4f,ShadowEnabled=true});
        BuildTerrain();lines=new Node3D();world.AddChild(lines);
        Camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=180,Near=.05f,Far=1000,Position=new(0,180,135),Current=true};world.AddChild(Camera);Camera.LookAt(Vector3.Zero);
        foreach(string id in new[]{"formation-0","formation-1","formation-2"})
        {
            var actor=new Officer3DCustomActor{Name=id,Visible=false};world.AddChild(actor);Actors[id]=actor;
            var marker=Box(world,new(.45f,.15f,.45f),new(0,.25f,0),"d6ad6e");marker.Visible=false;Markers[id]=marker;
            var label=new Label3D{FontSize=28,PixelSize=.009f,OutlineSize=6,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true,Visible=false};world.AddChild(label);labels[id]=label;
        }
        var scroll=new ScrollContainer{CustomMinimumSize=new(315,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(scroll);Details=new Label{CustomMinimumSize=new(300,0),AutowrapMode=TextServer.AutowrapMode.WordSmart};scroll.AddChild(Details);
        Session.WorldChanged+=_=>Synchronize();Synchronize();
    }
    void Add(Container parent,string id,string title,Action action)
    {var b=new Button{Text=title};parent.AddChild(b);Actions[id]=b;b.Pressed+=()=>{try{action();if(id=="send")Notice.Text="発送しました。伝令到達は左翼1刻・中央3刻・右翼5刻。到着結果は目撃または報告で確認します。";Synchronize();}catch(Exception e){Notice.Text="操作できません："+e.Message;GD.Print("FORMATION_INPUT_REJECTED "+e.GetType().Name);}};}
    static MeshInstance3D Box(Node3D parent,Vector3 size,Vector3 position,string color)
    {var m=new MeshInstance3D{Mesh=new BoxMesh{Size=size},Position=position,MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(color),Roughness=1}};parent.AddChild(m);return m;}
    static void Stroke(Node3D parent,Vector3 a,Vector3 b,string color,float width=.07f)
    {var d=b-a;var m=Box(parent,new(width,.05f,d.Length()),(a+b)/2,color);m.Rotation=new(0,Mathf.Atan2(d.X,d.Z),0);}
    void BuildTerrain()
    {
        // Fictional national context. The local terrain sits in this mesh at its unchanged world coordinates.
        var outline=new Vector2[]{new(-85,38),new(-65,15),new(-44,18),new(-30,-8),new(-12,-12),new(7,-35),new(30,-30),new(38,-65),new(64,-75),new(75,-54),new(57,-35),new(53,-12),new(31,2),new(19,20),new(-5,21),new(-25,38),new(-48,34),new(-70,55)};
        var mesh=new ImmediateMesh();mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);foreach(int i in Geometry2D.TriangulatePolygon(outline)){mesh.SurfaceSetNormal(Vector3.Up);mesh.SurfaceAddVertex(new(outline[i].X,-.2f,outline[i].Y));}mesh.SurfaceEnd();
        world.AddChild(new MeshInstance3D{Mesh=mesh,MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("5b7351"),CullMode=BaseMaterial3D.CullModeEnum.Disabled}});
        foreach(var (a,b) in new[]{(new Vector3(-65,-.05f,18),new Vector3(-7.5f,-.05f,0)),(new Vector3(7.5f,-.05f,0),new Vector3(50,-.05f,-40))})Stroke(world,a,b,"b6a47e",.6f);
        foreach(var (a,b) in new[]{(new Vector3(0,-.08f,-29),new Vector3(0,-.08f,-5.25f)),(new Vector3(0,-.08f,5.25f),new Vector3(12,-.08f,19))})Stroke(world,a,b,"39697f",1.4f);
        for(int x=0;x<MilitaryMap.Width;x++)for(int z=0;z<MilitaryMap.Height;z++)
        {
            var c=new MapCell(x,z);string color=MilitaryMap.Terrain(c) switch{MilitaryTerrain.River=>"39697f",MilitaryTerrain.Bridge=>"bb9965",MilitaryTerrain.Road=>"b6a47e",MilitaryTerrain.Rock=>"59615d",MilitaryTerrain.Forest=>"46623d",_=>"68815b"};
            Box(world,new(1.5f,.12f,1.5f),PositionOf(c)+new Vector3(0,-.09f,0),color);
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Forest)world.AddChild(new MeshInstance3D{Position=PositionOf(c)+new Vector3(0,.45f,0),Mesh=new CylinderMesh{TopRadius=0,BottomRadius=.45f,Height=.9f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("294b32")}});
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Rock)world.AddChild(new MeshInstance3D{Position=PositionOf(c)+new Vector3(0,.25f,0),Mesh=new PrismMesh{Size=new(1,.7f,1)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("555e5a")}});
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Bridge)foreach(float side in new[]{-.6f,.6f})Box(world,new(1.5f,.15f,.08f),PositionOf(c)+new Vector3(0,.05f,side),"c5a777");
        }
        foreach(var (pos,name) in new[]{(new Vector3(-55,0,20),"西の国"),(new Vector3(38,0,-28),"北の国"),(new Vector3(-9,0,0),"川西城"),(new Vector3(10,0,0),"川東城")})
        {
            Box(world,new(.7f,.65f,.7f),pos,"c2bba6");var label=new Label3D{Text=name,Position=pos+new Vector3(0,1.5f,0),FontSize=40,PixelSize=.015f,OutlineSize=6,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};world.AddChild(label);
            placeLabels.Add((label,name.EndsWith("国")));
        }
        // A geographic focus box at broad zooms, not a separate battlefield boundary.
        focusFrame=new Node3D();world.AddChild(focusFrame);
        foreach(var (a,b) in new[]{(new Vector3(-8,.02f,-5.5f),new Vector3(8,.02f,-5.5f)),(new Vector3(8,.02f,-5.5f),new Vector3(8,.02f,5.5f)),(new Vector3(8,.02f,5.5f),new Vector3(-8,.02f,5.5f)),(new Vector3(-8,.02f,5.5f),new Vector3(-8,.02f,-5.5f))})Stroke(focusFrame,a,b,"d4bd88",.1f);
    }
    public void SetZoom(float value,bool immediate=false){TargetZoom=Mathf.Clamp(value,12,180);if(immediate)Camera.Size=TargetZoom;}
    public void SetDraft(MapCell start,MapCell end){MilitaryFlow.FormationPositions(start,end);DraftStart=start;DraftEnd=end;Synchronize();}
    public MapCell? GroundCell(Vector2 surfacePosition)
    {
        var point=surfacePosition*new Vector2(View.Size.X,View.Size.Y)/Surface.Size;var origin=Camera.ProjectRayOrigin(point);var direction=Camera.ProjectRayNormal(point);
        if(Mathf.Abs(direction.Y)<.001f)return null;var hit=origin+direction*(-origin.Y/direction.Y);
        var cell=new MapCell(Mathf.RoundToInt(hit.X/1.5f+5),Mathf.RoundToInt(hit.Z/1.5f+3));return MilitaryMap.Contains(cell)?cell:null;
    }
    public Vector2 SurfacePoint(MapCell cell)=>Camera.UnprojectPosition(PositionOf(cell))*Surface.Size/new Vector2(View.Size.X,View.Size.Y);
    void OnMapInput(InputEvent input)
    {
        if(input is InputEventMouseButton wheel&&wheel.Pressed&&(wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)){SetZoom(TargetZoom*(wheel.ButtonIndex==MouseButton.WheelUp?.78f:1.28f));return;}
        if(!drawing||Camera.Size>22||Observer!="character-001")return;
        if(input is InputEventMouseButton e&&e.ButtonIndex==MouseButton.Left)
        {
            if(e.Pressed){dragStart=e.Position;dragging=true;return;}
            if(!dragging)return;dragging=false;
            try{var start=GroundCell(dragStart);var end=GroundCell(e.Position);if(start is null||end is null)throw new ArgumentException("演習地域の地面に線を引いてください。");SetDraft(start.Value,end.Value);Notice.Text="担当位置を確認し、布陣命令を発送してください。";}
            catch(Exception error){Notice.Text="線を確定できません："+error.Message;}
        }
    }
    public override void _Process(double delta)
    {Camera.Size=Mathf.Lerp(Camera.Size,TargetZoom,(float)Math.Min(1,delta*7));Synchronize();}
    public void Synchronize()
    {
        if(Camera is null)return;
        focusFrame.Visible=Camera.Size>25;
        foreach(var actor in Actors.Values)actor.Visible=false;foreach(var marker in Markers.Values)marker.Visible=false;foreach(var label in labels.Values)label.Visible=false;
        foreach(var (label,national) in placeLabels){label.PixelSize=.012f*Math.Max(1,Camera.Size/14);label.Visible=national?Camera.Size>70:Camera.Size<70;}
        foreach(var child in lines.GetChildren()){lines.RemoveChild(child);child.QueueFree();}
        var plans=Session.Simulation.FormationPlansFor(Observer);bool issued=plans.Length>0;
        if(Observer=="character-001")
        {
            var start=issued?plans[0].Start:DraftStart;var end=issued?plans[0].End:DraftEnd;
            Stroke(lines,PositionOf(start)+new Vector3(0,.08f,0),PositionOf(end)+new Vector3(0,.08f,0),issued?"e1ba6d":"9aabba",.12f);
            foreach(var cell in MilitaryFlow.FormationPositions(start,end)){var p=PositionOf(cell)+new Vector3(0,.08f,0);Stroke(lines,p+new Vector3(-.35f,0,0),p+new Vector3(.35f,0,0),"ead6a3",.1f);}
        }
        Actions["send"].Disabled=Observer!="character-001"||!Session.Simulation.HasFormationMap||issued;
        Actions["draw"].Disabled=Observer!="character-001"||issued;
        if(!Session.Simulation.HasFormationMap){LastView=null;Status.Text="未配置 / 全国・地域・近景は同じ場所を表示しています。";Details.Text="初期の布陣案：橋の東に左翼・中央・右翼。\n\n演習配置後、左ドラッグで線を変更できます。\n\n別の演習保存は上書きせず保持します。";return;}
        LastView=Session.Simulation.MilitaryViewFor(Observer);float f=(float)Session.PresentationFraction;
        foreach(var u in LastView.Units.Where(u=>u.Present))
        {
            if(Camera.Size>80&&!u.Own)continue;
            var position=PositionOf(u.Previous).Lerp(PositionOf(u.Cell),f);var label=labels[u.UnitId];label.Visible=true;
            bool current=u.Current||u.Own;
            if(current&&Camera.Size<25)
            {
                var actor=Actors[u.UnitId];var look=SnapshotCodec.Write(Session.Simulation.AppearanceFor(u.CommanderId));if(!looks.TryGetValue(u.UnitId,out var old)||look!=old){actor.Apply(SnapshotCodec.Read<Officer3DCustomLook>(look));looks[u.UnitId]=look;}
                actor.Visible=true;actor.SetLod("mid");actor.Position=position;var d=PositionOf(u.Cell)-PositionOf(u.Previous);actor.Rotation=new(0,d.LengthSquared()>.001f?Mathf.Atan2(d.X,d.Z):Mathf.Pi/2,0);
                string clip=d.LengthSquared()>.001f?"walk":"idle";double t=(LastView.Tick+f)*.7;actor.Freeze(clip,t%actor.Player.GetAnimation(actor.Clips[clip]).Length);
            }
            else
            {var marker=Markers[u.UnitId];marker.Visible=true;marker.Position=PositionOf(u.Cell)+new Vector3(0,.3f,0);float k=Math.Max(1,Camera.Size/25);marker.Scale=Vector3.One*k;}
            label.Position=(current?position:PositionOf(u.Cell))+new Vector3(0,Camera.Size<25?2.1f:3*Camera.Size/25,0);label.PixelSize=.009f*Math.Max(1,Camera.Size/14);
            label.Text=Camera.Size>80?"第一軍 / 川の渡河点":UnitName(u.UnitId)+(current?"":"\n第"+u.ObservedAt+"刻の記憶・報告");
        }
        Status.Text=$"第{Session.Simulation.Tick}刻 / {(Session.Simulation.Paused?"停止中":Session.Automatic?"自動進行":"手動進行")} {Session.ClockSpeed}倍 / {(Camera.Size>80?"全国概略":Camera.Size>25?"地域":"布陣近景")} / 視点：{Observer}";
        Details.Text="一本の線・三つの担当位置\n\n"+(issued?"発送済み。線は命令の目標です。\n配置完了を保証しません。":"未発送の布陣案")+"\n\n"+string.Join("\n\n",LastView.Orders.Select(o=>UnitName(o.UnitId)+"\n"+Known(o.KnownStatus)))+"\n\n認識した位置\n"+string.Join("\n",LastView.Units.Select(u=>UnitName(u.UnitId)+$"：({u.Cell.X},{u.Cell.Z}) "+(u.Current?"目撃・本人":"第"+u.ObservedAt+"刻")))+"\n\n線を引く操作と軍令配送は別です。\n伝令は演習用に1・3・5刻。\n詳細な軍議・拒否の事後評価は未実装。";
    }
    static string Known(string s)=>s.StartsWith("Applied:")?"履行開始の返書を受領":s.StartsWith("Refused:")?"拒否の返書を受領":s;
    static string UnitName(string id)=>id switch{"formation-0"=>"左翼・次郎","formation-1"=>"中央・太郎","formation-2"=>"右翼・三郎",_=>id};
}
