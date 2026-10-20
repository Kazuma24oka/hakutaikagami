using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class MovementControlTests
{
    static WorldSession New()=>new(MovementControl.Create());
    static EntityId Start(WorldSession s){s.Submit(BranchRoutes.Order(s.DiagnosticSnapshot(),"request:move"),s.Revision);s.AdvanceTo(5);return s.DiagnosticSnapshot().Tasks.Single().Id;}
    static Intent Send(WorldSession s,EntityId target,ActionKind kind,long at=25){var i=MovementControl.Order(s.DiagnosticSnapshot(),"request:control",target,kind,at);Assert.Equal(ResultCode.Accepted,s.Submit(i,s.Revision).Code);return i;}
    static void Open(WorldSession s){var m=s.GetActorView(ContractFixture.Officer).Messages.Single(m=>m.Sealed);Assert.Equal(ResultCode.Accepted,s.Open(ContractFixture.Officer,m.Id,s.Revision).Code);}
    [Fact] public void CancellationSeparatesDispatchDeliveryOpeningAndEffect()
    {var s=New();var target=Start(s);Send(s,target,ActionKind.Cancel);s.AdvanceTo(100);Assert.Equal(21,s.Seconds);Assert.True(s.Paused);Assert.True(s.GetBranchView(ContractFixture.Officer).Walking);Assert.Null(s.GetActorView(ContractFixture.Officer).Messages.Single(m=>m.Sealed).KnownBody);
        s.SetPaused(false);s.AdvanceTo(23);Open(s);s.AdvanceTo(24);Assert.True(s.GetBranchView(ContractFixture.Officer).Walking);s.AdvanceTo(25);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);Assert.Equal(new RoutePoint(48000,0),s.GetBranchView(ContractFixture.Officer).Position);s.AdvanceTo(90);Assert.Equal(new RoutePoint(48000,0),s.GetBranchView(ContractFixture.Officer).Position);}
    [Fact] public void RedirectCompletesCurrentEdgeThenReturnsToCastle()
    {var s=New();var target=Start(s);Send(s,target,ActionKind.Redirect);s.AdvanceTo(21);s.SetPaused(false);s.AdvanceTo(23);Open(s);s.AdvanceTo(25);
        Assert.Equal(new RoutePoint(48000,0),s.GetBranchView(ContractFixture.Officer).Position);Assert.Equal(81,s.GetBranchView(ContractFixture.Officer).ArrivalAt);
        s.AdvanceTo(41);Assert.Equal(new RoutePoint(80000,0),s.GetBranchView(ContractFixture.Officer).Position);Assert.True(s.GetBranchView(ContractFixture.Officer).Heading.EastMm<0);
        s.AdvanceTo(81);Assert.Equal(ContractFixture.Castle,s.GetActorView(ContractFixture.Officer).Location.Site);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);}
    [Fact] public void UnopenedOrderHasNoEffectEvenAfterSpecifiedTime()
    {var s=New();Send(s,Start(s),ActionKind.Cancel);s.AdvanceTo(21);s.SetPaused(false);s.AdvanceTo(40);Assert.True(s.GetBranchView(ContractFixture.Officer).Walking);Open(s);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);Assert.Equal(new RoutePoint(78000,0),s.GetBranchView(ContractFixture.Officer).Position);}
    [Fact] public void RendezvousDelayDoesNotReadHiddenPositionOrClosure()
    {var a=New();var b=New();var ta=Start(a);var tb=Start(b);var wa=a.DiagnosticSnapshot();var wb=b.DiagnosticSnapshot();wb.Branches=wb.Branches! with{BlockedEdges=[]};Assert.Equal(ContractJson.Write(MovementControl.Order(wa,"request:x",ta,ActionKind.Cancel)),ContractJson.Write(MovementControl.Order(wb,"request:x",tb,ActionKind.Cancel)));}
    [Fact] public void MissedMeetingCannotCreateRemoteReceiptAndFailureReportIsDelayed()
    {var s=New();var target=Start(s);s.AdvanceTo(22);Send(s,target,ActionKind.Cancel);s.AdvanceTo(32);Assert.False(s.Paused);Assert.True(s.GetBranchView(ContractFixture.Officer).Walking);Assert.DoesNotContain(s.GetActorView(ContractFixture.Officer).Messages,m=>m.Sealed);
        Assert.DoesNotContain(s.GetActorView(ContractFixture.Lord).Messages,m=>m.KnownBody?.Contains("attempt")==true);s.AdvanceTo(42);Assert.Contains(s.GetActorView(ContractFixture.Lord).Messages,m=>m.KnownBody?.Contains("attempt")==true);}
    [Theory][InlineData(ActionKind.Cancel)][InlineData(ActionKind.Redirect)]
    public void ReplayedRequestBeforeAndAfterEffectCannotExecuteTwice(ActionKind kind)
    {var s=New();var target=Start(s);var i=Send(s,target,kind);Assert.Equal(ResultCode.AlreadyAccepted,s.Submit(i,s.Revision).Code);s.AdvanceTo(21);Open(s);s.SetPaused(false);s.AdvanceTo(90);var bytes=ContractJson.Write(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.Submit(i,s.Revision).Code);Assert.Equal(bytes,ContractJson.Write(s.DiagnosticSnapshot()));Assert.Equal(2,s.DiagnosticSnapshot().Tasks.Length);Assert.Equal(2,s.DiagnosticSnapshot().Outbox.Length);}
    [Theory][InlineData(20)][InlineData(21)][InlineData(24)][InlineData(25)][InlineData(50)]
    public void InTransitUnopenedScheduledAndAppliedCommandsRoundTrip(int at)
    {var s=New();var target=Start(s);Send(s,target,ActionKind.Redirect);s.AdvanceTo(Math.Min(at,21));if(at>21){Open(s);s.SetPaused(false);s.AdvanceTo(at);}var root=Path.Combine(Path.GetTempPath(),"hakutai-control-"+Guid.NewGuid().ToString("N"));var store=new GenerationSaveStore(root,MovementControl.Create().Definitions);store.Save(s,"test",s.DiagnosticSnapshot().WorldId,new(.25,1));var c=store.Prepare("test");var host=new WorldSessionHost(New());Assert.Equal(ResultCode.Accepted,host.CommitLoad(c,host.Current,host.Current.Revision));
        Assert.Equal(s.GetBranchView(ContractFixture.Officer).Position,host.Current.GetBranchView(ContractFixture.Officer).Position);Assert.Equal(s.GetBranchView(ContractFixture.Officer).PlannedEdges,host.Current.GetBranchView(ContractFixture.Officer).PlannedEdges);Assert.True(host.Current.Paused);}
    [Fact] public void CancellationWinsAgainstUncommittedArrivalAtSameSecond()
    {var s=New();Send(s,Start(s),ActionKind.Cancel,141);s.AdvanceTo(21);Open(s);s.SetPaused(false);s.AdvanceTo(141);Assert.Equal(ExecutionPhase.Stopped,s.DiagnosticSnapshot().Tasks[0].Execution);Assert.DoesNotContain(s.DiagnosticSnapshot().EffectLedger,e=>e.BusinessKey=="task:1/finish/1");}
    [Theory][InlineData(ActionKind.Cancel)][InlineData(ActionKind.Redirect)]
    public void LegalLateOriginalAndRetryArriveTogetherWithoutRestartOrDuplicateEffect(ActionKind kind)
    {var s=New();var target=Start(s);var stop=MovementControl.Order(s.DiagnosticSnapshot(),"request:stop-first",target,ActionKind.Cancel,21,false);s.Submit(stop,s.Revision);s.AdvanceTo(6);
        var i=MovementControl.Order(s.DiagnosticSnapshot(),"request:lost",target,kind);s.Submit(i,s.Revision);var task=s.DiagnosticSnapshot().Tasks.Last();var mid="message:"+task.Id.Value.Replace(':','/')+"/order";
        s.DiagnosticTransport((m,_)=>MessageFlow.Fail(m,mid));s.AdvanceTo(31);Assert.Equal(new RoutePoint(40000,0),s.GetBranchView(ContractFixture.Officer).Position);
        s.DiagnosticTransport((m,t)=>{MessageFlow.Retry(m,mid,ContractFixture.Lord.Value,t,1);MessageFlow.ScheduleLate(m,mid,1,t,1);});s.AdvanceTo(32);Assert.Equal(2,s.DiagnosticSnapshot().Messaging.Attempts.Count(a=>a.MessageId==mid&&a.Status==AttemptStatus.Delivered));
        Open(s);s.SetPaused(false);s.AdvanceTo(90);var w=s.DiagnosticSnapshot();Assert.Single(w.Outbox,o=>o.Task==task.Id);Assert.Equal(new RoutePoint(40000,0),s.GetBranchView(ContractFixture.Officer).Position);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);
        if(kind==ActionKind.Redirect)Assert.Single(w.EffectLedger,e=>e.Task==task.Id&&e.BusinessKey==task.Id.Value+"/finish/1");}
    [Theory][InlineData(true)][InlineData(false)]
    public void SameTimeCancellationPrecedesUncommittedRedirectRegardlessOfRegistration(bool cancelFirst)
    {var s=New();var target=Start(s);var cancel=MovementControl.Order(s.DiagnosticSnapshot(),"request:cancel",target,ActionKind.Cancel,25,false);var redirect=MovementControl.Order(s.DiagnosticSnapshot(),"request:redirect",target,ActionKind.Redirect,25,false);
        foreach(var i in cancelFirst?new[]{cancel,redirect}:new[]{redirect,cancel})s.Submit(i,s.Revision);s.AdvanceTo(90);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);Assert.Equal(new RoutePoint(48000,0),s.GetBranchView(ContractFixture.Officer).Position);Assert.Equal(ExecutionPhase.Stopped,s.DiagnosticSnapshot().Tasks[0].Execution);}

    [Fact] public void RendezvousRespectsKnownFutureEffectiveStart()
    {var s=New();s.Submit(BranchRoutes.Order(s.DiagnosticSnapshot(),"request:future") with{EffectiveAt=100},0);s.AdvanceTo(5);var target=s.DiagnosticSnapshot().Tasks[0].Id;var command=Send(s,target,ActionKind.Cancel);Assert.Equal(115,command.DeliverySeconds);s.AdvanceTo(21);Assert.DoesNotContain(s.GetActorView(ContractFixture.Officer).Messages,m=>m.Sealed);s.AdvanceTo(120);Assert.True(s.Paused);Open(s);Assert.False(s.GetBranchView(ContractFixture.Officer).Walking);Assert.Equal(new RoutePoint(40000,0),s.GetBranchView(ContractFixture.Officer).Position);}

}
