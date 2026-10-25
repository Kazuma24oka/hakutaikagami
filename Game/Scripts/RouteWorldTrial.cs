using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// Explicit seconds-domain 3D entry. Rendering receives only the selected officer's read model.
public partial class RouteWorldTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;
    WorldSession Session=>host.Current;
    Officer3DCustomActor actor=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;OptionButton perspective=null!;
    EntityId observer=ContractFixture.Officer;EntityId? appliedObserver;
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    bool automatic,testing;double fraction;int speed=1;string clip="idle";double clipTime;
    string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/RouteWorldV1/runtime-local");
    string SaveRoot
    {
        get {var arg=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--route-save-key="));
            if(!Guid.TryParseExact(arg[17..],"N",out var key))throw new InvalidOperationException("Invalid isolated save key");
            return Output+"/saves/"+key.ToString("N");}
    }
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--route-"))??"";
    public override void _Ready()
    {
        testing=Mode is "--route-check" or "--route-restore";
        host=new(new(RouteContract.Create()));store=new(testing?SaveRoot:ProjectSettings.GlobalizePath("user://route-world-v1-manual"),RouteContract.Create().Definitions);
        Theme=new Theme{DefaultFontSize=19,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,18);
        var column=new VBoxContainer();margin.AddChild(column);column.AddChild(new Label{Text="百代鏡 / 秒基準Core × 3D移動・委任・返報"});
        column.AddChild(new Label{Text="架空の検証区間：平道40m＋峠道80m / 全国地理・製品速度の完成ではありません",Modulate=new Color("dec28d")});
        var bar=new HFlowContainer();column.AddChild(bar);
        Add(bar,"dispatch","移動と書役への委任を発送",()=>{
            Submit(RouteContract.Order(Session.DiagnosticSnapshot(),"request:route-move"));
            Submit(RouteContract.Order(Session.DiagnosticSnapshot(),"request:route-delegate",ActionKind.Transfer,ContractFixture.Clerk));});
        Add(bar,"step","1秒進める",()=>{Session.AdvanceTo(Session.Seconds+1);fraction=0;});
        Add(bar,"ten","10秒進める",()=>{Session.AdvanceTo(Session.Seconds+10);fraction=0;});
        Add(bar,"auto","自動進行 切替",()=>automatic=!automatic);
        Add(bar,"pause","停止 / 再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:route-fixture"),new(fraction,speed));notice.Text="現在の所在・配送・表示位相を保存しました";});
        Add(bar,"load","復旧",()=>{var c=store.Prepare("manual");var s=Session;var r=host.CommitLoad(c,s,s.Revision);if(r!=ResultCode.Accepted)throw new InvalidOperationException(r.ToString());fraction=c.Presentation.DisplayFraction;speed=c.Presentation.DisplaySpeed;automatic=false;notice.Text="保存した位置で復旧・全体停止。再開で続行します";});
        perspective=new OptionButton();foreach(var label in new[]{"奉行本人","主君","書役本人"})perspective.AddItem(label);bar.AddChild(perspective);
        perspective.ItemSelected+=i=>{observer=i==0?ContractFixture.Officer:i==1?ContractFixture.Lord:ContractFixture.Clerk;Refresh();};
        status=new Label();column.AddChild(status);notice=new Label{Text="発送 → 自動進行。主君の視点では返報が届くまで結果未確認です"};column.AddChild(notice);
        var row=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};column.AddChild(row);
        var surface=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(640,520)};row.AddChild(surface);
        viewport=new SubViewport{OwnWorld3D=true,Size=new(1000,700),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(viewport);
        var world=new Node3D();viewport.AddChild(world);
        world.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("23333c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.6f}});
        world.AddChild(new DirectionalLight3D{RotationDegrees=new(-55,-35,0),LightEnergy=.65f,ShadowEnabled=true});
        Box(world,new(180,.1f,180),new(20,-.12f,40),"5a7055");
        foreach(var leg in RouteContract.Legs){var a=MapPosition(leg.From);var b=MapPosition(leg.To);var road=Box(world,new(3,.12f,(b-a).Length()),(a+b)/2+new Vector3(0,leg.Id=="road"?-.05f:-.048f,0),leg.Id=="road"?"af9c79":"8a806d");road.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);}
        foreach(var point in new[]{new RoutePoint(0,0),new RoutePoint(40000,0),new RoutePoint(40000,80000)})
            Box(world,new(.25f,1,.25f),MapPosition(point)+new Vector3(2,.5f,0),"c9ae78");
        actor=new Officer3DCustomActor();world.AddChild(actor);actor.Apply(Officer3DCustomActor.Preset("red_guard"));
        camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=7,Current=true,Far=300};world.AddChild(camera);
        details=new Label{CustomMinimumSize=new(330,0),AutowrapMode=TextServer.AutowrapMode.WordSmart};row.AddChild(details);
        Refresh();if(testing)CallDeferred(MethodName.RunChecks);
    }
    static Vector3 MapPosition(RoutePoint p)=>new(p.EastMm/1000f,0,p.NorthMm/1000f);
    static MeshInstance3D Box(Node3D n,Vector3 size,Vector3 p,string color){var m=new MeshInstance3D{Mesh=new BoxMesh{Size=size},Position=p,MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(color),Roughness=1}};n.AddChild(m);return m;}
    void Submit(Intent i){var r=Session.Submit(i,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new InvalidOperationException(r.VisibleReason);}
    void Add(Container c,string id,string title,Action callback){var b=new Button{Text=title};c.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{callback();Refresh();}catch(Exception e){notice.Text=e.Message;GD.PrintErr(e);if(testing)throw;}};}
    void Click(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    public override void _Process(double delta){if(!testing)UpdateClock(delta);}
    void UpdateClock(double delta)
    {
        if(!automatic||Session.Paused)return;
        fraction+=Math.Min(delta,.25)*speed;
        while(fraction>=1){Session.AdvanceTo(Session.Seconds+1);fraction-=1;if(Session.Paused){fraction=0;break;}}
        Refresh();
    }
    void Refresh()
    {
        if(appliedObserver!=observer){actor.Apply(Officer3DCustomActor.Preset(observer==ContractFixture.Lord?"commander":observer==ContractFixture.Clerk?"veteran":"red_guard"));appliedObserver=observer;}
        var view=Session.GetRouteActorView(observer);var k=view.Knowledge;
        // No other actor's live transform is requested. Camera and actor share this one filtered sample.
        long offset=view.DistanceMm;
        if(view.Walking){var leg=RouteContract.Legs[RouteContract.Segment(offset)];offset=Math.Min(RouteContract.Length,offset+(long)(leg.FootMmPerSecond*fraction));}
        actor.Position=MapPosition(RouteContract.Point(offset));
        actor.Rotation=new(0,offset<40000?Mathf.Pi/2:0,0);
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;
        clipTime=view.Walking?(offset/1000.0/actor.WalkStride*length)%length:(k.ElapsedSeconds+fraction)%length;
        actor.Freeze(clip,clipTime);
        camera.Position=actor.Position+new Vector3(5,5,7);camera.LookAt(actor.Position+new Vector3(0,.9f,0));
        perspective.Select(observer==ContractFixture.Officer?0:observer==ContractFixture.Lord?1:2);
        status.Text=$"世界時刻 {k.ElapsedSeconds}秒 / {(Session.Paused?"停止":"進行可能")} / 視点 {observer.Value}";
        details.Text=$"本人の所在：{view.DistanceMm/1000.0:0.0}m\n表示：{clip}\n到着予定：{(view.ArrivalAt?.ToString()??"—")}秒\n\n平道：徒歩2m/s\n峠道：徒歩1m/s\n使者：各区間で徒歩の2倍\n同じ場所の受渡し：1秒\n\n任務（本人が知る範囲）\n"+string.Join("\n",k.Tasks.Select(t=>t.KnownStatus))+"\n\n返報の出典\n"+string.Join("\n",k.Evidence.Where(e=>e.Claim=="任務完了").Select(e=>$"観測 {e.ObservedAt}秒 → 受領 {e.RecordedAt}秒"));
    }
    object Pose()=>new{Seconds=Session.Seconds,Observer=observer.Value,X=actor.Position.X,Y=actor.Position.Y,Z=actor.Position.Z,Heading=actor.Rotation.Y,Clip=clip,ClipTime=clipTime,Fraction=fraction,Text=details.Text,Clock=status.Text.Replace("停止","進行可能")};
    string PoseJson()=>System.Text.Json.JsonSerializer.Serialize(Pose());
    void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);System.IO.Directory.CreateDirectory(Output);if(GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode=="--route-restore"){
                Click("load");Check(Session.Paused&&Session.Seconds==51,"separate process restores paused at 51 seconds");
                Check(PoseJson()==System.IO.File.ReadAllText(Output+"/saved-pose.json"),"separate process restores position heading clip phase and visible text");
                await Capture("restored-process");Click("pause");fraction=0;Session.AdvanceTo(151);observer=ContractFixture.Lord;Refresh();
                Check(PoseJson()==System.IO.File.ReadAllText(Output+"/final-pose.json"),"separate process continuation matches final visible report");
                await Capture("final-restored");
            }else{
                Click("dispatch");Check(Session.Seconds==0,"button dispatch does not advance clock");Session.AdvanceTo(1);Refresh();Check(clip=="walk"&&actor.Position==Vector3.Zero,"walk starts only after command delivery");await Capture("departure");
                Session.AdvanceTo(6);observer=ContractFixture.Lord;Refresh();Check(details.Text.Contains("任務完了")&&details.Text.Contains("発送済・結果未確認"),"delegation report visible while officer movement result remains unknown");await Capture("delegation");
                observer=ContractFixture.Officer;Session.AdvanceTo(51);fraction=.25;Refresh();Check(actor.Position.DistanceTo(new(40,0,30.25f))<.001f,"3D position uses road and pass distance in meters");
                var pose=PoseJson();Click("save");System.IO.File.WriteAllText(Output+"/saved-pose.json",pose);await Capture("midpoint");
                Session.AdvanceTo(70);Click("load");Check(Session.Paused&&PoseJson()==pose,"same process restores exact visible pose and text");await Capture("restored");
                var stopped=PoseJson();automatic=true;UpdateClock(.25);automatic=false;Check(PoseJson()==stopped,"pause freezes clock and animation pose");
                Click("pause");fraction=0;Session.AdvanceTo(101);Refresh();Check(clip=="idle"&&actor.Position==new Vector3(40,0,80),"arrival switches to idle at exact destination");await Capture("arrival");
                observer=ContractFixture.Lord;Session.AdvanceTo(150);Refresh();Check(details.Text.Contains("発送済・結果未確認"),"issuer still has no result one second before report arrival");
                Session.AdvanceTo(151);Refresh();Check(Session.GetActorView(observer).Tasks.Count(t=>t.KnownStatus=="任務完了")==2,"both results become visible only after report delivery");
                System.IO.File.WriteAllText(Output+"/final-pose.json",PoseJson());await Capture("complete");
                // Actual GPU frames at successive authoritative seconds, no OS input.
                host=new(new(RouteContract.Create()));Click("dispatch");observer=ContractFixture.Officer;
                for(int i=0;i<12;i++){Session.AdvanceTo(i+21);fraction=0;Refresh();await Capture("motion-"+i.ToString("D2"));}
                Check(actor.Position==new Vector3(40,0,11),"successive rendered frames follow the turning route");
                automatic=true;UpdateClock(.25);Check(Session.Seconds==32&&fraction==.25,"automatic clock renders subsecond phase without advancing Core early");
                for(int i=0;i<3;i++)UpdateClock(.25);automatic=false;Check(Session.Seconds==33&&fraction==0,"automatic clock commits one shared game second");
            }
            System.IO.File.WriteAllText(Output+"/"+(Mode=="--route-restore"?"restore-checks":"checks")+".json",System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));
            GD.Print("ROUTE_WORLD_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
