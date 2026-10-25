using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

public sealed class Officer3DCustomLook : HakutaiKagami.Core.Appearance.OfficerAppearance {}

public partial class Officer3DCustomActor : Node3D
{
    public float WalkStride {get;private set;}
    public string Lod { get; private set; } = "near";
    static readonly Dictionary<string,Dictionary<string,(Mesh Mesh,Skin Skin)>> lodMeshes = new();
    internal static void ReleaseProcessResources(){white?.Dispose();white=null;protectionTexture?.Dispose();protectionTexture=null;var resources=lodMeshes.Values.SelectMany(d=>d.Values).SelectMany(p=>new Resource[]{p.Mesh,p.Skin}).Where(r=>GodotObject.IsInstanceValid(r)).DistinctBy(r=>r.GetInstanceId()).ToArray();lodMeshes.Clear();foreach(var resource in resources)resource.Dispose();}
    static ImageTexture? white;
    static Texture2D White() { if(white is null) {using var i=Image.CreateEmpty(1,1,false,Image.Format.Rgba8);i.Fill(Colors.White);white=ImageTexture.CreateFromImage(i);} return white; }
    public void SetLod(string level)
    {
        if(!new[]{"near","mid","far"}.Contains(level)) throw new ArgumentException("Unknown LOD");
        if(level==Lod)return;
        if(!lodMeshes.ContainsKey(level))
        {
            var model=GD.Load<PackedScene>("res://Assets/Officer3D/TrialV3/"+level+".glb").Instantiate<Node3D>();
            lodMeshes[level]=Descendants(model).OfType<MeshInstance3D>().ToDictionary(m=>m.Name.ToString(),m=>(m.Mesh,m.Skin));model.Free();
        }
        foreach(var (name,m) in Meshes) {var next=lodMeshes[level][name];if(next.Mesh.GetSurfaceCount()!=m.Mesh.GetSurfaceCount())throw new InvalidOperationException("LOD material contract changed");m.Mesh=next.Mesh;m.Skin=next.Skin;}
        Lod=level;
    }
    public void UpdateLod(float pixels)
    {
        if(Lod=="near" && pixels<160)SetLod(pixels<65?"far":"mid");
        else if(Lod=="mid") {if(pixels>180)SetLod("near");else if(pixels<65)SetLod("far");}
        else if(Lod=="far" && pixels>80)SetLod(pixels>180?"near":"mid");
    }

