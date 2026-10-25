namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult DecideMilitary(MilitaryInput input,EntityId actor,long revision)
    {
        lock(gate){if(!CareerMilitary.Enabled(state)||actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の軍務ではありません");
        if(CareerMilitary.OtherIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"IDを別の案件で使用済みです");
        var old=state.Military!.Requests.SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>CareerMilitary.Dispatch(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"軍務手続きを受け付けました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"既知の許可・現地引受・帰還と返報を確認してください");}}
    }
}
