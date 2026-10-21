using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed partial class WorldSession
{
    public CommandResult ReportEngagement(EntityId id,EntityId actor,long revision){lock(gate){
        if(!EntityId.IsValid(id.Value)||!EngagementMission.Enabled(state)||actor!=state.PlayerControl)return new(ResultCode.NotAvailable,id,state.CommitSequence,"本人の交戦任務ではありません");
        if(state.Engagement!.Reports.Any(r=>r.Id==id))return new(ResultCode.AlreadyAccepted,id,state.CommitSequence,"報告済みです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
        var message=Child("message",id,"field-report").Value;var report=new EngagementReport(id,actor,state.Engagement.Terms.Principal,state.ElapsedSeconds,EngagementMission.Snapshot(state,state.ElapsedSeconds),message);
        try{Change(s=>{s.Engagement=s.Engagement! with{Reports=[..s.Engagement.Reports,report]};MessageFlow.Send(s.Messaging,new(message,id.Value,EngagementMission.Kind,actor.Value,report.Recipient.Value,EngagementMission.Body(report),ContractFixture.Castle.Value,s.ElapsedSeconds),s.ElapsedSeconds,1);});return new(ResultCode.Accepted,id,state.CommitSequence,"自隊の現況を発送しました。主君への到達は別です");}catch(InvalidDataException){return new(ResultCode.NotAvailable,id,state.CommitSequence,"現在地からの報告経路がありません");}
    }}
}
