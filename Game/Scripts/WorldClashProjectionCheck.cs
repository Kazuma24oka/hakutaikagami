using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Contracts;

// No Control nodes, input events, screen capture, UI callbacks, model assets, or display server.
// This exercises actual Node3D transforms under Godot --headless, not visual appearance.
public partial class WorldClashProjectionCheck : Node
{
    static float[] V(Vector3 p)=>[p.X,p.Y,p.Z];
    public override void _Ready()
    {
        try{
            if(DisplayServer.GetName()!="headless")throw new InvalidDataException("This check is headless-only");
            var dir=System.Environment.GetEnvironmentVariable("HAKUTAI_WORLD_CLASH_QA_DIR")??throw new InvalidDataException("Missing QA input directory");
            var files=Directory.GetFiles(dir,"clash-*.json").Order(StringComparer.Ordinal).ToArray();if(files.Length!=6)throw new InvalidDataException("Expected three actual generation-recovery pairs");
            var results=new Dictionary<string,string>();int nodes=0;
            foreach(var path in files){
                var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(path));ContractValidation.Validate(w);
                var center=FieldWorld.FromWorld(RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds));
                var positions=new List<(string Id,FieldPoint Position,int Soldiers,bool Attack)>();
                foreach(var u in w.Units.Where(u=>u.Organization==w.NewMilitary!.Terms.Owner)){
                    int i=Array.IndexOf(NewGameMilitary.UnitIdsFor(w),u.Id);positions.Add((u.Id.Value,NewGameMilitary.Position(w,i,w.ElapsedSeconds),(int)u.Soldiers,WorldClashes.AttacksNow(w,u.Id)));
                }
                foreach(var sight in NpcArmies.Sights(w,w.PlayerControl))positions.Add((sight.Army.Value,FieldWorld.FromWorld(sight.Position),sight.Soldiers,sight.Attacking));
                var rows=new List<object>();foreach(var row in positions.OrderBy(p=>p.Id,StringComparer.Ordinal)){
                    var actor=new Node3D();AddChild(actor);WorldArmyProjection.Apply(actor,row.Position,center);var p=actor.GlobalPosition;
                    if(Math.Abs(p.X*1000-(row.Position.X-center.X))>1||Math.Abs(-p.Z*1000-(row.Position.Y-center.Y))>1||Math.Abs(p.Y*1000-ContinuousWorld.Height(row.Position))>1)throw new InvalidDataException("Actual Node3D position differs from Core millimeters");
                    rows.Add(new{row.Id,Position=V(p),row.Soldiers,row.Attack});RemoveChild(actor);actor.Free();nodes++;
                }
                results[Path.GetFileName(path)]=JsonSerializer.Serialize(new{w.ElapsedSeconds,Actors=rows});
            }
            foreach(int phase in new[]{1,2,4})if(results["clash-"+phase+".before.json"]!=results["clash-"+phase+".restored.json"])throw new InvalidDataException("Restored Node3D presentation differs");
            File.WriteAllText(Path.Combine(dir,"projection-result.json"),JsonSerializer.Serialize(new{Status="PASSED",Mode="Godot headless Node3D data only",Files=files.Length,Pairs=3,Nodes=nodes,Results=results,Screens="NOT_CAPTURED",Gui="NOT_USED"},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("WORLD_CLASH_HEADLESS_PROJECTION_PASS files=6 pairs=3 nodes="+nodes);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
