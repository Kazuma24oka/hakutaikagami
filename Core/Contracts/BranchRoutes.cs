using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record BranchNode(string Id,RoutePoint Position);
public sealed record BranchEdge(string Id,string From,string To,long LengthMm,int FootSpeed,int CourierSpeed);
public sealed record BranchCatalog(BranchNode[] Nodes,BranchEdge[] Edges,string Algorithm,int HandlingSeconds,int WorkSeconds);
public sealed record JourneyLeg(string Edge,string From,string To,long StartAt,long EndAt);
public sealed record BranchJourney(EntityId Task,EntityId Actor,JourneyLeg[] Legs);
public sealed record BranchState(string[] BlockedEdges,BranchJourney[] Journeys);
public sealed record BranchView(ActorView Knowledge,RoutePoint Position,RoutePoint Heading,long DistanceMm,
    bool Walking,long? ArrivalAt,string[] PlannedEdges,string[] KnownBlockedEdges,RoutePoint NextPosition,long StepDistanceMm);

// Synthetic graph V1. Truth is consulted only at physical edge entry; planning takes personal evidence.
public static class BranchRoutes
{
    public const string Ruleset="common-branch-fixture-v1";
    public static BranchNode[] Nodes=>[new("castle",new(0,0)),new("fork",new(40000,0)),new("east",new(80000,0)),new("north",new(80000,80000)),new("village",new(40000,80000))];
    public static BranchEdge[] Edges=>[new("road","castle","fork",40000,2000,4000),new("pass","fork","village",80000,1000,2000),
        new("bypass-east","fork","east",40000,2000,4000),new("bypass-north","east","north",80000,1000,2000),new("bypass-west","north","village",40000,2000,4000)];
    public static string Version {get;}=ContractJson.CanonicalHash(new BranchCatalog(Nodes,Edges,"known-edges-dijkstra-time-ordinal-tie-v1",1,4));
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||MovementControl.Enabled(w)||RegionalBranch.Enabled(w);
    public static BranchNode[] GraphNodes(ContractWorld? w=null)=>w!=null&&(NationalRoutes.Enabled(w)||TravelWorld.Enabled(w))?NationalRoutes.Nodes:w!=null&&HistoricalRoads.Enabled(w)?HistoricalRoads.Nodes:w!=null&&RiverRoads.Enabled(w)?RiverRoads.Nodes:w!=null&&RegionalCampaign.Enabled(w)?RegionalCampaign.Nodes:w!=null&&RegionalBranch.Enabled(w)?RegionalBranch.Nodes:Nodes;
    public static BranchEdge[] GraphEdges(ContractWorld? w=null)=>w!=null&&(NationalRoutes.Enabled(w)||TravelWorld.Enabled(w))?NationalRoutes.Edges:w!=null&&HistoricalRoads.Enabled(w)?HistoricalRoads.Edges:w!=null&&RiverRoads.Enabled(w)?RiverRoads.Edges:w!=null&&RegionalCampaign.Enabled(w)?RegionalCampaign.Edges:w!=null&&RegionalBranch.Enabled(w)?RegionalBranch.Edges:Edges;
    public static string GraphVersion(ContractWorld w)=>RegionalBattle.Enabled(w)?RegionalBattle.Version(w):FieldWorld.Enabled(w)?FieldWorld.Version:TrafficWorld.Enabled(w)?TrafficWorld.Version:TravelControl.Enabled(w)?TravelControl.Version:TravelWorld.Enabled(w)?TravelWorld.Version:NationalRoutes.Enabled(w)?NationalRoutes.Version:HistoricalRoads.Enabled(w)?HistoricalRoads.Version:RiverRoads.Enabled(w)?RiverRoads.Version:RegionalCampaign.Enabled(w)?RegionalCampaign.Version:RegionalControl.Enabled(w)?RegionalControl.Version:RegionalBranch.Enabled(w)?RegionalBranch.Version:MovementControl.Enabled(w)?MovementControl.Version:Version;
    public static ContractWorld Create(bool passBlocked=true)
    {
        var w=ContractFixture.Create();w.Schema=2;w.WorldId=new("world:branch-fixture");
        w.Definitions=w.Definitions with{Id=new("scenario:branch-fixture"),Version=Version,Ruleset=Ruleset};
        w.Branches=new(passBlocked?["pass"]:[],[]);ContractValidation.Validate(w);return w;
    }
    public static string[] KnownBlocked(ContractWorld w,EntityId observer)
    {
        if(!RegionalCampaign.Enabled(w))return w.Evidence.Where(e=>e.Observer==observer&&w.Knowledge.Any(k=>k.Character==observer&&k.Evidence==e.Id)&&e.Claim.StartsWith("route-block:",StringComparison.Ordinal)).Select(e=>e.Claim[12..]).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        return w.Evidence.Where(e=>e.Observer==observer&&w.Knowledge.Any(k=>k.Character==observer&&k.Evidence==e.Id)&&(e.Claim.StartsWith("route-block:",StringComparison.Ordinal)||e.Claim.StartsWith("route-open:",StringComparison.Ordinal)))
            .GroupBy(e=>e.Claim[(e.Claim.StartsWith("route-open:")?11:12)..]).Where(g=>g.OrderByDescending(e=>e.ObservedAt).ThenByDescending(e=>e.RecordedAt).First().Claim.StartsWith("route-block:")).Select(g=>g.Key).Order(StringComparer.Ordinal).ToArray();
    }
    public static string[] Plan(string from,string to,IEnumerable<string> knownBlocked,bool courier=false,ContractWorld? w=null,long? departureAt=null)
    {
        var blocked=knownBlocked.ToHashSet(StringComparer.Ordinal);var distance=GraphNodes(w).ToDictionary(n=>n.Id,_=>long.MaxValue,StringComparer.Ordinal);
        if(!distance.ContainsKey(from)||!distance.ContainsKey(to))throw new InvalidDataException("Unknown graph node");
        if(blocked.Any(id=>!GraphEdges(w).Any(e=>e.Id==id)))throw new InvalidDataException("Unknown blocked edge");
        distance[from]=0;var paths=new Dictionary<string,string[]>(StringComparer.Ordinal){{from,[]}};var visited=new HashSet<string>();
        while(true){var current=distance.Where(p=>!visited.Contains(p.Key)&&p.Value<long.MaxValue).OrderBy(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal).FirstOrDefault();
            if(current.Key==null)throw new InvalidDataException("No known traversable path");if(current.Key==to)return paths[to];visited.Add(current.Key);
            foreach(var e in GraphEdges(w).Where(e=>!blocked.Contains(e.Id)&&(e.From==current.Key||e.To==current.Key)).OrderBy(e=>e.Id,StringComparer.Ordinal)){
                var next=e.From==current.Key?e.To:e.From;var d=checked(current.Value+(w!=null&&TravelWorld.Enabled(w)?TravelWorld.End(w,e,checked((departureAt??w.ElapsedSeconds)+current.Value),courier)-((departureAt??w.ElapsedSeconds)+current.Value):Duration(e,courier)));var path=paths[current.Key].Append(e.Id).ToArray();
                if(d<distance[next]||d==distance[next]&&string.CompareOrdinal(string.Join("/",path),string.Join("/",paths.GetValueOrDefault(next,[])))<0){distance[next]=d;paths[next]=path;}}
        }
    }
    static int Duration(BranchEdge e,bool courier=false)=>checked((int)((e.LengthMm+(courier?e.CourierSpeed:e.FootSpeed)-1)/(courier?e.CourierSpeed:e.FootSpeed)));
    public static long Duration(IEnumerable<string> path,bool courier=false,ContractWorld? w=null)=>path.Sum(id=>(long)Duration(GraphEdges(w).Single(e=>e.Id==id),courier));
    public static JourneyLeg[] Schedule(string from,string[] path,long at,ContractWorld? w=null)
    {
        var result=new List<JourneyLeg>();foreach(var id in path){var e=GraphEdges(w).Single(e=>e.Id==id);var to=e.From==from?e.To:e.From;
            var end=TravelWorld.End(w,e,at);result.Add(new(id,from,to,at,end));from=to;at=end;}return result.ToArray();
    }
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)
    {
        if(RegionalBranch.Enabled(w))return RegionalBranch.Order(w,id,action,executor);
        var i=ContractFixture.Order(id,action,action==ActionKind.Transfer?40:0,executor:executor,deadline:1000);
        return i with{DeliverySeconds=1,WorkSeconds=action==ActionKind.Move?checked((int)Duration(Plan("castle","village",KnownBlocked(w,i.Executor)))):4,ReportSeconds=1};
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {if(OfficerLoop.Enabled(w))OfficerLoop.ValidateOrder(w,i);else if(TravelWorld.Enabled(w))TravelWorld.ValidateOrder(w,i);else if(MovementControl.Enabled(w))MovementControl.ValidateOrder(w,i);else ValidateOrdinaryOrder(w,i);}
    public static void ValidateOrdinaryOrder(ContractWorld w,Intent i)
    {
        if(RegionalBranch.Enabled(w)){RegionalBranch.ValidateOrder(w,i);return;}
        if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.Scope!=ContractFixture.Castle||i.DeliverySeconds!=1||i.ReportSeconds!=1||
            w.Locations.Single(l=>l.Actor==i.Executor).Site!=ContractFixture.Castle||
            i.WorkSeconds!=(i.Action==ActionKind.Move?Duration(Plan("castle","village",KnownBlocked(w,i.Executor))):4)||
            i.Action==ActionKind.Move&&i.DestinationSite!=ContractFixture.Village)throw new InvalidDataException("Unsupported graph request/timing");
    }
    public static long Start(ContractWorld w,TaskState task)
    {
        var from=TravelWorld.Enabled(w)?TravelWorld.Origin(w,task.Executor):"castle";var legs=Schedule(from,Plan(from,RegionalCampaign.Enabled(w)?RegionalCampaign.Destination(w,w.Requests.Single(r=>r.Id==task.RequestId).Intent.DestinationSite!.Value):"village",KnownBlocked(w,task.Executor),w:w),w.ElapsedSeconds,w);
        w.Branches=w.Branches! with{Journeys=[..w.Branches.Journeys,new(task.Id,task.Executor,legs)]};return legs[^1].EndAt;
    }
    public static long? NextBoundary(ContractWorld w,long target)=>w.Branches!.Journeys
        .Where(j=>w.Tasks.Any(t=>t.Id==j.Task&&t.Execution==ExecutionPhase.Running))
        .SelectMany(j=>j.Legs.SelectMany(l=>new[]{l.EndAt,TravelWorld.Departure(w,l.Edge,l.StartAt)})).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    public static bool Advance(ContractWorld w,TaskState task)=>TrafficWorld.Enabled(w)?TrafficWorld.Advance(w,task):AdvanceOrdinary(w,task);
    internal static bool AdvanceOrdinary(ContractWorld w,TaskState task)
    {
        var journey=w.Branches!.Journeys.Single(j=>j.Task==task.Id);
        // Every junction is a scheduler boundary. Never consult the next edge before arriving there.
        int index=Array.FindIndex(journey.Legs,l=>l.StartAt==w.ElapsedSeconds);
        if(index>=0){var candidate=journey.Legs[index];
            if(w.Branches.BlockedEdges.Contains(candidate.Edge,StringComparer.Ordinal)){
                Observe(w,task,candidate.Edge);
                string[] path;try{path=Plan(candidate.From,journey.Legs[^1].To,KnownBlocked(w,task.Executor),w:w);}catch(InvalidDataException){var stopped=Location(w,task);w.Locations=w.Locations.Select(l=>l.Actor==task.Executor?stopped:l).ToArray();return false;}
                journey=journey with{Legs=[..journey.Legs.Take(index),..Schedule(candidate.From,path,w.ElapsedSeconds,w)]};
                w.Branches=w.Branches with{Journeys=w.Branches.Journeys.Select(j=>j.Task==task.Id?journey:j).ToArray()};
                var end=journey.Legs[^1].EndAt;w.Actions=w.Actions.Select(a=>a.Task==task.Id?a with{Until=end}:a).ToArray();
                w.PendingEvents=w.PendingEvents.Select(e=>e.Task==task.Id&&e.Kind==EventKind.Finish?e with{At=end}:e).ToArray();
                // If several adjacent unknown closures exist, observe at this same physical node until a usable edge is found.
                return Advance(w,task);
            }}
        var location=Location(w,task);w.Locations=w.Locations.Select(l=>l.Actor==task.Executor?location:l).ToArray();return true;
    }
    static void Observe(ContractWorld w,TaskState task,string edge)
    {
        var claim="route-block:"+edge;
        if(w.Evidence.Any(e=>e.Observer==task.Executor&&e.Claim==claim&&(!RegionalCampaign.Enabled(w)||e.ObservedAt==w.ElapsedSeconds)))return;
        var id=WorldSession.Child("evidence",task.Id,"block/"+edge+(RegionalCampaign.Enabled(w)?"/"+w.ElapsedSeconds:""));
        w.Evidence=[..w.Evidence,new(id,task.Executor,task.Executor,claim,w.ElapsedSeconds,w.ElapsedSeconds,null,null)];w.Knowledge=[..w.Knowledge,new(task.Executor,id,w.ElapsedSeconds)];
        var mid=WorldSession.Child("message",task.Id,"block/"+edge+(RegionalCampaign.Enabled(w)?"/"+w.ElapsedSeconds:""));
        var journey=w.Branches!.Journeys.Single(j=>j.Task==task.Id);var node=journey.Legs.Single(l=>l.StartAt==w.ElapsedSeconds).From;
        var delay=Math.Max(1,checked((int)Duration(Plan(node,"castle",KnownBlocked(w,task.Executor),true,w),true,w)));
        MessageFlow.Send(w.Messaging,new(mid.Value,task.RequestId.Value,"route-observation",task.Executor.Value,task.Issuer.Value,claim,task.Scope.Value,w.ElapsedSeconds),w.ElapsedSeconds,delay);
    }
    static (JourneyLeg Leg,int Index,long Progress,long Distance) Sample(ContractWorld w,BranchJourney journey)
    {
        var task=w.Tasks.Single(t=>t.Id==journey.Task);var time=task.FinishedAt??w.ElapsedSeconds;
        int index=Array.FindIndex(journey.Legs,l=>time<l.EndAt);if(index<0)index=journey.Legs.Length-1;
        var leg=journey.Legs[index];var edge=GraphEdges(w).Single(e=>e.Id==leg.Edge);var p=Math.Clamp(checked((time-TravelWorld.Departure(w,leg.Edge,leg.StartAt))*edge.FootSpeed),0,edge.LengthMm);
        return(leg,index,p,journey.Legs.Take(index).Sum(l=>GraphEdges(w).Single(e=>e.Id==l.Edge).LengthMm)+p);
    }
    public static LocationState Location(ContractWorld w,TaskState task)
    {
        var j=w.Branches!.Journeys.Single(j=>j.Task==task.Id);var sample=Sample(w,j);
        return new(task.Executor,LocationKind.Traveling,null,WorldSession.Child("route",task.Id,"graph"),sample.Index,sample.Progress,null,null,null,RegionalBranch.Enabled(w)?RegionalBranch.Version:Version,w.CommitSequence);
    }
    static RoutePoint Node(string id,ContractWorld w)=>GraphNodes(w).Single(n=>n.Id==id).Position;
    public static BranchView View(ContractWorld w,ActorView knowledge)
    {
        if(RegionalBattle.Active(w)&&w.Region!.Units.Any(u=>u.Commander==knowledge.Observer))return RegionalBattle.BranchView(w,knowledge);
        if(FieldWorld.Local(w)&&knowledge.Observer==w.PlayerControl){var u=FieldWorld.Sample(w,0,w.ElapsedSeconds);var p=FieldWorld.ToWorld(u.Position);var next=FieldWorld.ToWorld(u.Next);return new(knowledge,p,new(next.EastMm-p.EastMm,next.NorthMm-p.NorthMm),u.Distance,u.Moving,null,[],KnownBlocked(w,knowledge.Observer),next,(long)Math.Sqrt(Math.Pow(next.EastMm-p.EastMm,2)+Math.Pow(next.NorthMm-p.NorthMm,2)));}
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==knowledge.Observer);
        if(j==null)return new(knowledge,Node(TravelWorld.Enabled(w)?TravelWorld.InitialNode(knowledge.Observer):"castle",w),new(1,0),0,false,null,[],KnownBlocked(w,knowledge.Observer),Node(TravelWorld.Enabled(w)?TravelWorld.InitialNode(knowledge.Observer):"castle",w),0);
        var sample=Sample(w,j);var a=Node(sample.Leg.From,w);var b=Node(sample.Leg.To,w);var e=GraphEdges(w).Single(e=>e.Id==sample.Leg.Edge);
        var point=new RoutePoint(a.EastMm+(b.EastMm-a.EastMm)*sample.Progress/e.LengthMm,a.NorthMm+(b.NorthMm-a.NorthMm)*sample.Progress/e.LengthMm);
        var running=w.Tasks.Single(t=>t.Id==j.Task).Execution==ExecutionPhase.Running&&w.ElapsedSeconds>=TravelWorld.Departure(w,sample.Leg.Edge,sample.Leg.StartAt);
        long nextProgress=TravelWorld.Enabled(w)&&w.Tasks.Single(t=>t.Id==j.Task).Execution==ExecutionPhase.Running?TravelWorld.Progress(w,sample.Leg,w.ElapsedSeconds+1):running?Math.Min(e.LengthMm,sample.Progress+e.FootSpeed):sample.Progress;
        var nextPoint=new RoutePoint(a.EastMm+(b.EastMm-a.EastMm)*nextProgress/e.LengthMm,a.NorthMm+(b.NorthMm-a.NorthMm)*nextProgress/e.LengthMm);
        return new(knowledge,point,new(b.EastMm-a.EastMm,b.NorthMm-a.NorthMm),sample.Distance,running,running?j.Legs[^1].EndAt:null,
            j.Legs.Select(l=>l.Edge).ToArray(),KnownBlocked(w,knowledge.Observer),nextPoint,nextProgress-sample.Progress);
    }
    public static int ReportDelay(ContractWorld w,TaskState task)
    {
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==task.Executor);if(j==null)return 1;
        var sample=Sample(w,j);var edge=GraphEdges(w).Single(e=>e.Id==sample.Leg.Edge);
        var known=KnownBlocked(w,task.Executor);
        long Cost(string node,long mm){try{return ((mm+edge.CourierSpeed-1)/edge.CourierSpeed)+Duration(Plan(node,"castle",known,true,w),true,w);}catch(InvalidDataException){return long.MaxValue;}}
        return checked((int)Math.Max(1,Math.Min(Cost(sample.Leg.From,sample.Progress),Cost(sample.Leg.To,edge.LengthMm-sample.Progress))));
    }
    public static void Validate(ContractWorld w)
    {
        if(w.Schema!=2||w.Branches==null||w.Definitions.Version!=GraphVersion(w)||w.Definitions.Id!=new EntityId(CreatedCampaign.Enabled(w)?"scenario:"+CreatedCampaign.Ruleset:RetainerStart.Enabled(w)?"scenario:"+RetainerStart.Ruleset:PersonalStart.Enabled(w)?"scenario:"+PersonalStart.Ruleset:NpcWork.Enabled(w)?"scenario:"+NpcWork.Ruleset:RegionalWork.Enabled(w)?"scenario:"+RegionalWork.Ruleset:AssessmentNotice.Enabled(w)?"scenario:officer-assessment-notice-v1":MissionReview.Enabled(w)?"scenario:officer-mission-review-v1":IncomingMission.Enabled(w)?"scenario:officer-incoming-mission-v1":MissionEntry.Enabled(w)?"scenario:officer-mission-entry-v1":OfficerLoop.Enabled(w)?"scenario:officer-peace-war-loop-v1":ContinuousWorld.Enabled(w)?"scenario:continuous-world-v1":RegionalScenery.Enabled(w)?"scenario:regional-scenery-v1":RegionalBattle.Enabled(w)?"scenario:regional-integrated-validation-v1":FieldWorld.Enabled(w)?"scenario:national-land-field-v1":TrafficWorld.Enabled(w)?"scenario:national-traffic-conflicts-v1":TravelControl.Enabled(w)?"scenario:national-personal-control-v1":TravelWorld.Enabled(w)?"scenario:national-personal-travel-v1":NationalRoutes.Enabled(w)?"scenario:national-inferred-routes-v1":HistoricalRoads.Enabled(w)?"scenario:historical-road-preview-v1":RiverRoads.Enabled(w)?"scenario:river-roads-v1":RegionalCampaign.Enabled(w)?"scenario:regional-campaign-v1":RegionalControl.Enabled(w)?"scenario:owari-control-v1":RegionalBranch.Enabled(w)?"scenario:owari-branches-v1":MovementControl.Enabled(w)?"scenario:movement-control":"scenario:branch-fixture"))throw new InvalidDataException("Graph version/module missing");
        if(RegionalBranch.Enabled(w))RegionalBranch.ValidateSites(w);
        if(w.Locations.Single(l=>l.Actor==ContractFixture.Lord).Site!=ContractFixture.Castle||!RegionalBranch.Enabled(w)&&!w.Definitions.Sites.Select(s=>s.Id).ToHashSet().SetEquals(new[]{ContractFixture.Castle,ContractFixture.Village}))throw new InvalidDataException("Unsupported graph fixture sites");
        if(TrafficWorld.Enabled(w))TrafficWorld.Validate(w);if(TravelControl.Enabled(w))TravelControl.Validate(w);if(TravelWorld.Enabled(w))TravelWorld.Validate(w);var graph=w.Branches;var edges=GraphEdges(w).Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
        if(graph.BlockedEdges.Distinct().Count()!=graph.BlockedEdges.Length||graph.BlockedEdges.Any(e=>!edges.Contains(e))||graph.Journeys.Select(j=>j.Task).Distinct().Count()!=graph.Journeys.Length)throw new InvalidDataException("Invalid graph references");
        foreach(var j in graph.Journeys){var task=w.Tasks.Single(t=>t.Id==j.Task);if(j.Actor!=task.Executor||task.Action!=ActionKind.Move||j.Legs.Length==0||(!TravelWorld.Enabled(w)&&j.Legs[0].From!="castle")||(!RegionalCampaign.Enabled(w)&&j.Legs[^1].To!="village"&&(!MovementControl.Enabled(w)||j.Legs[^1].To!="castle")))throw new InvalidDataException("Invalid journey endpoints");
            for(int i=0;i<j.Legs.Length;i++){var l=j.Legs[i];var e=GraphEdges(w).Single(e=>e.Id==l.Edge);
                if(l.StartAt<0||l.EndAt!=TravelWorld.End(w,e,l.StartAt)||!(e.From==l.From&&e.To==l.To||e.From==l.To&&e.To==l.From)||
                    i==0&&l.StartAt!=task.StartedAt||i>0&&(l.StartAt!=j.Legs[i-1].EndAt||l.From!=j.Legs[i-1].To))throw new InvalidDataException("Invalid journey chronology");}
            var action=w.Actions.Single(a=>a.Task==task.Id);if(action.Until!=j.Legs[^1].EndAt||action.StartedAt!=task.StartedAt||action.Progress!=Math.Clamp((task.FinishedAt??w.ElapsedSeconds)-action.StartedAt,0,action.Until-action.StartedAt))throw new InvalidDataException("Journey/event completion mismatch");
            if(RegionalBattle.Active(w)&&w.Region!.Units.Any(u=>u.Commander==j.Actor))continue;if(FieldWorld.Local(w)&&j.Actor==w.PlayerControl)continue;if(TravelWorld.Enabled(w)&&j!=graph.Journeys.Last(x=>x.Actor==j.Actor))continue;var location=w.Locations.Single(l=>l.Actor==j.Actor);if(TravelControl.Enabled(w)&&(TravelControl.CancelledPort(w,j.Task)??(TrafficWorld.Enabled(w)?TrafficWorld.StoppedNode(w,j.Task):null)) is {} port){if(location.Kind!=LocationKind.AtSite||location.Site!=RegionalCampaign.Site(port))throw new InvalidDataException("Port cancellation location mismatch");continue;}if(task.Execution==ExecutionPhase.Completed ? location.Kind!=LocationKind.AtSite||location.Site!=(MovementControl.Enabled(w)?MovementControl.Destination(w,task):ContractFixture.Village) : location!=(Location(w,task) with{Revision=location.Revision}))throw new InvalidDataException("Journey/location mismatch");}
        if(w.Tasks.Any(t=>t.Action==ActionKind.Move&&t.StartedAt!=null&&!graph.Journeys.Any(j=>j.Task==t.Id)))throw new InvalidDataException("Missing journey");
        foreach(var evidence in w.Evidence.Where(e=>(e.Claim.StartsWith("route-block:",StringComparison.Ordinal)||RegionalCampaign.Enabled(w)&&e.Claim.StartsWith("route-open:",StringComparison.Ordinal)))){
            var edge=GraphEdges(w).Single(e=>e.Id==evidence.Claim[(evidence.Claim.StartsWith("route-open:")?11:12)..]);
            if(!w.Knowledge.Any(k=>k.Character==evidence.Observer&&k.Evidence==evidence.Id))throw new InvalidDataException("Closure claim lacks personal knowledge");
            if(evidence.Message is {} message){var m=w.Messaging.Messages.Single(m=>m.Id==message.Value);
                if(m.Kind!="route-observation"||m.Body!=evidence.Claim||m.CreatedAt!=evidence.ObservedAt||
                    !w.Evidence.Any(e=>e.Message==null&&e.Observer.Value==m.SenderId&&e.Claim==m.Body&&e.ObservedAt==m.CreatedAt))throw new InvalidDataException("Closure report lacks source observation");}
            else if(RegionalCampaign.Enabled(w)){
                if((evidence.Claim.StartsWith("route-block:")!=RegionalCampaign.BlockedAt(w,edge.Id,evidence.ObservedAt!.Value))||
                    !w.Couriers!.Sightings.Any(x=>x.Actor==evidence.Observer&&x.Edge==edge.Id&&x.At==evidence.ObservedAt)&&
                    !graph.Journeys.Any(j=>j.Actor==evidence.Observer&&j.Legs.Any(l=>l.StartAt==evidence.ObservedAt&&(l.From==edge.From||l.From==edge.To))))throw new InvalidDataException("Road claim lacks timed local observation");
            }
            else if(!graph.BlockedEdges.Contains(edge.Id)||!graph.Journeys.Any(j=>j.Actor==evidence.Observer&&j.Legs.Any(l=>l.StartAt==evidence.ObservedAt&&(l.From==edge.From||l.From==edge.To))))throw new InvalidDataException("Closure observation lacks physical presence");
        }
        foreach(var l in w.Locations.Where(l=>l.Kind==LocationKind.Traveling))if(!graph.Journeys.Any(j=>WorldSession.Child("route",j.Task,"graph")==l.Route&&j.Actor==l.Actor))throw new InvalidDataException("Unknown route location");
    }
}
