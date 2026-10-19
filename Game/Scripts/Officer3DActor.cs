using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

public sealed class Officer3DLook
{
    public int Schema { get; set; } = 2;
    public string Model { get; set; } = "v013";
    public string Body { get; set; } = "v013_base";
    public string Face { get; set; } = "v013_face";
    public string Hair { get; set; } = "none_under_helmet";
    public string Beard { get; set; } = "none";
    public float[] HairSrgb { get; set; } = {1,1,1};
    public Dictionary<string,float[]> MaterialSrgb { get; set; } = new() { ["cloth"]=new float[]{1,1,1}, ["lacquer"]=new float[]{1,1,1}, ["metal"]=new float[]{1,1,1} };
    public float[] SkinLinear { get; set; } = { .87f, .73f, .59f };
    public float[] GearSrgb { get; set; } = { 1, 1, 1 };
    public Dictionary<string,bool> Enabled { get; set; } = new();
    public Dictionary<string,float[]> SlotSrgb { get; set; } = new();
    public void Validate(IEnumerable<string> slots)
    {
        var keys = slots.Order().ToArray();
        if (Schema != 2 || Model != "v013" || Body != "v013_base" || !new[]{"v013_face","angular_jaw"}.Contains(Face) || !new[]{"none_under_helmet","tied"}.Contains(Hair) || !new[]{"none","short"}.Contains(Beard) || !MaterialSrgb.Keys.Order().SequenceEqual(new[]{"cloth","lacquer","metal"}) || !Enabled.Keys.Order().SequenceEqual(keys) || !SlotSrgb.Keys.Order().SequenceEqual(keys)) throw new InvalidOperationException("Unknown appearance schema/model/slots");
        foreach (var rgb in new[] { SkinLinear, GearSrgb, HairSrgb }.Concat(SlotSrgb.Values).Concat(MaterialSrgb.Values))
            if (rgb.Length != 3 || rgb.Any(v => !float.IsFinite(v) || v < 0 || v > 1)) throw new InvalidOperationException("Invalid RGB");
    }
}

