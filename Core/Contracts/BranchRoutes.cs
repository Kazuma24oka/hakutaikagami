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
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static ContractWorld Create(bool passBlocked=true)
    {
        var w=ContractFixture.Create();w.Schema=2;w.WorldId=new("world:branch-fixture");
        w.Definitions=w.Definitions with{Id=new("scenario:branch-fixture"),Version=Version,Ruleset=Ruleset};
        w.Branches=new(passBlocked?["pass"]:[],[]);ContractValidation.Validate(w);return w;
    }
    public static string[] KnownBlocked(ContractWorld w,EntityId observer)=>w.Evidence.Where(e=>e.Observer==observer&&w.Knowledge.Any(k=>k.Character==observer&&k.Evidence==e.Id)&&e.Claim.StartsWith("route-block:",StringComparison.Ordinal))
        .Select(e=>e.Claim[12..]).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    public static string[] Plan(string from,string to,IEnumerable<string> knownBlocked,bool courier=false)
    {
        var blocked=knownBlocked.ToHashSet(StringComparer.Ordinal);var distance=Nodes.ToDictionary(n=>n.Id,_=>long.MaxValue,StringComparer.Ordinal);
        if(!distance.ContainsKey(from)||!distance.ContainsKey(to))throw new InvalidDataException("Unknown graph node");
        if(blocked.Any(id=>!Edges.Any(e=>e.Id==id)))throw new InvalidDataException("Unknown blocked edge");
        distance[from]=0;var paths=new Dictionary<string,string[]>(StringComparer.Ordinal){{from,[]}};var visited=new HashSet<string>();
        while(true){var current=distance.Where(p=>!visited.Contains(p.Key)&&p.Value<long.MaxValue).OrderBy(p=>p.Value).ThenBy(p=>p.Key,StringComparer.Ordinal).FirstOrDefault();
            if(current.Key==null)throw new InvalidDataException("No known traversable path");if(current.Key==to)return paths[to];visited.Add(current.Key);
            foreach(var e in Edges.Where(e=>!blocked.Contains(e.Id)&&(e.From==current.Key||e.To==current.Key)).OrderBy(e=>e.Id,StringComparer.Ordinal)){
                var next=e.From==current.Key?e.To:e.From;var d=checked(current.Value+Duration(e,courier));var path=paths[current.Key].Append(e.Id).ToArray();
                if(d<distance[next]||d==distance[next]&&string.CompareOrdinal(string.Join("/",path),string.Join("/",paths.GetValueOrDefault(next,[])))<0){distance[next]=d;paths[next]=path;}}
        }
    }
    static int Duration(BranchEdge e,bool courier=false)=>checked((int)((e.LengthMm+(courier?e.CourierSpeed:e.FootSpeed)-1)/(courier?e.CourierSpeed:e.FootSpeed)));
    public static long Duration(IEnumerable<string> path,bool courier=false)=>path.Sum(id=>(long)Duration(Edges.Single(e=>e.Id==id),courier));
    static JourneyLeg[] Schedule(string from,string[] path,long at)
    {
        var result=new List<JourneyLeg>();foreach(var id in path){var e=Edges.Single(e=>e.Id==id);var to=e.From==from?e.To:e.From;
            var end=checked(at+Duration(e));result.Add(new(id,from,to,at,end));from=to;at=end;}return result.ToArray();
    }
    public static Intent Order(ContractWorld w,string id,ActionKind action=ActionKind.Move,EntityId? executor=null)
    {
        var i=ContractFixture.Order(id,action,action==ActionKind.Transfer?40:0,executor:executor,deadline:1000);
        return i with{DeliverySeconds=1,WorkSeconds=action==ActionKind.Move?checked((int)Duration(Plan("castle","village",KnownBlocked(w,i.Executor)))):4,ReportSeconds=1};
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        if(i.Actor!=ContractFixture.Lord||i.Executor==ContractFixture.Lord||i.Scope!=ContractFixture.Castle||i.DeliverySeconds!=1||i.ReportSeconds!=1||
            w.Locations.Single(l=>l.Actor==i.Executor).Site!=ContractFixture.Castle||
            i.WorkSeconds!=(i.Action==ActionKind.Move?Duration(Plan("castle","village",KnownBlocked(w,i.Executor))):4)||
            i.Action==ActionKind.Move&&i.DestinationSite!=ContractFixture.Village)throw new InvalidDataException("Unsupported graph request/timing");
    }
    public static long Start(ContractWorld w,TaskState task)
    {
        var legs=Schedule("castle",Plan("castle","village",KnownBlocked(w,task.Executor)),w.ElapsedSeconds);
        w.Branches=w.Branches! with{Journeys=[..w.Branches.Journeys,new(task.Id,task.Executor,legs)]};return legs[^1].EndAt;
    }
    public static long? NextBoundary(ContractWorld w,long target)=>w.Branches!.Journeys
        .Where(j=>w.Tasks.Any(t=>t.Id==j.Task&&t.Execution==ExecutionPhase.Running))
        .SelectMany(j=>j.Legs.Select(l=>l.EndAt)).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    public static bool Advance(ContractWorld w,TaskState task)
    {
        var journey=w.Branches!.Journeys.Single(j=>j.Task==task.Id);
        // Every junction is a scheduler boundary. Never consult the next edge before arriving there.
        int index=Array.FindIndex(journey.Legs,l=>l.StartAt==w.ElapsedSeconds);
        if(index>=0){var candidate=journey.Legs[index];
            if(w.Branches.BlockedEdges.Contains(candidate.Edge,StringComparer.Ordinal)){
                Observe(w,task,candidate.Edge);
                string[] path;try{path=Plan(candidate.From,"village",KnownBlocked(w,task.Executor));}catch(InvalidDataException){return false;}
                journey=journey with{Legs=[..journey.Legs.Take(index),..Schedule(candidate.From,path,w.ElapsedSeconds)]};
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
        if(w.Evidence.Any(e=>e.Observer==task.Executor&&e.Claim==claim))return;
        var id=WorldSession.Child("evidence",task.Id,"block/"+edge);
        w.Evidence=[..w.Evidence,new(id,task.Executor,task.Executor,claim,w.ElapsedSeconds,w.ElapsedSeconds,null,null)];w.Knowledge=[..w.Knowledge,new(task.Executor,id,w.ElapsedSeconds)];
        var mid=WorldSession.Child("message",task.Id,"block/"+edge);
        var journey=w.Branches!.Journeys.Single(j=>j.Task==task.Id);var node=journey.Legs.Single(l=>l.StartAt==w.ElapsedSeconds).From;
        var delay=Math.Max(1,checked((int)Duration(Plan(node,"castle",KnownBlocked(w,task.Executor)),true)));
        MessageFlow.Send(w.Messaging,new(mid.Value,task.RequestId.Value,"route-observation",task.Executor.Value,task.Issuer.Value,claim,task.Scope.Value,w.ElapsedSeconds),w.ElapsedSeconds,delay);
    }
    static (JourneyLeg Leg,int Index,long Progress,long Distance) Sample(ContractWorld w,BranchJourney journey)
    {
        var task=w.Tasks.Single(t=>t.Id==journey.Task);var time=task.FinishedAt??w.ElapsedSeconds;
        int index=Array.FindIndex(journey.Legs,l=>time<l.EndAt);if(index<0)index=journey.Legs.Length-1;
        var leg=journey.Legs[index];var edge=Edges.Single(e=>e.Id==leg.Edge);var p=Math.Clamp(checked((time-leg.StartAt)*edge.FootSpeed),0,edge.LengthMm);
        return(leg,index,p,journey.Legs.Take(index).Sum(l=>Edges.Single(e=>e.Id==l.Edge).LengthMm)+p);
    }
    public static LocationState Location(ContractWorld w,TaskState task)
    {
        var j=w.Branches!.Journeys.Single(j=>j.Task==task.Id);var sample=Sample(w,j);
        return new(task.Executor,LocationKind.Traveling,null,WorldSession.Child("route",task.Id,"graph"),sample.Index,sample.Progress,null,null,null,Version,w.CommitSequence);
    }
    static RoutePoint Node(string id)=>Nodes.Single(n=>n.Id==id).Position;
    public static BranchView View(ContractWorld w,ActorView knowledge)
    {
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==knowledge.Observer);
        if(j==null)return new(knowledge,Node("castle"),new(1,0),0,false,null,[],KnownBlocked(w,knowledge.Observer),Node("castle"),0);
        var sample=Sample(w,j);var a=Node(sample.Leg.From);var b=Node(sample.Leg.To);var e=Edges.Single(e=>e.Id==sample.Leg.Edge);
        var point=new RoutePoint(a.EastMm+(b.EastMm-a.EastMm)*sample.Progress/e.LengthMm,a.NorthMm+(b.NorthMm-a.NorthMm)*sample.Progress/e.LengthMm);
        var running=w.Tasks.Single(t=>t.Id==j.Task).Execution==ExecutionPhase.Running;
        long nextProgress=running?Math.Min(e.LengthMm,sample.Progress+e.FootSpeed):sample.Progress;
        var nextPoint=new RoutePoint(a.EastMm+(b.EastMm-a.EastMm)*nextProgress/e.LengthMm,a.NorthMm+(b.NorthMm-a.NorthMm)*nextProgress/e.LengthMm);
        return new(knowledge,point,new(b.EastMm-a.EastMm,b.NorthMm-a.NorthMm),sample.Distance,running,running?j.Legs[^1].EndAt:null,
            j.Legs.Select(l=>l.Edge).ToArray(),KnownBlocked(w,knowledge.Observer),nextPoint,nextProgress-sample.Progress);
    }
    public static int ReportDelay(ContractWorld w,TaskState task)
    {
        var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==task.Executor);if(j==null)return 1;
        var sample=Sample(w,j);var edge=Edges.Single(e=>e.Id==sample.Leg.Edge);
        var known=KnownBlocked(w,task.Executor);
        long Cost(string node,long mm){try{return ((mm+edge.CourierSpeed-1)/edge.CourierSpeed)+Duration(Plan(node,"castle",known),true);}catch(InvalidDataException){return long.MaxValue;}}
        return checked((int)Math.Max(1,Math.Min(Cost(sample.Leg.From,sample.Progress),Cost(sample.Leg.To,edge.LengthMm-sample.Progress))));
    }
    public static void Validate(ContractWorld w)
    {
        if(w.Schema!=2||w.Branches==null||w.Definitions.Version!=Version||w.Definitions.Id!=new EntityId("scenario:branch-fixture"))throw new InvalidDataException("Graph version/module missing");
        if(w.Locations.Single(l=>l.Actor==ContractFixture.Lord).Site!=ContractFixture.Castle||!w.Definitions.Sites.Select(s=>s.Id).ToHashSet().SetEquals(new[]{ContractFixture.Castle,ContractFixture.Village}))throw new InvalidDataException("Unsupported graph fixture sites");
        var graph=w.Branches;var edges=Edges.Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
        if(graph.BlockedEdges.Distinct().Count()!=graph.BlockedEdges.Length||graph.BlockedEdges.Any(e=>!edges.Contains(e))||graph.Journeys.Select(j=>j.Task).Distinct().Count()!=graph.Journeys.Length)throw new InvalidDataException("Invalid graph references");
        foreach(var j in graph.Journeys){var task=w.Tasks.Single(t=>t.Id==j.Task);if(j.Actor!=task.Executor||task.Action!=ActionKind.Move||j.Legs.Length==0||j.Legs[0].From!="castle"||j.Legs[^1].To!="village")throw new InvalidDataException("Invalid journey endpoints");
            for(int i=0;i<j.Legs.Length;i++){var l=j.Legs[i];var e=Edges.Single(e=>e.Id==l.Edge);
                if(l.StartAt<0||l.EndAt-l.StartAt!=Duration(e)||!(e.From==l.From&&e.To==l.To||e.From==l.To&&e.To==l.From)||
                    i==0&&l.StartAt!=task.StartedAt||i>0&&(l.StartAt!=j.Legs[i-1].EndAt||l.From!=j.Legs[i-1].To))throw new InvalidDataException("Invalid journey chronology");}
            var action=w.Actions.Single(a=>a.Task==task.Id);if(action.Until!=j.Legs[^1].EndAt||action.StartedAt!=task.StartedAt||action.Progress!=Math.Clamp((task.FinishedAt??w.ElapsedSeconds)-action.StartedAt,0,action.Until-action.StartedAt))throw new InvalidDataException("Journey/event completion mismatch");
            var location=w.Locations.Single(l=>l.Actor==j.Actor);if(task.Execution==ExecutionPhase.Completed ? location.Kind!=LocationKind.AtSite||location.Site!=ContractFixture.Village : location!=(Location(w,task) with{Revision=location.Revision}))throw new InvalidDataException("Journey/location mismatch");}
        if(w.Tasks.Any(t=>t.Action==ActionKind.Move&&t.StartedAt!=null&&!graph.Journeys.Any(j=>j.Task==t.Id)))throw new InvalidDataException("Missing journey");
        foreach(var evidence in w.Evidence.Where(e=>e.Claim.StartsWith("route-block:",StringComparison.Ordinal))){
            var edge=Edges.Single(e=>e.Id==evidence.Claim[12..]);
            if(!w.Knowledge.Any(k=>k.Character==evidence.Observer&&k.Evidence==evidence.Id))throw new InvalidDataException("Closure claim lacks personal knowledge");
            if(evidence.Message is {} message){var m=w.Messaging.Messages.Single(m=>m.Id==message.Value);
                if(m.Kind!="route-observation"||m.Body!=evidence.Claim||m.CreatedAt!=evidence.ObservedAt||
                    !w.Evidence.Any(e=>e.Message==null&&e.Observer.Value==m.SenderId&&e.Claim==m.Body&&e.ObservedAt==m.CreatedAt))throw new InvalidDataException("Closure report lacks source observation");}
            else if(!graph.BlockedEdges.Contains(edge.Id)||!graph.Journeys.Any(j=>j.Actor==evidence.Observer&&j.Legs.Any(l=>l.StartAt==evidence.ObservedAt&&(l.From==edge.From||l.From==edge.To))))throw new InvalidDataException("Closure observation lacks physical presence");
        }
        foreach(var l in w.Locations.Where(l=>l.Kind==LocationKind.Traveling))if(!graph.Journeys.Any(j=>WorldSession.Child("route",j.Task,"graph")==l.Route&&j.Actor==l.Actor))throw new InvalidDataException("Unknown route location");
    }
}
