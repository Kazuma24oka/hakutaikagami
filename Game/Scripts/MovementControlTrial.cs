using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// Explicit seconds-domain 3D entry. Rendering receives only the selected officer's read model.
public partial class MovementControlTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;
    WorldSession Session=>host.Current;
    Officer3DCustomActor actor=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;OptionButton perspective=null!;
    EntityId observer=ContractFixture.Officer;EntityId? appliedObserver;
    readonly Dictionary<string,MeshInstance3D> roads=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    bool automatic,testing;double fraction;int speed=1;string clip="idle";double clipTime;
    string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/MovementControlV1/runtime-local");
    string SaveRoot
    {
        get {var arg=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--control-save-key="));
            if(!Guid.TryParseExact(arg[19..],"N",out var key))throw new InvalidOperationException("Invalid isolated save key");
            return Output+"/saves/"+key.ToString("N");}
    }
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>new[]{"--control-cancel-check","--control-redirect-check","--control-cancel-restore","--control-redirect-restore"}.Contains(a))??"";
    public override void _Ready()
    {
        testing=Mode!="";
        host=new(new(MovementControl.Create()));store=new(testing?SaveRoot:ProjectSettings.GlobalizePath("user://movement-control-v1-manual"),MovementControl.Create().Definitions);
        Theme=new Theme{DefaultFontSize=19,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,18);
        var column=new VBoxContainer();margin.AddChild(column);column.AddChild(new Label{Text="百代鏡 / 移動中の取消・行先変更・保存復旧"});
        column.AddChild(new Label{Text="全国に城・集落・街道を置くための基盤検証 / 今回は分岐点での伝令受渡し",Modulate=new Color("dec28d")});
        var bar=new HFlowContainer();column.AddChild(bar);
        Add(bar,"dispatch","移動と書役への委任を発送",()=>{
            Submit(BranchRoutes.Order(Session.DiagnosticSnapshot(),"request:branch-move"));
            Submit(BranchRoutes.Order(Session.DiagnosticSnapshot(),"request:branch-delegate",ActionKind.Transfer,ContractFixture.Clerk));});
        Add(bar,"cancel","取消を発送",()=>ControlOrder(ActionKind.Cancel));
        Add(bar,"redirect","帰城への変更を発送",()=>ControlOrder(ActionKind.Redirect));
        Add(bar,"open","届いた命令を開封",()=>OpenLetter());
        Add(bar,"step","1秒進める",()=>{Session.AdvanceTo(Session.Seconds+1);fraction=0;});
        Add(bar,"ten","10秒進める",()=>{Session.AdvanceTo(Session.Seconds+10);fraction=0;});
        Add(bar,"auto","自動進行 切替",()=>automatic=!automatic);
        Add(bar,"pause","停止 / 再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:movement-control"),new(fraction,speed));notice.Text="現在の所在・配送・表示位相を保存しました";});
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
        int roadIndex=0;
        foreach(var edge in BranchRoutes.Edges){var a=MapPosition(BranchRoutes.Nodes.Single(n=>n.Id==edge.From).Position);var b=MapPosition(BranchRoutes.Nodes.Single(n=>n.Id==edge.To).Position);
            var road=Box(world,new(3,.12f,(b-a).Length()),(a+b)/2+new Vector3(0,-.05f+roadIndex++*.002f,0),"af9c79");road.Rotation=new(0,Mathf.Atan2((b-a).X,(b-a).Z),0);roads[edge.Id]=road;}
        foreach(var node in BranchRoutes.Nodes)Box(world,new(.25f,1,.25f),MapPosition(node.Position)+new Vector3(2,.5f,0),"c9ae78");
        actor=new Officer3DCustomActor();world.AddChild(actor);actor.Apply(Officer3DCustomActor.Preset("red_guard"));
        camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=7,Current=true,Far=300};world.AddChild(camera);
        details=new Label{CustomMinimumSize=new(330,0),AutowrapMode=TextServer.AutowrapMode.WordSmart};details.AddThemeFontSizeOverride("font_size",16);row.AddChild(details);
        Refresh();if(testing)CallDeferred(MethodName.RunChecks);
    }
    static Vector3 MapPosition(RoutePoint p)=>new(p.EastMm/1000f,0,p.NorthMm/1000f);
    static MeshInstance3D Box(Node3D n,Vector3 size,Vector3 p,string color){var m=new MeshInstance3D{Mesh=new BoxMesh{Size=size},Position=p,MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(color),Roughness=1}};n.AddChild(m);return m;}
    void ControlOrder(ActionKind kind)
    {
        var target=Session.GetActorView(ContractFixture.Lord).Tasks.First(t=>t.Executor==ContractFixture.Officer).Id;
        Submit(MovementControl.Order(Session.DiagnosticSnapshot(),"request:control-"+(kind==ActionKind.Cancel?"cancel":"redirect"),target,kind));
    }
    void OpenLetter(){var letter=Session.GetActorView(ContractFixture.Officer).Messages.FirstOrDefault(m=>m.Sealed);if(letter==null)return;
        var r=Session.Open(ContractFixture.Officer,letter.Id,Session.Revision);if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new Exception("Open rejected");}
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
        var view=Session.GetBranchView(observer);var k=view.Knowledge;
        var position=MapPosition(view.Position).Lerp(MapPosition(view.NextPosition),(float)fraction);
        actor.Position=position;actor.Rotation=new(0,Mathf.Atan2(view.Heading.EastMm,view.Heading.NorthMm),0);
        foreach(var pair in roads)((StandardMaterial3D)pair.Value.MaterialOverride).AlbedoColor=new Color(view.KnownBlockedEdges.Contains(pair.Key)?"c34c43":view.PlannedEdges.Contains(pair.Key)?"dbbd77":"af9c79");
        clip=view.Walking?"walk":"idle";var length=actor.Player.GetAnimation(actor.Clips[clip]).Length;
        clipTime=view.Walking?((view.DistanceMm+view.StepDistanceMm*fraction)/1000.0/actor.WalkStride*length)%length:(k.ElapsedSeconds+fraction)%length;
        actor.Freeze(clip,clipTime);
        camera.Position=actor.Position+new Vector3(5,5,7);camera.LookAt(actor.Position+new Vector3(0,.9f,0));
        perspective.Select(observer==ContractFixture.Officer?0:observer==ContractFixture.Lord?1:2);
        status.Text=$"世界時刻 {k.ElapsedSeconds}秒 / {(Session.Paused?"停止":"進行可能")} / 視点 {observer.Value}";
        var letterState=k.Messages.Any(m=>m.KnownBody?.Contains("/attempt/")==true)?"配送失敗報告を受領":k.Messages.Any(m=>m.Sealed)?"未開封の命令あり":"未開封の命令なし";
        details.Text=$"本人の所在：東{view.Position.EastMm/1000.0:0.0}m / 北{view.Position.NorthMm/1000.0:0.0}m\n表示：{clip}\n到着予定：{(view.ArrivalAt?.ToString()??"—")}秒\n\n手元の書状\n{letterState}\n\n本人が知る封鎖\n{(view.KnownBlockedEdges.Length==0?"封鎖の情報なし":string.Join(" / ",view.KnownBlockedEdges))}\n\n本人の予定経路\n{string.Join(" → ",view.PlannedEdges)}\n\n任務（知っている範囲）\n"+string.Join("\n",k.Tasks.Select(t=>t.KnownStatus))+"\n\n観測と受領の時刻\n"+string.Join("\n",k.Evidence.Where(e=>e.Claim=="任務完了"||e.Claim.StartsWith("route-block:")).Select(e=>$"{(e.Claim.StartsWith("route-block:")?"封鎖":"完了")} {e.ObservedAt}秒 → {e.RecordedAt}秒"));

    }
    object Pose()=>new{Seconds=Session.Seconds,Observer=observer.Value,X=actor.Position.X,Y=actor.Position.Y,Z=actor.Position.Z,Heading=actor.Rotation.Y,Clip=clip,ClipTime=clipTime,Fraction=fraction,Text=details.Text,Clock=status.Text.Replace("停止","進行可能")};
    string PoseJson()=>System.Text.Json.JsonSerializer.Serialize(Pose());
    void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);System.IO.Directory.CreateDirectory(Output);if(GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void Advance(long seconds){fraction=0;Session.AdvanceTo(seconds);Refresh();}
    void LoadSlot(string slot){var c=store.Prepare(slot);var old=Session;if(host.CommitLoad(c,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load rejected");fraction=c.Presentation.DisplayFraction;speed=c.Presentation.DisplaySpeed;automatic=false;Refresh();}
    string Scenario=>Mode.Contains("redirect")?"redirect":"cancel";
    void SaveStage(string stage){var name=Scenario+"-"+stage;store.Save(Session,name,new("world:movement-control"),new(fraction,speed));System.IO.File.WriteAllText(Output+"/"+name+"-pose.json",PoseJson());}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);bool redirect=Scenario=="redirect";
            if(Mode.EndsWith("restore")){
                foreach(var stage in new[]{"transit","sealed","scheduled","applied"}){
                    observer=ContractFixture.Officer;LoadSlot(Scenario+"-"+stage);Check(Session.Paused,"restore "+stage+" is paused");
                    Check(PoseJson()==System.IO.File.ReadAllText(Output+"/"+Scenario+"-"+stage+"-pose.json"),"restore "+stage+" matches full visible pose and task knowledge");
                    await Capture(Scenario+"-restored-"+stage);
                    Session.SetPaused(false);
                    if(stage=="transit"){Advance(21);Check(Session.Paused,"restored in-transit letter arrives at physical rendezvous");Session.SetPaused(false);}
                    if(stage is "transit" or "sealed"){Advance(23);Click("open");}
                    Advance(30);fraction=.25;Refresh();Check(PoseJson()==System.IO.File.ReadAllText(Output+"/"+Scenario+"-applied-pose.json"),"continuation from "+stage+" matches applied command");await Capture(Scenario+"-continued-"+stage);
                    Advance(90);observer=ContractFixture.Lord;Refresh();Check(PoseJson()==System.IO.File.ReadAllText(Output+"/"+Scenario+"-final-pose.json"),"continuation from "+stage+" matches final reports");
                    Check(Session.DiagnosticSnapshot().Transfers.Length==1,"continuation from "+stage+" preserves one delegation effect");
                }
            }else{
                Click("dispatch");Advance(5);Click(redirect?"redirect":"cancel");Check(Session.Seconds==5,"control dispatch does not move recipient instantly");
                Advance(20);fraction=.25;Refresh();SaveStage("transit");await Capture(Scenario+"-transit");
                Advance(21);Check(Session.Paused&&Session.GetActorView(ContractFixture.Officer).Messages.Any(m=>m.Sealed&&m.KnownBody==null),"physical receipt pauses with sealed body hidden");SaveStage("sealed");await Capture(Scenario+"-sealed");
                Session.SetPaused(false);Advance(23);Check(Session.GetBranchView(observer).Walking,"unopened control has not stopped movement");Click("open");Advance(24);fraction=.25;Refresh();
                Check(Session.GetBranchView(observer).Walking&&Session.GetBranchView(observer).ArrivalAt==141,"opening before effective time does not apply command early");SaveStage("scheduled");await Capture(Scenario+"-scheduled");
                Advance(25);var view=Session.GetBranchView(observer);Check(view.Position==new RoutePoint(48000,0),"effective command preserves physical position");
                Check(redirect?view.Walking&&view.ArrivalAt==81:!view.Walking,"effective command changes route or stops at correct boundary");await Capture(Scenario+"-effective");
                Advance(30);fraction=.25;Refresh();SaveStage("applied");await Capture(Scenario+"-applied");
                if(redirect){Advance(41);Check(Session.GetBranchView(observer).Heading.EastMm<0,"redirect turns back only after reaching next node");await Capture(Scenario+"-turn");}
                else{Advance(41);Check(Session.GetBranchView(observer).Position==new RoutePoint(48000,0),"cancelled actor stays at stop position");}
                Advance(90);observer=ContractFixture.Lord;Refresh();Check(Session.GetActorView(observer).Tasks.All(t=>t.KnownStatus!="発送済・結果未確認"),"issuer learns outcomes only through delivered reports");
                System.IO.File.WriteAllText(Output+"/"+Scenario+"-final-pose.json",PoseJson());await Capture(Scenario+"-final");
            }
            var key=Scenario+(Mode.EndsWith("restore")?"-restore-checks":"-checks");
            System.IO.File.WriteAllText(Output+"/"+key+".json",System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));
            GD.Print("MOVEMENT_CONTROL_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
