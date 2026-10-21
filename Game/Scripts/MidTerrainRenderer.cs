using Godot;
using System;
using HakutaiKagami.Core.Contracts;

// Visual LOD only. Core remains the authority for movement and detailed height.
public partial class MidTerrainRenderer:Node3D
{
    MeshInstance3D mesh=new();long ox,oy,step;bool built;
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
        if(!built||x!=ox||y!=oy||next!=step){
            ox=x;oy=y;step=next;var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
            void V(int i,int j){var p=new FieldPoint(ox+(i-16)*step,oy+(j-16)*step);st.AddVertex(new((p.X-ox)/1000f,ContinuousWorld.Height(p)/1000f,-(p.Y-oy)/1000f));}
            for(int i=0;i<32;i++)for(int j=0;j<32;j++){
                var p=new FieldPoint(ox+(i-16)*step+step/2,oy+(j-16)*step+step/2);
                string kind=ContinuousWorld.KindAt(p);st.SetColor(new Color(kind switch{"water"=>"467183","forest"=>"425b3b","road"=>"ac9764","cliff"=>"777568","slope"=>"74805b",_=>"8a985e"}));
                V(i,j);V(i+1,j);V(i+1,j+1);V(i,j);V(i+1,j+1);V(i,j+1);
            }
            st.GenerateNormals();mesh.Mesh=st.Commit();mesh.MaterialOverride=new StandardMaterial3D{VertexColorUseAsAlbedo=true,Roughness=1,CullMode=BaseMaterial3D.CullModeEnum.Disabled};built=true;Builds++;
        }
        Position=new((ox-center.X)/1000f,0,-(oy-center.Y)/1000f);
    }
}
