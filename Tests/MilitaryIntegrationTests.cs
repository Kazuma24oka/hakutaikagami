using HakutaiKagami.Core.Common;
using HakutaiKagami.Core.Messaging;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System.Text.Json.Nodes;
using Xunit;
namespace HakutaiKagami.Tests;

public class MilitaryIntegrationTests
{
    static TaxSimulation Create()
    {
        var s=TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"prototype.json"))));s.EnableOfficerIntegration();s.StartMilitaryMap();return s;
    }
    static void Step(TaxSimulation s,int n=1){for(int i=0;i<n;i++)s.AdvanceOneTick();}
    static void AdvanceBoth(TaxSimulation s)
    {s.SendMilitary("red-advance","character-001","unit-red",MilitaryAction.Advance,new(6,3));s.SendMilitary("blue-advance","character-002","unit-blue",MilitaryAction.Advance,new(4,3));}
    static MilitaryUnit Unit(TaxSimulation s,string id="unit-red")=>s.MilitaryDiagnostic!.Units.Single(u=>u.Id==id);
    [Fact] public void ArmiesUnitsAffiliationAndCommandAreSeparate()
    {
        var s=Create();string org=s.RestoreWorld().Characters["character-001"].OrganizationId;s.ReassignMilitaryCommander("unit-red","character-003","character-001");
        Assert.Equal("character-003",Unit(s).CommanderId);Assert.Equal("character-001",s.MilitaryDiagnostic!.Armies[0].CommanderId);Assert.Equal(org,s.RestoreWorld().Characters["character-003"].OrganizationId);
        Assert.DoesNotContain(s.MilitaryDiagnostic.Grants,g=>g.ActorId=="character-003");Assert.Throws<UnauthorizedAccessException>(()=>s.SendMilitary("bad","character-003","unit-red",MilitaryAction.Move,new(2,3)));
        s.SendMilitary("delegated","character-001","unit-red",MilitaryAction.Move,new(2,3));Assert.Equal("character-003",s.MilitaryDiagnostic.Orders.Single().RecipientId);
    }
    [Fact] public void WrongUnitOrRevokedGrantCannotDispatch()
    {
        var s=Create();string original=s.SaveJson();Assert.Throws<UnauthorizedAccessException>(()=>s.SendMilitary("bad","character-001","unit-blue",MilitaryAction.Move,new(8,3)));Assert.Equal(original,s.SaveJson());
        s.RevokeMilitaryGrant("grant-red");Assert.Throws<UnauthorizedAccessException>(()=>s.SendMilitary("bad2","character-001","unit-red",MilitaryAction.Move,new(2,3)));
    }
    [Fact] public void SharedMessageFlowOwnsPhysicalDeliveryAndReceipt()
    {
        var s=Create();var origin=Unit(s).Cell;s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(2,3),2);Assert.Equal(origin,Unit(s).Cell);Step(s);Assert.Equal(origin,Unit(s).Cell);
        Step(s);Assert.True(s.KnowsMessage(MilitaryFlow.OrderMessage("move"),"character-001"));Assert.Single(s.FormalReceipts);Assert.Single(s.MilitaryDiagnostic!.Applied);Assert.NotEqual(origin,Unit(s).Cell);
    }
    [Fact] public void LostOrderNeedsDeliveredFailureReportBeforeRetry()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));s.FailMessage(MilitaryFlow.OrderMessage("move"));Step(s);
        Assert.Equal(new MapCell(1,2),Unit(s).Cell);Assert.False(s.MilitaryViewFor("character-001").Orders.Single().RetryAvailable);
        Assert.Throws<InvalidOperationException>(()=>s.RetryMilitary("move","character-001"));
        s.ReportMessageFailure(MilitaryFlow.OrderMessage("move")+"/attempt/1","loss-report");Assert.Throws<InvalidOperationException>(()=>s.RetryMilitary("move","character-001"));Step(s);
        Assert.True(s.MilitaryViewFor("character-001").Orders.Single().RetryAvailable);s.RetryMilitary("move","character-001");Step(s);Assert.Single(s.MilitaryDiagnostic!.Applied);
    }
    [Theory] [InlineData(1)] [InlineData(3)]
    public void OriginalAndRetryArrivalCannotExecuteTwice(int lateDelay)
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));string m=MilitaryFlow.OrderMessage("move");s.FailMessage(m);Step(s);s.ReportMessageFailure(m+"/attempt/1","loss");Step(s);
        s.RetryMilitary("move","character-001");s.ScheduleLateMessage(m,1,lateDelay);Step(s,5);Assert.Single(s.MilitaryDiagnostic!.Applied);Assert.Single(s.FormalReceipts,r=>r.MessageId==m);
        Assert.Equal(1,s.MilitaryDiagnostic.Orders.Single().AppliedAt==3?1:0);
    }
    [Fact] public void LostFailureReportDoesNotTeachSenderOrEnableRetry()
    {
        var s=Create();string m=MilitaryFlow.OrderMessage("move");s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));s.FailMessage(m);Step(s);s.ReportMessageFailure(m+"/attempt/1","lost-failure");s.FailMessage("lost-failure");Step(s);
        Assert.False(s.MilitaryViewFor("character-001").Orders.Single().RetryAvailable);Assert.Throws<InvalidOperationException>(()=>s.RetryMilitary("move","character-001"));
    }
    [Fact] public void CancelArrivingBeforeDelayedOriginalStopsIt()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3),3);Step(s);s.SendMilitary("cancel","character-001","unit-red",MilitaryAction.Cancel,new(1,2),cancels:"move");Step(s,4);
        Assert.Equal(new MapCell(1,2),Unit(s).Cell);Assert.Equal(MilitaryOrderStatus.Cancelled,s.MilitaryDiagnostic!.Orders[0].Status);Assert.DoesNotContain("move",s.MilitaryDiagnostic.Applied);
    }
    [Fact] public void CancellationEffectTimeAndSameTickDependencyAreExplicit()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(6,3));s.SendMilitary("cancel","character-001","unit-red",MilitaryAction.Cancel,new(1,2),effectiveAt:3,cancels:"move");Step(s,2);var at2=Unit(s).Cell;Assert.NotEqual(new MapCell(1,2),at2);Step(s);
        Assert.Equal(at2,Unit(s).Cell);Assert.Equal(MilitaryOrderStatus.Cancelled,s.MilitaryDiagnostic!.Orders[0].Status);
        var t=Create();t.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(6,3));t.SendMilitary("cancel","character-001","unit-red",MilitaryAction.Cancel,new(1,2),cancels:"move");Step(t);Assert.Equal(new MapCell(1,2),Unit(t).Cell);
    }
    [Fact] public void DeadlineIsInclusiveAtStartAndRejectedAfterIt()
    {
        var a=Create();a.SendMilitary("ok","character-001","unit-red",MilitaryAction.Move,new(4,3),executeBy:1);Step(a);Assert.Contains("ok",a.MilitaryDiagnostic!.Applied);
        var b=Create();b.SendMilitary("late","character-001","unit-red",MilitaryAction.Move,new(4,3),2,executeBy:1);Step(b,3);Assert.Equal(new MapCell(1,2),Unit(b).Cell);Assert.Equal(MilitaryOrderStatus.Expired,b.MilitaryDiagnostic!.Orders.Single().Status);
        Assert.DoesNotContain(b.Messages,m=>m.Id==MilitaryFlow.ReplyMessage("late"));
    }
    [Fact] public void WaitingEffectCannotApplyAfterDeadlineOrGrantLoss()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3),executeBy:2,effectiveAt:4);Step(s,4);Assert.Empty(s.MilitaryDiagnostic!.Applied);Assert.Equal(new MapCell(1,2),Unit(s).Cell);
        var t=Create();t.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3),effectiveAt:3);Step(t);t.RevokeMilitaryGrant("grant-red");Step(t,2);Assert.Equal(MilitaryOrderStatus.Refused,t.MilitaryDiagnostic!.Orders.Single().Status);
    }
    [Fact] public void ArrivalChecksCommanderAndScriptedWillingness()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));s.SetMilitaryWillingness("unit-red",false);Step(s);Assert.Equal(MilitaryOrderStatus.Refused,s.MilitaryDiagnostic!.Orders.Single().Status);
        var t=Create();t.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));t.ReassignMilitaryCommander("unit-red","character-003","character-001");Step(t);Assert.Empty(t.MilitaryDiagnostic!.Applied);
    }
    [Fact] public void PayloadAndConcurrentOrderConflictsAreAtomic()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));string saved=s.SaveJson();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));Assert.Equal(saved,s.SaveJson());
        Assert.Throws<InvalidOperationException>(()=>s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(3,3)));Assert.Throws<InvalidOperationException>(()=>s.SendMilitary("other","character-001","unit-red",MilitaryAction.Advance,new(4,3)));Assert.Equal(saved,s.SaveJson());
    }
    [Fact] public void OrdersReachingSameEffectTimeDoNotUseRegistrationPriority()
    {
        var s=Create();s.SendMilitary("one","character-001","unit-red",MilitaryAction.Move,new(4,3),effectiveAt:3);Step(s);s.SendMilitary("two","character-001","unit-red",MilitaryAction.Move,new(3,3),effectiveAt:3);Step(s,2);
        Assert.Empty(s.MilitaryDiagnostic!.Applied);Assert.All(s.MilitaryDiagnostic.Orders,o=>Assert.Equal(MilitaryOrderStatus.Refused,o.Status));
    }
    [Fact] public void PathUsesBridgeAndWeightedRoadWithoutRiverSwimming()
    {
        var path=MilitaryMap.Route(new(1,2),new(9,4));Assert.Contains(new MapCell(5,3),path);Assert.All(path,c=>Assert.True(MilitaryMap.Walkable(c)));Assert.Equal(11,path.Sum(MilitaryMap.Cost));Assert.Equal(path,MilitaryMap.Route(new(1,2),new(9,4)));
        Assert.Throws<ArgumentException>(()=>MilitaryMap.Route(new(1,2),new(5,2)));
    }
    [Theory] [InlineData(5,0)] [InlineData(5,6)] [InlineData(4,1)] [InlineData(6,5)]
    public void ImpossibleDestinationCannotChangeSession(int x,int z)
    {var s=Create();string saved=s.SaveJson();Assert.ThrowsAny<Exception>(()=>s.SendMilitary("bad","character-001","unit-red",MilitaryAction.Move,new(x,z)));Assert.Equal(saved,s.SaveJson());}
    [Fact] public void WoodsOccludeSightAndUnknownEnemyHasNoRow()
    {
        Assert.False(MilitaryMap.CanSee(new(1,2),new(4,2)));var s=Create();Assert.DoesNotContain(s.MilitaryViewFor("character-001").Units,u=>u.UnitId=="unit-blue");Assert.Empty(s.MilitaryViewFor("character-003").Units);
    }
    [Fact] public void SightAndDelayedReportDoNotShareLiveEnemyState()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);var first=MilitaryFlow.LatestIntel(s.MilitaryDiagnostic!,"character-001","unit-blue")!;Assert.Equal(IntelKind.Sight,first.Kind);Assert.Equal(60,first.Soldiers);
        s.SendMilitaryIntel("intel","character-001","character-003",first.Id,3);Assert.Empty(s.MilitaryViewFor("character-003").Units);Step(s,2);Assert.Empty(s.MilitaryViewFor("character-003").Units);Step(s);
        var known=s.MilitaryViewFor("character-003").Units.Single(u=>u.UnitId=="unit-blue");Assert.Equal(60,known.Soldiers);Assert.False(known.Current);Assert.Equal(4,known.ObservedAt);Assert.Equal(36,Unit(s,"unit-blue").Soldiers);
        Step(s,5);var stale=s.MilitaryViewFor("character-003").Units.Single();Assert.Equal(60,stale.Soldiers);Assert.Equal(new MapCell(6,3),stale.Cell);Assert.Equal(24,Unit(s,"unit-blue").Soldiers);
    }
    [Fact] public void CannotReportAnObservationTheSenderNeverLearned()
    {
        var s=Create();var own=s.MilitaryDiagnostic!.Intel.First(i=>i.ObserverId=="character-002");Assert.Throws<UnauthorizedAccessException>(()=>s.SendMilitaryIntel("bad","character-001","character-003",own.Id));
    }
    [Fact] public void LateOldReportCannotOverwriteNewDirectObservation()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);var old=MilitaryFlow.LatestIntel(s.MilitaryDiagnostic!,"character-002","unit-blue")!;
        s.SendMilitaryIntel("old","character-002","character-001",old.Id,3);Step(s,3);var view=s.MilitaryViewFor("character-001").Units.Single(u=>u.UnitId=="unit-blue");Assert.Equal(36,view.Soldiers);Assert.True(view.Current);Assert.Equal(7,view.ObservedAt);
    }
    [Fact] public void LostBattleReportDoesNotRevealItsBody()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);var fact=MilitaryFlow.LatestIntel(s.MilitaryDiagnostic!,"character-001","unit-blue")!;s.SendMilitaryIntel("intel","character-001","character-003",fact.Id);s.FailMessage("intel/military-intel");Step(s,3);Assert.Empty(s.MilitaryViewFor("character-003").Units);
    }
    [Fact] public void BattlefieldAndTaxSharePauseRollbackAndRestart()
    {
        var a=Create();AdvanceBoth(a);a.Submit("tax","character-001","castle-001",60);a.SetPaused(true);string paused=a.SaveJson();Step(a,3);Assert.Equal(paused,a.SaveJson());a.SetPaused(false);
        string before=a.SaveJson();Assert.Throws<IOException>(()=>a.AdvanceOneTick(()=>throw new IOException("injected")));Assert.Equal(before,a.SaveJson());var b=TaxSimulation.LoadJson(a.SaveJson());
        for(int i=0;i<18;i++){a.AdvanceOneTick();b=TaxSimulation.LoadJson(b.SaveJson());b.AdvanceOneTick();Assert.Equal(a.SaveJson(),b.SaveJson());}
        Assert.Equal(160,a.TreasuryRice);Assert.Equal(MilitaryPhase.Exited,Unit(a,"unit-blue").Phase);Assert.Equal(24,Unit(a,"unit-blue").Soldiers);
    }
    [Fact] public void MoveWithoutBothAdvanceOrdersDoesNotStartCombat()
    {var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(6,3));s.SendMilitary("blue","character-002","unit-blue",MilitaryAction.Move,new(4,3));Step(s,14);Assert.Empty(s.MilitaryDiagnostic!.Losses);Assert.Equal(2,s.MilitaryDiagnostic.Units.Select(u=>u.Cell).Distinct().Count());}
    [Fact] public void DifferentArmiesDoNotAutomaticallyCreateHostility()
    {
        var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Military"]!["Hostilities"]=new JsonArray();s=TaxSimulation.LoadJson(n.ToJsonString());AdvanceBoth(s);Step(s,12);Assert.Empty(s.MilitaryDiagnostic!.Losses);
    }
    [Fact] public void WithdrawalFindsOwnExitAcrossBridgeAndKeepsSurvivors()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(7,3));Step(s,9);Assert.True(Unit(s).Cell.X>=6);s.SendMilitary("withdraw","character-001","unit-red",MilitaryAction.Withdraw,new(0,3));Step(s,20);
        Assert.Equal(MilitaryPhase.Exited,Unit(s).Phase);Assert.Equal(new MapCell(0,3),Unit(s).Cell);Assert.Equal(100,Unit(s).Soldiers);Assert.Equal(3,s.RestoreWorld().Characters.Count);
    }
    [Theory] [InlineData("Tick",999)] [InlineData("Schema",99)]
    public void CorruptMilitaryVersionAndClockAreRejected(string key,int value)
    {var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Military"]![key]=value;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));}
    [Fact] public void CorruptRouteAndIntelAreRejected()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Military"]!["Units"]![0]!["Route"]![0]!["Z"]=0;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
        var fact=MilitaryFlow.LatestIntel(s.MilitaryDiagnostic!,"character-001","unit-blue")!;s.SendMilitaryIntel("intel","character-001","character-003",fact.Id);Step(s);n=JsonNode.Parse(s.SaveJson())!;
        var intel=n["Officers"]!["Military"]!["Intel"]!.AsArray().Single(i=>i!["Kind"]!.GetValue<string>()=="Report")!;intel["Soldiers"]=999;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Fact] public void FormalProxyReceiptIsNotPrincipalKnowledgeOrExecution()
    {
        var s=Create();s.SetProxyPermission(new("p","character-001","character-003",MilitaryFlow.Scope,"military-command","試験代理受領",0,20,true,false));
        s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3),executeBy:3);string mid=MilitaryFlow.OrderMessage("move");s.RouteMessage(mid,"character-003");Step(s);
        Assert.True(s.FormalReceipts.Single().ByProxy);Assert.False(s.KnowsMessage(mid,"character-001"));Assert.Empty(s.MilitaryDiagnostic!.Applied);
        s.SetProxyPermission(new("open","character-001","character-003",MilitaryFlow.Scope,"military-command","試験代理開封",0,20,true,true));s.OpenProxyMessage(mid,"character-003");Assert.Empty(s.MilitaryDiagnostic.Applied);
        s.ForwardMessage(mid,"character-003","forward-military");Step(s);Assert.Contains("move",s.MilitaryDiagnostic!.Applied);Assert.Equal(2,s.MilitaryDiagnostic.Orders.Single().ReceivedAt);
    }
    [Fact] public void ResultReplyRetryDoesNotReexecuteTheOrder()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(4,3));Step(s);string reply=MilitaryFlow.ReplyMessage("move");s.FailMessage(reply);Step(s);
        Assert.DoesNotContain("Applied:",s.MilitaryViewFor("character-001").Orders.Single().KnownStatus);s.ReportMessageFailure(reply+"/attempt/1","reply-lost");Step(s);s.RetryMilitaryResult("move","character-001");Step(s);
        Assert.Contains("Applied:",s.MilitaryViewFor("character-001").Orders.Single().KnownStatus);Assert.Single(s.MilitaryDiagnostic!.Applied);
    }
    [Fact] public void ReportRetryPreservesOriginalObservedDateAndValues()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);var fact=MilitaryFlow.LatestIntel(s.MilitaryDiagnostic!,"character-001","unit-blue")!;s.SendMilitaryIntel("intel","character-001","character-003",fact.Id);s.FailMessage("intel/military-intel");Step(s);
        Assert.Throws<InvalidOperationException>(()=>s.RetryMilitaryIntel("intel","character-001"));s.ReportMessageFailure("intel/military-intel/attempt/1","intel-lost");Step(s);s.RetryMilitaryIntel("intel","character-001");Step(s);
        var view=s.MilitaryViewFor("character-003").Units.Single();Assert.Equal(4,view.ObservedAt);Assert.Equal(60,view.Soldiers);Assert.Equal(36,Unit(s,"unit-blue").Soldiers);
    }
    [Fact] public void LostCancellationAndLateCopyOnlyStopOnActualReceptionOnce()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(6,3));Step(s);s.SendMilitary("cancel","character-001","unit-red",MilitaryAction.Cancel,new(0,3),cancels:"move");string m=MilitaryFlow.OrderMessage("cancel");s.FailMessage(m);Step(s);
        Assert.Equal(MilitaryOrderStatus.Applied,s.MilitaryDiagnostic!.Orders[0].Status);s.ReportMessageFailure(m+"/attempt/1","cancel-lost");Step(s);var before=Unit(s).Cell;s.RetryMilitary("cancel","character-001");s.ScheduleLateMessage(m,1,1);Step(s);
        Assert.Equal(before,Unit(s).Cell);Assert.Equal(2,s.MilitaryDiagnostic!.Applied.Length);Assert.Equal(MilitaryOrderStatus.Cancelled,s.MilitaryDiagnostic.Orders[0].Status);
    }
    [Fact] public void WeightedCellProgressSurvivesSaveBeforeMovementCompletes()
    {
        var s=Create();s.SendMilitary("plain","character-001","unit-red",MilitaryAction.Move,new(2,2));Step(s);Assert.Equal(new MapCell(1,2),Unit(s).Cell);Assert.Equal(1,Unit(s).MoveBudget);
        s=TaxSimulation.LoadJson(s.SaveJson());Step(s);Assert.Equal(new MapCell(2,2),Unit(s).Cell);Assert.Equal(0,Unit(s).MoveBudget);
    }
    [Fact] public void RestoredBattleDoesNotForgetPerObserverMemoryOrAuthority()
    {
        var s=Create();AdvanceBoth(s);Step(s,4);s.SendKnownMilitaryIntel("intel","character-001","character-003","unit-blue",2);Step(s,2);s.RevokeMilitaryGrant("grant-red");
        var restored=TaxSimulation.LoadJson(s.SaveJson());Assert.Equal(SnapshotCodec.Write(s.MilitaryViewFor("character-003")),SnapshotCodec.Write(restored.MilitaryViewFor("character-003")));
        Assert.Throws<UnauthorizedAccessException>(()=>restored.SendMilitary("new","character-001","unit-red",MilitaryAction.Move,new(4,3)));
    }
    [Fact] public void RestoredPathCannotRedirectAValidCommandToAnotherDestination()
    {
        var s=Create();s.SendMilitary("move","character-001","unit-red",MilitaryAction.Move,new(6,3));Step(s);var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Military"]!["Units"]![0]!["Route"]!.AsArray().RemoveAt(4);
        Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Fact] public void OneCommanderCannotOccupyTwoFieldUnitsAtOnce()
    {
        var s=Create();string before=s.SaveJson();Assert.ThrowsAny<Exception>(()=>s.ReassignMilitaryCommander("unit-red","character-002","character-001"));Assert.Equal(before,s.SaveJson());
    }
    [Fact] public void CancellingReplacedOrderDoesNotStopItsSuccessor()
    {
        var s=Create();s.SendMilitary("one","character-001","unit-red",MilitaryAction.Move,new(4,3));Step(s);s.SendMilitary("two","character-001","unit-red",MilitaryAction.Move,new(6,3));Step(s);
        var before=Unit(s).Cell;s.SendMilitary("cancel-old","character-001","unit-red",MilitaryAction.Cancel,new(0,3),cancels:"one");Step(s);Assert.NotEqual(before,Unit(s).Cell);Assert.Equal("two",Unit(s).ActiveOrder);
    }
    [Fact] public void WithdrawalDestinationMustBelongToTheArmy()
    {var s=Create();string before=s.SaveJson();Assert.Throws<ArgumentException>(()=>s.SendMilitary("bad","character-001","unit-red",MilitaryAction.Withdraw,new(10,3)));Assert.Equal(before,s.SaveJson());}
}
