using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime : Control
{
    internal Action? MenuRequested;internal NewCampaignProfile Profile=null!;NewCampaignStore campaigns=null!;
    WorldSessionHost host=null!;GenerationSaveStore saves=null!;internal WorldSession Session=>host.Current;
    SubViewport view=null!;SubViewportContainer surface=null!;Node3D stage=null!;Camera3D camera=null!;ContinuousTerrainRenderer terrain=null!;
    readonly Dictionary<EntityId,Officer3DCustomActor> actors=new();readonly Dictionary<EntityId,Label3D> labels=new();
    NewGameMap overview=null!;bool nationMode;
    WorldSession? cachedSession;ContractWorld? cachedWorld;long cachedRevision=-1,cachedSeconds=-1;bool cachedPaused;
    readonly Dictionary<EntityId,BranchView> cachedBranches=new();
    TerrainTile? terrainTile;FieldPoint terrainOrigin=new(0,0);float terrainSpan;
    Label info=null!,message=null!,tasks=null!;OptionButton destination=null!;readonly Dictionary<string,Button> buttons=new();
    internal float Yaw=15,Span=50;double fraction;bool automatic;bool loading;int speed=1;OptionButton speedChoice=null!;
    internal void Configure(NewCampaignStore owner,NewCampaignProfile profile,bool load){campaigns=owner;Profile=profile;var start=NewCampaignStore.Build(profile);host=new(new WorldSession(start));saves=owner.Saves(profile);if(load){var prepared=saves.Prepare("manual");if(host.CommitLoad(prepared,host.Current,host.Current.Revision)!=ResultCode.Accepted)throw new InvalidDataException("保存を再開できません");ApplyPresentation(prepared.Presentation);}loading=load;}
    public override void _Ready()
    {
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,18);
        var root=new VBoxContainer();margin.AddChild(root);var bar=new HFlowContainer();root.AddChild(bar);
        Add(bar,"menu","スタート画面へ",()=>{Suspend();MenuRequested?.Invoke();});Add(bar,"ten","10秒進める",()=>Advance(Session.Seconds+10));Add(bar,"auto","自動進行",()=>{automatic=!automatic;Session.SetPaused(!automatic);});Add(bar,"pause","停止",()=>{automatic=false;Session.SetPaused(true);});Add(bar,"rotate","視点を回す",()=>Yaw=(Yaw+45)%360);Add(bar,"near","現在地",()=>{Span=50;nationMode=false;});Add(bar,"nation","全国地図",()=>nationMode=!nationMode);
        speedChoice=new();foreach(var label in new[]{"1倍","10倍","60倍","360倍"})speedChoice.AddItem(label);speedChoice.Select(Array.IndexOf(new[]{1,10,60,360},speed));speedChoice.ItemSelected+=i=>speed=new[]{1,10,60,360}[(int)i];bar.AddChild(speedChoice);
        destination=new(){CustomMinimumSize=new(170,0)};foreach(var s in NationalRoutes.Sites)destination.AddItem(s.Name);bar.AddChild(destination);Add(bar,"depart","この行先へ出発",Depart);Add(bar,"arrive","次の拠点まで",()=>{var v=Branch(Session.PlayerControl);if(v.ArrivalAt is {} at)Advance(at+2);});Add(bar,"inspect","現地を自分で点検",()=>Submit(NewGameWorld.Inspect(Snapshot(),"request:inspection/"+Session.Revision)));Add(bar,"open","届いた書状を開く",Open);Add(bar,"save","保存",Save);Add(bar,"load","保存から再開",Load);
        Add(bar,"work","現地で仕事を相談",()=>Submit(NewGameWorld.LocalWork(Snapshot(),"request:local-work/"+Session.Revision)));Add(bar,"accept","依頼を引き受ける",()=>Assent(true));Add(bar,"decline","依頼を断る",()=>Assent(false));
        info=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};message=new(){Text=loading?"保存した同じ人物の人生を再開しました。":"確定した世界で、選んだ武将の人生が始まりました。"};root.AddChild(info);root.AddChild(message);
        surface=new(){Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(600,350)};root.AddChild(surface);view=new(){OwnWorld3D=true,Size=new(1000,600),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(view);stage=new();view.AddChild(stage);
        stage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("364f5c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("e9e7d8"),AmbientLightEnergy=.7f}});stage.AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-30,0),LightEnergy=.9f});terrain=new();stage.AddChild(terrain);camera=new(){Current=true,Projection=Camera3D.ProjectionType.Orthogonal,KeepAspect=Camera3D.KeepAspectEnum.Width};stage.AddChild(camera);
        overview=new(){Visible=false,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};root.AddChild(overview);overview.SitePicked=site=>{SetDestination(NewGameWorld.Node(site));message.Text=NewGameWorld.SiteName(site)+"を選びました。出発する場合は「この行先へ出発」を押してください。";};
        tasks=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(tasks);root.AddChild(new Label{Text="地形・城郭は既存の試作表現です。開始時の所属や立場だけで、他人の兵・徴税権は得ません。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("d6bb7d")});view.SizeChanged+=()=>CallDeferred(nameof(Refresh));Refresh();if(!loading)Save();
    }
    void Add(Node parent,string id,string text,Action action){var b=new Button{Text=text};parent.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{action();Refresh();}catch(Exception e){message.Text="操作できません："+e.Message;GD.Print("RUNTIME_REJECT "+e.Message);}};}
    void Submit(Intent intent)=>Submit(Session.Submit(intent,Session.Revision));
    void Submit(CommandResult result){if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new InvalidDataException(result.VisibleReason);message.Text=result.VisibleReason;}
    void Depart(){var w=Snapshot();Submit(Session.Submit(TravelWorld.Move(w,"request:travel/"+Session.Revision,NationalRoutes.Nodes[destination.Selected].Id),Session.Revision));Session.SetPaused(false);}
    TaskState? Awaiting(){var w=Snapshot();var known=Branch(w.PlayerControl).Knowledge.Tasks.Select(t=>t.Id).ToHashSet();return w.Tasks.FirstOrDefault(t=>known.Contains(t.Id)&&t.Executor==w.PlayerControl&&t.Agreement==AgreementPhase.AwaitingAssent);}
    void Assent(bool accept){var task=Awaiting()??throw new InvalidDataException("回答できる依頼はありません");Submit(Session.Assent(task.Id,Session.PlayerControl,accept,Session.Revision));}
    void Open(){var item=Branch(Session.PlayerControl).Knowledge.Messages.FirstOrDefault(m=>m.KnownBody==null);if(item==null){message.Text="未開封の書状はありません。";return;}Submit(Session.Open(Session.PlayerControl,item.Id,Session.Revision));}
    internal void Advance(long to){automatic=false;fraction=0;while(Session.Seconds<to){Session.SetPaused(false);long prior=Session.Seconds;Session.AdvanceTo(to);if(Session.Seconds==prior)break;}Session.SetPaused(true);Refresh();}
    internal void Suspend(){automatic=false;Session.SetPaused(true);Visible=false;ProcessMode=ProcessModeEnum.Disabled;}
    internal void Resume(){Visible=true;ProcessMode=ProcessModeEnum.Inherit;Refresh();}
    internal void Save(){automatic=false;Session.SetPaused(true);saves.Save(Session,"manual",Snapshot().WorldId,new(fraction,speed,null,new(0,0,Span,Yaw,!nationMode,true)));message.Text="同じ人物・世界の進行を保存しました。";}
    void ApplyPresentation(SavePresentation p){fraction=p.DisplayFraction;speed=p.DisplaySpeed;if(speedChoice!=null)speedChoice.Select(Array.IndexOf(new[]{1,10,60,360},speed));if(p.WorldCamera is {} c){Span=(float)c.Span;Yaw=(float)c.Yaw;nationMode=!c.Follow;}}
    internal void Load(){automatic=false;var prepared=saves.Prepare("manual");if(host.CommitLoad(prepared,Session,Session.Revision)!=ResultCode.Accepted)throw new InvalidDataException("現在の進行が変わったため読み直してください");ApplyPresentation(prepared.Presentation);message.Text="保存した人物・所属・外見・位置へ戻りました。";Refresh();}
    internal string InfoText=>info.Text;
    internal Rect2 RenderRect=>surface.GetGlobalRect();
    internal Rect2 InfoRect=>info.GetGlobalRect();
    internal void SaveCheckpoint(string slot)=>saves.Save(Session,slot,Snapshot().WorldId,new(fraction,speed,null,new(0,0,Span,Yaw,!nationMode,true)));
    internal void LoadCheckpoint(string slot){var p=saves.Prepare(slot);if(host.CommitLoad(p,Session,Session.Revision)!=ResultCode.Accepted)throw new InvalidDataException("checkpoint restore failed");ApplyPresentation(p.Presentation);Refresh();}
    internal void Press(string id)=>buttons[id].EmitSignal(Button.SignalName.Pressed);
    internal void SetDestination(string id)=>destination.Select(Array.FindIndex(NationalRoutes.Nodes,n=>n.Id==id));
    internal void ReviewFraction(double value){fraction=value;Refresh();}
    RoutePoint RenderPosition(BranchView v)=>!v.Walking?v.Position:new(v.Position.EastMm+(long)Math.Round((v.NextPosition.EastMm-v.Position.EastMm)*fraction),v.Position.NorthMm+(long)Math.Round((v.NextPosition.NorthMm-v.Position.NorthMm)*fraction));
    internal string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Person=Session.PlayerControl.Value,Position=Branch(Session.PlayerControl).Position,Session.Seconds,Yaw,Span,Speed=speed,Fraction=fraction,Appearance=NewGameWorld.Look(Snapshot().NewGame!,Session.PlayerControl.Value),Actors=actors.OrderBy(p=>p.Key.Value).Where(p=>p.Value.Visible).Select(p=>new{Id=p.Key.Value,p.Value.Position,p.Value.Rotation,Lod=p.Value.Lod})});
    internal void Refresh()
    {
        if(camera==null)return;surface.Visible=!nationMode;overview.Visible=nationMode;var w=Snapshot();var own=Branch(Session.PlayerControl);var center=FieldWorld.FromWorld(RenderPosition(own));UpdateTerrain(center);float height=ContinuousWorld.Height(center)/1000f;
        camera.Size=Span;camera.Near=.05f;camera.Far=3000;camera.Position=new(Mathf.Sin(Mathf.DegToRad(Yaw))*Span*.6f,height+Span*.7f,Mathf.Cos(Mathf.DegToRad(Yaw))*Span*.6f);camera.LookAt(new(0,height+1,0));
        foreach(var a in actors.Values)a.Visible=false;foreach(var l in labels.Values)l.Visible=false;
        var local=RegionalCampaign.Nearby(w,w.PlayerControl).Where(p=>p.Position!=own.Position).GroupBy(p=>p.Position).Select(g=>g.First()).Select(p=>(p.Actor,p.Position,p.Walking)).Prepend((w.PlayerControl,own.Position,own.Walking));
        foreach(var (id,position,walking) in local){if(!actors.TryGetValue(id,out var actor)){actor=new(){Scale=Vector3.One*2};stage.AddChild(actor);actor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(NewGameWorld.Look(w.NewGame!,id.Value))));actors[id]=actor;var label=new Label3D{Text=w.Definitions.Characters.Single(p=>p.Id==id).Name,Font=GetThemeDefaultFont(),FontSize=20,PixelSize=.03f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};stage.AddChild(label);labels[id]=label;}
            var fp=FieldWorld.FromWorld(RenderPosition(Branch(id)));actor.Position=new((fp.X-center.X)/1000f,ContinuousWorld.Height(fp)/1000f,-(fp.Y-center.Y)/1000f);actor.Visible=true;var heading=Branch(id).Heading;actor.Rotation=new(0,Mathf.Atan2(heading.EastMm,-heading.NorthMm),0);var clip=actor.Player.GetAnimation(actor.Clips[walking?"walk":"idle"]);double time=walking?VisualDistance(w,id,fraction)/1000d/(actor.WalkStride*actor.Scale.Z)*clip.Length:Session.Seconds+fraction;actor.Freeze(walking?"walk":"idle",time%clip.Length);labels[id].Position=actor.Position+Vector3.Up*4.5f;labels[id].Visible=true;}
        var p=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value);string affiliation=p.Faction==null?"所属なし":w.Definitions.Organizations.Single(f=>f.Id.Value==p.Faction).Name;string lord=p.Lord==null?"直属なし":"直属："+w.Definitions.Characters.Single(q=>q.Id.Value==p.Lord).Name;var location=w.Locations.Single(l=>l.Actor==w.PlayerControl);string place=location.Site is {} at?NewGameWorld.SiteName(at.Value):"道中";
        string role=p.Faction!=null&&w.NewGame.Start.Factions.Any(f=>f.Head==p.Id)?"大名":p.InitialRole is {} initial?NewGameWorld.RoleName(initial):p.Lord==null?"浪人":"家臣";
        string abilities=p.Attributes==null?"能力は未採録":"統率"+p.Attributes.Leadership+"・武勇"+p.Attributes.Valor+"・知略"+p.Attributes.Intelligence+"・政務"+p.Attributes.Politics;
        info.Text=p.Name+" / "+role+" / "+affiliation+" / "+lord+" / "+place+" / "+abilities+"\n世界時間：開始から"+Session.Seconds+"秒 / "+(Session.Paused?"停止中":"進行中");
        var knowledge=Branch(w.PlayerControl).Knowledge;tasks.Text=string.Join(" / ",knowledge.Tasks.Select(t=>t.KnownStatus.Replace("Running","進行中").Replace("Completed","完了").Replace("Waiting","着手待ち").Replace("NotStarted","未着手").Replace("Stopped","中断").Replace("Failed","失敗")))+"\n届いた書状："+knowledge.Messages.Length+"通";
        var awaiting=Awaiting();buttons["accept"].Visible=buttons["decline"].Visible=awaiting!=null;buttons["work"].Disabled=location.Kind!=LocationKind.AtSite||w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));
        buttons["inspect"].Disabled=location.Kind!=LocationKind.AtSite;buttons["depart"].Disabled=TravelWorld.Active(w);buttons["arrive"].Disabled=own.ArrivalAt==null;
    }
    // A display snapshot is immutable and valid only for this exact session/revision/time/pause.
    // Loading another instance at the same revision must never reuse the old person's view.
    ContractWorld Snapshot()
    {
        if(!ReferenceEquals(cachedSession,Session)||cachedRevision!=Session.Revision||cachedSeconds!=Session.Seconds||cachedPaused!=Session.Paused){cachedSession=Session;cachedRevision=Session.Revision;cachedSeconds=Session.Seconds;cachedPaused=Session.Paused;cachedWorld=Session.DiagnosticSnapshot();cachedBranches.Clear();}
        return cachedWorld!;
    }
    BranchView Branch(EntityId actor)
    {
        Snapshot();if(!cachedBranches.TryGetValue(actor,out var value)){value=Session.GetBranchView(actor);cachedBranches.Add(actor,value);}return value;
    }
    // Keep terrain vertices fixed within one80m tile. Translate the retained terrain root
    // as the camera follows; rebuild picks/chunks only when tile coverage or zoom changes.
    void UpdateTerrain(FieldPoint center)
    {
        var tile=ContinuousWorld.TileAt(center);
        if(terrainTile!=tile||terrainSpan!=Span){terrain.Position=Vector3.Zero;terrainOrigin=ContinuousWorld.Point(tile.Node,tile.X*ContinuousWorld.Tile,tile.Y*ContinuousWorld.Tile);terrain.Update(terrainOrigin,Span);terrainTile=tile;terrainSpan=Span;}
        terrain.Position=new((terrainOrigin.X-center.X)/1000f,0,-(terrainOrigin.Y-center.Y)/1000f);
    }
    internal static double VisualDistance(ContractWorld world,EntityId actor,double phase)
    {
        var journey=world.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor);if(journey==null)return 0;
        var nodes=BranchRoutes.GraphNodes(world);var edges=BranchRoutes.GraphEdges(world);double distance=0;
        foreach(var leg in journey.Legs){var a=nodes.Single(n=>n.Id==leg.From).Position;var b=nodes.Single(n=>n.Id==leg.To).Position;double dx=b.EastMm-a.EastMm,dy=b.NorthMm-a.NorthMm;double length=Math.Sqrt(dx*dx+dy*dy);var edge=edges.Single(e=>e.Id==leg.Edge);double p=TravelWorld.Progress(world,leg,world.ElapsedSeconds),next=TravelWorld.Progress(world,leg,world.ElapsedSeconds+1);distance+=length*Math.Clamp((p+(next-p)*phase)/edge.LengthMm,0,1);}
        return distance;
    }
    public override void _Process(double delta){if(!automatic||Session.Paused)return;fraction+=Math.Min(delta,.25)*speed;long steps=(long)fraction;if(steps>0){fraction-=steps;Session.AdvanceTo(Session.Seconds+steps);}Refresh();}
}
