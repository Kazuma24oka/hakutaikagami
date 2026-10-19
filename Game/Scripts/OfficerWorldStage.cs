using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Simulation;

// Observes the single TaxPanel transaction boundary. All movement and losses come from Core.
public partial class OfficerWorldStage : VBoxContainer
{
    public TaxPanel Session {get;private set;}=null!;
    public SubViewport View {get;private set;}=null!;
    public Camera3D Camera {get;private set;}=null!;
    public Dictionary<string,Officer3DCustomActor> Actors {get;}=new();
    public Dictionary<string,Button> Actions {get;}=new();
    public OptionButton Selection {get;private set;}=null!;
    public Label Status {get;private set;}=null!;
    public Label Details {get;private set;}=null!;
    Label battleSummary=null!;
    readonly Dictionary<string,Label3D> markers=new();
    readonly Dictionary<string,string> displayNames=new();
    public string SelectedId {get;private set;}="character-001";
    readonly Dictionary<string,OptionButton> equipment=new();
    readonly Dictionary<string,ColorPickerButton> colors=new();
    OfficerIntegration viewState=null!;
    readonly Dictionary<string,string> signatures=new();
    bool refreshing;
    int orderNumber;
    public void Initialize(TaxPanel session)
    {
        OfficerWorldGate.Require(OS.GetCmdlineUserArgs().All(a=>a!="--officer-world-test"&&a!="--officer-world-restore"));Session=session;Session.Simulation.EnableOfficerIntegration();
        AddChild(new Label{Text="武将・外見・小規模合戦 / 共通世界時刻の縦断検証"});
        AddChild(new Label{Text="検証用管理画面：敵味方の真実を表示。正式な戦場認識UI・損害式ではありません。",Modulate=new Color("dfc58d")});
        var bar=new HFlowContainer();AddChild(bar);
        foreach(var (key,label) in new[]{("advance","1刻進める"),("pause","停止 / 再開"),("save","通常保存"),("load","通常復旧"),("submit","徴税命令"),("reset","世界を初期化")})
            Button(bar,key,label,()=>{Session.InvokeFrom3D(key);});
        Button(bar,"auto","自動進行 切替",()=>Session.Automatic=!Session.Automatic);
        foreach(int speed in new[]{1,2,4})Button(bar,"speed"+speed,speed+"倍",()=>Session.SetClockSpeed(speed));
        var battle=new HFlowContainer();AddChild(battle);
        Button(battle,"start","検証用の部隊を配置",()=>Session.ChangeIntegration(s=>s.StartFieldFixture()));
        Button(battle,"engage","両部隊へ交戦指示（1刻後到達）",()=>Session.ChangeIntegration(s=>s.EngageFieldFixture()));
        Button(battle,"withdraw","選択部隊へ撤退指示",()=>Session.ChangeIntegration(s=>
        {
            var u=s.OfficerState?.Battle?.Units.SingleOrDefault(u=>u.CommanderId==SelectedId)??throw new InvalidOperationException("この武将の部隊は未配置です。");
            string id;do{id="fixture-withdraw-"+(++orderNumber);}while(s.OfficerState!.Battle!.Directives.Any(d=>d.Id==id));
            s.DispatchField(id,SelectedId,u.Id,FieldAction.Withdraw);
        }));
        Status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(Status);
        battleSummary=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};AddChild(battleSummary);
        var row=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};AddChild(row);
        var vc=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(480,360)};row.AddChild(vc);
        View=new SubViewport{OwnWorld3D=true,Size=new(920,650),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};vc.AddChild(View);
        var world=new Node3D();View.AddChild(world);
        var env=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263337"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.35f,TonemapMode=Godot.Environment.ToneMapper.Linear};
        env.Sky=new Sky{SkyMaterial=new ProceduralSkyMaterial()};env.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;world.AddChild(new WorldEnvironment{Environment=env});
        world.AddChild(new DirectionalLight3D{RotationDegrees=new(-45,-35,0),LightEnergy=.4f,ShadowEnabled=true});
        world.AddChild(new MeshInstance3D{Mesh=new PlaneMesh{Size=new(100,60)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("435647"),Roughness=1}});
        // Exit boundary markers remain world objects; leaving a field is not deleting the officer.
        foreach(int x in new[]{-10,10})world.AddChild(new MeshInstance3D{Position=new(x,.015f,0),Mesh=new BoxMesh{Size=new(.05f,.03f,8)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("c4b98b")}});
        Camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=8,Position=new(0,5,9),Current=true};world.AddChild(Camera);Camera.LookAt(new Vector3(0,.8f,0));
        foreach(var c in Session.Simulation.RestoreWorld().Definition.Characters)
        {
            var a=new Officer3DCustomActor{Name=c.Id};world.AddChild(a);Actors[c.Id]=a;
            displayNames[c.Id]=c.Name;
            var label=new Label3D{FontSize=28,PixelSize=.009f,OutlineSize=6,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true};world.AddChild(label);markers[c.Id]=label;
        }
        var scroll=new ScrollContainer{CustomMinimumSize=new(340,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(scroll);var edit=new VBoxContainer{CustomMinimumSize=new(320,0)};scroll.AddChild(edit);
        edit.AddChild(new Label{Text="武将IDへ外見を割り当て"});Selection=new OptionButton();edit.AddChild(Selection);
        foreach(var c in Session.Simulation.RestoreWorld().Definition.Characters)Selection.AddItem(c.Name);
        Selection.ItemSelected+=i=>{SelectedId=Session.Simulation.RestoreWorld().Definition.Characters[(int)i].Id;RefreshEditor();};
        var presets=new HFlowContainer();edit.AddChild(presets);
        foreach(var (id,label) in new[]{("commander","銀具足"),("veteran","壮年"),("red_guard","赤具足")})Button(presets,"look-"+id,label,()=>Assign(Officer3DCustomActor.Preset(id)));
        Button(edit,"import","作成済み一覧を読み込む",()=>ImportCreatorFile(ProjectSettings.GlobalizePath("user://officer3d-v3-roster.json")));
        var choices=new OptionButton();edit.AddChild(choices);Officer3DCustomRoster? imported=null;
        importChoice=choices;setImported=r=>imported=r;
        Button(edit,"apply-import","一覧の外見を選択武将へ適用",()=>{if(imported is null)throw new InvalidOperationException("先に一覧を読み込んでください。");Assign(imported.Officers[choices.Selected].Appearance);});
        foreach(var (key,label) in new[]{("skin","肌"),("hair","髪・髭の乗算色"),("cloth","布の乗算色"),("lacquer","漆の乗算色"),("metal","金属の乗算色")})
        {
            edit.AddChild(new Label{Text=label});var p=new ColorPickerButton{EditAlpha=false,CustomMinimumSize=new(0,30)};edit.AddChild(p);colors[key]=p;
            p.ColorChanged+=c=>{if(refreshing)return;var a=Look();if(key=="skin"){var l=c.SrgbToLinear();a.SkinLinear=new[]{l.R,l.G,l.B};}else if(key=="hair")a.HairSrgb=new[]{c.R,c.G,c.B};else a.MaterialSrgb[key]=new[]{c.R,c.G,c.B};Assign(a);};
        }
        var labels=new[]{"面頬","刀","下衣","陣羽織","兜","胴","首","肩","腕","履物","腰","腿","脛"};
        foreach(var slot in AppearanceCatalog.Slots)
        {
            edit.AddChild(new Label{Text=labels[Array.IndexOf(AppearanceCatalog.Slots,slot)]});var pick=new OptionButton();pick.AddItem("外す");pick.AddItem("銀 / 共通部品");if(AppearanceCatalog.RedSlots.Contains(slot))pick.AddItem("赤具足");edit.AddChild(pick);equipment[slot]=pick;
            pick.ItemSelected+=i=>{if(refreshing)return;var a=Look();a.Enabled[slot]=i!=0;if(i>0)a.Equipment[slot]=i==1?"silver":"red_lamellar";Assign(a);};
        }
        Details=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};edit.AddChild(Details);
        Session.WorldChanged+=_=>{viewState=Session.Simulation.OfficerState!;Synchronize();RefreshEditor();};viewState=Session.Simulation.OfficerState!;Synchronize();RefreshEditor();
    }
    OptionButton importChoice=null!;Action<Officer3DCustomRoster> setImported=null!;
    public void ImportCreatorFile(string path)
    {
        var r=JsonSerializer.Deserialize<Officer3DCustomRoster>(System.IO.File.ReadAllText(path))??throw new ArgumentException();r.Validate();
        importChoice.Clear();foreach(var o in r.Officers)importChoice.AddItem(o.Name);setImported(r);
    }
    public void Assign(OfficerAppearance appearance){Session.ChangeIntegration(s=>s.AssignAppearance(SelectedId,appearance));}
    Officer3DCustomLook Look()=>SnapshotCodec.Read<Officer3DCustomLook>(SnapshotCodec.Write(viewState.Looks[SelectedId]));
    Button Button(Container parent,string id,string label,Action action)
    {
        var b=new Button{Text=label};parent.AddChild(b);Actions[id]=b;b.Pressed+=()=>{try{action();}catch(Exception e){Status.Text="操作を反映できません："+e.Message;GD.Print("FIELD_REJECTED "+e.GetType().Name);}};return b;
    }
    void RefreshEditor()
    {
        refreshing=true;var a=Look();foreach(var (slot,p) in equipment)p.Select(!a.Enabled[slot]?0:a.Equipment[slot]=="silver"?1:2);
        foreach(var (key,p) in colors){var c=key=="skin"?a.SkinLinear:key=="hair"?a.HairSrgb:a.MaterialSrgb[key];var color=new Color(c[0],c[1],c[2]);p.Color=key=="skin"?color.LinearToSrgb():color;}
        refreshing=false;
    }
    public override void _Process(double delta){if(Session is not null)Synchronize();}
    public void Synchronize()
    {
        double f=Session.PresentationFraction;var b=viewState.Battle;
        int index=0;foreach(var (id,a) in Actors)
        {
            var look=viewState.Looks[id];string signature=SnapshotCodec.Write(look);if(!signatures.TryGetValue(id,out var old)||old!=signature){a.Apply(SnapshotCodec.Read<Officer3DCustomLook>(signature));signatures[id]=signature;}
            var u=b?.Units.SingleOrDefault(u=>u.CommanderId==id);a.Visible=u?.Phase is not FieldPhase.Exited and not FieldPhase.Incapacitated;
            string clip="idle";double t=(Session.Simulation.Tick+f)*.7;
            if(u is null){a.Position=new((index++-1)*2.2f,0,b is null?0:3.5f);a.Rotation=Vector3.Zero;}
            else
            {
                // Interpolate the last committed step; no predicted movement or damage.
                float x=Mathf.Lerp(u.PreviousX,u.X,(float)f)/1000;a.Position=new(x,0,0);
                bool moving=u.X!=u.PreviousX;int direction=moving?Math.Sign(u.X-u.PreviousX):u.Side=="fixture-red"?1:-1;
                a.RotationDegrees=new(0,direction*90,0);
                if(moving){clip="walk";double distance=(u.Travel-Math.Abs(u.X-u.PreviousX)+Math.Abs(u.X-u.PreviousX)*f)/1000;t=distance/a.WalkStride*a.Player.GetAnimation(a.Clips[clip]).Length;}
                else if(u.ActionAt==Session.Simulation.Tick&&u.Phase==FieldPhase.Engaged){clip="attack";t=f*a.Player.GetAnimation(a.Clips[clip]).Length;}
            }
            a.SetLod("mid");a.Freeze(clip,t%a.Player.GetAnimation(a.Clips[clip]).Length);
            var marker=markers[id];marker.Position=a.Position+new Vector3(0,2.05f,0);marker.Visible=a.Visible;
            marker.Text=u is null?(b is null?displayNames[id]:"待機中"):$"{(u.Side=="fixture-red"?"赤隊":"青隊")} {u.Soldiers}\n士気 {u.Morale}";
        }
        Camera.Size=b is null?6:13;
        Status.Text=$"世界時刻 {Session.Simulation.Tick}刻 / {(Session.Simulation.Paused?"停止中":Session.Automatic?"自動進行":"手動進行")} / {Session.ClockSpeed}倍 / 城の米 {Session.Simulation.TreasuryRice} / 徴税命令 {Session.Simulation.Orders.Count}件";
        battleSummary.Text=b is null?"部隊未配置：外見を編集できます。":string.Join("  ／  ",b.Units.Select(u=>$"{(u.Side=="fixture-red"?"赤隊":"青隊")}：{Phase(u.Phase)}　兵{u.Soldiers}・士気{u.Morale}"));
        Details.Text=b is null?"部隊を配置してから交戦指示を送ります。\n外見変更は能力・所属・命令へ影響しません。":string.Join("\n\n",b.Units.Select(u=>$"{u.Id} / {u.CommanderId}\n{Phase(u.Phase)} / 兵 {u.Soldiers}/{u.InitialSoldiers}\n士気 {u.Morale} / 位置 {u.X/1000.0:F1}m"))+"\n\n指示到達 "+b.Directives.Count(d=>d.Delivered)+" / 損害記録 "+b.Losses.Length;
    }
    static string Phase(FieldPhase p)=>p switch{FieldPhase.Standing=>"待機",FieldPhase.Marching=>"移動",FieldPhase.Contact=>"接敵",FieldPhase.Engaged=>"交戦",FieldPhase.Routing=>"敗走",FieldPhase.Withdrawing=>"撤退",FieldPhase.Exited=>"戦場を退場",_=>"戦闘不能"};
}
