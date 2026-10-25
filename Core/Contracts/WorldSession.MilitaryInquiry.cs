using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult RetryMilitaryRequest(string message,string failedAttempt,EntityId actor,long revision)
    {
        lock(gate){var id=new EntityId("request:military-retry");if(!CareerMilitary.Enabled(state)||actor!=state.PlayerControl||!state.Military!.Requests.Any(q=>q.Message==message))return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人が発送した軍務書状ではありません");
            var old=state.Messaging.Attempts.SingleOrDefault(a=>a.Id==failedAttempt&&a.MessageId==message);if(old!=null&&state.Messaging.Attempts.Any(a=>a.MessageId==message&&a.Number==old.Number+1))return new(ResultCode.AlreadyAccepted,id,state.CommitSequence,"この失敗試行の再送は受付済みです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            if(CareerMilitary.Retryable(state,actor) is not {} card||card.Message!=message||card.Attempt!=failedAttempt)return new(ResultCode.NotAvailable,id,state.CommitSequence,"直近の不達報告・期限・再送回数を確認してください");
            try{Change(w=>MessageFlow.Retry(w.Messaging,message,actor.Value,w.ElapsedSeconds,1));return new(ResultCode.Accepted,id,state.CommitSequence,"元のID・本文のまま既知の清洲宛てへ再送しました");}catch(Exception e)when(e is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"既知の不達と物理経路を確認してください");}
        }
    }
}
