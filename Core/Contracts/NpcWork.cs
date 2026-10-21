using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record NpcWorkInputs(bool LocalWitness,bool InspectionPermit,EntityId[] OwnTasks,bool LastReportKnown,string? KnownResult,long ReadyAt,bool CooldownElapsed);
public sealed record NpcWorkDecision(EntityId Id,EntityId Actor,long At,long InputRevision,NpcWorkInputs Inputs,string InputHash,string Reason,EntityId? Request,ResultCode Result);
public sealed record NpcWorkState(NpcWorkDecision[] Decisions);
// A bounded knowledge-based planner, opt-in only. No second clock, transport or task implementation.
public static class NpcWork
{
    public const string Ruleset="npc-local-work-v1",Contract="local-witness-own-reports-stable-input-memory-3600cooldown-v1";
    public const long Interval=3600;
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||PersonalStart.Enabled(w)||RetainerStart.Enabled(w)||CreatedCampaign.Enabled(w);
    public static ContractWorld Create()
    {
        var w=RegionalWork.Create();w.WorldId=new("world:"+Ruleset);w.NpcWork=new([]);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset};w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};
        ContractValidation.Validate(w);return w;
    }
    public static NpcWorkInputs Inputs(ContractWorld w,EntityId actor,long at,long? inputRevision=null)
    {
        var a=RegionalCampaign.ActorPointAt(w,actor,at);var p=RegionalCampaign.ActorPointAt(w,w.PlayerControl,at);
        bool seen=Math.Abs(a.EastMm-p.EastMm)<=80000&&Math.Abs(a.NorthMm-p.NorthMm)<=80000;
        var own=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=(inputRevision??w.CommitSequence)&&r.Intent.Actor==actor&&r.Intent.Action==ActionKind.Inspect).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id.Value,StringComparer.Ordinal).ToArray();
        bool known=true;string? result=null;
        if(own.Length>0){var task=w.Tasks.Single(t=>t.RequestId==own[^1].Id);string id=WorldSession.Child("message",task.Id,"report").Value;
            known=w.Messaging.Knowledge.Any(k=>k.CharacterId==actor.Value&&k.MessageId==id&&k.LearnedAt<=at);if(known)result=w.Messaging.Messages.Single(m=>m.Id==id).Body;}
        long ready=own.Length==0?0:checked(own[^1].CreatedAt+Interval);
        var role=RegionalWork.Roles.Single(r=>r.Person==actor);
        bool permit=w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult)&&w.Grants.Any(g=>g.Agent==actor&&g.Scope==RegionalCampaign.Site(role.Node)&&g.Actions.Contains(ActionKind.Inspect)&&g.From<=at&&at<g.Until);
        return new(seen,permit,own.Select(r=>w.Tasks.Single(t=>t.RequestId==r.Id).Id).ToArray(),known,result,ready,at>=ready);
    }
    public static string Reason(NpcWorkInputs input)=>!input.LocalWitness?"no-local-witness":!input.InspectionPermit?"no-inspection-permit":!input.LastReportKnown?"await-own-report":!input.CooldownElapsed?"cooldown":"offer-local-inspection";
    public static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w)||FieldPresence.PlayerDeployed(w))return null;var candidates=new List<long>();
        foreach(var role in RegionalWork.Roles){var input=Inputs(w,role.Person,w.ElapsedSeconds);string hash=ContractJson.CanonicalHash(input);var last=w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==role.Person);
            if(last==null||last.InputHash!=hash)candidates.Add(checked(w.ElapsedSeconds+1));
            else if(input.LocalWitness&&input.InspectionPermit&&input.LastReportKnown&&input.ReadyAt>w.ElapsedSeconds)candidates.Add(input.ReadyAt);}
        return candidates.Where(t=>t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    internal static void TakeTurns(WorldSession session)
    {
        foreach(var role in RegionalWork.Roles){var w=session.DiagnosticSnapshot();if(w.Paused||FieldPresence.PlayerDeployed(w))return;
            var input=Inputs(w,role.Person,w.ElapsedSeconds);string hash=ContractJson.CanonicalHash(input);if(w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==role.Person)?.InputHash==hash)continue;
            string reason=Reason(input);EntityId? request=null;var result=ResultCode.NotAvailable;
            if(reason=="offer-local-inspection"){
                request=new("request:npc-work/"+role.Person.Value.Replace(':','/')+"/"+(input.OwnTasks.Length+1));
                result=session.Submit(RegionalWork.Build(w,request.Value.Value,role.Person),session.Revision).Code;
            }
            session.RecordNpcWork(role.Person,w.CommitSequence,input,hash,reason,request,result);
        }
    }
    public static void Validate(ContractWorld w)
    {
        if(w.NpcWork==null||w.Region!.Active&&!CreatedCampaign.Enabled(w))throw new InvalidDataException("NPC peace profile/module required");
        var ds=w.NpcWork.Decisions;
        if(ds.Select(d=>d.Id).Distinct().Count()!=ds.Length)throw new InvalidDataException("Duplicate NPC decision");
        for(int n=0;n<ds.Length;n++){var d=ds[n];
            if(d.Id!=new EntityId("decision:npc-work/"+(n+1))||!RegionalWork.Roles.Any(r=>r.Person==d.Actor)||d.At<0||d.At>w.ElapsedSeconds||d.InputRevision<0||d.InputRevision>w.CommitSequence||n>0&&d.At<ds[n-1].At||d.InputHash!=ContractJson.CanonicalHash(d.Inputs)||d.InputHash!=ContractJson.CanonicalHash(Inputs(w,d.Actor,d.At,d.InputRevision))||d.Reason!=Reason(d.Inputs))throw new InvalidDataException("NPC decision lacks its historical knowledge or local witness");
            if(d.Request is {} id){if(d.Reason!="offer-local-inspection"||id!=new EntityId("request:npc-work/"+d.Actor.Value.Replace(':','/')+"/"+(d.Inputs.OwnTasks.Length+1)))throw new InvalidDataException("Invalid NPC request identity");
                if(d.Result is ResultCode.Accepted or ResultCode.AlreadyAccepted){var request=w.Requests.Single(r=>r.Id==id);if(request.CreatedAt!=d.At||request.Intent.Actor!=d.Actor)throw new InvalidDataException("NPC dispatch differs from its decision");}}
            else if(d.Reason=="offer-local-inspection"||d.Result!=ResultCode.NotAvailable)throw new InvalidDataException("NPC wait has a dispatch result");
        }
    }
}
