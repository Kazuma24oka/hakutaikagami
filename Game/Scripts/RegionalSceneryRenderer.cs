using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;

// Render geometry comes from the same immutable surface/footprint catalog used by Core.
public partial class RegionalSceneryRenderer : Node3D
{
    public readonly List<(Vector3 A,Vector3 B,Vector3 C)> PickTriangles=new();
    public int TreeCount{get;private set;}public int StructureCount{get;private set;}
    public string CatalogHash=>RegionalScenery.Version;
    readonly SceneryVisualAssets assets=new();
    public string Skin{get;private set;}="skin:baseline";
    public int PackedAssetCount{get;private set;}
    public StandardMaterial3D RoadMaterial=>assets.Material(Visual("surface/road").Material);
    public string LayoutHash=>SceneryAppearance.PlacementHash;
    public string AppearanceHash=>ContractJson.CanonicalHash(assets.Catalog);
    public string[] Notices=>assets.Notices.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    public readonly Dictionary<string,string> Usage=new(StringComparer.Ordinal);
    string? selectionNotice;
    public void SetSkin(string id){selectionNotice=null;if(!assets.Catalog.Skins.Any(x=>x.Id==id)){selectionNotice="外観セットがないため標準表示："+id;id=assets.Catalog.DefaultSkin;}Skin=id;lastCenter=null;}
    SceneryVisual Visual(string role)=>SceneryAppearance.Resolve(assets.Catalog,Skin,SceneryAppearance.Appearance(role));
    FieldPoint? lastCenter;
    FieldPoint center=new(0,0);
    public Vector3 GroundPoint(FieldPoint p)=>new((p.X-center.X)/1000f,RegionalScenery.Height(p)/1000f,-(p.Y-center.Y)/1000f);
    Vector3 Vertex(SceneryVertex p)=>new((p.Point.X-center.X)/1000f,p.HeightMm/1000f,-(p.Point.Y-center.Y)/1000f);
    StandardMaterial3D Mat(string id,bool vertex=false)=>assets.Material(id,vertex);
    void AddMesh(Mesh mesh,Material material,Vector3 position,Vector3? rotation=null){var m=new MeshInstance3D{Mesh=mesh,MaterialOverride=material,Position=position};if(rotation is {} r)m.Rotation=r;AddChild(m);}
    void Box(Vector3 size,Vector3 p,string color)=>AddMesh(new BoxMesh{Size=size},Mat(color),p);
    void Pole(Vector3 p,float radius,float height,string color)=>AddMesh(new CylinderMesh{TopRadius=radius*.78f,BottomRadius=radius,Height=height,RadialSegments=7,Rings=1},Mat(color),p+Vector3.Up*height/2);
    static Color Tint(Color color,float amount)=>new(color.R*amount,color.G*amount,color.B*amount);
    Color GroundColor(FieldPoint p,string kind)
    {
        float h=RegionalScenery.Height(p)/1000f;
        var baseColor=assets.Color(Visual("surface/"+kind).Material);
        if(kind is "grass" or "slope"&&h>16)baseColor=baseColor.Lerp(assets.Color("material:highland"),Mathf.Clamp((h-16)/30,0,.75f));
        long ix=(long)Math.Floor(p.X/1250.0),iy=(long)Math.Floor(p.Y/1250.0);var n=(int)((ix*73856093^iy*19349663)&15);return Tint(baseColor,.93f+n*.008f);
    }
    public void Rebuild(FieldPoint newCenter)
    {
        if(lastCenter==newCenter)return;lastCenter=newCenter;center=newCenter;
        foreach(Node n in GetChildren()){RemoveChild(n);n.Free();}PickTriangles.Clear();TreeCount=0;StructureCount=0;PackedAssetCount=0;Usage.Clear();assets.Notices.Clear();if(selectionNotice!=null)assets.Notices.Add(selectionNotice);
        var surfaces=new Dictionary<string,SurfaceTool>();
        foreach(var role in SceneryAppearance.Roles.Where(r=>r.StartsWith("surface/",StringComparison.Ordinal)&&r!="surface/bridge")){var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);surfaces[role[8..]]=st;}
        var placements=SceneryAppearance.Placements();

