namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public StanddownCard[] StanddownView(){lock(gate)return PrivateStanddown.View(state);}
    public CommandResult NegotiateStanddown(StanddownInput input,EntityId actor,long revision)
    {lock(gate){if(actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の私的な相談ではありません");var old=state.Standdown?.Orders.SingleOrDefault(o=>o.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"相談済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"状況を確認し直してください");try{Change(w=>PrivateStanddown.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人との条件と実際の支払いを記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
