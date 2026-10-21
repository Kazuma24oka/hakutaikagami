using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult InterruptMission(EntityId taskId,EntityId actor,long expectedRevision)
    {
        lock(gate){
            var t=state.Tasks.SingleOrDefault(t=>t.Id==taskId);
            if(!MissionReview.Enabled(state)||t==null||actor!=t.Executor)return new(ResultCode.NotAvailable,taskId,state.CommitSequence,"本人の担当任務ではありません");
            if(t.InternalReason=="executor-interrupted")return new(ResultCode.AlreadyAccepted,taskId,state.CommitSequence,"中断済みです");
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,taskId,state.CommitSequence,"表示を更新してください");
            if(!MissionReview.CanInterrupt(this,taskId,actor))return new(ResultCode.NotAvailable,taskId,state.CommitSequence,"中断できる既知任務ではありません");
            Change(s=>{Stop(s,t,"executor-interrupted");s.Chronicle=[..s.Chronicle,new(Allocate(s,"chronicle"),actor,"本人判断による担当任務の中断: "+taskId,s.ElapsedSeconds)];FlushOutbox(s);});
            return new(ResultCode.Accepted,taskId,state.CommitSequence,"担当を中断しました。理由の返送は別操作です");
        }
    }
    public CommandResult ExplainMission(EntityId taskId,EntityId actor,MissionReview.Explanation reason,long expectedRevision)
    {
        lock(gate){
            var t=state.Tasks.SingleOrDefault(t=>t.Id==taskId);
            if(!MissionReview.Enabled(state)||t==null||actor!=t.Executor||!Enum.IsDefined(reason)||t.InternalReason!="executor-interrupted"||!MissionReview.KnownOrder(state,t,actor)||!state.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult))
                return new(ResultCode.NotAvailable,taskId,state.CommitSequence,"本人が中断した既知任務の理由だけ返送できます");
            var id=MissionReview.ExplanationId(taskId);string text=MissionReview.ExplanationText(reason);
            var old=state.Messaging.Messages.SingleOrDefault(m=>m.Id==id.Value);
            if(old!=null)return new(old.Body==text?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,taskId,state.CommitSequence,"理由書状は発送済みです");
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,taskId,state.CommitSequence,"表示を更新してください");
            Change(s=>MessageFlow.Send(s.Messaging,new(id.Value,t.RequestId.Value,MissionReview.ExplanationKind,actor.Value,t.Issuer.Value,text,t.Scope.Value,s.ElapsedSeconds),s.ElapsedSeconds,1));
            return new(ResultCode.Accepted,taskId,state.CommitSequence,"本人申告の理由書状を発送しました");
        }
    }
}
