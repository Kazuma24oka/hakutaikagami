namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public RegionalEconomyView? LocalEconomyView(){lock(gate)return RegionalEconomy.View(state,state.PlayerControl);}
    public CommandResult ManageLocalEconomy(EconomyInput input,EntityId actor,long revision)
    {lock(gate){if(actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の判断ではありません");var old=state.Economy?.Orders.SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みです");if(FactionStances.OtherIdUsed(state,input.Id,includeEconomy:false)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>RegionalEconomy.Apply(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"現地の受渡し・工事を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or OverflowException or InvalidOperationException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
