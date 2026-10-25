namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    internal void RecordNpcWork(EntityId actor,long inputRevision,NpcWorkInputs input,string hash,string reason,EntityId? request,ResultCode result)
    {
        lock(gate)Change(s=>s.NpcWork=s.NpcWork! with{Decisions=[..s.NpcWork.Decisions,new(new("decision:npc-work/"+(s.NpcWork.Decisions.Length+1)),actor,s.ElapsedSeconds,inputRevision,input,hash,reason,request,result)]});
    }
}
