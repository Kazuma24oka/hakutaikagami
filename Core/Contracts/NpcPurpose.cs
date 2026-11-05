namespace HakutaiKagami.Core.Contracts;

public sealed record NpcPurposeInputs(EntityId[] PracticeTasks,EntityId? ActivePractice,EntityId[] KnownDuties,bool OtherWork,long PracticeReadyAt,bool PracticeDue,DevelopmentSkill Skill);

public static partial class NewGameNpcRoutine
{
    public const string PurposePolicy="adult-own-duty-inspection-idle-practice-v3";
    public static bool PurposeEnabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.NpcRoutinePolicy==PurposePolicy;
    internal static bool DiscretionaryPractice(ContractWorld w,Intent i)=>PurposeEnabled(w)&&i.Actor!=w.PlayerControl&&i.Actor==i.Executor&&i.Action==ActionKind.Train;
    static NpcWorkInputs PurposeInputs(ContractWorld w,EntityId actor,long at,long revision)
    {
        var basis=ContinuingInputs(w,actor,at,revision);
        var requests=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision).ToArray();
        var own=requests.Where(r=>r.Intent.Actor==actor&&r.Intent.Executor==actor&&r.Intent.Action is ActionKind.Inspect or ActionKind.Train).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).ToArray();
        var practices=own.Where(r=>r.Intent.Action==ActionKind.Train).ToArray();
        bool Active(TaskState t)=>t.FinishedAt==null||t.FinishedAt>=at;
        bool Heard(TaskState t)=>w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==WorldSession.Child("message",t.Id,"order").Value&&k.LearnedAt<at);
        var practiceTasks=practices.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id)).ToArray();
        var active=practiceTasks.Where(Active).OrderBy(t=>t.Id.Value,StringComparer.Ordinal).FirstOrDefault();
        // Hearing an outside request also gives it attention, but never assents
        // to a voluntary request. Its existing agreement remains authoritative.
        var duties=w.Tasks.Where(t=>t.Executor==actor&&t.Issuer!=actor&&requests.Any(r=>r.Id==t.RequestId)&&Active(t)&&Heard(t)).Select(t=>t.Id).OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
        bool other=w.Tasks.Any(t=>t.Executor==actor&&Active(t)&&requests.Any(r=>r.Id==t.RequestId)&&!practiceTasks.Any(p=>p.Id==t.Id)&&(t.Issuer==actor||Heard(t)));
        bool known=true;string? result=null;
        if(own.Length>0){var t=w.Tasks.Single(t=>t.RequestId==own[^1].Id);string mid=WorldSession.Child("message",t.Id,"report").Value;known=w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==mid&&k.LearnedAt<at);if(known)result=w.Messaging.Messages.Single(m=>m.Id==mid).Body;}
        long ready=practices.Length==0?0:checked(practices[^1].CreatedAt+ContinuingInterval);
        var p=new NpcPurposeInputs(practiceTasks.Select(t=>t.Id).ToArray(),active?.Id,duties,other,ready,at>=ready,CharacterDevelopment.PreferredPractice(w,actor));
        return basis with{OwnTasks=own.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id).Id).ToArray(),LastReportKnown=known,KnownResult=result,Routine=new(PurposePolicy,basis.Routine!.Need){Purpose=p}};
    }
    static string PurposeReason(NpcWorkInputs i)
    {
        var p=i.Routine!.Purpose!;
        if(p.ActivePractice!=null&&(!i.LocalWitness||!i.InspectionPermit||p.KnownDuties.Length>0||p.OtherWork))return "yield-practice-to-known-duty";
        if(!i.LocalWitness)return "not-at-own-site";
        if(!i.InspectionPermit)return "no-personal-permit";
        if(p.KnownDuties.Length>0||p.OtherWork)return "existing-duty-first";
        if(p.ActivePractice!=null)return "continue-personal-practice";
        if(!i.LastReportKnown)return "await-own-report";
        if(i.CooldownElapsed&&i.Routine.Need?.Needed==true)return "inspect-own-site";
        // The practice ready flag is fixed by the same input clock, not a UI tick.
        if(p.PracticeDue)return "practice-own-skill";
        return "no-discretionary-work-due";
    }
    static long? PurposeBoundary(ContractWorld w,EntityId actor,NpcWorkInputs input)
    {
        var times=new List<long>();long at=w.ElapsedSeconds;
        if(w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.LearnedAt==at)||w.Tasks.Any(t=>t.Executor==actor&&t.FinishedAt==at))times.Add(checked(at+1));
        var p=input.Routine!.Purpose!;
        if(input.LocalWitness&&input.InspectionPermit&&input.LastReportKnown&&!p.OtherWork&&p.ActivePractice==null&&p.KnownDuties.Length==0){if(input.ReadyAt>at)times.Add(input.ReadyAt);if(p.PracticeReadyAt>at)times.Add(p.PracticeReadyAt);times.Add(checked((at/LocalWorkDemand.RecordAge+1)*LocalWorkDemand.RecordAge));}
        return times.Order().Select(x=>(long?)x).FirstOrDefault();
    }
}

public sealed partial class WorldSession
{
    private bool YieldNpcPracticeForCooperation(ContractWorld w,CompanionInput input)
    {
        if(!NewGameNpcRoutine.PurposeEnabled(w)||input.Person==w.PlayerControl||input.Action is not(CompanionAction.Inspect or CompanionAction.PrivateInspect or CompanionAction.TravelTogether or CompanionAction.VisitCustodySite))return false;
        var tasks=w.Tasks.Where(t=>t.Issuer==input.Person&&t.Executor==input.Person&&t.Action==ActionKind.Train&&!Terminal(t)).ToArray();
        foreach(var t in tasks)Stop(w,t,"npc-duty-priority");
        return tasks.Length>0;
    }
    internal void YieldNpcPractice(EntityId actor,EntityId task)
    {
        lock(gate){
            if(!NewGameNpcRoutine.PurposeEnabled(state)||actor==state.PlayerControl)throw new InvalidDataException("NPC practice policy is required");
            var t=state.Tasks.Single(t=>t.Id==task&&t.Issuer==actor&&t.Executor==actor&&t.Action==ActionKind.Train);
            if(t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)return;
            Change(s=>{Stop(s,t,"npc-duty-priority");FlushOutbox(s);});
        }
    }
}
