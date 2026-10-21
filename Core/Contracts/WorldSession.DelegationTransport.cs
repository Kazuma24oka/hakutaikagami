using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult RetryDelegation(string message,string failedAttempt,EntityId actor,FieldPoint address,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:delegation-retry");
            if(!DelegationMission.Enabled(state)||actor!=state.PlayerControl)return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の委任命令ではありません");
            var old=state.Messaging.Attempts.SingleOrDefault(a=>a.Id==failedAttempt&&a.MessageId==message);
            if(old!=null&&state.Messaging.Messages.Any(m=>m.Id==message&&m.SenderId==actor.Value)&&state.Messaging.Attempts.SingleOrDefault(a=>a.MessageId==message&&a.Number==old.Number+1) is {} next)
            {
                var delivery=state.Region!.Deliveries.SingleOrDefault(d=>d.Attempt==next.Id);
                return new(delivery?.Address==address?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"この失敗試行の再送は受付済みです");
            }
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>DelegationTransport.Retry(w,message,failedAttempt,actor,address));return new(ResultCode.Accepted,id,state.CommitSequence,"既知の宛先へ原本と同じ内容を再送しました");}
            catch(Exception e)when(e is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"不達の確認・宛先・再送回数を確認してください");}
        }
    }
}
