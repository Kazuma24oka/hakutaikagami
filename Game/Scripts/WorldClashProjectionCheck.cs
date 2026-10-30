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
            int pairs=int.Parse(System.Environment.GetEnvironmentVariable("HAKUTAI_PROJECTION_EXPECTED_PAIRS")??"3");if(pairs is not(3 or 5 or 7 or 9 or 11 or 13 or 15 or 18 or 21 or 23 or 28 or 32 or 37 or 40 or 45 or 50 or 56 or 62 or 65 or 75 or 80 or 90 or 95 or 97 or 106 or 114 or 8 or 122 or 126 or 130 or 140 or 146 or 150 or 162 or 168 or 172 or 184 or 194 or 206 or 218 or 230 or 242))throw new InvalidDataException("Unknown projection scope");
            var files=Directory.GetFiles(dir,"*.json").Where(p=>p.EndsWith(".before.json",StringComparison.Ordinal)||p.EndsWith(".restored.json",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();if(files.Length!=pairs*2)throw new InvalidDataException("Missing actual generation-recovery pairs");
            var results=new Dictionary<string,string>();int nodes=0;
            foreach(var path in files){
                var w=ContractJson.Read<ContractWorld>(File.ReadAllBytes(path));ContractValidation.Validate(w);
                var center=FieldWorld.FromWorld(RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds));
                var positions=new List<(string Id,FieldPoint Position,int Soldiers,bool Attack)>();
                if(PrivateInformation.Enabled(w)&&!PersonalCompanions.Enabled(w)&&!TruceNegotiations.Enabled(w)){foreach(var person in new[]{w.PlayerControl}.Concat(w.PrivateInformation!.Quotes.Select(q=>q.Speaker)).Distinct())positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));}
                if(TruceNegotiations.Enabled(w)&&!PersonalCompanions.Enabled(w)){foreach(var person in new[]{w.PlayerControl}.Concat(w.Truces!.Papers.SelectMany(p=>new[]{p.Author,p.Recipient})).Distinct())positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));}
                if(PersonalCompanions.Enabled(w)){foreach(var person in new[]{w.PlayerControl}.Concat(w.Companions!.Orders.Where(q=>q.Input.Action==CompanionAction.Agree).Select(q=>q.Input.Person)).Concat(w.PrivateInformation?.Quotes.Select(q=>q.Speaker)??Array.Empty<EntityId>()).Distinct()){positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));}}
                if(CommunityEconomy.Enabled(w)&&positions.All(p=>p.Id!=w.PlayerControl.Value))positions.Add((w.PlayerControl.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,w.ElapsedSeconds)),0,false));
                if(w.TruceConfirmations is {} confirmationPapers)foreach(var person in confirmationPapers.Papers.SelectMany(p=>new[]{p.Sender,p.Recipient}).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                foreach(var person in (w.Treaties?.Supply?.Entries??Array.Empty<TreatySupplyEntry>()).Select(e=>e.Actor).Concat((w.Treaties?.Supply?.Entries??Array.Empty<TreatySupplyEntry>()).Where(e=>e.Receiver!=null).Select(e=>e.Receiver!.Value)).Concat((w.Treaties?.Supply?.Papers??Array.Empty<TreatySupplyPaper>()).SelectMany(p=>new[]{p.Sender,p.Recipient})).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                foreach(var e in w.Treaties?.Supply?.Entries.Where(e=>e.Input.Action==TreatySupplyAction.Arrange)??Enumerable.Empty<TreatySupplyEntry>()){long cargo=TreatySupply.Cargo(w,e.Root);if(cargo>0)positions.Add(("treaty-cargo:"+e.Root.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,e.Actor,w.ElapsedSeconds)),0,false));}
                foreach(var person in (w.Treaties?.Papers??Array.Empty<TreatyPaper>()).SelectMany(p=>new[]{p.Author,p.Recipient}).Concat((w.Treaties?.Operations??Array.Empty<TreatyOperationPaper>()).SelectMany(p=>new[]{p.Author,p.Recipient})).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                foreach(var person in NewGameMilitary.LedgerStates(w).SelectMany(s=>s.RemoteSuccession?.Papers??Array.Empty<RemoteMilitaryPaper>()).SelectMany(p=>new[]{p.Sender,p.Recipient}).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                foreach(var person in NewGameMilitary.LedgerStates(w).SelectMany(s=>s.ActiveSuccession?.Entries??Array.Empty<ActiveMilitaryAgreement>()).SelectMany(e=>new[]{e.Previous,e.Principal,e.Agent}).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                if(NpcFreight.Enabled(w))foreach(var person in HouseLogistics.FreightJobs(w,w.ElapsedSeconds,w.CommitSequence+1).Select(j=>j.Terms.Carrier).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                if(FieldConvoy.Enabled(w))foreach(var person in FieldConvoy.Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Select(j=>j.Terms.Carrier).Distinct().Where(id=>positions.All(p=>p.Id!=id.Value)))positions.Add((person.Value,FieldWorld.FromWorld(RegionalCampaign.ActorPointAt(w,person,w.ElapsedSeconds)),0,false));
                foreach(var u in w.Units.Where(u=>NewGameMilitary.ManagedUnitPosition(w,u.Id,w.ElapsedSeconds)!=null)){
                    positions.Add((u.Id.Value,NewGameMilitary.ManagedUnitPosition(w,u.Id,w.ElapsedSeconds)!,(int)u.Soldiers,WorldClashes.AttacksNow(w,u.Id)));
                }
                foreach(var sight in NpcArmies.Sights(w,w.PlayerControl))positions.Add((sight.Army.Value,FieldWorld.FromWorld(sight.Position),sight.Soldiers,sight.Attacking));
                if(CastleHandover.ViewForProjection(w) is {} castleView)foreach(var card in castleView.Castles.Where(c=>c.Local&&CastleHandover.Jobs(w).Single(j=>j.Terms.Id==c.Id).Gate!=null)){var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==card.Site).Position);positions.Add(("castle-handover:"+card.Site.Value,point,0,false));}
                if(CastleAdministration.ViewForProjection(w) is {} administration)foreach(var c in administration.Appointments.Where(c=>c.Local)){var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==c.Site).Position);positions.Add(("castellan:"+c.Id.Value,point,0,false));}
                if(HouseSuccession.ViewForProjection(w) is {} succession)foreach(var c in succession.Changes.Where(c=>c.Local)){var point=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==c.Home).Position);positions.Add(("house-succession:"+c.Id.Value,point,0,false));}
                var rows=new List<object>();foreach(var row in positions.OrderBy(p=>p.Id,StringComparer.Ordinal)){
                    var actor=new Node3D();AddChild(actor);WorldArmyProjection.Apply(actor,row.Position,center);var p=actor.GlobalPosition;
                    if(Math.Abs(p.X*1000-(row.Position.X-center.X))>1||Math.Abs(-p.Z*1000-(row.Position.Y-center.Y))>1||Math.Abs(p.Y*1000-ContinuousWorld.Height(row.Position))>1)throw new InvalidDataException("Actual Node3D position differs from Core millimeters");
                    rows.Add(new{row.Id,Position=V(p),row.Soldiers,row.Attack});RemoveChild(actor);actor.Free();nodes++;
                }
                results[Path.GetFileName(path)]=JsonSerializer.Serialize(new{w.ElapsedSeconds,Actors=rows});
            }
            foreach(var name in results.Keys.Where(p=>p.EndsWith(".before.json",StringComparison.Ordinal)))if(results[name]!=results[name.Replace(".before.json",".restored.json",StringComparison.Ordinal)])throw new InvalidDataException("Restored Node3D presentation differs");
            File.WriteAllText(Path.Combine(dir,"projection-result.json"),JsonSerializer.Serialize(new{Status="PASSED",Mode="Godot headless Node3D data only",Files=files.Length,Pairs=pairs,Nodes=nodes,Results=results,Screens="NOT_CAPTURED",Gui="NOT_USED"},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("WORLD_CLASH_HEADLESS_PROJECTION_PASS files="+files.Length+" pairs="+pairs+" nodes="+nodes);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
