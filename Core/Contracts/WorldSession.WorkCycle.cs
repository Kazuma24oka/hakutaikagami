namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult CollectWorkPayment(EntityId id,EntityId task,EntityId actor,long revision)
    {using var _ccRev=ControlContext.At(revision);
        lock(gate){if(!NewGameWork.Enabled(state)||(actor!=state.PlayerControl&&!NpcJudgment.Enabled(state))||!EntityId.IsValid(id.Value))return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の報酬ではありません");var old=state.WorkCycle!.Payments.SingleOrDefault(p=>p.Id==id);if(old!=null)return new(old.Task==task?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"同じ受渡しは確定済みです");if(state.Requests.Any(r=>r.Id==id)||NewGameService.OwnIdUsed(state,id)||NewGameMilitary.OwnIdUsed(state,id))return new(ResultCode.IdPayloadConflict,id,state.CommitSequence,"別の操作で使用したIDです");if(NewGameWork.Paid(state,task))return new(ResultCode.AlreadyAccepted,id,state.CommitSequence,"受領済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");if(!NewGameWork.CanRequest(this,task,actor,state))return new(ResultCode.NotAvailable,id,state.CommitSequence,"開封した返答を確認し、依頼主と現地で受け取ってください");try{var t=state.Tasks.Single(t=>t.Id==task);var message=AssessmentNotice.LatestKnownMessageFor(state,actor,task)!.Value.Message;/* 本人が知る最新の版の評価の通知（受信箱を作らずに求める） */Change(w=>NewGameWork.Pay(w,id,t,message));return new(ResultCode.Accepted,id,state.CommitSequence,"報酬の銭20文を現地で受け取りました");}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}}
    }
}
