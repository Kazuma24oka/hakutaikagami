using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class OfficerPartsTrial : Control
{
    private const string Assets = "res://Assets/Trials/OfficerPartsV1/";
    private JsonElement contract;
    private readonly Dictionary<string, Texture2D> textures = new();
    private readonly Dictionary<string, Sprite2D[]> nodes = new();
    private readonly Dictionary<string, CheckBox> checks = new();
    private HashSet<string> enabled = new();
    private string hair = "hair";
    private bool blue;
    private OptionButton presets = null!;
    private CheckBox shortHair = null!, bluePaint = null!;
    private Sprite2D ownership = null!;
    private Control markers = null!;
    private Label status = null!;
    private readonly bool retiredTrial = true;
    public override void _Ready()
    {
        if (retiredTrial)
        {
            AddChild(new Label { Text = "OfficerPartsV1: rejected / quarantined. Correct base body is not authored yet.", Position = new Vector2(20, 20) });
            GD.Print("OFFICER_PARTS_BLOCKED rejected trial cannot be replayed as a current candidate");
            if (OS.GetCmdlineUserArgs().Contains("--parts-smoke-test")) GetTree().Quit(2);
            return;
        }
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Assets + "parts-contract.json"));
        contract = doc.RootElement.Clone();
        Theme = new Theme { DefaultFontSize = 18, DefaultFont = new SystemFont { FontNames = new[] { "Meiryo", "Yu Gothic UI", "sans-serif" } } };
        Label("百代鏡｜64×96 正面パーツの着脱・再合成（候補）", new(20,10));
        Label("見えていた画素は保持。下地・部位境界・短髪は未承認。全方向・動作は未制作。", new(20,45),16);
        presets = new OptionButton { Position = new(20,90), Size = new(420,45) };
        foreach (var item in contract.GetProperty("cases").EnumerateArray()) presets.AddItem(item.GetProperty("label").GetString());
        presets.ItemSelected += index => SetCase((int)index); AddChild(presets);
        shortHair = Check("短髪候補", new(460,92), value => { hair = value ? "hair_short" : "hair"; Apply(); });
        bluePaint = Check("漆だけ青へ", new(620,92), value => { blue = value; Apply(); });
        Check("部位色分け", new(20,150), value => ownership.Visible = value);
        Check("関節（仮）", new(220,150), value => markers.Visible = value);
        Label("元の確認画像・4倍", new(30,190)); Label("パーツ合成・4倍",new(340,190));
        foreach (float x in new[] {30f,340f}) AddChild(new ColorRect { Position = new(x,220), Size = new(256,384), Color = new("b8b9b2"), MouseFilter = MouseFilterEnum.Ignore });
        AddChild(Sprite("reference.png", new(30,220),4));
        AddChild(Sprite("underlayer.png",new(340,220),4));
        AddChild(Sprite("underlayer.png",new(650,508),1));
        foreach (var definition in contract.GetProperty("layers").EnumerateArray())
        {
            string id = definition.GetProperty("id").GetString()!;
            var pair = new[] { Sprite("layers/"+id+".png",new(340,220),4), Sprite("layers/"+id+".png",new(650,508),1) };
            foreach(var sprite in pair) AddChild(sprite); nodes.Add(id,pair);
        }
        ownership = Sprite("ownership-view.png",new(340,220),4); ownership.Modulate = new Color(1,1,1,.65f); ownership.Visible=false; AddChild(ownership);
        markers = new Control { Visible=false, MouseFilter=MouseFilterEnum.Ignore }; AddChild(markers);
        using var joints = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Assets+"landmarks.json"));
        foreach(var point in joints.RootElement.EnumerateObject())
        {
            if(point.Value.ValueKind != JsonValueKind.Array) continue;
            markers.AddChild(new ColorRect { Position = new(340+point.Value[0].GetInt32()*4,220+point.Value[1].GetInt32()*4), Size = new(3,3), Color=new("63ffe2"), MouseFilter=MouseFilterEnum.Ignore });
        }
        var labels = new Dictionary<string,string> { ["head"]="顔の肌",["hair"]="髪",["face"]="目眉・鼻",["beard"]="髭",["body"]="下衣・襟",["armor"]="甲冑",["coat"]="陣羽織",["weapon"]="刀・鞘",["hands"]="手・手甲",["feet"]="履物" };
        int row=0;
        foreach(var group in contract.GetProperty("groups").EnumerateArray())
        {
            string id=group.GetString()!;
            checks[id]=Check(labels.GetValueOrDefault(id,id),new(780,180+row++*35),value=> { if(value) enabled.Add(id); else enabled.Remove(id); Apply(); });
        }
        Label("通常1倍",new(637,470),16);
        status=Label("",new(20,626),17);
        Label("着脱の下地は推定。首・手首・顔と髪の境界を確認し、静止の合格を動作へ流用しません。",new(20,674),16);
        SetCase(0);
        if(OS.GetCmdlineUserArgs().Contains("--parts-smoke-test")) _=Smoke();
    }
    private Label Label(string text,Vector2 at,int size=18)
    { var l=new Label { Text=text,Position=at }; l.AddThemeFontSizeOverride("font_size",size); AddChild(l); return l; }
    private CheckBox Check(string text,Vector2 at,Action<bool> action)
    { var c=new CheckBox { Text=text,Position=at }; c.Toggled += value => action(value); AddChild(c); return c; }
    private Texture2D Texture(string file)
    { if(!textures.TryGetValue(file,out var texture)) textures[file]=texture=GD.Load<Texture2D>(Assets+file); return texture; }
    private Sprite2D Sprite(string file,Vector2 at,int zoom) => new() { Texture=Texture(file),Position=at,Scale=Vector2.One*zoom,Centered=false,TextureFilter=TextureFilterEnum.Nearest };
    private void SetCase(int index)
    {
        var item=contract.GetProperty("cases")[index];
        enabled=item.GetProperty("groups").EnumerateArray().Select(g=>g.GetString()!).ToHashSet();
        hair=item.TryGetProperty("hair",out var h)?h.GetString()!:"hair";
        blue=item.TryGetProperty("blue",out var b)&&b.GetBoolean();
        foreach(var pair in checks) pair.Value.SetPressedNoSignal(enabled.Contains(pair.Key));
        shortHair.SetPressedNoSignal(hair=="hair_short"); bluePaint.SetPressedNoSignal(blue); presets.Select(index); Apply();
    }
    private void Apply()
    {
        foreach(var item in contract.GetProperty("layers").EnumerateArray())
        {
            string id=item.GetProperty("id").GetString()!,group=item.GetProperty("group").GetString()!;
            string key=id=="hair"?hair:id;
            string file="layers/"+key+(blue&&group=="armor"?"-blue":"")+".png";
            foreach(var node in nodes[id]) { node.Visible=enabled.Contains(group); node.Texture=Texture(file); }
        }
        if(status!=null) status.Text=$"同じ64×96座標・通常1倍　／　表示グループ {enabled.Count}　／　全装備の再合成はRGBA差分0";
    }
    private async Task Smoke()
    {
        try
        {
            int captures=0;
            var cases=contract.GetProperty("cases");
            for(int i=0;i<cases.GetArrayLength();i++)
            {
                presets.EmitSignal(OptionButton.SignalName.ItemSelected,(long)i);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(nodes.Values.SelectMany(v=>v).Any(s=>s.Texture.GetSize()!=new Vector2(64,96)||s.TextureFilter!=TextureFilterEnum.Nearest)) throw new InvalidOperationException("Part size/filter mismatch.");
                if(DisplayServer.GetName()!="headless")
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    string id=cases[i].GetProperty("id").GetString()!;
                    if(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://../Docs/Proposals/Evidence/officer-parts-"+id+".png"))!=Error.Ok) throw new InvalidOperationException("Capture failed.");
                    captures++;
                }
            }
            GD.Print($"OFFICER_PARTS_SCENE_PASS states={cases.GetArrayLength()} captures={captures} fixed_canvas=64x96 native_input=0"); GetTree().Quit(0);
        }
        catch(Exception e){ GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
