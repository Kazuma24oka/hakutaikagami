namespace HakutaiKagami.Core.Contracts;

public sealed record NpcRoutineSituation(string Policy,LocalInspectionNeed? Need);

public static partial class NewGameNpcRoutine
{
    public const string ContinuingPolicy="adult-own-known-local-demand-daily-work-v2";
    public const int ContinuingMaximumActors=3000;
    public const long ContinuingInterval=86400;
    public static bool Continuing(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.NpcRoutinePolicy==ContinuingPolicy;
    public static bool Supported(string? policy)=>policy is Policy or ContinuingPolicy;

    // The planner receives only the actor's observed need, received result and
    // currently available personal action. Scheduled road truth is not an input.
    static NpcWorkInputs ContinuingInputs(ContractWorld w,EntityId actor,long at,long revision)
    {
        var own=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision&&r.Intent.Actor==actor&&r.Intent.Action==ActionKind.Inspect).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).ToArray();
        bool known=true;string? result=null;
        if(own.Length>0){var task=w.Tasks.Single(t=>t.RequestId==own[^1].Id);string report=WorldSession.Child("message",task.Id,"report").Value;known=w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==report&&k.LearnedAt<=at);if(known)result=w.Messaging.Messages.Single(m=>m.Id==report).Body;}
        var permissionWorld=w.ForkTransportLocation();permissionWorld.CommitSequence=revision;
        var site=PersonalCompanions.PhysicalSiteAt(w,actor,at,revision+1);bool local=site!=null;
        bool permit=local&&IsActor(w,actor)&&!HostageCustody.HeldAt(w,actor,at,revision+1)&&!FieldConvoy.Busy(w,actor,at,revision+1)&&!NpcFreight.Busy(w,actor,at,revision+1)&&!PersonalCompanions.Occupied(w,actor,at,revision)&&!NpcArmies.CommanderBusyAt(w,actor,at,revision)&&WorldSession.Permits(permissionWorld,actor,new(new("request:npc-routine-preview"),actor,actor,site!.Value,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+60,false,null,at+60),at,1,4,1,false,TaskKind.BindingOrder),at);
        long ready=own.Length==0?0:checked(own[^1].CreatedAt+ContinuingInterval);
        var need=local?LocalWorkDemand.At(w,actor,site!.Value,at,revision):null;
        return new(local,permit,own.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id).Id).ToArray(),known,result,ready,at>=ready){Routine=new(ContinuingPolicy,need)};
    }
    static string ContinuingReason(NpcWorkInputs i)=>!i.LocalWitness?"not-at-own-site":!i.InspectionPermit?"no-personal-permit":!i.LastReportKnown?"await-own-report":!i.CooldownElapsed?"personal-work-cooldown":i.Routine!.Need?.Needed!=true?"no-known-local-need":"inspect-own-site";
    static long? ContinuingBoundary(ContractWorld w,NpcWorkInputs input)
    {
        if(!Continuing(w)||!input.LocalWitness||!input.InspectionPermit||!input.LastReportKnown)return null;
        if(input.ReadyAt>w.ElapsedSeconds)return input.ReadyAt;
        // Fixed observation-age checkpoints cannot be postponed by frame rate.
        return checked((w.ElapsedSeconds/LocalWorkDemand.RecordAge+1)*LocalWorkDemand.RecordAge);
    }
}
