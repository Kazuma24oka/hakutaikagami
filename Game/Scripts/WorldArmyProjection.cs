using Godot;
using HakutaiKagami.Core.Contracts;

// Shared by live model placement and the CLI-only Node3D transform check.
public static class WorldArmyProjection
{
    public static Vector3 Position(FieldPoint p,FieldPoint center,float heightOffset=0)=>
        new((p.X-center.X)/1000f,ContinuousWorld.Height(p)/1000f+heightOffset,-(p.Y-center.Y)/1000f);
    public static void Apply(Node3D actor,FieldPoint p,FieldPoint center,float heightOffset=0)=>actor.Position=Position(p,center,heightOffset);
}