    public static readonly string[] RedSlots=HakutaiKagami.Core.Appearance.AppearanceCatalog.RedSlots;
    static string SlotForRole(string role){if(role.StartsWith("gear_red_")){string t=role[9..];return t[..t.LastIndexOf('_')];}return role[5..];}
    public static readonly string[] Slots=HakutaiKagami.Core.Appearance.AppearanceCatalog.Slots;
    static readonly Dictionary<string,string[]> Coverage = new()
    {
        ["skin_exposed"] = Array.Empty<string>(), ["skin_torso"] = new[] {"torso","underclothes"},
        ["skin_arms"] = new[] {"arms","underclothes"}, ["skin_waist"] = new[] {"waist","underclothes"},
        ["skin_thighs"] = new[] {"thighs","underclothes"}, ["skin_shins"] = new[] {"shins"}, ["skin_feet"] = new[] {"feet"}
    };
    public Officer3DCustomLook Appearance { get; private set; } = new();
    public AnimationPlayer Player { get; private set; } = null!;
    public Skeleton3D Skeleton { get; private set; } = null!;
    public readonly Dictionary<string, MeshInstance3D> Meshes = new();
    public readonly List<(ShaderMaterial Material,string Role)> Materials = new();
    public Dictionary<string,string> Clips { get; } = new();
    static Texture2D? protectionTexture;
    public static Texture2D FaceProtection(Texture2D source)
    {
        if(protectionTexture is not null)return protectionTexture;
        using var data=JsonDocument.Parse(FileAccess.GetFileAsString("res://Data/Officer3D/face-protection.json"));
        var root=data.RootElement;
        using var original=source.GetImage();if(original.IsCompressed())original.Decompress();original.ClearMipmaps();original.Convert(Image.Format.Rgba8);
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original.GetData())).ToLowerInvariant();
        if(hash!=root.GetProperty("source_rgba_sha256").GetString())throw new InvalidOperationException("Face texture changed; UV protection requires review");
        using var image=Image.CreateEmpty(root.GetProperty("width").GetInt32(),root.GetProperty("height").GetInt32(),false,Image.Format.Rgbaf);image.Fill(new Color(0,0,0,0));
        var coreValues=root.GetProperty("mouth_core_srgb");var core=new Vector3(coreValues[0].GetSingle(),coreValues[1].GetSingle(),coreValues[2].GetSingle());
        int top=root.GetProperty("underpaint_sample_rows")[0].GetInt32(),bottom=root.GetProperty("underpaint_sample_rows")[1].GetInt32();
        foreach(var row in root.GetProperty("mouth_rows").EnumerateArray())
            for(int x=row[1].GetInt32();x<=row[2].GetInt32();x++)
            {
                int y=row[0].GetInt32();var under=original.GetPixel(x,top).Lerp(original.GetPixel(x,bottom),(float)(y-top)/(bottom-top));var pixel=original.GetPixel(x,y);
                var u=new Vector3(under.R,under.G,under.B);var s=new Vector3(pixel.R,pixel.G,pixel.B);var direction=u-core;
                float alpha=Mathf.Clamp((u-s).Dot(direction)/direction.LengthSquared(),0,1);if(alpha>.999f)alpha=1;
                var ul=under.SrgbToLinear();var sl=pixel.SrgbToLinear();
                // Exact linear-light decomposition: source = skin*(1-alpha)+paint.
                // Preserve only paint, never the surrounding skin rectangle.
                alpha=Math.Max(alpha,Math.Max(1-sl.R/ul.R,Math.Max(1-sl.G/ul.G,1-sl.B/ul.B)));
                var paint=new Color(sl.R-ul.R*(1-alpha),sl.G-ul.G*(1-alpha),sl.B-ul.B*(1-alpha),alpha);image.SetPixel(x,y,paint);
            }
        protectionTexture=ImageTexture.CreateFromImage(image);return protectionTexture;
    }
    public static IEnumerable<Node> Descendants(Node n) { foreach (Node child in n.GetChildren()) { yield return child; foreach(var sub in Descendants(child)) yield return sub; } }
    public override void _Ready()
    {
        AddChild(GD.Load<PackedScene>("res://Assets/Officer3D/TrialV3/near.glb").Instantiate<Node3D>());
        var nodes = Descendants(this).ToArray();
        var sourceFace=nodes.OfType<MeshInstance3D>().SelectMany(m=>Enumerable.Range(0,m.Mesh.GetSurfaceCount()).Select(i=>m.GetActiveMaterial(i))).OfType<BaseMaterial3D>().First(m=>m.ResourceName=="skin_face");
        var faceProtection=FaceProtection(sourceFace.AlbedoTexture);
        Player = nodes.OfType<AnimationPlayer>().Single(); Skeleton = nodes.OfType<Skeleton3D>().Single();
        foreach(var name in Player.GetAnimationList())
        {
            var id = name.ToString().Split('/').Last();
            if (!new[] {"idle","walk","attack"}.Contains(id)) continue;
            Clips.Add(id,name); Player.GetAnimation(name).LoopMode = id == "attack" ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        }
        if(Clips.Count != 3) throw new InvalidOperationException("Expected exactly 3 clips");
        foreach(var mesh in nodes.OfType<MeshInstance3D>())
        {
            Meshes.Add(mesh.Name,mesh);
            for(int i=0;i<mesh.Mesh.GetSurfaceCount();i++)
            {
                var source = mesh.GetActiveMaterial(i) as BaseMaterial3D ?? throw new InvalidOperationException("Missing PBR material");
                string role = source.ResourceName;
                bool skin = role.StartsWith("skin_"); bool gear = role.StartsWith("gear_");
                if (!skin && !gear && !role.StartsWith("feature_")) throw new InvalidOperationException("Unbound material: "+role);
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Officer3DV3.gdshader") };
                material.SetShaderParameter("base_texture",source.AlbedoTexture ?? White());
                material.SetShaderParameter("solid_color",source.AlbedoTexture is null ? new Vector3(source.AlbedoColor.SrgbToLinear().R,source.AlbedoColor.SrgbToLinear().G,source.AlbedoColor.SrgbToLinear().B) : Vector3.One);
                material.SetShaderParameter("normal_texture", source.NormalTexture ?? GD.Load<Texture2D>("res://Assets/Officer3D/TrialV1/normal.png"));
                material.SetShaderParameter("orm_texture",source.OrmTexture ?? GD.Load<Texture2D>("res://Assets/Officer3D/TrialV1/orm.png"));
                material.SetShaderParameter("material_mask",GD.Load<Texture2D>("res://Assets/Officer3D/TrialV2/materials.png"));
                material.SetShaderParameter("hair_surface",role.StartsWith("feature_hair"));
                bool authored=role.StartsWith("gear_red_");
                material.SetShaderParameter("authored_pbr",authored);material.SetShaderParameter("authored_roughness",source.Roughness);material.SetShaderParameter("authored_metallic",source.Metallic);
                material.SetShaderParameter("category_override",authored?Array.IndexOf(new[]{"cloth","lacquer","metal"},role.Split('_').Last())+1:0);
                material.SetShaderParameter("kind",skin?1:gear?2:0);
                material.SetShaderParameter("face_surface",role=="skin_face");
                material.SetShaderParameter("protected_skin_texture",faceProtection);
                mesh.SetSurfaceOverrideMaterial(i,material); Materials.Add((material,role));
            }
        }
        foreach(var slot in Slots) { if(!Meshes.ContainsKey("slot_"+slot)) throw new InvalidOperationException("Missing slot "+slot); Appearance.Enabled[slot]=true;Appearance.Equipment[slot]="silver";Appearance.SlotSrgb[slot]=new float[]{1,1,1}; }
        Apply(Appearance);
        var feet=Enumerable.Range(0,Skeleton.GetBoneCount()).Where(i=>Skeleton.GetBoneName(i).ToString().EndsWith("Foot")).ToArray();
        var min=feet.ToDictionary(i=>i,i=>float.PositiveInfinity);var max=feet.ToDictionary(i=>i,i=>float.NegativeInfinity);
        for(int frame=0;frame<42;frame++){Freeze("walk",Player.GetAnimation(Clips["walk"]).Length*frame/42);foreach(int i in feet){float z=Skeleton.GetBoneGlobalPose(i).Origin.Z;min[i]=Math.Min(min[i],z);max[i]=Math.Max(max[i],z);}}
        WalkStride=feet.Select(i=>2*(max[i]-min[i])).DefaultIfEmpty(0).Average();if(WalkStride<.05f)throw new InvalidOperationException("Walk stride could not be measured");Play("idle");
        Player.AnimationFinished += clip => {if(clip == Clips["attack"]) Play("idle");};
    }
    public void Play(string id) => Player.Play(Clips[id],.15);
    public void Freeze(string id,double time) { Player.Play(Clips[id]);Player.Seek(time,true);Player.Pause(); }
    public void Apply(Officer3DCustomLook value)
    {
        value.Validate(Slots); Appearance = value;
        foreach(var slot in Slots) {Meshes["slot_"+slot].Visible=value.Enabled[slot]&&value.Equipment[slot]=="silver";if(RedSlots.Contains(slot))Meshes["alt_"+slot].Visible=value.Enabled[slot]&&value.Equipment[slot]=="red_lamellar";}
        Meshes["face"].Visible=value.Face=="v013_face"; Meshes["face_veteran"].Visible=value.Face=="mature_v1";
        Meshes["hair_tied"].Visible=Meshes["hair_knot"].Visible=value.Hair=="tied_v1"&&!value.Enabled["helmet"];Meshes["beard_short"].Visible=value.Beard=="short_v1"&&!value.Enabled["mask"];
        foreach(var pair in Coverage) if(Meshes.TryGetValue(pair.Key,out var mesh)) mesh.Visible=!pair.Value.Any(slot=>value.Enabled[slot]&&(slot=="underclothes"||value.Equipment[slot]=="silver"||slot=="shins"||slot=="feet"));
        var skin=new Vector3(value.SkinLinear[0],value.SkinLinear[1],value.SkinLinear[2]);
        var global=new Color(value.GearSrgb[0],value.GearSrgb[1],value.GearSrgb[2]).SrgbToLinear();
        foreach(var (mat,role) in Materials)
        {
            foreach(var category in new[]{"cloth","lacquer","metal"}) {var c=value.MaterialSrgb[category];var l=new Color(c[0],c[1],c[2]).SrgbToLinear();mat.SetShaderParameter(category+"_tint",new Vector3(l.R,l.G,l.B));}
            if(role.StartsWith("feature_hair") || role.StartsWith("feature_beard")) {var h=value.HairSrgb;var l=new Color(h[0],h[1],h[2]).SrgbToLinear();mat.SetShaderParameter("tint",new Vector3(l.R,l.G,l.B));}
            if(role.StartsWith("skin_")) mat.SetShaderParameter("tint",skin/new Vector3(.97647065f,.90196085f,.84705889f));
            if(role.StartsWith("gear_")) { var rgb=value.SlotSrgb[SlotForRole(role)]; var c=new Color(rgb[0],rgb[1],rgb[2]).SrgbToLinear()*global;mat.SetShaderParameter("tint",new Vector3(c.R,c.G,c.B)); }
        }
    }
    public void DebugMask(int mode,string target="") {foreach(var (mat,role) in Materials) {mat.SetShaderParameter("debug_mask",mode);mat.SetShaderParameter("selected_gear",target=="" || (role.StartsWith("gear_")&&SlotForRole(role)==target));} }
    public static Officer3DCustomLook Preset(string id)
    {
        if(!new[]{"commander","veteran","red_guard"}.Contains(id))throw new ArgumentException("Unknown preset");
        var a=new Officer3DCustomLook();foreach(var slot in Slots){a.Enabled[slot]=true;a.Equipment[slot]=id=="red_guard"&&RedSlots.Contains(slot)?"red_lamellar":"silver";a.SlotSrgb[slot]=new float[]{1,1,1};}
        if(id!="commander"){a.Face="mature_v1";a.Hair="tied_v1";a.Beard="short_v1";a.Enabled["mask"]=false;a.Enabled["helmet"]=false;a.Enabled["cloak"]=false;}
        return a;
    }
    public string Save() => JsonSerializer.Serialize(Appearance,new JsonSerializerOptions { WriteIndented=true });
    public void Restore(string json) => Apply(JsonSerializer.Deserialize<Officer3DCustomLook>(json) ?? throw new InvalidOperationException("Invalid appearance"));
}
