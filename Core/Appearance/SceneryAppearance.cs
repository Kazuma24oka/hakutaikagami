using System.Text.RegularExpressions;
using System.Text;
using HakutaiKagami.Core.Contracts;
namespace HakutaiKagami.Core.Appearance;

public sealed record SceneryMaterial(string Id,string Color,string? Resource,int TileSizeMm);
public sealed record ScenerySceneVariant(string Id,string Resource);
public sealed record SceneryVisual(string Id,string Role,string Builder,string Material,ScenerySceneVariant[] Variants,int AuthoringWidthMm,int AuthoringDepthMm,int AuthoringHeightMm,string? Fallback);
public sealed record SceneryBinding(string Appearance,string Visual);
public sealed record ScenerySkin(string Id,SceneryBinding[] Bindings);
public sealed record SceneryAppearanceCatalog(int Schema,string DefaultSkin,SceneryMaterial[] Materials,SceneryVisual[] Visuals,ScenerySkin[] Skins);
public sealed record SceneryPlacement(string Id,string SourceId,string Role,string Appearance,FieldPoint Point,FieldPoint FootMin,FieldPoint FootMax,int WidthMm,int DepthMm,int HeightMm,int YawMilliDegrees,string Variant,uint Seed);

// Presentation-only IDs. Neither the visual catalog nor selected skin is a world-rules dependency.
public static class SceneryAppearance
{
    public const string LayoutVersion="scenery-layout-v1";
    public const string RegionId="region:kiyosu-trial-v1";
    public static readonly string[] Roles=["surface/road","surface/grass","surface/forest","surface/water","surface/cliff","surface/slope","surface/bridge","cut","tree","rock","keep","wall","rail"];
    public static string Appearance(string role)=>"appearance:"+role;
    static bool Id(string s)=>s!=null&&Regex.IsMatch(s,"^[a-z][a-z0-9_:/-]{1,95}$");
    public static bool ResourcePath(string path,string extension)=>path!=null&&path.StartsWith("res://Assets/Scenery/",StringComparison.Ordinal)&&!path.Contains("..",StringComparison.Ordinal)&&Regex.IsMatch(path,"^res://Assets/Scenery/[A-Za-z0-9_/-]+\\."+extension+"$");
    public static SceneryAppearanceCatalog Read(byte[] bytes){var c=ContractJson.Read<SceneryAppearanceCatalog>(bytes);Validate(c);return c;}
    public static void Validate(SceneryAppearanceCatalog c)
    {
        if(c.Schema!=1||c.Materials==null||c.Visuals==null||c.Skins==null||c.Materials.Length>256||c.Visuals.Length>256||c.Skins.Length>32)throw new InvalidDataException("Unknown appearance catalog");
        if(c.Materials.Select(x=>x.Id).Distinct().Count()!=c.Materials.Length||c.Visuals.Select(x=>x.Id).Distinct().Count()!=c.Visuals.Length||c.Skins.Select(x=>x.Id).Distinct().Count()!=c.Skins.Length)throw new InvalidDataException("Duplicate appearance IDs");
        foreach(var m in c.Materials)if(!Id(m.Id)||!Regex.IsMatch(m.Color,"^[0-9a-f]{6}$")||m.TileSizeMm<100||m.TileSizeMm>100000||m.Resource!=null&&!ResourcePath(m.Resource,"tres"))throw new InvalidDataException("Material contract invalid");
        foreach(var v in c.Visuals)
        {
            if(!Id(v.Id)||!Roles.Contains(v.Role)||!c.Materials.Any(m=>m.Id==v.Material)||v.Variants==null||v.Variants.Length>16||v.AuthoringWidthMm<=0||v.AuthoringDepthMm<=0||v.AuthoringHeightMm<=0||v.AuthoringWidthMm>200000||v.AuthoringDepthMm>200000||v.AuthoringHeightMm>200000)throw new InvalidDataException("Visual contract invalid");
            if(v.Builder!="scene"&&v.Builder!=(v.Role.StartsWith("surface/",StringComparison.Ordinal)?"surface":v.Role))throw new InvalidDataException("Builder/role mismatch");
            if(v.Builder=="scene"&&(v.Role.StartsWith("surface/",StringComparison.Ordinal)||v.Role=="cut"||v.Variants.Length==0||v.Fallback==null))throw new InvalidDataException("Ground must remain a shared surface; scene needs a fallback");
            if(v.Builder!="scene"&&(v.Variants.Length!=0||v.Fallback!=null))throw new InvalidDataException("Procedural fallback must be terminal");
            if(v.Variants.Select(x=>x.Id).Distinct().Count()!=v.Variants.Length||v.Variants.Any(x=>!Id(x.Id)||!ResourcePath(x.Resource,"(?:tscn|glb)")))throw new InvalidDataException("Scene variants invalid");
            var seen=new HashSet<string>();var at=v;while(at.Fallback is {} id){if(!seen.Add(at.Id))throw new InvalidDataException("Cyclic fallback");at=c.Visuals.SingleOrDefault(x=>x.Id==id&&x.Role==v.Role)??throw new InvalidDataException("Missing fallback");}
        }
        foreach(var skin in c.Skins){if(!Id(skin.Id)||skin.Bindings==null||skin.Bindings.Length!=Roles.Length||skin.Bindings.Select(b=>b.Appearance).Distinct().Count()!=Roles.Length)throw new InvalidDataException("Skin binding invalid");foreach(var role in Roles){var b=skin.Bindings.SingleOrDefault(b=>b.Appearance==Appearance(role));if(b==null||!c.Visuals.Any(v=>v.Id==b.Visual&&v.Role==role))throw new InvalidDataException("Unbound role");}}
        if(!c.Skins.Any(s=>s.Id==c.DefaultSkin))throw new InvalidDataException("No default skin");
    }
    public static SceneryVisual Resolve(SceneryAppearanceCatalog c,string skin,string appearance)=>c.Visuals.Single(v=>v.Id==c.Skins.Single(s=>s.Id==skin).Bindings.Single(b=>b.Appearance==appearance).Visual);
    public static uint StableSeed(string id){uint hash=2166136261;foreach(var b in Encoding.UTF8.GetBytes(id)){hash^=b;hash=unchecked(hash*16777619);}return hash;}
    public static SceneryPlacement[] Placements(string regionId=RegionId)
    {
        if(!Id(regionId)||!regionId.StartsWith("region:",StringComparison.Ordinal))throw new InvalidDataException("Invalid region identity");
        var list=new List<SceneryPlacement>();
        void Add(string id,string role,FieldPoint p,FieldPoint a,FieldPoint b,int width,int depth,int height,bool rotate=false){var key=regionId+"/"+id;var seed=StableSeed(key);list.Add(new(key,id,role,Appearance(role),p,a,b,width,depth,height,rotate?(int)(seed%360000):0,(seed&1)==0?"variant:a":"variant:b",seed));}
        for(int x=-6;x<10;x++)for(int y=-6;y<10;y++){var p=new FieldPoint(x*10000L+5000,y*10000L+5000);var a=new FieldPoint(x*10000L,y*10000L);var b=new FieldPoint((x+1)*10000L,(y+1)*10000L);var kind=RegionalScenery.Kind(x,y);Add($"surface:{x}/{y}","surface/"+kind,p,a,b,10000,10000,0);if(kind=="cliff")Add($"rock:{x}/{y}","rock",p,a,b,4600,4600,3200,true);}
        foreach(var o in RegionalScenery.Obstacles)Add(o.Id,o.Kind,new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2),o.Min,o.Max,checked((int)(o.Max.X-o.Min.X)),checked((int)(o.Max.Y-o.Min.Y)),checked((int)o.HeightMm),o.Kind=="tree");
        Add("bridge:deck","surface/bridge",new(5000,55000),new(0,50000),new(10000,60000),10000,10000,0);
        Add("terrain:cut","cut",new(0,0),new(-60000,-60000),new(100000,100000),160000,160000,8000);
        return list.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
    }
    public static string PlacementHash=>ContractJson.CanonicalHash(Placements());
}
