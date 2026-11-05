namespace HakutaiKagami.Core.Contracts;

public sealed partial class WorldSession
{
    // Keep state private and hold the session gate for the ordered turn. Change()
    // copies before mutation, so a pre-submit generation remains valid for the
    // recorded input revision without serializing a diagnostic copy per actor.
    internal void TakeNewGameNpcTurns()
    {
        lock(gate)
        foreach(var actor in NewGameNpcRoutine.Actors(state)){if(NpcJudgment.Enabled(state))ReceiveNpcLocalOffers(actor);var w=state;if(w.Paused)return;var i=NewGameNpcRoutine.Inputs(w,actor,w.ElapsedSeconds,w.CommitSequence);var hash=ContractJson.CanonicalHash(i);if(w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==actor)?.InputHash==hash)continue;var reason=NewGameNpcRoutine.Reason(i);EntityId? request=null;ResultCode result=ResultCode.NotAvailable;
            if(reason=="inspect-own-site"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NewGameWorld.InspectFor(w,actor,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="offer-local-inspection"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NpcLocalOffers.Offer(w,actor,i.Routine!.Judgment!.Recipient!.Value,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="practice-own-skill"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(CharacterDevelopment.PracticeForNpc(w,actor,request.Value.Value,i.Routine!.Purpose!.Skill),Revision).Code;}
            else if(reason=="yield-practice-to-known-duty")YieldNpcPractice(actor,i.Routine!.Purpose!.ActivePractice!.Value);
            RecordNpcWork(actor,w.CommitSequence,i,hash,reason,request,result);
        }
    }
}
