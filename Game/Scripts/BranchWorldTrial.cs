using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// Explicit seconds-domain 3D entry. Rendering receives only the selected officer's read model.
public partial class BranchWorldTrial : Control
{
    WorldSessionHost host=null!;GenerationSaveStore store=null!;
    WorldSession Session=>host.Current;
    Officer3DCustomActor actor=null!;Camera3D camera=null!;SubViewport viewport=null!;
    Label status=null!,details=null!,notice=null!;OptionButton perspective=null!;
    EntityId observer=ContractFixture.Officer;EntityId? appliedObserver;
    readonly Dictionary<string,MeshInstance3D> roads=new();
    readonly Dictionary<string,Button> buttons=new();readonly List<string> checks=new();
    bool automatic,testing;double fraction;int speed=1;string clip="idle";double clipTime;
    string Output=>ProjectSettings.GlobalizePath("res://../Docs/Design/BranchWorldV1/runtime-local");
    string SaveRoot
    {
        get {var arg=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--branch-save-key="));
            if(!Guid.TryParseExact(arg[18..],"N",out var key))throw new InvalidOperationException("Invalid isolated save key");
            return Output+"/saves/"+key.ToString("N");}
    }
    string Mode=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>new[]{"--branch-check","--branch-restore","--branch-restore-before","--branch-open-check"}.Contains(a))??"";
    public override void _Ready()
    {
        testing=Mode!="";
        host=new(new(BranchRoutes.Create(Mode!="--branch-open-check")));store=new(testing?SaveRoot:ProjectSettings.GlobalizePath("user://branch-world-v1-manual"),BranchRoutes.Create(Mode!="--branch-open-check").Definitions);
        Theme=new Theme{DefaultFontSize=19,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo","sans-serif"}}};
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,18);
        var column=new VBoxContainer();margin.AddChild(column);column.AddChild(new Label{Text="百代鏡 / 分岐経路・目撃による迂回・保存復旧"});
        column.AddChild(new Label{Text="架空の分岐地図 / 封鎖は目撃か報告で知り、知った範囲で迂回します",Modulate=new Color("dec28d")});
        var bar=new HFlowContainer();column.AddChild(bar);
        Add(bar,"dispatch","移動と書役への委任を発送",()=>{
            Submit(BranchRoutes.Order(Session.DiagnosticSnapshot(),"request:branch-move"));
            Submit(BranchRoutes.Order(Session.DiagnosticSnapshot(),"request:branch-delegate",ActionKind.Transfer,ContractFixture.Clerk));});
        Add(bar,"step","1秒進める",()=>{Session.AdvanceTo(Session.Seconds+1);fraction=0;});
        Add(bar,"ten","10秒進める",()=>{Session.AdvanceTo(Session.Seconds+10);fraction=0;});
        Add(bar,"auto","自動進行 切替",()=>automatic=!automatic);
        Add(bar,"pause","停止 / 再開",()=>Session.SetPaused(!Session.Paused));
        Add(bar,"save","保存",()=>{store.Save(Session,"manual",new("world:branch-fixture"),new(fraction,speed));notice.Text="現在の所在・配送・表示位相を保存しました";});
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
        details.Text=$"本人の所在：東{view.Position.EastMm/1000.0:0.0}m / 北{view.Position.NorthMm/1000.0:0.0}m\n表示：{clip}\n到着予定：{(view.ArrivalAt?.ToString()??"—")}秒\n\n本人が知る封鎖\n{(view.KnownBlockedEdges.Length==0?"封鎖の情報なし":string.Join(" / ",view.KnownBlockedEdges))}\n\n本人の予定経路\n{string.Join(" → ",view.PlannedEdges)}\n\n任務（知っている範囲）\n"+string.Join("\n",k.Tasks.Select(t=>t.KnownStatus))+"\n\n観測と受領の時刻\n"+string.Join("\n",k.Evidence.Where(e=>e.Claim=="任務完了"||e.Claim.StartsWith("route-block:")).Select(e=>$"{(e.Claim.StartsWith("route-block:")?"封鎖":"完了")} {e.ObservedAt}秒 → {e.RecordedAt}秒"));

    }
    object Pose()=>new{Seconds=Session.Seconds,Observer=observer.Value,X=actor.Position.X,Y=actor.Position.Y,Z=actor.Position.Z,Heading=actor.Rotation.Y,Clip=clip,ClipTime=clipTime,Fraction=fraction,Text=details.Text,Clock=status.Text.Replace("停止","進行可能")};
    string PoseJson()=>System.Text.Json.JsonSerializer.Serialize(Pose());
    void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
    async Task Capture(string name){Refresh();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);System.IO.Directory.CreateDirectory(Output);if(GetViewport().GetTexture().GetImage().SavePng(Output+"/"+name+".png")!=Error.Ok)throw new Exception("Capture failed");}
    void Advance(long seconds){fraction=0;Session.AdvanceTo(seconds);Refresh();}
    void LoadSlot(string slot){var c=store.Prepare(slot);var old=Session;if(host.CommitLoad(c,old,old.Revision)!=ResultCode.Accepted)throw new Exception("Load rejected");fraction=c.Presentation.DisplayFraction;speed=c.Presentation.DisplaySpeed;automatic=false;Refresh();}
    async void RunChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode=="--branch-open-check"){
                Click("dispatch");Advance(20);fraction=.25;Refresh();Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"open control has no closure knowledge at 20 seconds");await Capture("unknown-open");
                observer=ContractFixture.Lord;Advance(30);Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"open control issuer has no remote closure knowledge at 30 seconds");await Capture("lord-open-uninformed");
            }else if(Mode=="--branch-restore"||Mode=="--branch-restore-before"){
                bool before=Mode=="--branch-restore-before";LoadSlot(before?"before":"manual");
                Check(Session.Paused,"separate process restores globally paused");
                Check(PoseJson()==System.IO.File.ReadAllText(Output+(before?"/before-pose.json":"/saved-pose.json")),"separate process restores exact visible pose knowledge and itinerary");
                await Capture(before?"before-restored-process":"restored-process");
                Click("pause");if(before){Advance(21);Check(Session.GetBranchView(observer).KnownBlockedEdges.SequenceEqual(new[]{"pass"}),"pre-discovery save discovers block only after restored journey reaches fork");
                    Advance(51);fraction=.25;Refresh();Check(PoseJson()==System.IO.File.ReadAllText(Output+"/saved-pose.json"),"pre-discovery continuation matches rerouted midpoint");await Capture("before-continued");}
                Advance(211);observer=ContractFixture.Lord;Refresh();Check(PoseJson()==System.IO.File.ReadAllText(Output+"/final-pose.json"),"restored continuation matches final received results");
                Check(Session.DiagnosticSnapshot().Transfers.Length==1,"restored delegation effect remains single");await Capture(before?"before-final":"final-restored");
            }else{
                Click("dispatch");Check(Session.Seconds==0,"actual dispatch buttons do not advance time");Advance(20);fraction=.25;Refresh();
                var view=Session.GetBranchView(observer);Check(view.KnownBlockedEdges.Length==0&&view.ArrivalAt==101&&view.PlannedEdges.SequenceEqual(new[]{"road","pass"}),"unknown closure does not change displayed route or ETA");
                store.Save(Session,"before",new("world:branch-fixture"),new(fraction,speed));System.IO.File.WriteAllText(Output+"/before-pose.json",PoseJson());await Capture("unknown-closed");
                Advance(21);view=Session.GetBranchView(observer);Check(view.KnownBlockedEdges.SequenceEqual(new[]{"pass"})&&view.ArrivalAt==141&&view.Position==new RoutePoint(40000,0),"physical fork discovery reroutes without teleporting");await Capture("discovery");
                observer=ContractFixture.Lord;Advance(30);Check(Session.GetBranchView(observer).KnownBlockedEdges.Length==0,"issuer screen does not learn remote closure early");await Capture("lord-uninformed");
                Advance(31);Check(Session.GetBranchView(observer).KnownBlockedEdges.SequenceEqual(new[]{"pass"}),"issuer learns closure after ten-second report delivery");await Capture("lord-informed");
                observer=ContractFixture.Officer;Advance(51);fraction=.25;Refresh();Check(actor.Position.DistanceTo(new(80,0,10.25f))<.001f,"3D position follows the detour at actual meter scale");
                var pose=PoseJson();Click("save");System.IO.File.WriteAllText(Output+"/saved-pose.json",pose);await Capture("midpoint");
                Advance(70);Click("load");Check(Session.Paused&&PoseJson()==pose,"same-process restored route and visible pose match");await Capture("restored");
                var stopped=PoseJson();automatic=true;UpdateClock(.25);automatic=false;Check(stopped==PoseJson(),"paused restore freezes motion and knowledge");Click("pause");
                Advance(141);Check(actor.Position==new Vector3(40,0,80)&&clip=="idle","detour arrives at 141 seconds");await Capture("arrival");
                observer=ContractFixture.Lord;Advance(210);Check(details.Text.Contains("発送済・結果未確認"),"result is unknown immediately before return report");
                Advance(211);Check(Session.GetActorView(observer).Tasks.Count(t=>t.KnownStatus=="任務完了")==2,"both task results visible after delayed report");System.IO.File.WriteAllText(Output+"/final-pose.json",PoseJson());await Capture("complete");
                host=new(new(BranchRoutes.Create()));Click("dispatch");observer=ContractFixture.Officer;
                for(int i=0;i<12;i++){Advance(41+i);await Capture("motion-"+i.ToString("D2"));}
                Check(actor.Position==new Vector3(80,0,11),"consecutive rendered frames traverse the detour corner");
            }
            var key=Mode=="--branch-check"?"checks":Mode=="--branch-restore"?"restore-checks":Mode=="--branch-open-check"?"open-checks":"before-checks";
            System.IO.File.WriteAllText(Output+"/"+key+".json",System.Text.Json.JsonSerializer.Serialize(new{Status="PASSED",Checks=checks,ViewportRect=new[]{((Control)viewport.GetParent()).GlobalPosition.X,((Control)viewport.GetParent()).GlobalPosition.Y,((Control)viewport.GetParent()).Size.X,((Control)viewport.GetParent()).Size.Y}}));
            GD.Print("BRANCH_WORLD_PASSED "+checks.Count);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
