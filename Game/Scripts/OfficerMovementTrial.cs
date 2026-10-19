using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// Display-only movement trial. Existing four-direction textures stay immutable.
public partial class OfficerMovementTrial : Node2D
{
    private sealed class Frames
    {
        public string[] Keys; public int Idle, Walk; public string[][] Expected;
        private readonly OfficerMotionFrames? front;
        private readonly Dictionary<string,byte[][]> layers=new();
        private readonly List<(string Sheet,string? When,string? Unless,bool Erase)> ops=new();
        public Frames(string path,bool isFront)
        {
            if(isFront)front=new OfficerMotionFrames(path);
            using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));var d=doc.RootElement;
            Keys=d.GetProperty("keys").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            Idle=d.GetProperty("idle_duration_ms").GetInt32();Walk=d.GetProperty("walk_duration_ms").GetInt32();
            Expected=d.GetProperty("hashes").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetString()!).ToArray()).ToArray();
            if(isFront)return;
            foreach(var op in d.GetProperty("operations").EnumerateArray())ops.Add((op.GetProperty("sheet").GetString()!,op.TryGetProperty("when",out var w)?w.GetString():null,op.TryGetProperty("unless",out var u)?u.GetString():null,op.TryGetProperty("erase",out var e)&&e.GetBoolean()));
            foreach(var p in d.GetProperty("sheets").EnumerateObject())
            {
                using var im=GD.Load<Texture2D>(p.Value.GetProperty("path").GetString()!).GetImage();if(im.IsCompressed())im.Decompress();im.Convert(Image.Format.Rgba8);
                if(im.GetSize()!=new Vector2I(768,96))throw new Exception("Sheet dimensions");var raw=im.GetData();var fs=new byte[12][];
                for(int f=0;f<12;f++){var a=new byte[64*96*4];for(int y=0;y<96;y++)Buffer.BlockCopy(raw,(y*768+f*64)*4,a,y*64*4,256);for(int j=0;j<a.Length;j+=4){if(a[j+3]==0)a[j]=a[j+1]=a[j+2]=0;if(a[j+3]!=0&&a[j+3]!=255)throw new Exception("Alpha");}if(OfficerMotionFrames.Hash(a)!=p.Value.GetProperty("frame_hashes")[f].GetString())throw new Exception("Imported pixels");fs[f]=a;}layers[p.Name]=fs;
            }
        }
        public byte[] Compose(int flags,int index)
        {
            if(front!=null)return front.Compose(flags,index);var a=new byte[64*96*4];bool On(string k)=>(flags&(1<<Array.IndexOf(Keys,k)))!=0;
            foreach(var o in ops){if(o.When!=null&&!On(o.When)||o.Unless!=null&&On(o.Unless))continue;var b=layers[o.Sheet][index];for(int j=0;j<a.Length;j+=4)if(b[j+3]!=0){if(o.Erase)Array.Clear(a,j,4);else Buffer.BlockCopy(b,j,a,j,4);}}return a;
        }
    }
    private readonly Dictionary<string,Frames> views=new();
    private readonly string[] directions={"front","right","back","left"};
    private readonly string[] labels={"正面","右","背面","左"};
    private readonly List<CheckButton> checks=new();
    private Sprite2D actor=null!,large=null!;private Label state=null!;
    private Vector2 foot=new(420,430);private string facing="front";private int flags=1023,frame;
    private bool walking,tour=true,testing;private double phase,tourTime;private Vector2 input;
    private const float Speed=25;private const string Evidence="res://../Art/Officer/MovementV13/";
    public override void _Ready()
    {
        try{
            GetWindow().Title="百代鏡 — 4方向移動 試作";GetWindow().Size=new(1280,800);
            views["front"]=new("res://Data/officer_motion_trial_v6.json",true);views["right"]=new("res://Data/officer_right_motion_v8.json",false);views["back"]=new("res://Data/officer_back_motion_v10.json",false);views["left"]=new("res://Data/officer_left_motion_v12.json",false);
            foreach(var v in views.Values)if(!v.Keys.SequenceEqual(views["front"].Keys))throw new Exception("Shared selection IDs differ");
            actor=Sprite(1);large=Sprite(4);large.Position=new(964,400);
            Text("百代鏡 — 4方向の移動確認",new(28,18),25);Text("64×96・1倍　矢印 / WASDで移動　離すと待機　斜め入力は縦優先",new(28,60));
            Button("自動巡回 / 停止",new(28,94),()=>{tour=!tour;input=Vector2.Zero;});Button("開始位置へ",new(214,94),()=>{foot=new(420,430);SetMovement(Vector2.Zero);Refresh();});
            Text("装備は4方向共通",new(910,84));var names=views["front"].Keys;var namesJa=new[]{"顔・髭","髪","胴","肩","腕","腰","脚","履物","陣羽織","刀"};
            for(int i=0;i<10;i++){int n=i;var c=new CheckButton{Text=namesJa[i],ButtonPressed=true,Position=new(910+i%2*150,126+i/2*42)};c.Toggled+=on=>{flags=on?flags|1<<n:flags&~(1<<n);Refresh();};checks.Add(c);AddChild(c);}
            Button("全部戻す",new(910,340),()=>Select(1023));Button("裸素体",new(1080,340),()=>Select(0));Text("4倍・接合の確認",new(962,374));
            state=Text("",new(28,710));Text("草地・街道・河岸での表示試験。体型と隠れ面は暫定。世界時間・合戦ロジックは進めません。",new(28,752),15);
            testing=OS.GetCmdlineUserArgs().Contains("--officer-movement-smoke-test");Refresh();if(testing){tour=false;_ = Smoke();}
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private Sprite2D Sprite(int scale){var s=new Sprite2D{Centered=false,Scale=new(scale,scale),TextureFilter=TextureFilterEnum.Nearest};AddChild(s);return s;}
    private Label Text(string s,Vector2 p,int size=17){var l=new Label{Text=s,Position=p};l.AddThemeFontSizeOverride("font_size",size);AddChild(l);return l;}
    private void Button(string s,Vector2 p,Action a){var b=new Godot.Button{Text=s,Position=p,Size=new(160,32)};b.Pressed+=a;AddChild(b);}
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,1600,1100),new Color("202d32"));DrawRect(new Rect2(28,140,850,550),new Color("526449"));
        for(int y=140;y<690;y+=32)for(int x=28;x<878;x+=32)if(((x+y)/32)%2==0)DrawRect(new Rect2(x,y,Math.Min(32,878-x),Math.Min(32,690-y)),new Color("56694c"));
        DrawRect(new Rect2(28,360,850,98),new Color("a59670"));DrawRect(new Rect2(694,140,90,550),new Color("4a7078"));DrawRect(new Rect2(682,140,12,550),new Color("8b9274"));DrawRect(new Rect2(678,373,114,70),new Color("958264"));
        for(int x=682;x<792;x+=9)DrawLine(new(x,373),new(x,443),new Color("675a45"));
        DrawRect(new Rect2(100,188,150,92),new Color("706c58"));for(int x=100;x<250;x+=26)DrawRect(new Rect2(x,176,18,18),new Color("706c58"));
        for(int x=115;x<240;x+=35){DrawLine(new(x,245),new(x,323),new Color("453e31"),2);DrawRect(new Rect2(x,245,18,31),new Color("803f34"));}
        DrawRect(new Rect2(28,140,850,550),new Color("a9b396"),false);DrawCircle(foot,3,new Color("dac690"));
    }
    private int Index=>(walking?4:0)+frame;
    private void Refresh(){using var im=Image.CreateFromData(64,96,false,Image.Format.Rgba8,views[facing].Compose(flags,Index));actor.Texture=ImageTexture.CreateFromImage(im);large.Texture=actor.Texture;actor.Position=new Vector2(Mathf.Round(foot.X),Mathf.Round(foot.Y))-new Vector2(32,92);if(state!=null)state.Text=$"{labels[Array.IndexOf(directions,facing)]}　{(walking?"歩行":"待機")} {frame+1}　足元 ({foot.X:F0}, {foot.Y:F0})　選択 {System.Numerics.BitOperations.PopCount((uint)flags)}/10";QueueRedraw();}
    private void Select(int f){flags=f;for(int i=0;i<10;i++)checks[i].SetPressedNoSignal((f&1<<i)!=0);Refresh();}
    private void SetMovement(Vector2 v)
    {
        if(v.Y!=0)v=new(0,Math.Sign(v.Y));else if(v.X!=0)v=new(Math.Sign(v.X),0);
        bool moving=v!=Vector2.Zero;string next=moving?(v.Y>0?"front":v.Y<0?"back":v.X>0?"right":"left"):facing;
        if(walking!=moving){frame=0;phase=0;}facing=next;walking=moving;input=v;
    }
    private void Advance(double dt)
    {
        var before=foot;foot+=input*(float)(Speed*dt);foot=new(Mathf.Clamp(foot.X,64,846),Mathf.Clamp(foot.Y,244,674));
        if(walking&&foot==before){SetMovement(Vector2.Zero);}
        phase+=dt*1000;int ms=walking?views[facing].Walk:views[facing].Idle;while(phase>=ms){phase-=ms;frame=(frame+1)%(walking?8:4);}Refresh();
    }
    public override void _Process(double delta)
    {
        if(testing)return;var v=new Vector2((Input.IsPhysicalKeyPressed(Key.Right)||Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.Left)||Input.IsPhysicalKeyPressed(Key.A)?1:0),(Input.IsPhysicalKeyPressed(Key.Down)||Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.Up)||Input.IsPhysicalKeyPressed(Key.W)?1:0));
        if(v!=Vector2.Zero)tour=false;
        if(tour){tourTime+=delta;int leg=(int)(tourTime/2)%5;v=leg switch{0=>Vector2.Right,1=>Vector2.Down,2=>Vector2.Left,3=>Vector2.Up,_=>Vector2.Zero};}
        SetMovement(v);Advance(delta);
    }
    private static void Need(bool b,string s){if(!b)throw new Exception(s);}
    private async Task Drawn(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    private void Save(Image im,string name){Need(im.SavePng(ProjectSettings.GlobalizePath(Evidence+name))==Error.Ok,"Save failed");}
    private async Task Smoke()
    {
        try{
            Need(DisplayServer.GetName()!="headless","GPU required");int cpu=0,gpu=0;var transition=new List<object>();
            foreach(var v in views.Values)for(int f=0;f<1024;f++)for(int i=0;i<12;i++){Need(OfficerMotionFrames.Hash(v.Compose(f,i))==v.Expected[f][i],"CPU selection/frame");cpu++;}
            foreach(var d in directions){var old=foot;SetMovement(d switch{"front"=>Vector2.Down,"back"=>Vector2.Up,"left"=>Vector2.Left,_=>Vector2.Right});Need(foot==old,"Facing changed anchor");Advance(.5);Need(foot.DistanceTo(old)==12.5f,"Travel distance");Need(facing==d&&walking,"Facing / walk");int selection=flags;SetMovement(Vector2.Zero);Need(!walking&&facing==d&&frame==0,"Stop idle");Need(flags==selection,"Selection lost");transition.Add(new{direction=d,anchor=foot.ToString(),stopped_idle=true});}
            SetMovement(new(1,1));Need(facing=="front"&&input==Vector2.Down,"Diagonal policy");SetMovement(Vector2.Zero);
            foot=new(420,430);
            foreach(var d in directions){facing=d;foreach(int f in new[]{1023,0,3}.Concat(Enumerable.Range(2,8).Select(i=>1023^(1<<i))))for(int i=0;i<12;i++){
                walking=i>=4;frame=i% (walking?8:4);Select(f); // Correct absolute clip index below
                frame=walking?i-4:i;Refresh();actor.Visible=false;await Drawn();using var bg=GetViewport().GetTexture().GetImage();bg.Convert(Image.Format.Rgba8);actor.Visible=true;await Drawn();using var shot=GetViewport().GetTexture().GetImage();shot.Convert(Image.Format.Rgba8);
                var pos=(Vector2I)actor.Position;var rect=new Rect2I(pos,new(64,96));var a=shot.GetRegion(rect).GetData();var b=bg.GetRegion(rect).GetData();var expected=views[d].Compose(f,i);for(int j=0;j<a.Length;j+=4)for(int c=0;c<3;c++)Need(a[j+c]==(expected[j+3]==255?expected[j+c]:b[j+c]),"GPU mismatch");gpu++;
                if(f==1023&&i==4)Save(shot,$"godot-{d}.png");
            }}
            // Same UI signal and input handler as the interactive map; no OS events.
            Select(1023);checks[4].ButtonPressed=false;Need(flags==(1023^(1<<4)),"UI armor toggle");
            foot=new(420,430);tour=false;testing=false;
            foreach(var key in new[]{Key.Right,Key.Down,Key.Left,Key.Up}){
                var before=foot;Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key,Pressed=true});Input.FlushBufferedEvents();_Process(.25);Need(Math.Abs(foot.DistanceTo(before)-6.25)<.001,"Godot key handler movement: "+key+" delta="+foot.DistanceTo(before));Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=key,Keycode=key,Pressed=false});Input.FlushBufferedEvents();_Process(0);Need(!walking&&frame==0,"Godot key release idle");
            }testing=true;
            foot=new(420,430);SetMovement(Vector2.Right);Advance(.5);Advance(.5);var one=foot;foot=new(420,430);SetMovement(Vector2.Right);for(int i=0;i<10;i++)Advance(.1);Need(foot.DistanceTo(one)<.001,"Delta partition drift");
            foot=new(846,674);SetMovement(Vector2.Right);Advance(.1);Need(foot==new Vector2(846,674)&&!walking,"Map boundary stop");
            Select(1023);foreach(var site in new[]{("grass",new Vector2(420,550)),("road",new Vector2(420,430)),("bank",new Vector2(674,550))}){foot=site.Item2;SetMovement(Vector2.Zero);Refresh();await Drawn();using var im=GetViewport().GetTexture().GetImage();Save(im,"context-"+site.Item1+".png");}
            // Record actual moving positions over a four-direction route (internal input, no OS events).
            Select(1023);foot=new(300,380);int n=0;var route=new List<object>();
            foreach(var v in new[]{Vector2.Right,Vector2.Down,Vector2.Left,Vector2.Up,Vector2.Zero}){SetMovement(v);for(int k=0;k<(v==Vector2.Zero?4:10);k++){Advance(.18);await Drawn();using var im=GetViewport().GetTexture().GetImage();string file=$"route-{n++:00}.png";Save(im,file);route.Add(new{file,direction=facing,index=Index,flags,x=(int)actor.Position.X,y=(int)actor.Position.Y,foot_x=foot.X,foot_y=foot.Y});} }
            foreach(int f in new[]{0,3,1023^(1<<4)}){Select(f);foreach(var d in directions){var old=foot;SetMovement(d switch{"front"=>Vector2.Down,"back"=>Vector2.Up,"left"=>Vector2.Left,_=>Vector2.Right});Need(flags==f&&foot==old,"Directional selection/anchor changed");Advance(.12);} }
            GetWindow().Size=new(1400,900);await Drawn();Need(actor.Scale==Vector2.One&&actor.GetGlobalTransformWithCanvas().Scale==Vector2.One,"Resize native scale");using(var im=GetViewport().GetTexture().GetImage())Save(im,"godot-resized.png");
            using var active=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Evidence+"active-plan.json"));
            var report=new{status="PASS",binding=active.RootElement.GetProperty("binding").GetString(),cpu_cases=cpu,gpu_cases=gpu,rgb_difference=0,transitions=transition,route_frames=n,route,selection_preserved=true,stop_idle=true,direction_anchor_jump=0,godot_key_press_release=4,ui_toggle=true,delta_partition=true,boundary_stop=true,contexts=new[]{"grass","road","bank"},diagonal_policy="vertical priority",native_scale=1,native_input=0,world_ticks=0};Godot.FileAccess.Open(Evidence+"runtime-validation.json",Godot.FileAccess.ModeFlags.Write).StoreString(JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));GD.Print($"MOVEMENT_GPU_PASS {cpu} CPU / {gpu} GPU");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