public partial class Officer3DActor : Node3D
{
    public float WalkStride {get;private set;}
    public string Lod { get; private set; } = "near";
    static readonly Dictionary<string,Dictionary<string,(Mesh Mesh,Skin Skin)>> lodMeshes = new();
    static ImageTexture? white;
    static Texture2D White() { if(white is null) {var i=Image.CreateEmpty(1,1,false,Image.Format.Rgba8);i.Fill(Colors.White);white=ImageTexture.CreateFromImage(i);} return white; }
    public void SetLod(string level)
    {
        if(!new[]{"near","mid","far"}.Contains(level)) throw new ArgumentException("Unknown LOD");
        if(level==Lod)return;
        if(!lodMeshes.ContainsKey(level))
        {
            var model=GD.Load<PackedScene>("res://Assets/Officer3D/TrialV2/"+level+".glb").Instantiate<Node3D>();
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

    public static readonly string[] Slots = { "mask", "weapon", "underclothes", "cloak", "helmet", "torso", "neck_guard", "shoulders", "arms", "feet", "waist", "thighs", "shins" };
    static readonly Dictionary<string,string[]> Coverage = new()
    {
        ["skin_exposed"] = Array.Empty<string>(), ["skin_torso"] = new[] {"torso","underclothes"},
        ["skin_arms"] = new[] {"arms","underclothes"}, ["skin_waist"] = new[] {"waist","underclothes"},
        ["skin_thighs"] = new[] {"thighs","underclothes"}, ["skin_shins"] = new[] {"shins"}, ["skin_feet"] = new[] {"feet"}
    };
    public Officer3DLook Appearance { get; private set; } = new();
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
        var image=Image.CreateEmpty(root.GetProperty("width").GetInt32(),root.GetProperty("height").GetInt32(),false,Image.Format.Rgbaf);image.Fill(new Color(0,0,0,0));
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
        AddChild(GD.Load<PackedScene>("res://Assets/Officer3D/TrialV2/near.glb").Instantiate<Node3D>());
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
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Officer3DV2.gdshader") };
                material.SetShaderParameter("base_texture",source.AlbedoTexture ?? White());
                material.SetShaderParameter("solid_color",source.AlbedoTexture is null ? new Vector3(source.AlbedoColor.SrgbToLinear().R,source.AlbedoColor.SrgbToLinear().G,source.AlbedoColor.SrgbToLinear().B) : Vector3.One);
                material.SetShaderParameter("normal_texture", source.NormalTexture ?? GD.Load<Texture2D>("res://Assets/Officer3D/TrialV1/normal.png"));
                material.SetShaderParameter("orm_texture",source.OrmTexture ?? GD.Load<Texture2D>("res://Assets/Officer3D/TrialV1/orm.png"));
                material.SetShaderParameter("material_mask",GD.Load<Texture2D>("res://Assets/Officer3D/TrialV2/materials.png"));
                material.SetShaderParameter("kind",skin?1:gear?2:0);
                material.SetShaderParameter("face_surface",role=="skin_face");
                material.SetShaderParameter("protected_skin_texture",faceProtection);
                mesh.SetSurfaceOverrideMaterial(i,material); Materials.Add((material,role));
            }
        }
        foreach(var slot in Slots) { if(!Meshes.ContainsKey("slot_"+slot)) throw new InvalidOperationException("Missing slot "+slot); Appearance.Enabled[slot]=true;Appearance.SlotSrgb[slot]=new float[]{1,1,1}; }
        Apply(Appearance);
        var feet=Enumerable.Range(0,Skeleton.GetBoneCount()).Where(i=>Skeleton.GetBoneName(i).ToString().EndsWith("Foot")).ToArray();
        var min=feet.ToDictionary(i=>i,i=>float.PositiveInfinity);var max=feet.ToDictionary(i=>i,i=>float.NegativeInfinity);
        for(int frame=0;frame<42;frame++){Freeze("walk",Player.GetAnimation(Clips["walk"]).Length*frame/42);foreach(int i in feet){float z=Skeleton.GetBoneGlobalPose(i).Origin.Z;min[i]=Math.Min(min[i],z);max[i]=Math.Max(max[i],z);}}
        WalkStride=feet.Select(i=>2*(max[i]-min[i])).DefaultIfEmpty(0).Average();if(WalkStride<.05f)throw new InvalidOperationException("Walk stride could not be measured");Play("idle");
        Player.AnimationFinished += clip => {if(clip == Clips["attack"]) Play("idle");};
    }
    public void Play(string id) => Player.Play(Clips[id],.15);
    public void Freeze(string id,double time) { Player.Play(Clips[id]);Player.Seek(time,true);Player.Pause(); }
    public void Apply(Officer3DLook value)
    {
        value.Validate(Slots); Appearance = value;
        foreach(var slot in Slots) Meshes["slot_"+slot].Visible=value.Enabled[slot];
        Meshes["face"].Visible=value.Face=="v013_face"; Meshes["face_veteran"].Visible=value.Face=="angular_jaw";
        Meshes["hair_tied"].Visible=Meshes["hair_knot"].Visible=value.Hair=="tied";Meshes["beard_short"].Visible=value.Beard=="short";
        foreach(var pair in Coverage) if(Meshes.TryGetValue(pair.Key,out var mesh)) mesh.Visible=!pair.Value.Any(slot=>value.Enabled[slot]);
        var skin=new Vector3(value.SkinLinear[0],value.SkinLinear[1],value.SkinLinear[2]);
        var global=new Color(value.GearSrgb[0],value.GearSrgb[1],value.GearSrgb[2]).SrgbToLinear();
        foreach(var (mat,role) in Materials)
        {
            foreach(var category in new[]{"cloth","lacquer","metal"}) {var c=value.MaterialSrgb[category];var l=new Color(c[0],c[1],c[2]).SrgbToLinear();mat.SetShaderParameter(category+"_tint",new Vector3(l.R,l.G,l.B));}
            if(role.StartsWith("feature_hair") || role.StartsWith("feature_beard")) {var h=value.HairSrgb;var l=new Color(h[0],h[1],h[2]).SrgbToLinear();mat.SetShaderParameter("tint",new Vector3(l.R,l.G,l.B));}
            if(role.StartsWith("skin_")) mat.SetShaderParameter("tint",skin/new Vector3(.97647065f,.90196085f,.84705889f));
            if(role.StartsWith("gear_")) { var rgb=value.SlotSrgb[role[5..]]; var c=new Color(rgb[0],rgb[1],rgb[2]).SrgbToLinear()*global;mat.SetShaderParameter("tint",new Vector3(c.R,c.G,c.B)); }
        }
    }
    public void DebugMask(int mode,string target="") {foreach(var (mat,role) in Materials) {mat.SetShaderParameter("debug_mask",mode);mat.SetShaderParameter("selected_gear",target=="" || role=="gear_"+target);} }
    public string Save() => JsonSerializer.Serialize(Appearance,new JsonSerializerOptions { WriteIndented=true });
    public void Restore(string json) => Apply(JsonSerializer.Deserialize<Officer3DLook>(json) ?? throw new InvalidOperationException("Invalid appearance"));
}
