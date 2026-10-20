using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// Explicit new rules, same graph-state schema. Delivery uses a known rendezvous, not live interception.
public static class MovementControl
{
    public const string Ruleset="common-movement-control-v1";
    public static string Version {get;}=ContractJson.CanonicalHash(new[]{BranchRoutes.Version,"fork-rendezvous-v1","read-effective-stop-v1","redirect-at-next-node-v1"});
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||RegionalControl.Enabled(w)||RegionalCampaign.Enabled(w);
    public static ContractWorld Create()
    {
        var w=BranchRoutes.Create();w.WorldId=new("world:movement-control");
        w.Definitions=w.Definitions with{Id=new("scenario:movement-control"),Version=Version,Ruleset=Ruleset};
        w.Grants=w.Grants.Select(g=>g.Actions.Contains(ActionKind.Cancel)?g with{Actions=[..g.Actions,ActionKind.Redirect]}:g).ToArray();
        ContractValidation.Validate(w);return w;
    }
    public static long Appointment(ContractWorld w,TaskState target)
    {var r=w.Requests.Single(r=>r.Id==target.RequestId);return checked(Math.Max(r.CreatedAt+r.Intent.DeliverySeconds,r.Intent.EffectiveAt)+BranchRoutes.Duration(["road"],w:w));}
    static int MinimumDelivery(ContractWorld w)=>(RegionalControl.Enabled(w)||RegionalCampaign.Enabled(w))?RegionalControl.CourierToFork(w):10;
    static RoutePoint MeetingPoint(ContractWorld w)=>BranchRoutes.GraphNodes(w).Single(n=>n.Id=="fork").Position;
    public static Intent Order(ContractWorld w,string id,EntityId targetId,ActionKind action,long effectiveAt=25,bool important=true)
    {
        var target=w.Tasks.Single(t=>t.Id==targetId);
        var due=Math.Max(Appointment(w,target),checked(w.ElapsedSeconds+MinimumDelivery(w)));
        return new(new(id),ContractFixture.Lord,target.Executor,ContractFixture.Castle,action,null,null,0,
            action==ActionKind.Redirect?ContractFixture.Castle:null,new(DeadlineCriterion.Completion,(RegionalControl.Enabled(w)||RegionalCampaign.Enabled(w))?1000000:1000,false,null,(RegionalControl.Enabled(w)||RegionalCampaign.Enabled(w))?1000000:1000),effectiveAt,
            checked((int)(due-w.ElapsedSeconds)),4,1,important,TaskKind.BindingOrder,targetId);
    }
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        if(RegionalCampaign.Enabled(w)){RegionalCampaign.ValidateOrder(w,i);return;}
        if(i.Action is not(ActionKind.Cancel or ActionKind.Redirect)){BranchRoutes.ValidateOrdinaryOrder(w,i);return;}
        var target=w.Tasks.SingleOrDefault(t=>t.Id==i.TargetTask);
        if(target==null||target.Action!=ActionKind.Move||i.Actor!=ContractFixture.Lord||i.Executor!=target.Executor||i.Scope!=target.Scope||
            i.WorkSeconds!=4||i.ReportSeconds!=1||i.DeliverySeconds!=Math.Max(Appointment(w,target),w.ElapsedSeconds+MinimumDelivery(w))-w.ElapsedSeconds||
            i.Action==ActionKind.Redirect&&i.DestinationSite!=ContractFixture.Castle)throw new InvalidDataException("Invalid rendezvous control command");
    }
    public static void CheckArrivals(ContractWorld w)
    {
        foreach(var t in w.Tasks.Where(t=>t.Action is ActionKind.Cancel or ActionKind.Redirect)){
            var mid=WorldSession.Child("message",t.Id,"order").Value;
            var attempts=w.Messaging.Attempts.Where(a=>a.MessageId==mid&&(a.Status==AttemptStatus.InTransit&&a.DueAt==w.ElapsedSeconds||a.Status==AttemptStatus.Lost&&a.LateDueAt==w.ElapsedSeconds)).ToArray();
            if(attempts.Length==0)continue;
            var view=BranchRoutes.View(w,new(t.Executor,w.CommitSequence,w.ElapsedSeconds,w.Locations.Single(l=>l.Actor==t.Executor),[],[],[]));
            if(view.Position==MeetingPoint(w))continue;
            // Late copies must also physically find their recipient; they cannot create remote knowledge.
            w.Messaging.Attempts=w.Messaging.Attempts.Select(a=>attempts.Any(x=>x.Id==a.Id)?a with{FailOnArrival=true,
                Status=a.Status==AttemptStatus.Lost?AttemptStatus.Rejected:a.Status,
                FinishedAt=a.Status==AttemptStatus.Lost?w.ElapsedSeconds:a.FinishedAt,LateDueAt=a.Status==AttemptStatus.Lost?null:a.LateDueAt}:a).ToArray();
        }
    }
    public static void ReportFailures(ContractWorld w)
    {
        foreach(var a in w.Messaging.Attempts.Where(a=>a.FailureAt==w.ElapsedSeconds)){
            var message=w.Messaging.Messages.Single(m=>m.Id==a.MessageId);
            if(message.Kind!="contract-order"||!w.Tasks.Any(t=>WorldSession.Child("message",t.Id,"order").Value==message.Id&&t.Action is ActionKind.Cancel or ActionKind.Redirect))continue;
            MessageFlow.ReportFailure(w.Messaging,"message:control-failure/"+a.Id.Replace(':','/'),a.Id,ContractFixture.Clerk.Value,w.ElapsedSeconds,MinimumDelivery(w));
        }
    }
    public static bool Redirect(ContractWorld w,TaskState control,TaskState target)
    {
        if(target.Execution!=ExecutionPhase.Running)return false;
        var journey=w.Branches!.Journeys.Single(j=>j.Task==target.Id);
        var current=Array.FindIndex(journey.Legs,l=>w.ElapsedSeconds<l.EndAt);if(current<0)return false;
        var keep=journey.Legs.Take(current+1).ToArray();var node=keep[^1].To;var at=keep[^1].EndAt;
        var path=BranchRoutes.Plan(node,"castle",BranchRoutes.KnownBlocked(w,target.Executor),w:w);
        var legs=keep.Concat(BranchRoutes.Schedule(node,path,at,w)).ToArray();
        w.Branches=w.Branches with{Journeys=w.Branches.Journeys.Select(j=>j.Task==target.Id?j with{Legs=legs}:j).ToArray()};
        w.Actions=w.Actions.Select(a=>a.Task==target.Id?a with{Until=legs[^1].EndAt}:a).ToArray();
        w.PendingEvents=w.PendingEvents.Select(e=>e.Task==target.Id&&e.Kind==EventKind.Finish?e with{At=legs[^1].EndAt}:e).ToArray();return true;
    }
    public static EntityId Destination(ContractWorld w,TaskState task)=>RegionalCampaign.Enabled(w)?RegionalCampaign.Site(w.Branches!.Journeys.Single(j=>j.Task==task.Id).Legs[^1].To):w.Branches!.Journeys.Single(j=>j.Task==task.Id).Legs[^1].To=="castle"?ContractFixture.Castle:ContractFixture.Village;
    public static void Validate(ContractWorld w)
    {
        foreach(var r in w.Requests.Where(r=>r.Intent.Action is ActionKind.Cancel or ActionKind.Redirect)){
            var i=r.Intent;var target=w.Tasks.Single(t=>t.Id==i.TargetTask);
            if(target.Action!=ActionKind.Move||i.Actor!=ContractFixture.Lord||i.Scope!=target.Scope||i.Executor!=target.Executor||i.WorkSeconds!=4||i.ReportSeconds!=1||
                !RegionalCampaign.Enabled(w)&&i.DeliverySeconds!=Math.Max(Appointment(w,target),r.CreatedAt+MinimumDelivery(w))-r.CreatedAt)throw new InvalidDataException("Control address/timing mismatch");
            var task=w.Tasks.Single(t=>t.RequestId==r.Id);
            if(task.Execution==ExecutionPhase.Completed){var learned=MessageFlow.LearnedAt(w.Messaging,WorldSession.Child("message",task.Id,"order").Value,task.Executor.Value);
                if(learned==null||task.FinishedAt<learned||task.FinishedAt<i.EffectiveAt)throw new InvalidDataException("Control applied before reception/recognition/effective time");}
        }
        foreach(var journey in w.Branches!.Journeys.Where(j=>j.Legs[^1].To=="castle"&&!(TravelWorld.Enabled(w)&&w.Requests.Single(r=>r.Id==w.Tasks.Single(t=>t.Id==j.Task).RequestId).Intent.DestinationSite==ContractFixture.Castle)))
            if(!w.Tasks.Any(t=>t.Action==ActionKind.Redirect&&t.Execution==ExecutionPhase.Completed&&w.Requests.Single(r=>r.Id==t.RequestId).Intent.TargetTask==journey.Task&&
                w.EffectLedger.Any(e=>e.Task==t.Id&&e.Result=="redirected"&&e.BusinessKey==t.Id.Value+"/finish/1")))throw new InvalidDataException("Changed destination lacks committed authority/effect");
    }

}
