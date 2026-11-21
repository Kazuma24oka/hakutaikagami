using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// 甲冑アニメ工房から書き出したドット絵の武将。いつもカメラを向く板として、時刻に合わせたコマを描く（試作版は Solid で、ドットを積んだ厚みのある形にする）。
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
    public float PixelSize{get=>sprite.PixelSize;set{sprite.PixelSize=value;if(solid!=null){solid.Scale=new Vector3(sprite.FlipH?-value:value,value,value);shadow!.Scale=new Vector3(value*24,1,value*15);}}}
    // 厚みのある形（試作版、2026-11-21 ユーザー「立体感がたりない」「作り変える」）：絵のドット一つ一つを奥行きのある箱にして、厚い人形として組み立てる。
    // 板の絵（Sprite3D）は使わず、コマごとに形を作って持っておく（同じ絵・同じコマは使い回す）。いつもカメラの方を向く（縦の軸で回る）ので、
    // 斜め上から見ると頭や肩の上の面が見える。上の面は明るく、横の面は暗くする。足元に丸い影を置く。
    // 建物に隠れても見えるように奥行きの比べをしないので、面は「横→上→前」の順に描く（前の面がいちばん手前）。
    const float Thick=5; // 厚み（ドット）
    const float Lean=.5f; // カメラの見下ろす角の半分だけ、体を後ろへ倒す（真上近くから見ても顔が見え、上の面も見える）
    MeshInstance3D? solid,shadow;static ImageTexture? shadowTex;static StandardMaterial3D? solidMat;
    static readonly Dictionary<string,Image> images=new();static readonly Dictionary<(string,int,int),ArrayMesh> meshes=new();
    public bool Solid
    {
        set
        {
            if(!value||solid!=null)return;sprite.Visible=false;
            solidMat??=new StandardMaterial3D{ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,VertexColorUseAsAlbedo=true,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,CullMode=BaseMaterial3D.CullModeEnum.Disabled,NoDepthTest=true,RenderPriority=-1};
            if(shadowTex==null)
            {
                var img=Image.CreateEmpty(32,32,false,Image.Format.Rgba8);
                for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=Mathf.Sqrt((x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f))/15.5f;img.SetPixel(x,y,new Color(0,0,0,r>=1?0:.5f*Mathf.Min(1,(1-r)*2.5f)));}
                shadowTex=ImageTexture.CreateFromImage(img);
            }
            shadow=new MeshInstance3D{Mesh=new PlaneMesh{Size=Vector2.One},MaterialOverride=new StandardMaterial3D{ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,AlbedoTexture=shadowTex,NoDepthTest=true,RenderPriority=-3},Position=new Vector3(0,.5f,0),Scale=new Vector3(sprite.PixelSize*24,1,sprite.PixelSize*15)};
            AddChild(shadow);
            solid=new MeshInstance3D{MaterialOverride=solidMat,Scale=Vector3.One*sprite.PixelSize};AddChild(solid);SetProcess(true);ShowFrame();
        }
    }
    // いつもカメラの方を向く（縦の軸で回し、見下ろす角の半分だけ後ろへ倒す）。
    public override void _Process(double delta)
    {
        if(solid==null||!Visible)return;
        if(GetViewport()?.GetCamera3D() is {} cam)solid.GlobalRotation=new Vector3(cam.GlobalRotation.X*Lean,cam.GlobalRotation.Y,0);
    }
    int frameCol,frameRow;
    void ShowFrame()
    {
        if(solid==null||sheet==null)return;
        var key=(sheet.File,frameCol,frameRow);
        if(!meshes.TryGetValue(key,out var m)){m=BuildMesh(sheet,frameCol,frameRow);meshes[key]=m;}
        solid.Mesh=m;solid.Scale=new Vector3(sprite.FlipH?-sprite.PixelSize:sprite.PixelSize,sprite.PixelSize,sprite.PixelSize);
    }
    static ArrayMesh BuildMesh(Sheet s,int col,int row)
    {
        if(!images.TryGetValue(s.File,out var img)){img=Image.LoadFromFile(ProjectSettings.GlobalizePath(Root+s.File));images[s.File]=img;}
        int x0=col*s.W,y0=row*s.H;float footX=s.Ox+12.5f,footY=s.Oy+25f,zf=Thick/2,zb=-Thick/2;
        bool Solid1(int x,int y)=>x>=0&&y>=0&&x<s.W&&y<s.H&&x0+x<img.GetWidth()&&y0+y<img.GetHeight()&&img.GetPixel(x0+x,y0+y).A>.5f;
        var groups=new[]{(new List<Vector3>(),new List<Color>()),(new List<Vector3>(),new List<Color>()),(new List<Vector3>(),new List<Color>())}; // 横・上・前
        void Quad(int g,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color col1){var (v,cs)=groups[g];v.Add(a);v.Add(b);v.Add(c);v.Add(d);for(int i=0;i<4;i++)cs.Add(col1);}
        for(int y=0;y<s.H;y++)for(int x=0;x<s.W;x++)
        {
            if(!Solid1(x,y))continue;var c=img.GetPixel(x0+x,y0+y);c.A=1;
            float l=x-footX,r=l+1,t=footY-y,bm=t-1;
            if(!Solid1(x-1,y))Quad(0,new(l,bm,zb),new(l,bm,zf),new(l,t,zf),new(l,t,zb),c.Darkened(.45f));
            if(!Solid1(x+1,y))Quad(0,new(r,bm,zf),new(r,bm,zb),new(r,t,zb),new(r,t,zf),c.Darkened(.35f));
            if(!Solid1(x,y-1))Quad(1,new(l,t,zf),new(r,t,zf),new(r,t,zb),new(l,t,zb),c.Lightened(.28f));
            Quad(2,new(l,bm,zf),new(r,bm,zf),new(r,t,zf),new(l,t,zf),c);
        }
        var verts=new List<Vector3>();var cols=new List<Color>();var idx=new List<int>();
        foreach(var (v,cs) in groups){int b0=verts.Count;verts.AddRange(v);cols.AddRange(cs);for(int q=0;q<v.Count/4;q++){int k=b0+q*4;idx.Add(k);idx.Add(k+1);idx.Add(k+2);idx.Add(k);idx.Add(k+2);idx.Add(k+3);}}
        var mesh=new ArrayMesh();if(verts.Count==0)return mesh;
        var arr=new Godot.Collections.Array();arr.Resize((int)Mesh.ArrayType.Max);arr[(int)Mesh.ArrayType.Vertex]=verts.ToArray();arr[(int)Mesh.ArrayType.Color]=cols.ToArray();arr[(int)Mesh.ArrayType.Index]=idx.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arr);return mesh;
    }
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
        if(solid!=null&&(col!=frameCol||row!=frameRow||solid.Mesh==null)){frameCol=col;frameRow=row;ShowFrame();}else{frameCol=col;frameRow=row;}
    }
    // 絵は左向き。画面上で右へ進むときは左右反転する。
    public void Face(Vector3 direction,Camera3D? camera)
    {
        if(camera==null||direction.LengthSquared()<1e-6f)return;
        float side=direction.Normalized().Dot(camera.GlobalBasis.X);if(Mathf.Abs(side)<.2f)return;sprite.FlipH=side>0; // 真上・真下に近い向きでは、前の右左のままif(solid!=null)solid.Scale=new Vector3(sprite.FlipH?-sprite.PixelSize:sprite.PixelSize,sprite.PixelSize,sprite.PixelSize);
    }
    public bool FacingRight=>sprite.FlipH;
    // 建物に隠れても見えるように、いつも手前に描く（試作版の地図、2026-11-18）。
    public bool OnTop{get=>sprite.NoDepthTest;set=>sprite.NoDepthTest=value;}
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
