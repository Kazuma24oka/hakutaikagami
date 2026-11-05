using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NpcLocalOfferTests
{
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    [Fact] public void PlayerInvitationRequiresOwnOpeningAndAssentThenPaysActualCoins()
    {
        var s=new WorldSession(NpcJudgmentTests.Start());At(s,10);var w=s.DiagnosticSnapshot();
        var task=Assert.Single(w.Tasks,t=>t.Executor==s.PlayerControl&&t.Issuer!=s.PlayerControl);
        Assert.Equal(AgreementPhase.AwaitingRecognition,task.Agreement);
        Assert.Equal(ExecutionPhase.NotStarted,task.Execution);
        var note=w.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="contract-order");
        Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(note.Id),s.Revision).Code);
        Assert.Equal(AgreementPhase.AwaitingAssent,s.DiagnosticSnapshot().Tasks.Single(t=>t.Id==task.Id).Agreement);
        Assert.Equal(ResultCode.Accepted,s.Assent(task.Id,s.PlayerControl,true,s.Revision).Code);At(s,30);
        var reply=s.DiagnosticSnapshot().Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="assessment-notice");
        Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,new(reply.Id),s.Revision).Code);
        var before=s.DiagnosticSnapshot();long issuer=NewGameWork.Balance(before,task.Issuer),recipient=NewGameWork.Balance(before,s.PlayerControl);
        Assert.Equal(ResultCode.Accepted,s.CollectWorkPayment(new("payment:judgment-player"),task.Id,s.PlayerControl,s.Revision).Code);
        var after=s.DiagnosticSnapshot();Assert.Equal(issuer-20,NewGameWork.Balance(after,task.Issuer));Assert.Equal(recipient+20,NewGameWork.Balance(after,s.PlayerControl));
        Assert.Equal(ContractJson.Write(after),ContractJson.Write(new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(after))).DiagnosticSnapshot()));
    }
    [Fact] public void NpcRecipientUsesDeliveryConsentWorkReportAndLocalPayment()
    {
        var s=new WorldSession(NpcJudgmentTests.Start());At(s,30);var w=s.DiagnosticSnapshot();
        var tasks=w.Tasks.Where(t=>t.Issuer!=t.Executor&&t.Executor!=s.PlayerControl).ToArray();Assert.NotEmpty(tasks);
        var done=Assert.Single(tasks,t=>t.Execution==ExecutionPhase.Completed);
        Assert.Equal(AgreementPhase.Active,done.Agreement);Assert.True(NewGameWork.Paid(w,done.Id));
        Assert.Contains(w.WorkCycle!.Openings,o=>w.Messaging.Messages.Any(m=>m.Id==o.Message&&m.RecipientId==done.Executor.Value));
        new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));
    }
    [Fact] public void PlayerCanDeclineWithoutExecutionOrReward()
    {
        var s=new WorldSession(NpcJudgmentTests.Start());At(s,10);var w=s.DiagnosticSnapshot();var task=w.Tasks.Single(t=>t.Executor==s.PlayerControl);
        var note=w.Messaging.Messages.Single(m=>m.CorrelationId==task.RequestId.Value&&m.Kind=="contract-order");s.Open(s.PlayerControl,new(note.Id),s.Revision);
        Assert.Equal(ResultCode.Accepted,s.Assent(task.Id,s.PlayerControl,false,s.Revision).Code);At(s,30);
        Assert.Equal(AgreementPhase.Declined,s.DiagnosticSnapshot().Tasks.Single(t=>t.Id==task.Id).Agreement);Assert.False(NewGameWork.Paid(s.DiagnosticSnapshot(),task.Id));
    }
}
