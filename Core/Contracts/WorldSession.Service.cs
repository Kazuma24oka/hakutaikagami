namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult DecideService(ServiceInput input,EntityId actor,long revision)
    {
        lock(gate){
            if(!NewGameWorld.Enabled(state)||actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の仕官手続きではありません");
            var old=state.Service?.Requests.SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"申し出は受付済みです");
            if(VillageConsultation.OwnIdUsed(state,input.Id)||HouseSalary.OwnIdUsed(state,input.Id)||NewGameMilitary.OwnIdUsed(state,input.Id)||state.Requests.Any(q=>q.Id==input.Id)||state.TravelDecisions?.Any(q=>q.Request.Id==input.Id)==true)return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の手続きで使ったIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>NewGameService.Dispatch(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人の申し出を発送しました。返書を開いて確認してください。");}
            catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"面会できる相手・開封した条件・引継ぎを確認してください");}
        }
    }
}