        foreach(var t in RegionalScenery.Surfaces())
        {
            var va=Vertex(t.A);var vb=Vertex(t.B);var vc=Vertex(t.C);PickTriangles.Add((va,vb,vc));
            bool bridge=t.Kind=="road"&&t.A.Point.X>=0&&t.C.Point.X<=10000&&t.A.Point.Y>=50000&&t.C.Point.Y<=60000;
            if(bridge)continue;
            var target=surfaces[t.Kind];var material=Visual("surface/"+t.Kind).Material;
            // Subdivision changes color only; vertices stay on the exact Core triangle.
            const int n=4;
            Vector3 At(int i,int j)=>va+(vb-va)*(i/(float)n)+(vc-va)*(j/(float)n);
            void Add(Vector3 v){var p=new FieldPoint((long)Math.Round(v.X*1000)+center.X,(long)Math.Round(-v.Z*1000)+center.Y);target.SetColor(GroundColor(p,t.Kind));target.SetUV(new(p.X/(float)assets.Tile(material),p.Y/(float)assets.Tile(material)));target.AddVertex(v);}
            for(int i=0;i<n;i++)for(int j=0;j<n-i;j++){Add(At(i,j));Add(At(i+1,j));Add(At(i,j+1));if(i+j<n-1){Add(At(i+1,j));Add(At(i+1,j+1));Add(At(i,j+1));}}
        }
        foreach(var (kind,st) in surfaces){st.GenerateNormals();var mat=Mat(Visual("surface/"+kind).Material,true);if(kind=="water")mat.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;AddMesh(st.Commit(),mat,Vector3.Zero);}
        BuildSides();BuildBridge();
        foreach(var p in placements){if(p.Role=="tree")TreeCount++;if(p.Role is "keep" or "wall" or "rail")StructureCount++;var v=Visual(p.Role);Usage[p.Id]=v.Id+" / "+p.Variant;if(p.Role.StartsWith("surface/",StringComparison.Ordinal)||p.Role=="cut")continue;
            while(v.Builder=="scene"){var node=assets.Instantiate(v,p);if(node!=null){node.Position=GroundPoint(p.Point);AddChild(node);PackedAssetCount++;Usage[p.Id]=v.Id+" / "+p.Variant+" / "+v.Variants.Single(x=>x.Id==p.Variant).Resource;break;}v=assets.Catalog.Visuals.Single(x=>x.Id==v.Fallback);Usage[p.Id]=v.Id+" / "+p.Variant;}
            if(v.Builder=="scene")continue;
            if(p.Role=="rock"){AddMesh(new SphereMesh{Radius=p.WidthMm/2000f,Height=p.HeightMm/1000f,RadialSegments=p.Variant=="variant:a"?6:8,Rings=3},Mat(v.Material),GroundPoint(p.Point)+Vector3.Up*(p.HeightMm/2000f),new(0,Mathf.DegToRad(p.YawMilliDegrees/1000f),0));continue;}
            var o=RegionalScenery.Obstacles.Single(o=>o.Id==p.SourceId);
            if(p.Role=="tree")BuildTree(o,p,v);else if(p.Role=="keep")BuildKeep(o,v);else if(p.Role=="wall")BuildWall(o,v);else if(p.Role=="rail")BuildRail(o,v);
        }
    }
    void BuildSides()
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        void Edge(FieldPoint a,FieldPoint b){var u=GroundPoint(a);var v=GroundPoint(b);var lowU=new Vector3(u.X,-8,u.Z);var lowV=new Vector3(v.X,-8,v.Z);foreach(var p in new[]{u,v,lowV,u,lowV,lowU})st.AddVertex(p);}
        for(int i=-6;i<10;i++){Edge(new(i*10000,-60000),new((i+1)*10000,-60000));Edge(new(i*10000,100000),new((i+1)*10000,100000));Edge(new(-60000,i*10000),new(-60000,(i+1)*10000));Edge(new(100000,i*10000),new(100000,(i+1)*10000));}
        st.GenerateNormals();AddMesh(st.Commit(),Mat(Visual("cut").Material),Vector3.Zero);
    }
    void BuildTree(SceneryObstacle o,SceneryPlacement placement,SceneryVisual visual)
    {
        var first=GetChildCount();var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float height=o.HeightMm/1000f;Pole(p,.35f,height*.74f,visual.Material);
        bool pine=placement.Variant=="variant:a";
        if(pine){for(int i=0;i<3;i++)AddMesh(new CylinderMesh{TopRadius=.08f,BottomRadius=2.5f-i*.45f,Height=4,RadialSegments=7,Rings=1},Mat(i==0?"material:pine-dark":i==1?"material:pine-mid":"material:pine-light"),p+Vector3.Up*(height-4+i*1.35f));}
        else{AddMesh(new SphereMesh{Radius=2.8f,Height=5,RadialSegments=7,Rings=4},Mat("material:broad-dark"),p+new Vector3(.4f,height-1.4f,0));AddMesh(new SphereMesh{Radius=2,Height=3.5f,RadialSegments=7,Rings=4},Mat("material:broad-light"),p+new Vector3(-.8f,height,.3f));}
        var turn=new Basis(Vector3.Up,Mathf.DegToRad(placement.YawMilliDegrees/1000f));foreach(Node3D child in GetChildren().Skip(first)){child.Position=p+turn*(child.Position-p);child.Basis=turn*child.Basis;}
    }
    void Roof(Vector3 p,float width,float depth,float rise,string color)
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);var a=p+new Vector3(-width/2,0,-depth/2);var b=p+new Vector3(width/2,0,-depth/2);var c=p+new Vector3(width/2,0,depth/2);var d=p+new Vector3(-width/2,0,depth/2);var l=p+new Vector3(-width*.28f,rise,0);var r=p+new Vector3(width*.28f,rise,0);var faces=new[]{a,b,r,a,r,l,b,c,r,c,d,l,c,l,r,d,a,l};for(int i=0;i<faces.Length;i+=3){st.AddVertex(faces[i+2]);st.AddVertex(faces[i+1]);st.AddVertex(faces[i]);}st.GenerateNormals();AddMesh(st.Commit(),Mat(color),Vector3.Zero);Box(new(width*.58f,.18f,.18f),p+Vector3.Up*rise,"material:ridge");
    }
    void BuildKeep(SceneryObstacle o,SceneryVisual visual)
    {
        var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float width=(o.Max.X-o.Min.X)/1000f,depth=(o.Max.Y-o.Min.Y)/1000f;
        Box(new(width,1.1f,depth),p+Vector3.Up*.55f,"material:foundation");Box(new(width-.5f,3,depth-.5f),p+Vector3.Up*2.6f,visual.Material);
        for(int side=-1;side<=1;side+=2)for(int i=-2;i<=2;i++){Box(new(.15f,3,.18f),p+new Vector3(i*1.5f,2.6f,side*(depth-.4f)/2),"material:timber-dark");Box(new(.9f,.95f,.1f),p+new Vector3(i*1.5f,3,side*(depth-.3f)/2),"material:window");}
        Roof(p+Vector3.Up*4.2f,width+1.6f,depth+1.6f,1.7f,"material:roof-lower");
        Box(new(width*.65f,2.5f,depth*.62f),p+Vector3.Up*6.2f,"material:plaster");
        for(int side=-1;side<=1;side+=2){Box(new(width*.65f,.2f,.16f),p+new Vector3(0,6.8f,side*depth*.32f),"material:beam");for(int i=-1;i<=1;i++)Box(new(.65f,.8f,.12f),p+new Vector3(i*1.6f,6.4f,side*depth*.32f),"material:upper-window");}
        Roof(p+Vector3.Up*7.5f,width*.8f,depth*.8f,1.6f,"material:roof-upper");
        // The entire foundation footprint is the exact blocked keep rectangle.
    }
    void BuildWall(SceneryObstacle o,SceneryVisual visual)
    {
        var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float sx=(o.Max.X-o.Min.X)/1000f,sz=(o.Max.Y-o.Min.Y)/1000f;Box(new(sx,1,sz),p+Vector3.Up*.5f,"material:wall-earth");
        bool alongX=sx>sz;int count=(int)Math.Floor(Math.Max(sx,sz)/.65f);for(int i=0;i<=count;i++){float offset=-Math.Max(sx,sz)/2+.28f+i*(Math.Max(sx,sz)-.56f)/Math.Max(1,count);var q=p+(alongX?new Vector3(offset,0,0):new Vector3(0,0,offset));Pole(q+Vector3.Up*.8f,.28f,2.2f,i%3==0?"material:wall-dark":visual.Material);}
        Box(alongX?new(sx,.22f,.3f):new(.3f,.22f,sz),p+Vector3.Up*2,"material:wall-beam");
    }
    void BuildRail(SceneryObstacle o,SceneryVisual visual)
    {
        var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float length=(o.Max.Y-o.Min.Y)/1000f;
        for(int i=0;i<=5;i++)Box(new(.5f,1.4f,.3f),p+new Vector3(0,.7f,-length/2+.15f+i*(length-.3f)/5),"material:rail-post");Box(new(.5f,.2f,length),p+Vector3.Up*1.3f,visual.Material);Box(new(.35f,.15f,length),p+Vector3.Up*.65f,"material:rail-mid");
    }
    void BuildBridge()
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=0;i<20;i++){long y1=50000+i*500,y2=y1+500;var a=GroundPoint(new(0,y1));var b=GroundPoint(new(10000,y1));var c=GroundPoint(new(10000,y2));var d=GroundPoint(new(0,y2));st.SetColor(assets.Color(i%3==0?Visual("surface/bridge").Material:i%3==1?"material:deck-1":"material:deck-2"));foreach(var p in new[]{a,b,c,a,c,d}){st.SetUV(new((p.X*1000+center.X)/assets.Tile(Visual("surface/bridge").Material),(-p.Z*1000+center.Y)/assets.Tile(Visual("surface/bridge").Material)));st.AddVertex(p);}}
        st.GenerateNormals();AddMesh(st.Commit(),Mat(Visual("surface/bridge").Material,true),Vector3.Zero);var centerPoint=GroundPoint(new(5000,55000));Box(new(10,.4f,10),centerPoint+new Vector3(0,-.201f,0),"material:deck-under");
        // Deck top is exactly the Core walking plane; water is drawn only on impassable adjacent cells.
    }
}
