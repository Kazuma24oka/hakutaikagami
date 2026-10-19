using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Common;

public sealed record Officer3DCustomRecord(string Id,string Name,Officer3DCustomLook Appearance);
public sealed class Officer3DCustomRoster
{
    public int Schema {get;set;}=1;
    public List<Officer3DCustomRecord> Officers {get;set;}=new();
    public void Validate()
    {
        if(Schema!=1||Officers.Count is <1 or >64||Officers.Select(x=>x.Id).Distinct().Count()!=Officers.Count)throw new ArgumentException("武将一覧の形式が不正です");
        foreach(var o in Officers){if(string.IsNullOrWhiteSpace(o.Id)||string.IsNullOrWhiteSpace(o.Name)||o.Name.Length>40)throw new ArgumentException("武将名・IDが不正です");o.Appearance.Validate(Officer3DCustomActor.Slots);}
    }
}

public partial class Officer3DCreator : Control
{
    public Officer3DCustomRoster Roster {get;private set;}=new();
    public SubViewport View {get;private set;}=null!;
    public Node3D World {get;private set;}=null!;
    public Camera3D Camera {get;private set;}=null!;
    public Officer3DCustomActor Actor=>Models[0];
    public List<Officer3DCustomActor> Models {get;}=new();
    public string Output=>ProjectSettings.GlobalizePath("res://../Art/Officer3D/TrialV3/runtime-local");
    public string StoragePath {get;set;}=ProjectSettings.GlobalizePath("user://officer3d-v3-roster.json");
    public Label Status {get;private set;}=null!;
    public Button SaveButton {get;private set;}=null!;
    public Button LoadButton {get;private set;}=null!;
    public Button NewButton {get;private set;}=null!;
    public OptionButton FaceChoice {get;private set;}=null!;
    public Dictionary<string,OptionButton> EquipmentChoices {get;}=new();
    public OptionButton OfficerChoice {get;private set;}=null!;
    public LineEdit NameEdit {get;private set;}=null!;
    readonly List<Action> refreshers=new();
    int selected;bool refreshing;
    public bool Paused {get;set;}
    public bool AutoLod {get;set;}=true;
    public string Motion {get;private set;}="idle";
    public float Facing {get;private set;}
    public override void _Ready()
    {
        bool test=OS.GetCmdlineUserArgs().Any(a=>a=="--v3-test"||a=="--v3-restore");
        try{Officer3DV3Gate.Require(!test);}catch(Exception e){GD.PushError(e.Message);GetTree().Quit(2);return;}
        if(test)StoragePath=ProjectSettings.GlobalizePath("user://officer3d-v3-test-roster.json");
        DisplayServer.WindowSetSize(new Vector2I(1280,900));Theme=new Theme{DefaultFontSize=18,DefaultFont=new SystemFont{FontNames=new[]{"Yu Gothic UI","Meiryo"}}};
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var root=new VBoxContainer();AddChild(root);root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(new Label{Text="百代鏡 / 武将作成・装備交換"});
        var top=new HFlowContainer();root.AddChild(top);
        OfficerChoice=new OptionButton{CustomMinimumSize=new Vector2(180,0)};top.AddChild(OfficerChoice);OfficerChoice.ItemSelected+=i=>{if(refreshing)return;selected=(int)i;Rebuild(1);RefreshEditor();};
        NewButton=Button(top,"新しい武将",()=>{int next=1;while(Roster.Officers.Any(o=>o.Id=="officer-"+next.ToString("D3")))next++;if(Roster.Officers.Count>=64)throw new InvalidOperationException("登録上限は64人です");Roster.Officers.Add(new("officer-"+next.ToString("D3"),"新しい武将 "+next,Copy(Actor.Appearance)));selected=Roster.Officers.Count-1;Rebuild(1);RefreshEditor();});
        SaveButton=Button(top,"保存",()=>{SnapshotCodec.SaveFile(StoragePath,SaveRoster());Status.Text="武将のパーツ・色・名前を保存しました。";});
        LoadButton=Button(top,"復旧",()=>{LoadRoster(System.IO.File.ReadAllText(StoragePath));Status.Text="保存した武将を復旧しました。";});
        foreach(var (n,count) in new[]{("1体",1),("10体で比較",10),("50体で比較",50)})Button(top,n,()=>{Rebuild(count);RefreshEditor();});
        Status=new Label{Text="同じ体・骨格で、顔・髪・髭・甲冑を交換します。",AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(Status);
        var row=new HBoxContainer{SizeFlagsVertical=SizeFlags.ExpandFill};root.AddChild(row);
        var vc=new SubViewportContainer{Stretch=true,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(420,400)};row.AddChild(vc);
        View=new SubViewport{OwnWorld3D=true,Size=new Vector2I(900,700),RenderTargetUpdateMode=SubViewport.UpdateMode.Always,MeshLodThreshold=0,Msaa3D=Viewport.Msaa.Disabled};vc.AddChild(View);World=new Node3D();View.AddChild(World);
        var env=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("24302f"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.18f,TonemapMode=Godot.Environment.ToneMapper.Linear};env.Sky=new Sky{SkyMaterial=new ProceduralSkyMaterial{SkyTopColor=new Color("627d96"),SkyHorizonColor=new Color("c3c7c5"),GroundBottomColor=new Color("36433c"),GroundHorizonColor=new Color("a0a8a1")}};env.ReflectedLightSource=Godot.Environment.ReflectionSource.Sky;World.AddChild(new WorldEnvironment{Environment=env});World.AddChild(new DirectionalLight3D{RotationDegrees=new(-45,-35,0),LightEnergy=.25f,ShadowEnabled=true});World.AddChild(new MeshInstance3D{Mesh=new PlaneMesh{Size=new Vector2(100,100)},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("435345"),Roughness=1}});
        Camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=3.3f,Position=new(0,2.1f,5),Current=true};World.AddChild(Camera);Camera.LookAt(new Vector3(0,.95f,0));
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(350,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(scroll);var edit=new VBoxContainer{CustomMinimumSize=new Vector2(330,0)};scroll.AddChild(edit);
        NameEdit=new LineEdit{PlaceholderText="武将名（40文字まで）",MaxLength=40};edit.AddChild(NameEdit);NameEdit.TextChanged+=v=>{if(refreshing)return;var o=Roster.Officers[selected];Roster.Officers[selected]=o with{Name=v};};
        var presets=new HFlowContainer();edit.AddChild(presets);foreach(var (id,label) in new[]{("commander","基準武将"),("veteran","壮年の武将"),("red_guard","赤漆の具足")})Button(presets,label,()=>{var a=Officer3DCustomActor.Preset(id);if(id=="red_guard")a.Enabled["helmet"]=true;Change(_=>a);});
        var facing=new HFlowContainer();edit.AddChild(facing);foreach(var (label,angle) in new[]{("正面",0),("右",90),("背面",180),("左",270)})Button(facing,label,()=>SetFacing(angle));
        var turn=new HSlider{MinValue=0,MaxValue=360,Step=1};edit.AddChild(turn);turn.ValueChanged+=v=>SetFacing((float)v);
        var motion=new HFlowContainer();edit.AddChild(motion);foreach(var (id,label) in new[]{("idle","待機"),("walk","歩行"),("attack","攻撃")})Button(motion,label,()=>SetMotion(id));Button(motion,"停止 / 再生",()=>{Paused=!Paused;foreach(var a in Models)a.Player.SpeedScale=Paused?0:1;});
        edit.AddChild(new Label{Text="表示距離"});var zoom=new HSlider{MinValue=2.6,MaxValue=18,Value=3.3,Step=.1};edit.AddChild(zoom);zoom.ValueChanged+=v=>Camera.Size=(float)v;
        FaceChoice=new OptionButton();FaceChoice.AddItem("基準の顔");FaceChoice.AddItem("壮年・顎と鼻筋を調整");edit.AddChild(FaceChoice);FaceChoice.ItemSelected+=i=>{if(!refreshing)Change(a=>{a.Face=i==0?"v013_face":"mature_v1";return a;});};refreshers.Add(()=>FaceChoice.Select(Actor.Appearance.Face=="v013_face"?0:1));
        Toggle(edit,"結い髪（兜を外すと表示）",()=>Actor.Appearance.Hair=="tied_v1",(a,v)=>a.Hair=v?"tied_v1":"none_under_helmet");Toggle(edit,"短髭（面頬を外すと表示）",()=>Actor.Appearance.Beard=="short_v1",(a,v)=>a.Beard=v?"short_v1":"none");
        ColorField(edit,"肌：顔・体・耳・手を共通変更",()=>new Color(Actor.Appearance.SkinLinear[0],Actor.Appearance.SkinLinear[1],Actor.Appearance.SkinLinear[2]).LinearToSrgb(),(a,c)=>{var l=c.SrgbToLinear();a.SkinLinear=new[]{l.R,l.G,l.B};});
        ColorField(edit,"髪・髭の乗算色",()=>ToColor(Actor.Appearance.HairSrgb),(a,c)=>a.HairSrgb=new[]{c.R,c.G,c.B});
        foreach(var (id,label) in new[]{("cloth","布の乗算色"),("lacquer","漆の乗算色"),("metal","金属の乗算色")})ColorField(edit,label,()=>ToColor(Actor.Appearance.MaterialSrgb[id]),(a,c)=>a.MaterialSrgb[id]=new[]{c.R,c.G,c.B});
        var names=new[]{"面頬","刀","下衣・袴","陣羽織","兜","胴","喉輪","肩・袖","籠手","履物","草摺","佩楯","脛当"};
        for(int i=0;i<Officer3DCustomActor.Slots.Length;i++){string slot=Officer3DCustomActor.Slots[i];edit.AddChild(new Label{Text=names[i]});var pick=new OptionButton();pick.AddItem("外す");pick.AddItem(Officer3DCustomActor.RedSlots.Contains(slot)?"銀南蛮の部品":"共通の部品");if(Officer3DCustomActor.RedSlots.Contains(slot))pick.AddItem("赤漆の部品");edit.AddChild(pick);EquipmentChoices[slot]=pick;pick.ItemSelected+=v=>{if(!refreshing)Change(a=>{a.Enabled[slot]=v!=0;if(v!=0)a.Equipment[slot]=v==1?"silver":"red_lamellar";return a;});};refreshers.Add(()=>pick.Select(!Actor.Appearance.Enabled[slot]?0:Actor.Appearance.Equipment[slot]=="silver"?1:2));}
        edit.AddChild(new Label{Text="赤具足は資料に基づくゲーム用の形状です。特定の実物の完全復元ではありません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        Roster.Officers.Add(new("officer-001","基準の武将",Officer3DCustomActor.Preset("commander")));Roster.Officers.Add(new("officer-002","赤具足の武将",Officer3DCustomActor.Preset("red_guard")));selected=1;Rebuild(1);RefreshEditor();
        if(OS.GetCmdlineUserArgs().Contains("--v3-test"))CallDeferred(nameof(RunValidation));else if(OS.GetCmdlineUserArgs().Contains("--v3-restore"))CallDeferred(nameof(RunRestoreValidation));
    }
    Button Button(Container parent,string label,Action action){var b=new Button{Text=label};parent.AddChild(b);b.Pressed+=()=>{try{action();}catch(Exception e){Status.Text="変更できませんでした："+e.Message;GD.Print("CREATOR_REJECTED "+e.GetType().Name);}};return b;}
    void Toggle(Container box,string label,Func<bool> get,Action<Officer3DCustomLook,bool> set){var c=new CheckButton{Text=label};box.AddChild(c);c.Toggled+=v=>{if(!refreshing)Change(a=>{set(a,v);return a;});};refreshers.Add(()=>c.SetPressedNoSignal(get()));}
    static Color ToColor(float[] c)=>new(c[0],c[1],c[2]);
    void ColorField(Container box,string label,Func<Color> get,Action<Officer3DCustomLook,Color> set)
    {
        box.AddChild(new Label{Text=label,AutowrapMode=TextServer.AutowrapMode.WordSmart});var row=new HBoxContainer();box.AddChild(row);var picker=new ColorPickerButton{EditAlpha=false,CustomMinimumSize=new Vector2(170,30)};row.AddChild(picker);var code=new LineEdit{CustomMinimumSize=new Vector2(140,0),PlaceholderText="#RRGGBB",MaxLength=7};row.AddChild(code);
        picker.ColorChanged+=c=>{if(!refreshing)Change(a=>{set(a,c);return a;});};code.TextSubmitted+=v=>{if(v.Length!=7||v[0]!='#'||!Color.HtmlIsValid(v)){Status.Text="色は #RRGGBB で入力してください。";return;}Change(a=>{set(a,new Color(v));return a;});};refreshers.Add(()=>{picker.Color=get();code.Text="#"+get().ToHtml(false).ToUpperInvariant();});
    }
    static Officer3DCustomLook Copy(Officer3DCustomLook a)=>JsonSerializer.Deserialize<Officer3DCustomLook>(JsonSerializer.Serialize(a))!;
    public void Change(Func<Officer3DCustomLook,Officer3DCustomLook> transform){var a=transform(Copy(Actor.Appearance));a.Validate(Officer3DCustomActor.Slots);Actor.Apply(a);var o=Roster.Officers[selected];Roster.Officers[selected]=o with{Appearance=Copy(a)};RefreshEditor();Status.Text="変更を反映しました。保存すると次回も使えます。";}
    public void RefreshEditor(){refreshing=true;OfficerChoice.Clear();foreach(var o in Roster.Officers)OfficerChoice.AddItem(o.Name);OfficerChoice.Select(selected);NameEdit.Text=Roster.Officers[selected].Name;foreach(var f in refreshers)f();refreshing=false;}
    public void SetFacing(float angle){Facing=angle;foreach(var a in Models)a.RotationDegrees=new(0,angle,0);}
    public void SetMotion(string id){Motion=id;foreach(var a in Models)a.Play(id);}
    public void Rebuild(int count)
    {
        foreach(var old in Models)old.Free();Models.Clear();
        for(int i=0;i<count;i++){var a=new Officer3DCustomActor{Name="Preview"+i};World.AddChild(a);var look=Copy(Roster.Officers[i==0?selected:i%Roster.Officers.Count].Appearance);if(i>0){float c=.5f+(i%7)/12f;look.MaterialSrgb["cloth"]=new[]{c,1f-c*.3f,.5f+c*.4f};if(i%3==0)foreach(var slot in Officer3DCustomActor.RedSlots)look.Equipment[slot]=i%2==0?"silver":"red_lamellar";}
            a.Apply(look);a.Play(Motion);a.Player.SpeedScale=Paused?0:1;a.RotationDegrees=new(0,Facing,0);a.Position=count==1?Vector3.Zero:new Vector3((i%10-4.5f)*1.15f,0,(i/10-2)*1.5f);Models.Add(a);}
        Camera.Size=count==1?3.3f:12;Camera.Position=count==1?new(0,2.1f,5):new(0,9,11);Camera.LookAt(count==1?new Vector3(0,.95f,0):new Vector3(0,.7f,0));
    }
    public override void _Process(double delta){if(AutoLod&&View!=null&&Camera!=null)foreach(var a in Models)a.UpdateLod(1.8f/Camera.Size*View.Size.Y);}
    public string SaveRoster(){Roster.Validate();return JsonSerializer.Serialize(Roster,new JsonSerializerOptions{WriteIndented=true});}
    public void LoadRoster(string json){var loaded=JsonSerializer.Deserialize<Officer3DCustomRoster>(json)??throw new ArgumentException("空の保存内容");loaded.Validate();Roster=loaded;selected=0;Rebuild(1);RefreshEditor();}
    public async void RunValidation(){await Officer3DV3Validation.Run(this);}
    public async void RunRestoreValidation(){await Officer3DV3Validation.Restore(this);}
}
