using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Simulation;

// No MilitaryDiagnostic or live enemy fields may be read here: all units come from MilitaryViewFor.
public partial class MilitaryGridStage : VBoxContainer
{
    public TaxPanel Session {get;private set;}=null!;
    public SubViewport View {get;private set;}=null!;
    public Dictionary<string,Button> Actions {get;}=new();
    public Dictionary<string,Officer3DCustomActor> Actors {get;}=new();
    public Dictionary<string,MeshInstance3D> Ghosts {get;}=new();
    public OptionButton ObserverChoice {get;private set;}=null!;
    public OptionButton DestinationChoice {get;private set;}=null!;
    public Label Status {get;private set;}=null!;
    public Label Details {get;private set;}=null!;
    public Label Notice {get;private set;}=null!;
    public string Observer {get;private set;}="character-001";
    public MilitaryView? LastView {get;private set;}
    readonly Dictionary<string,Label3D> labels=new();
    readonly Dictionary<string,string> signatures=new();
    readonly List<(MapCell Cell,StandardMaterial3D Material)> terrain=new();
    readonly Dictionary<string,string> names=new();
    int sequence;
    readonly MapCell[] destinations={new(6,3),new(4,3),new(0,3),new(10,3),new(7,3)};
    public static Vector3 CellPosition(MapCell c)=>new((c.X-5)*1.5f,0,(c.Z-3)*1.5f);
    public void Initialize(TaxPanel session)
    {
        bool test=OS.GetCmdlineUserArgs().Any(a=>a=="--military-grid-test"||a=="--military-grid-restore");MilitaryGridGate.Require(!test);Session=session;
        if(OS.GetCmdlineUserArgs().Contains("--military-grid-show"))DisplayServer.WindowSetSize(new(1600,1000));
        AddChild(new Label{Text="部隊・軍令・認識・地形 / 小規模演習"});
        AddChild(new Label{Text="表示は選択武将の目撃・受領情報のみ。軍事数値と地形は検証用です。",Modulate=new Color("d9c58f")});
        var bar=new HFlowContainer();AddChild(bar);
        foreach(var (key,text) in new[]{("advance","1刻進める"),("pause","停止 / 再開"),("save","通常保存"),("load","通常復旧"),("reset","世界を初期化"),("submit","徴税命令")})AddButton(bar,key,text,()=>Session.InvokeFrom3D(key));
        AddButton(bar,"auto","自動進行 切替",()=>Session.Automatic=!Session.Automatic);foreach(int speed in new[]{1,2,4})AddButton(bar,"speed"+speed,speed+"倍",()=>Session.SetClockSpeed(speed));
        var controls=new HFlowContainer();AddChild(controls);AddButton(controls,"start","演習部隊を配置",()=>Session.ChangeIntegration(s=>s.StartMilitaryMap()));
        ObserverChoice=new OptionButton();controls.AddChild(ObserverChoice);foreach(var c in session.Simulation.RestoreWorld().Definition.Characters){names[c.Id]=c.Name;ObserverChoice.AddItem(c.Name+" の視点");}
        ObserverChoice.ItemSelected+=i=>{Observer=names.Keys.ElementAt((int)i);DestinationChoice.Select(Observer=="character-002"?1:0);Synchronize();};
        DestinationChoice=new OptionButton();foreach(var text in new[]{"東側の橋詰","西側の橋詰","西の退路","東の退路","東の街道"})DestinationChoice.AddItem(text);controls.AddChild(DestinationChoice);
        AddButton(controls,"move","移動命令",()=>Send(MilitaryAction.Move));AddButton(controls,"attack","交戦許可付き進軍",()=>Send(MilitaryAction.Advance));
        AddButton(controls,"withdraw","退路へ撤退",()=>Send(MilitaryAction.Withdraw));
        AddButton(controls,"cancel","直前の命令を取消（2刻後に有効）",()=>
        {
            var o=Outgoing(false);Session.ChangeIntegration(s=>s.SendMilitary(Next("cancel"),Observer,o.UnitId,MilitaryAction.Cancel,new(0,3),effectiveAt:s.Tick+2,cancels:o.Id));
        });
        var fixture=new HFlowContainer();AddChild(fixture);
        AddButton(fixture,"opponent","試験：相手へ進軍指示",()=>Session.ChangeIntegration(s=>s.SendMilitary(Next("opponent"),"character-002","unit-blue",MilitaryAction.Advance,new(4,3))));
        AddButton(fixture,"fail","試験：直前の軍令を紛失",()=>Session.ChangeIntegration(s=>s.FailMessage(MilitaryFlow.OrderMessage(Outgoing().Id))));
        AddButton(fixture,"failure-report","試験：失敗報告を発送",()=>
        {
            var o=Outgoing();var attempt=Session.Simulation.MessageAttempts.Where(a=>a.MessageId==MilitaryFlow.OrderMessage(o.Id)).MaxBy(a=>a.Number)!;
            Session.ChangeIntegration(s=>s.ReportMessageFailure(attempt.Id,Next("failure")));
        });
        AddButton(fixture,"retry","失敗報告を確認して再送",()=>Session.ChangeIntegration(s=>s.RetryMilitary(Outgoing().Id,Observer)));
        AddButton(fixture,"late","試験：原本を1刻後に遅着",()=>Session.ChangeIntegration(s=>s.ScheduleLateMessage(MilitaryFlow.OrderMessage(Outgoing().Id),1,1)));
        AddButton(fixture,"deadline","試験：到達前に期限が来る軍令",()=>Send(MilitaryAction.Move,2,1));
        AddButton(fixture,"intel","目撃情報を三郎へ報告",()=>
        {
            var enemy=LastView?.Units.Where(u=>!u.Own).OrderByDescending(u=>u.ObservedAt).FirstOrDefault()??throw new InvalidOperationException("まだ相手を目撃・認識していません。");
            // The read model identifies the known fact; the adapter resolves only this person's learned record.
            Session.ChangeIntegration(s=>s.SendKnownMilitaryIntel(Next("intel"),Observer,"character-003",enemy.UnitId,3));
        });
        fixture.Visible=false;AddButton(bar,"fixture-toggle","配送試験の操作を表示",()=>fixture.Visible=!fixture.Visible);
        var looks=new HFlowContainer();AddChild(looks);looks.AddChild(new Label{Text="選択武将の外見："});
        foreach(var (id,text) in new[]{("commander","銀具足"),("veteran","壮年"),("red_guard","赤具足")})AddButton(looks,"look-"+id,text,()=>Session.ChangeIntegration(s=>s.AssignAppearance(Observer,Officer3DCustomActor.Preset(id))));
        var imported=new OptionButton();imported.CustomMinimumSize=new(180,0);looks.AddChild(imported);Officer3DCustomRoster? roster=null;
        AddButton(looks,"import","作成一覧を読む",()=>{var parsed=JsonSerializer.Deserialize<Officer3DCustomRoster>(System.IO.File.ReadAllText(CreatorImportPath))??throw new ArgumentException();parsed.Validate();roster=parsed;imported.Clear();foreach(var o in parsed.Officers)imported.AddItem(o.Name);});
        AddButton(looks,"apply-import","選んだ外見を適用",()=>{if(roster is null)throw new InvalidOperationException("一覧を先に読み込んでください。");Session.ChangeIntegration(s=>s.AssignAppearance(Observer,roster.Officers[imported.Selected].Appearance));});
        Status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(Status);Notice=new Label{Text="配置して移動先を選び、時刻を進めてください。",AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(Notice);
        var row=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};AddChild(row);var vc=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(600,360)};row.AddChild(vc);
        View=new SubViewport{OwnWorld3D=true,Size=new(1050,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};vc.AddChild(View);var world=new Node3D();View.AddChild(world);
        var env=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("16242a"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.35f,TonemapMode=Godot.Environment.ToneMapper.Linear};env.Sky=new Sky{SkyMaterial=new ProceduralSkyMaterial()};env.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;world.AddChild(new WorldEnvironment{Environment=env});world.AddChild(new DirectionalLight3D{RotationDegrees=new(-48,-30,0),LightEnergy=.4f,ShadowEnabled=true});
        for(int x=0;x<MilitaryMap.Width;x++)for(int z=0;z<MilitaryMap.Height;z++)
        {
            var c=new MapCell(x,z);var mat=new StandardMaterial3D{AlbedoColor=TerrainColor(c),Roughness=1};terrain.Add((c,mat));
            world.AddChild(new MeshInstance3D{Position=CellPosition(c)+new Vector3(0,-.1f,0),Mesh=new BoxMesh{Size=new(1.46f,.16f,1.46f)},MaterialOverride=mat});
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Rock)world.AddChild(new MeshInstance3D{Position=CellPosition(c)+new Vector3(0,.2f,0),Mesh=new PrismMesh{Size=new(1,.6f,1)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("454f52")}});
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Forest)world.AddChild(new MeshInstance3D{Position=CellPosition(c)+new Vector3(0,.4f,0),Mesh=new CylinderMesh{TopRadius=0,BottomRadius=.45f,Height=.9f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("294b32")}});
            if(MilitaryMap.Terrain(c)==MilitaryTerrain.Bridge)
            {
                foreach(float side in new[]{-.55f,.55f})world.AddChild(new MeshInstance3D{Position=CellPosition(c)+new Vector3(0,.08f,side),Mesh=new BoxMesh{Size=new(1.45f,.12f,.06f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("c3a16b")}});
                foreach(float plank in new[]{-.45f,0,.45f})world.AddChild(new MeshInstance3D{Position=CellPosition(c)+new Vector3(plank,.01f,0),Mesh=new BoxMesh{Size=new(.04f,.035f,1.05f)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("6b533a")}});
            }
        }
        var camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=13.5f,Position=new(0,14,12),Current=true};world.AddChild(camera);camera.LookAt(Vector3.Zero);
        foreach(var (unit,character) in new[]{("unit-red","character-001"),("unit-blue","character-002")})
        {
            var actor=new Officer3DCustomActor{Name=unit};world.AddChild(actor);actor.Visible=false;Actors[unit]=actor;
            var ghost=new MeshInstance3D{Mesh=new SphereMesh{Radius=.28f,Height=.56f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("d2b36a"),Roughness=1},Visible=false};world.AddChild(ghost);Ghosts[unit]=ghost;
            var label=new Label3D{FontSize=27,PixelSize=.009f,OutlineSize=7,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true,Visible=false};world.AddChild(label);labels[unit]=label;
        }
        var scroll=new ScrollContainer{CustomMinimumSize=new(330,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(scroll);Details=new Label{CustomMinimumSize=new(310,0),AutowrapMode=TextServer.AutowrapMode.WordSmart};scroll.AddChild(Details);
        Session.WorldChanged+=_=>Synchronize();Synchronize();
    }
    public string CreatorImportPath {get;set;}=ProjectSettings.GlobalizePath("user://officer3d-v3-roster.json");
    string Next(string prefix)=>"military-ui/"+prefix+"/"+Session.Simulation.Tick+"/"+(++sequence);
    MilitaryVisibleOrder Outgoing(bool includeCancel=true)=>LastView?.Orders.LastOrDefault(o=>o.IssuerId==Observer&&(includeCancel||o.Action!=MilitaryAction.Cancel))??throw new InvalidOperationException("この武将が発送した軍令はありません。");
    void Send(MilitaryAction action,int delay=1,int? deadline=null)
    {
        var own=LastView?.Units.FirstOrDefault(u=>u.Own)??throw new InvalidOperationException("この視点には指揮する部隊がありません。");
        var target=action==MilitaryAction.Withdraw?(own.ArmyId=="army-red"?new MapCell(0,3):new(10,3)):destinations[DestinationChoice.Selected];
        Session.ChangeIntegration(s=>s.SendMilitary(Next(action.ToString()),Observer,own.UnitId,action,target,delay,deadline is null?null:s.Tick+deadline));
    }
    void AddButton(Container parent,string key,string text,Action action)
    {var button=new Button{Text=text};parent.AddChild(button);Actions[key]=button;button.Pressed+=()=>{try{action();Notice.Text="操作を反映しました。配送結果と履行結果は報告で確認します。";}catch(Exception e){Notice.Text="操作できません："+e.Message;GD.Print("MILITARY_INPUT_REJECTED "+e.GetType().Name);}};}
    static Color TerrainColor(MapCell c)=>new(MilitaryMap.Terrain(c) switch{MilitaryTerrain.River=>"345f78",MilitaryTerrain.Bridge=>"a38a60",MilitaryTerrain.Road=>"988967",MilitaryTerrain.Forest=>"3d6241",MilitaryTerrain.Rock=>"535c5d",_=>"687c54"});
    public override void _Process(double delta){if(Session is not null)Synchronize();}
    public void Synchronize()
    {
        foreach(var a in Actors.Values)a.Visible=false;foreach(var a in Ghosts.Values)a.Visible=false;foreach(var a in labels.Values)a.Visible=false;
        if(!Session.Simulation.HasMilitaryMap){LastView=null;Status.Text=Session.Simulation.HasLegacyField?"旧検証の戦場を保持しています。旧入口で復旧するか、世界を初期化して新しい演習を配置してください。":"部隊未配置 / 世界時刻 "+Session.Simulation.Tick;Details.Text="川は橋で渡ります。林・岩は見通しを遮ります。\n\n視点切替は認識を検査するための操作です。\n相手の状態は観測・報告されるまで表示しません。";return;}
        LastView=Session.Simulation.MilitaryViewFor(Observer);double f=Session.PresentationFraction;
        foreach(var u in LastView.Units.Where(u=>u.Present))
        {
            if(!Actors.TryGetValue(u.UnitId,out var actor))continue;
            var label=labels[u.UnitId];label.Visible=true;
            if(!u.Current&&!u.Own)
            {var ghost=Ghosts[u.UnitId];ghost.Visible=true;ghost.Position=CellPosition(u.Cell)+new Vector3(0,.3f,0);label.Position=ghost.Position+new Vector3(0,1,0);label.Text=$"{(u.Evidence=="Report"?"報告":"記憶")} 第{u.ObservedAt}刻\n兵{u.Soldiers}（当時）";continue;}
            var appearance=SnapshotCodec.Write(Session.Simulation.AppearanceFor(u.CommanderId));if(!signatures.TryGetValue(u.UnitId,out var old)||old!=appearance){actor.Apply(SnapshotCodec.Read<Officer3DCustomLook>(appearance));signatures[u.UnitId]=appearance;}
            actor.Visible=true;actor.SetLod("mid");var previous=CellPosition(u.Previous);var current=CellPosition(u.Cell);actor.Position=previous.Lerp(current,(float)f);
            var direction=current-previous;if(direction.LengthSquared()>.001f)actor.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
            else actor.RotationDegrees=new(0,u.ArmyId=="army-red"?90:-90,0);
            string clip="idle";double t=(LastView.Tick+f)*.7;
            if(direction.LengthSquared()>.001f){clip="walk";double distance=(u.Own?Math.Max(0,u.TravelCells-1)*1.5:0)+direction.Length()*f;t=distance/actor.WalkStride*actor.Player.GetAnimation(actor.Clips[clip]).Length;}
            else if(u.Phase==MilitaryPhase.Fighting){clip="attack";t=f*actor.Player.GetAnimation(actor.Clips[clip]).Length;}
            actor.Freeze(clip,t%actor.Player.GetAnimation(actor.Clips[clip]).Length);label.Position=actor.Position+new Vector3(0,2.05f,0);label.Text=$"{(u.ArmyId=="army-red"?"赤隊":"青隊")} {u.Soldiers}\n士気 {u.Morale}";
        }
        var observerCell=LastView.Units.FirstOrDefault(u=>u.Own)?.Cell??new MapCell(0,6);
        foreach(var (cell,mat) in terrain)mat.AlbedoColor=TerrainColor(cell)*(MilitaryMap.CanSee(observerCell,cell)?1f:.48f);
        Status.Text=$"世界時刻 {LastView.Tick}刻 / {names[Observer]}の認識 / {(Session.Simulation.Paused?"停止中":Session.Automatic?"自動":"手動")} {Session.ClockSpeed}倍 / 城の米 {Session.Simulation.TreasuryRice}";
        Details.Text="認識した部隊\n\n"+string.Join("\n\n",LastView.Units.Select(u=>$"{(u.ArmyId=="army-red"?"赤隊":"青隊")}：{(u.Current?"現在の把握":u.Present?"過去の観測":"既知地点に姿なし")}\n観測 第{u.ObservedAt}刻 / {Evidence(u.Evidence)}\n{(u.Present?$"兵{u.Soldiers}・士気{u.Morale} / {Phase(u.Phase)}":"現在位置・兵数は不明")}"))+"\n\n軍令と本人が知る結果\n\n"+string.Join("\n\n",LastView.Orders.Select(o=>$"{ActionName(o.Action)}\n{KnownStatus(o.KnownStatus)}{(o.RetryAvailable?"\n再送の判断が可能":"")}"));
    }
    static string ActionName(MilitaryAction a)=>a switch{MilitaryAction.Move=>"移動",MilitaryAction.Advance=>"交戦許可付き進軍",MilitaryAction.Withdraw=>"撤退",_=>"取消"};
    static string Evidence(string s)=>s switch{"Own"=>"本人の把握","Sight"=>"直接目撃","Report"=>"受領した報告",_=>"既知地点の再確認"};
    static string KnownStatus(string text){var split=text.Split(':',2);if(split.Length==1)return text;return (split[0] switch{"Applied"=>"実行・効力を確認","Refused"=>"指示を拒否","Expired"=>"期限切れ","Cancelled"=>"取消済み",_=>"結果"})+"："+split[1];}
    static string Phase(MilitaryPhase? p)=>p switch{MilitaryPhase.Holding=>"待機",MilitaryPhase.Marching=>"移動",MilitaryPhase.Contact=>"接敵",MilitaryPhase.Fighting=>"交戦",MilitaryPhase.Routing=>"敗走",MilitaryPhase.Withdrawing=>"撤退",MilitaryPhase.Exited=>"退場",_=>"戦闘不能"};
}
