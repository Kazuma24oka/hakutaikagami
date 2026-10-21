using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Appearance;

// Static appearance assets cannot introduce collision/navigation or move the shared ground.
public sealed class SceneryVisualAssets
{
    public readonly SceneryAppearanceCatalog Catalog=SceneryAppearance.Read(FileAccess.GetFileAsBytes("res://Data/Scenery/scenery-appearance-v1.json"));
    // Godot's native cache does not provide a managed lifetime owner. Keep the
    // finite catalog resources strongly referenced while this process uses them.
    static readonly Dictionary<string,Resource> retainedResources=new(StringComparer.Ordinal);
    public static int RetainedResourceCount=>retainedResources.Count;
    public int ResourceLimit=>Catalog.Materials.Where(m=>m.Resource!=null).Select(m=>m.Resource!).Concat(Catalog.Visuals.SelectMany(v=>v.Variants).Select(v=>v.Resource)).Distinct(StringComparer.Ordinal).Count();
    static T Retain<T>(string path) where T:Resource{if(!retainedResources.TryGetValue(path,out var resource)){resource=GD.Load<T>(path);retainedResources.Add(path,resource);}return resource is T typed&&GodotObject.IsInstanceValid(typed)?typed:throw new InvalidOperationException("Retained resource type/lifetime differs");}
    readonly Dictionary<string,StandardMaterial3D> materials=new();
    public readonly HashSet<string> Notices=new(StringComparer.Ordinal);
    public Color Color(string id)=>new(Catalog.Materials.Single(m=>m.Id==id).Color);
    public int Tile(string id)=>Catalog.Materials.Single(m=>m.Id==id).TileSizeMm;
    public static bool SafeMaterial(Material? m)=>m==null||m is StandardMaterial3D s&&!s.Grow&&!s.NoDepthTest&&s.BillboardMode==BaseMaterial3D.BillboardModeEnum.Disabled&&s.NextPass==null;
    public StandardMaterial3D Material(string id,bool vertex=false)
    {
        var key=id+"/"+vertex;if(materials.TryGetValue(key,out var known))return known;
        var d=Catalog.Materials.Single(m=>m.Id==id);StandardMaterial3D? result=null;
        if(d.Resource!=null){if(ResourceLoader.Exists(d.Resource)&&Retain<Material>(d.Resource) is StandardMaterial3D s&&SafeMaterial(s))result=(StandardMaterial3D)s.Duplicate();else Notices.Add("材質を仮素材で表示："+id);}
        result??=new StandardMaterial3D{Roughness=1,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
        result.AlbedoColor=vertex?Colors.White:Color(id);result.VertexColorUseAsAlbedo=vertex;materials[key]=result;return result;
    }
    public Node3D? Instantiate(SceneryVisual visual,SceneryPlacement placement)
    {
        var variant=visual.Variants.SingleOrDefault(v=>v.Id==placement.Variant);
        if(variant==null||!ResourceLoader.Exists(variant.Resource)){Notices.Add("モデルを仮素材で表示："+visual.Id+" / "+placement.Variant);return null;}
        Node3D? root=null;
        try
        {
            root=Retain<PackedScene>(variant.Resource).Instantiate<Node3D>();
            if(!root.Transform.IsEqualApprox(Transform3D.Identity))throw new InvalidOperationException("Scene origin must be ground-center");
            var scale=new Vector3(placement.WidthMm/(float)visual.AuthoringWidthMm,placement.HeightMm/(float)visual.AuthoringHeightMm,placement.DepthMm/(float)visual.AuthoringDepthMm);
            var yaw=new Basis(Vector3.Up,Mathf.DegToRad(placement.YawMilliDegrees/1000f));
            var faces=new List<Vector3>();
            void Read(Node node,Transform3D t)
            {
                if(node.GetScript().VariantType!=Variant.Type.Nil||node is not Node3D n||(node.GetType()!=typeof(Node3D)&&node is not MeshInstance3D))throw new InvalidOperationException("Only static mesh nodes are allowed");
                t*=n.Transform;
                if(n is MeshInstance3D m&&m.Mesh!=null)
                {
                    if(!SafeMaterial(m.MaterialOverride)||!SafeMaterial(m.MaterialOverlay)||Enumerable.Range(0,m.Mesh.GetSurfaceCount()).Any(i=>!SafeMaterial(m.Mesh.SurfaceGetMaterial(i))))throw new InvalidOperationException("Animated/deforming material is not compatible");
                    foreach(var p in m.Mesh.GetFaces()){var q=t*p;faces.Add(yaw*(q*scale));if(faces.Count>600000)throw new InvalidOperationException("Scene requires a bounded static LOD");}
                    if(m.MaterialOverride==null&&Enumerable.Range(0,m.Mesh.GetSurfaceCount()).All(i=>m.Mesh.SurfaceGetMaterial(i)==null))m.MaterialOverride=Material(visual.Material);
                }
                foreach(Node child in node.GetChildren())Read(child,t);
            }
            Read(root,Transform3D.Identity);if(faces.Count==0)throw new InvalidOperationException("No mesh faces");
            float x1=(placement.FootMin.X-placement.Point.X)/1000f,x2=(placement.FootMax.X-placement.Point.X)/1000f;
            float z1=-(placement.FootMax.Y-placement.Point.Y)/1000f,z2=-(placement.FootMin.Y-placement.Point.Y)/1000f;
            void Check(Vector3 p){if(p.X<x1-.002f||p.X>x2+.002f||p.Z<z1-.002f||p.Z>z2+.002f)throw new InvalidOperationException("Scene exceeds authoritative ground footprint");}
            foreach(var p in faces){if(!p.IsFinite()||p.Y<-.002f||p.Y>Math.Max(8,placement.HeightMm/1000f*1.8f))throw new InvalidOperationException("Invalid ground pivot or height");if(placement.Role=="rock"||p.Y<=4)Check(p);}
            // Clip triangles at4m: roof eaves/canopies are permitted only above ground clearance.
            if(placement.Role!="rock")for(int i=0;i<faces.Count;i+=3)for(int j=0;j<3;j++){var a=faces[i+j];var b=faces[i+(j+1)%3];if((a.Y<4&&b.Y>4)||(a.Y>4&&b.Y<4))Check(a.Lerp(b,(4-a.Y)/(b.Y-a.Y)));}
            root.Scale=scale;root.Rotation=new(0,Mathf.DegToRad(placement.YawMilliDegrees/1000f),0);return root;
        }
        catch(Exception e)when(e is InvalidOperationException or ArgumentException or InvalidCastException)
        {root?.Free();Notices.Add("モデル契約に合わないため仮素材で表示："+visual.Id+" / "+e.Message);return null;}
    }
}
