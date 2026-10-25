namespace HakutaiKagami.Core.Contracts;
public sealed record PersonalTravelRequest(EntityId Id,EntityId Actor,EntityId Task,EntityId? Destination);
public sealed record TravelDecision(PersonalTravelRequest Request,long RequestedAt,long EffectiveAt,long? AppliedAt,
    string Node,JourneyLeg[] Before,JourneyLeg[] After,string[] KnownBlocked,string Result);
// Personal agency, no remote interception or instantaneous lord command. V1 travel saves remain immutable.
public static class TravelControl
{
    public const string Ruleset="national-personal-control-v1";
    public static string Version{get;}=ContractJson.CanonicalHash(new[]{TravelWorld.Version,"redirect-after-current-edge-v1","cancel-strictly-before-port-departure-v1","persist-personal-decisions-v1"});
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||TrafficWorld.Enabled(w);
    public static ContractWorld Create(){var w=TravelWorld.Create();w.WorldId=new("world:national-personal-control-v1");w.Definitions=w.Definitions with{Id=new("scenario:national-personal-control-v1"),Ruleset=Ruleset,Version=Version};w.TravelDecisions=[];ContractValidation.Validate(w);return w;}
    public static bool Pending(ContractWorld w,EntityId actor)=>w.TravelDecisions?.Any(d=>d.Request.Actor==actor&&d.AppliedAt==null)==true;
    public static bool PortWaiting(ContractWorld w,EntityId actor){var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor);if(j==null||w.Tasks.Single(t=>t.Id==j.Task).Execution!=ExecutionPhase.Running)return false;var l=j.Legs.FirstOrDefault(l=>w.ElapsedSeconds<l.EndAt);return l!=null&&NationalRoutes.Sea(l.Edge)&&w.ElapsedSeconds<TravelWorld.Departure(w,l.Edge,l.StartAt);}
    public static string Goal(ContractWorld w,BranchJourney j)=>w.TravelDecisions?.LastOrDefault(d=>d.Request.Task==j.Task&&d.AppliedAt!=null&&d.Result=="redirected")?.Request.Destination is {} site?RegionalCampaign.Destination(w,site):RegionalCampaign.Destination(w,w.Requests.Single(r=>r.Id==w.Tasks.Single(t=>t.Id==j.Task).RequestId).Intent.DestinationSite!.Value);
    public static string? CancelledPort(ContractWorld w,EntityId task)=>w.TravelDecisions?.LastOrDefault(d=>d.Request.Task==task&&d.Result=="port-cancelled")?.Node;
    public static PersonalTravelRequest Request(ContractWorld w,string id,string? destination){var t=w.Tasks.LastOrDefault(t=>t.Executor==w.PlayerControl&&t.Action==ActionKind.Move&&t.Execution==ExecutionPhase.Running)??throw new InvalidDataException("No running personal trip");return new(new(id),w.PlayerControl,t.Id,destination==null?null:RegionalCampaign.Site(destination));}
    public static TravelDecision Prepare(ContractWorld w,PersonalTravelRequest request){
        if(!Enabled(w)||!EntityId.IsValid(request.Id.Value)||request.Destination is {} dest&&!w.Definitions.Sites.Any(site=>site.Id==dest)||request.Actor!=w.PlayerControl||Pending(w,request.Actor))throw new InvalidDataException("Personal decision unavailable");
        var t=w.Tasks.SingleOrDefault(t=>t.Id==request.Task);if(t==null||t.Action!=ActionKind.Move||t.Executor!=request.Actor||t.Issuer!=request.Actor||t.Execution!=ExecutionPhase.Running||!WorldSession.Permits(w,request.Actor,w.Requests.Single(r=>r.Id==t.RequestId).Intent,w.ElapsedSeconds))throw new InvalidDataException("Trip is not controlled by this actor");
        var j=w.Branches!.Journeys.Single(j=>j.Task==t.Id);var index=Array.FindIndex(j.Legs,l=>w.ElapsedSeconds<l.EndAt);if(index<0)throw new InvalidDataException("Already arrived");var leg=j.Legs[index];var known=BranchRoutes.KnownBlocked(w,request.Actor);
        if(request.Destination==null){if(!PortWaiting(w,request.Actor))throw new InvalidDataException("Departure has begun or actor is not waiting at a port");return new(request,w.ElapsedSeconds,w.ElapsedSeconds,null,leg.From,j.Legs,j.Legs,known,"pending");}
        var destination=RegionalCampaign.Destination(w,request.Destination.Value);if(PortWaiting(w,request.Actor)||destination==Goal(w,j))throw new InvalidDataException("Cancel boarding first, or choose another destination");
        var path=BranchRoutes.Plan(leg.To,destination,VoyageExpense.PlanningExclusions(w,t,known),w:w,departureAt:leg.EndAt);var after=j.Legs.Take(index+1).Concat(BranchRoutes.Schedule(leg.To,path,leg.EndAt,w)).ToArray();
        return new(request,w.ElapsedSeconds,leg.EndAt,null,leg.To,j.Legs,after,known,"pending");
    }
    public static void ApplyDue(ContractWorld w){foreach(var d in w.TravelDecisions!.Where(d=>d.AppliedAt==null&&d.EffectiveAt<=w.ElapsedSeconds).ToArray()){
        var t=w.Tasks.Single(t=>t.Id==d.Request.Task);string result;
        if(t.Execution!=ExecutionPhase.Running)result="too-late";
        else if(!WorldSession.Permits(w,d.Request.Actor,w.Requests.Single(r=>r.Id==t.RequestId).Intent,w.ElapsedSeconds))result="unavailable";
        else if(d.Request.Destination==null){WorldSession.Stop(w,t,"port-cancelled");w.Locations=w.Locations.Select(l=>l.Actor==t.Executor?new LocationState(l.Actor,LocationKind.AtSite,RegionalCampaign.Site(d.Node),null,0,0,null,null,null,null,w.CommitSequence):l).ToArray();result="port-cancelled";}
        else{w.Branches=w.Branches! with{Journeys=w.Branches.Journeys.Select(j=>j.Task==t.Id?j with{Legs=d.After}:j).ToArray()};var end=d.After[^1].EndAt;w.Actions=w.Actions.Select(a=>a.Task==t.Id?a with{Until=end,Progress=Math.Clamp(w.ElapsedSeconds-a.StartedAt,0,end-a.StartedAt)}:a).ToArray();w.PendingEvents=w.PendingEvents.Select(e=>e.Task==t.Id&&e.Kind==EventKind.Finish?e with{At=end}:e).ToArray();result="redirected";}
        w.TravelDecisions=w.TravelDecisions!.Select(x=>x.Request.Id==d.Request.Id?x with{AppliedAt=w.ElapsedSeconds,Result=result}:x).ToArray();w.EffectLedger=[..w.EffectLedger,new(new("effect:personal/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(d.Request.Id.Value))),"personal-travel/"+d.Request.Id.Value,ContractJson.CanonicalHash(d.Request),t.Id,w.ElapsedSeconds,result)];
    }}
    public static long? NextBoundary(ContractWorld w,long target)=>w.TravelDecisions!.Where(d=>d.AppliedAt==null&&d.EffectiveAt>w.ElapsedSeconds&&d.EffectiveAt<=target).OrderBy(d=>d.EffectiveAt).Select(d=>(long?)d.EffectiveAt).FirstOrDefault();
    static bool Same<T>(T a,T b)=>ContractJson.CanonicalHash(a)==ContractJson.CanonicalHash(b);
    public static void Validate(ContractWorld w){
        var ds=w.TravelDecisions??throw new InvalidDataException("Missing personal decision module");if(ds.Select(d=>d.Request.Id).Distinct().Count()!=ds.Length||ds.Any(d=>w.Requests.Any(r=>r.Id==d.Request.Id)))throw new InvalidDataException("Duplicate personal request");
        foreach(var d in ds){var t=w.Tasks.Single(t=>t.Id==d.Request.Task);if(d.Request.Actor!=w.PlayerControl||t.Executor!=d.Request.Actor||t.Issuer!=d.Request.Actor||t.Action!=ActionKind.Move||d.RequestedAt<t.StartedAt||d.RequestedAt>w.ElapsedSeconds||d.Before.Length==0||d.Before[0].StartAt!=t.StartedAt)throw new InvalidDataException("Invalid personal decision authority/time");
            for(int i=0;i<d.Before.Length;i++){var l=d.Before[i];var e=BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge);if(l.EndAt!=TravelWorld.End(w,e,l.StartAt)||!(e.From==l.From&&e.To==l.To||e.From==l.To&&e.To==l.From)||i>0&&(l.StartAt!=d.Before[i-1].EndAt||l.From!=d.Before[i-1].To))throw new InvalidDataException("Invalid preserved decision route");}
            var ix=Array.FindIndex(d.Before,l=>d.RequestedAt<l.EndAt);if(ix<0)throw new InvalidDataException("Decision after arrival");var leg=d.Before[ix];var at=d.Request.Destination==null?d.RequestedAt:leg.EndAt;var node=d.Request.Destination==null?leg.From:leg.To;
            var evidenceWorld=ContractJson.Copy(w);evidenceWorld.Knowledge=w.Knowledge.Where(k=>k.LearnedAt<=d.RequestedAt).ToArray();evidenceWorld.Evidence=w.Evidence.Where(e=>e.RecordedAt<=d.RequestedAt).ToArray();if(!Same(d.KnownBlocked,BranchRoutes.KnownBlocked(evidenceWorld,d.Request.Actor)))throw new InvalidDataException("Decision used unknown closure");
            JourneyLeg[] after;if(d.Request.Destination==null){if(!NationalRoutes.Sea(leg.Edge)||d.RequestedAt>=TravelWorld.Departure(w,leg.Edge,leg.StartAt))throw new InvalidDataException("Cancelled after departure");after=d.Before;}else{var goal=RegionalCampaign.Destination(w,d.Request.Destination.Value);after=[..d.Before.Take(ix+1),..BranchRoutes.Schedule(node,BranchRoutes.Plan(node,goal,VoyageExpense.PlanningExclusions(w,t,d.KnownBlocked),w:w,departureAt:at),at,w)];}
            if(d.EffectiveAt!=at||d.Node!=node||!Same(d.After,after)||d.AppliedAt==null&&(d.Result!="pending"||d.EffectiveAt<=w.ElapsedSeconds)||d.AppliedAt!=null&&(d.AppliedAt!=d.EffectiveAt||d.AppliedAt>w.ElapsedSeconds||d.Result is not("redirected" or "port-cancelled" or "too-late" or "unavailable")))throw new InvalidDataException("Invalid personal decision result/plan");
            var effects=w.EffectLedger.Where(e=>e.BusinessKey=="personal-travel/"+d.Request.Id.Value).ToArray();if(d.AppliedAt==null?effects.Length!=0:effects.Length!=1||effects[0].BodyHash!=ContractJson.CanonicalHash(d.Request)||effects[0].At!=d.AppliedAt||effects[0].Task!=t.Id||effects[0].Result!=d.Result)throw new InvalidDataException("Personal effect missing/duplicated");
            if(d.Result=="port-cancelled"&&(t.Execution!=ExecutionPhase.Stopped||t.FinishedAt!=d.AppliedAt||t.InternalReason!="port-cancelled"))throw new InvalidDataException("Cancelled trip still active");
            if(d.Result=="redirected"){var current=w.Branches!.Journeys.Single(j=>j.Task==t.Id);if(!Same(current.Legs.Take(ix+1).ToArray(),d.Before.Take(ix+1).ToArray()))throw new InvalidDataException("Personal decision rewrote traveled prefix");}
        }
        foreach(var group in ds.GroupBy(d=>d.Request.Task)){long until=-1;foreach(var d in group){if(d.RequestedAt<until)throw new InvalidDataException("Overlapping personal decisions");until=d.AppliedAt??long.MaxValue;}}
    }
}
