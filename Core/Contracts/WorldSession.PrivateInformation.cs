namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public InformationCard[] PrivateInformationView(){lock(gate)return PrivateInformation.View(state);}
    public CommandResult ContactForInformation(InformationInput input,EntityId actor,long revision)
    {lock(gate){if(actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の私的な相談ではありません");var old=state.PrivateInformation?.Orders.SingleOrDefault(o=>o.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"相談済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"状況を確認し直してください");try{Change(w=>PrivateInformation.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"現地の情報交換を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
