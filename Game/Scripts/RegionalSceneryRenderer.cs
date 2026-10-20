using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using HakutaiKagami.Core.Contracts;

// Render geometry comes from the same immutable surface/footprint catalog used by Core.
public partial class RegionalSceneryRenderer : Node3D
{
    public readonly List<(Vector3 A,Vector3 B,Vector3 C)> PickTriangles=new();
    public int TreeCount{get;private set;}public int StructureCount{get;private set;}
    public string CatalogHash=>RegionalScenery.Version;
    FieldPoint? lastCenter;
    FieldPoint center=new(0,0);
    public Vector3 GroundPoint(FieldPoint p)=>new((p.X-center.X)/1000f,RegionalScenery.Height(p)/1000f,-(p.Y-center.Y)/1000f);
    Vector3 Vertex(SceneryVertex p)=>new((p.Point.X-center.X)/1000f,p.HeightMm/1000f,-(p.Point.Y-center.Y)/1000f);
    static StandardMaterial3D Mat(string color,bool vertex=false)=>new(){AlbedoColor=new Color(color),VertexColorUseAsAlbedo=vertex,Roughness=1,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
    void AddMesh(Mesh mesh,Material material,Vector3 position,Vector3? rotation=null){var m=new MeshInstance3D{Mesh=mesh,MaterialOverride=material,Position=position};if(rotation is {} r)m.Rotation=r;AddChild(m);}
    void Box(Vector3 size,Vector3 p,string color)=>AddMesh(new BoxMesh{Size=size},Mat(color),p);
    void Pole(Vector3 p,float radius,float height,string color)=>AddMesh(new CylinderMesh{TopRadius=radius*.78f,BottomRadius=radius,Height=height,RadialSegments=7,Rings=1},Mat(color),p+Vector3.Up*height/2);
    static Color Tint(Color color,float amount)=>new(color.R*amount,color.G*amount,color.B*amount);
    Color GroundColor(FieldPoint p,string kind)
    {
        float h=RegionalScenery.Height(p)/1000f;
        var baseColor=new Color(kind switch{"road"=>"a58a5b","forest"=>"617144","water"=>"467e91","cliff"=>"7d7b6a","slope"=>"849064",_=>"81916b"});
        if(kind is "grass" or "slope"&&h>16)baseColor=baseColor.Lerp(new Color("8e9079"),Mathf.Clamp((h-16)/30,0,.75f));
        long ix=(long)Math.Floor(p.X/1250.0),iy=(long)Math.Floor(p.Y/1250.0);var n=(int)((ix*73856093^iy*19349663)&15);return Tint(baseColor,.93f+n*.008f);
    }
    public void Rebuild(FieldPoint newCenter)
    {
        if(lastCenter==newCenter)return;lastCenter=newCenter;center=newCenter;
        foreach(Node n in GetChildren()){RemoveChild(n);n.Free();}PickTriangles.Clear();TreeCount=0;StructureCount=0;
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        var water=new SurfaceTool();water.Begin(Mesh.PrimitiveType.Triangles);
        foreach(var t in RegionalScenery.Surfaces())
        {
            var va=Vertex(t.A);var vb=Vertex(t.B);var vc=Vertex(t.C);PickTriangles.Add((va,vb,vc));
            bool bridge=t.Kind=="road"&&t.A.Point.X>=0&&t.C.Point.X<=10000&&t.A.Point.Y>=50000&&t.C.Point.Y<=60000;
            if(bridge)continue;
            var target=t.Kind=="water"?water:st;
            // Subdivision changes color only; vertices stay on the exact Core triangle.
            const int n=4;
            Vector3 At(int i,int j)=>va+(vb-va)*(i/(float)n)+(vc-va)*(j/(float)n);
            void Add(Vector3 v){var p=new FieldPoint((long)Math.Round(v.X*1000)+center.X,(long)Math.Round(-v.Z*1000)+center.Y);target.SetColor(GroundColor(p,t.Kind));target.AddVertex(v);}
            for(int i=0;i<n;i++)for(int j=0;j<n-i;j++){Add(At(i,j));Add(At(i+1,j));Add(At(i,j+1));if(i+j<n-1){Add(At(i+1,j));Add(At(i+1,j+1));Add(At(i,j+1));}}
        }
        st.GenerateNormals();AddMesh(st.Commit(),Mat("ffffff",true),Vector3.Zero);
        water.GenerateNormals();var wm=Mat("ffffff",true);wm.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;AddMesh(water.Commit(),wm,Vector3.Zero);
        BuildSides();BuildBridge();
        foreach(var o in RegionalScenery.Obstacles)
        {
            if(o.Kind=="tree")BuildTree(o);else if(o.Kind=="keep")BuildKeep(o);else if(o.Kind=="wall")BuildWall(o);else if(o.Kind=="rail")BuildRail(o);
        }
        // Boulders stay entirely on already impassable cliff cells.
        for(int x=-6;x<10;x++)for(int y=-6;y<10;y++)if(RegionalScenery.Kind(x,y)=="cliff")
        {var p=GroundPoint(new(x*10000L+5000,y*10000L+5000));AddMesh(new SphereMesh{Radius=2.3f,Height=3.2f,RadialSegments=6,Rings=3},Mat((x+y)%2==0?"747969":"969580"),p+new Vector3(0,1.1f,0),new(.15f,x*.4f,.12f));}
    }
    void BuildSides()
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        void Edge(FieldPoint a,FieldPoint b){var u=GroundPoint(a);var v=GroundPoint(b);var lowU=new Vector3(u.X,-8,u.Z);var lowV=new Vector3(v.X,-8,v.Z);foreach(var p in new[]{u,v,lowV,u,lowV,lowU})st.AddVertex(p);}
        for(int i=-6;i<10;i++){Edge(new(i*10000,-60000),new((i+1)*10000,-60000));Edge(new(i*10000,100000),new((i+1)*10000,100000));Edge(new(-60000,i*10000),new(-60000,(i+1)*10000));Edge(new(100000,i*10000),new(100000,(i+1)*10000));}
        st.GenerateNormals();AddMesh(st.Commit(),Mat("685e49"),Vector3.Zero);
    }
    void BuildTree(SceneryObstacle o)
    {
        TreeCount++;var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float height=o.HeightMm/1000f;Pole(p,.35f,height*.74f,"665642");
        bool pine=((o.Min.X/1000+o.Min.Y/1000)&1)==0;
        if(pine){for(int i=0;i<3;i++)AddMesh(new CylinderMesh{TopRadius=.08f,BottomRadius=2.5f-i*.45f,Height=4,RadialSegments=7,Rings=1},Mat(i==0?"3f6144":i==1?"4e7350":"61805a"),p+Vector3.Up*(height-4+i*1.35f));}
        else{AddMesh(new SphereMesh{Radius=2.8f,Height=5,RadialSegments=7,Rings=4},Mat("557447"),p+new Vector3(.4f,height-1.4f,0));AddMesh(new SphereMesh{Radius=2,Height=3.5f,RadialSegments=7,Rings=4},Mat("718a54"),p+new Vector3(-.8f,height,.3f));}
    }
    void Roof(Vector3 p,float width,float depth,float rise,string color)
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);var a=p+new Vector3(-width/2,0,-depth/2);var b=p+new Vector3(width/2,0,-depth/2);var c=p+new Vector3(width/2,0,depth/2);var d=p+new Vector3(-width/2,0,depth/2);var l=p+new Vector3(-width*.28f,rise,0);var r=p+new Vector3(width*.28f,rise,0);var faces=new[]{a,b,r,a,r,l,b,c,r,c,d,l,c,l,r,d,a,l};for(int i=0;i<faces.Length;i+=3){st.AddVertex(faces[i+2]);st.AddVertex(faces[i+1]);st.AddVertex(faces[i]);}st.GenerateNormals();AddMesh(st.Commit(),Mat(color),Vector3.Zero);Box(new(width*.58f,.18f,.18f),p+Vector3.Up*rise,"50574f");
    }
    void BuildKeep(SceneryObstacle o)
    {
        StructureCount++;var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float width=(o.Max.X-o.Min.X)/1000f,depth=(o.Max.Y-o.Min.Y)/1000f;
        Box(new(width,1.1f,depth),p+Vector3.Up*.55f,"8b8573");Box(new(width-.5f,3,depth-.5f),p+Vector3.Up*2.6f,"846a4b");
        for(int side=-1;side<=1;side+=2)for(int i=-2;i<=2;i++){Box(new(.15f,3,.18f),p+new Vector3(i*1.5f,2.6f,side*(depth-.4f)/2),"473f32");Box(new(.9f,.95f,.1f),p+new Vector3(i*1.5f,3,side*(depth-.3f)/2),"343d38");}
        Roof(p+Vector3.Up*4.2f,width+1.6f,depth+1.6f,1.7f,"3e4847");
        Box(new(width*.65f,2.5f,depth*.62f),p+Vector3.Up*6.2f,"d4cbb1");
        for(int side=-1;side<=1;side+=2){Box(new(width*.65f,.2f,.16f),p+new Vector3(0,6.8f,side*depth*.32f),"65523b");for(int i=-1;i<=1;i++)Box(new(.65f,.8f,.12f),p+new Vector3(i*1.6f,6.4f,side*depth*.32f),"3d433b");}
        Roof(p+Vector3.Up*7.5f,width*.8f,depth*.8f,1.6f,"384344");
        // The entire foundation footprint is the exact blocked keep rectangle.
    }
    void BuildWall(SceneryObstacle o)
    {
        StructureCount++;var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float sx=(o.Max.X-o.Min.X)/1000f,sz=(o.Max.Y-o.Min.Y)/1000f;Box(new(sx,1,sz),p+Vector3.Up*.5f,"998866");
        bool alongX=sx>sz;int count=(int)Math.Floor(Math.Max(sx,sz)/.65f);for(int i=0;i<=count;i++){float offset=-Math.Max(sx,sz)/2+.28f+i*(Math.Max(sx,sz)-.56f)/Math.Max(1,count);var q=p+(alongX?new Vector3(offset,0,0):new Vector3(0,0,offset));Pole(q+Vector3.Up*.8f,.28f,2.2f,i%3==0?"715d43":"887151");}
        Box(alongX?new(sx,.22f,.3f):new(.3f,.22f,sz),p+Vector3.Up*2,"594c38");
    }
    void BuildRail(SceneryObstacle o)
    {
        StructureCount++;var p=GroundPoint(new((o.Min.X+o.Max.X)/2,(o.Min.Y+o.Max.Y)/2));float length=(o.Max.Y-o.Min.Y)/1000f;
        for(int i=0;i<=5;i++)Box(new(.5f,1.4f,.3f),p+new Vector3(0,.7f,-length/2+.15f+i*(length-.3f)/5),"766247");Box(new(.5f,.2f,length),p+Vector3.Up*1.3f,"a28b65");Box(new(.35f,.15f,length),p+Vector3.Up*.65f,"8b7654");
    }
    void BuildBridge()
    {
        var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=0;i<20;i++){long y1=50000+i*500,y2=y1+500;var a=GroundPoint(new(0,y1));var b=GroundPoint(new(10000,y1));var c=GroundPoint(new(10000,y2));var d=GroundPoint(new(0,y2));st.SetColor(new Color(i%3==0?"a38b62":i%3==1?"9b8059":"ad956c"));foreach(var p in new[]{a,b,c,a,c,d})st.AddVertex(p);}
        st.GenerateNormals();AddMesh(st.Commit(),Mat("ffffff",true),Vector3.Zero);var centerPoint=GroundPoint(new(5000,55000));Box(new(10,.4f,10),centerPoint+new Vector3(0,-.201f,0),"695a43");
        // Deck top is exactly the Core walking plane; water is drawn only on impassable adjacent cells.
    }
}
