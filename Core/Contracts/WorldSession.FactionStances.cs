namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult DecideFactionStance(FactionStanceInput input,EntityId actor,long revision)
    {
        lock(gate){
            if(!NewGameWorld.Enabled(state)||actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の政治判断ではありません");
            var old=state.FactionStances?.Decisions.SingleOrDefault(d=>d.Input.Id==input.Id);
            if(old!=null&&old.Authority.Issuer!=actor)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の政治判断ではありません");
            if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みの判断です");
            if(FactionStances.OtherIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作で使ったIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>FactionStances.Apply(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"自家の意思を記録しました。他家や配下への伝達は別です");}
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
        }
    }
}
