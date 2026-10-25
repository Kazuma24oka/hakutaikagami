namespace HakutaiKagami.Core.Contracts;
public sealed record FactionProclamationInput(FactionStanceInput Decision,FactionNoticeInput Notice);
public sealed partial class WorldSession
{
    internal void ReactToFactionNotice(FactionNoticeRequest source,FactionStanceInput decision){lock(gate){Change(w=>FactionNoticeReactions.Apply(w,source,decision));}}
    public CommandResult ProclaimFactionStance(FactionProclamationInput input,EntityId actor,long revision)
    {
        lock(gate){
            var d=input.Decision;var n=input.Notice;
            if(actor!=state.PlayerControl||!WorldClashes.Enabled(state)||!EntityId.IsValid(d.Id.Value)||!EntityId.IsValid(n.Id.Value)||d.Id==n.Id||n.Original!=(d.Action==FactionStanceAction.DeclareHostile?d.Id:d.Original))return new(ResultCode.NotAvailable,d.Id,state.CommitSequence,"本人の方針と通知の原本が一致しません");
            var old=state.FactionStances?.Decisions.SingleOrDefault(q=>q.Input.Id==d.Id);var letter=state.FactionNotices?.Requests.SingleOrDefault(q=>q.Input.Id==n.Id);
            if(old!=null&&old.Authority.Issuer!=actor||letter!=null&&letter.Sender!=actor)return new(ResultCode.NotAvailable,d.Id,state.CommitSequence,"本人の方針と通知ではありません");
            if(old!=null||letter!=null)return new(old?.Input==d&&letter?.Input==n&&old.Revision==letter.Revision?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,d.Id,state.CommitSequence,"受付済みの方針と通知です");
            if(FactionStances.OtherIdUsed(state,d.Id)||FactionStances.OtherIdUsed(state,n.Id)||FactionStances.OwnIdUsed(state,n.Id))return new(ResultCode.IdPayloadConflict,d.Id,state.CommitSequence,"別の操作で使ったIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,d.Id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>{FactionStances.Apply(w,d,actor);FactionNotices.Dispatch(w,n,actor);});return new(ResultCode.Accepted,d.Id,state.CommitSequence,"自家の方針と本拠への通知発送を記録しました。到達と相手の判断は別です");}
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,d.Id,state.CommitSequence,e.Message);}
        }
    }
    public CommandResult NotifyFactionStance(FactionNoticeInput input,EntityId actor,long revision)
    {
        lock(gate){
            if(actor!=state.PlayerControl||!EntityId.IsValid(input.Id.Value))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の方針通知ではありません");
            var old=state.FactionNotices?.Requests.SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null&&old.Sender!=actor)return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の通知ではありません");if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"受付済みの通知です");
            if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作で使ったIDです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"表示を更新してください");
            try{Change(w=>FactionNotices.Dispatch(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"既知の本拠へ方針の通知を発送しました。相手の到達・開封・判断は別です");}
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
        }
    }
}
