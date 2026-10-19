using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>Isolated right-motion review. Never advances simulation or sends OS input.</summary>
public partial class OfficerRightMotionTrial : Control
{
    private sealed record Op(string Sheet, string? When, string? Unless, bool Erase);
    private readonly Dictionary<string,byte[][]> layers = new();
    private readonly List<Op> operations = new();
    private string[] keys = Array.Empty<string>();
    private string[][] hashes = Array.Empty<string[]>();
    private string binding = "";
    private Sprite2D native = null!, large = null!;
    private Label status = null!;
    private readonly List<CheckButton> checks = new();
    private bool walking=true, playing=true, testing;
    private int frame, flags=1023, idleMs, walkMs;
    private double elapsed, speed=1;
    private readonly HashSet<int> observed = new();
    private const string Evidence = "res://../Art/Officer/RightMotionV8/";
    private static string Hash(byte[] a) => Convert.ToHexString(SHA256.HashData(a)).ToLowerInvariant();
    public override void _Ready()
    {
        try
        {
            GetWindow().Title="百代鏡 — 右向き歩行 試作";
            using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/officer_right_motion_v8.json"));var d=doc.RootElement;
            if(d.GetProperty("canvas")[0].GetInt32()!=64||d.GetProperty("canvas")[1].GetInt32()!=96||d.GetProperty("display_scale").GetInt32()!=1)throw new InvalidOperationException("Native dimensions changed");
            keys=d.GetProperty("keys").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            if(keys.Length!=10||keys.Distinct().Count()!=10)throw new InvalidOperationException("Invalid slots");
            binding=d.GetProperty("binding").GetString()!;idleMs=d.GetProperty("idle_duration_ms").GetInt32();walkMs=d.GetProperty("walk_duration_ms").GetInt32();
            foreach(var p in d.GetProperty("operations").EnumerateArray())operations.Add(new(p.GetProperty("sheet").GetString()!,p.TryGetProperty("when",out var w)?w.GetString():null,p.TryGetProperty("unless",out var u)?u.GetString():null,p.TryGetProperty("erase",out var e)&&e.GetBoolean()));
            foreach(var p in d.GetProperty("sheets").EnumerateObject())
            {
                using var im=GD.Load<Texture2D>(p.Value.GetProperty("path").GetString()!).GetImage();
                if(im.IsCompressed()&&im.Decompress()!=Error.Ok)throw new InvalidOperationException("Decompression failed");im.Convert(Image.Format.Rgba8);
                if(im.GetWidth()!=768||im.GetHeight()!=96)throw new InvalidOperationException("Sheet dimensions");
                var a=im.GetData();var frames=new byte[12][];
                for(int f=0;f<12;f++)
                {
                    var b=new byte[64*96*4];for(int y=0;y<96;y++)Buffer.BlockCopy(a,(y*768+f*64)*4,b,y*64*4,64*4);
                    for(int i=0;i<b.Length;i+=4){if(b[i+3]!=0&&b[i+3]!=255)throw new InvalidOperationException("Nonbinary alpha");if(b[i+3]==0)b[i]=b[i+1]=b[i+2]=0;}
                    if(Hash(b)!=p.Value.GetProperty("frame_hashes")[f].GetString())throw new InvalidOperationException("Imported pixels changed: "+p.Name);frames[f]=b;
                }
                layers[p.Name]=frames;
            }
            hashes=d.GetProperty("hashes").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetString()!).ToArray()).ToArray();
            if(hashes.Length!=1024||hashes.Any(r=>r.Length!=12))throw new InvalidOperationException("Incomplete regression table");
            var bg=new ColorRect{Color=new Color("26343b"),Position=Vector2.Zero,Size=new(1600,1000),MouseFilter=MouseFilterEnum.Ignore};AddChild(bg);
            Text("百代鏡 — 右向き・待機 / 歩行",new(26,20),25);
            Text("64×96・1倍 / 左側が手前 / 表示試験・世界時間は進めません",new(26,60));
            Text("原寸・1倍",new(200,220));Text("静止原本 V7",new(330,220));Text("正面 V6",new(440,220));Text("4倍・接合確認用",new(650,138));
            native=Sprite(new(200,260),1);large=Sprite(new(650,175),4);
            var reference=Sprite(new(330,260),1);reference.Texture=GD.Load<Texture2D>("res://Assets/Trials/OfficerRightViewV7/source-right.png");
            var front=Sprite(new(440,260),1);front.Texture=GD.Load<Texture2D>("res://Assets/Trials/OfficerContextV1/front-64x96.png");
            var labels=d.GetProperty("labels").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            for(int i=0;i<keys.Length;i++){int id=i;var c=new CheckButton{Text=labels[i],ButtonPressed=true,Position=new(24+(i%5)*112,390+(i/5)*46),Size=new(108,40)};c.Toggled+=value=>{flags=value?flags|(1<<id):flags&~(1<<id);ShowFrame();};checks.Add(c);AddChild(c);}
            Button("全部戻す",new(24,505),()=>Select(1023));Button("裸素体",new(160,505),()=>Select(0));Button("待機 / 歩行",new(296,505),()=>{walking=!walking;frame=0;elapsed=0;ShowFrame();});
            Button("停止 / 再開",new(24,560),()=>playing=!playing);Button("1コマ送る",new(160,560),()=>{playing=false;frame=(frame+1)%(walking?8:4);ShowFrame();});Button("通常 / 1/4速",new(296,560),()=>speed=speed==1?.25:1);
            status=Text("",new(24,640));Text("遠側甲冑は同寸の近側素材から補完した試作。左右反転や背面の承認ではありません。",new(24,680),14);
            ShowFrame();testing=OS.GetCmdlineUserArgs().Contains("--officer-right-smoke-test");if(testing){playing=false;_ = SmokeTest();}
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private Label Text(string text,Vector2 at,int size=17){var l=new Label{Text=text,Position=at};l.AddThemeFontSizeOverride("font_size",size);AddChild(l);return l;}
    private void Button(string text,Vector2 at,Action action){var b=new Godot.Button{Text=text,Position=at,Size=new(126,42)};b.Pressed+=action;AddChild(b);}
    private Sprite2D Sprite(Vector2 at,int scale){var s=new Sprite2D{Position=at,Centered=false,Scale=new(scale,scale),TextureFilter=CanvasItem.TextureFilterEnum.Nearest};AddChild(s);return s;}
    private void Select(int value){flags=value;for(int i=0;i<10;i++)checks[i].SetPressedNoSignal((flags&(1<<i))!=0);ShowFrame();}
    private bool On(int selection,string k)=>(selection&(1<<Array.IndexOf(keys,k)))!=0;
    private byte[] Compose(int selection,int index)
    {
        var a=new byte[64*96*4];foreach(var o in operations)
        {
            if(o.When!=null&&!On(selection,o.When)||o.Unless!=null&&On(selection,o.Unless))continue;
            var b=layers[o.Sheet][index];for(int i=0;i<a.Length;i+=4)if(b[i+3]!=0){if(o.Erase)Array.Clear(a,i,4);else Buffer.BlockCopy(b,i,a,i,4);}
        }
        return a;
    }
    private int Index=>(walking?4:0)+frame;
    private void ShowFrame(){using var im=Image.CreateFromData(64,96,false,Image.Format.Rgba8,Compose(flags,Index));native.Texture=ImageTexture.CreateFromImage(im);large.Texture=native.Texture;if(status!=null)status.Text=$"64×96 / 1倍　{(walking?"歩行":"待機")} {frame+1}/{(walking?8:4)}　速度 {speed}　選択 {System.Numerics.BitOperations.PopCount((uint)flags)}/10";}
    public override void _Process(double delta){if(!playing)return;elapsed+=delta*1000*speed;int ms=walking?walkMs:idleMs;while(elapsed>=ms){elapsed-=ms;frame=(frame+1)%(walking?8:4);observed.Add(frame);ShowFrame();}}
    private async Task Drawn(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private void Save(Image im,string file){Require(im.SavePng(ProjectSettings.GlobalizePath(Evidence+file))==Error.Ok,"Screenshot failed");}
    private async Task SmokeTest()
    {
        try
        {
            Require(DisplayServer.GetName()!="headless","Real GPU required");int cpu=0,gpu=0,difference=0;
            for(int s=0;s<1024;s++)for(int i=0;i<12;i++){Require(Hash(Compose(s,i))==hashes[s][i],$"CPU mismatch {s}/{i}");cpu++;}
            var at=native.GetGlobalTransformWithCanvas().Origin;var rect=new Rect2I((Vector2I)at,new(64,96));native.Visible=false;await Drawn();using var bg=GetViewport().GetTexture().GetImage();bg.Convert(Image.Format.Rgba8);var background=bg.GetRegion(rect).GetData();native.Visible=true;
            var cases=new[]{1023,0,3}.Concat(Enumerable.Range(2,8).Select(i=>1023^(1<<i)));
            foreach(int s in cases)for(int i=0;i<12;i++)
            {
                walking=i>=4;frame=walking?i-4:i;Select(s);await Drawn();using var shot=GetViewport().GetTexture().GetImage();shot.Convert(Image.Format.Rgba8);var actual=shot.GetRegion(rect).GetData();var expected=Compose(s,i);
                for(int p=0;p<expected.Length;p+=4)for(int c=0;c<3;c++){int wanted=expected[p+3]==255?expected[p+c]:background[p+c];difference=Math.Max(difference,Math.Abs(actual[p+c]-wanted));}gpu++;
                if(s==1023&&i>=4)Save(shot,$"godot-walk-{i-4}.png");
            }
            Require(difference==0,"GPU pixel mismatch "+difference);walking=true;frame=0;Select(1023);await Drawn();using(var shot=GetViewport().GetTexture().GetImage())Save(shot,"godot-all.png");
            Select(0);await Drawn();using(var shot=GetViewport().GetTexture().GetImage())Save(shot,"godot-bare.png");Select(1023);
            var live=new List<object>();foreach(double rate in new[]{1.0,.25}){speed=rate;elapsed=0;frame=0;observed.Clear();playing=true;await ToSignal(GetTree().CreateTimer(walkMs*8/1000.0/rate+.3),SceneTreeTimer.SignalName.Timeout);playing=false;Require(observed.Count==8,"Incomplete live loop");live.Add(new{speed=rate,frames=observed.Order().ToArray()});}
            var toggles=new List<string>();walking=true;speed=1;playing=true;for(int i=2;i<10;i++){checks[i].ButtonPressed=false;await Drawn();Require(!On(flags,keys[i]),"Toggle did not apply");checks[i].ButtonPressed=true;await Drawn();toggles.Add(keys[i]);}playing=false;
            GetWindow().Size=new(1280,800);await Drawn();Require(native.Scale==Vector2.One,"Native resize changed scale");using(var shot=GetViewport().GetTexture().GetImage())Save(shot,"godot-resized.png");GetWindow().Size=new(1100,720);await Drawn();
            var report=new{status="PASS",engine=Engine.GetVersionInfo()["string"].AsString(),dotnet=System.Environment.Version.ToString(),binding,cpu_cases=cpu,gpu_cases=gpu,max_rgb_difference=difference,native_canvas=new[]{64,96},display_scale=1,live,dynamic_toggles=toggles,native_input=0,scope="Right-only surface-rig trial; no world tick"};
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence+"runtime-validation.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");GD.Print("RIGHT_MOTION_GPU_PASS "+cpu+" CPU / "+gpu+" GPU / "+difference+" difference");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
