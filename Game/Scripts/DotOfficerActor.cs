using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// 甲冑アニメ工房から書き出したドット絵の武将。いつもカメラを向く板として、時刻に合わせたコマを描く。
// 絵はローカルの res://Assets/DotOfficers/Local（規約確認まで Git に入れない）。無ければ使わない。
public partial class DotOfficerActor : Node3D
{
    public const string Root="res://Assets/DotOfficers/Local/";
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
    public float PixelSize{get=>sprite.PixelSize;set=>sprite.PixelSize=value;}
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
        Freeze("idle",0);return true;
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
    }
    // 絵は左向き。画面上で右へ進むときは左右反転する。
    public void Face(Vector3 direction,Camera3D? camera)
    {
        if(camera==null||direction.LengthSquared()<1e-6f)return;
        sprite.FlipH=direction.Dot(camera.GlobalBasis.X)>0;
    }
    public bool FacingRight=>sprite.FlipH;
    public Rect2 Region=>sprite.RegionRect;
}
