using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System.Text.Json.Nodes;
using Xunit;
namespace HakutaiKagami.Tests;
public class FormationTests
{
    static TaxSimulation Create(){var s=TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"prototype.json"))));s.EnableOfficerIntegration();s.StartFormationMap();return s;}
    static void Step(TaxSimulation s,int n=1){for(int i=0;i<n;i++)s.AdvanceOneTick();}
    static void Send(TaxSimulation s,int[]? delays=null)=>s.SendFormation("line","character-001",new(8,1),new(8,5),delays??[1,3,5]);
    [Fact] public void DeliveryControlsEachUnitRatherThanTeleportingWholeArmy()
    {var s=Create();Send(s);Assert.Empty(s.MilitaryDiagnostic!.Applied);Step(s);Assert.Single(s.MilitaryDiagnostic!.Applied);Assert.Equal(new MapCell(1,3),s.MilitaryDiagnostic.Units[1].Cell);Step(s,2);Assert.Equal(2,s.MilitaryDiagnostic!.Applied.Length);Step(s,2);Assert.Equal(3,s.MilitaryDiagnostic!.Applied.Length);}
    [Fact] public void AllThreeEventuallyDeployAcrossSameBridgeWithoutOverlap()
    {var s=Create();Send(s);for(int t=0;t<70;t++){Step(s);var u=s.MilitaryDiagnostic!.Units;Assert.Equal(3,u.Select(x=>x.Cell).Distinct().Count());Assert.All(u,x=>Assert.True(MilitaryMap.Walkable(x.Cell)));}Assert.All(s.MilitaryDiagnostic!.Units,u=>Assert.Equal(new MapCell(8,1+int.Parse(u.Id[^1..])*2),u.Cell));Assert.Equal(3,s.MilitaryDiagnostic.FormationArrivals.Length);Assert.Equal(3,s.MilitaryViewFor("character-001").Units.Count(u=>u.Current));Assert.Empty(s.MilitaryDiagnostic.Losses);}
    [Fact] public void PlanReservesOtherSlotsSoEarlyArrivalDoesNotBlockTrailingUnit()
    {var s=Create();Send(s,[1,1,1]);Step(s,60);Assert.Equal(3,s.MilitaryDiagnostic!.FormationArrivals.Length);}
    [Theory] [InlineData(5,0,5,6)] [InlineData(-1,1,8,5)] [InlineData(8,1,8,2)]
    public void InvalidLineIsAtomic(int x,int z,int ex,int ez)
    {var s=Create();var before=s.SaveJson();Assert.ThrowsAny<ArgumentException>(()=>s.SendFormation("bad","character-001",new(x,z),new(ex,ez),[1,3,5]));Assert.Equal(before,s.SaveJson());}
    [Fact] public void UnauthorizedCommanderCannotIssueArmyPlan()
    {var s=Create();var before=s.SaveJson();Assert.Throws<UnauthorizedAccessException>(()=>s.SendFormation("bad","character-002",new(8,1),new(8,5),[1,3,5]));Assert.Equal(before,s.SaveJson());}
    [Fact] public void RevokedOneUnitGrantRejectsWholePlanWithoutPartialMessages()
    {var s=Create();s.RevokeMilitaryGrant("formation-grant-2");var before=s.SaveJson();Assert.Throws<UnauthorizedAccessException>(()=>Send(s));Assert.Equal(before,s.SaveJson());}
    [Fact] public void RepeatedPlanIdIsIdempotentAndChangedPayloadIsRejected()
    {var s=Create();Send(s);var before=s.SaveJson();Send(s);Assert.Equal(before,s.SaveJson());Assert.Throws<InvalidOperationException>(()=>Send(s,[2,3,5]));Assert.Equal(before,s.SaveJson());}
    [Fact] public void RefusingCommanderDoesNotStopOthersAndOnlyReplyRevealsRefusal()
    {var s=Create();s.SetMilitaryWillingness("formation-2",false);Send(s);Step(s,5);Assert.DoesNotContain(s.MilitaryViewFor("character-001").Orders,o=>o.KnownStatus.StartsWith("Refused"));Step(s);Assert.Contains(s.MilitaryViewFor("character-001").Orders,o=>o.KnownStatus.StartsWith("Refused"));Step(s,60);Assert.Equal(new MapCell(1,5),s.MilitaryDiagnostic!.Units[2].Cell);Assert.Equal(2,s.MilitaryDiagnostic.FormationArrivals.Length);}
    [Fact] public void LossRetryAndLateOriginalCannotDuplicateDeployment()
    {var s=Create();Send(s);var id="line/unit/2";s.FailMessage(MilitaryFlow.OrderMessage(id));Step(s,5);s.ReportMessageFailure(MilitaryFlow.OrderMessage(id)+"/attempt/1","failed");Step(s);s.RetryMilitary(id,"character-001");s.ScheduleLateMessage(MilitaryFlow.OrderMessage(id),1,1);Step(s,70);Assert.Equal(1,s.MilitaryDiagnostic!.Applied.Count(x=>x==id));Assert.Equal(3,s.MilitaryDiagnostic.FormationArrivals.Length);}
    [Fact] public void SaveEveryTickPreservesPlanRoutesMessagesAndArrivalReports()
    {var s=Create();Send(s);for(int t=0;t<60;t++){var loaded=TaxSimulation.LoadJson(s.SaveJson());Step(s);Step(loaded);Assert.Equal(s.SaveJson(),loaded.SaveJson());}}
    [Fact] public void CancelBeforeDeliveryPreventsOneSlotWithoutReversingOthers()
    {var s=Create();Send(s);s.SendMilitary("cancel","character-001","formation-2",MilitaryAction.Cancel,new(8,5),cancels:"line/unit/2");Step(s,60);Assert.Equal(new MapCell(1,5),s.MilitaryDiagnostic!.Units[2].Cell);Assert.Equal(2,s.MilitaryDiagnostic.FormationArrivals.Length);}
    [Fact] public void TamperedSavedLineCannotChangeImmutableDeliveredDestination()
    {var s=Create();Send(s);var json=JsonNode.Parse(s.SaveJson())!;json["Officers"]!["Military"]!["FormationPlans"]![0]!["End"]!["Z"]=4;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(json.ToJsonString()));}
    [Fact] public void OldSaveWithoutNewFormationFieldsStillLoads()
    {var s=Create();var json=JsonNode.Parse(s.SaveJson())!;json["Officers"]!["Military"]!.AsObject().Remove("FormationPlans");json["Officers"]!["Military"]!.AsObject().Remove("FormationArrivals");Assert.True(TaxSimulation.LoadJson(json.ToJsonString()).HasFormationMap);}
}
