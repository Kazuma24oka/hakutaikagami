namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult RequestTransport(EntityId id,long revision)
    {
        lock(gate){var old=TransportWork.Jobs(state).SingleOrDefault(j=>j.Handover.Terms.Id==id);if(old!=null)return new(ResultCode.AlreadyAccepted,id,state.CommitSequence,"この依頼は作成済みです");if(!EntityId.IsValid(id.Value))return new(ResultCode.InvalidInput,id,state.CommitSequence,"依頼IDが不正です");if(NewGameWork.OwnIdUsed(state,id)||state.Requests.Any(r=>r.Id==id))return new(ResultCode.IdPayloadConflict,id,state.CommitSequence,"別の操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");try{var terms=TransportWork.OfferAt(state,id)??throw new InvalidDataException("この場所に運搬の依頼はありません");Change(w=>TransportWork.Create(w,terms));return new(ResultCode.Accepted,id,state.CommitSequence,"運搬の条件を確認してください");}catch(InvalidDataException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"この場所で運搬の依頼を準備できません");}}
    }
    public CommandResult HandoverTransport(EntityId job,HandoverInput input,long revision)
    {
        lock(gate){var j=TransportWork.Jobs(state).SingleOrDefault(j=>j.Handover.Terms.Id==job);if(j==null||input.Actor!=state.PlayerControl&&input.Action!=HandoverAction.Cancel)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の運搬ではありません");var old=j.Handover.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"この受渡しは確定済みです");if(NewGameWork.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作のIDです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");if(input.At!=state.ElapsedSeconds)return new(ResultCode.InvalidInput,input.Id,state.CommitSequence,"現在の時刻で判断してください");try{Change(w=>TransportWork.Apply(w,job,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"受渡しを記録しました");}catch(InvalidDataException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"現地の相手・荷物・期限を確認してください");}}
    }
}
