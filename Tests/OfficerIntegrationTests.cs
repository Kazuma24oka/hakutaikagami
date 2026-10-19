using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Simulation;
using HakutaiKagami.Core.World;
using System.Text.Json.Nodes;
using Xunit;
namespace HakutaiKagami.Tests;
public class OfficerIntegrationTests
{
    static TaxSimulation Create(bool battle=true)
    {
        var s=TaxSimulation.Create(PrototypeScenario.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"prototype.json"))));
        s.EnableOfficerIntegration();if(battle){s.StartFieldFixture();s.DispatchField("engage-red","character-001","unit-red",FieldAction.Engage);s.DispatchField("engage-blue","character-002","unit-blue",FieldAction.Engage);}return s;
    }
    static void Advance(TaxSimulation s,int n){for(int i=0;i<n;i++)s.AdvanceOneTick();}
    [Fact] public void AppearanceAssignmentPreservesCoreAndCopiesInputs()
    {
        var s=Create(false);var before=s.RestoreWorld();var a=AppearanceCatalog.Default(1);s.AssignAppearance("character-003",a);a.SkinLinear[0]=0;
        Assert.NotEqual(0,s.OfficerState!.Looks["character-003"].SkinLinear[0]);Assert.Equal(before.Tick,s.Tick);
        Assert.Equal(before.Random.State,s.RestoreWorld().Random.State);Assert.Equal(before.Characters["character-003"].Role,s.RestoreWorld().Characters["character-003"].Role);
        var copy=s.OfficerState!;copy.Looks.Clear();Assert.Equal(3,s.OfficerState!.Looks.Count);
    }
    [Fact] public void OldSaveOptInGetsDeterministicDefaultsWithoutBattleOrTimeChange()
    {
        var s=Create(false);var node=JsonNode.Parse(s.SaveJson())!;node.AsObject().Remove("Officers");var old=TaxSimulation.LoadJson(node.ToJsonString());Assert.Null(old.OfficerState);
        old.EnableOfficerIntegration();Assert.Equal(s.SaveJson(),old.SaveJson());old.EnableOfficerIntegration();Assert.Equal(s.SaveJson(),old.SaveJson());
    }
    [Theory] [InlineData("Face","missing")] [InlineData("Hair","unknown")] [InlineData("Body","other")] [InlineData("Model","../asset")]
    public void UnknownAppearanceRejectsWholeSnapshot(string field,string value)
    {
        var s=Create();string saved=s.SaveJson();var n=JsonNode.Parse(saved)!;n["Officers"]!["Looks"]!["character-001"]![field]=value;
        Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));Assert.Equal(saved,s.SaveJson());
    }
    [Fact] public void InvalidBindingAndColorAreRejectedAtomically()
    {
        var s=Create();string saved=s.SaveJson();var a=AppearanceCatalog.Default(1);a.SkinLinear=[float.NaN,0,0];Assert.ThrowsAny<Exception>(()=>s.AssignAppearance("character-001",a));
        Assert.ThrowsAny<Exception>(()=>s.AssignAppearance("unknown",AppearanceCatalog.Default(0)));Assert.Equal(saved,s.SaveJson());
        var n=JsonNode.Parse(saved)!;n["Officers"]!["Looks"]!.AsObject().Remove("character-002");Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Fact] public void OrdersTravelAndContactDoesNotImmediatelyDamage()
    {
        var s=Create();Assert.All(s.OfficerState!.Battle!.Units,u=>Assert.Equal(FieldPhase.Standing,u.Phase));Assert.All(s.OfficerState.Battle.Directives,d=>Assert.False(d.Delivered));
        s.AdvanceOneTick();Assert.All(s.OfficerState!.Battle!.Directives,d=>Assert.True(d.Delivered));Assert.Empty(s.OfficerState.Battle.Losses);
        for(int i=0;i<10&&s.OfficerState!.Battle!.Units[0].Phase!=FieldPhase.Contact;i++)s.AdvanceOneTick();
        Assert.All(s.OfficerState!.Battle!.Units,u=>Assert.Equal(FieldPhase.Contact,u.Phase));Assert.Empty(s.OfficerState.Battle.Losses);
        s.AdvanceOneTick();Assert.Equal(2,s.OfficerState!.Battle!.Losses.Length);Assert.Equal(92,s.OfficerState.Battle.Units[0].Soldiers);Assert.Equal(48,s.OfficerState.Battle.Units[1].Soldiers);
    }
    [Fact] public void BattleLossesRouteThenExitWithoutDeletingCommanderOrForce()
    {
        var s=Create();Advance(s,10);var b=s.OfficerState!.Battle!;Assert.Contains(b.Units,u=>u.Phase==FieldPhase.Routing);Assert.Equal(6,b.Losses.Length);
        Advance(s,12);b=s.OfficerState!.Battle!;Assert.Equal(FieldPhase.Exited,b.Units[1].Phase);Assert.Equal(24,b.Units[1].Soldiers);Assert.Equal(76,b.Units[0].Soldiers);
        Assert.Equal(3,s.RestoreWorld().Characters.Count);Assert.Equal(2,b.Units.Length);Assert.All(b.Units,u=>Assert.Equal(u.InitialSoldiers,u.Soldiers+b.Losses.Where(l=>l.UnitId==u.Id).Sum(l=>l.Soldiers)));
    }
    [Fact] public void WithdrawalArrivalAndOrderRetryHaveOneEffect()
    {
        var s=Create();s.DispatchField("leave","character-001","unit-red",FieldAction.Withdraw,2);string saved=s.SaveJson();s.DispatchField("leave","character-001","unit-red",FieldAction.Withdraw,2);Assert.Equal(saved,s.SaveJson());
        s.AdvanceOneTick();Assert.Equal(FieldPhase.Marching,s.OfficerState!.Battle!.Units[0].Phase);s.AdvanceOneTick();Assert.Equal(FieldPhase.Withdrawing,s.OfficerState!.Battle!.Units[0].Phase);Assert.Empty(s.OfficerState.Battle.Losses);
        Assert.ThrowsAny<Exception>(()=>s.DispatchField("leave","character-001","unit-red",FieldAction.Engage,2));
    }
    [Fact] public void WrongAuthorityAndConflictingOrdersDoNotChangeState()
    {
        var s=Create();string saved=s.SaveJson();Assert.ThrowsAny<Exception>(()=>s.DispatchField("bad","character-003","unit-red",FieldAction.Withdraw));
        Assert.ThrowsAny<Exception>(()=>s.DispatchField("conflict","character-001","unit-red",FieldAction.Withdraw));Assert.Equal(saved,s.SaveJson());
    }
    [Fact] public void SharedPauseStopsBattleDeliveryAndTaxAndFailedTickRollsBackAll()
    {
        var s=Create();s.Submit("tax-field","character-001","castle-001",60);s.SetPaused(true);string paused=s.SaveJson();Advance(s,3);Assert.Equal(paused,s.SaveJson());
        s.SetPaused(false);string saved=s.SaveJson();Assert.Throws<IOException>(()=>s.AdvanceOneTick(()=>throw new IOException("injected")));Assert.Equal(saved,s.SaveJson());
        Advance(s,4);Assert.Equal(4,s.Tick);Assert.Equal(s.Tick,s.OfficerState!.Battle!.Tick);Assert.Equal(160,s.TreasuryRice);Assert.True(s.Orders.Single().Reported);
    }
    [Fact] public void RestartEveryTickEqualsUninterruptedIncludingRngAndEffects()
    {
        var a=Create();a.Submit("tax-field","character-001","castle-001",60);var b=TaxSimulation.LoadJson(a.SaveJson());
        for(int i=0;i<25;i++){a.AdvanceOneTick();b=TaxSimulation.LoadJson(b.SaveJson());b.AdvanceOneTick();Assert.Equal(a.SaveJson(),b.SaveJson());}
    }
    [Theory] [InlineData("Tick",999)] [InlineData("Schema",99)]
    public void InvalidBattleVersionOrClockRejected(string field,int value)
    {
        var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Battle"]![field]=value;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Fact] public void TamperedLossLedgerRejected()
    {
        var s=Create();Advance(s,8);var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Battle"]!["Units"]![0]!["Soldiers"]=100;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void IntegrationCannotMasqueradeAsOlderVersion(int version)
    {
        var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Version"]=version;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Theory] [InlineData("Morale",100)] [InlineData("Travel",-1)] [InlineData("ActionAt",999)]
    public void InvalidBattleQuantitiesRejected(string field,int value)
    {
        var s=Create();Advance(s,8);var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Battle"]!["Units"]![0]![field]=value;Assert.ThrowsAny<Exception>(()=>TaxSimulation.LoadJson(n.ToJsonString()));
    }
    [Fact] public void SameTimeDamageIsIndependentOfUnitRegistrationOrder()
    {
        var a=Create();Advance(a,5);var n=JsonNode.Parse(a.SaveJson())!;var units=n["Officers"]!["Battle"]!["Units"]!.AsArray();var reversed=units.Select(u=>u!.DeepClone()).Reverse().ToArray();units.Clear();foreach(var u in reversed)units.Add(u);
        var b=TaxSimulation.LoadJson(n.ToJsonString());a.AdvanceOneTick();b.AdvanceOneTick();
        Assert.Equal(a.OfficerState!.Battle!.Units.OrderBy(u=>u.Id),b.OfficerState!.Battle!.Units.OrderBy(u=>u.Id));
    }
    [Fact] public void BatchFixtureOrdersRollbackIfEitherInputConflicts()
    {
        var s=Create(false);s.StartFieldFixture();s.DispatchField("fixture-engage-blue","character-002","unit-blue",FieldAction.Withdraw);string saved=s.SaveJson();
        Assert.ThrowsAny<Exception>(()=>s.EngageFieldFixture());Assert.Equal(saved,s.SaveJson());
    }
    [Fact] public void ArrivalRechecksCommandAuthoritySeparatelyFromDelivery()
    {
        var s=Create();s.RevokeFieldGrant("character-002");s.AdvanceOneTick();var order=s.OfficerState!.Battle!.Directives.Single(d=>d.UnitId=="unit-blue");
        Assert.True(order.Delivered);Assert.False(order.Accepted);Assert.Equal(FieldPhase.Standing,s.OfficerState.Battle.Units[1].Phase);
        string saved=s.SaveJson();Assert.Throws<UnauthorizedAccessException>(()=>s.DispatchField("retry-revoked","character-002","unit-blue",FieldAction.Engage));Assert.Equal(saved,s.SaveJson());
    }
    [Fact] public void ExclusiveGrantExpiryAtArrivalPreventsEffect()
    {
        var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Battle"]!["Grants"]![1]!["ValidUntil"]=1;var b=TaxSimulation.LoadJson(n.ToJsonString());b.AdvanceOneTick();
        Assert.False(b.OfficerState!.Battle!.Directives.Single(d=>d.UnitId=="unit-blue").Accepted);
    }
    [Fact] public void ValidAuthorityAndArrivalDoNotForceUnwillingRecipientToFight()
    {
        var s=Create();var n=JsonNode.Parse(s.SaveJson())!;n["Officers"]!["Battle"]!["Units"]![1]!["FixtureWilling"]=false;var b=TaxSimulation.LoadJson(n.ToJsonString());b.AdvanceOneTick();
        var d=b.OfficerState!.Battle!.Directives.Single(d=>d.UnitId=="unit-blue");Assert.True(d.Delivered);Assert.False(d.Accepted);Assert.Equal("Refused",d.Resolution);
        Advance(b,10);Assert.Empty(b.OfficerState!.Battle!.Losses);Assert.Equal(FieldPhase.Standing,b.OfficerState.Battle.Units[1].Phase);
    }
}
