namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult DecideNewMilitary(NewMilitaryInput input,EntityId actor,long revision)
    {
        lock(gate){if(!NewGameWorld.Enabled(state)||actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の軍務ではありません");
            var old=NewGameMilitary.LedgerStates(state).SelectMany(s=>s.Requests).SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みです");
            if(state.Requests.Any(q=>q.Id==input.Id)||NewGameService.OwnIdUsed(state,input.Id)||state.TravelDecisions?.Any(q=>q.Request.Id==input.Id)==true)return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の用件で使用したIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>NewGameMilitary.Dispatch(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"軍務の判断を受け付けました");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
        }
    }
}
