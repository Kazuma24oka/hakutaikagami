using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Simulation;

// Presentation only: TaxPanel's existing session owns tick, orders, RNG and persistence.
public partial class Officer3DStage : VBoxContainer
{
    public TaxPanel Session {get; private set;}=null!;
    public SubViewport View {get; private set;}=null!;
    public Node3D World {get; private set;}=null!;
    public Camera3D Camera {get; private set;}=null!;
    public Dictionary<string,Officer3DActor> Actors {get;}=new();
    public Label Status {get; private set;}=null!;
    public bool Automatic {get=>Session is not null&&Session.Automatic;set{if(Session is not null)Session.Automatic=value;}}
    public double Fraction {get;private set;}
    public bool AutomaticLod {get;set;}=true;
    public string SelectedId {get;set;}="character-002";
    readonly List<Action> editorUpdates=new();
    void RefreshEditor(){foreach(var update in editorUpdates)update();}
    public readonly Dictionary<string,Vector3> Stations=new() {["character-001"]=new(-3,0,0),["character-002"]=new(3,0,0),["character-003"]=new(-3,0,1.5f)};
    public override void _Ready() {SetProcess(false);}
    public void Initialize(TaxPanel session)
    {
        Session=session;
        var title=new Label{Text="3D武将 / 共通世界時間・命令配送（管理用の試作画面）"};AddChild(title);
        var toolbar=new HFlowContainer();AddChild(toolbar);
        foreach(var op in new[]{("submit","徴税命令を発送"),("advance","1刻進める"),("pause","停止 / 再開"),("save","保存"),("load","復旧")})
        {var b=new Button{Text=op.Item2};toolbar.AddChild(b);b.Pressed+=()=>{Session.InvokeFrom3D(op.Item1);Fraction=0;Synchronize();};}
        var auto=new CheckButton{Text="自動進行（1刻=1.5秒の試作速度）"};toolbar.AddChild(auto);auto.Toggled+=v=>Automatic=v;
        var zoom=new HSlider{MinValue=5,MaxValue=26,Value=10,Step=.5,CustomMinimumSize=new Vector2(170,0)};toolbar.AddChild(zoom);zoom.ValueChanged+=v=>Camera.Size=(float)v;
        Status=new Label();AddChild(Status);
        var content=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};AddChild(content);
        var container=new SubViewportContainer {Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(420,500)};content.AddChild(container);
        View=new SubViewport{Size=new Vector2I(960,600),OwnWorld3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always,MeshLodThreshold=0,Msaa3D=Viewport.Msaa.Disabled};container.AddChild(View);
        World=new Node3D();View.AddChild(World);
        var env=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("263530"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.18f,TonemapMode=Godot.Environment.ToneMapper.Linear};
        env.Sky=new Sky{SkyMaterial=new ProceduralSkyMaterial{SkyTopColor=new Color("627d96"),SkyHorizonColor=new Color("c3c7c5"),GroundBottomColor=new Color("36433c"),GroundHorizonColor=new Color("a0a8a1")}};env.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;
        World.AddChild(new WorldEnvironment{Environment=env});World.AddChild(new DirectionalLight3D{RotationDegrees=new(-45,-35,0),LightEnergy=.25f,ShadowEnabled=true});
        Block(new(0,-.08f,0),new(50,.15f,50),"435345");Block(new(0,-.01f,.7f),new(8,.02f,1.2f),"817760");
        // A stylized fixture castle and village; no claim of production map geometry.
        Block(new(-4,.4f,-2),new(1.8f,.8f,1.3f),"b5b2a0");Block(new(-4,.95f,-2),new(2.1f,.3f,1.6f),"3f4447");Block(new(4,.35f,-2),new(1.4f,.7f,1.2f),"a48a68");
        Camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=10,Position=new(0,8,10),Current=true};World.AddChild(Camera);Camera.LookAt(new Vector3(0,0,0));
        using var presetData=JsonDocument.Parse(FileAccess.GetFileAsString("res://Data/Officer3D/presets-v2.json"));
        foreach(var id in Stations.Keys){var actor=new Officer3DActor{Name=id};World.AddChild(actor);Actors[id]=actor;actor.Position=Stations[id];Preset(actor,presetData.RootElement.GetProperty("character_presets").GetProperty(id).GetString()!);}
        var editScroll=new ScrollContainer{CustomMinimumSize=new Vector2(280,0),SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(editScroll);var edit=new VBoxContainer{CustomMinimumSize=new Vector2(260,0)};editScroll.AddChild(edit);
        edit.AddChild(new Label{Text="表示・外見（戦況に影響しません）",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var who=new OptionButton();foreach(var id in Actors.Keys)who.AddItem(Session.Simulation.RestoreWorld().Definition.Characters.Single(c=>c.Id==id).Name);who.Selected=1;edit.AddChild(who);who.ItemSelected+=i=>{SelectedId=Actors.Keys.ElementAt((int)i);RefreshEditor();};
        var presets=new HBoxContainer();edit.AddChild(presets);foreach(var id in new[]{"commander","veteran"}){var b=new Button{Text=id=="commander"?"基準武将":"交換武将"};presets.AddChild(b);b.Pressed+=()=>{Preset(Actors[SelectedId],id);RefreshEditor();};}
        var face=new OptionButton();face.AddItem("基準の顔型");face.AddItem("角張った顎の顔型");edit.AddChild(face);face.ItemSelected+=i=>{var a=Actors[SelectedId];a.Appearance.Face=i==0?"v013_face":"angular_jaw";a.Apply(a.Appearance);};editorUpdates.Add(()=>face.Select(Actors[SelectedId].Appearance.Face=="v013_face"?0:1));
        foreach(var part in new[]{"hair","beard"}){var b=new CheckButton{Text=part=="hair"?"結い髪":"短い髭"};edit.AddChild(b);b.Toggled+=v=>{var a=Actors[SelectedId];if(part=="hair")a.Appearance.Hair=v?"tied":"none_under_helmet";else a.Appearance.Beard=v?"short":"none";a.Apply(a.Appearance);};editorUpdates.Add(()=>b.SetPressedNoSignal(part=="hair"?Actors[SelectedId].Appearance.Hair=="tied":Actors[SelectedId].Appearance.Beard=="short"));}
        foreach(var category in new[]{"cloth","lacquer","metal"}){edit.AddChild(new Label{Text=category switch{"cloth"=>"布の乗算色","lacquer"=>"漆の乗算色",_=>"金属の乗算色"}});var p=new ColorPickerButton{Color=Colors.White,EditAlpha=false,CustomMinimumSize=new Vector2(0,30)};edit.AddChild(p);p.ColorChanged+=c=>{var a=Actors[SelectedId];a.Appearance.MaterialSrgb[category]=new[]{c.R,c.G,c.B};a.Apply(a.Appearance);};editorUpdates.Add(()=>{var c=Actors[SelectedId].Appearance.MaterialSrgb[category];p.Color=new Color(c[0],c[1],c[2]);});}
        edit.AddChild(new Label{Text="肌（顔・体・耳・手を共通変更）",AutowrapMode=TextServer.AutowrapMode.WordSmart});var skin=new ColorPickerButton{Color=new Color(.87f,.73f,.59f).LinearToSrgb(),EditAlpha=false,CustomMinimumSize=new Vector2(0,30)};edit.AddChild(skin);skin.ColorChanged+=c=>{var a=Actors[SelectedId];var l=c.SrgbToLinear();a.Appearance.SkinLinear=new[]{l.R,l.G,l.B};a.Apply(a.Appearance);};editorUpdates.Add(()=>{var c=Actors[SelectedId].Appearance.SkinLinear;skin.Color=new Color(c[0],c[1],c[2]).LinearToSrgb();});
        var slotLabels=new[]{"面頬","刀","下衣・袴","陣羽織","兜","胴","喉輪","肩","籠手","履物","草摺","佩楯","脛当"};
        foreach(var slot in Officer3DActor.Slots){var b=new CheckButton{Text=slotLabels[Array.IndexOf(Officer3DActor.Slots,slot)],ButtonPressed=Actors[SelectedId].Appearance.Enabled[slot]};edit.AddChild(b);b.Toggled+=v=>{var a=Actors[SelectedId];a.Appearance.Enabled[slot]=v;a.Apply(a.Appearance);};editorUpdates.Add(()=>b.SetPressedNoSignal(Actors[SelectedId].Appearance.Enabled[slot]));}
        var saves=new HBoxContainer();edit.AddChild(saves);var save=new Button{Text="外見を保存"};var load=new Button{Text="外見を復旧"};saves.AddChild(save);saves.AddChild(load);
        save.Pressed+=()=>{using var f=FileAccess.Open("user://officer3d-v2-looks.json",FileAccess.ModeFlags.Write);f.StoreString(SaveLooks());};load.Pressed+=()=>RestoreLooks(FileAccess.GetFileAsString("user://officer3d-v2-looks.json"));
        edit.AddChild(new Label{Text="移動経路は配送の表示用。\n正式な部隊移動・合戦ではありません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        Session.WorldChanged+=_=>{Fraction=0;Synchronize();};Synchronize();RefreshEditor();SetProcess(true);
    }
    public void Block(Vector3 pos,Vector3 size,string color) {World.AddChild(new MeshInstance3D{Mesh=new BoxMesh{Size=size},Position=pos,MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(color),Roughness=1}});}
    public static void Preset(Officer3DActor actor,string id)
    {
        using var data=JsonDocument.Parse(FileAccess.GetFileAsString("res://Data/Officer3D/presets-v2.json"));var p=data.RootElement.GetProperty("presets").EnumerateArray().Single(p=>p.GetProperty("id").GetString()==id);
        var a=new Officer3DLook{Face=p.GetProperty("face").GetString()!,Hair=p.GetProperty("hair").GetString()!,Beard=p.GetProperty("beard").GetString()!};
        foreach(var slot in Officer3DActor.Slots){a.Enabled[slot]=true;a.SlotSrgb[slot]=new float[]{1,1,1};}a.Enabled["helmet"]=p.GetProperty("helmet").GetBoolean();a.Enabled["mask"]=p.GetProperty("mask").GetBoolean();
        foreach(var c in new[]{"cloth","lacquer","metal"})a.MaterialSrgb[c]=p.GetProperty(c).EnumerateArray().Select(v=>v.GetSingle()).ToArray();actor.Apply(a);
    }
    public string SaveLooks()=>JsonSerializer.Serialize(Actors.ToDictionary(p=>p.Key,p=>p.Value.Save()));
    public void RestoreLooks(string json)
    {
        var items=JsonSerializer.Deserialize<Dictionary<string,string>>(json)??throw new ArgumentException();if(!items.Keys.Order().SequenceEqual(Actors.Keys.Order()))throw new ArgumentException("Actor IDs changed");
        foreach(var jsonLook in items.Values){var look=JsonSerializer.Deserialize<Officer3DLook>(jsonLook)??throw new ArgumentException();look.Validate(Officer3DActor.Slots);}
        foreach(var (id,value) in items)Actors[id].Restore(value);RefreshEditor();
    }
    public override void _Process(double delta)
    {
        Fraction=Session.PresentationFraction;
        Synchronize();
    }
    public void Synchronize()
    {
        var sim=Session.Simulation;var active=sim.Deliveries.LastOrDefault(d=>!d.Delivered&&!d.Cancelled&&(!d.Lost||d.LateDueAt!=null));
        foreach(var (id,a) in Actors){a.Position=Stations[id];a.Rotation=Vector3.Zero;}
        var courier=Actors["character-003"];bool traveling=active is not null;float routeDistance=0;
        if(active is not null){long due=active.Lost?active.LateDueAt!.Value:active.DueAt;float t=Math.Clamp((float)(sim.Tick+Fraction-(due-1)),0,1);var from=Stations[active.SenderId]+new Vector3(0,0,1);var to=Stations[active.RecipientId]+new Vector3(0,0,1);courier.Position=from.Lerp(to,t);routeDistance=from.DistanceTo(to)*t;courier.RotationDegrees=new(0,to.X>from.X?90:-90,0);}
        foreach(var (id,a) in Actors){string clip=id=="character-003"&&traveling?"walk":"idle";double seconds=id=="character-003"&&traveling?routeDistance/a.WalkStride*a.Player.GetAnimation(a.Clips["walk"]).Length:sim.Tick*.7+Fraction*.7;a.Freeze(clip,seconds%a.Player.GetAnimation(a.Clips[clip]).Length);if(AutomaticLod)a.UpdateLod(2f/Camera.Size*View.Size.Y);}
        Status.Text=$"世界時刻 {sim.Tick}刻 / {(sim.Paused?"停止中":"手動 / 自動進行")} / 命令 {sim.Orders.Count}件 / 村落 {sim.SourceRice}俵 / 城 {sim.TreasuryRice}俵\n使者：{(active is null?"待機":active.Kind+" 配送中")} / 表示LOD {courier.Lod}（距離に応じて切替）";
    }
}
