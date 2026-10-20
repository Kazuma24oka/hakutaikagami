using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record TrafficStop(EntityId Task,EntityId Actor,string Edge,string Node,long At,string Reason);
// Local entry/departure checks; scheduled truth is never used by personal planning.
public static class TrafficWorld
{
    public const string Ruleset="national-traffic-conflicts-v1";
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{TravelControl.Version,"local-road-entry-sea-predeparture-check-v1","stop-at-site-explicit-restart-v1","courier-service-failure-static-return-address-v1"});
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||FieldWorld.Enabled(w);
    public static ContractWorld Create(RoadCondition[]? conditions=null){var w=TravelControl.Create();w.WorldId=new("world:national-traffic-conflicts-v1");w.Definitions=w.Definitions with{Id=new("scenario:national-traffic-conflicts-v1"),Ruleset=Ruleset,Version=Version};w.TrafficStops=[];w.Couriers=w.Couriers! with{Conditions=conditions??[]};ContractValidation.Validate(w);return w;}
    public static string? StoppedNode(ContractWorld w,EntityId task)=>w.TrafficStops?.SingleOrDefault(s=>s.Task==task)?.Node;
    static void Reschedule(ContractWorld w,BranchJourney j){w.Branches=w.Branches! with{Journeys=w.Branches.Journeys.Select(x=>x.Task==j.Task?j:x).ToArray()};w.Actions=w.Actions.Select(a=>a.Task==j.Task?a with{Until=j.Legs[^1].EndAt}:a).ToArray();w.PendingEvents=w.PendingEvents.Select(e=>e.Task==j.Task&&e.Kind==EventKind.Finish?e with{At=j.Legs[^1].EndAt}:e).ToArray();}
    static bool Halt(ContractWorld w,TaskState task,JourneyLeg leg,string reason){w.TrafficStops=[..w.TrafficStops!,new(task.Id,task.Executor,leg.Edge,leg.From,w.ElapsedSeconds,reason)];WorldSession.Stop(w,task,reason);w.Locations=w.Locations.Select(l=>l.Actor==task.Executor?new LocationState(l.Actor,LocationKind.AtSite,RegionalCampaign.Site(leg.From),null,0,0,null,null,null,null,w.CommitSequence):l).ToArray();return true;}
    public static bool Advance(ContractWorld w,TaskState task){
        for(int guard=0;guard<=BranchRoutes.GraphEdges(w).Length;guard++){
            var j=w.Branches!.Journeys.Single(j=>j.Task==task.Id);var ix=Array.FindIndex(j.Legs,l=>w.ElapsedSeconds<l.EndAt);if(ix<0)return BranchRoutes.AdvanceOrdinary(w,task);var l=j.Legs[ix];bool sea=NationalRoutes.Sea(l.Edge);var departure=TravelWorld.Departure(w,l.Edge,l.StartAt);
            bool entry=sea?w.ElapsedSeconds>=l.StartAt&&w.ElapsedSeconds<=departure:l.StartAt==w.ElapsedSeconds;
            if(!entry||!w.Branches.BlockedEdges.Contains(l.Edge))return BranchRoutes.AdvanceOrdinary(w,task);
            RegionalCampaign.InspectRoad(w,task.Executor,l.Edge);
            if(sea)return Halt(w,task,l,"service-suspended");
            string[] path;try{path=BranchRoutes.Plan(l.From,TravelControl.Goal(w,j),BranchRoutes.KnownBlocked(w,task.Executor),w:w,departureAt:w.ElapsedSeconds);}catch(InvalidDataException){return Halt(w,task,l,"road-unavailable");}
            Reschedule(w,j with{Legs=[..j.Legs.Take(ix),..BranchRoutes.Schedule(l.From,path,w.ElapsedSeconds,w)]});
        }throw new InvalidDataException("Traffic routing did not settle");
    }
    // Couriers already afloat continue. A cancelled service before departure produces a physical failure report.
    public static void CheckCouriers(ContractWorld w){foreach(var c in w.Couriers!.Missions.ToArray()){
        var a=w.Messaging.Attempts.Single(a=>a.Id==c.Attempt);if(a.Status!=AttemptStatus.InTransit||c.Obstructed)continue;
        int ix=Array.FindIndex(c.Legs,l=>NationalRoutes.Sea(l.Edge)&&l.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge).LengthMm&&l.StartAt<=w.ElapsedSeconds&&w.ElapsedSeconds<=TravelWorld.Departure(w,l.Edge,l.StartAt,l.LengthMm==BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge).LengthMm)&&w.Branches!.BlockedEdges.Contains(l.Edge));if(ix<0)continue;var leg=c.Legs[ix];
        var next=c with{Legs=c.Legs.Take(ix).ToArray(),Obstructed=true,KnownBlocked=c.KnownBlocked.Append(leg.Edge).Distinct().Order().ToArray(),ReadyAt=w.ElapsedSeconds+1};w.Couriers=w.Couriers with{Missions=w.Couriers.Missions.Select(x=>x.Attempt==c.Attempt?next:x).ToArray()};w.Messaging.Attempts=w.Messaging.Attempts.Select(x=>x.Id==a.Id?x with{DueAt=next.ReadyAt,FailOnArrival=true}:x).ToArray();
    }}
    public static string FailureAddress(ContractWorld w,string attempt){var origin=w.Couriers!.Missions.Single(c=>c.Attempt==attempt).Origin;return BranchRoutes.GraphNodes(w).SingleOrDefault(n=>n.Position==origin)?.Id??"castle";}
    public static void Validate(ContractWorld w){var stops=w.TrafficStops??throw new InvalidDataException("Missing traffic-stop module");if(stops.Select(s=>s.Task).Distinct().Count()!=stops.Length)throw new InvalidDataException("Duplicate traffic stop");foreach(var s in stops){var t=w.Tasks.Single(t=>t.Id==s.Task);var j=w.Branches!.Journeys.Single(j=>j.Task==s.Task);var l=j.Legs.SingleOrDefault(l=>l.Edge==s.Edge&&l.From==s.Node&&l.StartAt<=s.At&&s.At<l.EndAt)??throw new InvalidDataException("Stop lacks physical edge");if(t.Executor!=s.Actor||t.Execution!=ExecutionPhase.Stopped||t.FinishedAt!=s.At||t.InternalReason!=s.Reason||s.At>w.ElapsedSeconds||!RegionalCampaign.BlockedAt(w,s.Edge,s.At)||!w.Couriers!.Sightings.Any(x=>x.Actor==s.Actor&&x.Edge==s.Edge&&x.At==s.At&&x.Blocked)||s.Reason!=(NationalRoutes.Sea(s.Edge)?"service-suspended":"road-unavailable")||NationalRoutes.Sea(s.Edge)&&s.At>TravelWorld.Departure(w,l.Edge,l.StartAt)||!NationalRoutes.Sea(s.Edge)&&s.At!=l.StartAt)throw new InvalidDataException("Traffic stop lacks local closure or valid boundary");}}
}
