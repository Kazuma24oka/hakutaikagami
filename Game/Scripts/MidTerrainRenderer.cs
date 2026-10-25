using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Contracts;

// Visual LOD only. Core remains the authority for movement and detailed height.
public partial class MidTerrainRenderer:Node3D
{
    readonly SceneryVisualAssets assets=new();string skin="skin:baseline",builtSkin="";
    public void SetSkin(string value){if(!assets.Catalog.Skins.Any(s=>s.Id==value))throw new ArgumentException("Unknown skin");skin=value;}
    public Color SurfaceColor(string kind){var v=SceneryAppearance.Resolve(assets.Catalog,skin,SceneryAppearance.Appearance("surface/"+kind));return assets.Color(v.Material);}
    MeshInstance3D mesh=new();long ox,oy,step;bool built;
    readonly bool profiling=OS.GetCmdlineUserArgs().Contains("--terrain-perf");
    public double VertexHeightMs{get;private set;}public double KindAtMs{get;private set;}public double MeshCommitMs{get;private set;}public double BuildMs{get;private set;}
    public int Builds{get;private set;}
    public int Triangles=>Visible&&built?2048:0;
    public long Step=>step;
    public override void _Ready(){AddChild(mesh);}
    public void Update(FieldPoint center,float span)
    {
        Visible=span>240&&span<=30000;
        if(!Visible)return;
        long next=10000;while(next<span*1000*4/32)next*=2;
        long x=(long)Math.Floor(center.X/(double)next)*next,y=(long)Math.Floor(center.Y/(double)next)*next;
        if(!built||x!=ox||y!=oy||next!=step||builtSkin!=skin){
            long begin=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;KindAtMs=0;ox=x;oy=y;step=next;using var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
            var vertices=new Vector3[33,33];for(int a=0;a<=32;a++)for(int b=0;b<=32;b++){var p=new FieldPoint(ox+(a-16)*step,oy+(b-16)*step);vertices[a,b]=new((p.X-ox)/1000f,ContinuousWorld.Height(p)/1000f,-(p.Y-oy)/1000f);}
            if(profiling)VertexHeightMs=System.Diagnostics.Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
            void V(int i,int j)=>st.AddVertex(vertices[i,j]);
            for(int i=0;i<32;i++)for(int j=0;j<32;j++){
                var p=new FieldPoint(ox+(i-16)*step+step/2,oy+(j-16)*step+step/2);
                long kindStart=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;string kind=ContinuousWorld.KindAt(p);if(profiling)KindAtMs+=System.Diagnostics.Stopwatch.GetElapsedTime(kindStart).TotalMilliseconds;st.SetColor(SurfaceColor(kind));
                V(i,j);V(i+1,j);V(i+1,j+1);V(i,j);V(i+1,j+1);V(i,j+1);
            }
            long commitStart=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;st.GenerateNormals();var previous=mesh.Mesh;mesh.Mesh=st.Commit();previous?.Dispose();mesh.MaterialOverride??=new StandardMaterial3D{VertexColorUseAsAlbedo=true,Roughness=1,CullMode=BaseMaterial3D.CullModeEnum.Disabled};built=true;builtSkin=skin;Builds++;if(profiling){MeshCommitMs=System.Diagnostics.Stopwatch.GetElapsedTime(commitStart).TotalMilliseconds;BuildMs=System.Diagnostics.Stopwatch.GetElapsedTime(begin).TotalMilliseconds;}
        }
        Position=new((ox-center.X)/1000f,0,-(oy-center.Y)/1000f);
    }
}
