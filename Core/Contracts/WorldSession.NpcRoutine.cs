namespace HakutaiKagami.Core.Contracts;

public sealed partial class WorldSession
{
    // Keep state private and hold the session gate for the ordered turn. Change()
    // copies before mutation, so a pre-submit generation remains valid for the
    // recorded input revision without serializing a diagnostic copy per actor.
    internal void TakeNewGameNpcTurns()=>RunBatchedTurns(RunNewGameNpcTurns);
    // NPC の手番をまとめて変更し、世界全体の検査は手番の終わりに1回だけ行う（休眠の版の世界）。通らなければ手番の前に戻して1件ずつやり直す。
    // allowInPlace=false：時間を進める変更を含むときは、前後を比べる処理のために変更ごとに写す（検査だけをまとめる）。
    void RunBatchedTurns(Action run,bool allowInPlace=true)
    {
        lock(gate)
        {
            // 休眠の版の世界（v109）：起きている人物の手番は1人ずつ変更し、世界全体の検査は手番の終わりに1回だけ行う。
            // まとめた検査に通らないとき（途中で例外が出たときも）は、手番の前の状態に戻し、1人ずつ検査する今までのやり方でやり直す。
            // 外側のまとめた変更の中（時間を進める1回の呼び出しの中など）では、外側の終わりの検査にまとめる。ここで検査して後回しの印を外すと、残りの変更が1件ずつ検査されてしまう。
            if(deferValidation)
            {
                // 外側が変更ごとに写していても、ここが写さずに変えてよい手番なら、1回だけ写してから直接変える（外側の手番の前の状態は変えない）。
                bool nest=allowInPlace&&!inPlace&&!(WorldTimeControl.Enabled(state)&&NpcArmies.Enabled(state)&&state.NpcArmies!.Musters.Length>0);
                if(nest){state=state.CopyForChange();turnView=null;inPlace=true;}
                try{run();}finally{if(nest)inPlace=false;}
                return;
            }
            if(NpcDormancy.Enabled(state))
            {
                // 手番の前の状態は1回だけ写して取っておき、手番の間は世界を写さずに直接変える（v110）。
                // 前後を比べる処理（軍勢が見えたときの時間の止め方）が働く世界では、今までどおり手番ごとに写す。
                var before=state;bool ok=true;deferValidation=true;batchBroken=false;
                inPlace=allowInPlace&&!(WorldTimeControl.Enabled(state)&&NpcArmies.Enabled(state)&&state.NpcArmies!.Musters.Length>0);
                if(inPlace){state=before.CopyForChange();turnView=null;}
                try{run();}
                catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException or InvalidOperationException){ok=false;}
                finally{deferValidation=false;inPlace=false;}
                if(batchBroken)ok=false;
                if(ok&&state.CommitSequence==before.CommitSequence){state=before;turnView=null;return;} // 何も変わらなかった
                if(ok&&!ReferenceEquals(before,state)){try{if(FailBatchForTest)throw new InvalidDataException("検査用の失敗");ContractValidation.Validate(state,before);}catch(InvalidDataException){ok=false;}}
                if(ok)return;
                state=before;turnView=null;BatchRetries++;
            }
            run();
        }
    }
    // まとめた検査に通らず1人ずつやり直した回数（この糸の中だけ。検査用）。
    [ThreadStatic]internal static int BatchRetries;
    // まとめた検査をわざと通らなくする（検査用）。
    [ThreadStatic]internal static bool FailBatchForTest;
    void RunNewGameNpcTurns()
    {
        var actors=NewGameNpcRoutine.Actors(state).ToArray();
        // 判断の入力を先に並べて求めておく（世界を読むだけ）。誰かが動いて世界が変わったら（変更の番号が進んだら）、その後の人は1人ずつ求め直す。
        var spec=new (NpcWorkInputs I,string Hash)?[actors.Length];long specRev=-1,specAt=-1;ContractWorld? specWorld=null;int specEnd=0;bool parallel=actors.Length>=32&&!NewGameNpcRoutine.Sequential;
        void Speculate(int from)
        {
            int end=actors.Length;var view=state;long rev=view.CommitSequence,at=view.ElapsedSeconds;long? ctxRev=ControlContext.Revision,ctxTime=ControlContext.Time;
            System.Threading.Tasks.Parallel.For(from,end,new System.Threading.Tasks.ParallelOptions{MaxDegreeOfParallelism=Math.Max(1,Math.Min(8,Environment.ProcessorCount))},n=>{
                using var _=ControlContext.Restore(ctxRev,ctxTime);var x=NewGameNpcRoutine.Inputs(view,actors[n],at,rev);spec[n]=(x,ContractJson.CanonicalHash(x));});
            specWorld=view;specRev=rev;specAt=at;specEnd=end;
        }
        for(int k=0;k<actors.Length;k++){var actor=actors[k];if(NpcJudgment.Enabled(state))ReceiveNpcLocalOffers(actor);var w=state;if(w.Paused)return;long revision=w.CommitSequence; // 判断の時点の番号（写さずに変える間は w が後で変わるので先に取る）
            bool valid=ReferenceEquals(w,specWorld)&&revision==specRev&&w.ElapsedSeconds==specAt&&k<specEnd;
            if(!valid&&parallel&&k==0){Speculate(0);valid=true;} // 最初の人まで（誰かが動くと、その後は1人ずつ求める）
            var (i,hash)=valid&&spec[k] is {} pre?pre:Fresh(w,actor,revision);if(w.NpcWork!.Decisions.LastOrDefault(d=>d.Actor==actor)?.InputHash==hash)continue;var reason=NewGameNpcRoutine.Reason(i);EntityId? request=null;ResultCode result=ResultCode.NotAvailable;
            if(reason=="inspect-own-site"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NewGameWorld.InspectFor(w,actor,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="offer-local-inspection"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(NpcLocalOffers.Offer(w,actor,i.Routine!.Judgment!.Recipient!.Value,request.Value.Value,w.ElapsedSeconds),Revision).Code;}
            else if(reason=="practice-own-skill"){request=new("request:npc-routine/"+actor.Value.Replace(':','/')+"/"+(i.OwnTasks.Length+1));result=Submit(CharacterDevelopment.PracticeForNpc(w,actor,request.Value.Value,i.Routine!.Purpose!.Skill),Revision).Code;}
            else if(reason=="yield-practice-to-known-duty")YieldNpcPractice(actor,i.Routine!.Purpose!.ActivePractice!.Value);
            RecordNpcWork(actor,revision,i,hash,reason,request,result);
        }
        static (NpcWorkInputs,string) Fresh(ContractWorld w,EntityId actor,long revision){var x=NewGameNpcRoutine.Inputs(w,actor,w.ElapsedSeconds,revision);return (x,ContractJson.CanonicalHash(x));}
    }
}
