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
    readonly Dictionary<EntityId,Officer3DCustomActor> actors=new();readonly Dictionary<EntityId,Label> labels=new();
    NewGameMap overview=null!;bool nationMode;
    WorldSession? cachedSession;ContractWorld? cachedWorld;ServiceView? cachedServiceView;long cachedRevision=-1,cachedSeconds=-1;bool cachedPaused;
    readonly Dictionary<EntityId,BranchView> cachedBranches=new();
    TerrainTile? terrainTile;FieldPoint terrainOrigin=new(0,0);float terrainSpan;
    Label info=null!,message=null!,tasks=null!;
    HFlowContainer serviceBar=null!;OptionButton servicePrincipal=null!,serviceCase=null!;EntityId[] servicePrincipalIds=[],serviceCaseIds=[];OptionButton destination=null!;readonly Dictionary<string,Button> buttons=new();
    internal float Yaw=15,Span=50;double fraction;bool automatic;bool loading;int speed=1;OptionButton speedChoice=null!;
    internal void Configure(NewCampaignStore owner,NewCampaignProfile profile,bool load){campaigns=owner;Profile=profile;var start=NewCampaignStore.Build(profile);host=new(new WorldSession(start));saves=owner.Saves(profile);if(load){var prepared=saves.Prepare("manual");if(host.CommitLoad(prepared,host.Current,host.Current.Revision)!=ResultCode.Accepted)throw new InvalidDataException("保存を再開できません");ApplyPresentation(prepared.Presentation);}loading=load;}
    public override void _Ready()
    {
        var margin=new MarginContainer();AddChild(margin);margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);foreach(var side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,18);
        var root=new VBoxContainer();margin.AddChild(root);var bar=new HFlowContainer();root.AddChild(bar);
        Add(bar,"menu","スタート画面へ",()=>{Suspend();MenuRequested?.Invoke();});Add(bar,"ten","10秒進める",()=>RequestManualAdvance(Session.Seconds+10));Add(bar,"auto","自動進行",()=>RequestClockPause(automatic));Add(bar,"pause","停止",()=>RequestClockPause(true));Add(bar,"rotate","視点を回す",()=>Yaw=(Yaw+45)%360);Add(bar,"near","現在地",()=>{Span=50;nationMode=false;});Add(bar,"nation","全国地図",()=>nationMode=!nationMode);
        speedChoice=new();foreach(var label in new[]{"1倍","10倍","60倍","360倍"})speedChoice.AddItem(label);speedChoice.Select(Array.IndexOf(new[]{1,10,60,360},speed));speedChoice.ItemSelected+=i=>ChangeAdvanceSpeed(new[]{1,10,60,360}[(int)i]);bar.AddChild(speedChoice);
        destination=new(){CustomMinimumSize=new(170,0)};foreach(var s in NationalRoutes.Sites)destination.AddItem(s.Name);bar.AddChild(destination);Add(bar,"depart","この行先へ出発",Depart);Add(bar,"arrive","次の拠点まで",()=>{var v=Branch(Session.PlayerControl);if(v.ArrivalAt is {} at)RequestManualAdvance(at+2);});Add(bar,"inspect","現地を自分で点検",()=>Submit(NewGameWorld.Inspect(Snapshot(),"request:inspection/"+Session.Revision)));BuildMailUI(bar);Add(bar,"open","届いた書状を開く",Open);Add(bar,"save","保存",RequestManualSave);Add(bar,"load","保存から再開",Load);
        Add(bar,"work","現地で仕事を相談",ShowWorkConsultation);Add(bar,"accept","依頼を引き受ける",()=>Assent(true));Add(bar,"decline","依頼を断る",()=>Assent(false));
        serviceBar=new HFlowContainer();root.AddChild(serviceBar);servicePrincipal=new(){CustomMinimumSize=new(170,0)};serviceBar.AddChild(servicePrincipal);Add(serviceBar,"service-apply","仕官を申し出る",()=>DecideService(CareerAction.Apply));serviceCase=new(){CustomMinimumSize=new(230,0)};serviceBar.AddChild(serviceCase);serviceCase.ItemSelected+=_=>RefreshService();Add(serviceBar,"service-accept","仕官条件に同意",()=>DecideService(CareerAction.Accept));Add(serviceBar,"service-decline","仕官条件を辞退",()=>DecideService(CareerAction.Decline));Add(serviceBar,"service-release","辞去を願い出る",()=>DecideService(CareerAction.RequestRelease));Add(serviceBar,"service-inquire","仕官案件を照会",()=>DecideService(CareerAction.Inquire));
        BuildMilitaryUI(root);BuildAuthorityUI(root);BuildKnownTargetUI(root);
        info=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};message=new(){Text=loading?"保存した同じ人物の人生を再開しました。":"確定した世界で、選んだ武将の人生が始まりました。"};root.AddChild(info);root.AddChild(message);
        surface=new(){Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(600,350)};root.AddChild(surface);view=new(){OwnWorld3D=true,Size=new(1000,600),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};surface.AddChild(view);stage=new();view.AddChild(stage);
        stage.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("364f5c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("e9e7d8"),AmbientLightEnergy=.7f}});stage.AddChild(new DirectionalLight3D{RotationDegrees=new(-50,-30,0),LightEnergy=.9f});terrain=new();stage.AddChild(terrain);camera=new(){Current=true,Projection=Camera3D.ProjectionType.Orthogonal,KeepAspect=Camera3D.KeepAspectEnum.Width};stage.AddChild(camera);
        overview=new(){Visible=false,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};root.AddChild(overview);overview.SitePicked=site=>{if(IsSaving||IsClockControlling)return;SetDestination(NewGameWorld.Node(site));message.Text=NewGameWorld.SiteName(site)+"を選びました。出発する場合は「この行先へ出発」を押してください。";};
        tasks=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(tasks);root.AddChild(new Label{Text="地形・城郭は既存の試作表現です。開始時の所属や立場だけで、他人の兵・徴税権は得ません。",AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("d6bb7d")});view.SizeChanged+=()=>CallDeferred(nameof(Refresh));BuildUsableLayout(root,bar);BuildPlayGuide(bar);BuildWorkExperience(bar);Refresh();if(!loading)Save();
    }
    void Add(Node parent,string id,string text,Action action){var b=new Button{Text=text};parent.AddChild(b);buttons[id]=b;b.Pressed+=()=>{try{if(IsSaving||(IsClockControlling&&!ClockAllowed(id)))return;action();if(!IsClockControlling||id is "rotate" or "near" or "play-guide")Refresh();}catch(Exception e){message.Text="操作できません："+e.Message;RefreshWorkExperience();GD.Print("RUNTIME_REJECT "+e.Message);}};}
    void Submit(Intent intent)=>RequestLocalIntent(intent,intent.Action==ActionKind.Inspect?(intent.Actor==intent.Executor?"点検":"仕事の相談"):"操作");
    void Submit(CommandResult result){if(result.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted))throw new InvalidDataException(result.VisibleReason);message.Text=result.VisibleReason;}
    void Depart(){var w=Snapshot();RequestDeparture(TravelWorld.Move(w,"request:travel/"+Session.Revision,NationalRoutes.Nodes[destination.Selected].Id));}
    TaskState? Awaiting(){var w=Snapshot();var known=Branch(w.PlayerControl).Knowledge.Tasks.Select(t=>t.Id).ToHashSet();return w.Tasks.FirstOrDefault(t=>known.Contains(t.Id)&&t.Executor==w.PlayerControl&&t.Agreement==AgreementPhase.AwaitingAssent);}
    void Assent(bool accept){var task=Awaiting()??throw new InvalidDataException("回答できる依頼はありません");Submit(Session.Assent(task.Id,Session.PlayerControl,accept,Session.Revision));}
    void Open()=>OpenSelectedMail();
    internal void Advance(long to){if(IsSaving||IsClockControlling)return;automatic=false;fraction=0;while(Session.Seconds<to){Session.SetPaused(false);long prior=Session.Seconds;Session.AdvanceTo(to);if(Session.Seconds==prior)break;}Session.SetPaused(true);Refresh();}
    internal void Suspend(){CancelDeparture();CancelManualAdvance();CancelClockControl();CancelAdvancePrefetch();automatic=false;Session.SetPaused(true);Visible=false;ProcessMode=ProcessModeEnum.Disabled;}
    internal void Resume(){Visible=true;ProcessMode=ProcessModeEnum.Inherit;Refresh();}
    internal void Save(){CancelAdvancePrefetch();automatic=false;Session.SetPaused(true);saves.Save(Session,"manual",Snapshot().WorldId,CurrentPresentation());message.Text="同じ人物・世界の進行を保存しました。";}
    void ApplyPresentation(SavePresentation p){if(workOverlay!=null)CloseBottomTalk();if(historyOverlay!=null)historyOverlay.Hide();shownWork=null;consultation=null;RestoreOperationPanel(p.OperationPanel);selectedKnownTarget=p.MilitaryTarget;selectedDirectiveOriginal=p.MilitaryOriginal;selectedCancellationOriginal=p.MilitaryCancellation;fraction=p.DisplayFraction;speed=p.DisplaySpeed;if(speedChoice!=null)speedChoice.Select(Array.IndexOf(new[]{1,10,60,360},speed));if(p.WorldCamera is {} c){Span=(float)c.Span;Yaw=(float)c.Yaw;nationMode=!c.Follow;}}
    internal void Load(){CancelDeparture();CancelManualAdvance();CancelClockControl();CancelAdvancePrefetch();automatic=false;var prepared=saves.Prepare("manual");if(host.CommitLoad(prepared,Session,Session.Revision)!=ResultCode.Accepted)throw new InvalidDataException("現在の進行が変わったため読み直してください");ApplyPresentation(prepared.Presentation);message.Text="保存した人物・所属・外見・位置へ戻りました。";Refresh();}
    internal string InfoText=>info.Text;
    internal Rect2 RenderRect=>surface.GetGlobalRect();
    internal Rect2 InfoRect=>info.GetGlobalRect();
    internal void SaveCheckpoint(string slot)=>saves.Save(Session,slot,Snapshot().WorldId,CurrentPresentation());
    internal void LoadCheckpoint(string slot){CancelDeparture();CancelManualAdvance();CancelClockControl();CancelAdvancePrefetch();automatic=false;var p=saves.Prepare(slot);if(host.CommitLoad(p,Session,Session.Revision)!=ResultCode.Accepted)throw new InvalidDataException("checkpoint restore failed");ApplyPresentation(p.Presentation);Refresh();}
    internal bool ButtonEnabled(string id)=>!buttons[id].Disabled;
    internal void Press(string id){ShowOperation(buttons[id]);buttons[id].EmitSignal(Button.SignalName.Pressed);}
    internal void SetDestination(string id)=>destination.Select(Array.FindIndex(NationalRoutes.Nodes,n=>n.Id==id));
    internal void ReviewFraction(double value){fraction=value;Refresh();}
    RoutePoint RenderPosition(BranchView v)=>!v.Walking?v.Position:new(v.Position.EastMm+(long)Math.Round((v.NextPosition.EastMm-v.Position.EastMm)*fraction),v.Position.NorthMm+(long)Math.Round((v.NextPosition.NorthMm-v.Position.NorthMm)*fraction));
    static float[] V(Vector3 p)=>[p.X,p.Y,p.Z];
    internal string Pose()=>System.Text.Json.JsonSerializer.Serialize(new{Render=new{Size=new[]{view.Size.X,view.Size.Y},Surface=new[]{surface.Size.X,surface.Size.Y},Origin=terrainOrigin,Terrain=V(terrain.Position),Camera=V(camera.Position),Basis=new[]{V(camera.Basis.X),V(camera.Basis.Y),V(camera.Basis.Z)},Geometry=terrain.GeometryFingerprint()},OperationPanel,WorkConversation=WorkPose(),Person=Session.PlayerControl.Value,Position=Branch(Session.PlayerControl).Position,Session.Seconds,Yaw,Span,Speed=speed,Fraction=fraction,Appearance=NewGameWorld.Look(Snapshot().NewGame!,Session.PlayerControl.Value),Military=MilitaryPose(),Authority=AuthorityPose(),Actors=actors.OrderBy(p=>p.Key.Value).Where(p=>p.Value.Visible).Select(p=>new{Id=p.Key.Value,Position=V(p.Value.Position),Rotation=V(p.Value.Rotation),Lod=p.Value.Lod})});
    internal void Refresh()
    {
        if(camera==null)return;surface.Visible=!nationMode;overview.Visible=nationMode;var w=Snapshot();var own=Branch(Session.PlayerControl);var center=FieldWorld.FromWorld(RenderPosition(own));UpdateTerrain(center);float height=ContinuousWorld.Height(center)/1000f;
        float cameraSpan=NewGameMilitary.Deployed(w)&&Span>=90?Math.Max(Span,50f*view.Size.X/Math.Max(1,view.Size.Y)):Span;camera.Size=cameraSpan;camera.Near=.05f;camera.Far=3000;camera.Position=new(Mathf.Sin(Mathf.DegToRad(Yaw))*cameraSpan*.6f,height+cameraSpan*.7f,Mathf.Cos(Mathf.DegToRad(Yaw))*cameraSpan*.6f);camera.LookAt(new(0,height+1,0));
        foreach(var a in actors.Values)a.Visible=false;foreach(var l in labels.Values)l.Visible=false;
        var local=NearbyDisplay().Where(p=>p.Position!=own.Position).GroupBy(p=>p.Position).Select(g=>g.First()).Select(p=>(p.Actor,p.Position,p.Walking)).Prepend((w.PlayerControl,own.Position,own.Walking));
        foreach(var (id,position,walking) in local){if(!actors.TryGetValue(id,out var actor)){actor=new(){Scale=Vector3.One*2};stage.AddChild(actor);actor.Apply(ContractJson.Read<Officer3DCustomLook>(ContractJson.Write(NewGameWorld.Look(w.NewGame!,id.Value))));actors[id]=actor;var label=WorldLabel("10/person/"+id.Value,18);label.Text=w.Definitions.Characters.Single(p=>p.Id==id).Name;labels[id]=label;}
            var fp=FieldWorld.FromWorld(RenderPosition(Branch(id)));actor.Position=new((fp.X-center.X)/1000f,ContinuousWorld.Height(fp)/1000f,-(fp.Y-center.Y)/1000f);actor.Visible=true;var heading=Branch(id).Heading;actor.Rotation=new(0,Mathf.Atan2(heading.EastMm,-heading.NorthMm),0);bool fighting=id==w.PlayerControl&&!NewGameMilitary.Detached(w)&&ExerciseFighting(w,0);string animation=fighting?"attack":walking?"walk":"idle";if(fighting)actor.Rotation=new(0,Mathf.Pi/2,0);var clip=actor.Player.GetAnimation(actor.Clips[animation]);double time=walking?VisualDistance(w,id,fraction)/1000d/(actor.WalkStride*actor.Scale.Z)*clip.Length:Session.Seconds+fraction;actor.Freeze(animation,time%clip.Length);PlaceWorldLabel(labels[id],actor.Position+Vector3.Up*4.5f);labels[id].Visible=true;}
        RenderMilitary(w,center);ResolveWorldLabels();
        var p=w.NewGame!.Start.People.Single(p=>p.Id==w.PlayerControl.Value);var service=ServiceDisplay();string affiliation=service.House is {} house?w.Definitions.Organizations.Single(f=>f.Id==house).Name:"所属なし";string lord=service.Principal is {} principal?"直属："+w.Definitions.Characters.Single(q=>q.Id==principal).Name:"直属なし";var location=w.Locations.Single(l=>l.Actor==w.PlayerControl);string place=NewGameMilitary.Deployed(w)?"野外":location.Site is {} at?NewGameWorld.SiteName(at.Value):"道中";string role=service.Role;
        string abilities=p.Attributes==null?"能力は未採録":"統率"+p.Attributes.Leadership+"・武勇"+p.Attributes.Valor+"・知略"+p.Attributes.Intelligence+"・政務"+p.Attributes.Politics;
        info.Text=p.Name+" / "+role+" / "+affiliation+" / "+lord+" / "+place+" / "+abilities+"\n世界時間：開始から"+Session.Seconds+"秒 / "+(Session.Paused?"停止中":"進行中");
        var knowledge=Branch(w.PlayerControl).Knowledge;tasks.Text=string.Join(" / ",knowledge.Tasks.Select(t=>t.KnownStatus.Replace("Running","進行中").Replace("Completed","完了").Replace("Waiting","着手待ち").Replace("NotStarted","未着手").Replace("Stopped","中断").Replace("Failed","失敗")))+"\n届いた書状："+knowledge.Messages.Length+"通";
        RefreshMail();RefreshMilitary();RefreshAuthorityUI();RefreshKnownTarget();RefreshService();var awaiting=Awaiting();buttons["accept"].Visible=buttons["decline"].Visible=awaiting!=null;buttons["work"].Disabled=location.Kind!=LocationKind.AtSite||w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));
        buttons["inspect"].Disabled=location.Kind!=LocationKind.AtSite;buttons["depart"].Disabled=TravelWorld.Active(w);buttons["arrive"].Disabled=own.ArrivalAt==null;if(NewGameMilitary.Deployed(w)){buttons["depart"].Disabled=buttons["inspect"].Disabled=buttons["work"].Disabled=true;}RetainSaveInputLock();RetainClockInputLock();RetainManualAdvanceInputLock();RetainDepartureInputLock();RefreshWorkExperience();
    }
    ServiceView ServiceDisplay(){Snapshot();return cachedServiceView??=NewGameService.View(cachedWorld!);}
    void RefreshService()
    {
        var world=Snapshot();var view=ServiceDisplay();var principals=NewGameService.LocalPrincipals(world);var cases=view.Cases;
        if(!principals.SequenceEqual(servicePrincipalIds)){var chosen=servicePrincipal.Selected>=0&&servicePrincipal.Selected<servicePrincipalIds.Length?servicePrincipalIds[servicePrincipal.Selected]:(EntityId?)null;servicePrincipal.Clear();foreach(var id in principals)servicePrincipal.AddItem(world.Definitions.Characters.Single(c=>c.Id==id).Name);servicePrincipalIds=principals;int found=Array.FindIndex(principals,id=>id==chosen);if(found>=0)servicePrincipal.Select(found);}
        var ids=cases.Select(c=>c.Case).ToArray();int index=serviceCase.Selected;var prior=index>=0&&index<serviceCaseIds.Length?serviceCaseIds[index]:(EntityId?)null;if(!ids.SequenceEqual(serviceCaseIds)||Enumerable.Range(0,cases.Length).Any(i=>serviceCase.GetItemText(i)!=cases[i].Status)){serviceCase.Clear();foreach(var c in cases)serviceCase.AddItem(c.Status);serviceCaseIds=ids;int selected=Array.FindIndex(ids,id=>id==prior);if(selected>=0)serviceCase.Select(selected);else if(ids.Length>0)serviceCase.Select(ids.Length-1);}
        var card=serviceCase.Selected>=0&&serviceCase.Selected<cases.Length?cases[serviceCase.Selected]:null;buttons["service-apply"].Disabled=!view.CanApply||principals.Length==0;buttons["service-accept"].Disabled=card?.CanAccept!=true;buttons["service-decline"].Disabled=card?.CanDecline!=true;buttons["service-release"].Disabled=card?.CanRelease!=true;buttons["service-inquire"].Disabled=card==null;serviceBar.Visible=cases.Length>0||view.CanApply&&principals.Length>0;
    }
    void DecideService(CareerAction action)
    {
        EntityId? c=action==CareerAction.Apply?null:serviceCaseIds[serviceCase.Selected];EntityId? principal=action==CareerAction.Apply?servicePrincipalIds[servicePrincipal.Selected]:null;Submit(Session.DecideService(new(new("request:service-ui/"+Session.Revision),action,c,principal),Session.PlayerControl,Session.Revision));
    }
    // A display snapshot is immutable and valid only for this exact session/revision/time/pause.
    // Loading another instance at the same revision must never reuse the old person's view.
    ContractWorld Snapshot()
    {
        if(!ReferenceEquals(cachedSession,Session)||cachedRevision!=Session.Revision||cachedSeconds!=Session.Seconds||cachedPaused!=Session.Paused){cachedSession=Session;cachedRevision=Session.Revision;cachedSeconds=Session.Seconds;cachedPaused=Session.Paused;cachedWorld=Session.DiagnosticSnapshot();cachedBranches.Clear();cachedServiceView=null;cachedNearby=null;}
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
        if(terrainTile!=tile||terrainSpan!=Span){terrain.Position=Vector3.Zero;terrainOrigin=ContinuousWorld.Point(tile.Node,tile.X*ContinuousWorld.Tile,tile.Y*ContinuousWorld.Tile);var terrainTimer=System.Diagnostics.Stopwatch.StartNew();terrain.Update(terrainOrigin,Math.Min(Span,240f));TerrainBuildTestHook?.Invoke(terrainTimer.Elapsed.TotalMilliseconds);terrainTile=tile;terrainSpan=Span;}
        terrain.Position=new((terrainOrigin.X-center.X)/1000f,0,-(terrainOrigin.Y-center.Y)/1000f);
    }
    static double MilitaryDistance(ContractWorld w,int unit,double time)
    {
        var all=w.NewMilitary!.Orders;
        return all.Sum(o=>{double until=Math.Min(time,all.FirstOrDefault(n=>n.Revision>o.Revision)?.At??double.MaxValue);return o.Routes[unit].Sum(l=>RegionalGround.Distance(l.From,l.To)*Math.Max(0,Math.Min(until,l.EndAt)-Math.Max(o.At,l.StartAt))/(l.EndAt-l.StartAt));});
    }
    internal static double VisualDistance(ContractWorld world,EntityId actor,double phase)
    {
        if(actor==world.PlayerControl&&NewGameMilitary.Detached(world)){var j=world.NewMilitary!.Withdrawal!.Journeys.Last();double t=world.ElapsedSeconds+phase;return j.Route.Sum(l=>RegionalGround.Distance(l.From,l.To)*Math.Clamp((t-l.StartAt)/(l.EndAt-l.StartAt),0,1));}
        var journey=world.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor);if(NewGameMilitary.Deployed(world)){return MilitaryDistance(world,0,world.ElapsedSeconds+phase);}if(journey==null)return 0;
        var nodes=BranchRoutes.GraphNodes(world);var edges=BranchRoutes.GraphEdges(world);double distance=0;
        foreach(var leg in journey.Legs){var a=nodes.Single(n=>n.Id==leg.From).Position;var b=nodes.Single(n=>n.Id==leg.To).Position;double dx=b.EastMm-a.EastMm,dy=b.NorthMm-a.NorthMm;double length=Math.Sqrt(dx*dx+dy*dy);var edge=edges.Single(e=>e.Id==leg.Edge);double p=TravelWorld.Progress(world,leg,world.ElapsedSeconds),next=TravelWorld.Progress(world,leg,world.ElapsedSeconds+1);distance+=length*Math.Clamp((p+(next-p)*phase)/edge.LengthMm,0,1);}
        return distance;
    }
    public override void _Process(double delta){if(PollDeparture(delta))return;if(PollManualAdvance(delta))return;if(PollManualSave(delta))return;if(PollClockControl(delta))return;if(PollPreparedAdvance(delta))return;if(!automatic||Session.Paused)return;fraction+=Math.Min(delta,.25)*speed;long steps=(long)fraction;if(steps>0){fraction-=steps;Session.AdvanceTo(Session.Seconds+steps);}Refresh();}
}
