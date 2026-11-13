namespace HakutaiKagami.Core.Contracts;

public sealed record InspectedRoad(EntityId Task,EntityId Site,string Edge,string Destination,long LengthMm,bool Blocked,long ObservedAt,long LearnedAt,EntityId Evidence,bool Direct);
// Opt-in only. Observations occur on actual completion, at the actual local endpoint.
public static class LocalInspectionFindings
{
    public const string Policy="completed-local-land-road-observation-v1";
    static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.InspectionPolicy==Policy;
    static EntityId EvidenceId(TaskState t,string edge)=>WorldSession.Child("evidence",t.Id,"inspected-road/"+edge);
    static EntityId NoteId(TaskState t,string edge)=>WorldSession.Child("message",t.Id,"inspected-road/"+edge);
    static BranchEdge[] Roads(ContractWorld w,TaskState t){string node=NewGameWorld.Node(t.Scope.Value);return BranchRoutes.GraphEdges(w).Where(e=>(e.From==node||e.To==node)&&!NationalRoutes.Sea(e.Id)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();}
    internal static void Record(ContractWorld w,TaskState task)
    {
        if(!Enabled(w)||task.Action!=ActionKind.Inspect)return;
        string node=NewGameWorld.Node(task.Scope.Value);var point=BranchRoutes.GraphNodes(w).Single(n=>n.Id==node).Position;
        if(RegionalCampaign.ActorPointAt(w,task.Executor,w.ElapsedSeconds)!=point)throw new InvalidDataException("Inspection observations require physical local presence");
        AbilityEffects.RecordInspection(w,task);
        foreach(var edge in Roads(w,task)){
            var id=EvidenceId(task,edge.Id);if(w.Evidence.Any(e=>e.Id==id))continue;
            bool blocked=RegionalCampaign.BlockedAt(w,edge.Id,w.ElapsedSeconds);string claim=(blocked?"route-block:":"route-open:")+edge.Id;
            w.Couriers=w.Couriers! with{Sightings=[..w.Couriers.Sightings,new(task.Executor,edge.Id,blocked,w.ElapsedSeconds,point)]};
            w.Evidence=[..w.Evidence,new(id,task.Executor,task.Id,claim,w.ElapsedSeconds,w.ElapsedSeconds,null,null)];w.Knowledge=[..w.Knowledge,new(task.Executor,id,w.ElapsedSeconds)];
            if(task.Issuer==task.Executor)continue;
            var note=NoteId(task,edge.Id);var input=w.Requests.Single(r=>r.Id==task.RequestId).Intent;
            w.Outbox=[..w.Outbox,new(note,task.Id,task.Executor,task.Issuer,"route-observation",claim,w.ElapsedSeconds,input.ReportSeconds,false)];w.SealedMessages=[..w.SealedMessages,note];
        }
    }
    public static InspectedRoad[] View(WorldSession session,EntityId observer,EntityId task)
    {
        var view=session.GetActorView(observer);var w=session.DiagnosticSnapshot();var t=w.Tasks.SingleOrDefault(t=>t.Id==task);
        if(!Enabled(w)||t==null||!view.Tasks.Any(t=>t.Id==task))return [];
        return Roads(w,t).SelectMany(edge=>view.Evidence.Where(e=>e.Subject==task&&(e.Id==EvidenceId(t,edge.Id)||e.Message==NoteId(t,edge.Id))).Select(e=>new InspectedRoad(task,t.Scope,edge.Id,edge.From==NewGameWorld.Node(t.Scope.Value)?edge.To:edge.From,edge.LengthMm,e.Claim.StartsWith("route-block:",StringComparison.Ordinal),e.ObservedAt!.Value,e.RecordedAt,e.Id,e.Message==null))).OrderBy(e=>e.Edge,StringComparer.Ordinal).ToArray();
    }
    public static void Validate(ContractWorld w)
    {
        AbilityEffects.ValidateInspection(w);
        bool on=Enabled(w);var authored=w.Evidence.Where(e=>e.Id.Value.Contains("/inspected-road/",StringComparison.Ordinal)&&e.Message==null).ToArray();
        if(!on){if(authored.Length!=0||w.Outbox.Any(o=>o.Kind=="route-observation"&&o.Id.Value.Contains("/inspected-road/",StringComparison.Ordinal)))throw new InvalidDataException("Inspection results without policy");return;}
        var evidenceById=w.Evidence.ToLookup(e=>e.Id);var known=w.Knowledge.ToHashSet();
        var requests=w.Requests.ToLookup(r=>r.Id);var outbox=w.Outbox.ToLookup(o=>o.Id);var tasks=w.Tasks.ToLookup(t=>t.Id);
        var nodes=BranchRoutes.GraphNodes(w).ToLookup(n=>n.Id,StringComparer.Ordinal);
        var roads=BranchRoutes.GraphEdges(w).Where(e=>!NationalRoutes.Sea(e.Id)).SelectMany(e=>new[]{e.From,e.To}.Distinct(StringComparer.Ordinal).Select(n=>(Node:n,Edge:e))).ToLookup(x=>x.Node,x=>x.Edge,StringComparer.Ordinal);
        IEnumerable<BranchEdge> LocalRoads(TaskState t)=>roads[NewGameWorld.Node(t.Scope.Value)].OrderBy(e=>e.Id,StringComparer.Ordinal);
        var actorPoints=new Dictionary<long,Func<EntityId,RoutePoint>>();
        RoutePoint ActorAt(EntityId actor,long at){if(!actorPoints.TryGetValue(at,out var get))actorPoints[at]=get=RegionalCampaign.ActorPointsAt(w,at);return get(actor);}
        var expected=new HashSet<EntityId>();
        foreach(var t in w.Tasks.Where(t=>t.Action==ActionKind.Inspect&&t.Execution==ExecutionPhase.Completed)){
            long at=t.FinishedAt??throw new InvalidDataException("Inspection completion time missing");string node=NewGameWorld.Node(t.Scope.Value);var point=nodes[node].Single().Position;
            if(ActorAt(t.Executor,at)!=point)throw new InvalidDataException("Inspection result detached from local execution");
            foreach(var edge in LocalRoads(t)){
                var id=EvidenceId(t,edge.Id);expected.Add(id);var e=evidenceById[id].SingleOrDefault();string claim=(RegionalCampaign.BlockedAt(w,edge.Id,at)?"route-block:":"route-open:")+edge.Id;
                if(e==null||e.Observer!=t.Executor||e.Subject!=t.Id||e.Claim!=claim||e.ObservedAt!=at||e.RecordedAt!=at||e.Message!=null||e.Supersedes!=null||!known.Contains(new(t.Executor,id,at)))throw new InvalidDataException("Inspection result changed or incomplete");
                if(t.Issuer==t.Executor)continue;
                var mid=NoteId(t,edge.Id);var input=requests[t.RequestId].Single().Intent;var o=outbox[mid].SingleOrDefault();
                if(o==null||o.Task!=t.Id||o.Sender!=t.Executor||o.Recipient!=t.Issuer||o.Kind!="route-observation"||o.Body!=claim||o.At!=at||o.Delay!=input.ReportSeconds||!w.SealedMessages.Contains(mid))throw new InvalidDataException("Inspection report lacks immutable source paper");
            }
        }
        if(!expected.SetEquals(authored.Select(e=>e.Id)))throw new InvalidDataException("Unexpected inspection result");
        foreach(var o in w.Outbox.Where(o=>o.Id.Value.Contains("/inspected-road/",StringComparison.Ordinal))){var t=tasks[o.Task].Single();if(t.Execution!=ExecutionPhase.Completed||t.Issuer==t.Executor||!LocalRoads(t).Any(e=>NoteId(t,e.Id)==o.Id))throw new InvalidDataException("Orphan inspection report");}
    }
}
