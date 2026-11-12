namespace HakutaiKagami.Core.Contracts;

public sealed partial class WorldSession
{
    // Keep state private and hold the session gate for the ordered turn. Change()
    // copies before mutation, so a pre-submit generation remains valid for the
    // recorded input revision without serializing a diagnostic copy per actor.
    internal void TakeNewGameNpcTurns()
    {
        lock(gate)
        {
            // 休眠の版の世界（v109）：起きている人物の手番は1人ずつ変更し、世界全体の検査は手番の終わりに1回だけ行う。
            // まとめた検査に通らないとき（途中で例外が出たときも）は、手番の前の状態に戻し、1人ずつ検査する今までのやり方でやり直す。
            if(NpcDormancy.Enabled(state))
            {
                var before=state;bool ok=true;deferValidation=true;
                try{RunNewGameNpcTurns();}
                catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException or InvalidOperationException){ok=false;}
                finally{deferValidation=false;}
                if(ok&&!ReferenceEquals(before,state)){try{if(FailBatchForTest)throw new InvalidDataException("検査用の失敗");ContractValidation.Validate(state,before);}catch(InvalidDataException){ok=false;}}
                if(ok)return;
                state=before;BatchRetries++;
            }
            RunNewGameNpcTurns();
        }
    }
    // まとめた検査に通らず1人ずつやり直した回数（この糸の中だけ。検査用）。
    [ThreadStatic]internal static int BatchRetries;
    // まとめた検査をわざと通らなくする（検査用）。
    [ThreadStatic]internal static bool FailBatchForTest;
    void RunNewGameNpcTurns()
    {
        foreach(var actor in NewGameNpcRoutine.Actors(state)){if(NpcJudgment.Enabled(state))ReceiveNpcLocalOffers(actor);var w=state;if(w.Paused)return;var i=NewGameNpcRoutine.Inputs(w,actor,w.ElapsedSeconds,w.CommitSequence);var hash=ContractJson.CanonicalHash(i);if(w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==actor)?.InputHash==hash)continue;var reason=NewGameNpcRoutine.Reason(i);EntityId? request=null;ResultCode result=ResultCode.NotAvailable;
            if(reason=="inspect-own-site"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NewGameWorld.InspectFor(w,actor,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="offer-local-inspection"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NpcLocalOffers.Offer(w,actor,i.Routine!.Judgment!.Recipient!.Value,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="practice-own-skill"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(CharacterDevelopment.PracticeForNpc(w,actor,request.Value.Value,i.Routine!.Purpose!.Skill),Revision).Code;}
            else if(reason=="yield-practice-to-known-duty")YieldNpcPractice(actor,i.Routine!.Purpose!.ActivePractice!.Value);
            RecordNpcWork(actor,w.CommitSequence,i,hash,reason,request,result);
        }
    }
}
