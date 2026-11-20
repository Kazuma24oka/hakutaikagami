using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// 甲冑アニメ工房から書き出したドット絵の武将。いつもカメラを向く板として、時刻に合わせたコマを描く。
// 絵は res://Assets/DotOfficers/Sheets（Tools/DotOfficerBaker で書き出す）。無ければ使わない。
public partial class DotOfficerActor : Node3D
{
    public const string Root="res://Assets/DotOfficers/Sheets/";
    sealed record Sheet(string File,int W,int H,int Ox,int Oy,string[] Anims,Dictionary<string,int[]> Ms);
    static Dictionary<string,Sheet>? sheets;static readonly Dictionary<string,Texture2D> textures=new();
    public static bool Available=>Index()!=null;
    static Dictionary<string,Sheet>? Index()
    {
        if(sheets!=null)return sheets;
        if(!Godot.FileAccess.FileExists(Root+"index.json"))return null;
        using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Root+"index.json"));
        sheets=doc.RootElement.GetProperty("sheets").EnumerateObject().ToDictionary(p=>p.Name,p=>new Sheet(
            p.Value.GetProperty("file").GetString()!,p.Value.GetProperty("w").GetInt32(),p.Value.GetProperty("h").GetInt32(),
            p.Value.GetProperty("ox").GetInt32(),p.Value.GetProperty("oy").GetInt32(),
            p.Value.GetProperty("anims").EnumerateArray().Select(a=>a.GetString()!).ToArray(),
            p.Value.GetProperty("ms").EnumerateObject().ToDictionary(m=>m.Name,m=>m.Value.EnumerateArray().Select(x=>x.GetInt32()).ToArray())));
        return sheets;
    }
    public static bool Has(string key)=>Index()?.ContainsKey(key)==true;

    readonly Sprite3D sprite=new(){Billboard=BaseMaterial3D.BillboardModeEnum.FixedY,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,Shaded=false,AlphaCut=SpriteBase3D.AlphaCutMode.Discard,RegionEnabled=true,Centered=true};
    Sheet? sheet;public string Key{get;private set;}="";
    // 3D の武将と同じくらいの背丈（体25ドット）。
    public float PixelSize{get=>sprite.PixelSize;set{sprite.PixelSize=value;foreach(var b in backs)b.PixelSize=value;if(shadow!=null)shadow.Scale=new Vector3(value*26,1,value*16);}}
    // 厚み（試作版、2026-11-20 ユーザーの印「身体の厚みがなく見える」）：絵の右下に、暗い同じ絵を2枚ずらして重ねて厚みに見せ、足元に丸い影を置く。
    readonly List<Sprite3D> backs=new();MeshInstance3D? shadow;static ImageTexture? shadowTex;
    public bool Solid
    {
        set
        {
            if(!value||backs.Count>0)return;
            for(int i=1;i<=2;i++){var b=new Sprite3D{Billboard=sprite.Billboard,TextureFilter=sprite.TextureFilter,Shaded=false,AlphaCut=sprite.AlphaCut,RegionEnabled=true,Centered=true,PixelSize=sprite.PixelSize,NoDepthTest=sprite.NoDepthTest,RenderPriority=sprite.RenderPriority-i,Modulate=new Color(.13f,.1f,.16f),Texture=sprite.Texture,RegionRect=sprite.RegionRect,FlipH=sprite.FlipH};backs.Add(b);AddChild(b);}
            if(shadowTex==null)
            {
                var img=Image.CreateEmpty(32,32,false,Image.Format.Rgba8);
                for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=Mathf.Sqrt((x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f))/15.5f;img.SetPixel(x,y,new Color(0,0,0,r>=1?0:.5f*Mathf.Min(1,(1-r)*2.5f)));}
                shadowTex=ImageTexture.CreateFromImage(img);
            }
            shadow=new MeshInstance3D{Mesh=new PlaneMesh{Size=Vector2.One},MaterialOverride=new StandardMaterial3D{ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,AlbedoTexture=shadowTex,NoDepthTest=sprite.NoDepthTest,RenderPriority=sprite.RenderPriority-3},Position=new Vector3(0,.5f,0),Scale=new Vector3(sprite.PixelSize*26,1,sprite.PixelSize*16)};
            AddChild(shadow);MoveChild(sprite,-1);SyncBacks();
        }
    }
    void SyncBacks(){for(int i=0;i<backs.Count;i++){var b=backs[i];b.Texture=sprite.Texture;b.RegionRect=sprite.RegionRect;b.FlipH=sprite.FlipH;b.Offset=sprite.Offset+new Vector2(.9f*(i+1),-.55f*(i+1));}}
    public override void _Ready(){if(sprite.GetParent()==null)AddChild(sprite);}
    public DotOfficerActor(){sprite.PixelSize=.07f;AddChild(sprite);}

    public bool Apply(string key)
    {
        if(key==Key)return sheet!=null;
        var index=Index();if(index==null||!index.TryGetValue(key,out var s))return false;
        if(!textures.TryGetValue(s.File,out var tex)){tex=ImageTexture.CreateFromImage(Image.LoadFromFile(ProjectSettings.GlobalizePath(Root+s.File)));textures[s.File]=tex;}
        sheet=s;Key=key;sprite.Texture=tex;
        // 足元（体の下端・中央）を原点に置く。Sprite3D の Offset は上が正。
        float footX=s.Ox+12.5f,footY=s.Oy+25f;
        sprite.Offset=new(s.W/2f-footX,footY-s.H/2f);
        Freeze("idle",0);SyncBacks();return true;
    }
    public double ClipLength(string anim)=>sheet==null?1:(Resolve(anim) is {} a?sheet.Ms[a].Sum()/1000d:1);
    string? Resolve(string anim)=>sheet==null?null:sheet.Anims.Contains(anim)?anim:sheet.Anims.Contains("idle")?"idle":sheet.Anims.FirstOrDefault();
    // 時刻からコマを決める（同じ時刻なら同じ絵）。
    public void Freeze(string anim,double time)
    {
        if(sheet==null||Resolve(anim) is not {} a)return;
        int row=Array.IndexOf(sheet.Anims,a);var ms=sheet.Ms[a];double total=ms.Sum();double t=(time*1000)%total;if(t<0)t+=total;
        int col=0;for(double acc=0;col<ms.Length;col++){acc+=ms[col];if(t<acc)break;}
        col=Math.Min(col,ms.Length-1);sprite.RegionRect=new Rect2(col*sheet.W,row*sheet.H,sheet.W,sheet.H);
        foreach(var b in backs)b.RegionRect=sprite.RegionRect;
    }
    // 絵は左向き。画面上で右へ進むときは左右反転する。
    public void Face(Vector3 direction,Camera3D? camera)
    {
        if(camera==null||direction.LengthSquared()<1e-6f)return;
        sprite.FlipH=direction.Dot(camera.GlobalBasis.X)>0;foreach(var b in backs)b.FlipH=sprite.FlipH;
    }
    public bool FacingRight=>sprite.FlipH;
    // 建物に隠れても見えるように、いつも手前に描く（試作版の地図、2026-11-18）。
    public bool OnTop{get=>sprite.NoDepthTest;set{sprite.NoDepthTest=value;foreach(var b in backs)b.NoDepthTest=value;}}
    // 話す場面などの2Dの絵に使う1コマ（試作版の会話の場面、2026-11-18）。
    public static AtlasTexture? Frame(string key,string anim="idle",int col=0)
    {
        var index=Index();if(index==null||!index.TryGetValue(key,out var s))return null;
        if(!textures.TryGetValue(s.File,out var tex)){tex=ImageTexture.CreateFromImage(Image.LoadFromFile(ProjectSettings.GlobalizePath(Root+s.File)));textures[s.File]=tex;}
        int row=Math.Max(0,Array.IndexOf(s.Anims,s.Anims.Contains(anim)?anim:"idle"));
        return new AtlasTexture{Atlas=tex,Region=new Rect2(col*s.W,row*s.H,s.W,s.H)};
    }
    public Rect2 Region=>sprite.RegionRect;
}
