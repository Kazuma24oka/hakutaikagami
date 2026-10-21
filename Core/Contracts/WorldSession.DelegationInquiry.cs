using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult QueryDelegation(DelegationQuestion question,EntityId actor,long revision)
    {
        lock(gate){var id=question.Id;if(actor!=state.PlayerControl||!DelegationMission.Enabled(state))return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の委任案件ではありません");var old=state.Messaging.Messages.SingleOrDefault(m=>m.Id==DelegationInquiry.MessageId(id));if(old!=null)return new(old.Body==System.Text.Encoding.UTF8.GetString(ContractJson.Write(question))?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"同じ照会を受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");try{Change(w=>DelegationInquiry.Send(w,question,actor));return new(ResultCode.Accepted,id,state.CommitSequence,"既知の営地または指定先へ照会を発送しました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の発送済み命令を指定してください");}}
    }
}
