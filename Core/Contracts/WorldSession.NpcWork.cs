namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    [ThreadStatic]internal static bool SlowNpcDecisionRecord;
    internal void RecordNpcWork(EntityId actor,long inputRevision,NpcWorkInputs input,string hash,string reason,EntityId? request,ResultCode result)
    {
        lock(gate)
        {
            void Record(ContractWorld s)=>s.NpcWork=s.NpcWork! with{Decisions=[..s.NpcWork.Decisions,new(new("decision:npc-work/"+(s.NpcWork.Decisions.Length+1)),actor,s.ElapsedSeconds,inputRevision,input,hash,reason,request,result)]};
            // 手番の入力と実行は従来どおり。同期済みの同じ時刻に判断記録だけを足すとき、米・財布・地域帳簿を再生し直さない。
            // まとめた手番の最終検査と失敗時の逐次再実行は維持する。
            if(inPlace&&deferValidation&&NationalSystems.On(state,"NpcArmies")&&!SlowNpcDecisionRecord)
            {
                turnView=null;state.CommitSequence=checked(state.CommitSequence+1);Record(state);
            }
            else Change(Record);
        }
    }
}
