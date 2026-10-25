using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult PublishAssessment(EntityId messageId,EntityId taskId,EntityId assessor,long expectedRevision)
    {
        lock(gate){
            var t=state.Tasks.SingleOrDefault(t=>t.Id==taskId);
            if(!AssessmentNotice.Available(state)||t==null||t.Action!=ActionKind.Inspect||assessor!=t.Issuer)return new(ResultCode.NotAvailable,messageId,state.CommitSequence,"担当する評価者ではありません");
            var old=state.Messaging.Messages.SingleOrDefault(m=>m.Id==messageId.Value);
            if(old!=null)return new(old.Kind==AssessmentNotice.Kind&&old.SenderId==assessor.Value&&AssessmentNotice.Read(old.Body).Task==taskId?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,messageId,state.CommitSequence,"同じ書状IDは発送済みです");
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,messageId,state.CommitSequence,"表示を更新してください");
            if(!state.Characters.Any(c=>c.Id==assessor&&c.Life==LifeState.Adult)||!state.Grants.Any(g=>g.Agent==assessor&&g.Scope==t.Scope&&g.Actions.Contains(ActionKind.Inspect)&&g.From<=state.ElapsedSeconds&&state.ElapsedSeconds<g.Until))return new(ResultCode.NotAvailable,messageId,state.CommitSequence,"審査の権限がありません");
            var review=AssessmentNotice.ReviewAt(state,t,assessor,state.ElapsedSeconds);var chain=AssessmentNotice.Authored(state,taskId,assessor);
            if(review.Achievement=="結果未確認"||chain.Length>0&&ContractJson.CanonicalHash(AssessmentNotice.Read(chain[^1].Body).Review)==ContractJson.CanonicalHash(review))return new(ResultCode.NotAvailable,messageId,state.CommitSequence,"認識した新しい返報・理由が必要です");
            var p=new AssessmentNotice.Payload(taskId,assessor,t.Executor,chain.Length+1,chain.Length==0?null:new(chain[^1].Id),review);
            Change(s=>{MessageFlow.Send(s.Messaging,new(messageId.Value,t.RequestId.Value,AssessmentNotice.Kind,assessor.Value,t.Executor.Value,AssessmentNotice.Body(p),t.Scope.Value,s.ElapsedSeconds),s.ElapsedSeconds,1);s.SealedMessages=[..s.SealedMessages,messageId];NewGameWork.EnsureDebt(s,t,review);});
            return new(ResultCode.Accepted,messageId,state.CommitSequence,"暫定評価を根拠とともに封書で発送しました");
        }
    }
    public CommandResult RespondAssessment(EntityId noticeId,EntityId actor,AssessmentNotice.Reply reply,long expectedRevision)
    {
        lock(gate){
            var m=state.Messaging.Messages.SingleOrDefault(m=>m.Id==noticeId.Value&&m.Kind==AssessmentNotice.Kind);
            if(!AssessmentNotice.Available(state)||m==null||m.RecipientId!=actor.Value||!Enum.IsDefined(reply)||!AssessmentNotice.KnowsAt(state,m.Id,actor,state.ElapsedSeconds)||!state.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult))return new(ResultCode.NotAvailable,noticeId,state.CommitSequence,"本人が開封した評価だけに回答できます");
            var id=Child("message",noticeId,"response");var body=AssessmentNotice.Body(new AssessmentNotice.Response(noticeId,reply));var old=state.Messaging.Messages.SingleOrDefault(m=>m.Id==id.Value);
            if(old!=null)return new(old.Body==body?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,noticeId,state.CommitSequence,"回答は発送済みです");
            if(expectedRevision!=state.CommitSequence)return new(ResultCode.StateChanged,noticeId,state.CommitSequence,"表示を更新してください");
            Change(s=>MessageFlow.Send(s.Messaging,new(id.Value,m.CorrelationId,AssessmentNotice.ResponseKind,actor.Value,m.SenderId,body,m.Scope,s.ElapsedSeconds),s.ElapsedSeconds,1));
            return new(ResultCode.Accepted,noticeId,state.CommitSequence,"評価への回答を発送しました");
        }
    }
}
