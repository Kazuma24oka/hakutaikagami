namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult DecideCareer(CareerInput input,EntityId actor,long revision)
    {
        lock(gate){if(!Career.Enabled(state)||actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の仕官案件ではありません");if(CareerMilitary.OwnIdUsed(state,input.Id)||state.Requests.Any(r=>r.Id==input.Id)||state.Region!.Commands.Any(c=>c.Request.Id==input.Id)||state.Field!.Orders.Any(o=>o.Request.Id==input.Id)||state.TravelDecisions!.Any(d=>d.Request.Id==input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"同一IDの内容が異なります");var old=state.Career!.Requests.SingleOrDefault(r=>r.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"同じ申し出を受付済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");try{Change(w=>Career.Dispatch(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"本人の申し出を発送しました。結果は返書で確認してください");}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"既知の条件・面談場所・手続き中の案件を確認してください");}}
    }
}
