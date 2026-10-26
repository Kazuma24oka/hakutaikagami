namespace HakutaiKagami.Core.Contracts;
// Opt-in personal work for at most eight authored historical NPCs. Existing task/transport/clock only.
public static class NewGameNpcRoutine
{
    public const string Policy="historical-own-site-first-inspection-v1";
    public const int MaximumActors=8;
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.NpcRoutinePolicy==Policy;
    static EntityId[] Actors(ContractWorld w)=>!Enabled(w)?[]:w.NewGame!.Start.People.Where(p=>!p.Created&&p.Id!=w.PlayerControl.Value).Select(p=>new EntityId(p.Id)).OrderBy(p=>p.Value,StringComparer.Ordinal).ToArray();
    public static bool IsActor(ContractWorld w,EntityId actor)=>Actors(w).Contains(actor);
    public static int BoundaryCost(ContractWorld w)=>2*Actors(w).Length;
    public static NpcWorkInputs Inputs(ContractWorld w,EntityId actor,long at,long revision)
    {
        var own=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision&&r.Intent.Actor==actor&&(r.Intent.Executor==actor||LocalWorkDemand.Enabled(w))&&r.Intent.Action==ActionKind.Inspect).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).ToArray();
        bool known=true;string? result=null;
        if(own.Length>0){var task=w.Tasks.Single(t=>t.RequestId==own[^1].Id);string report=WorldSession.Child("message",task.Id,"report").Value;known=w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==report&&k.LearnedAt<=at);if(known)result=w.Messaging.Messages.Single(m=>m.Id==report).Body;}
        if(PrivateStanddown.Enabled(w)||w.NewGame?.CompanionPolicy==PersonalCompanions.PrivateInspectionPolicy){
            var historical=w.ForkTransportLocation();var requests=w.Requests.Where(r=>r.CreatedAt<at||r.CreatedAt==at&&r.CommitSequence<=revision).ToArray();historical.Branches=w.Branches! with{Journeys=w.Branches!.Journeys.Where(j=>requests.Any(r=>w.Tasks.Any(t=>t.Id==j.Task&&t.RequestId==r.Id))).ToArray()};
            var point=RegionalCampaign.RoadActorPointAt(historical,actor,at);var node=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>n.Position==point);bool moving=requests.Any(r=>r.Intent.Executor==actor&&r.Intent.Action==ActionKind.Move&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at)));historical.Locations=w.Locations.Select(l=>l.Actor==actor?l with{Kind=node!=null&&!moving?LocationKind.AtSite:LocationKind.Traveling,Site=node!=null&&!moving?RegionalCampaign.Site(node.Id):null}:l).ToArray();w=historical;
        }
        var place=w.Locations.Single(l=>l.Actor==actor);bool local=place.Kind==LocationKind.AtSite&&place.Site!=null;long ready=0;
        bool permit=!PersonalCompanions.Occupied(w,actor,at,revision)&&!NpcArmies.CommanderBusyAt(w,actor,at,revision)&&IsActor(w,actor)&&w.Characters.Single(c=>c.Id==actor).Life==LifeState.Adult&&local&&WorldSession.Permits(w,actor,NewGameWorld.InspectFor(w,actor,"request:npc-routine-preview",at),at);
        return new(local,permit,own.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id).Id).ToArray(),known,result,ready,at>=ready);
    }
    public static string Reason(NpcWorkInputs i)=>!i.LocalWitness?"not-at-own-site":!i.InspectionPermit?"no-personal-permit":!i.LastReportKnown?"await-own-report":i.OwnTasks.Length>0?"own-result-known":"inspect-own-site";
    public static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var times=new List<long>();
        foreach(var actor in Actors(w)){var i=Inputs(w,actor,w.ElapsedSeconds,w.CommitSequence);var last=w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==actor);if(last==null||last.InputHash!=ContractJson.CanonicalHash(i))times.Add(checked(w.ElapsedSeconds+1));}
        return times.Where(t=>t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    internal static void TakeTurns(WorldSession session)
    {
        foreach(var actor in Actors(session.DiagnosticSnapshot())){var w=session.DiagnosticSnapshot();if(w.Paused)return;var i=Inputs(w,actor,w.ElapsedSeconds,w.CommitSequence);var hash=ContractJson.CanonicalHash(i);if(w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==actor)?.InputHash==hash)continue;var reason=Reason(i);EntityId? request=null;ResultCode result=ResultCode.NotAvailable;
            if(reason=="inspect-own-site"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=session.Submit(NewGameWorld.InspectFor(w,actor,request.Value.Value,w.ElapsedSeconds),session.Revision).Code;}
            session.RecordNpcWork(actor,w.CommitSequence,i,hash,reason,request,result);
        }
    }
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w)||w.NpcWork==null||Actors(w).Length>MaximumActors)throw new InvalidDataException("NPC routine policy/module mismatch");var ds=w.NpcWork.Decisions;if(ds.Select(d=>d.Id).Distinct().Count()!=ds.Length)throw new InvalidDataException("Duplicate NPC routine decision");
        for(int n=0;n<ds.Length;n++){var d=ds[n];if(d.Id!=new EntityId("decision:npc-work/"+(n+1))||!IsActor(w,d.Actor)||d.At<0||d.At>w.ElapsedSeconds||d.InputRevision<0||d.InputRevision>w.CommitSequence||n>0&&d.At<ds[n-1].At||d.InputHash!=ContractJson.CanonicalHash(d.Inputs)||d.InputHash!=ContractJson.CanonicalHash(Inputs(w,d.Actor,d.At,d.InputRevision))||d.Reason!=Reason(d.Inputs))throw new InvalidDataException("NPC routine lacks its own historical inputs");
            if(d.Request is {} id){if(d.Reason!="inspect-own-site"||id!=new EntityId("request:npc-routine/"+d.Actor.Value.Replace(':','/')+"/"+(d.Inputs.OwnTasks.Length+1)))throw new InvalidDataException("Invalid NPC routine request identity");if(d.Result is ResultCode.Accepted or ResultCode.AlreadyAccepted){var request=w.Requests.Single(r=>r.Id==id);if(request.CreatedAt!=d.At||request.Intent.Actor!=d.Actor||request.Intent.Executor!=d.Actor)throw new InvalidDataException("NPC routine dispatch differs from decision");}}
            else if(d.Reason=="inspect-own-site"||d.Result!=ResultCode.NotAvailable)throw new InvalidDataException("NPC routine wait has dispatch result");
        }
    }
}
